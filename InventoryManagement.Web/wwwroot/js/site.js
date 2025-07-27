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
    // Map type to Bootstrap color classes
    const toastHtml = {
        'success': 'bg-success text-white',
        'error': 'bg-danger text-white',
        'warning': 'bg-warning',
        'info': 'bg-info text-white',
        'danger': 'bg-danger text-white'
    }
    const colorClasses = typeMap[type] || typeMap['info'];

    // Create toast HTML with proper styling
    const toastHtml = `
        <div class="toast align-items-center ${colorClasses} border-0" role="alert" aria-live="assertive" aria-atomic="true">
            <div class="d-flex">
                <div class="toast-body">
                    ${escapeHtml(message)}
                </div>
                <button type="button" class="btn-close ${type === 'warning' ? '' : 'btn-close-white'} me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
        </div>
    `;

    // Ensure toast container exists
    let container = document.getElementById('toast-container');

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
    const toastElement = container.lastElementChild;
    const toast = new bootstrap.Toast(toastElement, {
        delay: duration,
        autohide: true
    });

    toast.show();

    // Remove element after it's hidden
    toastElement.addEventListener('hidden.bs.toast', function () {
        toastElement.remove();
    });
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


function initializeImageZoom() {
    $('.zoomable-image').click(function () {
        var src = $(this).attr('src');
        var alt = $(this).attr('alt');
        showImageModal(src, alt);
    });
}

function showImageModal(src, filename) {
    var modal = $(`
        <div class="image-modal" onclick="closeImageModal()">
            <img class="image-modal-content" src="${src}" alt="${filename}">
            <a class="image-download" onclick="event.stopPropagation(); downloadImage('${src}', '${filename}')" title="Download">
                <i class="fas fa-download"></i>
            </a>
        </div>
    `);

    $('body').append(modal);
    $('.image-modal').fadeIn();
}

function closeImageModal() {
    $('.image-modal').fadeOut(function () {
        $(this).remove();
    });
}

// Initialize on page load
$(document).ready(function () {
    initializeImageZoom();
});

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