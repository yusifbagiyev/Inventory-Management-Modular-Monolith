// Notification Manager with proper SignalR configuration
window.NotificationManager = (function () {
    'use strict';

    let connection = null;
    let isConnected = false;
    let reconnectAttempts = 0;
    const maxReconnectAttempts = 5;

    function initialize() {
        // Get the JWT token properly
        const token = sessionStorage.getItem('JwtToken') ||
            document.cookie.split('; ').find(row => row.startsWith('jwt_token='))?.split('=')[1];

        if (!token) {
            console.warn('No JWT token found, cannot connect to notifications');
            return;
        }

        // Build connection with proper configuration
        connection = new signalR.HubConnectionBuilder()
            .withUrl(AppConfig.signalR.notificationHub, {
                accessTokenProvider: () => token,
                transport: signalR.HttpTransportType.WebSockets |
                    signalR.HttpTransportType.ServerSentEvents |
                    signalR.HttpTransportType.LongPolling
            })
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: retryContext => {
                    if (retryContext.elapsedMilliseconds < 60000) {
                        return Math.random() * 10000;
                    } else {
                        return null;
                    }
                }
            })
            .configureLogging(signalR.LogLevel.Information)
            .build();

        // Set up event handlers
        setupEventHandlers();

        // Start the connection
        startConnection();
    }

    function setupEventHandlers() {
        // Handle incoming notifications
        connection.on("ReceiveNotification", function (notification) {
            console.log('Notification received:', notification);
            displayNotification(notification);
            playNotificationSound();
            updateNotificationBadge();
        });

        // Handle connection events
        connection.onreconnecting(error => {
            console.warn('SignalR reconnecting...', error);
            isConnected = false;
        });

        connection.onreconnected(connectionId => {
            console.log('SignalR reconnected:', connectionId);
            isConnected = true;
            joinGroups();
        });

        connection.onclose(error => {
            console.error('SignalR connection closed:', error);
            isConnected = false;
            if (reconnectAttempts < maxReconnectAttempts) {
                setTimeout(() => {
                    reconnectAttempts++;
                    startConnection();
                }, 5000);
            }
        });
    }

    function startConnection() {
        connection.start()
            .then(() => {
                console.log('SignalR connected successfully');
                isConnected = true;
                reconnectAttempts = 0;
                joinGroups();
            })
            .catch(err => {
                console.error('SignalR connection failed:', err);
                if (reconnectAttempts < maxReconnectAttempts) {
                    setTimeout(() => {
                        reconnectAttempts++;
                        startConnection();
                    }, 5000);
                }
            });
    }

    function joinGroups() {
        const userId = getUserId();
        if (userId) {
            // Use the correct method name that exists on the server
            connection.invoke("JoinGroup", `user-${userId}`)
                .then(() => console.log('Joined user notification group'))
                .catch(err => console.error('Failed to join group:', err));
        }
    }

    function getUserId() {
        // Extract user ID from JWT token or page data
        const userDataElement = document.getElementById('userData');
        if (userDataElement) {
            return userDataElement.dataset.userId;
        }
        return null;
    }

    function displayNotification(notification) {
        // Show toast notification
        if (typeof showToast === 'function') {
            const type = notification.type === 'Approval' ? 'info' :
                notification.type === 'Success' ? 'success' : 'info';
            showToast(notification.message || notification.title, type, 5000);
        }

        // Update notification dropdown if it exists
        updateNotificationDropdown(notification);
    }

    function playNotificationSound() {
        try {
            const audio = new Audio('/sounds/notification.mp3');
            audio.volume = 0.5;
            audio.play().catch(e => console.log('Could not play notification sound:', e));
        } catch (e) {
            console.log('Notification sound error:', e);
        }
    }

    function updateNotificationBadge() {
        const badge = document.querySelector('.notification-badge');
        if (badge) {
            const currentCount = parseInt(badge.textContent) || 0;
            badge.textContent = currentCount + 1;
            badge.style.display = 'flex';
        }
    }

    function updateNotificationDropdown(notification) {
        const notificationList = document.querySelector('.notification-list');
        if (!notificationList) return;

        const notificationHtml = `
            <div class="notification-item unread" data-id="${notification.id}">
                <div class="d-flex">
                    <div class="notification-icon bg-primary text-white">
                        <i class="fas fa-bell"></i>
                    </div>
                    <div class="notification-content ms-3 flex-grow-1">
                        <h6 class="mb-1">${notification.title}</h6>
                        <p class="mb-0 text-muted small">${notification.message}</p>
                        <div class="notification-time">
                            <i class="far fa-clock"></i> Just now
                            <span class="notification-dot"></span>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Remove empty state if exists
        const emptyState = notificationList.querySelector('.notification-empty');
        if (emptyState) {
            emptyState.remove();
        }

        // Add new notification at the top
        notificationList.insertAdjacentHTML('afterbegin', notificationHtml);
    }

    // Public API
    return {
        init: initialize,
        isConnected: () => isConnected,
        disconnect: () => {
            if (connection) {
                connection.stop();
            }
        }
    };
})();

// Initialize when document is ready
document.addEventListener('DOMContentLoaded', function () {
    if (document.querySelector('#userData') || sessionStorage.getItem('JwtToken')) {
        NotificationManager.init();
    }
});