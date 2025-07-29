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
});

// AJAX helper functions
function showSpinner() {
    const spinner = document.createElement('div');
    spinner.className = 'spinner-overlay';
    spinner.innerHTML = '<div class="spinner-border text-light" style="width: 3rem; height: 3rem;"></div>';
    document.body.appendChild(spinner);
}

function hideSpinner() {
    const spinner = document.querySelector('.spinner-overlay');
    if (spinner) {
        spinner.remove();
    }
}

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

// Format date helper
function formatDate(dateString) {
    const options = { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' };
    return new Date(dateString).toLocaleDateString('en-US', options);
}

// Copy to clipboard
function copyToClipboard(text) {
    navigator.clipboard.writeText(text).then(function () {
        showToast('Copied to clipboard!', 'success');
    });
}

// Toast notification
function showToast(message, type = 'info', duration = 5000) {
    / Ensure we have a valid type
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

    const toastHtml = `
        <div id="${toastId}" class="toast" role="alert" aria-live="assertive" aria-atomic="true" data-bs-autohide="true" data-bs-delay="${duration}">
            <div class="toast-header bg-${type} text-white">
                <i class="fas fa-${getToastIcon(type)} me-2"></i>
                <strong class="me-auto">${getToastTitle(type)}</strong>
                <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
            <div class="toast-body">
                ${escapeHtml(message)}
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
    const toast = new bootstrap.Toast(toastElement);
    toast.show();

    // Remove element after it's hidden
    toastElement.addEventListener('hidden.bs.toast', function () {
        toastElement.remove();
    });
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

// Helper function to get toast title
function getToastTitle(type) {
    const titles = {
        'success': 'Success',
        'danger': 'Error',
        'warning': 'Warning',
        'info': 'Information',
        'secondary': 'Notice'
    };
    return titles[type] || 'Notice';
}

// Helper function to escape HTML
function escapeHtml(unsafe) {
    return unsafe.replace(/&/g, "&amp;")
                 .replace(/</g, "&lt;")
                 .replace(/>/g, "&gt;")
                 .replace(/"/g, "&quot;")
                 .replace(/'/g, "&#039;");
}


// Global function to reset form state
function resetFormState(fromElement){
    // Find all submit buttons in the form
    const submitButtons = fromElement.querySelectorAll('button[type="submit"]');

    submitButtons.forEach(button => {
        // Reset button state
        button.disabled = false;

        // Restore original text (store it first if not already)
        if (button.dataset.originalText) {
            button.innerHTML = button.dataset.originalText;
        } else {
            // Remove spinner if present
            const spinner = button.querySelector('.spinner-border');
            if (spinner) {
                spinner.remove();
            }
            // Remove "Processing..." text
            button.innerHTML = button.innerHTML.replace('Processing...', 'Submit');
        }
    });
}

notificationConnection.onreconnecting((error) => {
    console.log('SignalR Reconnecting:', error);
});

notificationConnection.onreconnected((connectionId) => {
    console.log('SignalR Reconnected:', connectionId);
});

notificationConnection.onclose((error) => {
    console.log('SignalR Connection closed:', error);
});
function loadRecentNotifications() {
    $('#notificationList').html(`
        <div class="text-center py-3">
            <div class="spinner-border spinner-border-sm" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
        </div>
    `);

    $.ajax({
        url: '/Notifications/GetRecentNotifications',
        type: 'GET',
        headers: {
            'Authorization': 'Bearer ' + $('#jwtToken').val()
        },
        success: function (notifications) {
            renderNotifications(notifications);
        },
        error: function (xhr, status, error) {
            console.error('Failed to load notifications:', error);
            $('#notificationList').html(`
                <div class="dropdown-item text-center py-3 text-danger">
                    <i class="fas fa-exclamation-circle"></i> Failed to load notifications
                </div>
            `);
        }
    });
}
function showLoader() {
    if (!$('.loader-overlay').length) {
        $('body').append('<div class="loader-overlay"><div class="spinner-border text-primary" role="status"></div></div>');
    }
}

function hideLoader() {
    $('.loader-overlay').remove();
}

// Update AJAX calls to show/hide loader
$.ajaxSetup({
    beforeSend: function () {
        showLoader();
    },
    complete: function () {
        hideLoader();
    }
});
function handleApprovalResponse(response, entityType, successRedirect) {
    // Debug logging
    console.log('Response type:', typeof response);
    console.log('Response:', response);

    let isApprovalRequest = false;
    let message = '';

    // Handle different response formats
    if (typeof response === 'string') {
        try {
            const parsed = JSON.parse(response);
            response = parsed;
        } catch (e) {
            // If not JSON, assume direct success
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
            response.message?.includes('approval') ||
            response.Message?.includes('approval')) {
            isApprovalRequest = true;
            message = response.message || response.Message ||
                `Your ${entityType.toLowerCase()} request has been submitted for approval. You will be notified once it's processed.`;
        }
    }

    if (isApprovalRequest) {
        showToast(message, 'info');
    } else {
        showToast(`${entityType} operation completed successfully!`, 'success');
    }

    setTimeout(() => window.location.href = successRedirect, 2000);
}