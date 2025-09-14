function exportRoutesToPDF() {
    const { jsPDF } = window.jspdf;
    const doc = new jsPDF('l', 'mm', 'a4'); // Landscape for better table fit

    // Add title
    doc.setFontSize(18);
    doc.text('Routes Report', 14, 15);

    // Add date and filters info
    doc.setFontSize(10);
    doc.text('Generated: ' + new Date().toLocaleDateString(), 14, 25);

    // Prepare table data
    const tableData = [];
    const headers = ['Type', 'Product', 'From', 'To', 'Status', 'Date'];

    document.querySelectorAll('#routesTable tbody tr').forEach(row => {
        const cells = row.querySelectorAll('td');
        if (cells.length >= 6) {
            // Extract route type
            const routeType = cells[0].querySelector('.badge')?.textContent.trim() || 'N/A';

            // Extract product info (just the model/vendor, not inventory code twice)
            const productInfo = cells[1].querySelector('div > div:first-child')?.textContent.trim() || 'N/A';

            // Extract From information
            const fromDept = cells[2].querySelector('div:first-child')?.textContent.trim() || 'N/A';
            const fromWorker = cells[2].querySelector('small')?.textContent.replace('Worker:', '').trim() || '';
            const fromInfo = fromWorker ? `${fromDept}\n${fromWorker}` : fromDept;

            // Extract To information
            const toDept = cells[3].querySelector('div:first-child')?.textContent.trim() || 'N/A';
            const toWorker = cells[3].querySelector('small')?.textContent.replace('Worker:', '').trim() || '';
            const toInfo = toWorker ? `${toDept}\n${toWorker}` : toDept;

            // Extract status
            const status = cells[4].textContent.includes('Completed') ? 'Completed' : 'Pending';

            // Extract date
            const date = cells[5].textContent.trim();

            tableData.push([routeType, productInfo, fromInfo, toInfo, status, date]);
        }
    });

    // Generate the table with better column widths
    doc.autoTable({
        head: [headers],
        body: tableData,
        startY: 30,
        theme: 'grid',
        styles: {
            fontSize: 8,
            cellPadding: 2,
            overflow: 'linebreak',
            halign: 'left'
        },
        headStyles: {
            fillColor: [239, 68, 68],
            textColor: [255, 255, 255],
            fontSize: 9,
            fontStyle: 'bold',
            halign: 'center'
        },
        columnStyles: {
            0: { cellWidth: 25, halign: 'center' }, // Type - narrower
            1: { cellWidth: 45 }, // Product - narrower
            2: { cellWidth: 55 }, // From - wider
            3: { cellWidth: 55 }, // To - wider
            4: { cellWidth: 20, halign: 'center' }, // Status - narrower
            5: { cellWidth: 30, halign: 'center' } // Date
        },
        alternateRowStyles: {
            fillColor: [250, 250, 250]
        },
        margin: { top: 30, left: 14, right: 14 }
    });

    // Add footer
    const pageCount = doc.internal.getNumberOfPages();
    for (let i = 1; i <= pageCount; i++) {
        doc.setPage(i);
        doc.setFontSize(8);
        doc.text('Page ' + i + ' of ' + pageCount,
            doc.internal.pageSize.width - 20,
            doc.internal.pageSize.height - 10);
    }

    // Save the PDF
    doc.save('routes-report-' + new Date().toISOString().split('T')[0] + '.pdf');
}

// Keep the existing product export function but improve it
function exportProductsToPDF() {
    const { jsPDF } = window.jspdf;
    const doc = new jsPDF('l', 'mm', 'a4');

    doc.setFontSize(18);
    doc.text('Products Inventory Report', 14, 15);

    doc.setFontSize(10);
    doc.text('Generated: ' + new Date().toLocaleDateString(), 14, 25);

    const tableData = [];
    const headers = ['Code', 'Model', 'Vendor', 'Department', 'Worker', 'Status'];

    document.querySelectorAll('#productsTable tbody tr').forEach(row => {
        const cells = row.querySelectorAll('td');
        if (cells.length >= 5) {
            const code = cells[1].querySelector('.badge')?.textContent.trim() || 'N/A';
            const model = cells[2].querySelector('strong')?.textContent.trim() || 'N/A';
            const vendor = cells[2].querySelector('small')?.textContent.replace('by', '').trim() || 'N/A';
            const dept = cells[3].querySelector('div:first-child')?.textContent.trim() || 'N/A';
            const worker = cells[3].querySelector('small')?.textContent.trim() || 'Unassigned';
            const isWorking = cells[4].textContent.includes('Working') ? 'Working' : 'Not Working';

            tableData.push([code, model, vendor, dept, worker, isWorking]);
        }
    });

    doc.autoTable({
        head: [headers],
        body: tableData,
        startY: 30,
        theme: 'grid',
        styles: {
            fontSize: 8,
            cellPadding: 2,
            overflow: 'linebreak'
        },
        headStyles: {
            fillColor: [59, 130, 246],
            textColor: [255, 255, 255],
            fontSize: 9,
            fontStyle: 'bold'
        },
        columnStyles: {
            0: { cellWidth: 20, halign: 'center' },
            1: { cellWidth: 50 },
            2: { cellWidth: 45 },
            3: { cellWidth: 45 },
            4: { cellWidth: 40 },
            5: { cellWidth: 25, halign: 'center' }
        },
        alternateRowStyles: {
            fillColor: [245, 245, 245]
        }
    });

    const pageCount = doc.internal.getNumberOfPages();
    for (let i = 1; i <= pageCount; i++) {
        doc.setPage(i);
        doc.setFontSize(8);
        doc.text('Page ' + i + ' of ' + pageCount,
            doc.internal.pageSize.width - 20,
            doc.internal.pageSize.height - 10);
    }

    doc.save('products-inventory-' + new Date().toISOString().split('T')[0] + '.pdf');
}

function exportTimelineToPDF() {
    const timeline = document.querySelector('.timeline');
    if (!timeline) {
        showToast('Timeline not found', 'error');
        return;
    }

    // Clone the timeline to modify it
    const timelineClone = timeline.cloneNode(true);

    // Remove images
    timelineClone.querySelectorAll('img').forEach(img => img.remove());

    // Remove action buttons
    timelineClone.querySelectorAll('.btn').forEach(btn => btn.remove());

    // Simplify timeline items
    timelineClone.querySelectorAll('.timeline-item').forEach(item => {
        const marker = item.querySelector('.timeline-marker');
        const content = item.querySelector('.timeline-content');

        // Create simplified HTML
        item.innerHTML = `
            <div style="display: flex; margin-bottom: 15px;">
                ${marker.outerHTML}
                <div style="flex: 1; margin-left: 15px; border-left: 2px solid #e0e0e0; padding-left: 15px;">
                    ${content.innerHTML}
                </div>
            </div>
        `;
    });

    // Generate HTML for PDF
    const htmlContent = `
        <div style="font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;">
            <h1 style="text-align: center; color: #1e40af; margin-bottom: 10px;">
                Transfer Timeline Report
            </h1>
            <div style="text-align: center; color: #6b7280; margin-bottom: 20px; border-bottom: 1px solid #eee; padding-bottom: 15px;">
                <div>Generated on: ${new Date().toLocaleString('en-US', {
        timeZone: 'Asia/Baku',
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    })}</div>
                <div>Total Transfers: ${timelineClone.querySelectorAll('.timeline-item').length}</div>
            </div>
            ${timelineClone.outerHTML}
        </div>
    `;

    const printWindow = window.open('', '_blank');
    printWindow.document.write(`
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="UTF-8">
            <title>Transfer Timeline Report</title>
            <style>
                @page { 
                    size: portrait; 
                    margin: 1cm;
                }
                body { 
                    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                    font-size: 10pt;
                    color: #333;
                    line-height: 1.4;
                }
                .timeline-item {
                    margin-bottom: 15px;
                }
                .timeline-marker {
                    width: 20px;
                    height: 20px;
                    border-radius: 50%;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    margin-top: 5px;
                }
                .fa-check-circle { color: #28a745; }
                .fa-clock { color: #ffc107; }
                .timeline-content {
                    background: #f8f9fa;
                    padding: 10px;
                    border-radius: 5px;
                    border: 1px solid #e0e0e0;
                }
                h6 {
                    font-size: 11pt;
                    margin: 0 0 5px 0;
                    display: flex;
                    justify-content: space-between;
                }
                .badge {
                    display: inline-block;
                    padding: 3px 8px;
                    border-radius: 4px;
                    font-weight: 600;
                    margin: 2px 0;
                }
                .bg-success { background-color: #28a745; color: white; }
                .bg-warning { background-color: #ffc107; color: black; }
            </style>
        </head>
        <body>
            ${htmlContent}
        </body>
        </html>
    `);
    printWindow.document.close();

    // Wait for content to load then print
    printWindow.onload = function () {
        setTimeout(() => {
            printWindow.print();
            printWindow.onafterprint = function () {
                printWindow.close();
            };
        }, 350);
    };
}

function exportCategoryTableToPDF() {
    const { jsPDF } = window.jspdf;
    const doc = new jsPDF('l', 'mm', 'a4'); // Landscape orientation

    // Add title
    doc.setFontSize(18);
    doc.text('Category Report', 14, 15);

    // Add date
    doc.setFontSize(10);
    doc.text('Generated: ' + new Date().toLocaleDateString(), 14, 25);

    // Prepare table data from the current view
    const tableData = [];
    const headers = ['Category Name', 'Description', 'Products', 'Status', 'Created Date'];

    document.querySelectorAll('#categoriesTable .category-item').forEach(row => {
        const categoryName = row.querySelector('.fw-semibold').textContent.trim();
        const description = row.querySelector('.text-truncate').textContent.trim();
        const products = row.querySelector('.badge.bg-primary').textContent.trim();
        const status = row.querySelector('[data-status]').getAttribute('data-status') === 'active' ? 'Active' : 'Inactive';
        const created = row.querySelector('.text-muted.small').textContent.trim();

        tableData.push([categoryName, description, products, status, created]);
    });

    // Generate the table with autoTable
    doc.autoTable({
        head: [headers],
        body: tableData,
        startY: 30,
        theme: 'grid',
        styles: {
            fontSize: 9,
            cellPadding: 3,
            overflow: 'linebreak',
            halign: 'left'
        },
        headStyles: {
            fillColor: [59, 130, 246],
            textColor: [255, 255, 255],
            fontSize: 10,
            fontStyle: 'bold',
            halign: 'center'
        },
        columnStyles: {
            0: { cellWidth: 40 }, // Category Name
            1: { cellWidth: 80 }, // Description
            2: { cellWidth: 25, halign: 'center' }, // Products
            3: { cellWidth: 25, halign: 'center' }, // Status
            4: { cellWidth: 35, halign: 'center' } // Created Date
        },
        alternateRowStyles: {
            fillColor: [245, 245, 245]
        },
        margin: { top: 30 }
    });

    // Add footer
    const pageCount = doc.internal.getNumberOfPages();
    for (let i = 1; i <= pageCount; i++) {
        doc.setPage(i);
        doc.setFontSize(8);
        doc.text('Page ' + i + ' of ' + pageCount,
            doc.internal.pageSize.width - 20,
            doc.internal.pageSize.height - 10);
    }

    // Save the PDF
    doc.save('categories-report-' + new Date().toISOString().split('T')[0] + '.pdf');
}

function exportDepartmentTableToPDF() {
    const { jsPDF } = window.jspdf;
    const doc = new jsPDF('l', 'mm', 'a4'); // Landscape orientation

    // Add title
    doc.setFontSize(18);
    doc.text('Department Report', 14, 15);

    // Add date
    doc.setFontSize(10);
    doc.text('Generated: ' + new Date().toLocaleDateString(), 14, 25);

    // Prepare table data
    const tableData = [];
    const headers = ['Department', 'Description', 'Products', 'Workers', 'Status', 'Created'];

    document.querySelectorAll('#departmentsTable .department-item').forEach(row => {
        const cells = row.querySelectorAll('td');
        if (cells.length >= 6) {
            const deptName = cells[0].querySelector('.fw-semibold').textContent.trim();
            const description = cells[1].textContent.trim();
            const products = cells[2].querySelector('.badge').textContent.trim();
            const workers = cells[3].querySelector('.badge').textContent.trim();
            const status = cells[4].textContent.includes('Active') ? 'Active' : 'Inactive';
            const created = cells[5].textContent.trim();

            tableData.push([deptName, description, products, workers, status, created]);
        }
    });

    // Generate the table
    doc.autoTable({
        head: [headers],
        body: tableData,
        startY: 30,
        theme: 'grid',
        styles: {
            fontSize: 9,
            cellPadding: 3,
            overflow: 'linebreak',
            halign: 'left'
        },
        headStyles: {
            fillColor: [16, 185, 129],
            textColor: [255, 255, 255],
            fontSize: 10,
            fontStyle: 'bold',
            halign: 'center'
        },
        columnStyles: {
            0: { cellWidth: 45 }, // Department
            1: { cellWidth: 75 }, // Description
            2: { cellWidth: 25, halign: 'center' }, // Products
            3: { cellWidth: 25, halign: 'center' }, // Workers
            4: { cellWidth: 25, halign: 'center' }, // Status
            5: { cellWidth: 35, halign: 'center' } // Created
        },
        alternateRowStyles: {
            fillColor: [245, 245, 245]
        },
        margin: { top: 30 }
    });

    // Add footer with page numbers
    const pageCount = doc.internal.getNumberOfPages();
    for (let i = 1; i <= pageCount; i++) {
        doc.setPage(i);
        doc.setFontSize(8);
        doc.text('Page ' + i + ' of ' + pageCount,
            doc.internal.pageSize.width - 20,
            doc.internal.pageSize.height - 10);
    }

    // Save the PDF
    doc.save('departments-report-' + new Date().toISOString().split('T')[0] + '.pdf');
}