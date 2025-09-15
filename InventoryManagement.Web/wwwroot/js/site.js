// Core site functionality
document.addEventListener('DOMContentLoaded', function () {
    // Initialize tooltips if Bootstrap is available
    if (typeof bootstrap !== 'undefined') {
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }

    // Auto-hide alerts after 5 seconds
    setTimeout(function () {
        const alerts = document.querySelectorAll('.alert:not(.alert-permanent)');
        alerts.forEach(function (alert) {
            if (typeof bootstrap !== 'undefined') {
                const bsAlert = new bootstrap.Alert(alert);
                bsAlert.close();
            }
        });
    }, 5000);
});

// Global toast notification function
window.showToast = function (message, type = 'info', duration = 5000) {
    // Validate type
    const validTypes = ['success', 'error', 'danger', 'warning', 'info', 'secondary'];
    if (!validTypes.includes(type)) {
        type = 'info';
    }

    // Map error to danger for Bootstrap
    if (type === 'error') {
        type = 'danger';
    }

    // Generate unique ID
    const toastId = 'toast-' + Date.now();
    const textClass = (type === 'warning' || type === 'info') ? 'text-dark' : 'text-white';
    const closeButtonClass = (type === 'warning' || type === 'info') ? '' : 'btn-close-white';

    // Create toast HTML
    const toastHtml = `
        <div id="${toastId}" class="toast align-items-center bg-${type} border-0" role="alert">
            <div class="d-flex">
                <div class="toast-body ${textClass}">
                    ${message}
                </div>
                <button type="button" class="btn-close ${closeButtonClass} me-2 m-auto" data-bs-dismiss="toast"></button>
            </div>
        </div>
    `;

    // Ensure container exists
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

    // Initialize and show toast
    const toastElement = document.getElementById(toastId);
    if (typeof bootstrap !== 'undefined') {
        const toast = new bootstrap.Toast(toastElement, {
            delay: duration,
            autohide: true
        });
        toast.show();

        // Clean up after hiding
        toastElement.addEventListener('hidden.bs.toast', function () {
            toastElement.remove();
        });
    }
};

// Global loader functions
window.showLoader = function () {
    if (!document.querySelector('.loader-overlay')) {
        const loader = document.createElement('div');
        loader.className = 'loader-overlay';
        loader.innerHTML = '<div class="spinner-border text-primary" role="status"></div>';
        document.body.appendChild(loader);
    }
};

window.hideLoader = function () {
    const loader = document.querySelector('.loader-overlay');
    if (loader) {
        loader.remove();
    }
};

// Image preview helper
window.previewImage = function (input, previewId) {
    if (input.files && input.files[0]) {
        const reader = new FileReader();
        reader.onload = function (e) {
            const preview = document.getElementById(previewId);
            if (preview) {
                preview.src = e.target.result;
                preview.style.display = 'block';
            }
        };
        reader.readAsDataURL(input.files[0]);
    }
};

// Format date helper
window.formatDate = function (dateString) {
    const options = {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    };
    return new Date(dateString).toLocaleDateString('en-US', options);
};

// Copy to clipboard
window.copyToClipboard = function (text) {
    navigator.clipboard.writeText(text).then(function () {
        showToast('Copied to clipboard!', 'success');
    }).catch(function () {
        showToast('Failed to copy to clipboard', 'error');
    });
};