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
        if (window.confirm(opts.message || 'Are you sure?')) onConfirm?.();
        return;
    }

    const titleEl = document.getElementById('globalConfirmTitle');
    const msgEl = document.getElementById('globalConfirmMessage');
    const okBtn = document.getElementById('globalConfirmOk');

    titleEl.textContent = opts.title || 'Please confirm';
    msgEl.textContent = opts.message || 'Are you sure?';
    okBtn.textContent = opts.okText || 'Confirm';
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
    const forms = document.querySelectorAll('form:not(.no-spinner)');
    forms.forEach(function (form) {
        form.addEventListener('submit', function () {
            const submitBtn = form.querySelector('button[type="submit"]');
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Processing...';
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

// Toast notification
function showToast(message, type = 'info', duration = 5000) {
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
        <div id="${toastId}" class="toast align-items-center bg-${type} border-0" role="alert" aria-live="assertive" aria-atomic="true">
            <div class="d-flex">
                <div class="toast-body ${type === 'warning' || type === 'info' ? 'text-dark' : 'text-white'}">
                    <i class="fas fa-${icon} me-2"></i>
                    ${escapeHtml(message)}
                </div>
                <button type="button" class="btn-close ${type === 'warning' || type === 'info' ? '' : 'btn-close-white'} me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
        </div>
    `;

    // Ensure toast container exists
    let container = document.getElementById('toastContainer');
    if (!container) {
        container = document.createElement('div');
        container.id = 'toastContainer';
        container.className = 'toast-container position-fixed top-0 end-0 p-3';
        container.style.zIndex = '9999';
        document.body.appendChild(container);
    }

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
        'success': 'check-circle',
        'danger': 'exclamation-circle',
        'warning': 'exclamation-triangle',
        'info': 'info-circle',
        'secondary': 'cog'
    };
    return icons[type] || 'info-circle';
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
    const isUserAuthenticated = document.querySelector('.user-menu-toggle') !== null;

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
                showToast('Your session has expired. Please log in again.', 'warning');
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

/** Opens an image in the layout's shared preview modal. */
function showImageModal(imageUrl, title) {
    $('#globalModalImage').attr('src', imageUrl);
    $('#globalImageModal .modal-title').text(title || 'Image Preview');
    $('#globalImageModal').modal('show');
}

/** Placeholder lines while a panel's content loads (same markup as Views/Shared/_Skeleton.cshtml). */
function skeletonHtml(lines) {
    const widths = ['100%', '92%', '78%', '96%', '68%', '88%'];
    let html = '<div class="skeleton-group" aria-busy="true" role="status">'
        + '<span class="visually-hidden">Loading...</span><span class="skeleton skeleton-title"></span>';
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
            button.title = dark ? 'Light mode' : 'Dark mode';
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
