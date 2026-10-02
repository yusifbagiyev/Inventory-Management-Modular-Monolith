// UI and API share one host, so the auth cookie covers API calls.
// POST, PUT and DELETE calls must also send AppConfig.antiforgeryHeaders().

window.AppConfig = (function () {
    'use strict';

    const config = {
        api: {
            gateway: '/api'
        },
        signalR: {
            notificationHub: '/notificationHub'
        }
    };

    config.buildApiUrl = function (endpoint) {
        return `/api/${endpoint.replace(/^\//, '')}`;
    };

    /** Antiforgery header for requests that change state. */
    config.antiforgeryHeaders = function () {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        return token ? { 'RequestVerificationToken': token } : {};
    };

    return config;
})();
