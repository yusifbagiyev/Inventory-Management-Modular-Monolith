// InventoryManagement.Web/wwwroot/js/product-form.js
//
// Product create/edit form (Views/Products/_ProductFormFields.cshtml):
//  - [data-char-count-for="Field"] shows how many characters the field holds;
//  - on Create, the inventory code is checked as soon as it is typed: a code that is taken shows
//    "This code is already in use: <model>" under the field instead of failing on save.

window.ProductForm = (function () {
    'use strict';

    let taken = false;

    function initCounters() {
        document.querySelectorAll('[data-char-count-for]').forEach(function (counter) {
            const field = document.getElementById(counter.dataset.charCountFor);
            if (!field) return;
            const update = () => { counter.textContent = t('{0} characters', field.value.length); };
            field.addEventListener('input', update);
            update();
        });
    }

    function initCodeCheck() {
        const input = document.querySelector('[data-code-check]');
        const message = document.querySelector('[data-code-taken]');
        if (!input || !message) return;

        let timer = null;
        let lastChecked = null;

        function show(text) {
            taken = !!text;
            message.textContent = text || '';
            message.classList.toggle('d-none', !text);
            input.classList.toggle('is-invalid', !!text);
        }

        async function check() {
            const code = parseInt(input.value, 10);
            if (!code || code < 1 || code > 9999) { show(''); return; }
            if (code === lastChecked) return;
            lastChecked = code;
            try {
                const response = await fetch(AppConfig.buildApiUrl(`products/search/inventory-code/${code}`), {
                    credentials: 'same-origin',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                if (String(code) !== String(parseInt(input.value, 10))) return;   // typed on meanwhile
                if (response.ok) {
                    const product = await response.json();
                    show(t('This code is already in use: {0}', product.model || product.Model || code));
                } else {
                    show('');   // 404: free (other statuses: let the server decide on save)
                }
            } catch (e) {
                show('');
            }
        }

        input.addEventListener('input', function () {
            clearTimeout(timer);
            lastChecked = null;
            show('');
            timer = setTimeout(check, 350);
        });
        input.addEventListener('blur', check);
        if (input.value) check();
    }

    document.addEventListener('DOMContentLoaded', function () {
        initCounters();
        initCodeCheck();
    });

    return { codeTaken: () => taken };
})();
