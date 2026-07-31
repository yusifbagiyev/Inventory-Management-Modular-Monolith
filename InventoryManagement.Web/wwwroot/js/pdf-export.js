// XSS guard: these exporters read rendered values back out with textContent (plain text) and
// re-inject them through innerHTML / document.write, which would turn any HTML inside that
// text into live markup. Everything derived from textContent must go through this first.
function escapePdfText(value) {
    return String(value ?? '').replace(/[&<>"']/g, c => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c]));
}

/**
 * Reads a rendered list table into plain data.
 * Driven by header NAMES, not column indexes - the old exporters hard-coded positions like
 * "4th column = Location" and silently produced blank columns whenever a table changed.
 * Only text is collected, so images never reach the PDF.
 */
function collectTableData(table, excludeHeaders = []) {
    const skip = excludeHeaders.map(h => h.toLowerCase());
    const allHeaders = Array.from(table.querySelectorAll('thead th')).map(th => th.textContent.trim());
    const keep = allHeaders
        .map((name, index) => ({ name, index }))
        .filter(col => col.name && !skip.includes(col.name.toLowerCase()));

    const rows = Array.from(table.querySelectorAll('tbody tr'))
        .filter(tr => tr.offsetParent !== null || tr.style.display !== 'none')
        .map(tr => {
            const cells = Array.from(tr.children);
            return keep.map(col => readCellText(cells[col.index]));
        })
        .filter(cells => cells.some(v => v !== ''));

    return { headers: keep.map(c => c.name), rows };
}

/** Joins a cell's distinct text blocks so "Surface" + "Microsoft" does not become "SurfaceMicrosoft". */
function readCellText(td) {
    if (!td) return '';
    const parts = [];
    const add = value => {
        const text = String(value || '').replace(/\s+/g, ' ').trim();
        if (text && !parts.includes(text)) parts.push(text);
    };

    const blocks = td.querySelectorAll('.cell-title, .cell-sub, .state, .badge');
    if (blocks.length) {
        blocks.forEach(b => { if (!b.closest('.pdf-omit')) add(b.textContent); });
    } else {
        // Placeholder spans (e.g. "Unassigned", an empty "-") are marked .pdf-omit so the
        // exported value prints blank instead of a filler word.
        const clone = td.cloneNode(true);
        clone.querySelectorAll('.pdf-omit').forEach(el => el.remove());
        add(clone.textContent);
    }
    return parts.join(' - ');
}

/**
 * Renders collected data as a clean, printable document and opens the print dialog.
 * Prints from a hidden same-page iframe rather than a popup window: a popup gets blocked by
 * default, steals focus, and on returning to the list left the page unresponsive until a
 * reload (selects stopped opening) - the iframe has none of those side effects.
 */
function renderPrintDocument({ title, headers, rows, filters }) {
    const printed = new Date().toLocaleString();
    const filterLine = filters ? `<div class="filters">${escapePdfText(filters)}</div>` : '';

    const thead = headers.map(h => `<th>${escapePdfText(h)}</th>`).join('');
    const tbody = rows.map(cells =>
        `<tr>${cells.map(c => `<td>${escapePdfText(c) || '<span class="empty">-</span>'}</td>`).join('')}</tr>`
    ).join('');

    const frame = document.createElement('iframe');
    frame.setAttribute('aria-hidden', 'true');
    frame.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0;visibility:hidden;';
    document.body.appendChild(frame);

    const printWindow = frame.contentWindow;
    printWindow.document.open();
    printWindow.document.write(`<!DOCTYPE html><html><head><meta charset="utf-8"><title>${escapePdfText(title)}</title>
<style>
  @page { size: A4 landscape; margin: 10mm 8mm; }
  * { box-sizing: border-box; }
  body { font-family: "Segoe UI", Roboto, Arial, sans-serif; color: #14232B; margin: 0; font-size: 9pt; }
  header { display: flex; justify-content: space-between; align-items: flex-end;
           border-bottom: 2px solid #0E9BC4; padding-bottom: 6px; margin-bottom: 4px; }
  h1 { font-size: 15pt; margin: 0; font-weight: 650; letter-spacing: -.2px; }
  .meta { text-align: right; font-size: 8pt; color: #5A6E7A; line-height: 1.5; }
  .filters { font-size: 8pt; color: #445966; background: #EEF5F9; border-radius: 3px;
             padding: 4px 7px; margin-bottom: 8px; }
  table { width: 100%; border-collapse: collapse; table-layout: auto; }
  thead { display: table-header-group; }
  th { background: #E7EFF5; text-align: left; font-size: 7.5pt; text-transform: uppercase;
       letter-spacing: .04em; color: #445966; padding: 5px 6px; border-bottom: 1.2px solid #C2D4E0;
       white-space: nowrap; }
  td { padding: 4px 6px; border-bottom: .8px solid #E1EAF1; vertical-align: top;
       word-break: break-word; }
  tbody tr { page-break-inside: avoid; }
  tbody tr:nth-child(even) td { background: #F6F9FB; }
  .empty { color: #9AACB8; }
  footer { margin-top: 8px; font-size: 7.5pt; color: #7C8F9B; text-align: right; }
  @media print { body { -webkit-print-color-adjust: exact; print-color-adjust: exact; } }
</style></head><body>
<header>
  <h1>${escapePdfText(title)}</h1>
  <div class="meta"><div>Printed: ${escapePdfText(printed)}</div><div>${rows.length} record(s)</div></div>
</header>
${filterLine}
<table><thead><tr>${thead}</tr></thead><tbody>${tbody}</tbody></table>
<footer>Inventory Pro</footer>
</body></html>`);

    printWindow.document.close();

    // Give the iframe a tick to lay out, print, then always clean up - even if the user
    // cancels the dialog - so no stray node is left behind on the page.
    const cleanup = () => { if (frame.parentNode) frame.parentNode.removeChild(frame); };
    setTimeout(() => {
        try {
            printWindow.focus();
            printWindow.print();
        } catch (e) {
            console.error('Print failed', e);
        }
        setTimeout(cleanup, 1000);
    }, 250);
}

/**
 * Prints an HTML document from a hidden same-page iframe.
 * Shared by the older exporters that still build their own markup. Replaces window.open:
 * popups get blocked, steal focus, and left the list page unresponsive on return.
 */
function openPrintFrame(html) {
    const frame = document.createElement('iframe');
    frame.setAttribute('aria-hidden', 'true');
    frame.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0;visibility:hidden;';
    document.body.appendChild(frame);

    const win = frame.contentWindow;
    win.document.open();
    win.document.write(html);
    win.document.close();

    setTimeout(() => {
        try {
            win.focus();
            win.print();
        } catch (e) {
            console.error('Print failed', e);
        }
        setTimeout(() => { if (frame.parentNode) frame.parentNode.removeChild(frame); }, 1000);
    }, 350);
}

/** Reads the applied-filter chips above a list so the export states what it was filtered by. */
function currentFilterSummary() {
    const chips = Array.from(document.querySelectorAll('.filter-applied .achip'))
        .map(c => c.textContent.replace(/\s*×\s*$/, '').replace(/\s+/g, ' ').trim())
        .filter(Boolean);
    return chips.length ? 'Filters: ' + chips.join('   |   ') : '';
}

function exportProductsToPDF() {
    const table = document.getElementById('productsTable');
    if (!table) {
        showToast('Products table not found', 'error');
        return;
    }

    const { headers, rows } = collectTableData(table, ['Actions']);
    if (!rows.length) {
        showToast('Nothing to export', 'warning');
        return;
    }
    renderPrintDocument({ title: 'Products', headers, rows, filters: currentFilterSummary() });
}


function exportRoutesToPDF() {
    const routesTable = document.getElementById('routesTable');
    if (!routesTable) {
        showToast('Routes table not found', 'error');
        return;
    }

    const data = collectTableData(routesTable, ['Actions']);
    if (!data.rows.length) {
        showToast('Nothing to export', 'warning');
        return;
    }
    renderPrintDocument({ title: 'Routes', headers: data.headers, rows: data.rows, filters: currentFilterSummary() });
}


function exportToPDF(tableHTML, filename, title, customStyles = '') {
    // Modern UI styles for portrait mode
    const styles = `
        <style>
            @page { 
                size: portrait; 
                margin: 0.7cm;
            }
            body { 
                font-family: 'Segoe UI', 'Roboto', 'Helvetica Neue', Arial, sans-serif;
                font-size: 10.5pt;
                color: #333;
                line-height: 1.35;
            }
            .container {
                max-width: 100%;
                padding: 0;
            }
            .header {
                text-align: center;
                margin-bottom: 12px;
                padding-bottom: 8px;
                border-bottom: 1px solid #e0e0e0;
            }
            h1 { 
                font-size: 18pt;
                margin: 0 0 5px 0;
                color: #1e40af;
                font-weight: 600;
            }
            .subheader {
                display: flex;
                justify-content: space-between;
                margin-top: 3px;
                font-size: 9.5pt;
                color: #6b7280;
            }
            table { 
                width: 100%; 
                border-collapse: collapse;
                margin-top: 12px;
                table-layout: fixed; /* Use fixed table layout */
            }
            th, td { 
                padding: 6px 5px; 
                text-align: left; 
                vertical-align: top;
                word-wrap: break-word;
            }
            th { 
                background-color: #3b82f6; 
                color: white;
                font-weight: 600;
                font-size: 10.5pt;
                text-transform: uppercase;
                letter-spacing: 0.3px;
                border: 1px solid #2563eb;
            }
            td {
                border: 1px solid #e2e8f0;
                font-size: 10pt;
            }
            .badge {
                display: inline-block;
                padding: 3px 7px;
                border-radius: 4px;
                font-size: 9.5pt;
                font-weight: 600;
                margin: 2px 0;
                line-height: 1.3;
            }
            .bg-success { background-color: #10b981; }
            .bg-danger { background-color: #ef4444; }
            .bg-info { background-color: #3b82f6; }
            .bg-warning { background-color: #f59e0b; }
            .bg-secondary { background-color: #64748b; }
            
            .footer {
                margin-top: 15px;
                text-align: center;
                font-size: 9pt;
                color: #6b7280;
                padding-top: 8px;
            }
            
            ${customStyles} /* Insert the custom styles here */
            
            @media print {
                body { margin: 0; }
                .no-print { display: none; }
            }
        </style>
    `;

    const generatedDate = new Date().toLocaleString('en-US', {
        timeZone: 'Asia/Baku',
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });

    // Count rows
    const tempDiv = document.createElement('div');
    tempDiv.innerHTML = tableHTML;
    const rowCount = tempDiv.querySelectorAll('tbody tr').length;

    // Build the document
    const documentContent = `
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="UTF-8">
            <title>${title}</title>
            ${styles}
        </head>
        <body>
            <div class="container">
                <div class="header">
                    <h1>${title}</h1>
                    <div class="subheader">
                        <span>Generated: ${generatedDate}</span>
                        <span>Total Records: ${rowCount}</span>
                    </div>
                </div>
                ${tableHTML}
                <div class="footer">
                    Inventory Management System | ${generatedDate}
                </div>
            </div>
        </body>
        </html>
    `;

    openPrintFrame(documentContent);
}

function exportTimelineToPDF() {
    const timeline = document.querySelector('.timeline');
    if (!timeline) {
        showToast('Timeline not found', 'error');
        return;
    }

    // Clone the timeline to modify it
    const timelineClone = timeline.cloneNode(true);

    // Remove images
    timelineClone.querySelectorAll('img').forEach(img => img.remove());

    // Remove action buttons
    timelineClone.querySelectorAll('.btn').forEach(btn => btn.remove());

    // Simplify timeline items
    timelineClone.querySelectorAll('.timeline-item').forEach(item => {
        const marker = item.querySelector('.timeline-marker');
        const content = item.querySelector('.timeline-content');

        // Create simplified HTML
        item.innerHTML = `
            <div style="display: flex; margin-bottom: 15px;">
                ${marker.outerHTML}
                <div style="flex: 1; margin-left: 15px; border-left: 2px solid #e0e0e0; padding-left: 15px;">
                    ${content.innerHTML}
                </div>
            </div>
        `;
    });

    // Generate HTML for PDF
    const htmlContent = `
        <div style="font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;">
            <h1 style="text-align: center; color: #1e40af; margin-bottom: 10px;">
                Transfer Timeline Report
            </h1>
            <div style="text-align: center; color: #6b7280; margin-bottom: 20px; border-bottom: 1px solid #eee; padding-bottom: 15px;">
                <div>Generated on: ${new Date().toLocaleString('en-US', {
        timeZone: 'Asia/Baku',
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    })}</div>
                <div>Total Transfers: ${timelineClone.querySelectorAll('.timeline-item').length}</div>
            </div>
            ${timelineClone.outerHTML}
        </div>
    `;

    openPrintFrame(`
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="UTF-8">
            <title>Transfer Timeline Report</title>
            <style>
                @page { 
                    size: portrait; 
                    margin: 1cm;
                }
                body { 
                    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                    font-size: 10pt;
                    color: #333;
                    line-height: 1.4;
                }
                .timeline-item {
                    margin-bottom: 15px;
                }
                .timeline-marker {
                    width: 20px;
                    height: 20px;
                    border-radius: 50%;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    margin-top: 5px;
                }
                .fa-check-circle { color: #28a745; }
                .fa-clock { color: #ffc107; }
                .timeline-content {
                    background: #f8f9fa;
                    padding: 10px;
                    border-radius: 5px;
                    border: 1px solid #e0e0e0;
                }
                h6 {
                    font-size: 11pt;
                    margin: 0 0 5px 0;
                    display: flex;
                    justify-content: space-between;
                }
                .badge {
                    display: inline-block;
                    padding: 3px 8px;
                    border-radius: 4px;
                    font-weight: 600;
                    margin: 2px 0;
                }
                .bg-success { background-color: #28a745; color: white; }
                .bg-warning { background-color: #ffc107; color: black; }
            </style>
        </head>
        <body>
            ${htmlContent}
        </body>
        </html>
    `);
}

function exportDepartmentsToPDF() {
    const table = document.querySelector('.table');
    if (!table) {
        showToast('Departments table not found', 'error');
        return;
    }

    // Clone the table to modify it
    const tableClone = table.cloneNode(true);
    tableClone.id = 'departmentsPdfTable';

    // Remove actions column (last column)
    const headers = tableClone.querySelectorAll('th');
    headers[headers.length - 1].remove();  // Remove Actions header

    tableClone.querySelectorAll('tr').forEach(row => {
        const cells = row.querySelectorAll('td');
        if (cells.length > 0) {
            cells[cells.length - 1].remove();  // Remove Actions cell
        }
    });

    // Clean up Department column (first column) - remove icon
    tableClone.querySelectorAll('td:first-child').forEach(cell => {
        const textDiv = cell.querySelector('.fw-semibold');
        if (textDiv) {
            cell.innerHTML = `<strong>${escapePdfText(textDiv.textContent)}</strong>`;
        }
    });

    // Clean up Department Head column (second column) - simplify text
    tableClone.querySelectorAll('td:nth-child(2)').forEach(cell => {
        const text = cell.textContent.trim();
        if (text === 'Not assigned') {
            cell.innerHTML = '<span style="color: #999; font-style: italic;">Not assigned</span>';
        } else {
            cell.textContent = text;
        }
    });

    // Clean up Description column (third column) - simplify text
    tableClone.querySelectorAll('td:nth-child(3)').forEach(cell => {
        const text = cell.textContent.trim();
        if (text === 'No description provided') {
            cell.innerHTML = '<span style="color: #999; font-style: italic;">None</span>';
        } else {
            // Keep the description as is, but remove any extra whitespace
            cell.textContent = text;
        }
    });

    // Remove icons from status column
    tableClone.querySelectorAll('.badge i').forEach(icon => icon.remove());

    // Set column widths for proper PDF layout
    const customStyles = `
        #departmentsPdfTable {
            table-layout: fixed;
            width: 100%;
        }
        #departmentsPdfTable th:nth-child(1) { width: 18%; }  /* Department */
        #departmentsPdfTable th:nth-child(2) { width: 15%; }  /* Department Head */
        #departmentsPdfTable th:nth-child(3) { width: 22%; }  /* Description */
        #departmentsPdfTable th:nth-child(4) { width: 12%; }  /* Products */
        #departmentsPdfTable th:nth-child(5) { width: 12%; }  /* Workers */
        #departmentsPdfTable th:nth-child(6) { width: 12%; }  /* Status */
        #departmentsPdfTable th:nth-child(7) { width: 12%; }  /* Created */
    `;

    exportToPDF(tableClone.outerHTML, 'departments_export.pdf', 'Departments Report', customStyles);
}

function exportCategoriesToPDF() {
    const table = document.querySelector('.table');
    if (!table) {
        showToast('Categories table not found', 'error');
        return;
    }

    // Clone the table to modify it
    const tableClone = table.cloneNode(true);
    tableClone.id = 'categoriesPdfTable';

    // Remove actions column
    const headers = tableClone.querySelectorAll('th');
    headers[headers.length - 1].remove();  // Remove Actions header

    tableClone.querySelectorAll('tr').forEach(row => {
        const cells = row.querySelectorAll('td');
        if (cells.length > 0) {
            cells[cells.length - 1].remove();  // Remove Actions cell
        }
    });

    // Clean up content
    tableClone.querySelectorAll('td:first-child').forEach(cell => {
        const textDiv = cell.querySelector('div:last-child');
        if (textDiv) {
            cell.innerHTML = textDiv.outerHTML;
        }
    });

    // Remove icons from status column
    tableClone.querySelectorAll('.badge i').forEach(icon => icon.remove());

    // Set column widths
    const customStyles = `
        #categoriesPdfTable {
            table-layout: fixed;
            width: 100%;
        }
        #categoriesPdfTable th:nth-child(1) { width: 25%; }  /* Category */
        #categoriesPdfTable th:nth-child(2) { width: 30%; }  /* Description */
        #categoriesPdfTable th:nth-child(3) { width: 15%; }  /* Products */
        #categoriesPdfTable th:nth-child(4) { width: 15%; }  /* Status */
        #categoriesPdfTable th:nth-child(5) { width: 15%; }  /* Created */
    `;

    exportToPDF(tableClone.outerHTML, 'categories_export.pdf', 'Categories Report', customStyles);
}