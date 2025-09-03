// Global configuration that adapts to the current environment
// This file centralizes all environment-specific settings

window.AppConfig = (function () {
    'use strict';

    // Detect the current environment based on the hostname
    const hostname = window.location.hostname;
    const protocol = window.location.protocol;
    const isLocalhost = hostname === 'localhost' || hostname === '127.0.0.1';
    const isDevelopment = isLocalhost;
    const isProduction = !isDevelopment;

    // Create the base configuration
    const config = {
        environment: isDevelopment ? 'development' : 'production',
        hostname: hostname,
        protocol: protocol,

        // API endpoints configuration
        api: {
            // In production, everything goes through nginx proxy
            // In development, we connect directly to services
            baseUrl: isDevelopment ? 'http://localhost:5000' : '',

            // Individual service endpoints
            gateway: isDevelopment ? 'http://localhost:5000' : '/api',
            identity: isDevelopment ? 'http://localhost:5003' : '/api/auth',
            product: isDevelopment ? 'http://localhost:5001' : '/api/products',
            route: isDevelopment ? 'http://localhost:5002' : '/api/inventoryroutes',
            approval: isDevelopment ? 'http://localhost:5004' : '/api/approvalrequests',
            notification: isDevelopment ? 'http://localhost:5005' : '/api/notifications'
        },

        // SignalR hub configuration
        signalR: {
            notificationHub: isDevelopment
                ? 'http://localhost:5005/notificationHub'
                : '/notificationHub',

            // Connection options
            options: {
                skipNegotiation: isProduction,
                transport: isProduction
                    ? signalR.HttpTransportType.WebSockets
                    : undefined
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

        // Timeouts and retry configuration
        timeouts: {
            ajax: 30000,  // 30 seconds
            signalR: 60000  // 60 seconds
        },

        retries: {
            max: 3,
            delay: 1000  // 1 second
        }
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

    // Helper function to get service-specific URL
    config.getServiceUrl = function (service, endpoint) {
        const serviceUrls = {
            'identity': this.api.identity,
            'product': this.api.product,
            'route': this.api.route,
            'approval': this.api.approval,
            'notification': this.api.notification
        };

        const baseUrl = serviceUrls[service] || this.api.gateway;
        endpoint = endpoint.replace(/^\//, '');

        return `${baseUrl}/${endpoint}`;
    };

    // Log configuration in development
    if (isDevelopment) {
        console.log('App Configuration:', config);
    }

    return config;
})();