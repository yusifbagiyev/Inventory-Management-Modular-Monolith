function loadPendingApprovalsCount() {
    // Use the configuration to build the correct URL
    const apiUrl = AppConfig.buildApiUrl('approvalrequests?pageNumber=1&pageSize=1');

    $.ajax({
        url: apiUrl,
        type: 'GET',
        headers: {
            'Authorization': 'Bearer ' + $('#jwtToken').val()
        },
        success: function (data) {
            const count = data.totalCount || 0;
            updatePendingApprovalsCount(count);
        },
        error: function (xhr, status, error) {
            console.error('Failed to load pending approvals:', error);
            // In production, don't show technical errors to users
            if (AppConfig.environment === 'production') {
                console.log('Service temporarily unavailable');
            } else {
                console.error('API URL:', apiUrl);
                console.error('Response:', xhr.responseText);
            }
        }
    });
}

function updatePendingApprovalsCount(count) {
    const $badge = $('#pendingApprovalsCount');
    if (count > 0) {
        $badge.text(count).show();
    } else {
        $badge.hide();
    }

    // Also update sidebar badge if exists
    const $sidebarBadge = $('#sidebarPendingCount');
    if ($sidebarBadge.length) {
        if (count > 0) {
            $sidebarBadge.text(count).show();
        } else {
            $sidebarBadge.hide();
        }
    }
}