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

        // Get the specific form element(s)
        const $forms = $(formSelector);

        if (!$forms.length) {
            console.error('Form not found:', formSelector);
            return;
        }

        // Handle each form individually to avoid conflicts
        $forms.each(function () {
            const $individualForm = $(this);

            // Find the submit button ONLY within THIS specific form
            // We use a more specific search that excludes nested forms
            const $submitBtnInThisForm = $individualForm.find('button[type="submit"]').filter(function () {
                // Make sure this button is a direct child of THIS form, not a nested form
                return $(this).closest('form')[0] === $individualForm[0];
            });

            // If no submit button found in this form, skip it
            if (!$submitBtnInThisForm.length) {
                console.warn('No submit button found in form:', $individualForm);
                return; // Skip this form
            }

            // Store the original state of THIS form's button
            const originalButtonHtml = $submitBtnInThisForm.html();
            const originalButtonDisabled = $submitBtnInThisForm.prop('disabled');

            // Attach submit handler to THIS specific form
            $individualForm.off('submit.ajaxHandler').on('submit.ajaxHandler', function (e) {
                e.preventDefault();
                const form = this;

                // Get the submit button for THIS form again (in case DOM changed)
                const $currentSubmitBtn = $(form).find('button[type="submit"]').filter(function () {
                    return $(this).closest('form')[0] === form;
                });

                // Validate form first
                if (settings.validateBeforeSubmit) {
                    if (!form.checkValidity()) {
                        form.reportValidity();
                        return false;
                    }

                    if ($.validator && !$(form).valid()) {
                        return false;
                    }
                }

                // Call before submit hook
                if (settings.onBeforeSubmit) {
                    const shouldContinue = settings.onBeforeSubmit(form);
                    if (shouldContinue === false) return false;
                }

                // Disable button and show loading
                $currentSubmitBtn.prop('disabled', true)
                    .html('<span class="spinner-border spinner-border-sm me-2"></span>Processing...');

                // Prepare form data
                const formData = new FormData(form);

                // Helper function to restore THIS form's button
                const restoreButton = () => {
                    $currentSubmitBtn.prop('disabled', originalButtonDisabled)
                        .html(originalButtonHtml);
                };

                // Submit form via AJAX
                $.ajax({
                    url: form.action || window.location.href,
                    type: form.method || 'POST',
                    data: formData,
                    processData: false,
                    contentType: false,
                    success: function (response, textStatus, xhr) {
                        // Handle different response types
                        const contentType = xhr.getResponseHeader('content-type') || '';

                        if (contentType.indexOf('text/html') > -1) {
                            handleHtmlResponse(response, form, settings);
                        } else {
                            handleSuccess(response, form, settings);
                        }
                    },
                    error: function (xhr, status, error) {
                        // Always restore button on error
                        restoreButton();
                        handleError(xhr, form, settings);
                    },
                    complete: function () {
                        // Failsafe: Always ensure button is restored after 3 seconds
                        setTimeout(() => {
                            restoreButton();
                        }, 3000);
                    }
                });
            });
        });
    }

    // Simplified handleSuccess function
    function handleSuccess(response, form, settings) {
        // FIRST: Check if this is an approval request (before checking for errors)
        if (isApprovalRequest(response)) {
            const message = response.message || 'Request submitted for approval';
            showToast(message, 'info');

            // Still redirect for approval requests
            if (settings.successRedirect) {
                setTimeout(() => window.location.href = settings.successRedirect, settings.redirectDelay);
            }
            return;
        }

        // THEN: Check for actual errors
        if (response && (
            response.isSuccess === false ||
            response.success === false ||
            (response.message && response.message.toLowerCase().includes('error'))
        )) {
            const errorMessage = response.message || 'Operation failed';
            showToast(errorMessage, 'error');

            if (settings.onError) {
                settings.onError(errorMessage, response);
            }
            return; // Don't redirect on actual errors
        }

        // Finally: Handle normal success
        if (settings.onSuccess) {
            const result = settings.onSuccess(response);
            if (result === false) return;
        }

        showToast(settings.successMessage, 'success');

        if (settings.resetFormOnSuccess) {
            form.reset();
        }

        if (settings.successRedirect) {
            setTimeout(() => window.location.href = settings.successRedirect, settings.redirectDelay);
        }
    }

    // Rest of your functions remain the same...
    function handleError(xhr, form, settings) {
        let errorMessage = 'An error occurred';
        let validationErrors = null;

        try {
            if (xhr.responseJSON) {
                errorMessage = xhr.responseJSON.message ||
                    xhr.responseJSON.error ||
                    xhr.responseJSON.title ||
                    errorMessage;

                if (xhr.responseJSON.errors) {
                    validationErrors = xhr.responseJSON.errors;
                    displayValidationErrors(form, validationErrors);
                }
            } else if (xhr.responseText) {
                try {
                    const response = JSON.parse(xhr.responseText);
                    errorMessage = response.message || errorMessage;
                } catch (e) {
                    if (xhr.responseText.length < 500 && !xhr.responseText.includes('<')) {
                        errorMessage = xhr.responseText;
                    }
                }
            }

            // Handle specific status codes
            if (xhr.status === 400) {
                errorMessage = errorMessage || 'Invalid request. Please check your input.';
            } else if (xhr.status === 401) {
                errorMessage = 'Session expired. Please login again.';
                setTimeout(() => window.location.href = '/Account/Login', 2000);
            } else if (xhr.status === 403) {
                errorMessage = 'You do not have permission to perform this action.';
            } else if (xhr.status === 409) {
                errorMessage = errorMessage || 'This item already exists.';
            } else if (xhr.status >= 500) {
                errorMessage = 'Server error occurred. Please try again later.';
            }
        } catch (e) {
            console.error('Error parsing error response:', e);
        }

        showToast(errorMessage, 'error');

        if (settings.onError) {
            settings.onError(errorMessage, xhr);
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
        // Clear previous validation errors
        $(form).find('.field-validation-error').removeClass('field-validation-error');
        $(form).find('.validation-message').remove();

        if (typeof errors === 'object') {
            for (const field in errors) {
                const $field = $(form).find(`[name="${field}"]`);
                if ($field.length) {
                    $field.addClass('is-invalid');
                    const messages = Array.isArray(errors[field]) ?
                        errors[field] : [errors[field]];
                    const errorHtml = `<span class="text-danger validation-message">
                                        ${messages.join(', ')}</span>`;
                    $field.after(errorHtml);
                }
            }
        }
    }

    function handleHtmlResponse(html, form, settings) {
        // Replace form with server response (for server-side validation)
        const $container = $(form).closest('.card-body');
        if ($container.length) {
            $container.html(html);
            // Re-attach handler to new form
            const $newForm = $container.find('form');
            if ($newForm.length) {
                // Use a more specific selector for the new form
                const formId = $newForm.attr('id');
                if (formId) {
                    AjaxHandler.handleForm('#' + formId, settings);
                } else {
                    // Add a unique identifier to the form
                    const uniqueId = 'form-' + Date.now();
                    $newForm.attr('id', uniqueId);
                    AjaxHandler.handleForm('#' + uniqueId, settings);
                }
            }
        }
    }

    // Public API
    return {
        handleForm: handleForm
    };
})();

// Global error handler utility remains the same
window.ErrorHandler = {
    parseErrorMessage: function (xhr, defaultMessage) {
        defaultMessage = defaultMessage || 'An error occurred';

        try {
            if (xhr.responseJSON) {
                return xhr.responseJSON.message ||
                    xhr.responseJSON.error ||
                    defaultMessage;
            } else if (xhr.responseText) {
                const response = JSON.parse(xhr.responseText);
                return response.message || defaultMessage;
            }
        } catch (e) {
            console.error('Error parsing response:', e);
        }

        return defaultMessage;
    }
};