// Filter bar shared by the Products and Routes lists. The server does the filtering.
// Every change updates the list in place through ListNav and keeps the filters in the query string.

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

        // Department to categories and back, for the cascading dropdowns.
        const deptToCats = new Map();
        const catToDepts = new Map();
        (config.pairs || []).forEach(function (pair) {
            const dep = pair[0], cat = pair[1];
            if (!deptToCats.has(dep)) deptToCats.set(dep, new Set());
            if (!catToDepts.has(cat)) catToDepts.set(cat, new Set());
            deptToCats.get(dep).add(cat);
            catToDepts.get(cat).add(dep);
        });

        function selectedDepartment() {
            const value = $('#departmentFilter').val();
            return value ? toDepartmentKey(value) : null;
        }
        function selectedCategory() {
            const value = $('#categoryFilter').val();
            return value ? toCategoryKey(value) : null;
        }

        // ListNav swaps the list in place and the URL keeps the filters.
        function navigate(params, options) {
            ListNav.go(config.basePath + '?' + params.toString(), options);
        }

        function currentParams() {
            return new URLSearchParams(window.location.search);
        }

        // Builds the query from every active control, starting from page 1.
        function collect() {
            const params = new URLSearchParams();

            const search = ($('#searchInput').val() || '').trim();
            if (search) params.append('search', search);

            const range = DateRange.parse($('#dateRange').val());
            if (range) {
                params.append('startDate', DateRange.iso(range.start));
                params.append('endDate', DateRange.iso(range.end));
            }

            Object.keys(config.fields).forEach(function (param) {
                const value = $(config.fields[param]).val();
                if (value !== undefined && value !== null && value !== '') params.append(param, value);
            });

            // URL-only flags have no control, so they are carried over from the current URL.
            const current = currentParams();
            config.urlFlags.forEach(function (flag) {
                if (current.get(flag) === 'false') params.set(flag, 'false');
            });

            params.append('pageSize', $('#pageSizeFilter').val() || String(config.pageSize));
            params.append('pageNumber', '1');
            return params;
        }

        function apply() { navigate(collect()); }

        function restore() {
            const params = currentParams();

            const search = params.get('search');
            const input = document.getElementById('searchInput');
            // Leave the box alone while it has focus. Live search reloads the list during typing.
            if (input && document.activeElement !== input) input.value = search || '';

            if (params.get('startDate') && params.get('endDate'))
                DateRange.set('#dateRange', DateRange.fromIso(params.get('startDate')), DateRange.fromIso(params.get('endDate')));
            else
                DateRange.clear('#dateRange');

            // A param missing from the URL means All, since Back can return to a state without it.
            Object.keys(config.fields).forEach(function (param) {
                $(config.fields[param]).val(params.get(param) || '');
            });
        }

        // Shows only the categories found in the selected department.
        function cascadeCategoryOptions() {
            const dep = selectedDepartment();
            const allowed = dep === null ? null : (deptToCats.get(dep) || new Set());
            document.querySelectorAll('#categoryFilter option').forEach(function (opt) {
                opt.hidden = opt.value !== '' && allowed !== null && !allowed.has(toCategoryKey(opt.value));
            });
        }

        // Shows only the departments that hold the selected category.
        function cascadeDepartmentOptions() {
            const cat = selectedCategory();
            const allowed = cat === null ? null : (catToDepts.get(cat) || new Set());
            document.querySelectorAll('#departmentFilter option').forEach(function (opt) {
                opt.hidden = opt.value !== '' && allowed !== null && !allowed.has(toDepartmentKey(opt.value));
            });
        }

        // An incompatible pairing is dropped so the reload cannot land on an empty result.
        function onDepartmentChange() {
            const dep = selectedDepartment(), cat = selectedCategory();
            if (dep !== null && cat !== null && !(deptToCats.get(dep) || new Set()).has(cat)) {
                $('#categoryFilter').val('');
            }
            cascadeCategoryOptions();
            apply();
        }

        function onCategoryChange() {
            const cat = selectedCategory(), dep = selectedDepartment();
            if (cat !== null && dep !== null && !(catToDepts.get(cat) || new Set()).has(dep)) {
                $('#departmentFilter').val('');
            }
            cascadeDepartmentOptions();
            apply();
        }

        /** Removes one applied filter. The dates key removes both ends of the range. */
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
                // Picking a range applies it immediately, like the other filters.
                onApply: apply,
                // Clear reloads only if a date filter was actually set.
                onClear: function () {
                    if (currentParams().has('startDate')) remove('dates');
                }
            });

            $('#searchInput').on('keypress', function (e) {
                if (e.which === 13) apply();
            });

            restore();
            cascadeCategoryOptions();
            cascadeDepartmentOptions();

            // Tabs, paging and Back change the URL in place, so the controls are restored from it.
            document.addEventListener('listnav:loaded', function () {
                restore();
                cascadeCategoryOptions();
                cascadeDepartmentOptions();
            });
        });

        // Filter sheet for phones and tablets. It is built from the bar so filters are defined in one place.
        const bar = document.querySelector('.ip-filterbar');
        let sheetEl = null;
        // Marked before DOMContentLoaded so MobileLayout leaves this bar to the sheet.
        if (bar && config.sheet) bar.setAttribute('data-filter-sheet', '');

        function activeCount() {
            const params = collect();
            let n = 0;
            if (params.has('startDate')) n++;
            Object.keys(config.fields).forEach(function (p) {
                // Only filters shown in the bar count, so a status tab does not.
                if (params.has(p) && bar && bar.querySelector(config.fields[p])) n++;
            });
            config.urlFlags.forEach(function (f) { if (params.get(f) === 'false') n++; });
            return n;
        }

        function syncMoreButton() {
            const count = bar && bar.querySelector('.ip-filter-more .count');
            if (count) { const n = activeCount(); count.textContent = n ? String(n) : ''; }
        }

        function chip(group, value, text, active) {
            const b = document.createElement('button');
            b.type = 'button';
            b.className = 'ip-chip' + (active ? ' active' : '');
            b.dataset.group = group;
            b.dataset.value = value;
            b.textContent = text;
            return b;
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
            // Follow the bar's order, since config.fields can list them in any order.
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
            // A department and category pair with no products keeps only the department.
            const dep = selectedDepartment(), cat = selectedCategory();
            if (dep !== null && cat !== null && !(deptToCats.get(dep) || new Set()).has(cat)) $('#categoryFilter').val('');

            const params = collect();
            // Dates go straight into the query and restore() shows them in the picker after the reload.
            // The custom chip keeps the range picked in the bar.
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
                    if (group.hasAttribute('data-multi')) c.classList.toggle('active');
                    else group.querySelectorAll('.ip-chip').forEach(function (o) { o.classList.toggle('active', o === c); });
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
