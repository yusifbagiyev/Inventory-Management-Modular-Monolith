// Changes carry no record data, so open pages re-fetch their regions with the viewer's own permissions

window.LiveUpdates = (function () {
    'use strict';

    const DEBOUNCE_MS = 700;
    const BUSY_RETRY_MS = 2000;
    const OFFLINE_RETRY_MS = 10000;
    const OWN_SAVE_WINDOW_MS = 10000;
    const watchers = [];

    // The markup the server last sent for each region, so a change that leaves a region as it was does not redraw it
    const served = new Map();

    // The page as it arrived, taken before other scripts decorate it, since the regions are compared with server markup
    const arrived = document.body ? document.body.cloneNode(true) : null;

    // Antiforgery tokens differ on every response without the page changing
    function markup(node) {
        const copy = node.cloneNode(true);
        copy.querySelectorAll('input[name="__RequestVerificationToken"]').forEach(input => input.removeAttribute('value'));
        return copy.outerHTML;
    }

    function remember(doc) {
        regions().forEach(selector => {
            const node = doc.querySelector(selector);
            if (node) served.set(selector, markup(node));
        });
    }

    // This tab's own submit comes back as a change too, while saves from another tab still warn
    let ownSaveUntil = 0;
    document.addEventListener('submit', () => { ownSaveUntil = Date.now() + OWN_SAVE_WINDOW_MS; }, true);

    // Warn mode is for edit forms and only shows a notice, never touching what was typed
    function watch(options) {
        const w = Object.assign({ mode: 'refresh', ids: {}, regions: [], entities: [] }, options, {
            pending: null,
            timer: null
        });
        watchers.push(w);
        if (w.mode !== 'warn' && arrived) w.regions.forEach(selector => {
            const node = arrived.querySelector(selector);
            if (node && !served.has(selector)) served.set(selector, markup(node));
        });
    }

    // Prefers this record's deletion, then its change, then any match, and a null id means many records
    function relevantChange(w, update) {
        const matching = ((update && update.changes) || []).filter(c =>
            w.entities.includes(c.entity) &&
            (w.ids[c.entity] == null || c.id == null || c.id === w.ids[c.entity]));
        const ownRecord = c => w.ids[c.entity] != null && c.id === w.ids[c.entity];
        return matching.find(c => ownRecord(c) && c.action === 'deleted')
            || matching.find(ownRecord)
            || matching[0]
            || null;
    }

    // Updates in one debounce window are merged so a deletion is not lost behind a later change
    function merge(pending, update) {
        if (!pending) return update;
        return Object.assign({}, update, { changes: (pending.changes || []).concat(update.changes || []) });
    }

    window.addEventListener('live:changed', e => {
        const update = e.detail || {};
        watchers.forEach(w => {
            if (!relevantChange(w, update)) return;
            if (w.mode === 'warn') {
                if (Date.now() < ownSaveUntil) return;
                showNotice(describe(w, update) + ' ' + (isDeletion(w, update)
                    ? t('Saving is no longer possible.')
                    : t('Please reload the page to see the latest version; saving now may overwrite it.')));
            } else {
                schedule(w, update);
            }
        });
    });

    // After a reconnect the page may have missed changes, so every region refreshes once
    window.addEventListener('live:resync', () => {
        watchers.filter(w => w.mode === 'refresh').forEach(w => schedule(w, {}));
    });

    function schedule(w, update) {
        w.pending = merge(w.pending, update);
        w.receivedAt = Date.now();
        clearTimeout(w.timer);
        w.timer = setTimeout(() => tryRefresh(w), DEBOUNCE_MS);
    }

    function isBusy(w) {
        if (document.hidden) return true;
        if (window.ListNav && ListNav.busy()) return true;
        if (document.querySelector('.modal.show, .dropdown-menu.show')) return true;

        const active = document.activeElement;
        if (active && active.matches('input, textarea, select, [contenteditable="true"]')
            && w.regions.some(selector => document.querySelector(selector)?.contains(active))) {
            return true;
        }

        if (typeof w.isEditing === 'function' && w.isEditing()) return true;

        const selection = window.getSelection ? window.getSelection().toString() : '';
        return selection.length > 0;
    }

    function tryRefresh(w) {
        if (!w.pending) return;
        if (isBusy(w)) {
            w.timer = setTimeout(() => tryRefresh(w), BUSY_RETRY_MS);
            return;
        }
        refresh(w);
    }

    async function refresh(w) {
        const update = w.pending;
        w.pending = null;
        // A list load requested after the last change arrived already shows it, while a later change still refreshes
        if (window.ListNav && ListNav.loadedSince(w.receivedAt)) return;

        let doc;
        try {
            const response = await fetch(window.location.href, {
                credentials: 'same-origin',
                cache: 'no-store',
                headers: { 'Accept': 'text/html' }
            });
            // A redirect or refusal means the record is gone or the session ended, so keep the screen
            if (!response.ok || new URL(response.url).pathname !== window.location.pathname) {
                showNotice(describe(w, update) || t('This page is out of date.'));
                return;
            }
            doc = new DOMParser().parseFromString(await response.text(), 'text/html');
        } catch {
            // Probably offline for a moment, so retry later with whatever arrived meanwhile
            w.pending = merge(update, w.pending || { changes: [] });
            w.timer = setTimeout(() => tryRefresh(w), OFFLINE_RETRY_MS);
            return;
        }

        // A missing region means the record is gone or access was lost, so offer a reload instead
        const pairs = w.regions.map(selector => [document.querySelector(selector), doc.querySelector(selector)]);
        if (pairs.some(([current, fresh]) => current && !fresh)) {
            showNotice(describe(w, update) || t('This page is out of date.'));
            return;
        }

        // Only regions whose markup changed are redrawn, and a change that touches none of them redraws nothing
        const changed = pairs.filter(([current, fresh], i) => {
            if (!current || !fresh) return false;
            const html = markup(fresh);
            if (served.get(w.regions[i]) === html) return false;
            served.set(w.regions[i], html);
            return true;
        });
        if (!changed.length) return;

        if (typeof w.beforeRefresh === 'function') w.beforeRefresh();
        changed.forEach(([current, fresh]) => {
            const node = document.importNode(fresh, true);
            node.setAttribute('data-quiet', '');   // Rows skip their entrance animation on a live refresh
            replaceKeepingFocus(current, node);
            node.classList.add('live-refreshed');
            setTimeout(() => node.classList.remove('live-refreshed'), 1600);
        });
        if (typeof w.afterRefresh === 'function') w.afterRefresh();
        // Lets shared decorations, like the breadcrumb Back button, be added to the new markup
        document.dispatchEvent(new CustomEvent('live:refreshed'));
    }

    function isDeletion(w, update) {
        const change = relevantChange(w, update);
        return !!change && change.action === 'deleted' && change.id != null;
    }

    // Whole sentences per record kind because a spliced noun translates badly, ordered plain, actor, time, both
    const NOTICES = {
        product: {
            changed: ['This product was changed.', 'This product was changed by {0}.', 'This product was changed at {1}.', 'This product was changed by {0} at {1}.'],
            deleted: ['This product was deleted.', 'This product was deleted by {0}.', 'This product was deleted at {1}.', 'This product was deleted by {0} at {1}.']
        },
        category: {
            changed: ['This category was changed.', 'This category was changed by {0}.', 'This category was changed at {1}.', 'This category was changed by {0} at {1}.'],
            deleted: ['This category was deleted.', 'This category was deleted by {0}.', 'This category was deleted at {1}.', 'This category was deleted by {0} at {1}.']
        },
        department: {
            changed: ['This department was changed.', 'This department was changed by {0}.', 'This department was changed at {1}.', 'This department was changed by {0} at {1}.'],
            deleted: ['This department was deleted.', 'This department was deleted by {0}.', 'This department was deleted at {1}.', 'This department was deleted by {0} at {1}.']
        },
        route: {
            changed: ['This route was changed.', 'This route was changed by {0}.', 'This route was changed at {1}.', 'This route was changed by {0} at {1}.'],
            deleted: ['This route was deleted.', 'This route was deleted by {0}.', 'This route was deleted at {1}.', 'This route was deleted by {0} at {1}.']
        },
        user: {
            changed: ['This user was changed.', 'This user was changed by {0}.', 'This user was changed at {1}.', 'This user was changed by {0} at {1}.'],
            deleted: ['This user was deleted.', 'This user was deleted by {0}.', 'This user was deleted at {1}.', 'This user was deleted by {0} at {1}.']
        },
        approval: {
            changed: ['This request was changed.', 'This request was changed by {0}.', 'This request was changed at {1}.', 'This request was changed by {0} at {1}.'],
            deleted: ['This request was deleted.', 'This request was deleted by {0}.', 'This request was deleted at {1}.', 'This request was deleted by {0} at {1}.']
        },
        page: {
            changed: ['A record on this page was changed.', 'A record on this page was changed by {0}.', 'A record on this page was changed at {1}.', 'A record on this page was changed by {0} at {1}.'],
            deleted: ['A record on this page was deleted.', 'A record on this page was deleted by {0}.', 'A record on this page was deleted at {1}.', 'A record on this page was deleted by {0} at {1}.']
        }
    };

    function describe(w, update) {
        const change = relevantChange(w, update);
        if (!change) return '';
        const set = NOTICES[w.label || change.entity] || NOTICES.page;
        const sentences = change.action === 'deleted' ? set.deleted : set.changed;
        const at = update.at
            ? new Date(update.at).toLocaleTimeString(uiLocale(), { hour: '2-digit', minute: '2-digit' })
            : '';
        const index = (update.actorName ? 1 : 0) + (at ? 2 : 0);
        return t(sentences[index], update.actorName || '', at);
    }

    // One notice at the top that later messages replace, set as text because the actor's name is user data
    function showNotice(text) {
        let notice = document.getElementById('liveUpdateNotice');
        if (!notice) {
            notice = document.createElement('div');
            notice.id = 'liveUpdateNotice';
            // Not a Bootstrap alert, because site.js auto-closes alerts shortly after load
            notice.className = 'ip-banner ip-banner-warning justify-content-between mt-3';
            notice.setAttribute('role', 'status');
            const toolbar = document.querySelector('.ip-main > .ip-toolbar');
            if (toolbar) toolbar.after(notice);
            else document.body.prepend(notice);
        }

        const message = document.createElement('span');
        message.textContent = text;

        const reload = document.createElement('button');
        reload.type = 'button';
        reload.className = 'ip-btn ip-btn-secondary ip-btn-sm flex-shrink-0';
        reload.textContent = t('Reload');
        reload.addEventListener('click', () => window.location.reload());

        notice.replaceChildren(message, reload);
    }

    // Used by ListNav, which swaps the same regions when a list's filters, tab or page change
    function refreshing() { return watchers.filter(w => w.mode !== 'warn'); }
    function regions() { return refreshing().flatMap(w => w.regions); }
    function beforeSwap() { refreshing().forEach(w => { if (typeof w.beforeRefresh === 'function') w.beforeRefresh(); }); }
    function afterSwap(doc) {
        if (doc) remember(doc);
        refreshing().forEach(w => { if (typeof w.afterRefresh === 'function') w.afterRefresh(); });
    }

    return { watch, regions, beforeSwap, afterSwap };
})();
