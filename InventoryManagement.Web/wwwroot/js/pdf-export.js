// PDF Export Module with proper formatting
window.PDFExport = (function () {
    'use strict';

    // Export products to PDF with proper column display
    function exportProductsToPDF() {
        const table = document.getElementById('productsTable');
        if (!table) {
            showToast('Products table not found', 'error');
            return;
        }

        // Clone and prepare table
        const tableClone = table.cloneNode(true);

        // Remove Image and Actions columns
        const headers = tableClone.querySelectorAll('thead th');
        if (headers[0]) headers[0].remove(); // Image
        if (headers[headers.length - 1]) headers[headers.length - 1].remove(); // Actions

        // Process each row
        tableClone.querySelectorAll('tbody tr').forEach(row => {
            const cells = row.querySelectorAll('td');

            // Remove image column (first)
            if (cells[0]) cells[0].remove();

            // Remove actions column (last)
            if (cells[cells.length - 1]) cells[cells.length - 1].remove();

            // Format Code column (now index 0 after removing image)
            if (cells[1]) {
                const badge = cells[1].querySelector('.badge');
                if (badge) {
                    cells[1].innerHTML = `<strong style="color: #1e40af;">${badge.textContent.trim()}</strong>`;
                }
            }

            // Format Product Details column properly (now index 1)
            if (cells[2]) {
                const modelElement = cells[2].querySelector('strong');
                const vendorElement = cells[2].querySelector('small');
                const categoryElement = cells[2].querySelectorAll('small')[1];

                const model = modelElement ? modelElement.textContent.trim() : '';
                const vendor = vendorElement ? vendorElement.textContent.replace('by ', '').trim() : '';
                const category = categoryElement ? categoryElement.textContent.replace(/fas fa-tag/gi, '').trim() : '';

                cells[2].innerHTML = `
                    <div><strong>Model:</strong> ${model}</div>
                    <div><strong>Vendor:</strong> ${vendor}</div>
                    <div><strong>Category:</strong> ${category}</div>
                `;
            }

            // Format Location column (now index 2)
            if (cells[3]) {
                const deptElement = cells[3].querySelector('div');
                const workerElement = cells[3].querySelector('small');

                const dept = deptElement ? deptElement.textContent.replace(/fas fa-building/gi, '').trim() : '';
                const worker = workerElement ? workerElement.textContent.replace(/fas fa-user/gi, '').trim() : '';

                cells[3].innerHTML = `
                    <div><strong>${dept}</strong></div>
                    ${worker ? `<div style="font-size: 9pt; color: #666;">${worker}</div>` : ''}
                `;
            }

            // Format Status column (now index 3)
            if (cells[4]) {
                const badges = cells[4].querySelectorAll('.badge');
                let statusHTML = '';
                badges.forEach(badge => {
                    const text = badge.textContent.trim();
                    const color = text === 'Working' ? 'green' :
                        text === 'Active' ? 'blue' : 'red';
                    statusHTML += `<span style="color: ${color}; display: block; margin: 2px 0;">${text}</span>`;
                });
                cells[4].innerHTML = statusHTML;
            }
        });

        const customStyles = `
            #productsPdfTable {
                table-layout: fixed;
                width: 100%;
            }
            #productsPdfTable th:nth-child(1) { width: 10%; }  /* Code */
            #productsPdfTable th:nth-child(2) { width: 35%; }  /* Product Details */
            #productsPdfTable th:nth-child(3) { width: 30%; }  /* Location */
            #productsPdfTable th:nth-child(4) { width: 25%; }  /* Status */
            
            #productsPdfTable td {
                vertical-align: top;
                padding: 8px 5px;
                font-size: 9pt;
            }
        `;

        tableClone.id = 'productsPdfTable';
        generatePDF(tableClone.outerHTML, 'products_export.pdf', 'Products Report', customStyles);
    }

    // Export routes to PDF with fixed product column
    function exportRoutesToPDF() {
        const table = document.getElementById('routesTable');
        if (!table) {
            showToast('Routes table not found', 'error');
            return;
        }

        const tableClone = table.cloneNode(true);

        // Remove Image and Actions columns
        const headers = tableClone.querySelectorAll('thead th');
        if (headers[0]) headers[0].remove(); // Image
        if (headers[headers.length - 1]) headers[headers.length - 1].remove(); // Actions

        // Process each row
        tableClone.querySelectorAll('tbody tr').forEach(row => {
            const cells = row.querySelectorAll('td');

            // Remove image column (first)
            if (cells[0]) cells[0].remove();

            // Remove actions column (last)
            if (cells[cells.length - 1]) cells[cells.length - 1].remove();

            // Format Product column properly (now index 2 after removing image)
            if (cells[3]) {
                // Extract the inventory code, vendor, model, and category
                const badge = cells[3].querySelector('.badge');
                const linkOrDiv = cells[3].querySelector('a, div');
                const categorySmall = cells[3].querySelector('small');

                let inventoryCode = badge ? badge.textContent.trim() : '';
                let vendor = '';
                let model = '';
                let category = '';

                if (linkOrDiv) {
                    // Get text content and parse it
                    const textContent = linkOrDiv.textContent;
                    // Remove the inventory code from the text to get vendor
                    vendor = textContent.replace(inventoryCode, '').trim();
                }

                if (categorySmall) {
                    category = categorySmall.textContent.replace(/fas fa-tag/gi, '').trim();
                }

                // Properly format the product details
                cells[3].innerHTML = `
                    <div><strong>Code:</strong> ${inventoryCode}</div>
                    <div><strong>Vendor:</strong> ${vendor}</div>
                    <div><strong>Category:</strong> ${category}</div>
                `;
            }

            // Clean up From column (now index 3)
            if (cells[4]) {
                const deptDiv = cells[4].querySelector('div');
                const workerSmall = cells[4].querySelector('small');

                const dept = deptDiv ? deptDiv.textContent.trim() : '';
                const worker = workerSmall ? workerSmall.textContent.replace(/fas fa-user/gi, '').trim() : '';

                cells[4].innerHTML = `
                    <div><strong>${dept}</strong></div>
                    ${worker ? `<div style="font-size: 8pt; color: #666;">${worker}</div>` : ''}
                `;
            }

            // Clean up To column (now index 4)
            if (cells[5]) {
                const deptDiv = cells[5].querySelector('div');
                const workerSmall = cells[5].querySelector('small');

                const dept = deptDiv ? deptDiv.textContent.trim() : '';
                const worker = workerSmall ? workerSmall.textContent.replace(/fas fa-user/gi, '').trim() : '';

                cells[5].innerHTML = `
                    <div><strong>${dept}</strong></div>
                    ${worker ? `<div style="font-size: 8pt; color: #666;">${worker}</div>` : ''}
                `;
            }

            // Clean up Status column (now index 5)
            if (cells[6]) {
                // Remove icons and format status
                cells[6].querySelectorAll('i').forEach(icon => icon.remove());
                const badge = cells[6].querySelector('.badge');
                const smallDate = cells[6].querySelector('small');

                let statusText = badge ? badge.textContent.trim() : '';
                let dateText = smallDate ? smallDate.textContent.trim() : '';

                cells[6].innerHTML = `
                    <div><strong>${statusText}</strong></div>
                    ${dateText ? `<div style="font-size: 8pt; color: #666;">${dateText}</div>` : ''}
                `;
            }
        });

        const customStyles = `
            #routesPdfTable {
                table-layout: fixed;
                width: 100%;
            }
            #routesPdfTable th:nth-child(1) { width: 12%; }  /* Date */
            #routesPdfTable th:nth-child(2) { width: 10%; }  /* Type */
            #routesPdfTable th:nth-child(3) { width: 25%; }  /* Product */
            #routesPdfTable th:nth-child(4) { width: 18%; }  /* From */
            #routesPdfTable th:nth-child(5) { width: 18%; }  /* To */
            #routesPdfTable th:nth-child(6) { width: 17%; }  /* Status */
            
            #routesPdfTable td {
                vertical-align: top;
                padding: 6px 4px;
                font-size: 9pt;
            }
        `;

        tableClone.id = 'routesPdfTable';
        generatePDF(tableClone.outerHTML, 'routes_export.pdf', 'Routes Report', customStyles);
    }

    // Generate PDF from HTML
    function generatePDF(tableHTML, filename, title, customStyles) {
        const printWindow = window.open('', '_blank');

        const styles = `
            <style>
                @page { 
                    size: portrait; 
                    margin: 0.7cm;
                }
                body { 
                    font-family: 'Segoe UI', Arial, sans-serif;
                    font-size: 10pt;
                    color: #333;
                    line-height: 1.4;
                }
                .header {
                    text-align: center;
                    margin-bottom: 15px;
                    padding-bottom: 10px;
                    border-bottom: 2px solid #3b82f6;
                }
                h1 { 
                    font-size: 20pt;
                    margin: 0 0 5px 0;
                    color: #1e40af;
                    font-weight: 600;
                }
                .subheader {
                    display: flex;
                    justify-content: space-between;
                    margin-top: 5px;
                    font-size: 10pt;
                    color: #6b7280;
                }
                table { 
                    width: 100%; 
                    border-collapse: collapse;
                    margin-top: 15px;
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
                    font-size: 10pt;
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                    border: 1px solid #2563eb;
                }
                td {
                    border: 1px solid #e2e8f0;
                    font-size: 9pt;
                }
                tr:nth-child(even) {
                    background-color: #f8fafc;
                }
                .footer {
                    margin-top: 20px;
                    text-align: center;
                    font-size: 9pt;
                    color: #6b7280;
                    border-top: 1px solid #e2e8f0;
                    padding-top: 10px;
                }
                ${customStyles}
            </style>
        `;

        const now = new Date();
        const timestamp = now.toLocaleString('en-US', {
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

        const documentContent = `
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="UTF-8">
                <title>${title}</title>
                ${styles}
            </head>
            <body>
                <div class="header">
                    <h1>${title}</h1>
                    <div class="subheader">
                        <span>Generated: ${timestamp}</span>
                        <span>Total Records: ${rowCount}</span>
                    </div>
                </div>
                ${tableHTML}
                <div class="footer">
                    Inventory Management System | ${timestamp}
                </div>
            </body>
            </html>
        `;

        printWindow.document.write(documentContent);
        printWindow.document.close();

        // Wait for content to load then print
        printWindow.onload = function () {
            setTimeout(() => {
                printWindow.print();
                printWindow.onafterprint = function () {
                    printWindow.close();
                };
            }, 250);
        };
    }

    // Export other tables (categories, departments)
    function exportCategoriesToPDF() {
        // Implementation remains the same as before
        window.exportCategoriesToPDF();
    }

    function exportDepartmentsToPDF() {
        // Implementation remains the same as before
        window.exportDepartmentsToPDF();
    }

    // Public API
    return {
        exportProducts: exportProductsToPDF,
        exportRoutes: exportRoutesToPDF,
        exportCategories: exportCategoriesToPDF,
        exportDepartments: exportDepartmentsToPDF
    };
})();

// Global function references for backward compatibility
window.exportProductsToPDF = PDFExport.exportProducts;
window.exportRoutesToPDF = PDFExport.exportRoutes;
window.exportCategoriesToPDF = PDFExport.exportCategories;
window.exportDepartmentsToPDF = PDFExport.exportDepartments;