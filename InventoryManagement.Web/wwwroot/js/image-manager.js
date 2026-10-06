// Image picker and gallery, writing picks back into the file input so a plain post sends them in order

window.ImageManager = (function () {
    'use strict';

    const MAX_BYTES = 5 * 1024 * 1024;
    const ALLOWED = /\.(jpe?g|png)$/i;
    const states = new WeakMap();

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
            files: [],          // New files in order, with their preview URLs
            removed: [],        // Saved image URLs to delete
            cover: null,        // Saved image URL chosen as cover
            coverNew: false     // The first new file is the cover, ahead of the saved images
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
        let overLimit = 0;
        picked.forEach(function (file) {
            if (!ALLOWED.test(file.name)) {
                errors.push(t('{0}: only JPG and PNG images are allowed', file.name));
            } else if (file.size > MAX_BYTES) {
                errors.push(t('{0}: larger than 5 MB', file.name));
            } else if (total(state) >= state.max) {
                overLimit++;
            } else {
                state.files.push({ file: file, url: URL.createObjectURL(file) });
            }
        });
        if (overLimit > 0) {
            errors.push(t('An item can have at most {0} images. Files not added: {1}', state.max, overLimit));
        }
        showErrors(state, errors);
        render(state);
    }

    // One line per refused file, written as text because file names come from the user
    function showErrors(state, messages) {
        state.error.replaceChildren(...Array.from(new Set(messages)).map(function (message) {
            const line = document.createElement('div');
            line.textContent = message;
            return line;
        }));
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
            // The file moves to the front of the new ones, which the server reads as new:0 next to saved images
            const index = parseInt(tile.dataset.imNew, 10);
            state.files.unshift(state.files.splice(index, 1)[0]);
            state.coverNew = existingTiles(state).length > 0;
        }
        render(state);
    }

    function render(state) {
        // Only new-file tiles are rebuilt, saved-image tiles stay put because their order holds the cover
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
            // A new cover leads the grid and other new files go after the saved images
            state.grid.insertBefore(tile, index === 0 && state.coverNew ? state.grid.firstChild : state.addTile);
        });

        state.grid.querySelectorAll('.im-tile').forEach(function (el, i) {
            el.classList.toggle('is-cover', i === 0);
            const star = el.querySelector('[data-im-cover]');
            if (star) star.setAttribute('aria-pressed', i === 0 ? 'true' : 'false');
        });

        try {
            const dt = new DataTransfer();
            state.files.forEach(item => dt.items.add(item.file));
            state.input.files = dt.files;
        } catch (e) { /* Old browsers without DataTransfer post only the last selection */ }

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

    /** Clears new files and pending removals after an AJAX submit that stays on the page. */
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

    // Gallery handlers are delegated on document so live-refreshed regions keep working
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
        gallery.querySelectorAll('[data-ig-dot]').forEach((d, i) => {
            d.classList.toggle('active', i === index);
            d.setAttribute('aria-current', i === index ? 'true' : 'false');
        });
    }

    function currentIndex(gallery) {
        const thumbs = Array.from(gallery.querySelectorAll('[data-ig-thumb]'));
        return Math.max(0, thumbs.findIndex(b => b.classList.contains('active')));
    }

    // A 40px horizontal swipe changes the image, and the click ending it is swallowed so no preview opens
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
        const dot = e.target.closest('[data-ig-dot]');
        const control = thumb || step || dot;
        const gallery = control && control.closest('[data-image-gallery]');
        if (!gallery) return;
        const thumbs = Array.from(gallery.querySelectorAll('[data-ig-thumb]'));
        if (dot) showGalleryImage(gallery, parseInt(dot.dataset.igDot, 10));
        else showGalleryImage(gallery, thumb ? thumbs.indexOf(thumb) : currentIndex(gallery) + parseInt(step.dataset.igStep, 10));
    });

    // The large gallery photo and saved picker tiles open the preview by click, Enter or Space
    function previewTarget(e) {
        const el = e.target.closest && e.target.closest('[data-ig-zoom], [data-im-preview]');
        if (!el) return null;
        return el.matches('[data-ig-zoom]')
            ? { src: el.getAttribute('src'), title: el.alt }
            : { src: el.dataset.src, title: el.dataset.title };
    }
    document.addEventListener('click', function (e) {
        const target = previewTarget(e);
        if (target) showImageModal(target.src, target.title);
    });
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter' && e.key !== ' ') return;
        const target = previewTarget(e);
        if (!target) return;
        e.preventDefault();
        showImageModal(target.src, target.title);
    });

    document.addEventListener('DOMContentLoaded', function () { initAll(); });

    return { init: init, initAll: initAll, reset: reset };
})();
