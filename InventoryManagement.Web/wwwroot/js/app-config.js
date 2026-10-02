// UI and API share one host, so the cookie covers API calls but writes also need antiforgeryHeaders()

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
