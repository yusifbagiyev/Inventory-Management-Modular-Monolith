let isLoadingApprovals = false;
async function loadPendingApprovalsCount() {
    if (isLoadingApprovals) {
        console.log('Already loading approvals, skipping duplicate call');
        return;
    }

    isLoadingApprovals = true;
    const apiUrl = AppConfig.buildApiUrl('approvalrequests?pageNumber=1&pageSize=1');

    setTimeout(async () => {
        try {
            // SECURITY: Get token from secure provider instead of DOM
            const token = await SecureTokenProvider.getToken();

            $.ajax({
                url: apiUrl,
                type: 'GET',
                headers: {
                    'Authorization': `Bearer ${token}` // Use token from secure provider
                },
                timeout: 10000,
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

                    if (xhr.status === 401) {
                        console.warn('Authentication expired, user needs to login');
                    } else if (xhr.status === 403) {
                        console.warn('User does not have permission to view approvals');
                    } else if (xhr.status === 0 || status === 'timeout') {
                        console.error('Network error or timeout occurred');
                    } else {
                        if (AppConfig.environment === 'development') {
                            console.error('API URL:', apiUrl);
                            console.error('Response:', xhr.responseText);
                        }
                    }
                },
                complete: function () {
                    isLoadingApprovals = false;
                }
            });
        } catch (error) {
            console.error('Failed to get token:', error);
            isLoadingApprovals = false;

            // If token fetch fails, user likely needs to re-authenticate
            if (error.message.includes('Unauthorized')) {
                window.location.href = '/Account/Login';
            }
        }
    }, 250);
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