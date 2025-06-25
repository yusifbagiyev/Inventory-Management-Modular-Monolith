// Product-specific functionality
$(document).ready(function () {
    // Initialize product features
    initializeProductFeatures();
});

function initializeProductFeatures() {
    // Auto-format inventory code input
    $('#InventoryCode').on('input', function () {
        var value = $(this).val();
        if (value && !isNaN(value)) {
            value = Math.min(9999, Math.max(1, parseInt(value)));
            $(this).val(value);
        }
    });

    // Live search for products
    $('#productSearch').on('keyup', function () {
        var value = $(this).val().toLowerCase();
        $('#productsTable tbody tr').filter(function () {
            $(this).toggle($(this).text().toLowerCase().indexOf(value) > -1);
        });
    });

    // Bulk actions
    $('#selectAll').change(function () {
        $('.product-checkbox').prop('checked', $(this).prop('checked'));
        updateBulkActions();
    });

    $('.product-checkbox').change(function () {
        updateBulkActions();
    });
}

function updateBulkActions() {
    var checkedCount = $('.product-checkbox:checked').length;
    if (checkedCount > 0) {
        $('#bulkActions').removeClass('d-none');
        $('#selectedCount').text(checkedCount);
    } else {
        $('#bulkActions').addClass('d-none');
    }
}

function exportProducts(format) {
    var selectedIds = $('.product-checkbox:checked').map(function () {
        return $(this).val();
    }).get();

    if (selectedIds.length === 0) {
        showToast('Please select products to export', 'warning');
        return;
    }

    // Create form and submit
    var form = $('<form>', {
        method: 'POST',
        action: '/Products/Export'
    });

    form.append($('<input>', {
        type: 'hidden',
        name: 'format',
        value: format
    }));

    form.append($('<input>', {
        type: 'hidden',
        name: 'productIds',
        value: JSON.stringify(selectedIds)
    }));

    form.append($('<input>', {
        type: 'hidden',
        name: '__RequestVerificationToken',
        value: $('input[name="__RequestVerificationToken"]').val()
    }));

    $('body').append(form);
    form.submit();
}

// Quick edit modal
function quickEdit(productId) {
    showSpinner();

    $.get('/Products/QuickEdit/' + productId, function (data) {
        hideSpinner();
        $('#quickEditModal .modal-body').html(data);
        $('#quickEditModal').modal('show');
    }).fail(function () {
        hideSpinner();
        showToast('Failed to load product details', 'error');
    });
}

// Product image gallery
function initializeImageGallery() {
    $('.product-image-thumb').click(function () {
        var fullImageUrl = $(this).data('full-image');
        $('#mainProductImage').attr('src', fullImageUrl);
    });
}