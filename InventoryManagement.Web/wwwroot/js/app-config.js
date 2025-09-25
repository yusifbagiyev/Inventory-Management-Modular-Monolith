window.AppConfig = (function () {
    'use strict';

    // Detect the current environment based on the hostname
    const hostname = window.location.hostname;
    const protocol = window.location.protocol;
    const isProduction = hostname.includes('inventory166.az');
    const isDevelopment = !isProduction;

    // Create the base configuration
    const config = {
        environment: isDevelopment ? 'development' : 'production',
        hostname: hostname,
        protocol: protocol,

        // API endpoints configuration
        api: {
            // In production, everything goes through relative paths
            baseUrl: isDevelopment ? 'http://localhost:5000' : '',
            gateway: isDevelopment ? 'http://localhost:5000' : '/api',
        },

        // SignalR hub configuration
        signalR: {
            notificationHub: isDevelopment
                ? 'http://localhost:5005/notificationHub'  // Direct to NotificationService
                : '/notificationHub',  // Through reverse proxy in production

            // Connection options
            options: {
                skipNegotiation: false,
                transport: signalR.HttpTransportType.WebSockets |
                    signalR.HttpTransportType.ServerSentEvents |
                    signalR.HttpTransportType.LongPolling,
                withCredentials: true
            }
        },

        // Image URLs
        images: {
            products: isDevelopment
                ? 'http://localhost:5001/images/products'
                : '/images/products',
            routes: isDevelopment
                ? 'http://localhost:5002/images/routes'
                : '/images/routes'
        },
    };

    // Helper function to build API URLs
    config.buildApiUrl = function (endpoint) {
        // Remove leading slash if present
        endpoint = endpoint.replace(/^\//, '');

        // In production, use relative URLs for proxy
        if (isProduction) {
            return `/api/${endpoint}`;
        }

        // In development, use full URLs
        return `${this.api.gateway}/api/${endpoint}`;
    };

    config.tokenManagement = {
        refreshInProgress: false,
        refreshPromise: null,

        // Centralized token refresh endpoint
        refreshToken: async function () {
            // If refresh is already in progress, return the existing promise
            if (this.refreshInProgress && this.refreshPromise) {
                console.log('Token refresh already in progress, waiting...');
                return this.refreshPromise;
            }

            this.refreshInProgress = true;

            // Create the refresh promise
            this.refreshPromise = new Promise(async (resolve, reject) => {
                try {
                    const response = await fetch('/Account/RefreshToken', {
                        method: 'POST',
                        headers: {
                            'X-Requested-With': 'XMLHttpRequest'
                        }
                    });
                    if (response.ok) {
                        const data = await response.json();
                        if (data.success && data.token) {
                            //Update the token in the page
                            $('#jwtToken').val(data.token);
                            console.log('Token refreshed succesfully');
                            resolve(data.token);
                        } else {
                            reject(new Error('Token refresh failed'));
                        }
                    } else {
                        reject(new Error('Token refresh request failed'));
                    }
                } catch (error) {
                    reject(error);
                } finally {
                    this.refreshInProgress = false;
                    this.refreshPromise = null;
                }
            });
            return this.refreshPromise;
        },

        // Get current token with auto-refresh
        getValidToken: async function () {
            const currentToken = $('#jwtToken').val();

            if (!currentToken) {
                throw new Error('No token available');
            }

            // Check if token is expired or about to expire (within 2 minutes)
            try {
                const tokenParts = currentToken.split('.');
                if (tokenParts.length === 3) {
                    const payload = JSON.parse(atob(tokenParts[1]));
                    const exp = payload.exp * 1000; // Convert to milliseconds
                    const now = Date.now();
                    const timeUntilExpiry = exp - now;

                    // If token expires in less than 2 minutes, refresh it
                    if (timeUntilExpiry < 120000) {
                        console.log('Token expiring soon, refreshing...');
                        return await this.refreshToken();
                    }
                }
            } catch (e) {
                console.error('Error checking token expiry:', e);
            }

            return currentToken;
        }
    };

    return config;
})();