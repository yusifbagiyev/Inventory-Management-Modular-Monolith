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
//       fields: { status: '#statusFilter', categoryId: '#categoryFilter', ... },  // param -> select
//       urlFlags: ['hasImage', 'assigned']        // URL-only flags kept across changes when 'false'
//   });
//
// Requires jQuery, air-datepicker.js and date-range.js on the page.

window.ListFilters = (function () {
    'use strict';

    function init(options) {
        const config = Object.assign({ categoryKey: 'id', fields: {}, urlFlags: [] }, options);
        const toCategoryKey = config.categoryKey === 'id'
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

        function selectedDepartment() { return parseInt($('#departmentFilter').val(), 10); }
        function selectedCategory() {
            const value = $('#categoryFilter').val();
            return value ? toCategoryKey(value) : null;
        }

        function navigate(params) {
            if (window.showListSkeleton) showListSkeleton();
            window.location.href = config.basePath + '?' + params.toString();
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
            if (search) $('#searchInput').val(search);

            DateRange.set('#dateRange', DateRange.fromIso(params.get('startDate')), DateRange.fromIso(params.get('endDate')));

            Object.keys(config.fields).forEach(function (param) {
                const value = params.get(param);
                if (value !== null) $(config.fields[param]).val(value);
            });
        }

        // Show only the categories present in the selected department (all when none).
        function cascadeCategoryOptions() {
            const dep = selectedDepartment();
            const allowed = Number.isNaN(dep) ? null : (deptToCats.get(dep) || new Set());
            document.querySelectorAll('#categoryFilter option').forEach(function (opt) {
                opt.hidden = opt.value !== '' && allowed !== null && !allowed.has(toCategoryKey(opt.value));
            });
        }

        // Show only the departments that contain the selected category (all when none).
        function cascadeDepartmentOptions() {
            const cat = selectedCategory();
            const allowed = cat === null ? null : (catToDepts.get(cat) || new Set());
            document.querySelectorAll('#departmentFilter option').forEach(function (opt) {
                opt.hidden = opt.value !== '' && allowed !== null && !allowed.has(parseInt(opt.value, 10));
            });
        }

        // An incompatible pairing is dropped so the reload cannot land on an empty result.
        function onDepartmentChange() {
            const dep = selectedDepartment(), cat = selectedCategory();
            if (!Number.isNaN(dep) && cat !== null && !(deptToCats.get(dep) || new Set()).has(cat)) {
                $('#categoryFilter').val('');
            }
            cascadeCategoryOptions();
            apply();
        }

        function onCategoryChange() {
            const cat = selectedCategory(), dep = selectedDepartment();
            if (cat !== null && !Number.isNaN(dep) && !(catToDepts.get(cat) || new Set()).has(dep)) {
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

        /** Removes one word from a multi-word search (its chip's x). */
        function removeWord(word) {
            const params = currentParams();
            const remaining = (params.get('search') || '')
                .split(/\s+/)
                .filter(function (w) { return w && w.toLowerCase() !== word.toLowerCase(); });
            if (remaining.length) params.set('search', remaining.join(' '));
            else params.delete('search');
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

            document.querySelectorAll('.achip-word').forEach(function (chip) {
                chip.addEventListener('click', function () { removeWord(chip.dataset.word); });
            });

            restore();
            cascadeCategoryOptions();
            cascadeDepartmentOptions();
        });

        return {
            apply: apply,
            collect: collect,
            remove: remove,
            removeWord: removeWord,
            toggleFlag: toggleFlag,
            reset: reset,
            onDepartmentChange: onDepartmentChange,
            onCategoryChange: onCategoryChange
        };
    }

    return { init: init };
})();
