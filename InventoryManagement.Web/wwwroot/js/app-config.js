// Application Configuration Module
window.AppConfig = (function () {
    'use strict';

    // Detect environment based on hostname
    const hostname = window.location.hostname;
    const isProduction = hostname.includes('inventory166.az') ||
        hostname === '10.0.7.39';

    const config = {
        environment: isProduction ? 'production' : 'development',

        // API configuration - simplified for proxy usage
        api: {
            baseUrl: isProduction ? '' : 'http://localhost:5000',
            timeout: 30000
        },

        // SignalR configuration
        signalR: {
            hubUrl: '/notificationHub',
            reconnectInterval: 5000,
            maxReconnectAttempts: 10
        },

        // Build API URL helper
        buildApiUrl: function (endpoint) {
            // Remove leading slash if present
            endpoint = endpoint.replace(/^\//, '');

            // In production, everything goes through the proxy
            if (isProduction) {
                return `/api/${endpoint}`;
            }

            // In development, use the base URL
            return `${this.api.baseUrl}/api/${endpoint}`;
        },

        // Get JWT token from storage
        getToken: function () {
            return sessionStorage.getItem('JwtToken') ||
                document.querySelector('#jwtToken')?.value ||
                this.getCookie('jwt_token');
        },

        // Cookie helper
        getCookie: function (name) {
            const value = `; ${document.cookie}`;
            const parts = value.split(`; ${name}=`);
            if (parts.length === 2) {
                return parts.pop().split(';').shift();
            }
            return null;
        }
    };

    return config;
})();