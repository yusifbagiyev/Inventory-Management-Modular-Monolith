// Sorting and per-column filters in table headers, on the server for paged lists and in the page otherwise
//
// A header opts in with data-sort="key" and/or data-filter="text|list|date|range" (+ data-param, data-options,
// data-param-from/to, data-param-min/max). Tables marked data-columns="client" sort and filter their own rows.
window.TableColumns = (function () {
    'use strict';

    let panel = null;
    let panelHeader = null;

    // Client tables keep their filters here, keyed by header cell
    const clientFilters = new WeakMap();

    function isClient(th) {
        const table = th.closest('table');
        return !!table && table.dataset.columns === 'client';
    }

    function dataTableOf(table) {
        return window.jQuery && $.fn.dataTable && $.fn.dataTable.isDataTable(table) ? $(table).DataTable() : null;
    }

    function columnIndex(th) {
        return Array.prototype.indexOf.call(th.parentElement.children, th);
    }

    function label(th) {
        return th.dataset.label || (th.querySelector('.ip-th-label') || th).textContent.trim();
    }

    function fold(text) {
        return String(text ?? '').toLocaleLowerCase('az')
            .replace(/ə/g, 'e').replace(/ı/g, 'i').replace(/ö/g, 'o').replace(/ü/g, 'u')
            .replace(/ğ/g, 'g').replace(/ş/g, 's').replace(/ç/g, 'c').trim();
    }

    function cellValue(row, index) {
        const cell = row.children[index];
        if (!cell) return '';
        return cell.dataset.filterValue ?? cell.dataset.order ?? cell.textContent.trim();
    }

    // Header markup

    function decorate(root) {
        const fresh = (root || document).querySelectorAll('th[data-sort]:not([data-decorated]), th[data-filter]:not([data-decorated])');
        if (!fresh.length) return;
        fresh.forEach(function (th) {
            th.dataset.decorated = '1';
            const text = th.textContent.trim();
            th.dataset.label = th.dataset.label || text;
            const sortable = !!th.dataset.sort && !dataTableOf(th.closest('table'));
            const wrap = document.createElement('div');
            wrap.className = 'ip-th';
            wrap.innerHTML = sortable
                ? `<button type="button" class="ip-th-sort" data-th-sort><span class="ip-th-label">${escapeHtml(text)}</span><i class="ip-th-sort-icon fa-solid fa-sort" aria-hidden="true"></i></button>`
                : `<span class="ip-th-label">${escapeHtml(text)}</span>`;
            if (th.dataset.filter) {
                wrap.insertAdjacentHTML('beforeend',
                    `<button type="button" class="ip-th-filter" data-th-filter aria-haspopup="dialog" aria-label="${escapeHtml(t('Filter by {0}').replace('{0}', text))}" title="${escapeHtml(t('Filter'))}"><i class="fa-solid fa-filter" aria-hidden="true"></i></button>`);
            }
            th.replaceChildren(wrap);
            th.classList.add('ip-th-cell');
            // Own listener, so a DataTables header does not sort when its filter opens
            const filterButton = wrap.querySelector('[data-th-filter]');
            if (filterButton) filterButton.addEventListener('click', function (e) {
                e.stopPropagation();
                if (panel && panelHeader === th) close(); else open(th, filterButton);
            });
        });
        syncHeaders();
    }

    // Marks the active sort and filters on every decorated header
    function syncHeaders() {
        const params = new URLSearchParams(window.location.search);
        document.querySelectorAll('th[data-decorated]').forEach(function (th) {
            let dir = '';
            if (isClient(th)) dir = th.dataset.sortDir || '';
            else if (th.dataset.sort && params.get('sort') === th.dataset.sort) dir = params.get('dir') === 'desc' ? 'desc' : 'asc';
            const icon = th.querySelector('.ip-th-sort-icon');
            if (icon) icon.className = 'ip-th-sort-icon fa-solid ' + (dir === 'asc' ? 'fa-sort-up' : dir === 'desc' ? 'fa-sort-down' : 'fa-sort');
            th.setAttribute('aria-sort', dir === 'asc' ? 'ascending' : dir === 'desc' ? 'descending' : 'none');
            th.classList.toggle('is-sorted', !!dir);
            const filterButton = th.querySelector('.ip-th-filter');
            if (filterButton) filterButton.classList.toggle('is-active', isFiltered(th, params));
        });
    }

    function paramNames(th) {
        const d = th.dataset;
        switch (d.filter) {
            case 'date': return [d.paramFrom, d.paramTo];
            case 'range': return [d.paramMin, d.paramMax];
            default: return [d.param];
        }
    }

    function isFiltered(th, params) {
        if (isClient(th)) return clientFilters.has(th);
        return paramNames(th).some(function (name) { return name && params.getAll(name).some(Boolean); });
    }

    // Sorting

    function sortServer(th) {
        const url = new URL(window.location.href);
        const p = url.searchParams;
        const key = th.dataset.sort;
        const first = th.dataset.sortFirst === 'desc' ? 'desc' : 'asc';
        const second = first === 'asc' ? 'desc' : 'asc';
        if (p.get('sort') !== key) { p.set('sort', key); p.set('dir', first); }
        else if ((p.get('dir') || 'asc') === first) p.set('dir', second);
        else { p.delete('sort'); p.delete('dir'); }
        p.delete('pageNumber');
        navigate(url);
    }

    function compare(a, b) {
        const na = Number(String(a).replace(/\s/g, '').replace(',', '.'));
        const nb = Number(String(b).replace(/\s/g, '').replace(',', '.'));
        if (a !== '' && b !== '' && !isNaN(na) && !isNaN(nb)) return na - nb;
        return String(a).localeCompare(String(b), document.documentElement.lang || 'az', { sensitivity: 'base', numeric: true });
    }

    function sortClient(th) {
        const table = th.closest('table');
        const first = th.dataset.sortFirst === 'desc' ? 'desc' : 'asc';
        const current = th.dataset.sortDir || '';
        const next = !current ? first : current === first ? (first === 'asc' ? 'desc' : 'asc') : '';
        table.querySelectorAll('th[data-sort-dir]').forEach(function (h) { delete h.dataset.sortDir; });
        const body = table.tBodies[0];
        if (!body) return;
        if (!table._originalOrder) table._originalOrder = Array.from(body.rows);
        const index = columnIndex(th);
        let rows = table._originalOrder.slice();
        if (next) {
            th.dataset.sortDir = next;
            rows.sort(function (a, b) {
                const result = compare(cellValue(a, index), cellValue(b, index));
                return next === 'asc' ? result : -result;
            });
        }
        rows.forEach(function (row) { body.appendChild(row); });
        syncHeaders();
    }

    // Filtering

    function navigate(url) {
        if (window.ListNav) ListNav.go(url.pathname + url.search);
        else window.location.href = url.href;
    }

    function applyServer(th, values) {
        const url = new URL(window.location.href);
        const p = url.searchParams;
        paramNames(th).forEach(function (name, i) {
            if (!name) return;
            p.delete(name);
            const value = values[i];
            if (Array.isArray(value)) value.forEach(function (v) { p.append(name, v); });
            else if (value !== '' && value != null) p.set(name, value);
        });
        p.delete('pageNumber');
        navigate(url);
    }

    function rowMatches(row, table) {
        return Array.from(table.tHead.rows[0].cells).every(function (th) {
            const filter = clientFilters.get(th);
            return !filter || filter(cellValue(row, columnIndex(th)));
        });
    }

    function applyClient(th, filter) {
        const table = th.closest('table');
        if (filter) clientFilters.set(th, filter); else clientFilters.delete(th);

        const dt = dataTableOf(table);
        if (dt) {
            dt.draw();
        } else if (table.tBodies[0]) {
            let shown = 0;
            Array.from(table.tBodies[0].rows).forEach(function (row) {
                if (row.classList.contains('ip-colfilter-empty')) return;
                const match = rowMatches(row, table);
                row.hidden = !match;
                if (match) shown++;
            });
            let empty = table.tBodies[0].querySelector('.ip-colfilter-empty');
            if (!shown && !empty) {
                empty = document.createElement('tr');
                empty.className = 'ip-colfilter-empty';
                empty.innerHTML = `<td colspan="${table.tHead.rows[0].cells.length}">${escapeHtml(t('No matching records found'))}</td>`;
                table.tBodies[0].appendChild(empty);
            } else if (shown && empty) {
                empty.remove();
            }
        }
        syncHeaders();
    }

    // DataTables tables run the same filters through their search hook
    if (window.jQuery && $.fn.dataTable) {
        $.fn.dataTable.ext.search.push(function (settings, data, index) {
            const table = settings.nTable;
            if (table.dataset.columns !== 'client') return true;
            const row = settings.aoData[index] && settings.aoData[index].nTr;
            return !row || rowMatches(row, table);
        });
    }

    // Panel

    function clientOptions(th) {
        const table = th.closest('table');
        const index = columnIndex(th);
        const dt = dataTableOf(table);
        const rows = dt ? dt.rows().nodes().toArray() : Array.from(table.tBodies[0]?.rows || []);
        const seen = new Map();
        rows.forEach(function (row) {
            if (row.classList.contains('ip-colfilter-empty')) return;
            const value = cellValue(row, index);
            const cell = row.children[index];
            if (value && !seen.has(value)) seen.set(value, cell?.dataset.filterLabel || cell?.textContent.trim() || value);
        });
        return Array.from(seen, function ([value, text]) { return { value: value, label: text }; })
            .sort(function (a, b) { return compare(a.label, b.label); });
    }

    function optionsOf(th) {
        if (th.dataset.options) {
            try { return JSON.parse(th.dataset.options); } catch (e) { return []; }
        }
        return isClient(th) ? clientOptions(th) : [];
    }

    function currentValues(th) {
        if (isClient(th)) return th._clientValues || [];
        const p = new URLSearchParams(window.location.search);
        return paramNames(th).map(function (name) { return name ? p.getAll(name) : []; });
    }

    function body(th) {
        const type = th.dataset.filter;
        const values = currentValues(th);
        if (type === 'list') {
            const selected = new Set((values[0] || []).map(String));
            const options = optionsOf(th);
            const multi = th.dataset.multi !== 'false';
            const search = options.length > 8
                ? `<input type="search" class="ip-input ip-input-sm" data-colfilter-find placeholder="${escapeHtml(t('Search...'))}" />` : '';
            const items = options.length
                ? options.map(function (o) {
                    return `<label class="ip-colfilter-opt"><input type="${multi ? 'checkbox' : 'radio'}" name="colfilter" value="${escapeHtml(o.value)}" ${selected.has(String(o.value)) ? 'checked' : ''} /><span>${escapeHtml(o.label)}</span></label>`;
                }).join('')
                : `<div class="ip-faint small">${escapeHtml(t('No values'))}</div>`;
            return `${search}<div class="ip-colfilter-list">${items}</div>`;
        }
        if (type === 'date') {
            return `<div data-colfilter-calendar></div>`;
        }
        if (type === 'range') {
            const [min, max] = [(values[0] || [])[0] || '', (values[1] || [])[0] || ''];
            return `<div class="ip-colfilter-range">
                <input type="number" class="ip-input ip-input-sm" data-colfilter-min placeholder="${escapeHtml(t('Minimum'))}" value="${escapeHtml(min)}" />
                <span aria-hidden="true">–</span>
                <input type="number" class="ip-input ip-input-sm" data-colfilter-max placeholder="${escapeHtml(t('Maximum'))}" value="${escapeHtml(max)}" />
            </div>`;
        }
        const text = (values[0] || [])[0] || '';
        return `<input type="search" class="ip-input ip-input-sm" data-colfilter-text placeholder="${escapeHtml(t('Search...'))}" value="${escapeHtml(text)}" />`;
    }

    function open(th, button) {
        close();
        panelHeader = th;
        panel = document.createElement('div');
        panel.className = 'ip-colfilter';
        panel.setAttribute('role', 'dialog');
        panel.setAttribute('aria-label', t('Filter by {0}').replace('{0}', label(th)));
        panel.innerHTML = `<div class="ip-colfilter-head">${escapeHtml(label(th))}</div>
            <div class="ip-colfilter-body">${body(th)}</div>
            <div class="ip-colfilter-foot">
                <button type="button" class="ip-btn ip-btn-ghost ip-btn-sm" data-colfilter-clear>${escapeHtml(t('Reset'))}</button>
                <button type="button" class="ip-btn ip-btn-primary ip-btn-sm" data-colfilter-apply>${escapeHtml(t('Apply'))}</button>
            </div>`;
        document.body.appendChild(panel);

        if (th.dataset.filter === 'date') {
            const holder = panel.querySelector('[data-colfilter-calendar]');
            DateRange.attach(holder, { inline: true });
            const values = currentValues(th);
            const from = DateRange.fromIso((values[0] || [])[0]);
            const to = DateRange.fromIso((values[1] || [])[0]);
            if (from && to) DateRange.set(holder, from, to);
        }

        place(button);
        button.setAttribute('aria-expanded', 'true');
        const first = panel.querySelector('input:not([type=checkbox]):not([type=radio]), input');
        if (first && window.matchMedia('(pointer: fine)').matches) first.focus();
    }

    function place(button) {
        if (!panel) return;
        if (window.matchMedia('(max-width: 767.98px)').matches) {
            panel.classList.add('is-sheet');
            return;
        }
        const rect = button.getBoundingClientRect();
        const width = panel.offsetWidth;
        const left = Math.min(Math.max(8, rect.right - width), window.innerWidth - width - 8);
        panel.style.left = (left + window.scrollX) + 'px';
        panel.style.top = (rect.bottom + window.scrollY + 6) + 'px';
    }

    function close() {
        if (!panel) return;
        panel.remove();
        panel = null;
        document.querySelectorAll('.ip-th-filter[aria-expanded="true"]').forEach(function (b) { b.removeAttribute('aria-expanded'); });
        panelHeader = null;
    }

    function readPanel(th) {
        const type = th.dataset.filter;
        if (type === 'list') {
            return [Array.from(panel.querySelectorAll('.ip-colfilter-list input:checked')).map(function (i) { return i.value; })];
        }
        if (type === 'date') {
            const picker = panel.querySelector('[data-colfilter-calendar]');
            const dates = DateRange.selected(picker);
            return dates.length === 2 ? [DateRange.iso(dates[0]), DateRange.iso(dates[1])] : ['', ''];
        }
        if (type === 'range') {
            return [panel.querySelector('[data-colfilter-min]').value.trim(), panel.querySelector('[data-colfilter-max]').value.trim()];
        }
        return [panel.querySelector('[data-colfilter-text]').value.trim()];
    }

    function emptyValues(th) {
        return th.dataset.filter === 'list' ? [[]] : paramNames(th).map(function () { return ''; });
    }

    function clientFilter(th, values) {
        const type = th.dataset.filter;
        if (type === 'list') {
            const set = new Set(values[0]);
            return set.size ? function (v) { return set.has(v); } : null;
        }
        if (type === 'date') {
            const [from, to] = values;
            return from && to ? function (v) { const day = String(v).slice(0, 10); return day >= from && day <= to; } : null;
        }
        if (type === 'range') {
            const min = values[0] === '' ? null : Number(values[0]);
            const max = values[1] === '' ? null : Number(values[1]);
            if (min === null && max === null) return null;
            return function (v) {
                const n = Number(String(v).replace(/\s/g, ''));
                return !isNaN(n) && (min === null || n >= min) && (max === null || n <= max);
            };
        }
        const needle = fold(values[0]);
        return needle ? function (v) { return fold(v).includes(needle); } : null;
    }

    function apply(th, values) {
        close();
        if (isClient(th)) {
            th._clientValues = values.map(function (v) { return Array.isArray(v) ? v : (v ? [v] : []); });
            applyClient(th, clientFilter(th, values));
        } else {
            applyServer(th, values);
        }
    }

    function clearAll() {
        document.querySelectorAll('table[data-columns="client"] th[data-decorated]').forEach(function (th) {
            if (clientFilters.has(th)) { th._clientValues = []; applyClient(th, null); }
        });
    }

    // Events

    document.addEventListener('click', function (e) {
        const sortButton = e.target.closest('[data-th-sort]');
        if (sortButton) {
            const th = sortButton.closest('th');
            if (isClient(th)) sortClient(th); else sortServer(th);
            return;
        }
        if (!panel) return;
        if (e.target.closest('[data-colfilter-apply]')) { apply(panelHeader, readPanel(panelHeader)); return; }
        if (e.target.closest('[data-colfilter-clear]')) { apply(panelHeader, emptyValues(panelHeader)); return; }
        if (!e.target.closest('.ip-colfilter') && !e.target.closest('.air-datepicker')) close();
    });

    document.addEventListener('keydown', function (e) {
        if (!panel) return;
        if (e.key === 'Escape') { close(); return; }
        if (e.key === 'Enter' && e.target.closest('.ip-colfilter') && e.target.matches('input')) {
            e.preventDefault();
            apply(panelHeader, readPanel(panelHeader));
        }
    });

    document.addEventListener('input', function (e) {
        if (!e.target.matches('[data-colfilter-find]')) return;
        const needle = fold(e.target.value);
        panel.querySelectorAll('.ip-colfilter-opt').forEach(function (opt) {
            opt.hidden = !!needle && !fold(opt.textContent).includes(needle);
        });
    });

    // A phone keyboard resizes the window too, which must not close the sheet
    window.addEventListener('resize', function () { if (panel && !panel.classList.contains('is-sheet')) close(); });
    window.addEventListener('popstate', function () { close(); syncHeaders(); });
    document.addEventListener('listnav:loaded', function () { close(); decorate(); syncHeaders(); });

    // Live updates and list navigation swap the table, so new headers are decorated as they appear
    new MutationObserver(function () { decorate(); })
        .observe(document.documentElement, { childList: true, subtree: true });

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', function () { decorate(); });
    else decorate();

    /**
     * Ties filter bar controls to the query values the headers use, for phones where the headers are hidden.
     * fields maps a select to its query name, sort is a select of "key:dir" values whose first option is the default.
     */
    function bindBar(options) {
        const fields = options.fields || {};
        const sortSelect = options.sort ? document.querySelector(options.sort) : null;
        const search = options.search ? document.querySelector(options.search) : null;
        const reset = options.reset ? document.querySelector(options.reset) : null;

        function go(changes) {
            const url = new URL(window.location.href);
            Object.entries(changes).forEach(function ([key, value]) {
                if (value) url.searchParams.set(key, value); else url.searchParams.delete(key);
            });
            url.searchParams.delete('pageNumber');
            navigate(url);
        }

        Object.entries(fields).forEach(function ([selector, param]) {
            const select = document.querySelector(selector);
            if (select) select.addEventListener('change', function () { go({ [param]: select.value }); });
        });
        if (sortSelect) sortSelect.addEventListener('change', function () {
            const [sort, dir] = sortSelect.value.split(':');
            go(sortSelect.selectedIndex === 0 ? { sort: '', dir: '' } : { sort: sort, dir: dir });
        });
        if (search) search.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); go({ search: search.value.trim() }); }
        });

        // The bar is not swapped with the list, so it follows the URL after paging, header clicks and Back
        function sync() {
            const p = new URLSearchParams(window.location.search);
            if (search) search.value = p.get('search') || '';
            Object.entries(fields).forEach(function ([selector, param]) {
                const select = document.querySelector(selector);
                if (select) select.value = p.getAll(param).length === 1 ? p.get(param) : '';
            });
            if (sortSelect) {
                const value = p.get('sort') ? p.get('sort') + ':' + (p.get('dir') || 'asc') : '';
                const match = Array.from(sortSelect.options).find(function (o) { return o.value === value; });
                sortSelect.value = match ? value : sortSelect.options[0].value;
            }
            if (reset) reset.classList.toggle('d-none', !Array.from(p.keys()).some(function (k) { return k !== 'pageNumber' && k !== 'pageSize'; }));
        }
        document.addEventListener('listnav:loaded', sync);
        window.addEventListener('popstate', sync);
        sync();
    }

    return { decorate: decorate, clear: clearAll, bindBar: bindBar };
})();
