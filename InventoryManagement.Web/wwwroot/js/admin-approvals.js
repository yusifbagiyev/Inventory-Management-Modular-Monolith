// Pending-approvals badge on the rail, which the page renders and this reloads whenever an approval request changes

let isLoadingApprovals = false;

function loadPendingApprovalsCount() {
    if (isLoadingApprovals) return;
    isLoadingApprovals = true;
    $.ajax({
        url: AppConfig.buildApiUrl('approvalrequests/pending-count'),
        type: 'GET',
        timeout: 10000,
        success: data => updatePendingApprovalsCount(data.count || 0),
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

// A burst of approvals loads the count only once
function debouncedLoadPendingApprovalsCount() {
    clearTimeout(window.approvalsLoadTimeout);
    window.approvalsLoadTimeout = setTimeout(loadPendingApprovalsCount, 500);
}

// Any decided, cancelled or new request changes the pending count
window.addEventListener('live:changed', function (e) {
    const changes = (e.detail && e.detail.changes) || [];
    if (changes.some(c => c.entity === 'approval')) debouncedLoadPendingApprovalsCount();
});

