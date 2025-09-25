window.NotificationManager = (function () {
    'use strict';

    let connection = null;
    let connectionRetryCount = 0;
    const maxRetries = 10;
    let connectionState = 'disconnected';
    let reconnectTimeout = null;

    // Initialize the notification system
    function initialize(isAdmin) {
        const token = $('#jwtToken').val();
        if (!token) {
            console.log('No JWT token available, skipping notification initialization');
            return;
        }

        // Store admin status passed from the page
        window.isAdmin = isAdmin;

        console.log('Initializing notification system for ' + (isAdmin ? 'admin' : 'regular') + ' user');
        establishConnection();
    }

    // Establish SignalR connection
    function establishConnection() {
        if (connection && connection.state === signalR.HubConnectionState.Connected) {
            console.log('Already connected to notification hub');
            return;
        }

        const hubUrl = AppConfig.signalR.notificationHub;
        console.log('Connecting to notification hub at:', hubUrl);

        // Create the connection with proper configuration
        connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
                accessTokenFactory: () => {
                    const token = $('#jwtToken').val();
                    return token;
                },
                transport: signalR.HttpTransportType.WebSockets |
                    signalR.HttpTransportType.ServerSentEvents |
                    signalR.HttpTransportType.LongPolling,
                withCredentials: true
            })
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: retryContext => {
                    if (retryContext.previousRetryCount >= maxRetries) {
                        return null;
                    }
                    return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
                }
            })
            .configureLogging(signalR.LogLevel.Information)
            .build();

        // Set up event handlers before starting
        setupConnectionHandlers();
        setupMessageHandlers();

        // Start the connection
        startConnection();
    }

    // Set up connection lifecycle handlers
    function setupConnectionHandlers() {
        connection.onreconnecting((error) => {
            connectionState = 'reconnecting';
            console.warn('SignalR connection lost, attempting to reconnect...', error);
            showToast('Connection lost. Reconnecting...', 'warning');
        });

        connection.onreconnected((connectionId) => {
            connectionState = 'connected';
            connectionRetryCount = 0;
            console.log('SignalR reconnected successfully:', connectionId);
            showToast('Connection restored', 'success');

            // Reload notifications after reconnection
            loadRecentNotifications();
            loadNotificationCount();

            if (window.isAdmin) {
                loadPendingApprovalsCount();
            }
        });

        connection.onclose((error) => {
            connectionState = 'disconnected';
            console.error('SignalR connection closed:', error);

            // Try to reconnect after a delay
            if (connectionRetryCount < maxRetries) {
                reconnectTimeout = setTimeout(() => {
                    console.log('Attempting manual reconnection...');
                    startConnection();
                }, 5000);
            } else {
                showToast('Unable to connect to notification service', 'error');
            }
        });
    }

    // Set up message handlers
    function setupMessageHandlers() {
        // Connection established confirmation
        connection.on("ConnectionEstablished", function (data) {
            console.log('✅ SignalR connection established:', data);
            connectionState = 'connected';
            connectionRetryCount = 0;

            // Store connection info for debugging
            window.notificationInfo = {
                userId: data.userId,
                userName: data.userName,
                userGroup: data.userGroup,
                roleGroups: data.roleGroups,
                connectionId: data.connectionId
            };

            console.log('Connected as:', data.userName, 'Groups:', [data.userGroup, ...data.roleGroups]);

            // Initial load of notifications
            loadRecentNotifications();
            loadNotificationCount();
        });


        // Handle incoming notifications
        connection.on("ReceiveNotification", function (notification) {
            console.log('📨 Notification received:', notification);
            handleIncomingNotification(notification);
        });


        // Handle pending notifications (sent when connecting)
        connection.on("ReceivePendingNotification", function (notification) {
            console.log('📬 Pending notification received:', notification);
            // For pending notifications, we might not want to show toasts for each one
            // Just update the UI
            window.incrementNotificationCount();
        });


        // Pending notifications complete
        connection.on("PendingNotificationsComplete", function (data) {
            console.log(`📭 Received ${data.count} pending notifications`);
            // Reload the notification list to show all pending notifications
            window.loadRecentNotifications();
            window.loadNotificationCount();
        });


        // Handle approval refresh (for admins)
        connection.on("RefreshApprovals", function (data) {
            console.log('🔄 Refresh approvals signal received:', data);
            if (window.isAdmin) {
                loadPendingApprovalsCount();

                // If on approvals page, refresh the list
                if (window.location.pathname.includes('/Approvals')) {
                    if (typeof window.refreshApprovalsList === 'function') {
                        window.refreshApprovalsList();
                    }
                }
            }
        });


        // Ping/Pong for connection health
        connection.on("Pong", function (timestamp) {
            console.log('🏓 Pong received:', timestamp);
        });
    }

    // Start the connection
    function startConnection() {
        if (connectionState === 'connecting') {
            console.log('Connection already in progress');
            return;
        }

        connectionState = 'connecting';

        connection.start()
            .then(() => {
                connectionState = 'connected';
                connectionRetryCount = 0;
                console.log('✅ SignalR connected successfully');

                // Send a ping to verify connection
                connection.invoke("Ping").catch(err => {
                    console.error('Ping failed:', err);
                });
            })
            .catch(err => {
                connectionState = 'disconnected';
                connectionRetryCount++;
                console.error('❌ SignalR connection failed:', err);

                if (connectionRetryCount < maxRetries) {
                    const delay = Math.min(1000 * Math.pow(2, connectionRetryCount), 10000);
                    console.log(`Retrying connection in ${delay}ms... (Attempt ${connectionRetryCount}/${maxRetries})`);
                    setTimeout(() => startConnection(), delay);
                } else {
                    console.error('Failed to establish SignalR connection after maximum retries');
                    showToast('Unable to connect to notification service', 'error');
                }
            });
    }

    // Handle incoming notification
    function handleIncomingNotification(notification) {
        // Play sound
        window.playNotificationSound();

        // Show toast
        const toastType = window.getNotificationType(notification.type);
        showToast(`${notification.title}: ${notification.message}`, toastType);

        // Update UI elements
        window.incrementNotificationCount();
        window.loadRecentNotifications();

        // Special handling for different notification types
        if (notification.type === 'ApprovalRequest' && window.isAdmin) {
            window.loadPendingApprovalsCount();

            if (window.location.pathname.includes('/Approvals')) {
                if (typeof window.refreshApprovalsList === 'function') {
                    window.refreshApprovalsList();
                } else {
                    location.reload();
                }
            }
        }
        else if (notification.type === 'ApprovalResponse') {
            if (window.location.pathname.includes('/MyRequests')) {
                location.reload();
            }
        }

        // Trigger custom event for other parts of the application
        $(document).trigger('notification:received', [notification]);
    }

    // Public API
    return {
        initialize: initialize,
        getConnection: () => connection,
        getConnectionState: () => connectionState,
        isConnected: () => connectionState === 'connected',
        reconnect: () => {
            if (connectionState !== 'connected' && connectionState !== 'connecting') {
                establishConnection();
            }
        },
        disconnect: () => {
            if (connection) {
                connection.stop();
            }
        }
    };
})();