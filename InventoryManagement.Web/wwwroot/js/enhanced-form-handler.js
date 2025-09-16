// Enhanced Form Handler for Create/Edit Pages
window.EnhancedFormHandler = (function () {
    'use strict';

    function initializeCreatePage(formSelector, options = {}) {
        const form = document.querySelector(formSelector);
        if (!form) {
            console.error('Form not found:', formSelector);
            return;
        }

        // Set up form submission
        form.addEventListener('submit', async function (e) {
            e.preventDefault();

            const submitButton = form.querySelector('button[type="submit"]');
            const originalButtonHtml = submitButton ? submitButton.innerHTML : '';
            const originalButtonDisabled = submitButton ? submitButton.disabled : false;

            try {
                // Disable submit button and show loading
                if (submitButton) {
                    submitButton.disabled = true;
                    submitButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Creating...';
                }

                // Create form data
                const formData = new FormData(form);

                // Make the request
                const response = await fetch(form.action, {
                    method: form.method || 'POST',
                    body: formData,
                    credentials: 'same-origin'
                });

                // Handle response
                await handleFormResponse(response, options, 'created');

            } catch (error) {
                console.error('Form submission error:', error);
                NotificationSystem.showError('Failed to create item');
            } finally {
                // Reset submit button
                if (submitButton) {
                    submitButton.disabled = originalButtonDisabled;
                    submitButton.innerHTML = originalButtonHtml;
                }
            }
        });

        // Set up image preview if image upload exists
        setupImagePreview(form);
    }

    function initializeEditPage(formSelector, options = {}) {
        const form = document.querySelector(formSelector);
        if (!form) {
            console.error('Form not found:', formSelector);
            return;
        }

        // Set up form submission
        form.addEventListener('submit', async function (e) {
            e.preventDefault();

            const submitButton = form.querySelector('button[type="submit"]');
            const originalButtonHtml = submitButton ? submitButton.innerHTML : '';
            const originalButtonDisabled = submitButton ? submitButton.disabled : false;

            try {
                // Disable submit button and show loading
                if (submitButton) {
                    submitButton.disabled = true;
                    submitButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Updating...';
                }

                // Create form data
                const formData = new FormData(form);

                // Make the request
                const response = await fetch(form.action, {
                    method: form.method || 'POST',
                    body: formData,
                    credentials: 'same-origin'
                });

                // Handle response
                await handleFormResponse(response, options, 'updated');

            } catch (error) {
                console.error('Form submission error:', error);
                NotificationSystem.showError('Failed to update item');
            } finally {
                // Reset submit button
                if (submitButton) {
                    submitButton.disabled = originalButtonDisabled;
                    submitButton.innerHTML = originalButtonHtml;
                }
            }
        });

        // Set up image preview and removal for edit forms
        setupImagePreview(form);
        setupImageRemoval(form);
    }

    async function handleFormResponse(response, options, action) {
        // Get response based on content type
        const contentType = response.headers.get('content-type');
        let result;

        try {
            if (contentType && contentType.includes('application/json')) {
                result = await response.json();
            } else {
                const text = await response.text();
                // Try to parse as JSON, fallback to text
                try {
                    result = JSON.parse(text);
                } catch {
                    result = { message: text };
                }
            }
        } catch (parseError) {
            console.error('Error parsing response:', parseError);
            result = { message: 'Invalid response format' };
        }

        if (response.ok) {
            // Check if it's an approval request
            if (result.isApprovalRequest || result.status === 'PendingApproval') {
                NotificationSystem.showInfo(result.message || `${action.charAt(0).toUpperCase() + action.slice(1)} request submitted for approval`);
            } else {
                // Success - show success message but no sound
                const successMessage = options.successMessage || `Item ${action} successfully!`;
                NotificationSystem.showSuccess(successMessage);
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
            let errorMessage = 'An error occurred';

            if (result.message) {
                errorMessage = result.message;
            } else if (result.error) {
                errorMessage = result.error;
            } else if (result.title) {
                errorMessage = result.title;
            }

            NotificationSystem.showError(errorMessage);

            // Handle validation errors
            if (result.errors && typeof result.errors === 'object') {
                displayValidationErrors(result.errors);
            }

            // Call error callback
            if (options.onError) {
                options.onError(errorMessage, result);
            }
        }
    }

    function setupImagePreview(form) {
        const imageInputs = form.querySelectorAll('input[type="file"][accept*="image"]');

        imageInputs.forEach(input => {
            input.addEventListener('change', function (e) {
                const file = e.target.files[0];
                if (file) {
                    const reader = new FileReader();
                    reader.onload = function (e) {
                        // Look for existing preview or create one
                        let preview = form.querySelector('#imagePreview, .image-preview');
                        if (!preview) {
                            preview = document.createElement('img');
                            preview.className = 'img-thumbnail mt-2 image-preview';
                            preview.style.maxWidth = '200px';
                            preview.style.display = 'block';
                            input.parentNode.appendChild(preview);
                        }
                        preview.src = e.target.result;
                        preview.style.display = 'block';
                    };
                    reader.readAsDataURL(file);
                }
            });
        });
    }

    function setupImageRemoval(form) {
        // Handle current image removal buttons
        const removeButtons = form.querySelectorAll('[onclick*="removeCurrentImage"], .remove-current-image');
        removeButtons.forEach(button => {
            button.addEventListener('click', function (e) {
                e.preventDefault();

                // Hide current image container
                const currentImageContainer = form.querySelector('#currentImageContainer, .current-image-container');
                if (currentImageContainer) {
                    currentImageContainer.style.display = 'none';
                }

                // Set remove flag
                const removeInput = form.querySelector('#RemoveImage, input[name="RemoveImage"]');
                if (removeInput) {
                    removeInput.value = 'true';
                }

                // Show new image upload section
                const newImageContainer = form.querySelector('#newImageContainer, .new-image-container');
                if (newImageContainer) {
                    newImageContainer.style.display = 'block';
                }
            });
        });
    }

    function displayValidationErrors(errors) {
        // Clear existing validation errors
        document.querySelectorAll('.is-invalid').forEach(el => {
            el.classList.remove('is-invalid');
        });
        document.querySelectorAll('.invalid-feedback').forEach(el => {
            el.remove();
        });

        // Display new validation errors
        Object.keys(errors).forEach(field => {
            const input = document.querySelector(`[name="${field}"], #${field}`);
            if (input) {
                input.classList.add('is-invalid');

                const errorDiv = document.createElement('div');
                errorDiv.className = 'invalid-feedback';
                errorDiv.textContent = Array.isArray(errors[field]) ? errors[field][0] : errors[field];

                input.parentNode.appendChild(errorDiv);
            }
        });
    }

    // Public API
    return {
        initializeCreatePage: initializeCreatePage,
        initializeEditPage: initializeEditPage
    };
})();

// Global convenience functions
window.initializeCreateForm = function (formSelector, options = {}) {
    EnhancedFormHandler.initializeCreatePage(formSelector, options);
};

window.initializeEditForm = function (formSelector, options = {}) {
    EnhancedFormHandler.initializeEditPage(formSelector, options);
};