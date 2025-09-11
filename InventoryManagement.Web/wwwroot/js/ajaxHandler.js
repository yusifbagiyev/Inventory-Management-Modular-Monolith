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

        $form.on('submit', function (e) {
            e.preventDefault();
            const form = this;
            const $submitBtn = $(form).find('button[type="submit"]');

            if ($submitBtn.length && !$submitBtn.data('original-html')) {
                $submitBtn.data('original-html', $submitBtn.html());
                $submitBtn.data('original-disabled', $submitBtn.prop('disabled') || false);
            }

            // Validate form first
            if (settings.validateBeforeSubmit) {
                if (!form.checkValidity()) {
                    form.reportValidity();
                    return false;
                }

                // Check jQuery validation if available
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
            $submitBtn.prop('disabled', true)
                .html('<span class="spinner-border spinner-border-sm me-2"></span>Processing...');

            // Prepare form data
            const formData = new FormData(form);

            // Submit form via AJAX
            $.ajax({
                url: form.action,
                type: form.method || 'POST',
                data: formData,
                processData: false,
                contentType: false,
                success: function (response, textStatus, xhr) {
                    // Check if server returned HTML (validation errors from server-side)
                    const contentType = xhr.getResponseHeader('content-type') || '';
                    if (typeof response === 'string' && contentType.indexOf('text/html') !== -1) {
                        // Server returned HTML, likely validation errors
                        handleServerValidationHTML(response, form, $submitBtn, settings);
                        return;
                    }

                    // Normal JSON response handling
                    handleSuccess(response, form, $submitBtn, settings);
                },
                error: function (xhr, status, error) {
                    handleError(xhr, form, $submitBtn, settings);
                }
            });
        });
    }

    function handleServerValidationHTML(html, form, $submitBtn, settings) {
        // Reset button immediately when we get HTML back (server-side validation failed)
        resetButton($submitBtn);

        // Replace form content with server response if container exists
        const $container = $(form).closest('.card-body');
        if ($container.length) {
            $container.html(html);

            // Re-bind the handler to the new form
            const $newForm = $container.find('form');
            if ($newForm.length) {
                AjaxHandler.handleForm($newForm, settings);
            }
        }
    }

    function handleSuccess(response, form, $submitBtn, settings) {
        // Always reset button first
        resetButton($submitBtn);

        // Check if it's an approval request
        if (isApprovalRequest(response)) {
            const message = response.message || response.Message ||
                'Request submitted for approval';
            showToast(message, 'info');

            if (settings.successRedirect) {
                setTimeout(() => window.location.href = settings.successRedirect,
                    settings.redirectDelay);
            }
            return;
        }

        // Check if response indicates actual success
        if (response && response.isSuccess === false) {
            // This is actually an error response
            const errorMessage = response.message || 'Operation failed';
            showToast(errorMessage, 'error');

            if (settings.onError) {
                settings.onError(errorMessage, response);
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
            setTimeout(() => window.location.href = settings.successRedirect,
                settings.redirectDelay);
        }
    }

    function handleError(xhr, form, $submitBtn, settings) {
        // ALWAYS reset button on error
        resetButton($submitBtn);

        let errorMessage = 'An error occurred';
        let validationErrors = null;

        try {
            if (xhr.responseJSON) {
                errorMessage = xhr.responseJSON.message ||
                    xhr.responseJSON.error ||
                    xhr.responseJSON.title ||
                    errorMessage;

                // Check for validation errors
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
            } else if (xhr.status === 404) {
                errorMessage = 'Resource not found.';
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

    function resetButton($submitBtn) {
        if (!$submitBtn || !$submitBtn.length) return;

        // Try to get the stored original HTML first
        const originalHtml = $submitBtn.data('original-html');
        const originalDisabled = $submitBtn.data('original-disabled');

        if (originalHtml !== undefined) {
            // Restore from data attributes (most reliable)
            $submitBtn.html(originalHtml);
            $submitBtn.prop('disabled', originalDisabled || false);
        } else {
            // Fallback: Remove spinner and restore generic text
            $submitBtn.find('.spinner-border').remove();
            const currentHtml = $submitBtn.html();
            const cleanedHtml = currentHtml.replace('Processing...', 'Submit');
            $submitBtn.html(cleanedHtml);
            $submitBtn.prop('disabled', false);
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
        // Clear all previous validation errors
        $(form).find('.is-invalid').removeClass('is-invalid');
        $(form).find('.invalid-feedback, .validation-message').remove();

        if (typeof errors === 'object') {
            for (const field in errors) {
                const $field = $(form).find(`[name="${field}"]`);
                if ($field.length) {
                    $field.addClass('is-invalid');
                    const messages = Array.isArray(errors[field]) ?
                        errors[field] : [errors[field]];
                    const errorHtml = `<div class="invalid-feedback">${messages.join(', ')}</div>`;
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