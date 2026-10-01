/**
 * Interface language. The layout loads the Azerbaijani table as window.I18n (from
 * Resources/i18n/az.json, the same file the server's IStringLocalizer uses); in English it is absent.
 *
 *   t('Products')                        -> "Məhsullar"
 *   t('{0} selected', n)                 -> "{0} seçilib" with n filled in
 *
 * The English text is the key, so an untranslated string simply shows in English.
 */
function t(key, ...args) {
    const table = window.I18n || {};
    const text = Object.prototype.hasOwnProperty.call(table, key) ? table[key] : key;
    return args.length ? String(text).replace(/\{(\d+)\}/g, (m, i) => (args[i] ?? m)) : text;
}

/** The locale for Intl / toLocale*String: follows the interface language. */
function uiLocale() {
    return document.documentElement.lang === 'az' ? 'az-Latn-AZ' : 'en-US';
}

/** dd.MM.yyyy (and HH:mm when withTime): the one date format used across the UI. */
function formatDate(value, withTime) {
    if (!value) return '';
    const d = value instanceof Date ? value : new Date(value);
    if (isNaN(d)) return '';
    const pad = n => String(n).padStart(2, '0');
    const date = `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()}`;
    return withTime ? `${date} ${pad(d.getHours())}:${pad(d.getMinutes())}` : date;
}

// DataTables texts for every table (a page's own `language` option still wins).
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

/**
 * In-page confirmation dialog, replacing the browser's confirm() popup.
 * Enter confirms, Esc/Cancel dismisses (Bootstrap handles Esc).
 *
 *   confirmAction('Delete this route?', () => { ...proceed... });
 *   confirmAction({ message: '...', title: 'Delete route', okText: 'Delete', danger: true }, onOk);
 *
 * Falls back to window.confirm if the shared modal markup is missing (e.g. a layout-less page).
 */
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

    // Rebuild the OK button so a previous dialog's handler can never fire for this one.
    const freshOk = okBtn.cloneNode(true);
    okBtn.parentNode.replaceChild(freshOk, okBtn);

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);

    // Track the outcome on the element itself. Listeners left over from an earlier dialog can
    // still fire on this shared modal; keying off a per-call token means only the current
    // dialog's listener acts, and a stale one can never swallow or double-run the callback.
    const token = Symbol('confirm');
    modalEl.__confirmToken = token;
    modalEl.__confirmAccepted = false;

    freshOk.addEventListener('click', function () {
        modalEl.__confirmAccepted = true;
        modal.hide();
    });

    // Enter anywhere in the dialog = confirm.
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
        if (modalEl.__confirmToken !== token) return;   // a newer dialog owns the modal now
        if (modalEl.__confirmAccepted) onConfirm?.();   // run after it is gone, so redirects are clean
    });

    // Opening while a previous instance is still mid-hide leaves Bootstrap wedged (no dialog,
    // stuck backdrop). Wait for that transition to finish before showing.
    if (modalEl.classList.contains('show')) {
        modalEl.addEventListener('hidden.bs.modal', () => modal.show(), { once: true });
    } else {
        modal.show();
    }
}

// Global site functionality
document.addEventListener('DOMContentLoaded', function () {
    // Initialize tooltips
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // Add loading spinner for forms
    // Layout forms (language switch, sign out) navigate at once and keep their button as it is.
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

    // Start session monitoring (only if user is authenticated)
    setupSessionMonitor();
});

// Toast notification (.ip-toast: surface card, bottom right, the icon carries the colour)
/** The small copy of an uploaded photo for lists (ImageThumbnails on the server); other URLs unchanged. */
function thumbUrl(url, width) {
    return typeof url === 'string' && url.startsWith('/images/') ? '/thumbs/' + (width || 160) + url : url;
}

function showToast(message, type = 'info', duration = 4000) {
    message = typeof message === 'string' ? t(message) : message;
    // Ensure we have a valid type
    const validTypes = ['success', 'error', 'danger', 'warning', 'info', 'secondary'];
    if (!validTypes.includes(type)) {
        type = 'info';
    }

    // Map error to danger for Bootstrap compatibility
    if (type === 'error') {
        type = 'danger';
    }

    // Create toast HTML with proper styling
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

    // Ensure toast container exists
    let container = document.getElementById('toastContainer');
    if (!container) {
        container = document.createElement('div');
        container.id = 'toastContainer';
        document.body.appendChild(container);
    }
    container.className = 'toast-container position-fixed bottom-0 end-0 p-4';

    // Add toast to container
    container.insertAdjacentHTML('beforeend', toastHtml);

    // Initialize and show the toast
    const toastElement = document.getElementById(toastId);
    const toast = new bootstrap.Toast(toastElement, {
        delay: duration,
        autohide: true
    });
    toast.show();

    // Remove element after it's hidden
    toastElement.addEventListener('hidden.bs.toast', function () {
        toastElement.remove();
    });

    // Returned so callers can attach their own handlers (e.g. click-to-refresh toasts).
    return toastElement;
}


// Helper function to get toast icon
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


// Helper function to escape HTML
/** HTML-escapes text for insertion into markup. null/undefined become ''. */
function escapeHtml(value) {
    if (value === null || value === undefined) return '';
    return String(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}


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


/** Pager buttons (_Pagination): same page with the current filters, a different pageNumber. */
/**
 * While a list page loads its next state (filter, page, tab), the rows are replaced by skeleton
 * rows (design: .ip-skeleton). The list is the element marked [data-list-region].
 */
function showListSkeleton() {
    const region = document.querySelector('[data-list-region]');
    if (!region) return;
    const bar = width => `<span class="ip-skeleton" style="width: ${width}; height: 12px"></span>`;
    const rows = Array.from({ length: 6 }, () =>
        `<div class="ip-skeleton-row"><span class="ip-skeleton" style="width: 44px; height: 44px; border-radius: 10px; flex: none"></span>${bar('10%')}${bar('26%')}${bar('20%')}${bar('12%')}</div>`).join('');
    region.setAttribute('aria-busy', 'true');
    region.innerHTML = `<div role="status"><span class="visually-hidden">${escapeHtml(t('Loading...'))}</span>${rows}</div>`;
}

/**
 * Lists change in place: a status tab, a page number, rows per page, a filter or a search fetch
 * the new page and swap only the list ([data-list-region]), its tabs, the count under the title,
 * the regions the page gave LiveUpdates.watch and the filter bar's buttons (quick filters,
 * Reset) - the filter controls themselves stay, so typing and open pickers are not disturbed.
 * The URL follows (Back/Forward work); pages without a list region simply navigate.
 *
 *   ListNav.go(url)                                   // tabs, pages, filters
 *   ListNav.go(url, { replace: true, quiet: true })   // live search: no history entry per letter
 * Fires 'listnav:loaded' on document afterwards (list-filters.js re-reads the URL).
 */
window.ListNav = (function () {
    'use strict';

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
            // Redirected (signed out, no access) or failed: let the browser show the real page.
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

        // A new page of results starts at the top of the list, not wherever the old one was scrolled.
        const list = document.querySelector('[data-list-region]');
        if (!options.quiet && list && list.getBoundingClientRect().top < 0)
            list.scrollIntoView({ block: 'start', behavior: 'smooth' });
    }

    // Quick filters (on/off) and Reset come and go with the filters: take them from the new page,
    // keep the controls (and the phone "Filters" button) as they are.
    function syncFilterButtons(doc) {
        const fresh = doc.querySelectorAll('.ip-filterbar');
        document.querySelectorAll('.ip-filterbar').forEach(function (bar, i) {
            if (!fresh[i]) return;
            const isButton = el => el.matches('a.ip-btn, button.ip-btn') && !el.classList.contains('ip-filter-toggle');
            Array.from(bar.children).filter(isButton).forEach(el => el.remove());
            Array.from(fresh[i].children).filter(isButton).forEach(el => bar.appendChild(document.importNode(el, true)));
        });
    }

    // Tabs, page numbers, Reset/clear links in the bar or the empty state: same page, new query.
    document.addEventListener('click', function (e) {
        const link = e.target.closest('.ip-table-foot a[href], [data-list-tabs] a[href], .ip-filterbar a[href], [data-list-region] .ip-empty a[href]');
        if (!link || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || link.target) return;
        if (new URL(link.href, window.location.href).pathname !== window.location.pathname || !available()) return;
        e.preventDefault();
        go(link.href);
    });

    // GET filter forms (the audit log): submit in place.
    document.addEventListener('submit', function (e) {
        const form = e.target;
        if (!form.matches('form.ip-filterbar') || (form.method || 'get').toLowerCase() !== 'get' || !available()) return;
        e.preventDefault();
        const params = new URLSearchParams(new FormData(form));
        Array.from(params.keys()).forEach(k => { if (!params.get(k)) params.delete(k); });
        go(window.location.pathname + '?' + params.toString());
    });

    // Search as you type: the list follows 350 ms after the last key (Enter: at once).
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

    // Back / Forward between list states of this page.
    history.replaceState(Object.assign({}, history.state, { listNav: true }), '');
    window.addEventListener('popstate', function (e) {
        if (e.state && e.state.listNav && available()) load({});
    });

    return {
        go: go,
        /** After this page's own change (approve, complete, cancel...): refresh the list in place. */
        reload: () => load({ quiet: true }),
        /** For LiveUpdates: a load is running, or one finished within <ms> (nothing left to refresh). */
        busy: () => pending !== null,
        freshWithin: ms => Date.now() - loadedAt < ms
    };
})();

/** Rows per page (the select in _Pagination): back to the first page with the new size. */
function changePageSize(size) {
    const params = new URLSearchParams(window.location.search);
    params.set('pageSize', size);
    params.set('pageNumber', '1');
    ListNav.go(window.location.pathname + '?' + params.toString());
}

// A table row with data-href opens that page (design: the whole row is clickable). Links,
// buttons, form controls and cells marked data-no-row-link keep their own behaviour; selecting
// text does not navigate; Ctrl/Cmd-click opens a new tab.
document.addEventListener('click', function (e) {
    const row = e.target.closest('tr[data-href]');
    if (!row || e.button !== 0) return;
    if (e.target.closest('a, button, input, select, textarea, label, [data-no-row-link]')) return;
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

/** Placeholder lines while a panel's content loads (same markup as Views/Shared/_Skeleton.cshtml). */
function skeletonHtml(lines) {
    const widths = ['100%', '92%', '78%', '96%', '68%', '88%'];
    let html = '<div class="skeleton-group" aria-busy="true" role="status">'
        + `<span class="visually-hidden">${t('Loading...')}</span><span class="skeleton skeleton-title"></span>`;
    for (let i = 0; i < Math.max(1, lines || 4); i++) {
        html += `<span class="skeleton" style="width: ${widths[i % widths.length]}"></span>`;
    }
    return html + '</div>';
}

/**
 * Light/dark theme. The <head> script in _Layout already applied the saved choice (or the OS
 * setting) before first paint; this owns the toggle button and tells pages about a change
 * ('themechange' on window, e.g. the dashboard redraws its charts).
 */
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
            try { localStorage.setItem(KEY, theme); } catch (e) { /* private mode: this page only */ }
        }
        syncButtons();
        window.dispatchEvent(new CustomEvent('themechange', { detail: theme }));
    }

    document.addEventListener('click', function (e) {
        if (e.target.closest('[data-theme-toggle]')) apply(current() === 'dark' ? 'light' : 'dark', true);
    });

    // Follow the OS while the user has not picked a theme here.
    if (window.matchMedia) {
        matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function (e) {
            if (!saved()) apply(e.matches ? 'dark' : 'light', false);
        });
    }

    syncButtons();
    return { current: current, apply: apply };
})();

/**
 * The sidebar rail (_Sidebar.cshtml). Wide screens: expanded or collapsed to icons, the choice
 * kept in localStorage 'ip-rail' (the inline script in _Sidebar applies it before first paint).
 * 768-1024px: collapsed by default, the toggle opens it for the moment. Phones: a drawer.
 */
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

    function layout() {
        const r = rail();
        if (!r) return;
        if (phone.matches) {
            r.classList.remove('collapsed', 'expanded');
        } else {
            document.body.classList.remove('rail-open');
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
            document.body.classList.toggle('rail-open');
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
        if (e.target.closest('[data-rail-toggle]')) toggle();
    });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && document.body.classList.contains('rail-open')) document.body.classList.remove('rail-open');
    });
    phone.addEventListener('change', layout);
    narrow.addEventListener('change', layout);
    document.addEventListener('DOMContentLoaded', layout);

    return { toggle: toggle };
})();

/**
 * Opens an approval request in a dialog (Approvals, My Requests). The partial's
 * [data-approval-head] goes into the modal's [data-dialog-head], the rest into [data-dialog-body].
 * Resolves with the head element (its data-status says whether a decision is still open).
 */
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

/**
 * The toolbar's "find by code" (_Layout, [data-code-finder]). Typing shows up to six matching
 * products (Products/Suggest) with a last row that searches the whole list; arrows move, Enter
 * opens the highlighted one. Enter with nothing highlighted, or the button, submits the form:
 * an exact inventory code opens that product, anything else the filtered product list.
 */
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
        } catch (e) { /* aborted by newer typing, or offline: the form still submits */ }
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

/**
 * Phones (CSS below 768px, ip-components.css "phones"): every .ip-table is shown as cards, one per
 * row, each cell as "Header: value". This labels the cells from the table's headers and marks
 * the photo, actions and empty cells. Filter bars get a "Filters (n)" button that folds all but
 * the search away. Re-applied when live updates or list refreshes swap content in.
 */
window.MobileLayout = (function () {
    'use strict';

    function labelTable(table) {
        const headers = Array.from(table.querySelectorAll('thead th')).map(th => th.textContent.replace(/\s+/g, ' ').trim());
        table.querySelectorAll('tbody tr').forEach(function (tr) {
            Array.from(tr.children).forEach(function (td, i) {
                if (td.tagName !== 'TD') return;
                const label = headers[i] || '';
                // By content, not header: their headers are visually hidden texts ("Image", "Actions").
                const media = !!td.querySelector('img, .ip-thumb') && !td.textContent.trim();
                const onlyControls = td.children.length > 0 && Array.from(td.children).every(c => c.matches('a.ip-btn, a.ip-btn-icon, button, form'));
                const actions = !media && (!!td.querySelector('.actions') || onlyControls || (!label && !!td.querySelector('a, button')));
                if (label && td.colSpan === 1 && !media && !actions) td.dataset.label = label; else delete td.dataset.label;
                td.classList.toggle('cell-media', media);
                td.classList.toggle('cell-actions', actions);
                // No text and nothing to use (an arrow between "from" and "to", an empty cell).
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
        if (bar.querySelector(':scope > .ip-filter-toggle')) return;
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
        // Live updates and list refreshes replace regions: label the new rows too.
        new MutationObserver(function (mutations) {
            if (mutations.some(function (m) { return Array.from(m.addedNodes).some(touchesLists); }))
                schedule();
        }).observe(document.body, { childList: true, subtree: true });
    });

    return { apply: apply };
})();

// A transfer's failed WhatsApp message: "Send again" (Views/Routes/_WhatsAppStatus) queues it once
// more; the route then shows "queued" and, a few seconds later, the outcome (live updates).
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

/*
 * Back button on every page with a breadcrumb (details, timelines, forms): "← Back" before the
 * crumbs. It returns to the parent list as the user left it (filters, page, scroll) when they
 * came from it - history.back() - and otherwise opens the parent crumb's page.
 */
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
            back.addEventListener('click', function (e) {
                if (e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
                let from = null;
                try { from = document.referrer ? new URL(document.referrer) : null; } catch { from = null; }
                const target = new URL(back.href, location.href);
                if (from && from.origin === location.origin && history.length > 1
                    && from.pathname.toLowerCase() === target.pathname.toLowerCase()) {
                    e.preventDefault();
                    history.back();
                }
            });

            const row = document.createElement('div');
            row.className = 'ip-crumbs';
            crumbs.parentNode.insertBefore(row, crumbs);
            row.append(back, crumbs);
        });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', addBackButtons);
    else addBackButtons();
})();
