// Sorting and per-column filters in table headers, on the server for paged lists and in the page otherwise
//
// A header opts in with data-sort="key" and/or data-filter="text|list|date|range" (+ data-param, data-options,
// data-param-from/to, data-param-min/max). Tables marked data-column-filters="client" sort and filter their own rows.
window.TableColumns = (function () {
    'use strict';

    let panel = null;
    let panelHeader = null;
    let panelOpener = null;

    // The header control that takes focus again once a reload has rebuilt the headers
    let refocus = null;

    // Client tables keep their filters here, keyed by header cell
    const clientFilters = new WeakMap();

    // Filter values and sort of client tables by table and column, so a live refresh that swaps the table keeps them
    const clientState = new Map();

    function stateKey(th) {
        const table = th.closest('table');
        return (table.id || window.location.pathname) + ':' + columnIndex(th);
    }

    function remember(th, change) {
        clientState.set(stateKey(th), Object.assign({}, clientState.get(stateKey(th)), change));
    }

    function changed(table) {
        table.dispatchEvent(new CustomEvent('tablecolumns:change', { bubbles: true }));
    }

    function isClient(th) {
        const table = th.closest('table');
        return !!table && table.dataset.columnFilters === 'client';
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

    // A swapped table has new header cells, so a column is found again by its table and position
    function mark(th, control) {
        return { table: th.closest('table').id || '', index: columnIndex(th), filter: th.dataset.filter || '', control: control };
    }

    function findHeader(place) {
        return Array.from(document.querySelectorAll('th[data-decorated]')).find(function (th) {
            return (th.closest('table').id || '') === place.table && columnIndex(th) === place.index && (th.dataset.filter || '') === place.filter;
        }) || null;
    }

    function focusQuietly(el) {
        if (el && el.isConnected) el.focus({ preventScroll: true });
    }

    function focusIsFree() {
        return !document.activeElement || document.activeElement === document.body;
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
            if (filterButton) {
                filterButton.addEventListener('click', function (e) {
                    e.stopPropagation();
                    if (panel && panelHeader === th) close(); else open(th, filterButton);
                });
                // DataTables sorts on Enter in a header, so the key stays with the button
                ['keydown', 'keypress'].forEach(function (type) {
                    filterButton.addEventListener(type, function (e) {
                        if (e.key === 'Enter' || e.key === ' ') e.stopPropagation();
                    });
                });
            }
        });
        fresh.forEach(function (th) { if (isClient(th)) restore(th); });
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
        refocus = mark(th, '.ip-th-sort');
        navigate(url);
    }

    function compare(a, b) {
        const na = Number(String(a).replace(/\s/g, '').replace(',', '.'));
        const nb = Number(String(b).replace(/\s/g, '').replace(',', '.'));
        if (a !== '' && b !== '' && !isNaN(na) && !isNaN(nb)) return na - nb;
        return String(a).localeCompare(String(b), document.documentElement.lang || 'az', { sensitivity: 'base', numeric: true });
    }

    function sortClient(th, dir) {
        const table = th.closest('table');
        const first = th.dataset.sortFirst === 'desc' ? 'desc' : 'asc';
        const current = th.dataset.sortDir || '';
        const next = dir !== undefined ? dir : !current ? first : current === first ? (first === 'asc' ? 'desc' : 'asc') : '';
        table.querySelectorAll('th[data-decorated]').forEach(function (h) {
            delete h.dataset.sortDir;
            if (clientState.has(stateKey(h))) remember(h, { sortDir: '' });
        });
        remember(th, { sortDir: next });
        const body = table.tBodies[0];
        if (!body) return;
        // The row that says nothing matches is not data, so it is neither remembered nor sorted
        const isData = function (row) { return !row.classList.contains('ip-colfilter-empty'); };
        if (!table._originalOrder) table._originalOrder = Array.from(body.rows).filter(isData);
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
        Array.from(body.rows).filter(function (row) { return !isData(row); }).forEach(function (row) { body.appendChild(row); });
        syncHeaders();
        changed(table);
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
        // Nothing reloads when the address stays the same, so there is no rebuilt header to wait for
        if (url.href !== window.location.href) refocus = mark(th, '.ip-th-filter');
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
        changed(table);
    }

    // DataTables tables run the same filters through their search hook
    if (window.jQuery && $.fn.dataTable) {
        // Read through the public API, since the settings object's own fields change between DataTables versions
        $.fn.dataTable.ext.search.push(function (settings, data, index) {
            const api = new $.fn.dataTable.Api(settings);
            const table = api.table().node();
            if (!table || table.dataset.columnFilters !== 'client') return true;
            const row = api.row(index).node();
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

    /** The filter a header holds in words, such as the names picked from a list or the two ends of a range. */
    function describe(th, values) {
        const first = function (i) { return (values[i] || []).filter(Boolean)[0] || ''; };
        switch (th.dataset.filter) {
            case 'list': {
                const names = new Map(optionsOf(th).map(function (o) { return [String(o.value), o.label]; }));
                return (values[0] || []).filter(Boolean).map(function (v) { return names.get(String(v)) || v; }).join(', ');
            }
            case 'date': {
                const days = [first(0), first(1)].filter(Boolean).map(function (iso) {
                    const day = window.DateRange ? DateRange.fromIso(iso) : null;
                    return day ? DateRange.text(day) : iso;
                });
                return days.length < 2 || days[0] === days[1] ? days[0] : days.join(' – ');
            }
            case 'range': {
                const min = first(0), max = first(1);
                return min && max ? min + ' – ' + max : min ? '≥ ' + min : '≤ ' + max;
            }
            default:
                return first(0);
        }
    }

    /** The header filters in effect as { label, text, params }, for places that list them away from the header. */
    function activeFilters(root) {
        const found = [];
        (root || document).querySelectorAll('th[data-filter]').forEach(function (th) {
            const values = currentValues(th);
            if (!values.some(function (v) { return (v || []).some(Boolean); })) return;
            found.push({ label: label(th), text: describe(th, values), params: paramNames(th).filter(Boolean) });
        });
        return found;
    }

    function body(th) {
        const type = th.dataset.filter;
        const values = currentValues(th);
        if (type === 'list') {
            const selected = new Set((values[0] || []).map(String));
            const options = optionsOf(th);
            const multi = th.dataset.multi !== 'false';
            const search = options.length > 8
                ? `<input type="search" class="ip-input" data-colfilter-find placeholder="${escapeHtml(t('Search...'))}" aria-label="${escapeHtml(t('Search'))}" />` : '';
            const items = options.length
                ? options.map(function (o) {
                    return `<label class="ip-colfilter-opt"><input type="${multi ? 'checkbox' : 'radio'}" name="colfilter" value="${escapeHtml(o.value)}" ${selected.has(String(o.value)) ? 'checked' : ''} /><span>${escapeHtml(o.label)}</span></label>`;
                }).join('')
                : `<div class="ip-faint small">${escapeHtml(t('No values'))}</div>`;
            return `${search}<div class="ip-colfilter-list" role="group" aria-label="${escapeHtml(label(th))}">${items}</div>`;
        }
        if (type === 'date') {
            // The two fields are the keyboard's way to a range, and the calendar under them fills the same fields
            const field = function (end, name) {
                return `<label class="ip-colfilter-date"><span>${escapeHtml(name)}</span><input type="text" class="ip-input" data-colfilter-${end} inputmode="numeric" autocomplete="off" size="10" maxlength="10" placeholder="${escapeHtml(t('dd.mm.yyyy'))}" aria-describedby="colfilterDateError" /></label>`;
            };
            return `<div class="ip-colfilter-dates">${field('from', t('Start date'))}${field('to', t('End date'))}</div>
                <div class="ip-colfilter-error" id="colfilterDateError" role="alert" hidden>${escapeHtml(t('Enter the date as dd.mm.yyyy'))}</div>
                <div data-colfilter-calendar aria-hidden="true"></div>`;
        }
        if (type === 'range') {
            const [min, max] = [(values[0] || [])[0] || '', (values[1] || [])[0] || ''];
            return `<div class="ip-colfilter-range">
                <input type="number" class="ip-input" data-colfilter-min placeholder="${escapeHtml(t('Minimum'))}" aria-label="${escapeHtml(t('Minimum'))}" value="${escapeHtml(min)}" />
                <span aria-hidden="true">–</span>
                <input type="number" class="ip-input" data-colfilter-max placeholder="${escapeHtml(t('Maximum'))}" aria-label="${escapeHtml(t('Maximum'))}" value="${escapeHtml(max)}" />
            </div>`;
        }
        const text = (values[0] || [])[0] || '';
        return `<input type="search" class="ip-input" data-colfilter-text placeholder="${escapeHtml(t('Search...'))}" aria-label="${escapeHtml(label(th))}" value="${escapeHtml(text)}" />`;
    }

    function dateFields() {
        return [panel.querySelector('[data-colfilter-from]'), panel.querySelector('[data-colfilter-to]')];
    }

    function markDates(wrong) {
        dateFields().forEach(function (field) {
            const invalid = field === wrong;
            field.classList.toggle('is-invalid', invalid);
            if (invalid) field.setAttribute('aria-invalid', 'true'); else field.removeAttribute('aria-invalid');
        });
        panel.querySelector('.ip-colfilter-error').hidden = !wrong;
    }

    function attachCalendar(th) {
        const holder = panel.querySelector('[data-colfilter-calendar]');
        const fields = dateFields();
        const owner = panel;
        DateRange.attach(holder, {
            inline: true,
            // The picker reports a moment later, when this panel may already be closed
            onChange: function (dates) {
                if (panel !== owner) return;
                fields.forEach(function (field, i) { field.value = dates[i] ? DateRange.text(dates[i]) : ''; });
                markDates(null);
            }
        });
        const values = currentValues(th);
        const from = DateRange.fromIso((values[0] || [])[0]);
        const to = DateRange.fromIso((values[1] || [])[0]);
        if (from && to) {
            DateRange.set(holder, from, to);
            fields[0].value = DateRange.text(from);
            fields[1].value = DateRange.text(to);
        }
    }

    // Typed days show in the calendar as soon as they read as dates
    function syncCalendar() {
        const holder = panel.querySelector('[data-colfilter-calendar]');
        const days = dateFields().map(function (field) { return DateRange.day(field.value); });
        markDates(null);
        const from = days[0] || days[1], to = days[1] || days[0];
        if (from) DateRange.set(holder, from <= to ? from : to, from <= to ? to : from);
        else if (dateFields().every(function (field) { return !field.value.trim(); })) DateRange.clear(holder);
    }

    function open(th, button) {
        close();
        panelHeader = th;
        panelOpener = button;
        panel = document.createElement('div');
        panel.className = 'ip-colfilter';
        panel.setAttribute('role', 'dialog');
        panel.setAttribute('aria-label', t('Filter by {0}').replace('{0}', label(th)));
        panel.tabIndex = -1;
        panel.innerHTML = `<div class="ip-colfilter-head">${escapeHtml(label(th))}</div>
            <div class="ip-colfilter-body">${body(th)}</div>
            <div class="ip-colfilter-foot">
                <button type="button" class="ip-btn ip-btn-ghost ip-btn-sm" data-colfilter-clear>${escapeHtml(t('Reset'))}</button>
                <button type="button" class="ip-btn ip-btn-primary ip-btn-sm" data-colfilter-apply>${escapeHtml(t('Apply'))}</button>
            </div>`;
        document.body.appendChild(panel);

        if (th.dataset.filter === 'date') attachCalendar(th);

        place(button);
        button.setAttribute('aria-expanded', 'true');
        // A touch screen would raise its keyboard for a focused field, so there the panel itself takes the focus
        const first = window.matchMedia('(pointer: fine)').matches
            ? panel.querySelector('input:not([type=checkbox]):not([type=radio]), input') : null;
        focusQuietly(first || panel);
    }

    function place(button) {
        if (!panel) return;
        if (window.matchMedia('(max-width: 767.98px)').matches) {
            panel.classList.add('is-sheet');
            return;
        }
        const rect = button.getBoundingClientRect();
        const column = (button.closest('.ip-th') || button).getBoundingClientRect();
        const width = panel.offsetWidth;
        // Opens under its column and turns left only when the window has no room, and never lies over the menu
        const main = document.querySelector('.ip-main');
        const min = (main ? Math.max(0, main.getBoundingClientRect().left) : 0) + 8;
        const max = window.innerWidth - width - 8;
        const left = Math.max(min, Math.min(column.left <= max ? column.left : rect.right - width, max));
        panel.style.left = (left + window.scrollX) + 'px';
        panel.style.top = (rect.bottom + window.scrollY + 6) + 'px';
    }

    function close() {
        if (!panel) return;
        const opener = panelOpener;
        // Focus goes back to the button that opened the panel unless the user has already put it somewhere else
        const back = focusIsFree() || panel.contains(document.activeElement);
        panel.remove();
        panel = null;
        document.querySelectorAll('.ip-th-filter[aria-expanded="true"]').forEach(function (b) { b.removeAttribute('aria-expanded'); });
        panelHeader = null;
        panelOpener = null;
        if (back) focusQuietly(opener);
    }

    /** Keeps Tab inside the open panel, which sits at the end of the page and not next to its button. */
    function trapTab(e) {
        const stops = Array.from(panel.querySelectorAll('input, button, select, [tabindex]:not([tabindex="-1"])'))
            .filter(function (el, i, all) {
                if (el.disabled || el.closest('[hidden]')) return false;
                if (el.type !== 'radio') return true;
                // Radio buttons of one group are a single stop, the chosen one or else the first
                const group = all.filter(function (other) { return other.type === 'radio' && other.name === el.name; });
                return el === (group.find(function (other) { return other.checked; }) || group[0]);
            });
        if (!stops.length) { e.preventDefault(); focusQuietly(panel); return; }
        const active = document.activeElement;
        const outside = !active || !panel.contains(active) || active === panel;
        const atStart = !outside && (active === stops[0] || (active.type === 'radio' && stops[0].type === 'radio' && active.name === stops[0].name));
        if (e.shiftKey && (outside || atStart)) { e.preventDefault(); stops[stops.length - 1].focus(); }
        else if (!e.shiftKey && (outside || active === stops[stops.length - 1])) { e.preventDefault(); stops[0].focus(); }
    }

    /** The values the panel holds, or null while a typed date cannot be read. */
    function readPanel(th) {
        const type = th.dataset.filter;
        if (type === 'list') {
            return [Array.from(panel.querySelectorAll('.ip-colfilter-list input:checked')).map(function (i) { return i.value; })];
        }
        if (type === 'date') {
            const fields = dateFields();
            const days = fields.map(function (field) { return DateRange.day(field.value); });
            const wrong = fields.find(function (field, i) { return field.value.trim() && !days[i]; });
            markDates(wrong || null);
            if (wrong) { wrong.focus(); wrong.select(); return null; }
            // One day alone filters that day, and ends given the wrong way round are swapped
            const from = days[0] || days[1], to = days[1] || days[0];
            if (!from) return ['', ''];
            return [DateRange.iso(from <= to ? from : to), DateRange.iso(from <= to ? to : from)];
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
        if (!values) return;
        close();
        if (isClient(th)) {
            th._clientValues = values.map(function (v) { return Array.isArray(v) ? v : (v ? [v] : []); });
            remember(th, { values: values });
            applyClient(th, clientFilter(th, values));
        } else {
            applyServer(th, values);
        }
    }

    function clearAll() {
        document.querySelectorAll('table[data-column-filters="client"] th[data-decorated]').forEach(function (th) {
            remember(th, { values: null });
            if (clientFilters.has(th)) { th._clientValues = []; applyClient(th, null); }
        });
    }

    // Puts back the filters and sort a swapped-in client table had before
    function restore(th) {
        const state = clientState.get(stateKey(th));
        if (!state) return;
        if (state.values) {
            const filter = clientFilter(th, state.values);
            if (filter) {
                th._clientValues = state.values.map(function (v) { return Array.isArray(v) ? v : (v ? [v] : []); });
                applyClient(th, filter);
            }
        }
        if (state.sortDir) sortClient(th, state.sortDir);
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
        // The calendar redraws its cells on a click, so the target may have left the page by now and the path is asked instead
        if (!e.composedPath().includes(panel)) close();
    });

    document.addEventListener('keydown', function (e) {
        if (!panel) return;
        if (e.key === 'Escape') { close(); return; }
        if (e.key === 'Tab') { trapTab(e); return; }
        if (e.key === 'Enter' && e.target.closest('.ip-colfilter') && e.target.matches('input')) {
            e.preventDefault();
            apply(panelHeader, readPanel(panelHeader));
        }
    });

    document.addEventListener('input', function (e) {
        if (!panel) return;
        if (e.target.matches('[data-colfilter-from], [data-colfilter-to]')) { syncCalendar(); return; }
        if (!e.target.matches('[data-colfilter-find]')) return;
        const needle = fold(e.target.value);
        panel.querySelectorAll('.ip-colfilter-opt').forEach(function (opt) {
            opt.hidden = !!needle && !fold(opt.textContent).includes(needle);
        });
    });

    // A day typed loosely, like 5.1.2026, is rewritten the way the lists show dates once the field is left
    document.addEventListener('change', function (e) {
        if (!panel || !e.target.matches('[data-colfilter-from], [data-colfilter-to]')) return;
        const day = DateRange.day(e.target.value);
        if (day) e.target.value = DateRange.text(day);
    });

    // A phone keyboard resizes the window too, which must not close the sheet
    window.addEventListener('resize', function () { if (panel && !panel.classList.contains('is-sheet')) close(); });
    window.addEventListener('popstate', function () { refocus = null; close(); syncHeaders(); });
    document.addEventListener('listnav:loaded', function () {
        close();
        decorate();
        syncHeaders();
        // The reload replaced the header that was sorted or filtered from, so its new copy takes the focus it had
        const th = refocus && findHeader(refocus);
        if (th && focusIsFree()) focusQuietly(th.querySelector(refocus.control));
        refocus = null;
    });

    // Live updates and list navigation swap the table, so new headers are decorated as they appear
    new MutationObserver(function () {
        decorate();
        // An open panel follows its column into the new table, or closes when the column is gone
        if (!panel || panelHeader.isConnected) return;
        const th = findHeader(mark(panelHeader));
        const button = th && th.querySelector('.ip-th-filter');
        if (!button) { close(); return; }
        panelHeader = th;
        panelOpener = button;
        button.setAttribute('aria-expanded', 'true');
    }).observe(document.documentElement, { childList: true, subtree: true });

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

        // The PDF export reads which query values each control holds, so it does not repeat them from the headers
        Object.entries(fields).forEach(function ([selector, param]) {
            const select = document.querySelector(selector);
            if (select) select.dataset.query = param;
            if (select) select.addEventListener('change', function () { go({ [param]: select.value }); });
        });
        if (sortSelect) sortSelect.dataset.query = 'sort dir';
        if (search) search.dataset.query = 'search';
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
            // Left alone while it has focus, since the answer to an earlier search can arrive during typing
            if (search && document.activeElement !== search) search.value = p.get('search') || '';
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

    return { decorate: decorate, clear: clearAll, bindBar: bindBar, filters: activeFilters };
})();
