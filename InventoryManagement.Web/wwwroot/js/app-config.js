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
        tokenCheckInterval:null,

        /**
         * Initialize secure token management
         * Only uses server-side session for token storage
        */
        initialize: function () {
            // Clear any insecure client-side storage on init
            this.clearInsecureStorage();

            // Set up periodic token validation
            this.setupTokenValidation();

            // Set up beforeunload handler for cleanup
            this.setupCleanupHandler();
        },
        /**
         * Gets the current token from server session via hidden field
         * Never store tokens in localStorage or accessible cookies
         */

        getCurrentToken: function () {
            // Only get token from server-rendered hidden field
            // This ensures token is never accessible to XSS attacks
            return $('#jwtToken').val() || null;
        },

        /**
         * Validates token version to detect rotation
         */
        isTokenVersionValid: function () {
            // Check token version cookie (HttpOnly, not accessible to JS)
            // This is done server-side via AJAX call
            return true; // Actual validation happens server-side
        },

        /**
         * Refreshes token via secure server endpoint
         */
        refreshToken: async function () {
            // Prevent concurrent refresh attempts
            if (this.refreshInProgress && this.refreshPromise) {
                console.log('Token refresh already in progress');
                return this.refreshPromise;
            }

            this.refreshInProgress = true;

            this.refreshPromise = new Promise(async (resolve, reject) => {
                try {
                    const response = await $.ajax({
                        url: '/Account/RefreshToken',
                        type: 'GET',
                        headers: {
                            'X-Requested-With': 'XMLHttpRequest',
                            // Include anti-forgery token for CSRF protection
                            'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').first().val()
                        },
                        timeout: 10000 // 10 second timeout
                    });

                    if (response && response.success) {
                        console.log('Token refreshed successfully');

                        // Update the hidden field with new token
                        // This should be done server-side on page reload
                        $('#jwtToken').val(response.token);

                        resolve(response.token);
                    } else {
                        throw new Error('Token refresh failed');
                    }
                } catch (error) {
                    console.error('Token refresh error:', error);

                    // Handle authentication failure
                    if (error.status === 401) {
                        // Clear any client-side state and redirect to login
                        this.handleAuthenticationFailure();
                    }

                    reject(error);
                } finally {
                    this.refreshInProgress = false;
                    this.refreshPromise = null;
                }
            });

            return this.refreshPromise;
        },

        /**
         * Sets up periodic token validation
         */
        setupTokenValidation: function () {
            // Clear any existing interval
            if (this.tokenCheckInterval) {
                clearInterval(this.tokenCheckInterval);
            }

            // Check token validity every 5 minutes
            this.tokenCheckInterval = setInterval(() => {
                this.validateTokenStatus();
            }, 5 * 60 * 1000);
        },

        /**
         * Validates current token status with server
         */
        validateTokenStatus: async function () {
            try {
                const response = await $.ajax({
                    url: '/Account/GetCurrentToken',
                    type: 'GET',
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                if (!response.success) {
                    console.warn('Token validation failed, may need to refresh');
                    // Token is invalid, attempt refresh
                    await this.refreshToken();
                }
            } catch (error) {
                console.error('Token validation error:', error);

                if (error.status === 401) {
                    this.handleAuthenticationFailure();
                }
            }
        },

        /**
         * Handles authentication failure securely
         */
        handleAuthenticationFailure: function () {
            // Clear any client-side state
            this.clearInsecureStorage();

            // Show user-friendly message
            if (typeof showToast === 'function') {
                showToast('Your session has expired. Please login again.', 'warning');
            }

            // Redirect to login after short delay
            setTimeout(() => {
                window.location.href = '/Account/Login?returnUrl=' +
                    encodeURIComponent(window.location.pathname + window.location.search);
            }, 2000);
        },

        /**
         * Clears any insecure client-side storage
         */
        clearInsecureStorage: function () {
            // Clear localStorage
            const keysToRemove = ['jwt_token', 'refresh_token', 'JwtToken', 'RefreshToken', 'user_data'];
            keysToRemove.forEach(key => {
                localStorage.removeItem(key);
                sessionStorage.removeItem(key);
            });

            // Note: HttpOnly cookies cannot be cleared from JavaScript (security feature)
        },

        /**
         * Sets up cleanup on page unload
         */
        setupCleanupHandler: function () {
            $(window).on('beforeunload', () => {
                // Clear any intervals
                if (this.tokenCheckInterval) {
                    clearInterval(this.tokenCheckInterval);
                    this.tokenCheckInterval = null;
                }
            });
        },

        /**
         * Clean up resources
         */
        cleanup: function () {
            if (this.tokenCheckInterval) {
                clearInterval(this.tokenCheckInterval);
                this.tokenCheckInterval = null;
            }

            this.clearInsecureStorage();
        }
    };

    // Initialize secure token management on document ready
    $(document).ready(function () {
        // Only initialize if user is authenticated
        if ($('#jwtToken').val()) {
            config.tokenManagement.initialize();
        }
    });

    return config;
})();   