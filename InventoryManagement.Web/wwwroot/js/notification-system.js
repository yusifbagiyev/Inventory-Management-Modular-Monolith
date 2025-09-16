// wwwroot/js/notification-system.js
window.NotificationSystem = (function () {
    'use strict';

    let connection = null;
    let reconnectAttempts = 0;
    const maxReconnectAttempts = 5;
    let notificationSound = null;
    let unreadCount = 0;

    // Initialize the notification system
    function initialize() {
        console.log('Initializing Notification System...');

        // Initialize notification sound
        initializeSound();

        // Get authentication token
        const token = getAuthToken();
        if (!token) {
            console.warn('No authentication token found. Retrying in 3 seconds...');
            setTimeout(initialize, 3000);
            return;
        }

        // Setup SignalR connection
        setupSignalRConnection(token);

        // Load initial notification count
        loadUnreadCount();

        // Setup UI event handlers
        setupUIHandlers();
    }

    // Initialize notification sound
    function initializeSound() {
        notificationSound = new Audio('/sounds/notify.mp3');
        notificationSound.volume = 0.5;

        // Fallback to generated beep if MP3 fails
        notificationSound.onerror = function () {
            console.log('notify.mp3 not found, using generated sound');
            notificationSound = null; // Will use generated beep
        };
    }

    // Play notification sound
    function playNotificationSound() {
        if (notificationSound) {
            notificationSound.play().catch(err => {
                console.log('Could not play notification sound:', err);
                generateBeep();
            });
        } else {
            generateBeep();
        }
    }

    // Generate a beep sound as fallback
    function generateBeep() {
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
    }

    // Get authentication token from various sources
    function getAuthToken() {
        // Try session storage first
        let token = sessionStorage.getItem('JwtToken');
        if (token) return token;

        // Try hidden input field
        const tokenInput = document.querySelector('#jwtToken');
        if (tokenInput && tokenInput.value) {
            return tokenInput.value;
        }

        // Try cookie as last resort
        const cookies = document.cookie.split(';');
        for (let cookie of cookies) {
            const [name, value] = cookie.trim().split('=');
            if (name === 'jwt_token') {
                return decodeURIComponent(value);
            }
        }

        return null;
    }

    // Setup SignalR connection
    function setupSignalRConnection(token) {
        try {
            connection = new signalR.HubConnectionBuilder()
                .withUrl('/notificationHub', {
                    accessTokenFactory: () => token,
                    transport: signalR.HttpTransportType.WebSockets |
                        signalR.HttpTransportType.ServerSentEvents
                })
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .configureLogging(signalR.LogLevel.Information)
                .build();

            // Setup event handlers
            setupConnectionHandlers();

            // Start the connection
            startConnection();
        } catch (error) {
            console.error('Failed to setup SignalR connection:', error);
            setTimeout(() => initialize(), 5000);
        }
    }

    // Setup connection event handlers
    function setupConnectionHandlers() {
        // Handle incoming notifications - THIS IS THE KEY HANDLER
        connection.on("ReceiveNotification", function (notification) {
            console.log('📨 Notification received:', notification);
            handleIncomingNotification(notification);
        });

        // Handle connection established
        connection.on("ConnectionEstablished", function (data) {
            console.log('✅ SignalR connection established:', data);
            reconnectAttempts = 0;
            showSystemNotification('Connected', 'Connected to notification service', 'success');
        });

        // Handle reconnecting
        connection.onreconnecting((error) => {
            console.warn('🔄 Reconnecting to SignalR...', error);
            updateConnectionStatus('reconnecting');
        });

        // Handle reconnected
        connection.onreconnected((connectionId) => {
            console.log('✅ Reconnected to SignalR');
            reconnectAttempts = 0;
            updateConnectionStatus('connected');
        });

        // Handle connection closed
        connection.onclose((error) => {
            console.error('❌ SignalR connection closed:', error);
            updateConnectionStatus('disconnected');

            if (reconnectAttempts < maxReconnectAttempts) {
                reconnectAttempts++;
                setTimeout(() => startConnection(), 5000);
            }
        });
    }

    // Start the SignalR connection
    function startConnection() {
        connection.start()
            .then(() => {
                console.log('✅ Connected to SignalR hub');
                updateConnectionStatus('connected');

                // Join user group
                connection.invoke("JoinUserGroup")
                    .then(() => console.log('Joined user notification group'))
                    .catch(err => console.error('Failed to join user group:', err));

                // Test the connection
                connection.invoke("TestNotification")
                    .catch(err => console.error('Test notification failed:', err));
            })
            .catch(err => {
                console.error('❌ Failed to connect to SignalR:', err);
                updateConnectionStatus('disconnected');

                if (reconnectAttempts < maxReconnectAttempts) {
                    reconnectAttempts++;
                    setTimeout(() => startConnection(), 5000);
                }
            });
    }

    // Handle incoming notification
    function handleIncomingNotification(notification) {
        // Play notification sound
        playNotificationSound();

        // Update badge count
        incrementBadgeCount();

        // Show popup notification
        showPopupNotification(notification);

        // Add to dropdown
        addToDropdown(notification);

        // Show browser notification if permitted
        showBrowserNotification(notification);

        // Trigger custom events for specific notification types
        if (notification.type === 'ProductUpdate') {
            document.dispatchEvent(new CustomEvent('productNotification', {
                detail: notification
            }));
        }
    }

    // Show popup notification in upper right corner
    function showPopupNotification(notification) {
        const toastId = `toast-${Date.now()}`;
        const type = getNotificationType(notification.type);
        const icon = getNotificationIcon(notification.type);

        const toastHtml = `
            <div id="${toastId}" class="toast notification-toast show" 
                 style="min-width: 350px; animation: slideInRight 0.5s ease;">
                <div class="toast-header bg-${type} text-white">
                    <i class="${icon} me-2"></i>
                    <strong class="me-auto">${escapeHtml(notification.title || 'Notification')}</strong>
                    <small class="text-white-50">Just now</small>
                    <button type="button" class="btn-close btn-close-white" 
                            onclick="this.closest('.toast').remove()"></button>
                </div>
                <div class="toast-body">
                    ${escapeHtml(notification.message || '')}
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

        // Add toast to container
        container.insertAdjacentHTML('afterbegin', toastHtml);

        // Auto-remove after 10 seconds
        setTimeout(() => {
            const element = document.getElementById(toastId);
            if (element) {
                element.style.animation = 'slideOutRight 0.5s ease';
                setTimeout(() => element.remove(), 500);
            }
        }, 10000);
    }

    // Show system notification (for success messages)
    function showSystemNotification(title, message, type = 'success') {
        showPopupNotification({
            title: title,
            message: message,
            type: type,
            createdAt: new Date()
        });
    }

    // Update badge count
    function incrementBadgeCount() {
        unreadCount++;
        updateBadgeDisplay();
    }

    // Update badge display
    function updateBadgeDisplay() {
        const badges = document.querySelectorAll('#notificationBadge, .notification-badge');
        badges.forEach(badge => {
            if (unreadCount > 0) {
                badge.textContent = unreadCount > 99 ? '99+' : unreadCount;
                badge.style.display = 'block';
                badge.classList.add('pulse-animation');
            } else {
                badge.style.display = 'none';
                badge.classList.remove('pulse-animation');
            }
        });
    }

    // Load unread notification count
    function loadUnreadCount() {
        fetch('/Notifications/GetUnreadCount')
            .then(response => response.json())
            .then(count => {
                unreadCount = count;
                updateBadgeDisplay();
            })
            .catch(err => console.error('Failed to load unread count:', err));
    }

    // Add notification to dropdown
    function addToDropdown(notification) {
        const list = document.getElementById('notificationList');
        if (!list) return;

        // Remove empty state if exists
        const emptyState = list.querySelector('.notification-empty');
        if (emptyState) emptyState.remove();

        const icon = getNotificationIcon(notification.type);
        const itemHtml = `
            <div class="notification-item unread" data-id="${notification.id}">
                <div class="d-flex align-items-start p-2">
                    <div class="notification-icon me-3">
                        <i class="${icon}"></i>
                    </div>
                    <div class="flex-grow-1">
                        <h6 class="mb-1">${escapeHtml(notification.title)}</h6>
                        <p class="mb-0 small text-muted">${escapeHtml(notification.message)}</p>
                        <small class="text-muted">Just now</small>
                    </div>
                </div>
            </div>
        `;

        list.insertAdjacentHTML('afterbegin', itemHtml);

        // Keep only 5 most recent in dropdown
        const items = list.querySelectorAll('.notification-item');
        if (items.length > 5) {
            items[items.length - 1].remove();
        }
    }

    // Show browser notification
    function showBrowserNotification(notification) {
        if ('Notification' in window && Notification.permission === 'granted') {
            try {
                const browserNotif = new Notification(notification.title || 'New Notification', {
                    body: notification.message,
                    icon: '/icon-192x192.png',
                    tag: `notification-${notification.id}`,
                    requireInteraction: false
                });

                browserNotif.onclick = function () {
                    window.focus();
                    this.close();
                };

                setTimeout(() => browserNotif.close(), 5000);
            } catch (err) {
                console.log('Browser notification failed:', err);
            }
        }
    }

    // Setup UI event handlers
    function setupUIHandlers() {
        // Mark all as read
        document.getElementById('markAllAsRead')?.addEventListener('click', function (e) {
            e.preventDefault();
            markAllAsRead();
        });

        // Request browser notification permission
        if ('Notification' in window && Notification.permission === 'default') {
            Notification.requestPermission();
        }
    }

    // Mark all notifications as read
    function markAllAsRead() {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (!token) return;

        fetch('/Notifications/MarkAllAsRead', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            }
        })
            .then(() => {
                unreadCount = 0;
                updateBadgeDisplay();

                // Mark all items in dropdown as read
                document.querySelectorAll('.notification-item.unread').forEach(item => {
                    item.classList.remove('unread');
                });
            })
            .catch(err => console.error('Failed to mark all as read:', err));
    }

    // Update connection status indicator
    function updateConnectionStatus(status) {
        const indicator = document.getElementById('connectionStatus');
        if (indicator) {
            indicator.className = `connection-status ${status}`;
            indicator.title = `Connection: ${status}`;
        }

        console.log(`📡 Connection status: ${status}`);
    }

    // Helper functions
    function getNotificationType(type) {
        const types = {
            'ApprovalRequest': 'warning',
            'ApprovalResponse': 'success',
            'ProductUpdate': 'info',
            'RouteUpdate': 'primary',
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
            'System': 'fas fa-info-circle'
        };
        return icons[type] || 'fas fa-bell';
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

    // Public API
    return {
        initialize: initialize,
        showSuccess: function (message) {
            showSystemNotification('Success', message, 'success');
            playNotificationSound();
        },
        showError: function (message) {
            showSystemNotification('Error', message, 'danger');
        },
        showInfo: function (message) {
            showSystemNotification('Information', message, 'info');
        },
        testConnection: function () {
            if (connection && connection.state === signalR.HubConnectionState.Connected) {
                connection.invoke("TestNotification");
            } else {
                console.error('SignalR not connected');
            }
        }
    };
})();