// User or role permission editor that saves each change at once and puts the control back if it fails

window.PermissionEditor = (function () {
    'use strict';

    function post(url, name, grant) {
        return fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: Object.assign({ 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' }, AppConfig.antiforgeryHeaders()),
            body: JSON.stringify({ permissionName: name, isGranting: grant })
        }).then(function (response) {
            return response.json().catch(function () { return {}; }).then(function (body) {
                if (!response.ok || body.success === false) throw new Error(body.message || t('Permission change failed'));
            });
        });
    }

    function init(editor) {
        const url = editor.dataset.saveUrl;
        if (!url || editor.dataset.readonly === 'true') return;

        function updateCount(area) {
            const count = area.querySelector('[data-count]');
            if (!count) return;
            const switches = Array.from(area.querySelectorAll('.ip-switch'));
            const segs = Array.from(area.querySelectorAll('.ip-seg'));
            const on = switches.filter(s => s.checked).length
                + segs.filter(s => s.querySelector('.active')?.dataset.level !== 'none').length;
            count.textContent = on + '/' + (switches.length + segs.length);
        }

        // Granting an action without the page it lives on would leave it unreachable
        async function ensureView(area) {
            const view = area.querySelector('.ip-switch[data-view="true"]');
            if (!view || view.checked) return;
            view.checked = true;
            try {
                await post(url, view.dataset.perm, true);
                view.dataset.own = 'true';
                showToast(t('Viewing permission was granted too.'), 'info');
            } catch (e) {
                view.checked = false;
            }
        }

        editor.addEventListener('change', async function (e) {
            const sw = e.target.closest('.ip-switch');
            if (!sw || sw.disabled) return;
            const area = sw.closest('.ip-perm-area');
            const grant = sw.checked;
            sw.disabled = true;
            try {
                await post(url, sw.dataset.perm, grant);
                sw.dataset.own = grant ? 'true' : 'false';
                if (grant && sw.dataset.view !== 'true') await ensureView(area);
            } catch (err) {
                sw.checked = !grant;
                showToast(err.message, 'error');
            } finally {
                sw.disabled = false;
                updateCount(area);
            }
        });

        editor.addEventListener('click', async function (e) {
            const button = e.target.closest('.ip-seg [data-level]');
            if (!button || button.disabled || button.classList.contains('active')) return;
            const seg = button.closest('.ip-seg');
            const area = seg.closest('.ip-perm-area');
            const from = seg.dataset.own;
            const to = button.dataset.level;
            const buttons = Array.from(seg.querySelectorAll('[data-level]'));
            const select = level => buttons.forEach(b => {
                b.classList.toggle('active', b.dataset.level === level);
                b.setAttribute('aria-checked', b.dataset.level === level ? 'true' : 'false');
            });

            select(to);
            seg.classList.add('is-saving');
            try {
                // Direct can mean both names are held, and revoking before granting leaves fewer rights when a step fails
                if (to !== 'approval' && from !== 'none') await post(url, seg.dataset.base, false);
                if (to !== 'direct' && from === 'direct') await post(url, seg.dataset.direct, false);
                if (to !== 'none') await post(url, to === 'direct' ? seg.dataset.direct : seg.dataset.base, true);
                seg.dataset.own = to;
                if (to !== 'none') await ensureView(area);
            } catch (err) {
                select(from);
                showToast(err.message, 'error');
            } finally {
                seg.classList.remove('is-saving');
                updateCount(area);
            }
        });
    }

    function initSearch() {
        const input = document.getElementById('permissionSearch');
        if (!input) return;
        input.addEventListener('input', function () {
            const term = input.value.trim().toLowerCase();
            document.querySelectorAll('.ip-perm-editor').forEach(function (editor) {
                let any = false;
                editor.querySelectorAll('.ip-perm-area').forEach(function (area) {
                    let visible = 0;
                    area.querySelectorAll('.ip-perm-row').forEach(function (row) {
                        const show = !term || row.dataset.search.includes(term);
                        row.classList.toggle('d-none', !show);
                        if (show) visible++;
                    });
                    area.classList.toggle('d-none', visible === 0);
                    any = any || visible > 0;
                });
                editor.querySelector('[data-perm-empty]')?.classList.toggle('d-none', any);
            });
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('.ip-perm-editor').forEach(init);
        initSearch();
    });

    return { init: init };
})();
