// Route-specific functionality
$(document).ready(function () {
    initializeRouteFeatures();
});

function initializeRouteFeatures() {
    // Product selection change handler
    $('#productSelect').change(function () {
        var productId = $(this).val();
        if (productId) {
            loadProductDetails(productId);
        } else {
            $('#productInfo').addClass('d-none');
        }
    });

    // Initialize date range picker for route filtering
    if ($('#dateRange').length) {
        $('#dateRange').daterangepicker({
            autoUpdateInput: false,
            locale: {
                cancelLabel: 'Clear'
            }
        });

        $('#dateRange').on('apply.daterangepicker', function (ev, picker) {
            $(this).val(picker.startDate.format('MM/DD/YYYY') + ' - ' + picker.endDate.format('MM/DD/YYYY'));
            filterRoutes();
        });

        $('#dateRange').on('cancel.daterangepicker', function (ev, picker) {
            $(this).val('');
            filterRoutes();
        });
    }

    // Batch selection
    $('#selectAllRoutes').change(function () {
        $('.route-checkbox').prop('checked', $(this).prop('checked'));
        updateBatchActions();
    });

    $('.route-checkbox').change(function () {
        updateBatchActions();
    });
}

function loadProductDetails(productId) {
    showSpinner();

    $.get('/api/products/' + productId, function (product) {
        hideSpinner();

        var details = `
            <div class="row">
                <div class="col-md-6">
                    <strong>Inventory Code:</strong> ${product.inventoryCode}<br>
                    <strong>Model:</strong> ${product.model}<br>
                    <strong>Vendor:</strong> ${product.vendor}
                </div>
                <div class="col-md-6">
                    <strong>Current Department:</strong> ${product.departmentName}<br>
                    <strong>Current Worker:</strong> ${product.worker || 'Not Assigned'}<br>
                    <strong>Status:</strong> ${product.isWorking ? 'Working' : 'Not Working'}
                </div>
            </div>
        `;

        $('#productDetails').html(details);
        $('#productInfo').removeClass('d-none');
    }).fail(function () {
        hideSpinner();
        showToast('Failed to load product details', 'error');
    });
}

function updateBatchActions() {
    var checkedCount = $('.route-checkbox:checked').length;
    if (checkedCount > 0) {
        $('#batchActions').removeClass('d-none');
        $('#selectedRoutesCount').text(checkedCount);
    } else {
        $('#batchActions').addClass('d-none');
    }
}

function batchDeleteRoutes() {
    var selectedIds = $('.route-checkbox:checked').map(function () {
        return parseInt($(this).val());
    }).get();

    if (selectedIds.length === 0) {
        showToast('Please select routes to delete', 'warning');
        return;
    }

    if (!confirm(`Are you sure you want to delete ${selectedIds.length} routes?`)) {
        return;
    }

    showSpinner();

    $.ajax({
        url: '/Routes/BatchDelete',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({ routeIds: selectedIds }),
        headers: {
            'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
        },
        success: function (result) {
            hideSpinner();

            if (result.totalSuccessful > 0) {
                showToast(`Successfully deleted ${result.totalSuccessful} routes`, 'success');
            }

            if (result.totalFailed > 0) {
                var failedMessage = `Failed to delete ${result.totalFailed} routes:\n`;
                result.failed.forEach(function (failure) {
                    failedMessage += `Route ${failure.routeId}: ${failure.reason}\n`;
                });
                showToast(failedMessage, 'error');
            }

            setTimeout(function () {
                location.reload();
            }, 2000);
        },
        error: function () {
            hideSpinner();
            showToast('An error occurred during batch delete', 'error');
        }
    });
}

function filterRoutes() {
    var dateRange = $('#dateRange').val();
    var status = $('#statusFilter').val();
    var department = $('#departmentFilter').val();

    var queryParams = new URLSearchParams();

    if (dateRange) {
        var dates = dateRange.split(' - ');
        queryParams.append('startDate', dates[0]);
        queryParams.append('endDate', dates[1]);
    }

    if (status) {
        queryParams.append('isCompleted', status === 'completed');
    }

    if (department) {
        queryParams.append('departmentId', department);
    }

    window.location.href = '/Routes?' + queryParams.toString();
}

// Timeline view enhancements
function initializeTimeline() {
    // Add animation to timeline items
    $('.timeline-item').each(function (index) {
        $(this).css('animation-delay', (index * 0.1) + 's');
        $(this).addClass('fade-in');
    });

    // Lightbox for timeline images
    $('.timeline-content img').click(function () {
        var src = $(this).attr('src');
        $('#imageLightbox img').attr('src', src);
        $('#imageLightbox').modal('show');
    });
}

// Route completion with progress
function completeRouteWithProgress(routeId) {
    if (!confirm('Are you sure you want to complete this route?')) {
        return;
    }

    var progressModal = `
        <div class="modal fade" id="progressModal" tabindex="-1" data-bs-backdrop="static">
            <div class="modal-dialog modal-sm">
                <div class="modal-content">
                    <div class="modal-body text-center">
                        <div class="spinner-border text-primary mb-3" role="status">
                            <span class="visually-hidden">Loading...</span>
                        </div>
                        <p>Completing route...</p>
                    </div>
                </div>
            </div>
        </div>
    `;

    $('body').append(progressModal);
    $('#progressModal').modal('show');

    $.ajax({
        url: '/Routes/Complete/' + routeId,
        type: 'POST',
        headers: {
            'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
        },
        success: function () {
            $('#progressModal').modal('hide');
            showToast('Route completed successfully!', 'success');
            setTimeout(function () {
                location.reload();
            }, 1500);
        },
        error: function () {
            $('#progressModal').modal('hide');
            showToast('Failed to complete route', 'error');
        }
    });
}