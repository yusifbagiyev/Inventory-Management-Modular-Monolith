let isLoadingApprovals = false;
function loadPendingApprovalsCount() {
    // Prevent multiple simultaneous calls
    if (isLoadingApprovals) {
        console.log('Already loading approvals, skipping duplicate call');
        return;
    }

    isLoadingApprovals = true;
    // Use the configuration to build the correct URL
    const apiUrl = AppConfig.buildApiUrl('approvalrequests?pageNumber=1&pageSize=1');

    // Add a small delay to prevent rapid successive calls
    setTimeout(() => {
        $.ajax({
            url: apiUrl,
            type: 'GET',
            headers: {
                'Authorization': 'Bearer ' + $('#jwtToken').val()
            },
            timeout: 10000, // 10 second timeout
            success: function (data) {
                const count = data.totalCount || 0;
                updatePendingApprovalsCount(count);
                console.log('✅ Loaded approval count:', count);
            },
            error: function (xhr, status, error) {
                console.error('❌ Failed to load pending approvals:', {
                    status: xhr.status,
                    error: error,
                    responseText: xhr.responseText
                });

                // Handle different error scenarios gracefully
                if (xhr.status === 401) {
                    console.warn('Authentication expired, user needs to login');
                    // Don't show error toast for auth issues, just log it
                } else if (xhr.status === 403) {
                    console.warn('User does not have permission to view approvals');
                } else if (xhr.status === 0 || status === 'timeout') {
                    console.error('Network error or timeout occurred');
                    // Could show a subtle indicator that data is stale
                } else {
                    // Only log technical errors in development
                    if (AppConfig.environment === 'development') {
                        console.error('API URL:', apiUrl);
                        console.error('Response:', xhr.responseText);
                    }
                }

                // Keep the last known count visible instead of hiding it
                // This provides better user experience than showing zero
            },
            complete: function () {
                // Always reset the loading flag, even if the request failed
                isLoadingApprovals = false;
            }
        });
    }, 250); // 250ms delay to debounce rapid calls
}

function updatePendingApprovalsCount(count) {
    // Ensure count is a valid number
    count = parseInt(count) || 0;

    // Update the main navigation badge
    const $headerBadge = $('#pendingApprovalsCount');
    if ($headerBadge.length) {
        if (count > 0) {
            $headerBadge.text(count > 99 ? '99+' : count).show();
        } else {
            $headerBadge.hide();
        }
    }

    // Update the sidebar badge
    const $sidebarBadge = $('#sidebarPendingCount');
    if ($sidebarBadge.length) {
        if (count > 0) {
            $sidebarBadge.text(count > 99 ? '99+' : count).show();
        } else {
            $sidebarBadge.hide();
        }
    }

    // Store the count for reference
    window.currentApprovalsCount = count;

    // Trigger a custom event that other parts of the app can listen to
    $(document).trigger('approvals:count-updated', [count]);
}

// Debounced version for frequent calls
function debouncedLoadPendingApprovalsCount() {
    // Clear any existing timeout
    if (window.approvalsLoadTimeout) {
        clearTimeout(window.approvalsLoadTimeout);
    }

    // Set a new timeout
    window.approvalsLoadTimeout = setTimeout(() => {
        loadPendingApprovalsCount();
    }, 500); // Wait 500ms before actually loading
}

// Export the functions for use by other modules
window.loadPendingApprovalsCount = loadPendingApprovalsCount;
window.debouncedLoadPendingApprovalsCount = debouncedLoadPendingApprovalsCount;
window.updatePendingApprovalsCount = updatePendingApprovalsCount;