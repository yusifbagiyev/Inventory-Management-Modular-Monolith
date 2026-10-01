// InventoryManagement.Web/wwwroot/js/list-filters.js
//
// The filter panel shared by the Products and Routes lists: search (Enter applies), a date range
// picker, dropdowns mapped to query parameters, the department <-> category cascade, removable
// chips and reset (paging is changePage in site.js). Every change reloads the page with the filters in the query string;
// the server does the filtering.
//
//   const filters = ListFilters.init({
//       basePath: '/Products',
//       pageSize: 30,
//       pairs: [[deptId, categoryKey], ...],      // facet pairs from the server
//       categoryKey: 'id' | 'name',               // what #categoryFilter option values hold
//       departmentKey: 'id' | 'name',             // what #departmentFilter option values hold
//       fields: { status: '#statusFilter', categoryId: '#categoryFilter', ... },  // param -> select
//       urlFlags: ['hasImage', 'assigned']        // URL-only flags kept across changes when 'false'
//   });
//
// Requires jQuery, air-datepicker.js and date-range.js on the page.

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

        // department id -> Set(category key) and back, for the cascading dropdowns.
        const deptToCats = new Map();
        const catToDepts = new Map();
        (config.pairs || []).forEach(function (pair) {
            const dep = pair[0], cat = pair[1];
            if (!deptToCats.has(dep)) deptToCats.set(dep, new Set());
            if (!catToDepts.has(cat)) catToDepts.set(cat, new Set());
            deptToCats.get(dep).add(cat);
            catToDepts.get(cat).add(dep);
        });

        // null when no department is chosen.
        function selectedDepartment() {
            const value = $('#departmentFilter').val();
            return value ? toDepartmentKey(value) : null;
        }
        function selectedCategory() {
            const value = $('#categoryFilter').val();
            return value ? toCategoryKey(value) : null;
        }

        // In place (ListNav swaps the list, tabs and counts); the URL keeps the filters.
        function navigate(params, options) {
            ListNav.go(config.basePath + '?' + params.toString(), options);
        }

        function currentParams() {
            return new URLSearchParams(window.location.search);
        }

        // Every active control as query parameters, starting from page 1.
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

            // URL-only quick flags have no control; carry them over from the current URL.
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
            // Never under the cursor of someone typing (live search reloads the list as they type).
            if (input && document.activeElement !== input) input.value = search || '';

            if (params.get('startDate') && params.get('endDate'))
                DateRange.set('#dateRange', DateRange.fromIso(params.get('startDate')), DateRange.fromIso(params.get('endDate')));
            else
                DateRange.clear('#dateRange');

            // Absent from the URL = "All" (Back can return to a state without it).
            Object.keys(config.fields).forEach(function (param) {
                $(config.fields[param]).val(params.get(param) || '');
            });
        }

        // Show only the categories present in the selected department (all when none).
        function cascadeCategoryOptions() {
            const dep = selectedDepartment();
            const allowed = dep === null ? null : (deptToCats.get(dep) || new Set());
            document.querySelectorAll('#categoryFilter option').forEach(function (opt) {
                opt.hidden = opt.value !== '' && allowed !== null && !allowed.has(toCategoryKey(opt.value));
            });
        }

        // Show only the departments that contain the selected category (all when none).
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

        /** Removes one applied filter ('dates' removes both ends of the range). */
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

        /** Flips a URL-only flag (e.g. hasImage=false) while keeping every other filter. */
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
                // "Clear" drops the date filter (reloads only if one was set).
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

            // A tab, page or the back button changed the URL in place: show its filters.
            document.addEventListener('listnav:loaded', function () {
                restore();
                cascadeCategoryOptions();
                cascadeDepartmentOptions();
            });
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
