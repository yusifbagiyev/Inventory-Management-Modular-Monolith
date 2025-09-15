// admin-approvals.js - Admin-specific approval functionality
window.AdminApprovals = (function () {
    'use strict';

    let checkInterval = null;
    const CHECK_FREQUENCY = 30000; // Check every 30 seconds

    function initialize() {
        // Only initialize if user is admin
        if (window.isAdmin !== 'true') {
            return;
        }

        // Load initial count
        loadPendingApprovalsCount();

        // Set up periodic checking
        checkInterval = setInterval(loadPendingApprovalsCount, CHECK_FREQUENCY);

        // Listen for approval-related events
        document.addEventListener('approvalRequestCreated', handleNewApprovalRequest);
        document.addEventListener('approvalRequestProcessed', handleApprovalProcessed);
    }

    function loadPendingApprovalsCount() {
        const token = AppConfig.getToken();
        if (!token) {
            console.warn('No token available for loading approvals');
            return;
        }

        const url = AppConfig.buildApiUrl('approvalrequests?pageNumber=1&pageSize=100&status=Pending');

        fetch(url, {
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        })
            .then(response => {
                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }
                return response.json();
            })
            .then(data => {
                const pendingCount = data.totalCount || 0;
                updatePendingApprovalsCount(pendingCount);

                // If we have new pending approvals, notify
                const lastCount = parseInt(sessionStorage.getItem('lastApprovalCount') || '0');
                if (pendingCount > lastCount && lastCount > 0) {
                    notifyNewApprovals(pendingCount - lastCount);
                }
                sessionStorage.setItem('lastApprovalCount', pendingCount.toString());
            })
            .catch(error => {
                console.error('Failed to load pending approvals:', error);
            });
    }

    function updatePendingApprovalsCount(count) {
        // Update all approval count badges
        const badges = document.querySelectorAll('#pendingApprovalsCount, #sidebarPendingCount, .approval-count-badge');
        badges.forEach(badge => {
            if (count > 0) {
                badge.textContent = count > 99 ? '99+' : count;
                badge.style.display = 'inline-block';
                badge.classList.add('pulse-animation');
            } else {
                badge.style.display = 'none';
                badge.classList.remove('pulse-animation');
            }
        });
    }

    function handleNewApprovalRequest(event) {
        const detail = event.detail;

        // Show notification
        showApprovalNotification({
            title: 'New Approval Request',
            message: `${detail.requestedBy} has submitted a ${detail.type} request`,
            type: 'warning'
        });

        // Reload count
        loadPendingApprovalsCount();
    }

    function handleApprovalProcessed(event) {
        // Reload count when an approval is processed
        loadPendingApprovalsCount();
    }

    function notifyNewApprovals(count) {
        const message = count === 1
            ? 'You have 1 new approval request'
            : `You have ${count} new approval requests`;

        showApprovalNotification({
            title: 'Pending Approvals',
            message: message,
            type: 'warning',
            actionUrl: '/Approvals'
        });
    }

    function showApprovalNotification(options) {
        // Create enhanced notification with action button
        const notificationHtml = `
            <div class="d-flex align-items-start">
                <i class="fas fa-exclamation-circle me-2 mt-1"></i>
                <div class="flex-grow-1">
                    <strong>${options.title}</strong><br>
                    <small>${options.message}</small>
                    ${options.actionUrl ? `<br><a href="${options.actionUrl}" class="btn btn-sm btn-primary mt-2">View Now</a>` : ''}
                </div>
            </div>
        `;

        window.showToast(notificationHtml, options.type || 'warning', 8000);

        // Also play sound for admin notifications
        playNotificationSound();
    }

    function playNotificationSound() {
        try {
            const audio = new Audio('/sounds/notify.mp3');
            audio.volume = 0.5;
            audio.play().catch(() => {
                // Fallback: Create a beep sound using Web Audio API
                const audioContext = new (window.AudioContext || window.webkitAudioContext)();
                const oscillator = audioContext.createOscillator();
                const gainNode = audioContext.createGain();

                oscillator.connect(gainNode);
                gainNode.connect(audioContext.destination);

                oscillator.frequency.value = 800;
                oscillator.type = 'sine';
                gainNode.gain.value = 0.3;

                oscillator.start();
                oscillator.stop(audioContext.currentTime + 0.2);
            });
        } catch (error) {
            console.log('Could not play notification sound');
        }
    }

    function destroy() {
        if (checkInterval) {
            clearInterval(checkInterval);
            checkInterval = null;
        }

        document.removeEventListener('approvalRequestCreated', handleNewApprovalRequest);
        document.removeEventListener('approvalRequestProcessed', handleApprovalProcessed);
    }

    // Public API
    return {
        init: initialize,
        destroy: destroy,
        checkApprovals: loadPendingApprovalsCount
    };
})();