// InventoryManagement.Web/wwwroot/js/admin-approvals.js
//
// The pending-approvals count on the rail's Approvals link (#sidebarPendingCount), for whoever
// may see the approvals page. Loaded on start, and again whenever a request is created,
// decided or cancelled (live updates) or a new-request notification arrives.

let isLoadingApprovals = false;

function loadPendingApprovalsCount() {
    if (isLoadingApprovals) return;
    isLoadingApprovals = true;
    $.ajax({
        url: AppConfig.buildApiUrl('approvalrequests?pageNumber=1&pageSize=1'),
        type: 'GET',
        timeout: 10000,
        success: data => updatePendingApprovalsCount(data.totalCount || 0),
        error: xhr => console.warn('Pending approvals count unavailable', xhr.status),
        complete: () => { isLoadingApprovals = false; }
    });
}

function updatePendingApprovalsCount(count) {
    count = parseInt(count) || 0;
    const $badge = $('#sidebarPendingCount');
    if (count > 0) $badge.text(count > 99 ? '99+' : count).show();
    else $badge.hide();
}

// Several changes in a row (a batch of approvals) load the count once.
function debouncedLoadPendingApprovalsCount() {
    clearTimeout(window.approvalsLoadTimeout);
    window.approvalsLoadTimeout = setTimeout(loadPendingApprovalsCount, 500);
}

window.loadPendingApprovalsCount = loadPendingApprovalsCount;
window.debouncedLoadPendingApprovalsCount = debouncedLoadPendingApprovalsCount;

// Any decided, cancelled or new request changes the pending count.
window.addEventListener('live:changed', function (e) {
    const changes = (e.detail && e.detail.changes) || [];
    if (changes.some(c => c.entity === 'approval')) debouncedLoadPendingApprovalsCount();
});

$(loadPendingApprovalsCount);
