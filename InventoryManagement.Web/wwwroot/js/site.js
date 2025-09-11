// Global site functionality
document.addEventListener('DOMContentLoaded', function () {
    // Initialize Bootstrap tooltips
    var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // Auto-hide alerts after 5 seconds (excluding permanent alerts)
    setTimeout(function () {
        const alerts = document.querySelectorAll('.alert:not(.alert-permanent):not(#productInfo):not(#errorInfo)');
        alerts.forEach(function (alert) {
            const bsAlert = new bootstrap.Alert(alert);
            bsAlert.close();
        });
    }, 5000);
});

// Image preview for file inputs
function previewImage(input, previewId) {
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
}

// Format date helper
function formatDate(dateString) {
    const options = {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    };
    return new Date(dateString).toLocaleDateString('en-US', options);
}

// Copy to clipboard functionality
function copyToClipboard(text) {
    navigator.clipboard.writeText(text).then(function () {
        showToast('Copied to clipboard!', 'success');
    }).catch(function (err) {
        console.error('Failed to copy:', err);
        showToast('Failed to copy to clipboard', 'error');
    });
}

// Toast notification system
function showToast(message, type = 'info', duration = 5000) {
    // Validate and normalize type
    const validTypes = ['success', 'error', 'danger', 'warning', 'info', 'secondary'];
    if (!validTypes.includes(type)) {
        type = 'info';
    }

    // Map error to danger for Bootstrap
    if (type === 'error') {
        type = 'danger';
    }

    // Generate unique toast ID
    const toastId = 'toast-' + Date.now() + '-' + Math.random().toString(36).substr(2, 9);
    const icon = getToastIcon(type);
    const textClass = (type === 'warning' || type === 'info') ? 'text-dark' : 'text-white';
    const closeButtonClass = (type === 'warning' || type === 'info') ? '' : 'btn-close-white';

    // Create toast HTML
    const toastHtml = `
        <div id="${toastId}" class="toast align-items-center bg-${type} border-0" role="alert" aria-live="assertive" aria-atomic="true">
            <div class="d-flex">
                <div class="toast-body ${textClass}">
                    <i class="fas fa-${icon} me-2"></i>
                    ${escapeHtml(message)}
                </div>
                <button type="button" class="btn-close ${closeButtonClass} me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
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

    // Clean up after hiding
    toastElement.addEventListener('hidden.bs.toast', function () {
        toastElement.remove();
    });
}

// Helper function to get appropriate icon for toast type
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

// Helper function to escape HTML to prevent XSS
function escapeHtml(unsafe) {
    if (typeof unsafe !== 'string') return '';
    return unsafe
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}

// Global loader functions
function showLoader() {
    if (!document.querySelector('.loader-overlay')) {
        const loader = document.createElement('div');
        loader.className = 'loader-overlay';
        loader.innerHTML = '<div class="spinner-border text-primary" role="status"></div>';
        document.body.appendChild(loader);
    }
}

function hideLoader() {
    const loader = document.querySelector('.loader-overlay');
    if (loader) {
        loader.remove();
    }
}

// Handle approval responses consistently
function handleApprovalResponse(response, entityType, successRedirect) {
    let isApprovalRequest = false;
    let message = '';

    // Parse string responses
    if (typeof response === 'string') {
        try {
            response = JSON.parse(response);
        } catch (e) {
            // Not JSON, assume direct success
            showToast(`${entityType} created successfully!`, 'success');
            setTimeout(() => window.location.href = successRedirect, 1500);
            return;
        }
    }

    // Check for approval indicators
    if (response && typeof response === 'object') {
        if (response.status === 'PendingApproval' ||
            response.Status === 'PendingApproval' ||
            response.approvalRequestId ||
            response.ApprovalRequestId ||
            (response.message && response.message.includes('approval')) ||
            (response.Message && response.Message.includes('approval'))) {
            isApprovalRequest = true;
            message = response.message || response.Message ||
                `Your ${entityType.toLowerCase()} request has been submitted for approval.`;
        }
    }

    // Show appropriate message
    if (isApprovalRequest) {
        showToast(message, 'info');
    } else {
        showToast(`${entityType} operation completed successfully!`, 'success');
    }

    // Redirect after delay
    setTimeout(() => window.location.href = successRedirect, 2000);
}