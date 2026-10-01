// InventoryManagement.Web/wwwroot/js/image-manager.js
//
// Multi-image picker (Views/Shared/_ImageManager.cshtml) and details-page gallery
// (Views/Shared/_ImageGallery.cshtml).
//
// The picker keeps every file the user picked (across several "Add images" clicks) in a
// DataTransfer and writes it back to the <input name="ImageFiles" multiple>, so a normal form post
// or new FormData(form) sends them in order. Current images are removed/re-ordered through hidden
// RemoveImageUrls / CoverImageUrl inputs. The first image is the cover.
//
//   ImageManager.reset(formOrElement)   // after a successful AJAX submit that keeps the page

window.ImageManager = (function () {
    'use strict';

    const MAX_BYTES = 5 * 1024 * 1024;
    const ALLOWED = /\.(jpe?g|png)$/i;
    const states = new WeakMap();   // root element -> state

    function init(root) {
        if (!root || states.has(root)) return;
        const input = root.querySelector('[data-im-input]');
        const state = {
            root: root,
            input: input,
            grid: root.querySelector('[data-im-grid]'),
            addTile: root.querySelector('[data-im-add]'),
            hidden: root.querySelector('[data-im-hidden]'),
            error: root.querySelector('[data-im-error]'),
            count: root.querySelector('[data-im-count]'),
            max: parseInt(root.dataset.max, 10) || 10,
            files: [],          // { file, url } in order
            removed: [],        // current image urls to delete
            cover: null,        // current image url chosen as cover
            coverNew: false     // the first new file was chosen as cover over the current images
        };
        states.set(root, state);

        input.addEventListener('change', function () {
            addFiles(state, Array.from(input.files || []));
        });

        root.addEventListener('click', function (e) {
            const tile = e.target.closest('.im-tile');
            if (!tile) return;
            if (e.target.closest('[data-im-remove]')) {
                e.preventDefault();
                removeTile(state, tile);
            } else if (e.target.closest('[data-im-cover]')) {
                e.preventDefault();
                makeCover(state, tile);
            }
        });

        // Drag & drop onto the picker.
        root.addEventListener('dragover', function (e) {
            if (!e.dataTransfer || !Array.from(e.dataTransfer.types || []).includes('Files')) return;
            e.preventDefault();
            root.classList.add('im-dragover');
        });
        root.addEventListener('dragleave', function (e) {
            if (!root.contains(e.relatedTarget)) root.classList.remove('im-dragover');
        });
        root.addEventListener('drop', function (e) {
            if (!e.dataTransfer || !e.dataTransfer.files.length) return;
            e.preventDefault();
            root.classList.remove('im-dragover');
            addFiles(state, Array.from(e.dataTransfer.files));
        });

        render(state);
    }

    function existingTiles(state) {
        return Array.from(state.grid.querySelectorAll('[data-im-existing]'));
    }

    function total(state) {
        return existingTiles(state).length + state.files.length;
    }

    function addFiles(state, picked) {
        const errors = [];
        picked.forEach(function (file) {
            if (!ALLOWED.test(file.name)) {
                errors.push(t('{0}: only JPG and PNG images are allowed', file.name));
            } else if (file.size > MAX_BYTES) {
                errors.push(t('{0}: larger than 5 MB', file.name));
            } else if (total(state) >= state.max) {
                errors.push(t('At most {0} images', state.max));
            } else {
                state.files.push({ file: file, url: URL.createObjectURL(file) });
            }
        });
        state.error.textContent = Array.from(new Set(errors)).join(' ');
        render(state);
    }

    function removeTile(state, tile) {
        if (tile.dataset.imExisting !== undefined) {
            const url = tile.dataset.imExisting;
            state.removed.push(url);
            if (state.cover === url) state.cover = null;
            tile.remove();
        } else {
            const index = parseInt(tile.dataset.imNew, 10);
            const item = state.files.splice(index, 1)[0];
            if (item) URL.revokeObjectURL(item.url);
            if (index === 0) state.coverNew = false;
        }
        state.error.textContent = '';
        render(state);
    }

    function makeCover(state, tile) {
        if (tile.dataset.imExisting !== undefined) {
            state.cover = tile.dataset.imExisting;
            state.coverNew = false;
            state.grid.insertBefore(tile, state.grid.firstChild);
        } else {
            // The chosen file goes first among the new ones; with current images present it is
            // posted as CoverImageUrl "new:0" (the server resolves it after the upload).
            const index = parseInt(tile.dataset.imNew, 10);
            state.files.unshift(state.files.splice(index, 1)[0]);
            state.coverNew = existingTiles(state).length > 0;
        }
        render(state);
    }

    function render(state) {
        // New-file tiles are rebuilt; current-image tiles stay in the DOM (their order = cover choice).
        state.grid.querySelectorAll('[data-im-new]').forEach(el => el.remove());
        if (existingTiles(state).length === 0) state.coverNew = false;

        state.files.forEach(function (item, index) {
            const tile = document.createElement('div');
            tile.className = 'im-tile im-tile-new';
            tile.dataset.imNew = String(index);
            tile.innerHTML =
                '<div class="ip-image-tile">' +
                `<img src="${item.url}" alt="${escapeHtml(item.file.name)}" />` +
                `<button type="button" class="ctl star" data-im-cover title="${escapeHtml(t('Make cover'))}" aria-label="${escapeHtml(t('Make cover'))}"><i class="fa-solid fa-star"></i></button>` +
                `<button type="button" class="ctl remove" data-im-remove title="${escapeHtml(t('Remove'))}" aria-label="${escapeHtml(t('Remove'))}"><i class="fa-solid fa-xmark"></i></button>` +
                '</div>' +
                `<div class="ip-image-caption"><span class="text-truncate" title="${escapeHtml(item.file.name)}">${escapeHtml(item.file.name)}</span>` +
                `<span class="cover-label">${escapeHtml(t('Cover image'))}</span></div>`;
            // A new cover leads the whole grid; other new files follow the current images.
            state.grid.insertBefore(tile, index === 0 && state.coverNew ? state.grid.firstChild : state.addTile);
        });

        // The first tile is the cover.
        state.grid.querySelectorAll('.im-tile').forEach(function (el, i) {
            el.classList.toggle('is-cover', i === 0);
            const star = el.querySelector('[data-im-cover]');
            if (star) star.setAttribute('aria-pressed', i === 0 ? 'true' : 'false');
        });

        // Files back into the input, in order.
        try {
            const dt = new DataTransfer();
            state.files.forEach(item => dt.items.add(item.file));
            state.input.files = dt.files;
        } catch (e) { /* very old browsers: the last selection is posted as picked */ }

        // Hidden fields for the current images.
        let hidden = state.removed.map(url => `<input type="hidden" name="RemoveImageUrls" value="${escapeHtml(url)}" />`).join('');
        if (state.coverNew && state.files.length > 0) {
            hidden += '<input type="hidden" name="CoverImageUrl" value="new:0" />';
        } else if (state.cover && !state.removed.includes(state.cover)) {
            hidden += `<input type="hidden" name="CoverImageUrl" value="${escapeHtml(state.cover)}" />`;
        }
        state.hidden.innerHTML = hidden;

        const n = total(state);
        state.addTile.classList.toggle('d-none', n >= state.max);
        if (state.count) state.count.textContent = `${n} / ${state.max}`;
    }

    /** Clears new files and pending removals (e.g. after an AJAX submit that keeps the page). */
    function reset(scope) {
        const el = typeof scope === 'string' ? document.querySelector(scope) : scope;
        const roots = el && el.matches && el.matches('[data-image-manager]') ? [el]
            : Array.from((el || document).querySelectorAll('[data-image-manager]'));
        roots.forEach(function (root) {
            const state = states.get(root);
            if (!state) return;
            state.files.forEach(item => URL.revokeObjectURL(item.url));
            state.files = [];
            state.removed = [];
            state.cover = null;
            state.coverNew = false;
            state.error.textContent = '';
            render(state);
        });
    }

    function initAll(scope) {
        (scope || document).querySelectorAll('[data-image-manager]').forEach(init);
    }

    // Gallery (_ImageGallery): a thumbnail or prev/next shows that image large; the "cover" chip only
    // on the first. Delegated, so live-refreshed regions keep working.
    function showGalleryImage(gallery, index) {
        const thumbs = Array.from(gallery.querySelectorAll('[data-ig-thumb]'));
        const main = gallery.querySelector('[data-ig-main]');
        if (!main || !thumbs.length) return;
        index = (index + thumbs.length) % thumbs.length;
        main.setAttribute('src', thumbs[index].dataset.igThumb);
        thumbs.forEach((b, i) => b.classList.toggle('active', i === index));
        const counter = gallery.querySelector('[data-ig-counter]');
        if (counter) counter.textContent = `${index + 1} / ${thumbs.length}`;
        const cover = gallery.querySelector('[data-ig-cover]');
        if (cover) cover.hidden = index !== 0;
        gallery.querySelectorAll('.ip-gallery-dots i').forEach((d, i) => d.classList.toggle('active', i === index));
    }

    function currentIndex(gallery) {
        const thumbs = Array.from(gallery.querySelectorAll('[data-ig-thumb]'));
        return Math.max(0, thumbs.findIndex(b => b.classList.contains('active')));
    }

    // Swipe (phones): a horizontal drag of 40px or more shows the next / previous image, and the
    // tap that ends it does not open the preview.
    let swipe = null;
    document.addEventListener('pointerdown', function (e) {
        const box = e.target.closest('[data-image-gallery] .ip-gallery');
        swipe = box ? { gallery: box.closest('[data-image-gallery]'), x: e.clientX, y: e.clientY, moved: false } : null;
    });
    document.addEventListener('pointerup', function (e) {
        if (!swipe) return;
        const dx = e.clientX - swipe.x, dy = e.clientY - swipe.y;
        if (Math.abs(dx) >= 40 && Math.abs(dx) > Math.abs(dy)) {
            swipe.moved = true;
            showGalleryImage(swipe.gallery, currentIndex(swipe.gallery) + (dx < 0 ? 1 : -1));
        }
    });
    document.addEventListener('click', function (e) {
        if (swipe && swipe.moved && e.target.closest('[data-image-gallery]')) {
            e.stopPropagation();
            e.preventDefault();
        }
        swipe = null;
    }, true);

    document.addEventListener('click', function (e) {
        const thumb = e.target.closest('[data-ig-thumb]');
        const step = e.target.closest('[data-ig-step]');
        const gallery = (thumb || step) && (thumb || step).closest('[data-image-gallery]');
        if (!gallery) return;
        const thumbs = Array.from(gallery.querySelectorAll('[data-ig-thumb]'));
        showGalleryImage(gallery, thumb ? thumbs.indexOf(thumb) : currentIndex(gallery) + parseInt(step.dataset.igStep, 10));
    });

    document.addEventListener('DOMContentLoaded', function () { initAll(); });

    return { init: init, initAll: initAll, reset: reset };
})();
