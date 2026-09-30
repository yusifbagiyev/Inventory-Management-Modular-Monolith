// InventoryManagement.Web/wwwroot/js/product-form.js
//
// Product create/edit form (Views/Products/_ProductFormFields.cshtml):
//  - [data-char-count-for="Field"] shows how many characters the field holds;
//  - the specifications list adds and removes name/value rows, named Specifications[i].Name/Value
//    in order so model binding gets a gap-free list;
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

    function initSpecifications() {
        const list = document.querySelector('[data-spec-list]');
        const template = document.querySelector('[data-spec-template]');
        const add = document.querySelector('[data-spec-add]');
        if (!list || !template || !add) return;
        const max = parseInt(list.dataset.max, 10) || 30;

        function renumber() {
            list.querySelectorAll('[data-spec-row]').forEach(function (row, i) {
                const inputs = row.querySelectorAll('input');
                inputs[0].name = `Specifications[${i}].Name`;
                inputs[1].name = `Specifications[${i}].Value`;
            });
            add.classList.toggle('d-none', list.querySelectorAll('[data-spec-row]').length >= max);
        }

        add.addEventListener('click', function () {
            const row = template.content.firstElementChild.cloneNode(true);
            list.appendChild(row);
            renumber();
            row.querySelector('input').focus();
        });
        list.addEventListener('click', function (e) {
            const remove = e.target.closest('[data-spec-remove]');
            if (!remove) return;
            remove.closest('[data-spec-row]').remove();
            renumber();
            add.focus();
        });
        renumber();
    }

    document.addEventListener('DOMContentLoaded', function () {
        initCounters();
        initCodeCheck();
        initSpecifications();
    });

    return { codeTaken: () => taken };
})();
