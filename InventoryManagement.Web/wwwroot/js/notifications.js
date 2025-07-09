let notificationConnection = null;

function initializeNotifications() {
    // Load initial notification count
    loadNotificationCount();

    // Set up notification dropdown
    $('#notificationIcon').click(function (e) {
        e.preventDefault();
        e.stopPropagation();
        loadRecentNotifications();
    });
}

function connectToNotificationHub() {
    // Get the JWT token from session
    const token = $('#jwtToken').val() || '';

    // Use the API Gateway URL for SignalR
    notificationConnection = new signalR.HubConnectionBuilder()
        .withUrl("http://localhost:5000/notificationHub", {
            accessTokenFactory: () => token,
            transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents | signalR.HttpTransportType.LongPolling
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build();

    notificationConnection.on("ReceiveNotification", function (notification) {
        console.log('Notification received:', notification);
        // Update notification count
        loadNotificationCount();

        // Show toast notification
        showToast(`${notification.title}: ${notification.message}`, 'info');
    });

    notificationConnection.onclose(error => {
        console.error('SignalR connection closed:', error);
    });

    notificationConnection.start()
        .then(() => {
            console.log('Connected to notification hub');
        })
        .catch(err => {
            console.error('Error connecting to notification hub:', err);
            // Retry connection after 5 seconds
            setTimeout(() => connectToNotificationHub(), 5000);
        });
}

function loadNotificationCount() {
    $.ajax({
        url: '/Notifications/GetUnreadCount',
        type: 'GET',
        success: function (count) {
            if (count > 0) {
                $('#notificationBadge').text(count).show();
            } else {
                $('#notificationBadge').hide();
            }
        },
        error: function (xhr, status, error) {
            console.error('Error loading notification count:', error);
        }
    });
}

function loadRecentNotifications() {
    $('#notificationDropdown').html('<div class="text-center py-3"><div class="spinner-border spinner-border-sm" role="status"></div></div>');

    $.ajax({
        url: '/Notifications/GetRecentNotifications',
        type: 'GET',
        success: function (notifications) {
            let html = '';

            if (!notifications || notifications.length === 0) {
                html = '<div class="dropdown-item text-center py-3 text-muted">No new notifications</div>';
            } else {
                notifications.forEach(function (n) {
                    html += `
                        <a class="dropdown-item notification-item ${n.isRead ? '' : 'unread'}" 
                           href="#" onclick="markAsRead(${n.id}); return false;">
                            <div class="d-flex">
                                <div class="flex-grow-1">
                                    <h6 class="mb-1">${n.title}</h6>
                                    <p class="mb-0 small">${n.message}</p>
                                    <small class="text-muted">${formatDate(n.createdAt)}</small>
                                </div>
                            </div>
                        </a>
                    `;
                });
                html += '<div class="dropdown-divider"></div>';
                html += '<a class="dropdown-item text-center" href="/Notifications">View All</a>';
            }

            $('#notificationDropdown').html(html);
        },
        error: function (xhr, status, error) {
            console.error('Error loading notifications:', error);
            $('#notificationDropdown').html('<div class="dropdown-item text-center py-3 text-danger">Error loading notifications</div>');
        }
    });
}

function markAsRead(notificationId) {
    $.post('/Notifications/MarkAsRead', { notificationId: notificationId }, function () {
        loadNotificationCount();
        loadRecentNotifications();
    });
}

function formatDate(dateString) {
    const date = new Date(dateString);
    const now = new Date();
    const diff = Math.floor((now - date) / 1000); // seconds

    if (diff < 60) return 'just now';
    if (diff < 3600) return Math.floor(diff / 60) + ' minutes ago';
    if (diff < 86400) return Math.floor(diff / 3600) + ' hours ago';
    return date.toLocaleDateString();
}