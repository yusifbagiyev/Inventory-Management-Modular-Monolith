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
    const imageColIndex = 0;  // Image column
    const actionColIndex = headers.length - 1;  // Actions column

    // Remove headers
    if (headers[imageColIndex]) headers[imageColIndex].remove();
    if (headers[actionColIndex]) headers[actionColIndex].remove();

    // Process each row
    tableClone.querySelectorAll('tbody tr').forEach(row => {
        const cells = row.querySelectorAll('td');

        // Remove image column
        if (cells[imageColIndex]) cells[imageColIndex].remove();

        // Remove actions column (now at index -1 after removing image)
        if (cells[cells.length - 1]) cells[cells.length - 1].remove();

        // Clean up the Code column (now first column)
        const codeCell = cells[1]; // After removing image, code is at index 0
        if (codeCell) {
            const badge = codeCell.querySelector('.badge');
            if (badge) {
                // Keep just the code number with better formatting
                codeCell.innerHTML = `<strong style="color: #1e40af;">${badge.textContent.trim()}</strong>`;
            }
        }

        // Clean up Product Details column
        const detailsCell = cells[2];
        if (detailsCell) {
            const strong = detailsCell.querySelector('strong');
            const small = detailsCell.querySelector('small');
            if (strong && small) {
                detailsCell.innerHTML = `
                    <div><strong>${strong.textContent}</strong></div>
                    <div style="font-size: 9pt; color: #666;">${small.textContent}</div>
                `;
            }
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

function exportCategoriesToPDF() {
    const table = document.querySelector('.table');
    if (!table) {
        showToast('Categories table not found', 'error');
        return;
    }

    const tableClone = table.cloneNode(true);
    tableClone.id = 'categoriesPdfTable';

    // Remove actions column
    const headers = tableClone.querySelectorAll('th');
    headers[headers.length - 1].remove();

    tableClone.querySelectorAll('tr').forEach(row => {
        const cells = row.querySelectorAll('td');
        if (cells.length > 0) {
            cells[cells.length - 1].remove();
        }
    });

    // Clean up content
    tableClone.querySelectorAll('td:first-child').forEach(cell => {
        const textDiv = cell.querySelector('div:last-child');
        if (textDiv) {
            cell.innerHTML = textDiv.innerHTML;
        }
    });

    // Remove icons from status column
    tableClone.querySelectorAll('.badge i').forEach(icon => icon.remove());

    // Adjusted column widths - decreased name, increased description
    const customStyles = `
        #categoriesPdfTable {
            table-layout: fixed;
            width: 100%;
        }
        #categoriesPdfTable th:nth-child(1) { width: 18%; }  /* Category Name - reduced */
        #categoriesPdfTable th:nth-child(2) { width: 42%; }  /* Description - increased */
        #categoriesPdfTable th:nth-child(3) { width: 12%; }  /* Products */
        #categoriesPdfTable th:nth-child(4) { width: 15%; }  /* Status */
        #categoriesPdfTable th:nth-child(5) { width: 13%; }  /* Created */
    `;

    exportToPDF(tableClone.outerHTML, 'categories_export.pdf', 'Categories Report', customStyles);
}

function exportRoutesToPDF() {
    const table = document.getElementById('routesTable');
    if (!table) {
        showToast('Routes table not found', 'error');
        return;
    }

    const tableClone = table.cloneNode(true);

    // Remove Image and Actions columns
    const headers = tableClone.querySelectorAll('th');
    if (headers[0]) headers[0].remove(); // Image
    if (headers[headers.length - 1]) headers[headers.length - 1].remove(); // Actions

    // Process rows
    tableClone.querySelectorAll('tbody tr').forEach(row => {
        const cells = row.querySelectorAll('td');

        // Remove image column
        if (cells[0]) cells[0].remove();

        // Remove actions column (now last after removing image)
        if (cells[cells.length - 1]) cells[cells.length - 1].remove();

        // Clean up Date column (now at index 0 after removals)
        const dateCell = cells[1];
        if (dateCell) {
            const dateText = dateCell.textContent.trim().replace(/\s+/g, ' ');
            dateCell.innerHTML = `<small>${dateText}</small>`;
        }

        // Clean up Type column
        const typeCell = cells[2];
        if (typeCell) {
            const badge = typeCell.querySelector('.badge');
            if (badge) {
                typeCell.innerHTML = badge.textContent.trim();
            }
        }

        // Clean up Product column - THIS IS THE IMPORTANT FIX
        const productCell = cells[3];
        if (productCell) {
            // Extract the inventory code (should only appear once)
            const codeElement = productCell.querySelector('.badge');
            const inventoryCode = codeElement ? codeElement.textContent.trim() : '';

            // Get the rest of the product info
            const productTextNodes = Array.from(productCell.childNodes)
                .filter(node => node.nodeType === Node.TEXT_NODE || node.tagName !== 'SPAN')
                .map(node => node.textContent.trim())
                .filter(text => text && text !== inventoryCode); // Remove duplicate code

            // Get vendor and model
            const vendorModel = productTextNodes.join(' ').trim();

            // Get category from small tag
            const categoryElement = productCell.querySelector('small');
            const category = categoryElement ? categoryElement.textContent.trim() : '';

            // Rebuild the cell content with proper formatting
            productCell.innerHTML = `
                <div><strong>Code: ${inventoryCode}</strong></div>
                <div>${vendorModel || 'No details'}</div>
                ${category ? `<small style="color: #666;">${category}</small>` : ''}
            `;
        }

        // Clean up From/To columns
        [cells[4], cells[5]].forEach(cell => {
            if (cell) {
                const deptElement = cell.querySelector('div') || cell;
                const dept = deptElement.childNodes[0]?.textContent?.trim() || cell.textContent.split('\n')[0]?.trim() || '';
                const workerElement = cell.querySelector('small');
                const worker = workerElement ? workerElement.textContent.replace('👤', '').trim() : '';

                cell.innerHTML = `
                    <div>${dept}</div>
                    ${worker ? `<small style="color: #666;">${worker}</small>` : ''}
                `;
            }
        });

        // Clean up Status column
        const statusCell = cells[6];
        if (statusCell) {
            const badge = statusCell.querySelector('.badge');
            const dateInfo = statusCell.querySelector('small');
            statusCell.innerHTML = `
                <div>${badge ? badge.textContent.trim() : ''}</div>
                ${dateInfo ? `<small style="color: #666;">${dateInfo.textContent.trim()}</small>` : ''}
            `;
        }
    });

    // Set table ID and custom styles
    tableClone.id = 'routesPdfTable';

    const customStyles = `
        #routesPdfTable {
            table-layout: fixed;
            width: 100%;
            font-size: 9pt;
        }
        #routesPdfTable th:nth-child(1) { width: 12%; }  /* Date */
        #routesPdfTable th:nth-child(2) { width: 10%; }  /* Type */
        #routesPdfTable th:nth-child(3) { width: 25%; }  /* Product - increased */
        #routesPdfTable th:nth-child(4) { width: 17%; }  /* From */
        #routesPdfTable th:nth-child(5) { width: 17%; }  /* To */
        #routesPdfTable th:nth-child(6) { width: 19%; }  /* Status */
        
        #routesPdfTable td {
            padding: 6px 4px;
            vertical-align: top;
            word-wrap: break-word;
        }
        
        #routesPdfTable small {
            font-size: 8pt;
            color: #666;
            display: block;
            margin-top: 2px;
        }
        
        #routesPdfTable strong {
            font-weight: 600;
        }
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
        #departmentsPdfTable {
            table-layout: fixed;
            width: 100%;
        }
        #departmentsPdfTable th:nth-child(1) { width: 25%; }  /* Department */
        #departmentsPdfTable th:nth-child(2) { width: 15%; }  /* Description */
        #departmentsPdfTable th:nth-child(3) { width: 15%; }  /* Products */
        #departmentsPdfTable th:nth-child(4) { width: 15%; }  /* Workers */
        #departmentsPdfTable th:nth-child(5) { width: 15%; }  /* Status */
        #departmentsPdfTable th:nth-child(6) { width: 15%; }  /* Created */
    `;

    exportToPDF(tableClone.outerHTML, 'departments_export.pdf', 'Departments Report', customStyles);
}