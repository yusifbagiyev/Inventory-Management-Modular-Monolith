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
    okBtn.className = 'btn btn-sm ' + (opts.danger ? 'btn-danger' : 'btn-primary');

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

    // Auto-hide alerts after 5 seconds
    setTimeout(function () {
        const alerts = document.querySelectorAll('.alert:not(.alert-permanent):not(#productInfo):not(#errorInfo)');
        alerts.forEach(function (alert) {
            const bsAlert = new bootstrap.Alert(alert);
            bsAlert.close();
        });
    }, 5000);

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

// Image preview for file inputs
function previewImage(input, previewId) {
    if (input.files && input.files[0]) {
        const reader = new FileReader();
        reader.onload = function (e) {
            document.getElementById(previewId).src = e.target.result;
            document.getElementById(previewId).style.display = 'block';
        };
        reader.readAsDataURL(input.files[0]);
    }
}

// Toast notification (.ip-toast: surface card, bottom right, the icon carries the colour)
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


function showLoader() {
    if (!$('.loader-overlay').length) {
        $('body').append('<div class="loader-overlay"><div class="spinner-border text-primary" role="status"></div></div>');
    }
}

function hideLoader() {
    $('.loader-overlay').remove();
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
function changePage(page) {
    const params = new URLSearchParams(window.location.search);
    params.set('pageNumber', page);
    window.location.href = window.location.pathname + '?' + params.toString();
}

/** Rows per page (the select in _Pagination): back to the first page with the new size. */
function changePageSize(size) {
    const params = new URLSearchParams(window.location.search);
    params.set('pageSize', size);
    params.set('pageNumber', '1');
    window.location.href = window.location.pathname + '?' + params.toString();
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
