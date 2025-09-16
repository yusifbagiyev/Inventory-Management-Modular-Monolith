// Global request handler to prevent duplicate submissions
(function () {
    'use strict';

    // Track active requests
    const activeRequests = new Map();

    // Debounce function for preventing rapid clicks
    function debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    // Initialize protection on all forms and action buttons
    function initializeRequestProtection() {
        // Protect all forms
        document.querySelectorAll('form').forEach(form => {
            form.addEventListener('submit', function (e) {
                const formId = this.id || this.action;

                // Check if request is already in progress
                if (activeRequests.has(formId)) {
                    e.preventDefault();
                    console.log('Duplicate form submission prevented');
                    return false;
                }

                // Mark as active
                activeRequests.set(formId, true);

                // Find and disable submit button
                const submitBtn = this.querySelector('button[type="submit"], input[type="submit"]');
                if (submitBtn) {
                    submitBtn.disabled = true;
                    const originalText = submitBtn.innerHTML;
                    submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Processing...';

                    // Re-enable after 3 seconds (failsafe)
                    setTimeout(() => {
                        activeRequests.delete(formId);
                        submitBtn.disabled = false;
                        submitBtn.innerHTML = originalText;
                    }, 3000);
                }
            });
        });

        // Protect action buttons with data-action attribute
        document.querySelectorAll('button[data-action], a[data-action]').forEach(button => {
            const originalClick = button.onclick;
            button.onclick = debounce(function (e) {
                const actionId = this.dataset.action;

                if (activeRequests.has(actionId)) {
                    e.preventDefault();
                    console.log('Duplicate action prevented');
                    return false;
                }

                activeRequests.set(actionId, true);
                this.disabled = true;

                // Call original handler if exists
                if (originalClick) {
                    originalClick.call(this, e);
                }

                // Re-enable after 2 seconds
                setTimeout(() => {
                    activeRequests.delete(actionId);
                    this.disabled = false;
                }, 2000);
            }, 300); // 300ms debounce
        });
    }

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeRequestProtection);
    } else {
        initializeRequestProtection();
    }

    // Re-initialize after AJAX content loads
    document.addEventListener('ajaxComplete', initializeRequestProtection);
})();