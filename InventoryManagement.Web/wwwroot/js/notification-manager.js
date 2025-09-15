window.NotificationManager = (function () {
    'use strict';

    let connection = null;
    let isInitialized = false;
    let reconnectAttempts = 0;
    let notificationQueue = [];
    const config = window.AppConfig;

    function initialize() {
        if (isInitialized) {
            console.log('NotificationManager already initialized');
            return;
        }

        const token = config.getToken();
        if (!token) {
            console.warn('No authentication token available');
            setTimeout(initialize, 5000); // Retry after 5 seconds
            return;
        }

        setupSignalRConnection(token);
        setupUI();
        loadInitialData();
        setupBrowserNotifications();
        isInitialized = true;
    }

    function setupBrowserNotifications() {
        // Request permission for browser notifications
        if ('Notification' in window && Notification.permission === 'default') {
            Notification.requestPermission().then(permission => {
                console.log('Notification permission:', permission);
            });
        }
    }
    // Setup SignalR connection
    function setupSignalRConnection(token) {
        try {
            connection = new signalR.HubConnectionBuilder()
                .withUrl(config.signalR.hubUrl, {
                    accessTokenFactory: () => token,
                    transport: signalR.HttpTransportType.WebSockets |
                        signalR.HttpTransportType.ServerSentEvents |
                        signalR.HttpTransportType.LongPolling
                })
                .withAutomaticReconnect({
                    nextRetryDelayInMilliseconds: retryContext => {
                        if (reconnectAttempts >= config.signalR.maxReconnectAttempts) {
                            return null;
                        }
                        reconnectAttempts++;
                        return Math.min(1000 * Math.pow(2, reconnectAttempts), 30000);
                    }
                })
                .configureLogging(signalR.LogLevel.Information)
                .build();

            setupConnectionHandlers();
            startConnection();
        } catch (error) {
            console.error('Failed to setup SignalR connection:', error);
            setTimeout(() => initialize(), 5000);
        }
    }

    // Setup connection event handlers
    function setupConnectionHandlers() {
        // Handle the main notification event
        connection.on("ReceiveNotification", function (notification) {
            console.log('Notification received:', notification);
            handleIncomingNotification(notification);
        });

        // Handle connection established event (replacing the problematic "Connected" event)
        connection.on("ConnectionEstablished", function (data) {
            console.log('Connection established:', data);
            updateConnectionStatus('connected');

            // Process any queued notifications
            processNotificationQueue();
        });

        connection.onreconnecting(error => {
            console.warn('Reconnecting to notification hub...', error);
            updateConnectionStatus('reconnecting');
        });

        connection.onreconnected(connectionId => {
            console.log('Reconnected to notification hub');
            reconnectAttempts = 0;
            updateConnectionStatus('connected');
            joinUserGroup();
            processNotificationQueue();
        });

        connection.onclose(error => {
            console.error('Connection closed:', error);
            updateConnectionStatus('disconnected');
            isInitialized = false;

            // Attempt to reconnect
            setTimeout(() => {
                if (reconnectAttempts < config.signalR.maxReconnectAttempts) {
                    initialize();
                }
            }, config.signalR.reconnectInterval);
        });
    }

    // Start the SignalR connection
    function startConnection() {
        connection.start()
            .then(() => {
                console.log('Connected to notification hub successfully');
                reconnectAttempts = 0;
                updateConnectionStatus('connected');
                joinUserGroup();
                processNotificationQueue();
            })
            .catch(err => {
                console.error('Failed to connect:', err);
                updateConnectionStatus('disconnected');

                // Retry connection
                setTimeout(() => {
                    if (reconnectAttempts < config.signalR.maxReconnectAttempts) {
                        reconnectAttempts++;
                        startConnection();
                    }
                }, 5000);
            });
    }

    // Join user-specific notification group
    function joinUserGroup() {
        if (connection.state === signalR.HubConnectionState.Connected) {
            connection.invoke("JoinUserGroup")
                .then(() => console.log('Joined user notification group'))
                .catch(err => console.error('Failed to join user group:', err));
        }
    }

    // Handle incoming notifications
    function handleIncomingNotification(notification) {
        // If connection is not ready, queue the notification
        if (connection.state !== signalR.HubConnectionState.Connected) {
            notificationQueue.push(notification);
            return;
        }

        // Process notification immediately
        processNotification(notification);
    }

    // Display notification as toast
    function processNotification(notification) {
        // Display browser notification
        showBrowserNotification(notification);

        // Display toast notification
        displayToast(notification);

        // Update badge
        incrementBadge();

        // Add to dropdown
        addToDropdown(notification);

        // Play sound
        playNotificationSound();

        // Trigger custom events for specific notification types
        if (notification.type === 'ApprovalRequest' && window.isAdmin === 'true') {
            document.dispatchEvent(new CustomEvent('approvalRequestCreated', {
                detail: notification
            }));
        }
    }
    function processNotificationQueue() {
        while (notificationQueue.length > 0) {
            const notification = notificationQueue.shift();
            processNotification(notification);
        }
    }
    function showBrowserNotification(notification) {
        if ('Notification' in window && Notification.permission === 'granted') {
            try {
                const browserNotification = new Notification(notification.title || 'New Notification', {
                    body: notification.message,
                    icon: '/icon-192x192.png',
                    badge: '/icon-72x72.png',
                    tag: 'notification-' + (notification.id || Date.now()),
                    requireInteraction: false,
                    silent: false,
                    vibrate: [200, 100, 200]
                });

                // Auto-close after 10 seconds
                setTimeout(() => browserNotification.close(), 10000);

                // Handle click
                browserNotification.onclick = function (event) {
                    event.preventDefault();
                    window.focus();
                    this.close();

                    // Navigate to relevant page based on notification type
                    if (notification.type === 'ApprovalRequest') {
                        window.location.href = '/Approvals';
                    } else if (notification.type === 'ProductUpdate') {
                        window.location.href = '/Products';
                    } else if (notification.type === 'RouteUpdate') {
                        window.location.href = '/Routes';
                    }
                };
            } catch (error) {
                console.error('Error showing browser notification:', error);
            }
        }
    }
    function displayToast(notification) {
        const type = getNotificationType(notification.type);
        const icon = getNotificationIcon(notification.type);

        const message = `
            <div class="d-flex align-items-start">
                <i class="${icon} me-2 mt-1"></i>
                <div>
                    <strong>${escapeHtml(notification.title || 'Notification')}</strong><br>
                    <small>${escapeHtml(notification.message || '')}</small>
                </div>
            </div>
        `;

        window.showToast(message, type, 7000);
    }
    // Setup UI event handlers
    function setupUI() {
        // Mark all as read button
        document.getElementById('markAllAsRead')?.addEventListener('click', function (e) {
            e.preventDefault();
            markAllAsRead();
        });

        // Request notification permission button (if needed)
        const permissionBtn = document.getElementById('enableNotifications');
        if (permissionBtn) {
            permissionBtn.addEventListener('click', function () {
                Notification.requestPermission().then(permission => {
                    if (permission === 'granted') {
                        showToast('Browser notifications enabled!', 'success');
                    }
                });
            });
        }
    }

    // Load initial notification data
    function loadInitialData() {
        loadUnreadCount();
        loadRecentNotifications();

        if (window.isAdmin === 'true') {
            loadPendingApprovals();
        }
    }

    // Load unread notification count
    function loadUnreadCount() {
        fetch('/Notifications/GetUnreadCount')
            .then(response => response.json())
            .then(count => updateBadge(count))
            .catch(error => console.error('Failed to load unread count:', error));
    }

    // Load recent notifications
    function loadRecentNotifications() {
        const list = document.getElementById('notificationList');
        if (!list) return;

        list.innerHTML = '<div class="text-center py-3"><div class="spinner-border spinner-border-sm"></div></div>';

        fetch('/Notifications/GetRecentNotifications')
            .then(response => response.json())
            .then(notifications => renderNotificationList(notifications))
            .catch(error => {
                console.error('Failed to load notifications:', error);
                list.innerHTML = '<div class="text-center py-3 text-muted">Failed to load notifications</div>';
            });
    }

    // Load pending approvals count (admin only)
    function loadPendingApprovals() {
        const token = config.getToken();
        if (!token) return;

        fetch(config.buildApiUrl('approvalrequests?pageNumber=1&pageSize=1'), {
            headers: { 'Authorization': `Bearer ${token}` }
        })
            .then(response => response.json())
            .then(data => {
                const count = data.totalCount || 0;
                updatePendingApprovalsCount(count);
            })
            .catch(error => console.error('Failed to load approvals:', error));
    }

    // Render notification list in dropdown
    function renderNotificationList(notifications) {
        const list = document.getElementById('notificationList');
        if (!list) return;

        if (!notifications || notifications.length === 0) {
            list.innerHTML = `
                <div class="notification-empty">
                    <i class="fas fa-bell-slash"></i>
                    <p class="mb-0">No new notifications</p>
                </div>
            `;
            return;
        }

        list.innerHTML = notifications.map(n => createNotificationItem(n)).join('');
    }

    // Create notification item HTML
    function createNotificationItem(notification) {
        const timeAgo = formatTimeAgo(notification.createdAt);
        const unreadClass = notification.isRead ? '' : 'unread';
        const icon = getNotificationIcon(notification.type);
        const iconColor = getNotificationIconColor(notification.type);

        return `
            <div class="notification-item ${unreadClass}" data-id="${notification.id}" onclick="NotificationManager.markAsRead(${notification.id})">
                <div class="d-flex align-items-start">
                    <div class="notification-icon ${iconColor} me-3">
                        <i class="${icon} text-white"></i>
                    </div>
                    <div class="notification-content">
                        <h6>${escapeHtml(notification.title)}</h6>
                        <p>${escapeHtml(notification.message)}</p>
                        <div class="notification-time">
                            <i class="fas fa-clock me-1"></i>${timeAgo}
                            ${!notification.isRead ? '<span class="notification-dot ms-2"></span>' : ''}
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    // Add notification to dropdown
    function addToDropdown(notification) {
        const list = document.getElementById('notificationList');
        if (!list) return;

        // Remove empty state if exists
        const emptyState = list.querySelector('.notification-empty');
        if (emptyState) {
            emptyState.remove();
        }

        // Add new notification at top
        const html = createNotificationItem(notification);
        list.insertAdjacentHTML('afterbegin', html);

        // Keep only 5 most recent
        const items = list.querySelectorAll('.notification-item');
        if (items.length > 5) {
            items[items.length - 1].remove();
        }
    }

    // Mark notification as read
    function markAsRead(notificationId) {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (!token) return;

        fetch('/Notifications/MarkAsRead', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(notificationId)
        })
            .then(() => {
                document.querySelector(`.notification-item[data-id="${notificationId}"]`)?.classList.remove('unread');
                decrementBadge();
            })
            .catch(error => console.error('Failed to mark as read:', error));
    }

    // Mark all notifications as read
    function markAllAsRead() {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (!token) return;

        fetch('/Notifications/MarkAllAsRead', {
            method: 'POST',
            headers: { 'RequestVerificationToken': token }
        })
            .then(() => {
                document.querySelectorAll('.notification-item').forEach(item => {
                    item.classList.remove('unread');
                });
                updateBadge(0);
                window.showToast('All notifications marked as read', 'success');
            })
            .catch(error => console.error('Failed to mark all as read:', error));
    }

    // Update notification badge
    function updateBadge(count) {
        const badges = document.querySelectorAll('#notificationBadge, #sidebarUnreadCount');
        badges.forEach(badge => {
            if (count > 0) {
                badge.textContent = count > 99 ? '99+' : count;
                badge.style.display = 'block';
            } else {
                badge.style.display = 'none';
            }
        });
    }

    // Increment badge count
    function incrementBadge() {
        const badge = document.getElementById('notificationBadge');
        if (badge) {
            const current = parseInt(badge.textContent) || 0;
            updateBadge(current + 1);
        }
    }

    // Decrement badge count
    function decrementBadge() {
        const badge = document.getElementById('notificationBadge');
        if (badge) {
            const current = parseInt(badge.textContent) || 0;
            if (current > 0) {
                updateBadge(current - 1);
            }
        }
    }

    // Update pending approvals count
    function updatePendingApprovalsCount(count) {
        const badges = document.querySelectorAll('#pendingApprovalsCount, #sidebarPendingCount');
        badges.forEach(badge => {
            if (count > 0) {
                badge.textContent = count;
                badge.style.display = 'inline-block';
            } else {
                badge.style.display = 'none';
            }
        });
    }

    // Update connection status indicator
    function updateConnectionStatus(status) {
        // Could add a visual indicator if needed
        console.log('Connection status:', status);
    }

    // Check if sound should play for notification
    function playNotificationSound() {
        try {
            // Create and play notification sound
            const audio = new Audio();
            audio.volume = 0.5;

            // Try to load the sound file
            audio.src = '/sounds/notify.mp3';

            audio.play().catch(error => {
                console.log('Primary sound failed, trying fallback');
                // Fallback to a data URI beep sound
                audio.src = 'data:audio/wav;base64,UklGRnoGAABXQVZFZm10IBAAAAABAAEARKwAAIhYAQACABAAZGF0YQoGAACBhYqFbF1fdJivrJBhNjVgodDbq2EcBj+a2/LDciUFLIHO8tiJNwgZaLvt559NEAxQp+PwtmMcBjiR1/LMeSwFJHfH8N2QQAoUXrTp66hVFApGn+DyvmwhBTGH0fPTgjMGHm7A7+OZURE';
                audio.play().catch(() => {
                    // Final fallback: use Web Audio API
                    playBeepSound();
                });
            });
        } catch (error) {
            console.error('Error playing notification sound:', error);
            playBeepSound();
        }
    }

    function playBeepSound() {
        try {
            const audioContext = new (window.AudioContext || window.webkitAudioContext)();
            const oscillator = audioContext.createOscillator();
            const gainNode = audioContext.createGain();

            oscillator.connect(gainNode);
            gainNode.connect(audioContext.destination);

            oscillator.frequency.value = 800;
            oscillator.type = 'sine';

            gainNode.gain.setValueAtTime(0.3, audioContext.currentTime);
            gainNode.gain.exponentialRampToValueAtTime(0.01, audioContext.currentTime + 0.5);

            oscillator.start(audioContext.currentTime);
            oscillator.stop(audioContext.currentTime + 0.5);
        } catch (error) {
            console.log('Could not play beep sound');
        }
    }

    // Helper functions
    function getNotificationType(type) {
        const types = {
            'ApprovalRequest': 'warning',
            'ApprovalResponse': 'success',
            'ProductUpdate': 'info',
            'RouteUpdate': 'info',
            'RouteCompleted': 'success',
            'TransferCompleted': 'success',
            'System': 'secondary'
        };
        return types[type] || 'info';
    }

    function getNotificationIcon(type) {
        const icons = {
            'ApprovalRequest': 'fas fa-clock',
            'ApprovalResponse': 'fas fa-check-circle',
            'ProductUpdate': 'fas fa-box',
            'RouteUpdate': 'fas fa-route',
            'RouteCompleted': 'fas fa-check-double',
            'TransferCompleted': 'fas fa-exchange-alt',
            'System': 'fas fa-info-circle'
        };
        return icons[type] || 'fas fa-bell';
    }

    function getNotificationIconColor(type) {
        const colors = {
            'ApprovalRequest': 'bg-warning',
            'ApprovalResponse': 'bg-success',
            'ProductUpdate': 'bg-info',
            'RouteUpdate': 'bg-primary',
            'RouteCompleted': 'bg-success',
            'TransferCompleted': 'bg-success',
            'System': 'bg-secondary'
        };
        return colors[type] || 'bg-primary';
    }

    function formatTimeAgo(dateString) {
        const date = new Date(dateString);
        const now = new Date();
        const diffMs = now - date;
        const diffSecs = Math.floor(diffMs / 1000);
        const diffMins = Math.floor(diffSecs / 60);
        const diffHours = Math.floor(diffMins / 60);
        const diffDays = Math.floor(diffHours / 24);

        if (diffSecs < 60) return 'just now';
        if (diffMins < 60) return `${diffMins}m ago`;
        if (diffHours < 24) return `${diffHours}h ago`;
        if (diffDays < 30) return `${diffDays}d ago`;

        return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    }

    function escapeHtml(text) {
        if (typeof text !== 'string') return '';
        const map = {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        };
        return text.replace(/[&<>"']/g, m => map[m]);
    }

    // Public API
    return {
        initialize: initialize,
        markAsRead: markAsRead,
        disconnect: function () {
            if (connection) {
                connection.stop();
                isInitialized = false;
            }
        },
        getConnectionState: function () {
            return connection ? connection.state : 'Not initialized';
        }
    };
})();