// InventoryManagement.Web/wwwroot/js/live-updates.js
//
// Keeps open pages current. The server announces every committed change ("EntityChanged",
// relayed by notification-manager.js as a 'live:changed' window event): the kind of record, its
// id, the action and who made it - no record data. A page says which kinds it shows:
//
//   LiveUpdates.watch({
//       entities: ['product', 'category'],   // kinds that affect this page
//       ids: { product: 12 },                // optional: only this record of that kind counts
//       regions: ['#productsRegion'],        // re-fetched from this URL and swapped in place
//       beforeRefresh() {}, afterRefresh() {},  // tear down / re-initialise page JS inside regions
//       isEditing() { return false; },       // true while the page has its own unsaved edit open
//       mode: 'refresh' | 'warn',            // warn: edit forms - never overwrite what is typed
//       label: 'product'                     // noun used in notices
//   });
//
// A refresh waits while the viewer is busy (a modal or dropdown open, typing inside a region,
// text selected) or the tab is hidden, then runs once for everything that arrived meanwhile.
// If a region is missing from the re-fetched page (record deleted, access lost) the page is
// left as it is and a notice offers a reload instead.

window.LiveUpdates = (function () {
    'use strict';

    const DEBOUNCE_MS = 700;
    const BUSY_RETRY_MS = 2000;
    const OFFLINE_RETRY_MS = 10000;
    const OWN_SAVE_WINDOW_MS = 10000;
    const watchers = [];

    // This tab's own form submission comes back as a change too; it is not a conflict. Saves made
    // elsewhere - another tab, an approval of someone's edit - still warn, even by the same user.
    let ownSaveUntil = 0;
    document.addEventListener('submit', () => { ownSaveUntil = Date.now() + OWN_SAVE_WINDOW_MS; }, true);

    function watch(options) {
        watchers.push(Object.assign({ mode: 'refresh', ids: {}, regions: [], entities: [] }, options, {
            pending: null,
            timer: null
        }));
    }

    // The change in the update that best describes what happened to this page (null id =
    // "many changed"): this very record deleted, else this record changed, else anything relevant.
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

    // Updates arriving within one debounce window are handled together, so a deletion is not
    // lost behind the route-history change that follows it.
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
                    : t('Reload to see the latest version; saving now may overwrite it.')));
            } else {
                schedule(w, update);
            }
        });
    });

    // After a reconnect the page may have missed changes: refresh every region once.
    window.addEventListener('live:resync', () => {
        watchers.filter(w => w.mode === 'refresh').forEach(w => schedule(w, {}));
    });

    function schedule(w, update) {
        w.pending = merge(w.pending, update);
        clearTimeout(w.timer);
        w.timer = setTimeout(() => tryRefresh(w), DEBOUNCE_MS);
    }

    function isBusy(w) {
        if (document.hidden) return true;
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

        let doc;
        try {
            const response = await fetch(window.location.href, {
                credentials: 'same-origin',
                cache: 'no-store',
                headers: { 'Accept': 'text/html' }
            });
            // Redirected (record gone, signed out) or refused: keep what is on screen.
            if (!response.ok || new URL(response.url).pathname !== window.location.pathname) {
                showNotice(describe(w, update) || t('This page is out of date.'));
                return;
            }
            doc = new DOMParser().parseFromString(await response.text(), 'text/html');
        } catch {
            // Offline for a moment: try again later with whatever arrived meanwhile.
            w.pending = merge(update, w.pending || { changes: [] });
            w.timer = setTimeout(() => tryRefresh(w), OFFLINE_RETRY_MS);
            return;
        }

        const pairs = w.regions.map(selector => [document.querySelector(selector), doc.querySelector(selector)]);
        if (pairs.some(([current, fresh]) => current && !fresh)) {
            showNotice(describe(w, update) || t('This page is out of date.'));
            return;
        }

        if (typeof w.beforeRefresh === 'function') w.beforeRefresh();
        pairs.forEach(([current, fresh]) => {
            if (!current || !fresh) return;
            const node = document.importNode(fresh, true);
            current.replaceWith(node);
            node.classList.add('live-refreshed');
            setTimeout(() => node.classList.remove('live-refreshed'), 1600);
        });
        if (typeof w.afterRefresh === 'function') w.afterRefresh();
    }

    function isDeletion(w, update) {
        const change = relevantChange(w, update);
        return !!change && change.action === 'deleted' && change.id != null;
    }

    // Whole sentences per kind of record (a noun spliced into one template reads badly once
    // translated). Each list: [plain, by {0}, at {1}, by {0} at {1}]; {0} = actor, {1} = time.
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

    // "This product was changed by Aysel Məmmədova at 10:42."
    function describe(w, update) {
        const change = relevantChange(w, update);
        if (!change) return '';
        const set = NOTICES[w.label || change.entity] || NOTICES.page;
        const sentences = change.action === 'deleted' ? set.deleted : set.changed;
        const at = update.at
            ? new Date(update.at).toLocaleTimeString(uiLocale(), { hour: '2-digit', minute: '2-digit' })
            : '';
        // [plain, by {0}, at {1}, by {0} at {1}]
        const index = (update.actorName ? 1 : 0) + (at ? 2 : 0);
        return t(sentences[index], update.actorName || '', at);
    }

    // One notice at the top of the page; later messages replace it. Built with textContent,
    // since the actor's name is user data.
    function showNotice(text) {
        let notice = document.getElementById('liveUpdateNotice');
        if (!notice) {
            notice = document.createElement('div');
            notice.id = 'liveUpdateNotice';
            // A banner, not a Bootstrap .alert: site.js auto-closes alerts 5 s after load.
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

    // For ListNav (site.js), which swaps the same regions when a list's filters, tab or page change.
    function refreshing() { return watchers.filter(w => w.mode !== 'warn'); }
    function regions() { return refreshing().flatMap(w => w.regions); }
    function beforeSwap() { refreshing().forEach(w => { if (typeof w.beforeRefresh === 'function') w.beforeRefresh(); }); }
    function afterSwap() { refreshing().forEach(w => { if (typeof w.afterRefresh === 'function') w.afterRefresh(); }); }

    return { watch, regions, beforeSwap, afterSwap };
})();
