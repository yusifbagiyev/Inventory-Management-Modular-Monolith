// Delete buttons of departments and categories (data-catalog-delete, data-name, data-products, data-url, data-edit-url) post the page's #catalogDeleteForm
(function () {
    const texts = {
        department: {
            title: () => t('Delete department'),
            confirm: name => t('Delete the department {0}? It is removed for good and cannot be restored.', name),
            blockedTitle: () => t('Department cannot be deleted'),
            blocked: (name, count) => t('The department {0} still has products ({1}). Move them to another department first, or deactivate the department instead.', name, count),
            edit: () => t('Edit Department')
        },
        category: {
            title: () => t('Delete category'),
            confirm: name => t('Delete the category {0}? It is removed for good and cannot be restored.', name),
            blockedTitle: () => t('Category cannot be deleted'),
            blocked: (name, count) => t('The category {0} still has products ({1}). Move them to another category first, or deactivate the category instead.', name, count),
            edit: () => t('Edit Category')
        }
    };

    // Delegated, because live updates swap the regions holding the buttons
    document.addEventListener('click', function (e) {
        const button = e.target.closest('[data-catalog-delete]');
        const kind = button && texts[button.dataset.catalogDelete];
        if (!kind) return;
        const name = button.dataset.name;
        const products = parseInt(button.dataset.products, 10) || 0;

        // The server refuses to delete a record that products still point at, so the dialog says so up front
        if (products > 0) {
            const editUrl = button.dataset.editUrl;
            confirmAction({
                title: kind.blockedTitle(),
                message: kind.blocked(name, products),
                okText: editUrl ? kind.edit() : t('Close')
            }, function () { if (editUrl) window.location.href = editUrl; });
            return;
        }

        confirmAction({
            title: kind.title(),
            message: kind.confirm(name),
            okText: t('Delete'),
            danger: true
        }, function () {
            const form = document.getElementById('catalogDeleteForm');
            form.action = button.dataset.url;
            form.submit();
        });
    });
})();
