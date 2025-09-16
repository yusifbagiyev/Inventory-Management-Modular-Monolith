window.AjaxHandler = (function () {
    'use strict';

    // Store original button states
    const buttonStates = new WeakMap();

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

        $form.on('submit', function (e) {
            e.preventDefault();
            const form = this;
            const $submitBtn = $(form).find('button[type="submit"]');

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

            // Store original button state ( only once )
            if ($submitBtn.length && !buttonStates.has($submitBtn[0])) {
                    buttonStates.set($submitBtn[0], {
                        html: $submitBtn.html(),
                        disabled: $submitBtn.prop('disabled')
                    });
                }

            // Call before submit hook
            if (settings.onBeforeSubmit) {
                const shouldContinue = settings.onBeforeSubmit(form);
                if (shouldContinue === false) return false;
            }

            // Disable button and show loading
            $submitBtn.prop('disabled', true)
                .html('<span class="spinner-border spinner-border-sm me-2"></span>Processing...');

            // Prepare form data
            const formData = new FormData(form);

            // Submit form via AJAX with proper error handling
            $.ajax({
                url: form.action,
                type: form.method || 'POST',
                data: formData,
                processData: false,
                contentType: false,
                success: function (response, textStatus, xhr) {
                    // ALWAYS reset button first, no matter what
                    resetButton($submitBtn);

                    // Handle different response types
                    const contentType = xhr.getResponseHeader('content-type') || '';

                    if (contentType.indexOf('text/html') > -1) {
                        // Handle HTML response (server-side validation errors)
                        handleHtmlResponse(response, form, settings);
                    } else {
                        // Handle JSON response
                        handleSuccess(response, form, $submitBtn, settings);
                    }
                },
                error: function (xhr, status, error) {
                    // ALWAYS reset button on error
                    resetButton($submitBtn);
                    handleError(xhr, form, $submitBtn, settings);
                },
                complete: function () {
                    // Failsafe: Always ensure button is reset after 3 seconds
                    setTimeout(function () {
                        resetButton($submitBtn);
                    }, 3000);
                }
            });
        });
    }

    function resetButton($submitBtn) {
        if (!$submitBtn || !$submitBtn.length) return;

        const originalState = buttonStates.get($submitBtn[0]);
        if (originalState) {
            $submitBtn.prop('disabled', originalState.disabled)
                .html(originalState.html);
        } else {
            // Fallback if no original state stored
            const currentHtml = $submitBtn.html();
            const newHtml = currentHtml.replace(/<span[^>]*>.*?<\/span>\s*/gi, '')
                .replace('Processing...', 'Submit');
            $submitBtn.prop('disabled', false).html(newHtml);
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
                AjaxHandler.handleForm($newForm, settings);
            }
        }
    }

    function handleSuccess(response, form, $submitBtn, settings) {
        // Check for various error indicators
        if (response && (
            response.isSuccess === false ||
            response.success === false ||
            (response.message && response.message.toLowerCase().includes('error'))
        )) {
            // This is actually an error response
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

        showToast(settings.successMessage, 'success');

        if (settings.resetFormOnSuccess) {
            form.reset();
        }

        if (settings.successRedirect) {
            setTimeout(() => window.location.href = settings.successRedirect, settings.redirectDelay);
        }
    }

    function handleError(xhr, form, $submitBtn, settings) {
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

    // Public API
    return {
        handleForm: handleForm,
        resetButton: resetButton
    };
})();

// Global error handler utility
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