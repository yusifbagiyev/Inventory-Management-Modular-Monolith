// Reusable AJAX form handler with comprehensive error handling
function initializeAjaxForm(formSelector, options = {}) {
    const defaults = {
        successMessage: 'Operation completed successfully!',
        successRedirect: null,
        redirectDelay: 2000,
        resetOnSuccess: false,
        beforeSubmit: null,
        onSuccess: null,
        onError: null,
        validateBeforeSubmit: true
    };

    const settings = { ...defaults, ...options };

    $(formSelector).on('submit', function (e) {
        e.preventDefault();

        const form = this;
        const $form = $(form);
        const $submitBtn = $form.find('button[type="submit"]');

        // Run custom validation if provided
        if (settings.validateBeforeSubmit && settings.beforeSubmit) {
            if (!settings.beforeSubmit(form)) {
                return false;
            }
        }

        // Clear previous errors
        $form.find('.is-invalid').removeClass('is-invalid');
        $form.find('.invalid-feedback').remove();

        // Store original button state
        const originalBtnHtml = $submitBtn.html();
        $submitBtn.prop('disabled', true)
            .html('<span class="spinner-border spinner-border-sm me-2"></span>Processing...');

        // Prepare form data
        const formData = new FormData(form);

        $.ajax({
            url: form.action,
            type: form.method,
            data: formData,
            processData: false,
            contentType: false,
            success: function (response) {
                // Reset button state
                $submitBtn.prop('disabled', false).html(originalBtnHtml);

                // Check if response is a string (might be HTML)
                if (typeof response === 'string') {
                    // Check if it's JSON string
                    try {
                        response = JSON.parse(response);
                    } catch (e) {
                        // Not JSON, might be HTML redirect
                        if (response.includes('<!DOCTYPE')) {
                            window.location.reload();
                            return;
                        }
                    }
                }

                // Now handle the response properly
                if (response && typeof response === 'object') {
                    // Handle approval requests
                    if (response.isApprovalRequest === true) {
                        showToast(
                            response.message || 'Request submitted for approval',
                            'info'
                        );

                        if (settings.successRedirect) {
                            setTimeout(() => {
                                window.location.href = settings.successRedirect;
                            }, settings.redirectDelay);
                        }
                        return;
                    }

                    // Handle success (including when isSuccess is true or undefined for backward compatibility)
                    if (response.isSuccess !== false) {
                        showToast(response.message || settings.successMessage, 'success');

                        if (settings.onSuccess) {
                            settings.onSuccess(response, form);
                        }

                        if (settings.resetOnSuccess) {
                            form.reset();
                        }

                        if (settings.successRedirect) {
                            setTimeout(() => {
                                window.location.href = settings.successRedirect;
                            }, settings.redirectDelay);
                        }
                    } else {
                        // Handle failure
                        const errorMsg = response.message || response.error || 'Operation failed';
                        showToast(errorMsg, 'error');

                        if (settings.onError) {
                            settings.onError(errorMsg, response);
                        }
                    }
                } else {
                    // Fallback for non-standard responses
                    showToast(settings.successMessage, 'success');
                    if (settings.successRedirect) {
                        setTimeout(() => {
                            window.location.href = settings.successRedirect;
                        }, settings.redirectDelay);
                    }
                }
            },
            error: function (xhr, status, error) {
                // Reset button state
                $submitBtn.prop('disabled', false).html(originalBtnHtml);

                // Parse error message
                const errorMessage = ErrorHandler.parseErrorMessage(xhr);
                showToast(errorMessage, 'error');

                // Handle validation errors
                if (xhr.status === 400 && xhr.responseJSON && xhr.responseJSON.errors) {
                    Object.entries(xhr.responseJSON.errors).forEach(([field, messages]) => {
                        const $field = $form.find(`[name="${field}"]`);
                        if ($field.length) {
                            $field.addClass('is-invalid');
                            const errorMsg = Array.isArray(messages) ? messages.join(', ') : messages;
                            $field.after(`<div class="invalid-feedback">${errorMsg}</div>`);
                        }
                    });
                }

                if (settings.onError) {
                    settings.onError(errorMessage, xhr);
                }
            }
        });
    });
}