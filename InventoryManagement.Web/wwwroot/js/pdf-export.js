// Exporters write textContent back as HTML, so all of it must pass through here or markup goes live
function escapePdfText(value) {
    return String(value ?? '').replace(/[&<>"']/g, c => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c]));
}

/** Reads a table into text rows, picking columns by header name so layout changes do not shift them. */
function collectTableData(table, excludeHeaders = []) {
    // Headers are in the UI language, so both the English name and its translation match
    const skip = excludeHeaders.flatMap(h => [h, t(h)]).map(h => h.toLowerCase());
    const allHeaders = Array.from(table.querySelectorAll('thead th'));
    const keep = allHeaders
        .map((th, index) => ({ name: th.textContent.trim(), index, th }))
        .filter(col => col.name
            && !col.th.classList.contains('actions-column')
            && !col.th.classList.contains('pdf-omit')
            && !skip.includes(col.name.toLowerCase()));

    const rows = Array.from(table.querySelectorAll('tbody tr'))
        // Rows hidden by a filter and the empty-state rows are not data
        .filter(tr => tr.style.display !== 'none' && !tr.hidden
            && !tr.classList.contains('ip-colfilter-empty') && !tr.classList.contains('ip-empty-row'))
        .map(tr => {
            const cells = Array.from(tr.children);
            return keep.map(col => readCellText(cells[col.index]));
        })
        .filter(cells => cells.some(v => v !== ''));

    return { headers: keep.map(c => c.name), rows };
}

/** Joins a cell's separate text blocks so they do not run together into one word. */
function readCellText(td) {
    if (!td) return '';
    const parts = [];
    const add = value => {
        const text = String(value || '').replace(/\s+/g, ' ').trim();
        if (text && !parts.includes(text)) parts.push(text);
    };

    // Placeholder text is marked .pdf-omit so the export prints a blank instead of a filler word
    const textWithoutPlaceholders = el => {
        const clone = el.cloneNode(true);
        clone.querySelectorAll('.pdf-omit').forEach(p => p.remove());
        return clone.textContent;
    };

    const blocks = td.querySelectorAll('.cell-title, .cell-sub, .state, .badge, .ip-badge, .ip-status-text, .ip-tag-plain');
    if (blocks.length) {
        blocks.forEach(b => { if (!b.closest('.pdf-omit')) add(textWithoutPlaceholders(b)); });
    } else {
        add(textWithoutPlaceholders(td));
    }
    return parts.join(' - ');
}

/** Builds a printable document from collected rows and opens the print dialog. */
function renderPrintDocument({ title, headers, rows, filters }) {
    title = t(title);
    const printed = formatDate(new Date(), true);
    const filterLine = filters ? `<div class="filters">${escapePdfText(filters)}</div>` : '';

    const thead = headers.map(h => `<th>${escapePdfText(h)}</th>`).join('');
    const tbody = rows.map(cells =>
        `<tr>${cells.map(c => `<td>${escapePdfText(c) || '<span class="empty">-</span>'}</td>`).join('')}</tr>`
    ).join('');

    // A hidden iframe, because a popup gets blocked, steals focus and can leave the list unresponsive
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
  /* overflow-wrap breaks only words that cannot fit, where word-break would split short ones */
  td { padding: 4px 6px; border-bottom: .8px solid #E4E4E1; vertical-align: top;
       overflow-wrap: break-word; hyphens: none; }
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
<footer>${escapePdfText(t('166 Inventory'))}</footer>
</body></html>`);

    printWindow.document.close();

    // Give the iframe a tick to lay out, and remove it afterwards even if printing is cancelled
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

/** Prints a full HTML document from a hidden iframe, for exports that are not a table. */
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

/** Summarises the active status tab and filters so the export says what it was filtered by. */
function currentFilterSummary() {
    const clean = s => (s || '').replace(/\s+/g, ' ').trim();
    const parts = [];
    const tab = document.querySelector('[data-list-tabs] .ip-tab.active');
    if (tab && tab !== document.querySelector('[data-list-tabs] .ip-tab')) {
        parts.push(clean(tab.firstChild?.textContent));
    }
    document.querySelectorAll('.ip-filterbar .ip-filter').forEach(filter => {
        const label = clean(filter.querySelector(':scope > span')?.textContent);
        const select = filter.querySelector('select');
        const input = filter.querySelector('input');
        let value = '';
        if (select && select.value) value = clean(select.selectedOptions[0]?.textContent);
        else if (input && input.value) value = clean(input.value);
        if (value) parts.push((label || t('Search')) + ': ' + value);
    });
    document.querySelectorAll('.ip-filterbar .ip-btn.is-on').forEach(b => parts.push(clean(b.textContent)));
    return parts.length ? t('Filters:') + ' ' + parts.filter(Boolean).join('   |   ') : '';
}

/** Re-reads every matching row from the server as one page, falling back to the rows on screen. */
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

/** Exports a list table with every matching row, not just the page on screen. */
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

/** Prints the route history from data-* attributes, so it does not depend on the screen markup. */
function exportTimelineToPDF() {
    const timeline = document.querySelector('.timeline');
    if (!timeline) {
        showToast(t('Timeline not found'), 'error');
        return;
    }

    const items = Array.from(timeline.querySelectorAll('.timeline-item'));
    const rows = items.map(item => {
        const d = item.dataset;
        const move = d.from
            ? `${escapePdfText(d.from)} <span class="arrow">&rarr;</span> <b>${escapePdfText(d.to)}</b>`
            : `<b>${escapePdfText(d.to)}</b>`;
        const chip = d.status === 'completed' ? 'ok' : 'wait';
        return `
            <div class="timeline-item">
                <div class="timeline-marker"></div>
                <div class="timeline-content">
                    <div class="head">
                        <span class="when">${escapePdfText(d.when)}</span>
                        <span class="type">${escapePdfText(d.type)}</span>
                        <span class="chip ${chip}">${escapePdfText(d.statusText)}</span>
                    </div>
                    <div class="move">${move}</div>
                    ${d.notes ? `<div class="notes">${escapePdfText(d.notes)}</div>` : ''}
                    ${d.completed ? `<div class="done">${escapePdfText(t('Completed'))}: ${escapePdfText(d.completed)}</div>` : ''}
                </div>
            </div>`;
    }).join('');

    const title = t('Transfer Timeline Report');
    openPrintFrame(`<!DOCTYPE html>
<html lang="${document.documentElement.lang || 'en'}">
<head>
<meta charset="UTF-8">
<title>${escapePdfText(title)}</title>
<style>
  @page { size: A4 portrait; margin: 12mm; }
  * { box-sizing: border-box; }
  body { font-family: "Segoe UI", Roboto, Arial, sans-serif; font-size: 10pt; line-height: 1.4; color: #1B1C1E; margin: 0; }
  header { border-bottom: 2px solid #1B1C1E; padding-bottom: 6px; margin-bottom: 12px; }
  h1 { font-size: 15pt; margin: 0; font-weight: 650; }
  .product { font-size: 11pt; font-weight: 600; margin-top: 2px; }
  .meta { font-size: 8pt; color: #4F5358; margin-top: 4px; }
  .timeline-item { display: flex; gap: 10px; padding: 8px 0; border-bottom: .8px solid #E4E4E1; page-break-inside: avoid; }
  .timeline-item:last-child { border-bottom: 0; }
  .timeline-marker { width: 8px; height: 8px; flex: none; margin-top: 5px; border-radius: 50%; background: #8A8E94; }
  .timeline-content { flex: 1; min-width: 0; }
  .head { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
  .when { font-weight: 600; font-variant-numeric: tabular-nums; }
  .type { color: #6B6F75; font-size: 9pt; }
  .chip { font-size: 8pt; font-weight: 500; padding: 1px 6px; border-radius: 3px; }
  .chip.ok { background: #E7F3EC; color: #1E6B45; }
  .chip.wait { background: #FBF0D9; color: #7F5200; }
  .move { margin-top: 2px; }
  .arrow { color: #6B6F75; }
  .notes { color: #4F5358; margin-top: 2px; white-space: pre-wrap; }
  .done { color: #6B6F75; font-size: 8pt; margin-top: 2px; font-variant-numeric: tabular-nums; }
  footer { margin-top: 10px; font-size: 7.5pt; color: #6B6F75; text-align: right; }
  @media print { body { -webkit-print-color-adjust: exact; print-color-adjust: exact; } }
</style>
</head>
<body>
<header>
  <h1>${escapePdfText(title)}</h1>
  ${timeline.dataset.product ? `<div class="product">${escapePdfText(timeline.dataset.product)}</div>` : ''}
  <div class="meta">${escapePdfText(t('Generated on: {0}', formatDate(new Date(), true)))} &middot; ${escapePdfText(t('Total Transfers: {0}', items.length))}</div>
</header>
<div class="timeline">${rows}</div>
<footer>${escapePdfText(t('166 Inventory'))}</footer>
</body>
</html>`);
}

function exportDepartmentsToPDF() {
    exportListTable(document.getElementById('departmentsTable'), 'Departments');
}

function exportCategoriesToPDF() {
    // The id is on the tbody because the list's client-side search uses it
    const body = document.getElementById('categoriesTable');
    exportListTable(body && body.closest('table'), 'Categories');
}
