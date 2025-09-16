// wwwroot/js/form-handler.js
window.FormHandler = (function () {
    'use strict';

    // Handle form submissions without jQuery/AJAX
    function handleForm(formSelector, options = {}) {
        const form = document.querySelector(formSelector);
        if (!form) {
            console.error('Form not found:', formSelector);
            return;
        }

        form.addEventListener('submit', async function (e) {
            e.preventDefault();

            const submitButton = form.querySelector('button[type="submit"]');
            const originalButtonHtml = submitButton ? submitButton.innerHTML : '';

            try {
                // Disable submit button and show loading
                if (submitButton) {
                    submitButton.disabled = true;
                    submitButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Processing...';
                }

                // Create form data
                const formData = new FormData(form);

                // Make the request
                const response = await fetch(form.action, {
                    method: form.method || 'POST',
                    body: formData,
                    credentials: 'same-origin'
                });

                // Get response based on content type
                const contentType = response.headers.get('content-type');
                let result;

                if (contentType && contentType.includes('application/json')) {
                    result = await response.json();
                } else {
                    result = await response.text();
                }

                // Handle response
                if (response.ok) {
                    // Check if it's an approval request
                    if (result.isApprovalRequest || result.status === 'PendingApproval') {
                        NotificationSystem.showInfo(result.message || 'Request submitted for approval');
                    } else {
                        // Success
                        NotificationSystem.showSuccess(options.successMessage || 'Operation completed successfully');
                    }

                    // Redirect if specified
                    if (options.successRedirect) {
                        setTimeout(() => {
                            window.location.href = options.successRedirect;
                        }, options.redirectDelay || 1500);
                    }

                    // Call success callback
                    if (options.onSuccess) {
                        options.onSuccess(result);
                    }
                } else {
                    // Error
                    const errorMessage = result.message || result.error || 'An error occurred';
                    NotificationSystem.showError(errorMessage);

                    // Call error callback
                    if (options.onError) {
                        options.onError(errorMessage, result);
                    }
                }
            } catch (error) {
                console.error('Form submission error:', error);
                NotificationSystem.showError('Failed to submit form');
            } finally {
                // Reset submit button
                if (submitButton) {
                    submitButton.disabled = false;
                    submitButton.innerHTML = originalButtonHtml;
                }
            }
        });
    }

    // Public API
    return {
        handleForm: handleForm
    };
})();