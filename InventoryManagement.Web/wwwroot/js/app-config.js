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
            baseUrl: isDevelopment ? 'http://localhost:5000' : '',
            gateway: isDevelopment ? 'http://localhost:5000' : '/api',
        },

        // SignalR hub configuration
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

    // Token management functionality
    config.tokenManagement = {
        refreshInProgress: false,
        refreshPromise: null,

        // Get current token from various sources
        getCurrentToken: function () {
            return $('#jwtToken').val() ||
                sessionStorage.getItem('JwtToken') ||
                localStorage.getItem('jwt_token') ||
                getCookie('jwt_token');
        },

        // Get refresh token
        getRefreshToken: function () {
            return sessionStorage.getItem('RefreshToken') ||
                localStorage.getItem('refresh_token') ||
                getCookie('refresh_token');
        },

        // Check if token is expired
        isTokenExpired: function (token) {
            if (!token) return true;

            try {
                const parts = token.split('.');
                if (parts.length !== 3) return true;

                const payload = JSON.parse(atob(parts[1]));
                const exp = payload.exp * 1000; // Convert to milliseconds
                const now = Date.now();

                // Consider expired if less than 2 minutes remaining
                return (exp - now) < 120000;
            } catch (e) {
                console.error('Error checking token expiry:', e);
                return true;
            }
        },

        // Refresh the token
        refreshToken: async function () {
            // If refresh is already in progress, return the existing promise
            if (this.refreshInProgress && this.refreshPromise) {
                console.log('Token refresh already in progress, waiting...');
                return this.refreshPromise;
            }

            this.refreshInProgress = true;

            this.refreshPromise = new Promise(async (resolve, reject) => {
                try {
                    const currentToken = this.getCurrentToken();
                    const refreshToken = this.getRefreshToken();

                    if (!currentToken || !refreshToken) {
                        throw new Error('Missing tokens for refresh');
                    }

                    const response = await $.ajax({
                        url: '/Account/RefreshToken',
                        type: 'GET',
                        headers: {
                            'X-Requested-With': 'XMLHttpRequest'
                        }
                    });

                    if (response && response.success && response.token) {
                        // Update the token in various places
                        $('#jwtToken').val(response.token);
                        sessionStorage.setItem('JwtToken', response.token);

                        if (response.refreshToken) {
                            sessionStorage.setItem('RefreshToken', response.refreshToken);
                        }

                        console.log('Token refreshed successfully');
                        resolve(response.token);
                    } else {
                        throw new Error('Token refresh failed - invalid response');
                    }
                } catch (error) {
                    console.error('Token refresh error:', error);

                    // If refresh fails, redirect to login
                    if (error.status === 401 || error.responseJSON?.message === 'No tokens available') {
                        setTimeout(() => {
                            window.location.href = '/Account/Login';
                        }, 1000);
                    }

                    reject(error);
                } finally {
                    this.refreshInProgress = false;
                    this.refreshPromise = null;
                }
            });

            return this.refreshPromise;
        },

        // Get a valid token (refresh if needed)
        getValidToken: async function () {
            const currentToken = this.getCurrentToken();

            if (!currentToken) {
                console.warn('No token available');
                throw new Error('No authentication token available');
            }

            // Check if token is expired or about to expire
            if (this.isTokenExpired(currentToken)) {
                console.log('Token expired or expiring soon, refreshing...');
                try {
                    return await this.refreshToken();
                } catch (error) {
                    console.error('Failed to refresh token:', error);
                    throw error;
                }
            }

            return currentToken;
        },

        // Setup automatic token refresh
        setupAutoRefresh: function () {
            // Clear any existing interval
            if (window.tokenRefreshInterval) {
                clearInterval(window.tokenRefreshInterval);
            }

            // Check token every minute
            window.tokenRefreshInterval = setInterval(async () => {
                try {
                    const token = this.getCurrentToken();
                    if (token && this.isTokenExpired(token)) {
                        console.log('Auto-refreshing expired token...');
                        await this.refreshToken();
                    }
                } catch (error) {
                    console.error('Auto-refresh failed:', error);
                }
            }, 60000); // Check every minute
        },

        // Clean up
        cleanup: function () {
            if (window.tokenRefreshInterval) {
                clearInterval(window.tokenRefreshInterval);
                window.tokenRefreshInterval = null;
            }
        }
    };

    // Helper function to get cookie value
    function getCookie(name) {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
    }

    // Initialize auto-refresh on load if user is authenticated
    $(document).ready(function () {
        if ($('#jwtToken').val()) {
            config.tokenManagement.setupAutoRefresh();
        }
    });

    // Clean up on page unload
    $(window).on('beforeunload', function () {
        config.tokenManagement.cleanup();
    });

    return config;
})();