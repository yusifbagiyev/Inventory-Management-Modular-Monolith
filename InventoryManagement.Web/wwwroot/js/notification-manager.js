window.NotificationManager = (function () {
    'use strict';

    let connection = null;
    let connectionRetryCount = 0;
    const maxRetries = 5; // Reduced from 10 to prevent excessive retries
    let connectionState = 'disconnected';
    let reconnectTimeout = null;
    let isInitialized = false; // Flag to prevent multiple initializations

    // Short drops (a laptop waking up, a network switch, a restart) usually recover within
    // seconds; only an outage that lasts is worth a toast, and "restored" only follows that toast.
    const OUTAGE_NOTICE_DELAY_MS = 10000;
    let outageNoticeTimer = null;
    let outageNoticeShown = false;
    let hasConnectedBefore = false; // a later start() is a reconnection: pages may have missed changes
    let suspended = false;

    // Browsers freeze background tabs and keep pages in the back/forward cache; both cut the
    // socket, which showed up as "Connection lost" on every back/forward. Close it quietly
    // first and reopen when the page is back (the reconnect makes open pages resync).
    function suspendConnection() {
        suspended = true;
        clearTimeout(reconnectTimeout);
        reconnectTimeout = null;
        clearTimeout(outageNoticeTimer);
        outageNoticeTimer = null;
        if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
            connection.stop();
        }
    }

    function resumeConnection() {
        if (!suspended) return;
        suspended = false;
        connectionRetryCount = 0;
        if (connection) startConnection();
    }

    window.addEventListener('pagehide', e => { if (e.persisted) suspendConnection(); });
    window.addEventListener('pageshow', e => { if (e.persisted) resumeConnection(); });
    document.addEventListener('freeze', suspendConnection);
    document.addEventListener('resume', resumeConnection);

    function noteOutage() {
        if (outageNoticeTimer || outageNoticeShown) return;
        outageNoticeTimer = setTimeout(() => {
            outageNoticeTimer = null;
            outageNoticeShown = true;
            showToast('Connection lost. Reconnecting...', 'warning');
        }, OUTAGE_NOTICE_DELAY_MS);
    }

    function noteRecovered() {
        clearTimeout(outageNoticeTimer);
        outageNoticeTimer = null;
        if (outageNoticeShown) {
            outageNoticeShown = false;
            showToast('Connection restored', 'success');
        }
    }

    // Track recent notifications to prevent duplicates
    const recentNotifications = new Map();
    const DUPLICATE_CHECK_WINDOW = 5000; // 5 seconds


    // Initialize the notification system (with duplicate protection)
    function initialize(isAdmin) {
        if (isInitialized) {
            return;
        }

        // Only rendered for signed-in users; the hub authenticates with the auth cookie.
        isInitialized = true;
        window.isAdmin = isAdmin;
        establishConnection();
    }


    // Establish SignalR connection with improved error handling
    function establishConnection() {
        if (connection && connection.state === signalR.HubConnectionState.Connected) {
            return;
        }

        // Clean up any existing connection first
        if (connection) {
            connection.stop();
            connection = null;
        }

        const hubUrl = AppConfig.signalR.notificationHub;

        // Create the connection with proper configuration
        connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
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
                    return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 16000);
                }
            })
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        // Pairs with the server's ClientTimeoutInterval (2 min) and KeepAliveInterval (15 s).
        connection.serverTimeoutInMilliseconds = 60000;

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
            noteOutage();
        });

        connection.onreconnected((connectionId) => {
            connectionState = 'connected';
            connectionRetryCount = 0;
            noteRecovered();
            window.dispatchEvent(new Event('live:resync'));

            // Reload data after reconnection, but with a delay to avoid overwhelming the server
            setTimeout(() => {
                loadRecentNotifications();
                loadNotificationCount();

                if (window.isAdmin) {
                    // Use the debounced version to avoid rapid calls
                    debouncedLoadPendingApprovalsCount();
                }
            }, 1000);
        });

        connection.onclose((error) => {
            connectionState = 'disconnected';
            if (suspended) return; // closed on purpose; resumeConnection() reopens it
            console.error('SignalR connection closed:', error);
            noteOutage();

            // Only try to reconnect if we haven't exceeded max retries
            if (connectionRetryCount < maxRetries) {
                connectionRetryCount++;
                scheduleReconnect(5000);
            } else {
                console.error('Maximum reconnection attempts exceeded');
                clearTimeout(outageNoticeTimer);
                outageNoticeTimer = null;
                showToast('Unable to connect to notification service', 'error');
                // Reset for potential future retry attempts
                setTimeout(() => {
                    connectionRetryCount = 0;
                }, 60000); // Reset after 1 minute
            }
        });
    }



    // Set up message handlers with duplicate prevention
    function setupMessageHandlers() {
        // Connection established confirmation
        connection.on("ConnectionEstablished", function (data) {
            connectionState = 'connected';
            connectionRetryCount = 0;

            // Store connection info for debugging
            window.notificationInfo = {
                userId: data.userId,
                userName: data.userName,
                userGroup: data.userGroup,
                roleGroups: data.roleGroups
            };


            // Initial load of data (with slight delay to ensure UI is ready)
            setTimeout(() => {
                loadRecentNotifications();
                loadNotificationCount();
            }, 500);
        });

        // Handle incoming notifications with duplicate prevention
        connection.on("ReceiveNotification", function (notification) {
            // Check for duplicate notifications
            if (isDuplicateNotification(notification)) {
                return;
            }

            // Track this notification
            trackNotification(notification);

            // Handle the notification
            handleIncomingNotification(notification);
        });

        // Handle pending notifications (sent when connecting)
        connection.on("ReceivePendingNotification", function (notification) {
            // For pending notifications, we don't want to show individual toasts
            // Just update the badge count
            window.incrementNotificationCount();
        });

        // Pending notifications complete
        connection.on("PendingNotificationsComplete", function (data) {
            // Reload the notification list and count after receiving all pending
            setTimeout(() => {
                window.loadRecentNotifications();
                window.loadNotificationCount();
            }, 100);
        });

        // Handle approval refresh (for admins) with rate limiting
        connection.on("RefreshApprovals", function (data) {
            if (window.isAdmin) {
                // Use debounced function to prevent rapid successive calls
                if (typeof debouncedLoadPendingApprovalsCount === 'function') {
                    debouncedLoadPendingApprovalsCount();
                } else if (typeof loadPendingApprovalsCount === 'function') {
                    loadPendingApprovalsCount();
                }

            }
        });

        // A change was committed somewhere in the system. live-updates.js decides whether the
        // open page shows that kind of record and refreshes it (the approvals list included).
        connection.on("EntityChanged", function (update) {
            window.dispatchEvent(new CustomEvent('live:changed', { detail: update }));
        });
    }



    // Check if notification is a duplicate
    function isDuplicateNotification(notification) {
        if (!notification || !notification.id) {
            return false;
        }

        const notificationKey = `${notification.id}-${notification.type}`;
        const now = Date.now();

        // Check if we've seen this notification recently
        if (recentNotifications.has(notificationKey)) {
            const lastSeen = recentNotifications.get(notificationKey);
            if (now - lastSeen < DUPLICATE_CHECK_WINDOW) {
                return true; // This is a duplicate
            }
        }

        return false;
    }



    // Track notification to prevent duplicates
    function trackNotification(notification) {
        if (!notification || !notification.id) {
            return;
        }

        const notificationKey = `${notification.id}-${notification.type}`;
        const now = Date.now();

        // Store the current time for this notification
        recentNotifications.set(notificationKey, now);

        // Clean up old entries to prevent memory leaks
        if (recentNotifications.size > 100) { // Keep only last 100 entries
            const entries = Array.from(recentNotifications.entries());
            entries.sort((a, b) => b[1] - a[1]); // Sort by timestamp, newest first

            // Keep only the 50 most recent
            recentNotifications.clear();
            entries.slice(0, 50).forEach(([key, timestamp]) => {
                recentNotifications.set(key, timestamp);
            });
        }
    }



    // Start the connection with better error handling
    // Single owner of the reconnect timer. Previously onclose and the start() catch each held
    // their own timeout, so two retry chains could run in parallel and open duplicate connections.
    function scheduleReconnect(delay) {
        if (reconnectTimeout) {
            clearTimeout(reconnectTimeout);
        }
        reconnectTimeout = setTimeout(() => {
            reconnectTimeout = null;
            startConnection();
        }, delay);
    }

    function startConnection() {
        if (connectionState === 'connecting' || connectionState === 'connected') {
            return;
        }

        connectionState = 'connecting';

        connection.start()
            .then(() => {
                connectionState = 'connected';
                connectionRetryCount = 0;
                noteRecovered();
                if (hasConnectedBefore) window.dispatchEvent(new Event('live:resync'));
                hasConnectedBefore = true;

                // Clear any existing reconnect timeout
                if (reconnectTimeout) {
                    clearTimeout(reconnectTimeout);
                    reconnectTimeout = null;
                }
            })
            .catch(err => {
                connectionState = 'disconnected';
                console.error('❌ SignalR connection failed:', err);

                // Only retry if we haven't exceeded the limit and it's not an auth error
                if (connectionRetryCount < maxRetries && !isAuthError(err)) {
                    connectionRetryCount++;
                    const delay = Math.min(1000 * Math.pow(2, connectionRetryCount), 10000);
                    scheduleReconnect(delay);
                } else if (isAuthError(err)) {
                    console.error('Authentication error, user may need to login');
                    showToast('Authentication expired. Please refresh the page.', 'warning');
                } else {
                    console.error('Failed to establish SignalR connection after maximum retries');
                    showToast('Unable to connect to notification service', 'error');
                }
            });
    }



    // Check if error is authentication-related
    function isAuthError(error) {
        const errorMessage = error.message || error.toString();
        return errorMessage.includes('401') ||
            errorMessage.includes('Unauthorized') ||
            errorMessage.includes('authentication') ||
            errorMessage.includes('token');
    }



    // Handle incoming notification with improved logic
    function handleIncomingNotification(notification) {
        // Play sound (but not too frequently)
        if (shouldPlaySound()) {
            window.playNotificationSound();
        }

        // Show toast with appropriate type
        const toastType = window.getNotificationType(notification.type);
        showToast(`${notification.title}: ${notification.message}`, toastType);

        // Update UI elements
        window.incrementNotificationCount();

        // Debounce the notification list reload to prevent excessive calls
        clearTimeout(window.notificationListReloadTimeout);
        window.notificationListReloadTimeout = setTimeout(() => {
            window.loadRecentNotifications();
        }, 500);

        // Handle special notification types
        handleSpecialNotifications(notification);

        // Trigger custom event for other parts of the application
        $(document).trigger('notification:received', [notification]);
    }



    // The pending-approvals badge; the lists themselves refresh through live-updates.js.
    function handleSpecialNotifications(notification) {
        if (notification.type === 'ApprovalRequest' && window.isAdmin
            && typeof debouncedLoadPendingApprovalsCount === 'function') {
            debouncedLoadPendingApprovalsCount();
        }
    }

    // Prevent too frequent sound notifications
    let lastSoundPlayed = 0;
    function shouldPlaySound() {
        const now = Date.now();
        const timeSinceLastSound = now - lastSoundPlayed;

        if (timeSinceLastSound > 2000) { // Minimum 2 seconds between sounds
            lastSoundPlayed = now;
            return true;
        }
        return false;
    }




    // Public API
    return {
        initialize: initialize,
        getConnection: () => connection,
        getConnectionState: () => connectionState,
        isConnected: () => connectionState === 'connected',
        reconnect: () => {
            if (connectionState !== 'connected' && connectionState !== 'connecting') {
                connectionRetryCount = 0; // Reset retry count for manual reconnection
                establishConnection();
            }
        },
        disconnect: () => {
            isInitialized = false;
            if (connection) {
                connection.stop();
            }
            // Clear any pending timeouts
            if (reconnectTimeout) {
                clearTimeout(reconnectTimeout);
                reconnectTimeout = null;
            }
        }
    };
})();