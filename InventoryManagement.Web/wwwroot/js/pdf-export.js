function exportProductsToPDF() {
    const table = document.getElementById('productsTable');
    if (!table) {
        showToast('Products table not found', 'error');
        return;
    }

    // Clone and prepare table
    const tableClone = table.cloneNode(true);

    // Find headers to remove (Image and Actions columns)
    const headers = tableClone.querySelectorAll('th');
    headers[0].remove();  // Remove Image header
    headers[headers.length - 1].remove();  // Actions column

    // Process each row
    tableClone.querySelectorAll('tbody tr').forEach(row => {
        const cells = row.querySelectorAll('td');

        // Remove image and actions cells
        cells[0].remove();
        cells[cells.length - 1].remove();

        // Clean up the Code column (now first column)
        const codeCell = cells[1]; // After removing image, code is at index 0
        if (codeCell) {
            const badge = codeCell.querySelector('.badge');
            if (badge) {
                // Keep just the code number with better formatting
                codeCell.innerHTML = `<strong style="color: #1e40af;">${badge.textContent.trim()}</strong>`;
            }
        }

        // Clean up Product Details column to include category
        const detailsCell = cells[2];
        if (detailsCell) {
            const model = detailsCell.querySelector('strong')?.textContent || '';
            const vendor = detailsCell.querySelector('small:first-of-type')?.textContent?.replace('by ', '') || '';
            const category = detailsCell.querySelector('.fa-tag')?.parentElement?.textContent?.trim() || '';

            detailsCell.innerHTML = `
                <div><strong>Vendor: ${vendor}</strong></div>
                <div style="font-size: 9pt;">Model: ${model}</div>
                <div style="font-size: 9pt;">Category: ${category}</div>
            `;
        }
    });

    tableClone.querySelectorAll('td:nth-child(4)').forEach(cell => { // Assuming Location is 4th column
        const deptText = cell.textContent.trim();
        const parts = deptText.split('(');

        if (parts.length > 1) {
            const department = parts[0].trim();
            const worker = parts[1].replace(')', '').trim();

            cell.innerHTML = `
                <div><strong>${department}</strong></div>
                <div style="font-size: 8pt; color: #666;">${worker}</div>
            `;
        }
    });

    // Process Status column (Working + Active in one column)
    tableClone.querySelectorAll('td:nth-child(5)').forEach(cell => { // Assuming Status is 5th column
        const badges = cell.querySelectorAll('.badge');
        let statusHTML = '';

        badges.forEach(badge => {
            const text = badge.textContent.trim();
            statusHTML += `<span class="badge" style="display: block; margin: 2px 0;">${text}</span>`;
        });

        cell.innerHTML = statusHTML;
    });

    const customStyles = `
        #productsPdfTable {
            table-layout: fixed;
            width: 100%;
        }
        #productsPdfTable th:nth-child(1) { width: 12%; }  /* Code */
        #productsPdfTable th:nth-child(2) { width: 30%; }  /* Product Details */
        #productsPdfTable th:nth-child(3) { width: 25%; }  /* Location */
        #productsPdfTable th:nth-child(4) { width: 33%; }  /* Status */
        
        #productsPdfTable td {
            vertical-align: top;
            padding: 8px 5px;
        }
        
        .badge {
            white-space: nowrap;
        }
    `;

    tableClone.id = 'productsPdfTable';
    exportToPDF(tableClone.outerHTML, 'products_export.pdf', 'Products Report', customStyles);
}

function exportRoutesToPDF() {
    const table = document.getElementById('routesTable');
    if (!table) {
        showToast('Routes table not found', 'error');
        return;
    }

    // Clone the table to modify it
    const tableClone = table.cloneNode(true);

    // Find all headers to determine indices to remove
    const headers = tableClone.querySelectorAll('th');
    const indicesToRemove = [];
    const headersToKeep = ['Date', 'Type', 'Product', 'From', 'To', 'Status'];

    headers.forEach((header, index) => {
        const headerText = header.textContent.trim();
        // Keep only specified columns
        if (!headersToKeep.some(keep => headerText.includes(keep))) {
            indicesToRemove.push(index);
        }
    });

    // Sort indices in descending order for safe removal
    indicesToRemove.sort((a, b) => b - a);

    // Remove headers
    indicesToRemove.forEach(index => {
        headers[index].remove();
    });

    // Remove corresponding cells in rows
    tableClone.querySelectorAll('tr').forEach(row => {
        const cells = row.querySelectorAll('td');
        indicesToRemove.forEach(index => {
            if (cells[index]) cells[index].remove();
        });
    });

    // Clean up columns
    // After removal, the columns are: Date, Type, Product, From, To, Status
    tableClone.querySelectorAll('td:nth-child(3)').forEach(cell => {
        const badge = cell.querySelector('.badge');
        const vendorModel = cell.textContent.replace(badge?.textContent || '', '').trim();

        cell.innerHTML = `
            <div><strong>Code: ${badge?.textContent || ''}</strong></div>
            <div>${vendorModel}</div>
        `;
    });

    // Clean up From column (now 4th column)
    tableClone.querySelectorAll('td:nth-child(4)').forEach(cell => {
        const div = cell.querySelector('div');
        const small = cell.querySelector('small');
        cell.innerHTML = `
            <div style="font-weight: bold;">${div?.textContent || ''}</div>
            ${small?.outerHTML || ''}
        `;
    });

    // Clean up To column (now 5th column)
    tableClone.querySelectorAll('td:nth-child(5)').forEach(cell => {
        const div = cell.querySelector('div');
        const small = cell.querySelector('small');
        cell.innerHTML = `
            <div style="font-weight: bold;">${div?.textContent || ''}</div>
            ${small?.outerHTML || ''}
        `;
    });

    // Clean up Status column (now 6th column)
    tableClone.querySelectorAll('td:nth-child(6)').forEach(cell => {
        // Remove icons and extra elements
        cell.querySelectorAll('i').forEach(icon => icon.remove());
        cell.querySelectorAll('br').forEach(br => br.remove());
        cell.querySelectorAll('small').forEach(sm => sm.remove());
    });

    // Remove any remaining images
    tableClone.querySelectorAll('img').forEach(img => img.remove());

    // Set a fixed ID for the table in the PDF
    tableClone.id = 'routesPdfTable';

    const customStyles = `
        #routesPdfTable {
            table-layout: fixed;
            width: 100%;
        }
        #routesPdfTable th:nth-child(1) { width: 12%; }  /* Date */
        #routesPdfTable th:nth-child(2) { width: 10%; }  /* Type */
        #routesPdfTable th:nth-child(3) { width: 23%; }  /* Product */
        #routesPdfTable th:nth-child(4) { width: 15%; }  /* From */
        #routesPdfTable th:nth-child(5) { width: 15%; }  /* To */
        #routesPdfTable th:nth-child(6) { width: 25%; }  /* Status */
    `;

    exportToPDF(tableClone.outerHTML, 'routes_export.pdf', 'Routes Report', customStyles);
}

function exportToPDF(tableHTML, filename, title, customStyles = '') {
    const printWindow = window.open('', '_blank');

    // Modern UI styles for portrait mode
    const styles = `
        <style>
            @page { 
                size: portrait; 
                margin: 0.7cm;
            }
            body { 
                font-family: 'Segoe UI', 'Roboto', 'Helvetica Neue', Arial, sans-serif;
                font-size: 10.5pt;
                color: #333;
                line-height: 1.35;
            }
            .container {
                max-width: 100%;
                padding: 0;
            }
            .header {
                text-align: center;
                margin-bottom: 12px;
                padding-bottom: 8px;
                border-bottom: 1px solid #e0e0e0;
            }
            h1 { 
                font-size: 18pt;
                margin: 0 0 5px 0;
                color: #1e40af;
                font-weight: 600;
            }
            .subheader {
                display: flex;
                justify-content: space-between;
                margin-top: 3px;
                font-size: 9.5pt;
                color: #6b7280;
            }
            table { 
                width: 100%; 
                border-collapse: collapse;
                margin-top: 12px;
                table-layout: fixed; /* Use fixed table layout */
            }
            th, td { 
                padding: 6px 5px; 
                text-align: left; 
                vertical-align: top;
                word-wrap: break-word;
            }
            th { 
                background-color: #3b82f6; 
                color: white;
                font-weight: 600;
                font-size: 10.5pt;
                text-transform: uppercase;
                letter-spacing: 0.3px;
                border: 1px solid #2563eb;
            }
            td {
                border: 1px solid #e2e8f0;
                font-size: 10pt;
            }
            .badge {
                display: inline-block;
                padding: 3px 7px;
                border-radius: 4px;
                font-size: 9.5pt;
                font-weight: 600;
                margin: 2px 0;
                line-height: 1.3;
            }
            .bg-success { background-color: #10b981; }
            .bg-danger { background-color: #ef4444; }
            .bg-info { background-color: #3b82f6; }
            .bg-warning { background-color: #f59e0b; }
            .bg-secondary { background-color: #64748b; }
            
            .footer {
                margin-top: 15px;
                text-align: center;
                font-size: 9pt;
                color: #6b7280;
                padding-top: 8px;
            }
            
            ${customStyles} /* Insert the custom styles here */
            
            @media print {
                body { margin: 0; }
                .no-print { display: none; }
            }
        </style>
    `;

    const generatedDate = new Date().toLocaleString('en-US', {
        timeZone: 'Asia/Baku',
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });

    // Count rows
    const tempDiv = document.createElement('div');
    tempDiv.innerHTML = tableHTML;
    const rowCount = tempDiv.querySelectorAll('tbody tr').length;

    // Build the document
    const documentContent = `
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="UTF-8">
            <title>${title}</title>
            ${styles}
        </head>
        <body>
            <div class="container">
                <div class="header">
                    <h1>${title}</h1>
                    <div class="subheader">
                        <span>Generated: ${generatedDate}</span>
                        <span>Total Records: ${rowCount}</span>
                    </div>
                </div>
                ${tableHTML}
                <div class="footer">
                    Inventory Management System | ${generatedDate}
                </div>
            </div>
        </body>
        </html>
    `;

    // Write content and trigger print
    printWindow.document.write(documentContent);
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

function exportDepartmentsToPDF() {
    const table = document.querySelector('.table');
    if (!table) {
        showToast('Departments table not found', 'error');
        return;
    }

    // Clone the table to modify it
    const tableClone = table.cloneNode(true);
    tableClone.id = 'departmentsPdfTable';

    // Remove actions column (last column)
    const headers = tableClone.querySelectorAll('th');
    headers[headers.length - 1].remove();  // Remove Actions header

    tableClone.querySelectorAll('tr').forEach(row => {
        const cells = row.querySelectorAll('td');
        if (cells.length > 0) {
            cells[cells.length - 1].remove();  // Remove Actions cell
        }
    });

    // Clean up Department column (first column) - remove icon
    tableClone.querySelectorAll('td:first-child').forEach(cell => {
        const textDiv = cell.querySelector('.fw-semibold');
        if (textDiv) {
            cell.innerHTML = `<strong>${textDiv.textContent}</strong>`;
        }
    });

    // Clean up Department Head column (second column) - simplify text
    tableClone.querySelectorAll('td:nth-child(2)').forEach(cell => {
        const text = cell.textContent.trim();
        if (text === 'Not assigned') {
            cell.innerHTML = '<span style="color: #999; font-style: italic;">Not assigned</span>';
        } else {
            cell.innerHTML = text;
        }
    });

    // Clean up Description column (third column) - simplify text
    tableClone.querySelectorAll('td:nth-child(3)').forEach(cell => {
        const text = cell.textContent.trim();
        if (text === 'No description provided') {
            cell.innerHTML = '<span style="color: #999; font-style: italic;">None</span>';
        } else {
            // Keep the description as is, but remove any extra whitespace
            cell.innerHTML = text;
        }
    });

    // Remove icons from status column
    tableClone.querySelectorAll('.badge i').forEach(icon => icon.remove());

    // Set column widths for proper PDF layout
    const customStyles = `
        #departmentsPdfTable {
            table-layout: fixed;
            width: 100%;
        }
        #departmentsPdfTable th:nth-child(1) { width: 18%; }  /* Department */
        #departmentsPdfTable th:nth-child(2) { width: 15%; }  /* Department Head */
        #departmentsPdfTable th:nth-child(3) { width: 22%; }  /* Description */
        #departmentsPdfTable th:nth-child(4) { width: 12%; }  /* Products */
        #departmentsPdfTable th:nth-child(5) { width: 12%; }  /* Workers */
        #departmentsPdfTable th:nth-child(6) { width: 12%; }  /* Status */
        #departmentsPdfTable th:nth-child(7) { width: 12%; }  /* Created */
    `;

    exportToPDF(tableClone.outerHTML, 'departments_export.pdf', 'Departments Report', customStyles);
}

function exportCategoriesToPDF() {
    const table = document.querySelector('.table');
    if (!table) {
        showToast('Categories table not found', 'error');
        return;
    }

    // Clone the table to modify it
    const tableClone = table.cloneNode(true);
    tableClone.id = 'categoriesPdfTable';

    // Remove actions column
    const headers = tableClone.querySelectorAll('th');
    headers[headers.length - 1].remove();  // Remove Actions header

    tableClone.querySelectorAll('tr').forEach(row => {
        const cells = row.querySelectorAll('td');
        if (cells.length > 0) {
            cells[cells.length - 1].remove();  // Remove Actions cell
        }
    });

    // Clean up content
    tableClone.querySelectorAll('td:first-child').forEach(cell => {
        const textDiv = cell.querySelector('div:last-child');
        if (textDiv) {
            cell.innerHTML = textDiv.outerHTML;
        }
    });

    // Remove icons from status column
    tableClone.querySelectorAll('.badge i').forEach(icon => icon.remove());

    // Set column widths
    const customStyles = `
        #categoriesPdfTable {
            table-layout: fixed;
            width: 100%;
        }
        #categoriesPdfTable th:nth-child(1) { width: 25%; }  /* Category */
        #categoriesPdfTable th:nth-child(2) { width: 30%; }  /* Description */
        #categoriesPdfTable th:nth-child(3) { width: 15%; }  /* Products */
        #categoriesPdfTable th:nth-child(4) { width: 15%; }  /* Status */
        #categoriesPdfTable th:nth-child(5) { width: 15%; }  /* Created */
    `;

    exportToPDF(tableClone.outerHTML, 'categories_export.pdf', 'Categories Report', customStyles);
}