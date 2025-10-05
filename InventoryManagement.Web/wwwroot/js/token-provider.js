/**
 * Secure Token Provider
 * Fetches tokens from server only when needed
 * Never stores tokens in DOM or localStorage
 * Tokens live only in memory and are refreshed as needed
 */
window.SecureTokenProvider = (function () {
    'use strict';

    let cachedToken = null;
    let tokenExpiryTime = null;
    let fetchPromise = null; // Prevent multiple simultaneous fetches

    /**
     * Gets a valid token from the server
     * Caches the token in memory for 5 minutes
     * Automatically refetches when cache expires
     */
    async function getToken() {
        // If we have a cached token that hasn't expired, use it
        if (cachedToken && tokenExpiryTime && Date.now() < tokenExpiryTime) {
            console.log('Using cached token');
            return cachedToken;
        }

        // If a fetch is already in progress, wait for it
        if (fetchPromise) {
            console.log('Token fetch already in progress, waiting...');
            return await fetchPromise;
        }

        // Start a new fetch
        console.log('Fetching fresh token from server...');
        fetchPromise = fetchTokenFromServer();

        try {
            const token = await fetchPromise;
            return token;
        } finally {
            fetchPromise = null; // Clear the promise
        }
    }

    /**
     * Fetches token from server endpoint
     * @private
     */
    async function fetchTokenFromServer() {
        try {
            const response = await fetch('/api/token/current', {
                method: 'GET',
                credentials: 'same-origin', // Send cookies
                headers: {
                    'Accept': 'application/json'
                }
            });

            if (!response.ok) {
                if (response.status === 401) {
                    console.error('Token fetch failed: Unauthorized');
                    // User needs to login
                    window.location.href = '/Account/Login';
                    throw new Error('Unauthorized');
                }
                throw new Error(`Token fetch failed with status ${response.status}`);
            }

            const data = await response.json();

            if (!data.token) {
                throw new Error('No token in response');
            }

            // Cache for 2 minutes instead of 5
            // This ensures we get fresh tokens more frequently without overwhelming the server
            cachedToken = data.token;
            tokenExpiryTime = Date.now() + (2 * 60 * 1000); // 2 minutes

            console.log('Token fetched and cached successfully');
            return cachedToken;

        } catch (error) {
            console.error('Error fetching token:', error);
            cachedToken = null;
            tokenExpiryTime = null;
            throw error;
        }
    }

    /**
     * Clears the cached token
     * Call this when user logs out or token becomes invalid
     */
    function clearToken() {
        console.log('Clearing cached token');
        cachedToken = null;
        tokenExpiryTime = null;
    }

    /**
     * Forces a fresh token fetch, bypassing cache
     */
    async function refreshToken() {
        console.log('Forcing token refresh');
        clearToken();
        return await getToken();
    }

    /**
     * Checks if user is authenticated
     */
    async function isAuthenticated() {
        try {
            const response = await fetch('/api/token/validate', {
                method: 'GET',
                credentials: 'same-origin'
            });

            if (!response.ok) {
                return false;
            }

            const data = await response.json();
            return data.isValid === true;
        } catch (error) {
            console.error('Error checking authentication:', error);
            return false;
        }
    }

    // Public API
    return {
        getToken: getToken,
        clearToken: clearToken,
        refreshToken: refreshToken,
        isAuthenticated: isAuthenticated
    };
})();