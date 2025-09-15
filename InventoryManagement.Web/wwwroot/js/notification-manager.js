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

        // Enhanced token retrieval with multiple fallbacks
        const token = getAuthToken();
        if (!token) {
            console.warn('No authentication token available. Retrying in 2 seconds...');
            setTimeout(initialize, 2000); // Retry more frequently
            return;
        }

        console.log('Token found, initializing SignalR connection...');
        setupSignalRConnection(token);
        setupUI();
        loadInitialData();
        setupBrowserNotifications();
        isInitialized = true;
    }

    // Enhanced token retrieval function
    function getAuthToken() {
        // Simplified token retrieval - prioritize session storage
        let token = sessionStorage.getItem('JwtToken');
        if (token) return token;

        // Try hidden input
        const tokenInput = document.querySelector('#jwtToken');
        if (tokenInput?.value) {
            sessionStorage.setItem('JwtToken', tokenInput.value);
            return tokenInput.value;
        }

        //  Try cookie
        // Try cookie
        token = getCookie('jwt_token');
        if (token) {
            sessionStorage.setItem('JwtToken', token);
            return token;
        }

        return null;
    }

    // Helper function to get cookie value
    function getCookie(name) {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) {
            return parts.pop().split(';').shift();
        }
        return null;
    }

    function setupBrowserNotifications() {
        // Request permission for browser notifications
        if ('Notification' in window && Notification.permission === 'default') {
            Notification.requestPermission().then(permission => {
                console.log('Notification permission:', permission);
            });
        }
    }

    // Setup SignalR connection with enhanced error handling
    function setupSignalRConnection(token) {
        try {
            connection = new signalR.HubConnectionBuilder()
                .withUrl(config.signalR.hubUrl, {
                    accessTokenFactory: () => getAuthToken(),
                    transport: signalR.HttpTransportType.WebSockets |
                        signalR.HttpTransportType.ServerSentEvents |
                        signalR.HttpTransportType.LongPolling
                })
                .withAutomaticReconnect({
                    nextRetryDelayInMilliseconds: retryContext => {
                        if (reconnectAttempts >= 10) return null;
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
        // Main notification handler - THIS IS THE KEY PART
        connection.on("ReceiveNotification", function (notification) {
            console.log('Notification received:', notification);

            // Process immediately with visual popup
            processNotificationImmediately(notification);
        });

        // Handle connection established
        connection.on("ConnectionEstablished", function (data) {
            console.log('Connection established:', data);
            updateConnectionStatus('connected');
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
        });

        connection.onclose(error => {
            console.error('Connection closed:', error);
            updateConnectionStatus('disconnected');
            isInitialized = false;
            // Attempt to reconnect
            setTimeout(() => {
                if (reconnectAttempts < 10) initialize();
            }, 5000);
        });
    }

    // NEW FUNCTION: Process notification immediately with popup
    function processNotificationImmediately(notification) {
        console.log('Processing notification immediately:', notification);

        // 1. Show popup toast IMMEDIATELY
        showPopupNotification(notification);

        // 2. Update badge to red with animation
        updateBadgeUrgent();

        // 3. Add to dropdown
        addToDropdown(notification);

        // 4. Play sound
        playNotificationSound();

        // 5. Show browser notification
        showBrowserNotification(notification);

        // 6. Trigger specific events
        if (notification.type === 'ApprovalRequest' && window.isAdmin === 'true') {
            document.dispatchEvent(new CustomEvent('approvalRequestCreated', {
                detail: notification
            }));
        }
    }


    // NEW FUNCTION: Show popup notification with animation
    function showPopupNotification(notification) {
        const type = getNotificationType(notification.type);
        const icon = getNotificationIcon(notification.type);

        // Create enhanced popup HTML
        const popupHtml = `
            <div class="notification-popup animated-popup">
                <div class="d-flex align-items-start">
                    <div class="notification-icon-wrapper me-3">
                        <i class="${icon} fs-4"></i>
                    </div>
                    <div class="flex-grow-1">
                        <h6 class="mb-1 fw-bold">${escapeHtml(notification.title || 'New Notification')}</h6>
                        <p class="mb-2 text-muted small">${escapeHtml(notification.message || '')}</p>
                        <small class="text-muted">
                            <i class="fas fa-clock me-1"></i>Just now
                        </small>
                    </div>
                    <button type="button" class="btn-close" onclick="this.closest('.toast').remove()"></button>
                </div>
            </div>
        `;

        // Show as prominent toast
        showEnhancedToast(popupHtml, type, 15000); // 15 seconds duration
    }

    // NEW FUNCTION: Show enhanced toast with animation
    function showEnhancedToast(html, type, duration) {
        const toastId = 'toast-' + Date.now();
        const toastClass = type === 'error' ? 'danger' : type;

        const toastHtml = `
            <div id="${toastId}" class="toast notification-toast bg-${toastClass} show" 
                 role="alert" style="animation: slideInRight 0.5s ease;">
                <div class="toast-body text-white">
                    ${html}
                </div>
            </div>
        `;

        // Ensure container exists
        let container = document.getElementById('toastContainer');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toastContainer';
            container.className = 'toast-container position-fixed top-0 end-0 p-3';
            container.style.zIndex = '9999';
            document.body.appendChild(container);
        }

        container.insertAdjacentHTML('afterbegin', toastHtml);

        const toastElement = document.getElementById(toastId);

        // Auto remove after duration
        setTimeout(() => {
            toastElement.style.animation = 'slideOutRight 0.5s ease';
            setTimeout(() => toastElement.remove(), 500);
        }, duration);
    }

    // NEW FUNCTION: Update badge urgently
    function updateBadgeUrgent() {
        const badges = document.querySelectorAll('#notificationBadge, .notification-badge');
        badges.forEach(badge => {
            // Get current count or set to 0
            const currentCount = parseInt(badge.textContent) || 0;
            const newCount = currentCount + 1;

            badge.textContent = newCount > 99 ? '99+' : newCount;
            badge.style.display = 'block';

            // Make it red and animated
            badge.style.backgroundColor = '#dc3545';
            badge.style.color = 'white';
            badge.classList.add('pulse-animation');

            // Add CSS animation if not exists
            if (!document.querySelector('#notification-animations')) {
                const style = document.createElement('style');
                style.id = 'notification-animations';
                style.textContent = `
                    @keyframes pulse {
                        0% { transform: scale(1); }
                        50% { transform: scale(1.2); }
                        100% { transform: scale(1); }
                    }
                    .pulse-animation {
                        animation: pulse 0.5s ease 3;
                    }
                    @keyframes slideInRight {
                        from {
                            transform: translateX(100%);
                            opacity: 0;
                        }
                        to {
                            transform: translateX(0);
                            opacity: 1;
                        }
                    }
                    @keyframes slideOutRight {
                        from {
                            transform: translateX(0);
                            opacity: 1;
                        }
                        to {
                            transform: translateX(100%);
                            opacity: 0;
                        }
                    }
                    .notification-popup {
                        min-width: 400px;
                        box-shadow: 0 6px 20px rgba(0,0,0,0.2);
                    }
                `;
                document.head.appendChild(style);
            }
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
                setTimeout(() => {
                    if (reconnectAttempts < 10) {
                        reconnectAttempts++;
                        startConnection();
                    }
                }, 3000);
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

    // Helper functions
    function getCookie(name) {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) {
            return parts.pop().split(';').shift();
        }
        return null;
    }

    function escapeHtml(text) {
        if (!text) return '';
        const map = {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        };
        return text.toString().replace(/[&<>"']/g, m => map[m]);
    }

    function getNotificationType(type) {
        const types = {
            'ApprovalRequest': 'warning',
            'ApprovalResponse': 'success',
            'ProductUpdate': 'info',
            'RouteUpdate': 'primary'
        };
        return types[type] || 'info';
    }

    function getNotificationIcon(type) {
        const icons = {
            'ApprovalRequest': 'fas fa-clock',
            'ApprovalResponse': 'fas fa-check-circle',
            'ProductUpdate': 'fas fa-box',
            'RouteUpdate': 'fas fa-route'
        };
        return icons[type] || 'fas fa-bell';
    }

    function playNotificationSound() {
        try {
            const audio = new Audio('data:audio/wav;base64,UklGRnoGAABXQVZFZm10IBAAAAABAAEARKwAAIhYAQACABAAZGF0YQoGAACBhYqFbF1fdJivrJBhNjVgodDbq2EcBj+a2/LDciUFLIHO8tiJNwgZaLvt559NEAxQp+PwtmMcBjiR1/LMeSwFJHfH8N2QQAoUXrTp66hVFApGn+DyvmwhBTGH0fPTgjMGHm7A7+OZURE');
            audio.volume = 0.5;
            audio.play().catch(() => { });
        } catch (error) {
            console.log('Could not play notification sound');
        }
    }

    function showBrowserNotification(notification) {
        if ('Notification' in window && Notification.permission === 'granted') {
            try {
                const browserNotif = new Notification(notification.title || 'New Notification', {
                    body: notification.message,
                    icon: '/icon-192x192.png',
                    requireInteraction: false,
                    vibrate: [200, 100, 200]
                });

                setTimeout(() => browserNotif.close(), 10000);

                browserNotif.onclick = function () {
                    window.focus();
                    this.close();
                };
            } catch (error) {
                console.error('Error showing browser notification:', error);
            }
        }
    }

    function addToDropdown(notification) {
        const list = document.getElementById('notificationList');
        if (!list) return;

        const emptyState = list.querySelector('.notification-empty');
        if (emptyState) emptyState.remove();

        const itemHtml = `
            <div class="notification-item unread" data-id="${notification.id}">
                <div class="d-flex align-items-start">
                    <div class="notification-icon me-3">
                        <i class="${getNotificationIcon(notification.type)}"></i>
                    </div>
                    <div class="notification-content">
                        <h6>${escapeHtml(notification.title)}</h6>
                        <p>${escapeHtml(notification.message)}</p>
                        <small class="text-muted">Just now</small>
                    </div>
                </div>
            </div>
        `;

        list.insertAdjacentHTML('afterbegin', itemHtml);

        // Keep only 5 most recent
        const items = list.querySelectorAll('.notification-item');
        if (items.length > 5) {
            items[items.length - 1].remove();
        }
    }

    function processNotificationQueue() {
        while (notificationQueue.length > 0) {
            const notification = notificationQueue.shift();
            processNotificationImmediately(notification);
        }
    }

    function updateConnectionStatus(status) {
        console.log('Notification connection status:', status);
    }

    function setupUI() {
        document.getElementById('markAllAsRead')?.addEventListener('click', function (e) {
            e.preventDefault();
            markAllAsRead();
        });
    }

    function loadInitialData() {
        loadUnreadCount();
        loadRecentNotifications();
    }

    function loadUnreadCount() {
        fetch('/Notifications/GetUnreadCount')
            .then(response => response.json())
            .then(count => {
                const badges = document.querySelectorAll('#notificationBadge');
                badges.forEach(badge => {
                    if (count > 0) {
                        badge.textContent = count > 99 ? '99+' : count;
                        badge.style.display = 'block';
                    } else {
                        badge.style.display = 'none';
                    }
                });
            })
            .catch(error => console.error('Failed to load unread count:', error));
    }

    function loadRecentNotifications() {
        const list = document.getElementById('notificationList');
        if (!list) return;

        fetch('/Notifications/GetRecentNotifications')
            .then(response => response.json())
            .then(notifications => {
                if (!notifications || notifications.length === 0) {
                    list.innerHTML = `
                        <div class="notification-empty">
                            <i class="fas fa-bell-slash"></i>
                            <p class="mb-0">No new notifications</p>
                        </div>
                    `;
                } else {
                    list.innerHTML = notifications.map(n => `
                        <div class="notification-item ${n.isRead ? '' : 'unread'}" data-id="${n.id}">
                            <div class="d-flex align-items-start">
                                <div class="notification-icon me-3">
                                    <i class="${getNotificationIcon(n.type)}"></i>
                                </div>
                                <div class="notification-content">
                                    <h6>${escapeHtml(n.title)}</h6>
                                    <p>${escapeHtml(n.message)}</p>
                                </div>
                            </div>
                        </div>
                    `).join('');
                }
            })
            .catch(error => console.error('Failed to load notifications:', error));
    }

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

                const badges = document.querySelectorAll('#notificationBadge');
                badges.forEach(badge => {
                    badge.style.display = 'none';
                    badge.classList.remove('pulse-animation');
                });
            })
            .catch(error => console.error('Failed to mark all as read:', error));
    }

    // Public API
    return {
        initialize: initialize,
        markAsRead: function (notificationId) {
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

                    const badges = document.querySelectorAll('#notificationBadge');
                    badges.forEach(badge => {
                        const current = parseInt(badge.textContent) || 0;
                        if (current > 1) {
                            badge.textContent = current - 1;
                        } else {
                            badge.style.display = 'none';
                        }
                    });
                })
                .catch(error => console.error('Failed to mark as read:', error));
        },
        disconnect: function () {
            if (connection) {
                connection.stop();
                isInitialized = false;
            }
        }
    };
})();