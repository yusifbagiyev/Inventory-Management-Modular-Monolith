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
    // Headers are rendered in the interface language: match the English name and its translation.
    // The actions column is also recognised by its class, whatever its caption says.
    const skip = excludeHeaders.flatMap(h => [h, t(h)]).map(h => h.toLowerCase());
    const allHeaders = Array.from(table.querySelectorAll('thead th'));
    const keep = allHeaders
        .map((th, index) => ({ name: th.textContent.trim(), index, th }))
        .filter(col => col.name
            && !col.th.classList.contains('actions-column')
            && !skip.includes(col.name.toLowerCase()));

    const rows = Array.from(table.querySelectorAll('tbody tr'))
        .filter(tr => tr.style.display !== 'none')
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

    // Placeholder spans (e.g. "Unassigned", "No description") are marked .pdf-omit so the
    // exported value prints blank instead of a filler word.
    const textWithoutPlaceholders = el => {
        const clone = el.cloneNode(true);
        clone.querySelectorAll('.pdf-omit').forEach(p => p.remove());
        return clone.textContent;
    };

    const blocks = td.querySelectorAll('.cell-title, .cell-sub, .state, .badge');
    if (blocks.length) {
        blocks.forEach(b => { if (!b.closest('.pdf-omit')) add(textWithoutPlaceholders(b)); });
    } else {
        add(textWithoutPlaceholders(td));
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
    title = t(title);
    const printed = formatDate(new Date(), true);
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
    printWindow.document.write(`<!DOCTYPE html><html lang="${document.documentElement.lang || 'en'}"><head><meta charset="utf-8"><title>${escapePdfText(title)}</title>
<style>
  @page { size: A4 landscape; margin: 10mm 8mm; }
  * { box-sizing: border-box; }
  body { font-family: "Segoe UI", Roboto, Arial, sans-serif; color: #1B1C1E; margin: 0; font-size: 9pt; }
  header { display: flex; justify-content: space-between; align-items: flex-end;
           border-bottom: 2px solid #1B1C1E; padding-bottom: 6px; margin-bottom: 4px; }
  h1 { font-size: 15pt; margin: 0; font-weight: 650; letter-spacing: -.2px; }
  .meta { text-align: right; font-size: 8pt; color: #4F5358; line-height: 1.5; }
  .filters { font-size: 8pt; color: #4F5358; background: #EFEFEC; border-radius: 3px;
             padding: 4px 7px; margin-bottom: 8px; }
  table { width: 100%; border-collapse: collapse; table-layout: auto; }
  thead { display: table-header-group; }
  th { background: #EFEFEC; text-align: left; font-size: 7.5pt; text-transform: uppercase;
       letter-spacing: .04em; color: #4F5358; padding: 5px 6px; border-bottom: 1.2px solid #CFCFCB;
       white-space: nowrap; }
  td { padding: 4px 6px; border-bottom: .8px solid #E4E4E1; vertical-align: top;
       word-break: break-word; }
  tbody tr { page-break-inside: avoid; }
  tbody tr:nth-child(even) td { background: #FAFAF9; }
  .empty { color: #6B6F75; }
  footer { margin-top: 8px; font-size: 7.5pt; color: #6B6F75; text-align: right; }
  @media print { body { -webkit-print-color-adjust: exact; print-color-adjust: exact; } }
</style></head><body>
<header>
  <h1>${escapePdfText(title)}</h1>
  <div class="meta"><div>${escapePdfText(t('Printed: {0}', printed))}</div><div>${escapePdfText(t('{0} record(s)', rows.length))}</div></div>
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
 * Used by the timeline export, which prints its own markup rather than a table. Replaces window.open:
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
    return chips.length ? t('Filters:') + ' ' + chips.join('   |   ') : '';
}

/**
 * The same list with every row that matches the current filters: the page on screen holds one
 * page (20-30 rows), so the list is re-read from the server as a single page. Falls back to the
 * rows on screen if that fails.
 */
async function loadWholeList(table) {
    const key = table.id || table.querySelector('tbody[id]')?.id;
    if (!key) return table;
    try {
        const url = new URL(window.location.href);
        url.searchParams.set('pageNumber', '1');
        url.searchParams.set('pageSize', '100000');
        const response = await fetch(url, { credentials: 'same-origin' });
        if (!response.ok) return table;
        const doc = new DOMParser().parseFromString(await response.text(), 'text/html');
        const found = doc.getElementById(key);
        return (found && (found.tagName === 'TABLE' ? found : found.closest('table'))) || table;
    } catch (e) {
        console.error('Could not load the whole list for export', e);
        return table;
    }
}

/** Exports one list table, columns picked by header name; `title` also names the toast target. */
async function exportListTable(table, title) {
    title = t(title);
    if (!table) {
        showToast(t('{0} table not found', title), 'error');
        return;
    }

    table = await loadWholeList(table);
    const { headers, rows } = collectTableData(table, ['Actions']);
    if (!rows.length) {
        showToast(t('Nothing to export'), 'warning');
        return;
    }
    renderPrintDocument({ title, headers, rows, filters: currentFilterSummary() });
}

function exportProductsToPDF() {
    exportListTable(document.getElementById('productsTable'), 'Products');
}

function exportRoutesToPDF() {
    exportListTable(document.getElementById('routesTable'), 'Routes');
}

function exportTimelineToPDF() {
    const timeline = document.querySelector('.timeline');
    if (!timeline) {
        showToast(t('Timeline not found'), 'error');
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
            <h1 style="text-align: center; color: #1B1C1E; margin-bottom: 4px;">
                ${escapePdfText(t('Transfer Timeline Report'))}
            </h1>
            ${timeline.dataset.product ? `<div style="text-align: center; font-size: 12pt; font-weight: 600; margin-bottom: 10px;">${escapePdfText(timeline.dataset.product)}</div>` : ''}
            <div style="text-align: center; color: #6b7280; margin-bottom: 20px; border-bottom: 1px solid #eee; padding-bottom: 15px;">
                <div>${escapePdfText(t('Generated on: {0}', formatDate(new Date(), true)))}</div>
                <div>${escapePdfText(t('Total Transfers: {0}', timelineClone.querySelectorAll('.timeline-item').length))}</div>
            </div>
            ${timelineClone.outerHTML}
        </div>
    `;

    openPrintFrame(`
        <!DOCTYPE html>
        <html lang="${document.documentElement.lang || 'en'}">
        <head>
            <meta charset="UTF-8">
            <title>${escapePdfText(t('Transfer Timeline Report'))}</title>
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
                .fa-check-circle { color: #1E6B45; }
                .fa-clock { color: #7F5200; }
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
                .bg-success { background-color: #E7F3EC; color: #1E6B45; }
                .bg-warning { background-color: #FBF0D9; color: #7F5200; }
            </style>
        </head>
        <body>
            ${htmlContent}
        </body>
        </html>
    `);
}

function exportDepartmentsToPDF() {
    exportListTable(document.getElementById('departmentsTable'), 'Departments');
}

function exportCategoriesToPDF() {
    // The id sits on the <tbody> (the list's client-side search uses it).
    const body = document.getElementById('categoriesTable');
    exportListTable(body && body.closest('table'), 'Categories');
}
