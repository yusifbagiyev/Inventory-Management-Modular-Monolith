// InventoryManagement.Web/wwwroot/js/app-config.js

window.AppConfig = (function () {
    'use strict';

    const hostname = window.location.hostname;
    const protocol = window.location.protocol;
    const isProduction = hostname.includes('inventory166.az');
    const isDevelopment = !isProduction;

    const config = {
        environment: isDevelopment ? 'development' : 'production',
        hostname: hostname,
        protocol: protocol,

        api: {
            baseUrl: isDevelopment ? 'http://localhost:5000' : '',
            gateway: isDevelopment ? 'http://localhost:5000' : '/api',
        },

        signalR: {
            notificationHub: isDevelopment
                ? 'http://localhost:5005/notificationHub'
                : '/notificationHub',
            options: {
                skipNegotiation: false,
                transport: typeof signalR !== 'undefined' ?
                    (signalR.HttpTransportType.WebSockets |
                        signalR.HttpTransportType.ServerSentEvents |
                        signalR.HttpTransportType.LongPolling) : 1,
                withCredentials: true
            }
        },

        images: {
            products: isDevelopment
                ? 'http://localhost:5001/images/products'
                : '/images/products',
            routes: isDevelopment
                ? 'http://localhost:5002/images/routes'
                : '/images/routes'
        },
    };

    config.buildApiUrl = function (endpoint) {
        endpoint = endpoint.replace(/^\//, '');
        if (isProduction) {
            return `/api/${endpoint}`;
        }
        return `${this.api.gateway}/api/${endpoint}`;
    };

    // SECURITY: Token management now works through server-side sessions
    // JavaScript no longer has direct access to tokens
    config.tokenManagement = {
        // Get current token is now server-side only
        // JavaScript can only request a refresh through an endpoint
        getCurrentToken: function () {
            return $('#jwtToken').val(); // This is set by server in hidden field
        },

        // Check if we should refresh (based on user activity)
        shouldRefresh: async function () {
            // Tokens are managed server-side
            // Just ping the server to check status
            try {
                const response = await fetch('/Account/GetCurrentToken', {
                    method: 'GET',
                    credentials: 'same-origin'
                });

                if (response.ok) {
                    const data = await response.json();
                    return data.success;
                }
                return false;
            } catch (error) {
                console.error('Error checking token status:', error);
                return false;
            }
        },

        // Setup periodic health check instead of managing tokens
        setupHealthCheck: function () {
            if (window.healthCheckInterval) {
                clearInterval(window.healthCheckInterval);
            }

            // Check every 5 minutes if session is still valid
            window.healthCheckInterval = setInterval(async () => {
                try {
                    const isValid = await this.shouldRefresh();
                    if (!isValid) {
                        console.log('Session invalid, redirecting to login');
                        window.location.href = '/Account/Login';
                    }
                } catch (error) {
                    console.error('Health check failed:', error);
                }
            }, 300000); // 5 minutes
        },

        cleanup: function () {
            if (window.healthCheckInterval) {
                clearInterval(window.healthCheckInterval);
                window.healthCheckInterval = null;
            }
        }
    };

    // Initialize health check if user is authenticated
    $(document).ready(function () {
        if ($('#jwtToken').val()) {
            config.tokenManagement.setupHealthCheck();
        }
    });

    $(window).on('beforeunload', function () {
        config.tokenManagement.cleanup();
    });

    return config;
})();