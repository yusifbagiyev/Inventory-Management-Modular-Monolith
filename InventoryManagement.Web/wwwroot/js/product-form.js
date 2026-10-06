// Product form counters and specification rows, plus a code check that flags a taken code while typing

window.ProductForm = (function () {
    'use strict';

    let taken = false;

    function initCounters() {
        document.querySelectorAll('[data-char-count-for]').forEach(function (counter) {
            const field = document.getElementById(counter.dataset.charCountFor);
            if (!field) return;
            // A field with a limit shows how much of it is used, since typing simply stops there
            const max = field.maxLength > 0 ? field.maxLength : 0;
            const update = () => {
                counter.textContent = max ? `${field.value.length} / ${max}` : t('{0} characters', field.value.length);
            };
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
                if (String(code) !== String(parseInt(input.value, 10))) return;   // Input changed while waiting
                if (response.ok) {
                    const product = await response.json();
                    show(t('This code is already in use: {0}', product.model || product.Model || code));
                } else {
                    show('');   // 404 means free, and other errors are left to the save
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

        // Names must stay gap-free or model binding drops the rows after a gap
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

    // Required fields and the code range are checked when a field is left and before the form is sent, in the page language
    function initValidation() {
        const form = document.getElementById('productForm');
        if (!form) return;
        form.noValidate = true;
        const fields = () => Array.from(form.querySelectorAll('[required], [data-code-check]'));

        function labelOf(field) {
            const label = form.querySelector(`label[for="${CSS.escape(field.id)}"]`);
            const text = label && (label.querySelector('span:not(.req)') || label);
            return text ? text.textContent.replace('*', '').trim() : field.name;
        }

        // The data-val texts are the model's own messages, already in the page language
        function problem(field) {
            if (field.validity.valueMissing) return field.dataset.valRequired || t('The {0} field is required.', labelOf(field));
            if (field.validity.badInput || field.validity.rangeUnderflow || field.validity.rangeOverflow || field.validity.stepMismatch) {
                return field.dataset.valRange || t('Inventory code must be between 1 and 9999');
            }
            return '';
        }

        // Writes into the field's validation span the way FieldErrors does, so its clearing on edit applies too
        function check(field) {
            const text = problem(field);
            const message = form.querySelector(`[data-valmsg-for="${CSS.escape(field.name)}"]`);
            if (!text || !message) return !text;
            if (!message.id) message.id = field.id + '-error';
            message.setAttribute('aria-live', 'polite');
            message.textContent = text;
            message.classList.remove('field-validation-valid');
            message.classList.add('field-validation-error');
            field.classList.add('is-invalid');
            field.setAttribute('aria-invalid', 'true');
            const ids = (field.getAttribute('aria-describedby') || '').split(/\s+/).filter(id => id && id !== message.id);
            field.setAttribute('aria-describedby', ids.concat(message.id).join(' '));
            return false;
        }

        // Listening on document runs after the shared handlers that clear a field once it is edited
        document.addEventListener('focusout', function (e) { if (fields().includes(e.target)) check(e.target); });
        document.addEventListener('change', function (e) { if (e.target.tagName === 'SELECT' && fields().includes(e.target)) check(e.target); });
        // Capturing on document runs before the form's own submit handler
        document.addEventListener('submit', function (e) {
            if (e.target !== form) return;
            const invalid = fields().filter(field => !check(field));
            if (!invalid.length) return;
            e.preventDefault();
            e.stopImmediatePropagation();
            invalid[0].focus();
        }, true);
    }

    document.addEventListener('DOMContentLoaded', function () {
        initCounters();
        initCodeCheck();
        initSpecifications();
        initValidation();
    });

    return { codeTaken: () => taken };
})();
