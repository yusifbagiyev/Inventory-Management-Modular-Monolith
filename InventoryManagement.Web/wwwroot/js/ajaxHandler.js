window.AjaxHandler = (function () {
    'use strict';

    // Configure jQuery to always send AJAX headers
    $.ajaxSetup({
        beforeSend: function (xhr) {
            xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
        },
        error: function (xhr, status, error) {
            // Handle 401 response from Ajax request 
            if (xhr.status === 401 || xhr.status === 403) {
                showToast('Your session has expired. Redirecting to login...', 'warning');
                setTimeout(function () {
                    window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(window.location.pathname);
                }, 2000);
            }
        }
    });

    function handleFormSubmit(formSelector, options) {
        const defaults = {
            beforeSubmit: null,
            onSuccess: null,
            onError: null,
            successMessage: 'Operation completed successfully',
            successRedirect: null,
            redirectDelay: 2000,
            resetOnSuccess: false
        };

        const settings = $.extend({}, defaults, options);

        $(formSelector).on('submit', function (e) {
            e.preventDefault();

            const $form = $(this);
            const form = this;
            const formData = new FormData(form);
            const $submitBtn = $form.find('button[type="submit"]');

            // Clear previous validation errors
            $form.find('.is-invalid').removeClass('is-invalid');
            $form.find('.invalid-feedback').remove();

            // Run custom validation
            if (settings.beforeSubmit && !settings.beforeSubmit(form)) {
                return false;
            }

            // Store original button state
            const originalBtnHtml = $submitBtn.html();
            const originalBtnDisabled = $submitBtn.prop('disabled');

            // Store original button content as data attribute for recovery
            if (!$submitBtn.data('original-html')) {
                $submitBtn.data('original-html', originalBtnHtml);
            }

            $submitBtn.prop('disabled', true)
                .html('<span class="spinner-border spinner-border-sm me-2"></span>Processing...');


            $.ajax({
                url: form.action,
                type: form.method,
                data: formData,
                processData: false,
                contentType: false,
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                success: function (response) {
                    resetButton(); // Always reset button first

                    // Check if we got a redirect page instead of JSON
                    if (typeof response === 'string' && response.includes('<!DOCTYPE')) {
                        if (response.toLowerCase().includes('login')) {
                            showToast('Your session has expired. Please login again.', 'warning');
                            setTimeout(() => window.location.href = '/Account/Login', 2000);
                            return;
                        }
                    }

                    // Handle structured JSON response
                    if (response && typeof response === 'object') {
                        if (response.isSuccess === false) {
                            handleErrorResponse(response, $form, settings);
                        } else if (response.isApprovalRequest) {
                            handleApprovalResponse(response, settings);
                        } else {
                            handleSuccessResponse(response, settings);
                        }
                    } else {
                        // If response is not JSON, assume success
                        handleSuccessResponse({ message: settings.successMessage }, settings);
                    }
                },
                error: function (xhr, status, error) {
                    resetButton(); // Always reset button on error

                    // Handle authentication errors
                    if (xhr.status === 401) {
                        showToast('Your session has expired. Please login again.', 'warning');
                        setTimeout(() => window.location.href = '/Account/Login', 2000);
                        return;
                    }

                    handleAjaxError(xhr, $form, settings);
                }
            });
        });
    }

    function handleErrorResponse(response, $form, settings) {
        const errorMessage = response.message || response.error || 'Operation failed';

        // Handle field-specific validation errors
        if (response.errors) {
            Object.keys(response.errors).forEach(function (field) {
                const $field = $form.find('[name="' + field + '"]');
                if ($field.length) {
                    $field.addClass('is-invalid');
                    const messages = Array.isArray(response.errors[field])
                        ? response.errors[field].join(', ')
                        : response.errors[field];
                    $field.after('<div class="invalid-feedback">' + messages + '</div>');
                }
            });
        }

        // Show general error message
        showToast(errorMessage, 'error');

        // Special handling for specific error types
        if (errorMessage.toLowerCase().includes('inventory code')) {
            const $inventoryCode = $form.find('#InventoryCode');
            if ($inventoryCode.length && !$inventoryCode.hasClass('is-invalid')) {
                $inventoryCode.addClass('is-invalid')
                    .after('<div class="invalid-feedback">' + errorMessage + '</div>');
            }
        }

        // Ensure button is reset (it should already be reset, but just in case)
        const $submitBtn = $form.find('button[type="submit"]');
        if ($submitBtn.data('original-html')) {
            $submitBtn.prop('disabled', false).html($submitBtn.data('original-html'));
        }

        if (settings.onError) {
            settings.onError(errorMessage, response);
        }
    }

    function handleApprovalResponse(response, settings) {
        const message = response.message || 'Request submitted for approval';
        showToast(message, 'info');

        if (settings.successRedirect) {
            setTimeout(() => window.location.href = settings.successRedirect, settings.redirectDelay);
        }
    }

    function handleSuccessResponse(response, settings) {
        const message = response.message || settings.successMessage;
        showToast(message, 'success');

        if (settings.onSuccess) {
            settings.onSuccess(response);
        }

        if (settings.successRedirect) {
            setTimeout(() => window.location.href = settings.successRedirect, settings.redirectDelay);
        }
    }

    function handleAjaxError(xhr, $form, settings) {
        let errorMessage = 'An error occurred';

        try {
            if (xhr.responseJSON) {
                errorMessage = xhr.responseJSON.message ||
                    xhr.responseJSON.error ||
                    xhr.responseJSON.title ||
                    errorMessage;

                // Handle validation errors from API
                if (xhr.responseJSON.errors) {
                    const errors = xhr.responseJSON.errors;
                    if (typeof errors === 'object') {
                        Object.keys(errors).forEach(function (field) {
                            const $field = $form.find('[name="' + field + '"]');
                            if ($field.length) {
                                $field.addClass('is-invalid');
                                const messages = Array.isArray(errors[field])
                                    ? errors[field].join(', ')
                                    : errors[field];
                                $field.after('<div class="invalid-feedback">' + messages + '</div>');
                            }
                        });
                    }
                }
            } else if (xhr.responseText) {
                // Try to parse as JSON
                try {
                    const parsed = JSON.parse(xhr.responseText);
                    errorMessage = parsed.message || parsed.error || errorMessage;
                } catch (e) {
                    // Check if it's an HTML page (likely login redirect)
                    if (xhr.responseText.includes('<!DOCTYPE') || xhr.responseText.includes('<html')) {
                        if (xhr.responseText.toLowerCase().includes('login')) {
                            errorMessage = 'Your session has expired. Please login again.';
                            setTimeout(() => window.location.href = '/Account/Login', 2000);
                        }
                    } else if (xhr.responseText.length < 500) {
                        errorMessage = xhr.responseText;
                    }
                }
            }

            // Use status code specific messages
            switch (xhr.status) {
                case 400:
                    errorMessage = errorMessage || 'Invalid request. Please check your input.';
                    break;
                case 401:
                    errorMessage = 'You are not authorized. Please login again.';
                    setTimeout(() => window.location.href = '/Account/Login', 2000);
                    break;
                case 403:
                    errorMessage = 'You do not have permission to perform this action.';
                    break;
                case 404:
                    errorMessage = 'The requested resource was not found.';
                    break;
                case 409:
                    errorMessage = errorMessage || 'This operation conflicts with existing data.';
                    break;
                case 500:
                    errorMessage = 'A server error occurred. Please try again later.';
                    break;
            }
        } catch (e) {
            console.error('Error parsing error response:', e);
        }

        showToast(errorMessage, 'error');

        if (settings.onError) {
            settings.onError(errorMessage, xhr);
        }
    }

    // Public API
    return {
        handleForm: handleFormSubmit,
        handleError: handleAjaxError
    };
})();