// Global configuration that adapts to the current environment
// This file centralizes all environment-specific settings

window.AppConfig = (function () {
    'use strict';

    // Detect the current environment based on the hostname
    const hostname = window.location.hostname;
    const protocol = window.location.protocol;
    const isProduction = hostname.includes('inventory166.az') || hostname.includes('www.inventory166.az');
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

    return config;
})();