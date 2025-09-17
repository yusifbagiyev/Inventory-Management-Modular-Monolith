// Simplified and more reliable ajaxHandler.js
window.AjaxHandler = (function () {
    'use strict';

    function handleForm(formSelector, options) {
        const defaults = {
            validateBeforeSubmit: true,
            successMessage: 'Operation completed successfully',
            successRedirect: null,
            redirectDelay: 1500,
            resetFormOnSuccess: false,
            onSuccess: null,
            onError: null,
            onBeforeSubmit: null
        };

        const settings = { ...defaults, ...options };
        const $form = $(formSelector);

        if (!$form.length) {
            console.error('Form not found:', formSelector);
            return;
        }

        // Remove any existing submit handlers to prevent duplicates
        $form.off('submit.ajaxHandler');

        // Attach our submit handler with a namespace
        $form.on('submit.ajaxHandler', function (e) {
            e.preventDefault();
            console.log('Form submission intercepted by AjaxHandler');

            const form = this;
            const $submitBtn = $(form).find('button[type="submit"]');

            // Store the original button HTML directly as a data attribute
            // This is more reliable than WeakMap
            if ($submitBtn.length && !$submitBtn.data('original-html')) {
                $submitBtn.data('original-html', $submitBtn.html());
                $submitBtn.data('original-disabled', $submitBtn.prop('disabled'));
                console.log('Stored original button state:', $submitBtn.data('original-html'));
            }

            // Validate form if required
            if (settings.validateBeforeSubmit) {
                if (!form.checkValidity()) {
                    form.reportValidity();
                    return false;
                }
            }

            // Call before submit hook if provided
            if (settings.onBeforeSubmit) {
                const shouldContinue = settings.onBeforeSubmit(form);
                if (shouldContinue === false) {
                    return false;
                }
            }

            // Disable button and show loading state
            const originalHtml = $submitBtn.data('original-html') || $submitBtn.html();
            $submitBtn.prop('disabled', true);
            $submitBtn.html('<span class="spinner-border spinner-border-sm me-2"></span>Processing...');
            console.log('Button disabled, showing processing state');

            // Create a function to reset the button
            // We define this here so it has access to the correct button reference
            const resetButtonState = function () {
                console.log('Resetting button state');
                $submitBtn.prop('disabled', false);
                $submitBtn.html(originalHtml);
                console.log('Button reset complete');
            };

            // Prepare form data
            const formData = new FormData(form);

            // Make the AJAX request
            $.ajax({
                url: form.action,
                type: form.method || 'POST',
                data: formData,
                processData: false,
                contentType: false,
                success: function (response, textStatus, xhr) {
                    console.log('AJAX success response:', response);

                    // First, always reset the button
                    resetButtonState();

                    // Check if this is an HTML response (server-side validation error)
                    const contentType = xhr.getResponseHeader('content-type') || '';
                    if (contentType.indexOf('text/html') > -1) {
                        console.log('Received HTML response, likely validation errors');
                        // If we get HTML back, it's probably a validation error
                        // Don't replace the form, just show an error
                        showToast('Please check the form for validation errors', 'error');
                        return;
                    }

                    // Handle JSON response
                    handleJsonResponse(response, form, settings);
                },
                error: function (xhr, status, error) {
                    console.log('AJAX error:', status, error);
                    console.log('Response status:', xhr.status);
                    console.log('Response text:', xhr.responseText);

                    // Always reset the button first
                    resetButtonState();

                    // Parse and handle the error
                    let errorMessage = 'An error occurred';

                    try {
                        if (xhr.responseJSON) {
                            errorMessage = xhr.responseJSON.message ||
                                xhr.responseJSON.error ||
                                xhr.responseJSON.title ||
                                errorMessage;

                            // Handle validation errors
                            if (xhr.responseJSON.errors) {
                                displayValidationErrors(form, xhr.responseJSON.errors);
                            }
                        } else if (xhr.responseText && xhr.responseText.length < 500) {
                            // Try to parse as JSON
                            try {
                                const response = JSON.parse(xhr.responseText);
                                errorMessage = response.message || errorMessage;
                            } catch (e) {
                                // If not JSON and short enough, use as is
                                if (!xhr.responseText.includes('<')) {
                                    errorMessage = xhr.responseText;
                                }
                            }
                        }

                        // Handle specific HTTP status codes
                        if (xhr.status === 400) {
                            errorMessage = errorMessage || 'Invalid request. Please check your input.';
                        } else if (xhr.status === 401) {
                            errorMessage = 'Session expired. Redirecting to login...';
                            setTimeout(() => window.location.href = '/Account/Login', 2000);
                        } else if (xhr.status === 403) {
                            errorMessage = 'You do not have permission to perform this action.';
                        } else if (xhr.status === 409) {
                            errorMessage = errorMessage || 'This item already exists or conflicts with existing data.';
                        } else if (xhr.status >= 500) {
                            errorMessage = 'Server error occurred. Please try again later.';
                        }
                    } catch (e) {
                        console.error('Error parsing error response:', e);
                    }

                    // Show error toast
                    showToast(errorMessage, 'error');

                    // Call error callback if provided
                    if (settings.onError) {
                        settings.onError(errorMessage, xhr);
                    }
                },
                complete: function () {
                    console.log('AJAX request complete');
                    // As a final failsafe, ensure button is reset after 2 seconds
                    setTimeout(function () {
                        if ($submitBtn.prop('disabled')) {
                            console.log('Failsafe: Button still disabled, forcing reset');
                            resetButtonState();
                        }
                    }, 2000);
                }
            });

            // Return false to prevent any default form submission
            return false;
        });
    }

    function handleJsonResponse(response, form, settings) {
        console.log('Processing JSON response:', response);

        // Check if this is actually an error response
        if (response && (response.isSuccess === false || response.success === false)) {
            const errorMessage = response.message || 'Operation failed';
            showToast(errorMessage, 'error');

            if (settings.onError) {
                settings.onError(errorMessage, response);
            }
            return;
        }

        // Check if it's an approval request
        if (isApprovalRequest(response)) {
            const message = response.message || response.Message || 'Request submitted for approval';
            showToast(message, 'info');

            if (settings.successRedirect) {
                setTimeout(() => window.location.href = settings.successRedirect, settings.redirectDelay);
            }
            return;
        }

        // Handle success
        if (settings.onSuccess) {
            const result = settings.onSuccess(response);
            if (result === false) return;
        }

        // Show success message
        showToast(settings.successMessage, 'success');

        // Reset form if requested
        if (settings.resetFormOnSuccess && form) {
            form.reset();
        }

        // Redirect if specified
        if (settings.successRedirect) {
            setTimeout(() => window.location.href = settings.successRedirect, settings.redirectDelay);
        }
    }

    function isApprovalRequest(response) {
        if (!response) return false;

        return response.isApprovalRequest === true ||
            response.status === 'PendingApproval' ||
            response.Status === 'PendingApproval' ||
            response.approvalRequestId != null ||
            response.ApprovalRequestId != null;
    }

    function displayValidationErrors(form, errors) {
        console.log('Displaying validation errors:', errors);

        // Clear previous validation errors
        $(form).find('.field-validation-error').removeClass('field-validation-error');
        $(form).find('.validation-message').remove();
        $(form).find('.is-invalid').removeClass('is-invalid');

        if (typeof errors === 'object') {
            for (const field in errors) {
                const $field = $(form).find(`[name="${field}"]`);
                if ($field.length) {
                    $field.addClass('is-invalid');
                    const messages = Array.isArray(errors[field]) ? errors[field] : [errors[field]];
                    const errorHtml = `<span class="text-danger validation-message">${messages.join(', ')}</span>`;
                    $field.after(errorHtml);
                }
            }
        }
    }

    // Public API
    return {
        handleForm: handleForm
    };
})();

// Also add this helper function to ensure showToast is available
if (typeof showToast === 'undefined') {
    window.showToast = function (message, type) {
        console.log(`Toast [${type}]: ${message}`);
        // You can implement your toast logic here or it should already be in site.js
    };
}