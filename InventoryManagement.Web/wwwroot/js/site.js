/** Translates a UI string keyed by its English text, so a missing key shows in English. */
function t(key, ...args) {
    const table = window.I18n || {};
    const text = Object.prototype.hasOwnProperty.call(table, key) ? table[key] : key;
    return args.length ? String(text).replace(/\{(\d+)\}/g, (m, i) => (args[i] ?? m)) : text;
}

/** Locale for Intl and toLocale*String, following the UI language. */
function uiLocale() {
    return ({ az: 'az-Latn-AZ', ru: 'ru-RU' })[document.documentElement.lang] || 'en-US';
}

/** Formats as dd.MM.yyyy with optional HH:mm, the only date format the UI uses. */
function formatDate(value, withTime) {
    if (!value) return '';
    const d = value instanceof Date ? value : new Date(value);
    if (isNaN(d)) return '';
    const pad = n => String(n).padStart(2, '0');
    const date = `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()}`;
    return withTime ? `${date} ${pad(d.getHours())}:${pad(d.getMinutes())}` : date;
}

// DataTables texts for every table, though a page's own language option still wins
if (window.DataTable && window.DataTable.defaults) {
    window.DataTable.defaults.language = Object.assign({}, window.DataTable.defaults.language, {
        search: t('Search:'),
        searchPlaceholder: t('Search...'),
        lengthMenu: t('Show _MENU_ entries'),
        info: t('Showing _START_ to _END_ of _TOTAL_ entries'),
        infoEmpty: t('Showing 0 to 0 of 0 entries'),
        infoFiltered: t('(filtered from _MAX_ total entries)'),
        zeroRecords: t('No matching records found'),
        emptyTable: t('No data available in table'),
        loadingRecords: t('Loading...'),
        processing: t('Processing...'),
        paginate: { first: t('First'), last: t('Last'), next: t('Next'), previous: t('Previous') },
        aria: { orderable: t('Activate to sort'), orderableReverse: t('Activate to invert sorting'), orderableRemove: t('Activate to remove sorting') }
    });
}

/** In-page confirm dialog that falls back to window.confirm on pages without the shared modal. */
function confirmAction(options, onConfirm) {
    const opts = typeof options === 'string' ? { message: options } : (options || {});
    const modalEl = document.getElementById('globalConfirmModal');

    if (!modalEl || typeof bootstrap === 'undefined') {
        if (window.confirm(opts.message || t('Are you sure?'))) onConfirm?.();
        return;
    }

    const titleEl = document.getElementById('globalConfirmTitle');
    const msgEl = document.getElementById('globalConfirmMessage');
    const okBtn = document.getElementById('globalConfirmOk');

    titleEl.textContent = opts.title || t('Please confirm');
    msgEl.textContent = opts.message || t('Are you sure?');
    okBtn.textContent = opts.okText || t('Confirm');
    okBtn.className = 'ip-btn ' + (opts.danger ? 'ip-btn-danger' : 'ip-btn-primary');

    // Rebuild the OK button so a previous dialog's handler can never fire for this one
    const freshOk = okBtn.cloneNode(true);
    okBtn.parentNode.replaceChild(freshOk, okBtn);

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);

    // Earlier dialogs' listeners can still fire on the shared modal, so a per-call token picks the current one
    const token = Symbol('confirm');
    modalEl.__confirmToken = token;
    modalEl.__confirmAccepted = false;

    freshOk.addEventListener('click', function () {
        modalEl.__confirmAccepted = true;
        modal.hide();
    });

    function onKeydown(e) {
        if (e.key === 'Enter') {
            e.preventDefault();
            freshOk.click();
        }
    }
    modalEl.addEventListener('keydown', onKeydown);

    modalEl.addEventListener('shown.bs.modal', function onShown() {
        freshOk.focus();
        modalEl.removeEventListener('shown.bs.modal', onShown);
    });

    modalEl.addEventListener('hidden.bs.modal', function onHidden() {
        modalEl.removeEventListener('keydown', onKeydown);
        modalEl.removeEventListener('hidden.bs.modal', onHidden);
        if (modalEl.__confirmToken !== token) return;   // A newer dialog owns the modal now
        if (modalEl.__confirmAccepted) onConfirm?.();   // Runs after the modal is gone so redirects are clean
    });

    // Showing while the previous dialog is still hiding leaves Bootstrap stuck on its backdrop
    if (modalEl.classList.contains('show')) {
        modalEl.addEventListener('hidden.bs.modal', () => modal.show(), { once: true });
    } else {
        modal.show();
    }
}

document.addEventListener('DOMContentLoaded', function () {
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // Layout forms like sign out navigate at once, so their buttons get no spinner
    const forms = document.querySelectorAll('form:not(.no-spinner):not([data-no-ajax])');
    forms.forEach(function (form) {
        form.addEventListener('submit', function () {
            const submitBtn = form.querySelector('button[type="submit"]');
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>' + escapeHtml(t('Processing...'));
            }
        });
    });

    setupSessionMonitor();
});

/** Server-made thumbnail URL for an uploaded photo, leaving other URLs unchanged. */
function thumbUrl(url, width) {
    return typeof url === 'string' && url.startsWith('/images/') ? '/thumbs/' + (width || 160) + url : url;
}

function showToast(message, type = 'info', duration = 4000) {
    message = typeof message === 'string' ? t(message) : message;
    const validTypes = ['success', 'error', 'danger', 'warning', 'info', 'secondary'];
    if (!validTypes.includes(type)) {
        type = 'info';
    }

    if (type === 'error') {
        type = 'danger';
    }

    const toastId = 'toast-' + Date.now() + '-' + Math.random().toString(36).substr(2, 9);

    const icon = getToastIcon(type);

    const toastHtml = `
        <div id="${toastId}" class="toast ip-toast ${type}" role="${type === 'danger' ? 'alert' : 'status'}" aria-live="${type === 'danger' ? 'assertive' : 'polite'}" aria-atomic="true">
            <div class="toast-body">
                <i class="fa-solid fa-${icon}"></i>
                <div class="text flex-grow-1">${escapeHtml(message)}</div>
                <button type="button" class="btn-close" data-bs-dismiss="toast" aria-label="${escapeHtml(t('Close'))}"></button>
            </div>
        </div>
    `;

    let container = document.getElementById('toastContainer');
    if (!container) {
        container = document.createElement('div');
        container.id = 'toastContainer';
        document.body.appendChild(container);
    }
    container.className = 'toast-container position-fixed bottom-0 end-0 p-4';

    container.insertAdjacentHTML('beforeend', toastHtml);

    const toastElement = document.getElementById(toastId);
    const toast = new bootstrap.Toast(toastElement, {
        delay: duration,
        autohide: true
    });
    toast.show();

    toastElement.addEventListener('hidden.bs.toast', function () {
        toastElement.remove();
    });

    // Returned so callers can attach their own handlers, like click-to-refresh
    return toastElement;
}


function getToastIcon(type) {
    const icons = {
        'success': 'circle-check',
        'danger': 'circle-xmark',
        'warning': 'triangle-exclamation',
        'info': 'circle-info',
        'secondary': 'circle-info'
    };
    return icons[type] || 'circle-info';
}


/** HTML-escapes text for markup, turning null and undefined into an empty string. */
function escapeHtml(value) {
    if (value === null || value === undefined) return '';
    return String(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}


// Pings every 5 minutes so an ended session sends the page to sign-in instead of failing later
function setupSessionMonitor() {
    const isUserAuthenticated = document.getElementById('ipRail') !== null;

    if (!isUserAuthenticated) {
        return;
    }


    const monitorInterval = setInterval(async function () {
        try {
            const response = await fetch('/Account/Ping', {
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                credentials: 'same-origin'
            });

            if (response.status === 401) {
                clearInterval(monitorInterval);
                showToast(t('Your session has expired. Please log in again.'), 'warning');
                setTimeout(() => {
                    window.location.href = '/Account/Login?returnUrl=' +
                        encodeURIComponent(window.location.pathname);
                }, 2000);
            }
        } catch (error) {
            console.error('Session check failed:', error);
        }
    }, 5 * 60 * 1000);

    window.sessionMonitorInterval = monitorInterval;
}


/** Replaces the list's rows with skeleton rows while its next state loads. */
function showListSkeleton() {
    const region = document.querySelector('[data-list-region]');
    if (!region) return;
    const bar = width => `<span class="ip-skeleton" style="width: ${width}; height: 12px"></span>`;
    const rows = Array.from({ length: 6 }, () =>
        `<div class="ip-skeleton-row"><span class="ip-skeleton" style="width: 44px; height: 44px; border-radius: 10px; flex: none"></span>${bar('10%')}${bar('26%')}${bar('20%')}${bar('12%')}</div>`).join('');
    region.setAttribute('aria-busy', 'true');
    region.innerHTML = `<div role="status"><span class="visually-hidden">${escapeHtml(t('Loading...'))}</span>${rows}</div>`;
}

/** Updates a list in place for tabs, paging, filters and search, and keeps the URL in step. */
window.ListNav = (function () {
    'use strict';

    // Filter controls are never swapped, so typing and open pickers are not disturbed
    const SWAPPED = ['[data-list-region]', '[data-list-tabs]', '.ip-page-head .ip-sub'];
    let pending = null;
    let loadedAt = 0;

    function available() { return !!document.querySelector('[data-list-region]'); }

    function go(url, options) {
        options = options || {};
        const target = new URL(url, window.location.href);
        if (!available() || target.pathname !== window.location.pathname) {
            window.location.href = target.href;
            return;
        }
        if (target.href === window.location.href && !options.force) return;
        history[options.replace ? 'replaceState' : 'pushState']({ listNav: true }, '', target.href);
        load(options);
    }

    async function load(options) {
        options = options || {};
        if (pending) pending.abort();
        const request = pending = new AbortController();
        const region = document.querySelector('[data-list-region]');
        if (options.quiet) region?.classList.add('is-loading'); else showListSkeleton();

        let doc;
        try {
            const response = await fetch(window.location.href, {
                credentials: 'same-origin',
                headers: { 'Accept': 'text/html' },
                signal: request.signal
            });
            // A redirect or failure means sign-out or lost access, so let the browser show the real page
            if (!response.ok || new URL(response.url).pathname !== window.location.pathname) throw new Error('reload');
            doc = new DOMParser().parseFromString(await response.text(), 'text/html');
        } catch (e) {
            if (e.name !== 'AbortError') window.location.reload();
            return;
        }
        if (request !== pending) return;
        pending = null;
        loadedAt = Date.now();

        if (window.LiveUpdates) LiveUpdates.beforeSwap();
        const swapped = new Set();
        const selectors = SWAPPED.concat(window.LiveUpdates ? LiveUpdates.regions() : []);
        selectors.forEach(function (selector) {
            const fresh = doc.querySelectorAll(selector);
            document.querySelectorAll(selector).forEach(function (current, i) {
                if (swapped.has(current) || !fresh[i]) return;
                const node = document.importNode(fresh[i], true);
                current.replaceWith(node);
                swapped.add(node);
            });
        });
        syncFilterButtons(doc);
        document.title = doc.title;
        if (window.LiveUpdates) LiveUpdates.afterSwap();
        document.dispatchEvent(new CustomEvent('listnav:loaded'));

        // New results start at the top of the list, not where the old ones were scrolled to
        const list = document.querySelector('[data-list-region]');
        if (!options.quiet && list && list.getBoundingClientRect().top < 0)
            list.scrollIntoView({ block: 'start', behavior: 'smooth' });
    }

    // Quick filter and Reset buttons depend on the filters, so they come from the new page
    function syncFilterButtons(doc) {
        const fresh = doc.querySelectorAll('.ip-filterbar');
        document.querySelectorAll('.ip-filterbar').forEach(function (bar, i) {
            if (!fresh[i]) return;
            const isButton = el => el.matches('a.ip-btn, button.ip-btn') && !el.classList.contains('ip-filter-toggle');
            Array.from(bar.children).filter(isButton).forEach(el => el.remove());
            Array.from(fresh[i].children).filter(isButton).forEach(el => bar.appendChild(document.importNode(el, true)));
        });
    }

    // Tab, pager, reset and empty-state links stay on this page with a new query
    document.addEventListener('click', function (e) {
        const link = e.target.closest('.ip-table-foot a[href], [data-list-tabs] a[href], .ip-filterbar a[href], [data-list-region] .ip-empty a[href]');
        if (!link || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || link.target) return;
        if (new URL(link.href, window.location.href).pathname !== window.location.pathname || !available()) return;
        e.preventDefault();
        go(link.href);
    });

    // GET filter forms, like the audit log's, submit in place
    document.addEventListener('submit', function (e) {
        const form = e.target;
        if (!form.matches('form.ip-filterbar') || (form.method || 'get').toLowerCase() !== 'get' || !available()) return;
        e.preventDefault();
        const params = new URLSearchParams(new FormData(form));
        Array.from(params.keys()).forEach(k => { if (!params.get(k)) params.delete(k); });
        go(window.location.pathname + '?' + params.toString());
    });

    // Search as you type replaces the history entry so Back does not step through every letter
    let typing = null;
    function searchNow(input) {
        clearTimeout(typing);
        const params = new URLSearchParams(window.location.search);
        const term = input.value.trim();
        if ((params.get('search') || '') === term) return;
        if (term) params.set('search', term); else params.delete('search');
        params.set('pageNumber', '1');
        go(window.location.pathname + '?' + params.toString(), { replace: true, quiet: true });
    }
    document.addEventListener('input', function (e) {
        const input = e.target.closest('.ip-filterbar .search input[type="search"]');
        if (!input || !available()) return;
        clearTimeout(typing);
        typing = setTimeout(() => searchNow(input), 350);
    });
    document.addEventListener('keydown', function (e) {
        const input = e.target.closest('.ip-filterbar .search input[type="search"]');
        if (!input || e.key !== 'Enter' || !available()) return;
        e.preventDefault();
        e.stopImmediatePropagation();
        searchNow(input);
    }, true);

    history.replaceState(Object.assign({}, history.state, { listNav: true }), '');
    window.addEventListener('popstate', function (e) {
        if (e.state && e.state.listNav && available()) load({});
    });

    return {
        go: go,
        /** Refreshes the list in place after the page's own action, like an approval. */
        reload: () => load({ quiet: true }),
        /** Lets LiveUpdates skip a refresh while a load runs or just finished. */
        busy: () => pending !== null,
        freshWithin: ms => Date.now() - loadedAt < ms
    };
})();

/** Rows-per-page select, which goes back to the first page with the new size. */
function changePageSize(size) {
    const params = new URLSearchParams(window.location.search);
    params.set('pageSize', size);
    params.set('pageNumber', '1');
    ListNav.go(window.location.pathname + '?' + params.toString());
}

// A row with data-href opens its page unless the click hit a control or ended a text selection
document.addEventListener('click', function (e) {
    const row = e.target.closest('tr[data-href]');
    if (!row || e.button !== 0) return;
    if (e.target.closest('a, button, input, select, textarea, label, [data-no-row-link], [data-image-preview]')) return;
    if (String(window.getSelection ? window.getSelection() : '').length) return;
    if (e.ctrlKey || e.metaKey) window.open(row.dataset.href, '_blank');
    else window.location.href = row.dataset.href;
});

/** Opens an image in the layout's shared preview modal. */
function showImageModal(imageUrl, title) {
    $('#globalModalImage').attr('src', imageUrl);
    $('#globalImageModal .modal-title').text(title || t('Image Preview'));
    $('#globalImageModal').modal('show');
}

/** Placeholder lines while a panel loads, matching the markup of _Skeleton.cshtml. */
function skeletonHtml(lines) {
    const widths = ['100%', '92%', '78%', '96%', '68%', '88%'];
    let html = '<div class="skeleton-group" aria-busy="true" role="status">'
        + `<span class="visually-hidden">${t('Loading...')}</span><span class="skeleton skeleton-title"></span>`;
    for (let i = 0; i < Math.max(1, lines || 4); i++) {
        html += `<span class="skeleton" style="width: ${widths[i % widths.length]}"></span>`;
    }
    return html + '</div>';
}

// Theme toggle that fires themechange so charts can redraw, while _Layout applies it before paint
window.Theme = (function () {
    const KEY = 'theme';
    const root = document.documentElement;

    function saved() {
        try { return localStorage.getItem(KEY); } catch (e) { return null; }
    }

    function current() {
        return root.getAttribute('data-bs-theme') === 'dark' ? 'dark' : 'light';
    }

    function syncButtons() {
        const dark = current() === 'dark';
        document.querySelectorAll('[data-theme-toggle]').forEach(function (button) {
            const icon = button.querySelector('i');
            if (icon) icon.className = dark ? 'fas fa-sun' : 'fas fa-moon';
            button.title = dark ? t('Light mode') : t('Dark mode');
        });
    }

    function apply(theme, remember) {
        root.setAttribute('data-bs-theme', theme);
        if (remember) {
            try { localStorage.setItem(KEY, theme); } catch (e) { /* Private mode keeps it for this page only */ }
        }
        syncButtons();
        window.dispatchEvent(new CustomEvent('themechange', { detail: theme }));
    }

    document.addEventListener('click', function (e) {
        if (e.target.closest('[data-theme-toggle]')) apply(current() === 'dark' ? 'light' : 'dark', true);
    });

    // Follow the OS until a theme has been picked here
    if (window.matchMedia) {
        matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function (e) {
            if (!saved()) apply(e.matches ? 'dark' : 'light', false);
        });
    }

    syncButtons();
    return { current: current, apply: apply };
})();

// Sidebar rail, remembered on wide screens, collapsed by default on tablets and a drawer on phones
window.Rail = (function () {
    const KEY = 'ip-rail';
    const phone = window.matchMedia('(max-width: 767.98px)');
    const narrow = window.matchMedia('(max-width: 1024px)');

    function rail() { return document.getElementById('ipRail'); }

    function syncToggle() {
        const r = rail();
        const button = r && r.querySelector('.ip-rail-toggle');
        if (!button) return;
        const label = r.classList.contains('collapsed') ? button.dataset.labelExpand : button.dataset.labelCollapse;
        button.title = label;
        button.setAttribute('aria-label', label);
        const span = button.querySelector('span');
        if (span) span.textContent = label;
    }

    function backdrop() { return document.querySelector('.ip-rail-backdrop'); }

    function openDrawer() {
        const r = rail();
        if (!r) return;
        r.classList.add('open');
        backdrop()?.classList.add('show');
        document.body.classList.add('ip-drawer-open');
        r.querySelector('.ip-rail-close')?.focus();
    }

    function closeDrawer() {
        rail()?.classList.remove('open');
        backdrop()?.classList.remove('show');
        document.body.classList.remove('ip-drawer-open');
    }

    function layout() {
        const r = rail();
        if (!r) return;
        if (phone.matches) {
            r.classList.remove('collapsed', 'expanded');
        } else {
            closeDrawer();
            if (narrow.matches) {
                r.classList.toggle('collapsed', !r.classList.contains('expanded'));
            } else {
                let stored = null;
                try { stored = localStorage.getItem(KEY); } catch (e) { }
                r.classList.remove('expanded');
                r.classList.toggle('collapsed', stored === 'collapsed');
            }
        }
        syncToggle();
    }

    function toggle() {
        const r = rail();
        if (!r) return;
        if (phone.matches) {
            if (r.classList.contains('open')) closeDrawer(); else openDrawer();
        } else if (narrow.matches) {
            r.classList.toggle('expanded');
            layout();
        } else {
            const collapse = !r.classList.contains('collapsed');
            try { localStorage.setItem(KEY, collapse ? 'collapsed' : 'expanded'); } catch (e) { }
            layout();
        }
    }

    document.addEventListener('click', function (e) {
        if (e.target.closest('[data-rail-open]')) openDrawer();
        else if (e.target.closest('[data-rail-close]')) closeDrawer();
        else if (e.target.closest('[data-rail-toggle]')) toggle();
        else if (phone.matches && e.target.closest('#ipRail a[href]')) closeDrawer();
    });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && rail()?.classList.contains('open')) closeDrawer();
    });
    phone.addEventListener('change', layout);
    narrow.addEventListener('change', layout);
    document.addEventListener('DOMContentLoaded', layout);

    return { toggle: toggle, open: openDrawer, close: closeDrawer };
})();

/** Opens an approval request in a dialog and resolves with its head element, which carries data-status. */
function loadApprovalDetails(url, modalEl) {
    const head = modalEl.querySelector('[data-dialog-head]');
    const body = modalEl.querySelector('[data-dialog-body]');
    head.innerHTML = skeletonHtml(1);
    body.innerHTML = skeletonHtml(5);
    bootstrap.Modal.getOrCreateInstance(modalEl).show();

    return $.get(url).then(function (html) {
        const holder = document.createElement('div');
        holder.innerHTML = html;
        const partHead = holder.querySelector('[data-approval-head]');
        head.innerHTML = '';
        if (partHead) head.appendChild(partHead);
        body.innerHTML = '';
        body.append(...holder.childNodes);
        return partHead;
    }, function () {
        head.innerHTML = '';
        body.innerHTML = `<div class="ip-banner ip-banner-danger">${escapeHtml(t('Failed to load details. Please try again.'))}</div>`;
        return null;
    });
}

// Find-by-code box whose plain submit opens the product for an exact code and the filtered list otherwise
document.addEventListener('DOMContentLoaded', function () {
    const form = document.querySelector('[data-code-finder]');
    if (!form) return;
    const input = form.querySelector('input[name="code"]');
    const menu = form.querySelector('.ip-finder-menu');
    let timer = null, active = -1, lastTerm = '', controller = null;

    function close() {
        menu.hidden = true;
        input.setAttribute('aria-expanded', 'false');
        input.removeAttribute('aria-activedescendant');
        active = -1;
    }

    function render(list, term) {
        let html = list.map((p, i) => `
            <a class="ip-finder-item" role="option" id="codeFinder-${i}" href="/Products/Details/${encodeURIComponent(p.id)}">
                <span class="ip-thumb">${p.imageUrl ? `<img src="${escapeHtml(thumbUrl(p.imageUrl))}" alt="" />` : '<i class="fa-solid fa-box"></i>'}</span>
                <span class="meta">
                    <span><b class="ip-mono">${escapeHtml(String(p.code))}</b> · ${escapeHtml(p.model || '')}</span>
                    <span>${escapeHtml([p.vendor, p.department].filter(Boolean).join(' · '))}</span>
                </span>
            </a>`).join('');
        if (!list.length) html = `<div class="ip-finder-empty">${escapeHtml(t('No product matches this code or name'))}</div>`;
        html += `<a class="ip-finder-all" role="option" id="codeFinder-all" href="/Products?search=${encodeURIComponent(term)}">${escapeHtml(t('Search all products for "{0}"', term))}</a>`;
        menu.innerHTML = html;
        menu.hidden = false;
        input.setAttribute('aria-expanded', 'true');
        active = -1;
    }

    async function suggest() {
        const term = input.value.trim();
        if (!term) { close(); lastTerm = ''; return; }
        if (term === lastTerm && !menu.hidden) return;
        lastTerm = term;
        if (controller) controller.abort();
        controller = new AbortController();
        try {
            const response = await fetch('/Products/Suggest?term=' + encodeURIComponent(term), {
                signal: controller.signal,
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (!response.ok) return;
            const list = await response.json();
            if (input.value.trim() === term) render(list, term);
        } catch (e) { /* Aborted by newer typing or offline, and the form still submits */ }
    }

    function options() { return Array.from(menu.querySelectorAll('a')); }

    function move(step) {
        const list = options();
        if (!list.length) return;
        active = (active + step + list.length) % list.length;
        list.forEach((a, i) => a.classList.toggle('active', i === active));
        input.setAttribute('aria-activedescendant', list[active].id);
    }

    input.addEventListener('input', function () {
        clearTimeout(timer);
        timer = setTimeout(suggest, 200);
    });
    input.addEventListener('focus', function () { if (input.value.trim()) suggest(); });
    input.addEventListener('keydown', function (e) {
        if (e.key === 'ArrowDown') { e.preventDefault(); if (menu.hidden) suggest(); else move(1); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); move(-1); }
        else if (e.key === 'Escape') { close(); }
        else if (e.key === 'Enter' && active >= 0 && !menu.hidden) { e.preventDefault(); window.location.href = options()[active].href; }
    });
    document.addEventListener('click', function (e) { if (!form.contains(e.target)) close(); });
    form.addEventListener('submit', function (e) {
        if (!input.value.trim()) { e.preventDefault(); input.focus(); }
    });
});

// Phone cards get header labels on cells without a role class, and plain filter bars fold away
window.MobileLayout = (function () {
    'use strict';

    const ROLES = ['thumb', 'code', 'title', 'meta', 'state', 'date', 'actions', 'hide-sm', 'field', 'old', 'arrow'];

    function labelTable(table) {
        const headers = Array.from(table.querySelectorAll('thead th')).map(th => th.textContent.replace(/\s+/g, ' ').trim());
        table.querySelectorAll('tbody tr').forEach(function (tr) {
            Array.from(tr.children).forEach(function (td, i) {
                if (td.tagName !== 'TD') return;
                if (ROLES.some(function (r) { return td.classList.contains(r); })) { delete td.dataset.label; return; }
                const label = headers[i] || '';
                // Detected by content because these columns have visually hidden headers
                const media = !!td.querySelector('img, .ip-thumb') && !td.textContent.trim();
                const onlyControls = td.children.length > 0 && Array.from(td.children).every(c => c.matches('a.ip-btn, a.ip-btn-icon, button, form'));
                const actions = !media && (!!td.querySelector('.actions') || onlyControls || (!label && !!td.querySelector('a, button')));
                if (label && td.colSpan === 1 && !media && !actions) td.dataset.label = label; else delete td.dataset.label;
                td.classList.toggle('cell-media', media);
                td.classList.toggle('cell-actions', actions);
                // No text and nothing to interact with, like the arrow cell in a transfer row
                td.classList.toggle('cell-empty', !media && !td.textContent.trim() && !td.querySelector('img, input, button, select, a[href]'));
            });
        });
    }

    function activeFilters(bar) {
        let n = 0;
        bar.querySelectorAll('.ip-filter:not(.search) select').forEach(function (s) { if (s.value) n++; });
        bar.querySelectorAll('.ip-filter:not(.search) input:not([type=hidden])').forEach(function (i) { if (i.value) n++; });
        return n + bar.querySelectorAll('.ip-btn.is-on').length;
    }

    function addFilterToggle(bar) {
        if (bar.hasAttribute('data-filter-sheet') || bar.querySelector(':scope > .ip-filter-toggle, :scope > .ip-filter-more')) return;
        const controls = Array.from(bar.children).filter(function (el) { return !el.classList.contains('search') && el.type !== 'hidden'; });
        if (controls.length < 2) return;
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'ip-btn ip-btn-secondary ip-filter-toggle';
        const n = activeFilters(bar);
        button.innerHTML = '<i class="fa-solid fa-sliders"></i><span>' + escapeHtml(t('Filters')) + '</span>'
            + (n ? '<span class="count">' + n + '</span>' : '');
        button.setAttribute('aria-expanded', 'false');
        button.addEventListener('click', function () {
            const open = bar.classList.toggle('open');
            button.setAttribute('aria-expanded', open ? 'true' : 'false');
        });
        const search = bar.querySelector(':scope > .search');
        if (search) search.after(button); else bar.prepend(button);
        bar.classList.add('has-toggle');
    }

    function apply(root) {
        (root || document).querySelectorAll('table.ip-table').forEach(labelTable);
        (root || document).querySelectorAll('.ip-filterbar').forEach(addFilterToggle);
    }

    let pending = 0;
    function schedule() {
        if (pending) return;
        pending = requestAnimationFrame(function () { pending = 0; apply(document); });
    }

    function touchesLists(node) {
        return node.nodeType === 1 && (node.matches('table, tbody, tr, .ip-filterbar') || !!node.querySelector('table, .ip-filterbar'));
    }

    document.addEventListener('DOMContentLoaded', function () {
        apply(document);
        // Live updates and list refreshes swap regions, so new rows need labels too
        new MutationObserver(function (mutations) {
            if (mutations.some(function (m) { return Array.from(m.addedNodes).some(touchesLists); }))
                schedule();
        }).observe(document.body, { childList: true, subtree: true });
    });

    return { apply: apply };
})();

// Send again queues a failed WhatsApp message once more and live updates show the outcome later
document.addEventListener('click', async function (e) {
    const button = e.target.closest('[data-wa-resend]');
    if (!button) return;
    e.preventDefault();
    e.stopPropagation();
    button.disabled = true;
    try {
        const response = await fetch('/Routes/ResendWhatsApp/' + encodeURIComponent(button.dataset.waResend), {
            method: 'POST',
            credentials: 'same-origin',
            headers: Object.assign({ 'X-Requested-With': 'XMLHttpRequest' }, AppConfig.antiforgeryHeaders())
        });
        const body = await response.json().catch(() => ({}));
        showToast(body.message || (response.ok ? t('Queued') : t('Failed')), body.isSuccess ? 'success' : 'error');
        if (body.isSuccess) ListNav.reload();
        else button.disabled = false;
    } catch (err) {
        showToast(t('Failed'), 'error');
        button.disabled = false;
    }
});

// Breadcrumb back button that uses history.back() from the parent page to keep its filters and scroll
(function () {
    function addBackButtons() {
        document.querySelectorAll('.ip-page-head ol.breadcrumb').forEach(function (crumbs) {
            if (crumbs.parentElement.classList.contains('ip-crumbs')) return;
            const links = crumbs.querySelectorAll('.breadcrumb-item a[href]');
            if (!links.length) return;

            const back = document.createElement('a');
            back.className = 'ip-back';
            back.href = links[links.length - 1].getAttribute('href');
            back.setAttribute('data-no-prefetch', '');
            back.innerHTML = '<i class="fa-solid fa-arrow-left" aria-hidden="true"></i><span></span>';
            back.querySelector('span').textContent = t('Back');
            function onBack(e) {
                if (e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
                let from = null;
                try { from = document.referrer ? new URL(document.referrer) : null; } catch { from = null; }
                const target = new URL(back.href, location.href);
                if (from && from.origin === location.origin && history.length > 1
                    && from.pathname.toLowerCase() === target.pathname.toLowerCase()) {
                    e.preventDefault();
                    history.back();
                }
            }
            back.addEventListener('click', onBack);

            const row = document.createElement('div');
            row.className = 'ip-crumbs';
            crumbs.parentNode.insertBefore(row, crumbs);
            row.append(back, crumbs);

            // On phones the app bar shows a back arrow instead of the menu button
            const appBack = document.querySelector('.ip-appbar-back');
            if (appBack) {
                appBack.href = back.href;
                appBack.hidden = false;
                appBack.addEventListener('click', onBack);
                document.querySelector('.ip-appbar [data-rail-open]')?.setAttribute('hidden', '');
            }
        });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', addBackButtons);
    else addBackButtons();
})();

// Dashboard figures count up once when the page opens, not on live refreshes
(function () {
    if (window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    document.addEventListener('DOMContentLoaded', function () {
        if (document.hidden) return;   // Background tabs get no animation frames
        document.querySelectorAll('.ip-kpi .value').forEach(function (el) {
            const shown = el.textContent.trim();
            if (!/^[\d,]+$/.test(shown)) return;
            const target = parseInt(shown.replace(/,/g, ''), 10);
            if (!target) return;
            const start = performance.now(), duration = 700;
            function step(now) {
                const p = Math.min(1, (now - start) / duration);
                el.textContent = p < 1 ? Math.round(target * (1 - Math.pow(1 - p, 3))).toLocaleString('en-US') : shown;
                if (p < 1) requestAnimationFrame(step);
            }
            el.textContent = '0';
            requestAnimationFrame(step);
            setTimeout(function () { el.textContent = shown; }, duration + 400);   // In case frames stop because the tab was hidden
        });
    });
})();

// Thumbnails with data-image-preview peek larger on hover and open the photo instead of the row's page
(function () {
    let peek = null, timer = null;
    const canHover = window.matchMedia && matchMedia('(hover: hover) and (pointer: fine)').matches;

    function hide() {
        clearTimeout(timer);
        if (peek) peek.classList.remove('show');
    }
    function show(el) {
        if (!peek) {
            peek = document.createElement('div');
            peek.className = 'ip-img-peek';
            peek.setAttribute('aria-hidden', 'true');
            peek.innerHTML = '<img alt="">';
            document.body.appendChild(peek);
        }
        const img = peek.firstChild;
        img.src = thumbUrl(el.dataset.image, 480);
        const r = el.getBoundingClientRect(), size = 264, gap = 12;
        let left = r.right + gap, top = r.top + r.height / 2 - size / 2;
        if (left + size > window.innerWidth - 8) left = r.left - gap - size;
        top = Math.max(8, Math.min(top, window.innerHeight - size - 8));
        peek.style.left = left + 'px';
        peek.style.top = top + 'px';
        peek.classList.add('show');
    }

    document.addEventListener('mouseover', function (e) {
        if (!canHover) return;
        const el = e.target.closest('[data-image-preview]');
        if (!el || el.contains(e.relatedTarget)) return;
        clearTimeout(timer);
        timer = setTimeout(function () { show(el); }, 180);
    });
    document.addEventListener('mouseout', function (e) {
        const el = e.target.closest('[data-image-preview]');
        if (el && !el.contains(e.relatedTarget)) hide();
    });
    window.addEventListener('scroll', hide, true);
    document.addEventListener('click', function (e) {
        const el = e.target.closest('[data-image-preview]');
        if (!el) return;
        hide();
        showImageModal(el.dataset.image, el.dataset.title);
    });
    document.addEventListener('keydown', function (e) {
        const el = e.target.closest && e.target.closest('[data-image-preview]');
        if (el && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); showImageModal(el.dataset.image, el.dataset.title); }
    });
    // Makes thumbnails reachable from the keyboard
    function focusable() { document.querySelectorAll('[data-image-preview]:not([tabindex])').forEach(function (el) { el.tabIndex = 0; el.setAttribute('role', 'button'); el.setAttribute('aria-label', t('View image')); }); }
    document.addEventListener('DOMContentLoaded', focusable);
    document.addEventListener('listnav:loaded', focusable);
})();

