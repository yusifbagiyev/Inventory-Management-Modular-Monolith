// InventoryManagement.Web/wwwroot/js/app-config.js
//
// The UI and the API are served by the same host, so every URL is same-origin and the browser's
// auth cookie authenticates API calls. Unsafe API calls (POST/PUT/DELETE) must send the
// antiforgery token - use AppConfig.antiforgeryHeaders().

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

    /** Header carrying the page's antiforgery token, required for state-changing requests. */
    config.antiforgeryHeaders = function () {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        return token ? { 'RequestVerificationToken': token } : {};
    };

    return config;
})();
