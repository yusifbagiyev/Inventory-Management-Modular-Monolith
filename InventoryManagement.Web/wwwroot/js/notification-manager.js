// SignalR connection for notifications and live updates, with its own reconnect and outage toasts
window.NotificationManager = (function () {
    'use strict';

    let connection = null;
    let connectionRetryCount = 0;
    const maxRetries = 5;
    let connectionState = 'disconnected';
    let reconnectTimeout = null;
    let isInitialized = false;

    // Short drops recover within seconds, so only a lasting outage gets a toast and then a restored one
    const OUTAGE_NOTICE_DELAY_MS = 10000;
    let outageNoticeTimer = null;
    let outageNoticeShown = false;
    let hasConnectedBefore = false; // A later start() is a reconnect, so pages may have missed changes
    let suspended = false;

    // Frozen and back/forward cached tabs lose the socket, so close it quietly and reopen it on return
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
        if (!connection) return;
        if (document.hidden) waitUntilVisible();
        else startConnection();
    }

    // A hidden tab that loses the socket waits to be shown instead of reconnecting every minute
    let waitingForVisible = false;

    function waitUntilVisible() {
        waitingForVisible = true;
        clearTimeout(reconnectTimeout);
        reconnectTimeout = null;
        clearTimeout(outageNoticeTimer);
        outageNoticeTimer = null;
    }

    // Reconnecting also reloads the notification list and count, see ConnectionEstablished
    document.addEventListener('visibilitychange', () => {
        if (document.hidden || !waitingForVisible || suspended || !connection) return;
        waitingForVisible = false;
        connectionRetryCount = 0;
        startConnection();
    });

    window.addEventListener('pagehide', e => { if (e.persisted) suspendConnection(); });
    window.addEventListener('pageshow', e => { if (e.persisted) resumeConnection(); });
    document.addEventListener('freeze', suspendConnection);
    document.addEventListener('resume', resumeConnection);

    function noteOutage() {
        if (outageNoticeTimer || outageNoticeShown) return;
        outageNoticeTimer = setTimeout(() => {
            outageNoticeTimer = null;
            outageNoticeShown = true;
            showToast(t('Connection lost. Reconnecting...'), 'warning');
        }, OUTAGE_NOTICE_DELAY_MS);
    }

    function noteRecovered() {
        clearTimeout(outageNoticeTimer);
        outageNoticeTimer = null;
        if (outageNoticeShown) {
            outageNoticeShown = false;
            showToast(t('Connection restored'), 'success');
        }
    }

    const recentNotifications = new Map();
    const DUPLICATE_CHECK_WINDOW = 5000;


    function initialize(isAdmin) {
        if (isInitialized) {
            return;
        }

        // Only rendered for signed-in users, and the hub authenticates with the cookie
        isInitialized = true;
        window.isAdmin = isAdmin;
        establishConnection();
    }


    function establishConnection() {
        if (connection && connection.state === signalR.HubConnectionState.Connected) {
            return;
        }

        if (connection) {
            connection.stop();
            connection = null;
        }

        const hubUrl = AppConfig.signalR.notificationHub;

        connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
                transport: signalR.HttpTransportType.WebSockets |
                    signalR.HttpTransportType.ServerSentEvents |
                    signalR.HttpTransportType.LongPolling,
                withCredentials: true
            })
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: retryContext => {
                    // Giving up in a hidden tab hands over to onclose, which waits for the tab to be shown
                    if (document.hidden || retryContext.previousRetryCount >= maxRetries) {
                        return null;
                    }
                    return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 16000);
                }
            })
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        // Matches the server's 15 s keep-alive and 2 min client timeout
        connection.serverTimeoutInMilliseconds = 60000;

        setupConnectionHandlers();
        setupMessageHandlers();

        startConnection();
    }



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

            // A short delay so reconnecting tabs do not all hit the server at once
            setTimeout(() => {
                loadRecentNotifications();
                loadNotificationCount();

                if (window.isAdmin) {
                    debouncedLoadPendingApprovalsCount();
                }
            }, 1000);
        });

        connection.onclose((error) => {
            connectionState = 'disconnected';
            if (suspended) return; // Closed on purpose, resumeConnection() reopens it
            if (document.hidden) {
                waitUntilVisible();
                return;
            }
            console.error('SignalR connection closed:', error);
            noteOutage();

            if (connectionRetryCount < maxRetries) {
                connectionRetryCount++;
                scheduleReconnect(5000);
            } else {
                console.error('Maximum reconnection attempts exceeded');
                clearTimeout(outageNoticeTimer);
                outageNoticeTimer = null;
                showToast(t('Unable to connect to notification service'), 'error');
                // Allow another round of retries after a minute
                setTimeout(() => {
                    connectionRetryCount = 0;
                }, 60000);
            }
        });
    }



    function setupMessageHandlers() {
        // The layout loads the list on page load, but it may be stale after a reconnect
        let connectedBefore = false;

        connection.on("ConnectionEstablished", function (data) {
            connectionState = 'connected';
            connectionRetryCount = 0;

            // The list also reloads the unread count
            if (connectedBefore) {
                setTimeout(() => {
                    window.loadRecentNotifications();
                    if (window.isAdmin && typeof debouncedLoadPendingApprovalsCount === 'function') {
                        debouncedLoadPendingApprovalsCount();
                    }
                }, 500);
            }
            connectedBefore = true;
        });

        connection.on("ReceiveNotification", function (notification) {
            if (isDuplicateNotification(notification)) {
                return;
            }

            trackNotification(notification);

            handleIncomingNotification(notification);
        });

        connection.on("RefreshApprovals", function (data) {
            if (window.isAdmin) {
                if (typeof debouncedLoadPendingApprovalsCount === 'function') {
                    debouncedLoadPendingApprovalsCount();
                } else if (typeof loadPendingApprovalsCount === 'function') {
                    loadPendingApprovalsCount();
                }

            }
        });

        // live-updates.js decides whether the open page cares about a committed change
        connection.on("EntityChanged", function (update) {
            window.dispatchEvent(new CustomEvent('live:changed', { detail: update }));
        });
    }



    function isDuplicateNotification(notification) {
        if (!notification || !notification.id) {
            return false;
        }

        const notificationKey = `${notification.id}-${notification.type}`;
        const now = Date.now();

        if (recentNotifications.has(notificationKey)) {
            const lastSeen = recentNotifications.get(notificationKey);
            if (now - lastSeen < DUPLICATE_CHECK_WINDOW) {
                return true;
            }
        }

        return false;
    }



    function trackNotification(notification) {
        if (!notification || !notification.id) {
            return;
        }

        const notificationKey = `${notification.id}-${notification.type}`;
        const now = Date.now();

        recentNotifications.set(notificationKey, now);

        // Bounded so a tab left open for days does not keep growing the map
        if (recentNotifications.size > 100) {
            const entries = Array.from(recentNotifications.entries());
            entries.sort((a, b) => b[1] - a[1]);

            recentNotifications.clear();
            entries.slice(0, 50).forEach(([key, timestamp]) => {
                recentNotifications.set(key, timestamp);
            });
        }
    }



    // Single owner of the reconnect timer, so two retry chains can never open duplicate connections
    function scheduleReconnect(delay) {
        if (reconnectTimeout) {
            clearTimeout(reconnectTimeout);
        }
        reconnectTimeout = setTimeout(() => {
            reconnectTimeout = null;
            if (document.hidden) waitUntilVisible();
            else startConnection();
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

                if (reconnectTimeout) {
                    clearTimeout(reconnectTimeout);
                    reconnectTimeout = null;
                }
            })
            .catch(err => {
                connectionState = 'disconnected';
                console.error('❌ SignalR connection failed:', err);

                if (document.hidden && !isAuthError(err)) {
                    waitUntilVisible();
                    return;
                }

                // An auth error will not fix itself, so it is not retried
                if (connectionRetryCount < maxRetries && !isAuthError(err)) {
                    connectionRetryCount++;
                    const delay = Math.min(1000 * Math.pow(2, connectionRetryCount), 10000);
                    scheduleReconnect(delay);
                } else if (isAuthError(err)) {
                    console.error('Authentication error, user may need to login');
                    showToast(t('Authentication expired. Please refresh the page.'), 'warning');
                } else {
                    console.error('Failed to establish SignalR connection after maximum retries');
                    showToast(t('Unable to connect to notification service'), 'error');
                }
            });
    }



    function isAuthError(error) {
        const errorMessage = error.message || error.toString();
        return errorMessage.includes('401') ||
            errorMessage.includes('Unauthorized') ||
            errorMessage.includes('authentication') ||
            errorMessage.includes('token');
    }



    function handleIncomingNotification(notification) {
        if (shouldPlaySound()) {
            window.playNotificationSound();
        }

        const toastType = window.getNotificationType(notification.type);
        showToast(`${t(notification.title || '')}: ${t(notification.message || '')}`, toastType);

        window.incrementNotificationCount();

        // Several notifications in a row reload the list once
        clearTimeout(window.notificationListReloadTimeout);
        window.notificationListReloadTimeout = setTimeout(() => {
            window.loadRecentNotifications();
        }, 500);

        handleSpecialNotifications(notification);

    }



    // Only the approvals badge is updated here since the lists refresh through live-updates.js
    function handleSpecialNotifications(notification) {
        if (notification.type === 'ApprovalRequest' && window.isAdmin
            && typeof debouncedLoadPendingApprovalsCount === 'function') {
            debouncedLoadPendingApprovalsCount();
        }
    }

    // Every tab gets the same push, so a timestamp in localStorage keeps it to one sound per 2 seconds
    let lastSoundPlayed = 0;
    function shouldPlaySound() {
        const now = Date.now();
        let shared = 0;
        try { shared = parseInt(localStorage.getItem('notificationSoundAt'), 10) || 0; } catch (e) { }

        if (now - Math.max(lastSoundPlayed, shared) > 2000) {
            lastSoundPlayed = now;
            try { localStorage.setItem('notificationSoundAt', String(now)); } catch (e) { }
            return true;
        }
        return false;
    }




    return {
        initialize: initialize,
        getConnection: () => connection,
        getConnectionState: () => connectionState,
        isConnected: () => connectionState === 'connected',
        reconnect: () => {
            if (connectionState !== 'connected' && connectionState !== 'connecting') {
                connectionRetryCount = 0;
                establishConnection();
            }
        },
        disconnect: () => {
            isInitialized = false;
            if (connection) {
                connection.stop();
            }
            if (reconnectTimeout) {
                clearTimeout(reconnectTimeout);
                reconnectTimeout = null;
            }
        }
    };
})();