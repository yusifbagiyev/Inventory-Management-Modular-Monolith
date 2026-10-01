window.AjaxHandler = (function () {
    'use strict';

    // Add a registry to track form submission states
    const submissionStates = new WeakMap();
    function handleForm(formSelector, options) {
        const defaults = {
            validateBeforeSubmit: true,
            successMessage: t('Operation completed successfully'),
            successRedirect: null,
            redirectDelay: 1500,
            resetFormOnSuccess: false,
            onSuccess: null,
            onError: null,
            onBeforeSubmit: null
        };

        const settings = { ...defaults, ...options };

        // Get the specific form element(s). Layout forms that must post normally (language switch,
        // sign out) carry data-no-ajax, so a page calling handleForm('form') leaves them alone.
        const $forms = $(formSelector).not('[data-no-ajax]');

        if (!$forms.length) {
            console.error('Form not found:', formSelector);
            return;
        }

        // Handle each form individually to avoid conflicts
        $forms.each(function () {
            const $individualForm = $(this);
            const formElement = this;


            // Initialize submission state for this form
            if (!submissionStates.has(formElement)) {
                submissionStates.set(formElement, { isSubmitting: false });
            }

            // Remove any existing handlers first (important!)
            $individualForm.off('submit.ajaxHandler');

            // Find submit button for this specific form
            const $submitBtnInThisForm = $individualForm.find('button[type="submit"]').filter(function () {
                return $(this).closest('form')[0] === formElement;
            });

            if (!$submitBtnInThisForm.length) {
                console.warn('No submit button found in form:', $individualForm);
                return;
            }

            // Store original button state
            const originalButtonHtml = $submitBtnInThisForm.html();
            const originalButtonDisabled = $submitBtnInThisForm.prop('disabled');

            // Add a click handler to prevent rapid clicks
            $submitBtnInThisForm.off('click.preventDouble');
            $submitBtnInThisForm.on('click.preventDouble', function (e) {
                const formState = submissionStates.get(formElement);
                if (formState.isSubmitting) {
                    e.preventDefault();
                    e.stopPropagation();
                    e.stopImmediatePropagation();
                    return false;
                }
            });


            // Attach the submit handler
            $individualForm.on('submit.ajaxHandler', function (e) {
                e.preventDefault();
                e.stopPropagation(); // Prevent event bubbling

                const form = this;
                const formState = submissionStates.get(form);

                // Check if already submitting
                if (formState.isSubmitting) {
                    return false;
                }

                // IMMEDIATELY mark as submitting and disable button
                formState.isSubmitting = true;
                const $currentSubmitBtn = $(form).find('button[type="submit"]').filter(function () {
                    return $(this).closest('form')[0] === form;
                });

                $currentSubmitBtn.prop('disabled', true)
                    .html('<span class="spinner-border spinner-border-sm me-2"></span>' + t('Processing...'));


                // Validate form
                if (settings.validateBeforeSubmit) {
                    if (!form.checkValidity()) {
                        form.reportValidity();
                        formState.isSubmitting = false;
                        $currentSubmitBtn.prop('disabled', originalButtonDisabled).html(originalButtonHtml);
                        return false;
                    }

                    if ($.validator && !$(form).valid()) {
                        formState.isSubmitting = false;
                        $currentSubmitBtn.prop('disabled', originalButtonDisabled).html(originalButtonHtml);
                        return false;
                    }
                }

                // Call before submit hook
                if (settings.onBeforeSubmit) {
                    const shouldContinue = settings.onBeforeSubmit(form);
                    if (shouldContinue === false) {
                        formState.isSubmitting = false;
                        $currentSubmitBtn.prop('disabled', originalButtonDisabled).html(originalButtonHtml);
                        return false;
                    }
                }

                // Disable button and show loading
                $currentSubmitBtn.prop('disabled', true)
                    .html('<span class="spinner-border spinner-border-sm me-2"></span>' + t('Processing...'));

                // Prepare form data
                const formData = new FormData(form);

                // Helper function to restore button
                const restoreButton = () => {
                    $currentSubmitBtn.prop('disabled', originalButtonDisabled)
                        .html(originalButtonHtml);
                    formState.isSubmitting = false; // Reset submission state
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
                            handleHtmlResponse(response);
                        } else {
                            handleSuccess(response, form, settings);
                        }
                        restoreButton(); // Always restore after success
                    },
                    error: function (xhr, status, error) {
                        restoreButton(); // Always restore on error
                        handleError(xhr, form, settings);
                    },
                    complete: function () {
                        // Failsafe: Always ensure button is restored and state is reset
                        setTimeout(() => {
                            restoreButton();
                        }, 3000);
                    }
                });

                return false; // Prevent default form submission
            });
        });
    }

    // Simplified handleSuccess function
    function handleSuccess(response, form, settings) {
        // FIRST: Check if this is an approval request (before checking for errors)
        if (isApprovalRequest(response)) {
            const message = response.message || t('Request submitted for approval');
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
            const errorMessage = response.message || t('Operation failed');
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
        let errorMessage = t('An error occurred');
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
                errorMessage = errorMessage || t('Invalid request. Please check your input.');
            } else if (xhr.status === 401) {
                errorMessage = t('Session expired. Please login again.');
                setTimeout(() => window.location.href = '/Account/Login', 2000);
            } else if (xhr.status === 403) {
                errorMessage = t('You do not have permission to perform this action.');
            } else if (xhr.status === 409) {
                errorMessage = errorMessage || t('This item already exists.');
            } else if (xhr.status >= 500) {
                errorMessage = t('Server error occurred. Please try again later.');
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

    // A page instead of JSON: the session expired (the login page came back) or the server
    // failed in an unexpected way. Never leave the user with a silently restored button.
    function handleHtmlResponse(html) {
        if (/action="\/Account\/Login/i.test(html)) {
            window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(location.pathname + location.search);
            return;
        }
        showToast(t('Unexpected response from the server. Please reload the page.'), 'error');
    }

    // Public API
    return {
        handleForm: handleForm
    };
})();

// Global error handler utility remains the same
window.ErrorHandler = {
    parseErrorMessage: function (xhr, defaultMessage) {
        defaultMessage = defaultMessage || t('An error occurred');

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