const ErrorHandler = {
    // Parse error message from various response formats
    parseErrorMessage: function (xhr, defaultMessage = 'An error occured') {
        try {
            // Check if response has JSON content
            const contentType = xhr.getResponseHeader('content-type');

            if (contentType && contentType.includes('application/json')) {
                let response;

                // Try to parse the response
                if (typeof xhr.responseJSON === 'object') {
                    response = xhr.responseJSON;
                } else if (xhr.responseText) {
                    try {
                        response = JSON.parse(xhr.responseText);
                    } catch (e) {
                        // If parsing fails, check if it's too long
                        if (xhr.responseText.length < 500) {
                            return xhr.responseText;
                        }
                        return defaultMessage;
                    }

                    // Handle ASP.NET Core problem details format
                    if (response && response.type && response.title) {
                        // This is a problem details response
                        if (response.status === 404) {
                            return 'The requested resource was not found';
                        }
                        return response.title || response.detail || defaultMessage;
                    }

                    // Check various error message formats
                    if (response) {
                        // For simple error responses
                        if (typeof response === 'string') {
                            return response;
                        }

                        // For complex error objects
                        if (response.error) {
                            if (typeof response.error === 'object' && response.error.message) {
                                return response.error.message;
                            }
                            return response.error;
                        }
                        if (response.message) return response.message;
                        if (response.Message) return response.Message;
                        if (response.errors) {
                            // Handle validation errors
                            if (typeof response.errors === 'object' && !Array.isArray(response.errors)) {
                                const errorMessages = [];
                                for (const [key, value] of Object.entries(response.errors)) {
                                    if (Array.isArray(value)) {
                                        errorMessages.push(...value);
                                    } else {
                                        errorMessages.push(value);
                                    }
                                }
                                return errorMessages.join(', ');
                            }
                            return response.errors;
                        }
                        if (response.detail) return response.detail;
                        if (response.Detail) return response.Detail;
                    }
                }

                // Check for plain text response
                if (xhr.responseText &&
                    xhr.responseText.length < 500 &&
                    !xhr.responseText.includes('<!DOCTYPE') &&
                    !xhr.responseText.includes('<html')) {
                    return xhr.responseText;
                }

                // Status-based messages
                switch (xhr.status) {
                    case 400:
                        return 'Invalid request. Please check your input.';
                    case 401:
                        return 'You are not authorized. Please login again.';
                    case 403:
                        return 'You do not have permission to perform this action.';
                    case 404:
                        return 'The requested item was not found.';
                    case 409:
                        return 'This operation conflicts with existing data.';
                    case 500:
                        return 'Server error occurred. Please try again later.';
                    case 503:
                        return 'Service temporarily unavailable. Please try again later.';
                    default:
                        return defaultMessage || `Request failed with status ${xhr.status}`;
                }
            }
        } catch (e) {
            console.error('Error parsing response:', e);
            return defaultMessage;
        }
    },

    // Handle AJAX errors uniformly
    handleAjaxError: function (xhr, status, error, context = {}) {
        const errorMessage = this.parseErrorMessage(xhr, context.defaultMessage);

        // Show error toast
        showToast(errorMessage, 'error', 7000);

        // Reset form state if form element provided
        if (context.form) {
            resetFormState(context.form);
        }

        // Log detailed error for debugging
        console.error('AJAX Error Details:', {
            status: xhr.status,
            statusText: xhr.statusText,
            responseText: xhr.responseText,
            error: error,
            context: context
        });

        // Call custom error handler if provided
        if (context.onError) {
            context.onError(errorMessage, xhr);
        }
    },

    // Attach to jQuery AJAX if available
    setupGlobalHandlers: function () {
        if (typeof $ !== 'undefined') {
            $(document).ajaxError(function (event, xhr, settings, error) {
                // Don't show error for aborted requests
                if (xhr.statusText === 'abort') return;

                // Check if specific error handling is disabled
                if (settings.skipGlobalError) return;

                // Use global error handler as fallback
                if (!settings.errorHandled) {
                    ErrorHandler.handleAjaxError(xhr, xhr.statusText, error, {
                        defaultMessage: 'Request failed. Please try again.'
                    });
                }
            });
        }
    }
};

// Initialize global handlers when document is ready
document.addEventListener('DOMContentLoaded', function () {
    ErrorHandler.setupGlobalHandlers();
});