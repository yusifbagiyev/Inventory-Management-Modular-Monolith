function exportProductsToPDF() {
    const selectedIds = $('.product-checkbox:checked').map(function () {
        return $(this).val();
    }).get();

    if (selectedIds.length === 0) {
        // If no selection, export visible products
        selectedIds.push(...$('.product-checkbox').map(function () {
            return $(this).val();
        }).get());
    }

    // Show loading
    showToast('Generating PDF...', 'info');

    // Create form for POST request
    const form = $('<form>', {
        method: 'POST',
        action: '/Products/ExportPdf'
    });

    selectedIds.forEach(id => {
        form.append($('<input>', {
            type: 'hidden',
            name: 'productIds',
            value: id
        }));
    });

    form.append($('<input>', {
        type: 'hidden',
        name: '__RequestVerificationToken',
        value: $('input[name="__RequestVerificationToken"]').val()
    }));

    $('body').append(form);
    form.submit();
    form.remove();
}

function exportRoutesToPDF() {
    const selectedIds = $('.route-checkbox:checked').map(function () {
        return $(this).val();
    }).get();

    if (selectedIds.length === 0) {
        showToast('Please select routes to export', 'warning');
        return;
    }

    showToast('Generating PDF...', 'info');

    const form = $('<form>', {
        method: 'POST',
        action: '/Routes/ExportPdf'
    });

    selectedIds.forEach(id => {
        form.append($('<input>', {
            type: 'hidden',
            name: 'routeIds',
            value: id
        }));
    });

    form.append($('<input>', {
        type: 'hidden',
        name: '__RequestVerificationToken',
        value: $('input[name="__RequestVerificationToken"]').val()
    }));

    $('body').append(form);
    form.submit();
    form.remove();
}