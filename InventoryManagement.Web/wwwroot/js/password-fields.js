// Show and hide buttons beside password fields (data-password-toggle="<field id>") and a copy button (data-password-copy="<field id>")

window.PasswordFields = (function () {
    'use strict';

    /** Shows or masks a password field and updates every button that toggles it. */
    function setShown(field, show) {
        field.type = show ? 'text' : 'password';
        document.querySelectorAll(`[data-password-toggle="${CSS.escape(field.id)}"]`).forEach(function (button) {
            const label = show ? t('Hide password') : t('Show password');
            button.setAttribute('aria-pressed', show ? 'true' : 'false');
            button.setAttribute('aria-label', label);
            button.title = label;
            const icon = button.querySelector('i');
            if (icon) icon.className = show ? 'fa-regular fa-eye-slash' : 'fa-regular fa-eye';
        });
    }

    async function copy(field) {
        if (!field.value) { showToast(t('There is no password to copy yet'), 'warning'); return; }
        try {
            await navigator.clipboard.writeText(field.value);
        } catch (e) {
            // Older browsers and pages outside a secure context have no clipboard API
            const type = field.type;
            field.type = 'text';
            field.select();
            const copied = document.execCommand && document.execCommand('copy');
            field.type = type;
            if (!copied) { showToast(t('The password could not be copied'), 'error'); return; }
        }
        showToast(t('Password copied'), 'success');
    }

    document.addEventListener('click', function (e) {
        const toggle = e.target.closest('[data-password-toggle]');
        if (toggle) {
            const field = document.getElementById(toggle.dataset.passwordToggle);
            if (field) setShown(field, field.type === 'password');
            return;
        }
        const copyButton = e.target.closest('[data-password-copy]');
        if (copyButton) {
            const field = document.getElementById(copyButton.dataset.passwordCopy);
            if (field) copy(field);
        }
    });

    return { setShown: setShown };
})();
