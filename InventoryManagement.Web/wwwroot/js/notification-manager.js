window.NotificationManager = (function () {
    'use strict';

    let connection = null;
    let connectionRetryCount = 0;
    const maxRetries = 5; // Reduced from 10 to prevent excessive retries
    let connectionState = 'disconnected';
    let reconnectTimeout = null;
    let isInitialized = false; // Flag to prevent multiple initializations

    // Track recent notifications to prevent duplicates
    const recentNotifications = new Map();
    const DUPLICATE_CHECK_WINDOW = 5000; // 5 seconds


    // Initialize the notification system (with duplicate protection)
   async function initialize(isAdmin) {
        // Prevent multiple initializations
        if (isInitialized) {
            console.log('NotificationManager already initialized, skipping');
            return;
        }

       // Check if user is authenticated by trying to validate their session
       // This doesn't require checking for a token in the DOM
       try {
           const isAuthenticated = await SecureTokenProvider.isAuthenticated();

           if (!isAuthenticated) {
               console.log('User not authenticated, skipping notification initialization');
               return;
           }

           // Mark as initialized before proceeding
           isInitialized = true;

           // Store admin status passed from the page
           window.isAdmin = isAdmin;

           console.log('Initializing notification system for ' + (isAdmin ? 'admin' : 'regular') + ' user');

           // Establish the connection - it will fetch the token when needed
           establishConnection();

       } catch (error) {
           console.error('Failed to check authentication status:', error);
           // Don't initialize if we can't verify authentication
           return;
       }
   }


    // Establish SignalR connection with improved error handling
    function establishConnection() {
        if (connection && connection.state === signalR.HubConnectionState.Connected) {
            console.log('Already connected to notification hub');
            return;
        }

        // Clean up any existing connection first
        if (connection) {
            console.log('Cleaning up existing connection');
            connection.stop();
            connection = null;
        }

        const hubUrl = AppConfig.signalR.notificationHub;
        console.log('Connecting to notification hub at:', hubUrl);

        // Create the connection with proper configuration
        connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
                accessTokenFactory: async () => {
                    try {
                        // SECURITY: Fetch token from secure server endpoint
                        // Token is never stored in DOM, only in JavaScript memory temporarily
                        const token = await SecureTokenProvider.getToken();

                        if (!token) {
                            throw new Error('No authentication token available');
                        }

                        console.log('Token provided to SignalR connection');
                        return token;
                    } catch (error) {
                        console.error('Failed to get token for SignalR:', error);
                        throw new Error('Authentication failed - please refresh the page');
                    }
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
                    return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 16000);
                }
            })
            .configureLogging(signalR.LogLevel.Warning)
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
            console.error('SignalR connection closed:', error);

            // Only try to reconnect if we haven't exceeded max retries
            if (connectionRetryCount < maxRetries) {
                reconnectTimeout = setTimeout(() => {
                    console.log(`Attempting manual reconnection... (${connectionRetryCount + 1}/${maxRetries})`);
                    connectionRetryCount++;
                    startConnection();
                }, 5000);
            } else {
                console.error('Maximum reconnection attempts exceeded');
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
            console.log('✅ SignalR connection established:', data);
            connectionState = 'connected';
            connectionRetryCount = 0;

            // Store connection info for debugging
            window.notificationInfo = {
                userId: data.userId,
                userName: data.userName,
                userGroup: data.userGroup,
                roleGroups: data.roleGroups
            };

            console.log('Connected as:', data.userName, 'Groups:', [data.userGroup, ...data.roleGroups]);

            // Initial load of data (with slight delay to ensure UI is ready)
            setTimeout(() => {
                loadRecentNotifications();
                loadNotificationCount();
            }, 500);
        });

        // Handle incoming notifications with duplicate prevention
        connection.on("ReceiveNotification", function (notification) {
            console.log('📨 Notification received:', notification);

            // Check for duplicate notifications
            if (isDuplicateNotification(notification)) {
                console.log('Duplicate notification detected, ignoring:', notification.id);
                return;
            }

            // Track this notification
            trackNotification(notification);

            // Handle the notification
            handleIncomingNotification(notification);
        });

        // Handle pending notifications (sent when connecting)
        connection.on("ReceivePendingNotification", function (notification) {
            console.log('📬 Pending notification received:', notification);

            // For pending notifications, we don't want to show individual toasts
            // Just update the badge count
            window.incrementNotificationCount();
        });

        // Pending notifications complete
        connection.on("PendingNotificationsComplete", function (data) {
            console.log(`📭 Received ${data.count} pending notifications`);

            // Reload the notification list and count after receiving all pending
            setTimeout(() => {
                window.loadRecentNotifications();
                window.loadNotificationCount();
            }, 100);
        });

        // Handle approval refresh (for admins) with rate limiting
        connection.on("RefreshApprovals", function (data) {
            console.log('🔄 Refresh approvals signal received:', data);

            if (window.isAdmin) {
                // Use debounced function to prevent rapid successive calls
                if (typeof debouncedLoadPendingApprovalsCount === 'function') {
                    debouncedLoadPendingApprovalsCount();
                } else if (typeof loadPendingApprovalsCount === 'function') {
                    loadPendingApprovalsCount();
                }

                // If on approvals page, refresh the list (also with rate limiting)
                if (window.location.pathname.includes('/Approvals')) {
                    // Debounce page refreshes to prevent excessive updates
                    clearTimeout(window.approvalsPageRefreshTimeout);
                    window.approvalsPageRefreshTimeout = setTimeout(() => {
                        if (typeof window.refreshApprovalsList === 'function') {
                            window.refreshApprovalsList();
                        }
                    }, 1000);
                }
            }
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
                    console.log(`Retrying connection in ${delay}ms... (${connectionRetryCount}/${maxRetries})`);
                    reconnectTimeout = setTimeout(() => startConnection(), delay);
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



    // Handle special notification types with proper debouncing
    function handleSpecialNotifications(notification) {
        if (notification.type === 'ApprovalRequest' && window.isAdmin) {
            // Use debounced function to prevent rapid calls
            if (typeof debouncedLoadPendingApprovalsCount === 'function') {
                debouncedLoadPendingApprovalsCount();
            }

            // Handle approvals page refresh with debouncing
            if (window.location.pathname.includes('/Approvals')) {
                clearTimeout(window.approvalsRefreshTimeout);
                window.approvalsRefreshTimeout = setTimeout(() => {
                    // Try to refresh the table data without full page reload
                    refreshApprovalsTable();
                }, 1500); // 1.5 second delay to batch multiple notifications
            }
        }
        else if (notification.type === 'ApprovalResponse') {
            // Handle approval responses
            if (window.location.pathname.includes('/MyRequests')) {
                clearTimeout(window.myRequestsRefreshTimeout);
                window.myRequestsRefreshTimeout = setTimeout(() => {
                    location.reload();
                }, 1500);
            }
        }
    }

    function refreshApprovalsTable() {
        console.log('🔄 Refreshing approvals table...');

        // Check if DataTable exists
        const table = $('#approvalsTable');
        if (table.length && $.fn.DataTable.isDataTable(table)) {
            // Show a subtle loading indicator
            showSubtleLoader();

            // Reload the entire page content for the approvals section
            $.ajax({
                url: window.location.pathname,
                type: 'GET',
                success: function (html) {
                    // Extract just the table section from the response
                    const $newContent = $(html);
                    const $newTable = $newContent.find('#approvalsTable').closest('.card');
                    const $newStats = $newContent.find('.row.mb-4').first(); // Statistics cards

                    // Update statistics cards if they exist
                    if ($newStats.length) {
                        $('.row.mb-4').first().replaceWith($newStats);
                    }

                    // Update the table
                    if ($newTable.length) {
                        $('#approvalsTable').closest('.card').replaceWith($newTable);

                        // Reinitialize DataTable
                        $('#approvalsTable').DataTable({
                            order: [[0, 'desc']],
                            pageLength: 25
                        });

                        // Re-parse summaries for the new rows
                        $('.request-summary').each(function () {
                            const $this = $(this);
                            const actionData = $this.data('action-data');
                            const requestType = $this.closest('tr').find('.badge').first().text().trim();

                            try {
                                // Use the getRequestSummary function from the page
                                const summary = getRequestSummary(requestType, actionData);
                                $this.html(summary);
                            } catch (e) {
                                $this.html('<span class="text-danger">Error parsing data</span>');
                            }
                        });

                        hideSubtleLoader();

                        console.log('✅ Approvals table refreshed successfully');
                    } else {
                        // Fallback to full page reload if we can't find the table
                        console.warn('Could not find table in response, reloading page');
                        location.reload();
                    }
                },
                error: function (xhr, status, error) {
                    console.error('Failed to refresh approvals table:', error);
                    hideSubtleLoader();

                    // Show error message and offer manual refresh
                    showToast('New approvals available. Click to refresh.', 'warning', 10000)
                        .addEventListener('click', function () {
                            location.reload();
                        });
                }
            });
        } else {
            // If no DataTable, just reload the page
            console.log('No DataTable found, reloading page');
            location.reload();
        }
    }



    function showSubtleLoader() {
        if (!$('.subtle-loader').length) {
            const loader = $(`
            <div class="subtle-loader" style="position: fixed; top: 70px; right: 20px; z-index: 9998; 
                        background: rgba(255, 255, 255, 0.95); padding: 10px 20px; border-radius: 8px;
                        box-shadow: 0 2px 8px rgba(0,0,0,0.15); display: flex; align-items: center; gap: 10px;">
                <div class="spinner-border spinner-border-sm text-primary" role="status"></div>
                <span class="text-muted" style="font-size: 0.9rem;">Updating...</span>
            </div>
        `);
            $('body').append(loader);
        }
    }



    function hideSubtleLoader() {
        $('.subtle-loader').fadeOut(300, function () {
            $(this).remove();
        });
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


    // Make functions available globally
    window.refreshApprovalsTable = refreshApprovalsTable;
    window.showSubtleLoader = showSubtleLoader;
    window.hideSubtleLoader = hideSubtleLoader;


    // Public API
    return {
        initialize: initialize,
        getConnection: () => connection,
        getConnectionState: () => connectionState,
        isConnected: () => connectionState === 'connected',
        reconnect: () => {
            if (connectionState !== 'connected' && connectionState !== 'connecting') {
                console.log('Manual reconnection requested');
                connectionRetryCount = 0; // Reset retry count for manual reconnection
                establishConnection();
            } else {
                console.log('Already connected or connecting');
            }
        },
        disconnect: () => {
            console.log('Manual disconnection requested');
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