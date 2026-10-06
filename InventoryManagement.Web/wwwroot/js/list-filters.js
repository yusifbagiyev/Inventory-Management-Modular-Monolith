// Products and Routes filter bar, each change reloads the list in place and keeps the filters in the URL

window.ListFilters = (function () {
    'use strict';

    function init(options) {
        const config = Object.assign({ categoryKey: 'id', departmentKey: 'id', fields: {}, urlFlags: [] }, options);
        const toCategoryKey = config.categoryKey === 'id'
            ? function (v) { return parseInt(v, 10); }
            : function (v) { return v; };
        const toDepartmentKey = config.departmentKey === 'id'
            ? function (v) { return parseInt(v, 10); }
            : function (v) { return v; };

        // Department to categories and back, for the cascading dropdowns
        const deptToCats = new Map();
        const catToDepts = new Map();
        (config.pairs || []).forEach(function (pair) {
            const dep = pair[0], cat = pair[1];
            if (!deptToCats.has(dep)) deptToCats.set(dep, new Set());
            if (!catToDepts.has(cat)) catToDepts.set(cat, new Set());
            deptToCats.get(dep).add(cat);
            catToDepts.get(cat).add(dep);
        });

        // Several values picked in a column header show as one option that keeps them all
        const SEVERAL = '__several';

        // Query values the bar has no control for, like column header filters and the sort, are carried over
        const owned = new Set(['search', 'startDate', 'endDate', 'pageSize', 'pageNumber']
            .concat(Object.keys(config.fields), config.urlFlags));

        // The PDF export reads which query values each control holds, so it does not repeat them from the headers
        Object.keys(config.fields).forEach(function (param) { $(config.fields[param]).attr('data-query', param); });
        $('#searchInput').attr('data-query', 'search');
        $('#dateRange').attr('data-query', 'startDate endDate');

        // The pairs were built for the other filters of the request that rendered the page, and the bar is never swapped
        const pairParams = config.pairParams || Object.keys(config.fields)
            .filter(function (param) { return !['#departmentFilter', '#categoryFilter'].includes(config.fields[param]); })
            .concat(config.urlFlags);
        function pairState(query) {
            const params = query || currentParams();
            return pairParams.map(function (param) { return params.getAll(param).join(','); }).join('|');
        }
        const pairsBuiltFor = pairState();

        /** False once a tab, state or flag of the address (or of the query given) differs from the page load, when the pairs no longer describe the list. */
        function pairsHold(query) { return pairState(query) === pairsBuiltFor; }

        function selectedDepartment() {
            const value = $('#departmentFilter').val();
            return value && value !== SEVERAL ? toDepartmentKey(value) : null;
        }
        function selectedCategory() {
            const value = $('#categoryFilter').val();
            return value && value !== SEVERAL ? toCategoryKey(value) : null;
        }

        function navigate(params, options) {
            ListNav.go(config.basePath + '?' + params.toString(), options);
        }

        function currentParams() {
            return new URLSearchParams(window.location.search);
        }

        /** Builds the query from every active control, starting from page 1. */
        function collect() {
            const params = new URLSearchParams();

            const search = ($('#searchInput').val() || '').trim();
            if (search) params.append('search', search);

            const range = DateRange.parse($('#dateRange').val());
            if (range) {
                params.append('startDate', DateRange.iso(range.start));
                params.append('endDate', DateRange.iso(range.end));
            }

            const current = currentParams();
            Object.keys(config.fields).forEach(function (param) {
                const value = $(config.fields[param]).val();
                // A list with nothing selected holds a value it has no option for, which stays as the URL has it
                if (value === SEVERAL || value === null) current.getAll(param).forEach(function (v) { params.append(param, v); });
                else if (value !== undefined && value !== '') params.append(param, value);
            });

            // URL-only flags have no control, so they are carried over from the current URL
            config.urlFlags.forEach(function (flag) {
                if (current.get(flag) === 'false') params.set(flag, 'false');
            });
            current.forEach(function (value, key) {
                if (!owned.has(key)) params.append(key, value);
            });

            params.append('pageSize', $('#pageSizeFilter').val() || String(config.pageSize));
            params.append('pageNumber', '1');
            return params;
        }

        // Arrow keys and typed letters change a closed select at every step, so a choice made from the keyboard
        // is applied after a pause, on Enter or when the field is left, not on every step
        let keyedSelect = null;
        let pending = null;
        function runPending() {
            if (!pending) return;
            const run = pending;
            pending = null;
            clearTimeout(run.timer);
            run();
        }
        function apply() {
            const run = function () { navigate(collect()); };
            if (keyedSelect && document.activeElement === keyedSelect) {
                if (pending) clearTimeout(pending.timer);
                pending = run;
                run.timer = setTimeout(runPending, 900);
                return;
            }
            run();
        }
        Object.keys(config.fields).forEach(function (param) {
            const select = document.querySelector(config.fields[param]);
            if (!select || select.tagName !== 'SELECT') return;
            select.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') { keyedSelect = null; runPending(); return; }
                if (e.key !== 'Tab' && e.key !== 'Escape') keyedSelect = select;
            });
            select.addEventListener('pointerdown', function () { keyedSelect = null; });
            select.addEventListener('blur', function () { keyedSelect = null; runPending(); });
        });

        /** The name a column header gives a value, for options the bar's own list lacks. */
        function optionLabel(param, value) {
            const th = Array.from(document.querySelectorAll('th[data-options]')).find(function (h) { return h.dataset.param === param; });
            let options = [];
            try { options = th ? JSON.parse(th.dataset.options) : []; } catch (e) { options = []; }
            const match = options.find(function (o) { return String(o.value) === value; });
            return match ? match.label : value;
        }

        function restore() {
            const params = currentParams();

            const search = params.get('search');
            const input = document.getElementById('searchInput');
            // Leave the box alone while it has focus, since live search reloads the list during typing
            if (input && document.activeElement !== input) input.value = search || '';

            if (params.get('startDate') && params.get('endDate'))
                DateRange.set('#dateRange', DateRange.fromIso(params.get('startDate')), DateRange.fromIso(params.get('endDate')));
            else
                DateRange.clear('#dateRange');

            // A param missing from the URL means All, since Back can return to a state without it
            Object.keys(config.fields).forEach(function (param) {
                const select = $(config.fields[param]);
                const values = params.getAll(param);
                select.find('option[value="' + SEVERAL + '"], option[data-carried]').remove();
                if (values.length > 1) {
                    select.append($('<option>').val(SEVERAL).text(t('{0} selected', values.length)));
                    select.val(SEVERAL);
                } else {
                    const value = values[0] || '';
                    // The list is the one of the page load, so a value it lacks gets an option instead of being dropped
                    if (value && select.length && !Array.from(select[0].options).some(function (o) { return o.value === value; }))
                        select.append($('<option data-carried>').val(value).text(optionLabel(param, value)));
                    select.val(value);
                }
            });
        }

        // Shows only the categories found in the selected department
        function cascadeCategoryOptions() {
            const dep = selectedDepartment();
            const allowed = dep === null || !pairsHold() ? null : (deptToCats.get(dep) || new Set());
            document.querySelectorAll('#categoryFilter option').forEach(function (opt) {
                opt.hidden = opt.value !== '' && opt.value !== SEVERAL && !opt.selected && allowed !== null && !allowed.has(toCategoryKey(opt.value));
            });
        }

        // Shows only the departments that hold the selected category
        function cascadeDepartmentOptions() {
            const cat = selectedCategory();
            const allowed = cat === null || !pairsHold() ? null : (catToDepts.get(cat) || new Set());
            document.querySelectorAll('#departmentFilter option').forEach(function (opt) {
                opt.hidden = opt.value !== '' && opt.value !== SEVERAL && !opt.selected && allowed !== null && !allowed.has(toDepartmentKey(opt.value));
            });
        }

        /** True when the pairs still describe the list and say this department holds nothing of this category. */
        function incompatible(dep, cat, query) {
            return dep !== null && cat !== null && pairsHold(query) && !(deptToCats.get(dep) || new Set()).has(cat);
        }

        // An incompatible pairing is dropped so the reload cannot land on an empty result
        function onDepartmentChange() {
            if (incompatible(selectedDepartment(), selectedCategory())) $('#categoryFilter').val('');
            cascadeCategoryOptions();
            apply();
        }

        function onCategoryChange() {
            if (incompatible(selectedDepartment(), selectedCategory())) $('#departmentFilter').val('');
            cascadeDepartmentOptions();
            apply();
        }

        /** Removes one applied filter, where the dates key removes both ends of the range. */
        function remove(key) {
            const params = currentParams();
            if (key === 'dates') {
                params.delete('startDate');
                params.delete('endDate');
            } else {
                params.delete(key);
            }
            params.set('pageNumber', '1');
            navigate(params);
        }

        /** Flips a URL-only flag while keeping every other filter. */
        function toggleFlag(flag) {
            const params = collect();
            if (params.get(flag) === 'false') params.delete(flag);
            else params.set(flag, 'false');
            navigate(params);
        }

        function reset() {
            navigate(new URLSearchParams({ pageSize: String(config.pageSize), pageNumber: '1' }));
        }

        $(function () {
            DateRange.attach('#dateRange', {
                position: 'bottom right',
                onApply: apply,
                // Clear reloads only if a date filter was actually set
                onClear: function () {
                    if (currentParams().has('startDate')) remove('dates');
                }
            });

            restore();
            cascadeCategoryOptions();
            cascadeDepartmentOptions();

            // Tabs, paging and Back change the URL in place, so the controls are restored from it
            document.addEventListener('listnav:loaded', function () {
                restore();
                cascadeCategoryOptions();
                cascadeDepartmentOptions();
            });
        });

        // Filter sheet for phones and tablets, built from the bar so filters are defined in one place
        const bar = document.querySelector('.ip-filterbar');
        let sheetEl = null;
        // Marked before DOMContentLoaded so MobileLayout leaves this bar to the sheet
        if (bar && config.sheet) bar.setAttribute('data-filter-sheet', '');

        /** Filters set in a column header that the bar has no control for, which a phone cannot see or change otherwise. */
        function headerFilters() {
            return TableColumns.filters(document.querySelector('[data-list-region]'))
                .filter(function (f) { return f.params.length && f.params.every(function (name) { return !owned.has(name); }); });
        }

        function activeCount() {
            const params = collect();
            let n = 0;
            if (params.has('startDate')) n++;
            Object.keys(config.fields).forEach(function (p) {
                // Only filters shown in the bar count, so a status tab does not
                if (params.has(p) && bar && bar.querySelector(config.fields[p])) n++;
            });
            config.urlFlags.forEach(function (f) { if (params.get(f) === 'false') n++; });
            return n + headerFilters().length;
        }

        function syncMoreButton() {
            const count = bar && bar.querySelector('.ip-filter-more .count');
            if (count) { const n = activeCount(); count.textContent = n ? String(n) : ''; }
        }

        function press(b, on) {
            b.classList.toggle('active', on);
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
        }

        function chip(group, value, text, active) {
            const b = document.createElement('button');
            b.type = 'button';
            b.className = 'ip-chip';
            b.dataset.group = group;
            b.dataset.value = value;
            const name = document.createElement('span');
            name.textContent = text;
            b.appendChild(name);
            press(b, !!active);
            return b;
        }

        /** Shows the order a sort chip stands for as an arrow, or none while another column sorts the list. */
        function direct(b, dir) {
            b.dataset.dir = dir;
            b.querySelectorAll('i, .visually-hidden').forEach(function (el) { el.remove(); });
            if (!dir) return;
            b.insertAdjacentHTML('beforeend', '<i class="fa-solid ' + (dir === 'desc' ? 'fa-arrow-down' : 'fa-arrow-up') + ' ms-2" aria-hidden="true"></i>'
                + '<span class="visually-hidden">, ' + escapeHtml(dir === 'desc' ? t('Descending') : t('Ascending')) + '</span>');
        }

        // Phones hide the table headers, so their sorting is offered here as one chip for each sortable column
        function sortChips() {
            const params = currentParams();
            const sort = params.get('sort') || '', dir = params.get('dir') === 'desc' ? 'desc' : 'asc';
            const chips = [chip('sort', '', t('Default order'), !sort)];
            document.querySelectorAll('[data-list-region] th[data-sort]').forEach(function (th) {
                const b = chip('sort', th.dataset.sort, th.dataset.label || th.textContent.trim(), th.dataset.sort === sort);
                b.dataset.first = th.dataset.sortFirst === 'desc' ? 'desc' : 'asc';
                direct(b, th.dataset.sort === sort ? dir : '');
                chips.push(b);
            });
            return chips;
        }

        function section(title, chips, multi) {
            const s = document.createElement('section');
            s.className = 'mb-4';
            const h = document.createElement('div');
            h.className = 'ip-label mb-2';
            h.textContent = title;
            const wrap = document.createElement('div');
            wrap.className = 'ip-chips';
            if (multi) wrap.dataset.multi = '';
            chips.forEach(function (c) { wrap.appendChild(c); });
            s.append(h, wrap);
            return s;
        }

        const DAY = 86400000;
        function today() { const d = new Date(); d.setHours(0, 0, 0, 0); return d; }
        const datePresets = [
            { key: '', label: function () { return t('Any date'); } },
            { key: '7', label: function () { return t('Last 7 days'); }, start: function () { return new Date(today() - 6 * DAY); } },
            { key: '30', label: function () { return t('Last 30 days'); }, start: function () { return new Date(today() - 29 * DAY); } },
            { key: '90', label: function () { return t('Last 90 days'); }, start: function () { return new Date(today() - 89 * DAY); } },
            { key: 'year', label: function () { return t('This year'); }, start: function () { return new Date(today().getFullYear(), 0, 1); } }
        ];

        /** Rebuilds the sheet's chips from the bar's current state. */
        function fillSheet() {
            const body = sheetEl.querySelector('.modal-body');
            body.innerHTML = '';
            const sorting = sortChips();
            if (sorting.length > 1) body.appendChild(section(t('Sort'), sorting, false));
            // Follow the bar's order, since config.fields can list them in any order
            const inBar = Object.keys(config.fields)
                .map(function (param) { return { param: param, select: bar.querySelector(config.fields[param]) }; })
                .filter(function (f) { return f.select; })
                .sort(function (a, b) { return a.select.compareDocumentPosition(b.select) & Node.DOCUMENT_POSITION_FOLLOWING ? -1 : 1; });
            inBar.forEach(function (f) {
                const param = f.param, select = f.select;
                const label = select.closest('.ip-filter')?.querySelector('span')?.textContent.trim() || param;
                const chips = Array.from(select.options).map(function (o) {
                    return chip(param, o.value, o.textContent.trim(), select.value === o.value);
                });
                body.appendChild(section(label, chips, false));
            });
            if (bar.querySelector('#dateRange')) {
                const range = DateRange.parse($('#dateRange').val());
                const chips = datePresets.map(function (p) { return chip('date', p.key, p.label(), !range && p.key === ''); });
                if (range) chips.push(chip('date', 'custom', $('#dateRange').val(), true));
                body.appendChild(section(t('Created'), chips, false));
            }
            const flagButtons = bar.querySelectorAll('[data-flag]');
            if (flagButtons.length) {
                const chips = Array.from(flagButtons).map(function (b) {
                    return chip('flag', b.dataset.flag, b.textContent.trim(), b.classList.contains('is-on'));
                });
                body.appendChild(section(t('Other'), chips, true));
            }
            // Each of these stays on until it is tapped off, and then Show results drops it
            const fromHeaders = headerFilters().map(function (f) {
                const b = chip('header', f.params.join(','), f.label + ': ' + f.text, true);
                b.insertAdjacentHTML('beforeend', '<i class="fa-solid fa-xmark ms-2" aria-hidden="true"></i>');
                return b;
            });
            if (fromHeaders.length) body.appendChild(section(t('Column filters'), fromHeaders, true));
        }

        function applySheet() {
            const pick = function (group) {
                const c = sheetEl.querySelector('.ip-chip.active[data-group="' + group + '"]');
                return c ? c.dataset.value : '';
            };
            Object.keys(config.fields).forEach(function (param) {
                const select = bar.querySelector(config.fields[param]);
                if (select) select.value = pick(param);
            });
            const params = collect();
            // Presets go straight into the query and the custom chip keeps the range picked in the bar
            const date = pick('date');
            const preset = datePresets.find(function (p) { return p.key === date; });
            if (preset && preset.start) {
                params.set('startDate', DateRange.iso(preset.start()));
                params.set('endDate', DateRange.iso(today()));
            } else if (date === '') {
                params.delete('startDate');
                params.delete('endDate');
            }
            config.urlFlags.forEach(function (flag) {
                const on = !!sheetEl.querySelector('.ip-chip.active[data-group="flag"][data-value="' + flag + '"]');
                if (on) params.set(flag, 'false'); else params.delete(flag);
            });
            // A department and category pair with no products keeps only the department, judged by the state and flags the sheet applies
            if (incompatible(selectedDepartment(), selectedCategory(), params)) {
                $('#categoryFilter').val('');
                Object.keys(config.fields).forEach(function (param) { if (config.fields[param] === '#categoryFilter') params.delete(param); });
            }
            sheetEl.querySelectorAll('.ip-chip[data-group="header"]:not(.active)').forEach(function (c) {
                c.dataset.value.split(',').forEach(function (name) { params.delete(name); });
            });
            const sorted = sheetEl.querySelector('.ip-chip.active[data-group="sort"]');
            if (sorted) {
                params.delete('sort');
                params.delete('dir');
                if (sorted.dataset.value) { params.set('sort', sorted.dataset.value); params.set('dir', sorted.dataset.dir); }
            }
            bootstrap.Modal.getOrCreateInstance(sheetEl).hide();
            navigate(params);
        }

        function buildSheet() {
            if (!bar || !config.sheet || bar.querySelector('.ip-filter-more')) return;
            const more = document.createElement('button');
            more.type = 'button';
            more.className = 'ip-filter-more';
            more.innerHTML = '<i class="fa-solid fa-sliders" aria-hidden="true"></i><span></span><span class="count"></span>';
            more.querySelector('span').textContent = t('Filter');
            const search = bar.querySelector(':scope > .search');
            if (search) search.after(more); else bar.prepend(more);

            sheetEl = document.createElement('div');
            sheetEl.className = 'modal fade ip-modal';
            sheetEl.id = 'filterSheet';
            sheetEl.tabIndex = -1;
            sheetEl.setAttribute('aria-hidden', 'true');
            sheetEl.innerHTML =
                '<div class="modal-dialog modal-dialog-scrollable"><div class="modal-content">' +
                '<div class="modal-header"><h2 class="modal-title flex-grow-1"></h2>' +
                '<button type="button" class="ip-btn ip-btn-ghost ip-btn-sm" data-filter-reset></button></div>' +
                '<div class="modal-body"></div>' +
                '<div class="modal-footer"><button type="button" class="ip-btn ip-btn-secondary" data-bs-dismiss="modal"></button>' +
                '<button type="button" class="ip-btn ip-btn-primary" data-filter-apply></button></div>' +
                '</div></div>';
            sheetEl.querySelector('.modal-title').textContent = t('Filters');
            sheetEl.querySelector('[data-filter-reset]').textContent = t('Reset');
            sheetEl.querySelector('[data-bs-dismiss]').textContent = t('Close');
            sheetEl.querySelector('[data-filter-apply]').textContent = t('Show results');
            document.body.appendChild(sheetEl);

            more.addEventListener('click', function () {
                fillSheet();
                bootstrap.Modal.getOrCreateInstance(sheetEl).show();
            });
            sheetEl.addEventListener('click', function (e) {
                const c = e.target.closest('.ip-chip');
                if (c) {
                    const group = c.parentElement;
                    if (c.dataset.group === 'sort') {
                        // A second tap on the chosen column turns its order around
                        const dir = !c.dataset.value ? '' : !c.classList.contains('active') ? c.dataset.first : c.dataset.dir === 'asc' ? 'desc' : 'asc';
                        group.querySelectorAll('.ip-chip').forEach(function (o) { direct(o, o === c ? dir : ''); });
                    }
                    if (group.hasAttribute('data-multi')) press(c, !c.classList.contains('active'));
                    else group.querySelectorAll('.ip-chip').forEach(function (o) { press(o, o === c); });
                    if (c.dataset.group === 'header') c.querySelector('i').hidden = !c.classList.contains('active');
                    return;
                }
                if (e.target.closest('[data-filter-apply]')) applySheet();
                if (e.target.closest('[data-filter-reset]')) { bootstrap.Modal.getOrCreateInstance(sheetEl).hide(); reset(); }
            });
            syncMoreButton();
        }

        $(function () {
            buildSheet();
            document.addEventListener('listnav:loaded', syncMoreButton);
        });

        return {
            apply: apply,
            collect: collect,
            remove: remove,
            toggleFlag: toggleFlag,
            reset: reset,
            onDepartmentChange: onDepartmentChange,
            onCategoryChange: onCategoryChange
        };
    }

    return { init: init };
})();
