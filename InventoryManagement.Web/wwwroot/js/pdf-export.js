// Complete PDF Export Module with proper global registration
window.PDFExport = (function () {
    'use strict';

    // Export products to PDF with proper column display
    function exportProductsToPDF() {
        const table = document.getElementById('productsTable');
        if (!table) {
            window.showToast('Products table not found', 'error');
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

    // FIXED: Export routes to PDF with corrected product column parsing
    function exportRoutesToPDF() {
        console.log('exportRoutesToPDF called - function is working!'); // Debug log

        const table = document.getElementById('routesTable');
        if (!table) {
            console.error('Routes table not found'); // Debug log
            window.showToast('Routes table not found', 'error');
            return;
        }

        console.log('Found routes table, processing...'); // Debug log

        const tableClone = table.cloneNode(true);

        // Remove Image and Actions columns from headers
        const headers = tableClone.querySelectorAll('thead th');
        console.log('Found headers:', headers.length); // Debug log

        if (headers[0]) headers[0].remove(); // Image
        if (headers[headers.length - 1]) headers[headers.length - 1].remove(); // Actions

        // Process each row
        const rows = tableClone.querySelectorAll('tbody tr');
        console.log('Processing rows:', rows.length); // Debug log

        rows.forEach((row, index) => {
            const cells = row.querySelectorAll('td');
            console.log(`Row ${index} has ${cells.length} cells`); // Debug log

            // Remove image column (first)
            if (cells[0]) cells[0].remove();

            // Remove actions column (last)  
            const lastCell = row.querySelector('td:last-child');
            if (lastCell) lastCell.remove();

            // Get updated cell list after removals
            const updatedCells = row.querySelectorAll('td');

            // FIXED: Format Product column properly (should be index 2 after removing image)
            const productCell = updatedCells[2];
            if (productCell) {
                console.log(`Processing product cell for row ${index}:`, productCell.innerHTML); // Debug log

                // Create a temporary div to safely parse the HTML
                const tempDiv = document.createElement('div');
                tempDiv.innerHTML = productCell.innerHTML;

                // Extract elements from the original HTML structure  
                const badge = tempDiv.querySelector('.badge');
                const linkOrDiv = tempDiv.querySelector('a, div');
                const categorySmall = tempDiv.querySelector('small.text-muted');

                let inventoryCode = '';
                let vendor = '';
                let category = '';

                // Extract inventory code from badge
                if (badge) {
                    inventoryCode = badge.textContent.trim();
                    console.log('Found inventory code:', inventoryCode);
                }

                // Extract vendor - this is the tricky part
                if (linkOrDiv) {
                    // Clone the element to avoid modifying the original
                    const clonedElement = linkOrDiv.cloneNode(true);

                    // Remove the badge from the cloned element
                    const badgeInCloned = clonedElement.querySelector('.badge');
                    if (badgeInCloned) {
                        badgeInCloned.remove();
                    }

                    // Remove any small elements (they contain category info)
                    const smallElements = clonedElement.querySelectorAll('small');
                    smallElements.forEach(small => small.remove());

                    // Now get the remaining text, which should be the vendor
                    vendor = clonedElement.textContent.trim();
                    console.log('Extracted vendor:', vendor);
                }

                // Extract category from the small element
                if (categorySmall) {
                    // Clone to avoid modifying original
                    const categoryClone = categorySmall.cloneNode(true);
                    // Remove any icons
                    const icons = categoryClone.querySelectorAll('i');
                    icons.forEach(icon => icon.remove());
                    category = categoryClone.textContent.trim();
                    console.log('Found category:', category);
                }

                // Create the properly formatted product details
                productCell.innerHTML = `
                    <div><strong>Code:</strong> ${inventoryCode}</div>
                    <div><strong>Vendor:</strong> ${vendor}</div>
                    <div><strong>Category:</strong> ${category}</div>
                `;

                console.log('Final formatted HTML:', productCell.innerHTML);
            }

            // Clean up From column
            const fromCell = updatedCells[3];
            if (fromCell) {
                const deptDiv = fromCell.querySelector('div');
                const workerSmall = fromCell.querySelector('small');

                const dept = deptDiv ? deptDiv.textContent.trim() : '';
                let worker = '';
                if (workerSmall) {
                    // Remove icons and clean up worker text
                    const workerClone = workerSmall.cloneNode(true);
                    const icons = workerClone.querySelectorAll('i');
                    icons.forEach(icon => icon.remove());
                    worker = workerClone.textContent.trim();
                }

                fromCell.innerHTML = `
                    <div><strong>${dept}</strong></div>
                    ${worker ? `<div style="font-size: 8pt; color: #666;">${worker}</div>` : ''}
                `;
            }

            // Clean up To column
            const toCell = updatedCells[4];
            if (toCell) {
                const deptDiv = toCell.querySelector('div');
                const workerSmall = toCell.querySelector('small');

                const dept = deptDiv ? deptDiv.textContent.trim() : '';
                let worker = '';
                if (workerSmall) {
                    // Remove icons and clean up worker text
                    const workerClone = workerSmall.cloneNode(true);
                    const icons = workerClone.querySelectorAll('i');
                    icons.forEach(icon => icon.remove());
                    worker = workerClone.textContent.trim();
                }

                toCell.innerHTML = `
                    <div><strong>${dept}</strong></div>
                    ${worker ? `<div style="font-size: 8pt; color: #666;">${worker}</div>` : ''}
                `;
            }

            // Clean up Status column
            const statusCell = updatedCells[5];
            if (statusCell) {
                // Create temp div for safe manipulation
                const tempStatusDiv = document.createElement('div');
                tempStatusDiv.innerHTML = statusCell.innerHTML;

                // Remove all icons
                const icons = tempStatusDiv.querySelectorAll('i');
                icons.forEach(icon => icon.remove());

                const badge = tempStatusDiv.querySelector('.badge');
                const smallDate = tempStatusDiv.querySelector('small');

                let statusText = '';
                let dateText = '';

                if (badge) {
                    statusText = badge.textContent.trim();
                }

                if (smallDate) {
                    dateText = smallDate.textContent.replace('Created:', '').trim();
                }

                statusCell.innerHTML = `
                    <div><strong>${statusText}</strong></div>
                    ${dateText ? `<div style="font-size: 8pt; color: #666;">${dateText}</div>` : ''}
                `;
            }
        });

        // Custom styles for routes PDF
        const customStyles = `
            #routesPdfTable {
                table-layout: fixed;
                width: 100%;
            }
            #routesPdfTable th:nth-child(1) { width: 12%; }  /* Date */
            #routesPdfTable th:nth-child(2) { width: 10%; }  /* Type */
            #routesPdfTable th:nth-child(3) { width: 28%; }  /* Product */
            #routesPdfTable th:nth-child(4) { width: 17%; }  /* From */
            #routesPdfTable th:nth-child(5) { width: 17%; }  /* To */
            #routesPdfTable th:nth-child(6) { width: 16%; }  /* Status */
            
            #routesPdfTable td {
                vertical-align: top;
                padding: 6px 4px;
                font-size: 9pt;
                word-wrap: break-word;
            }
        `;

        tableClone.id = 'routesPdfTable';
        console.log('Calling generatePDF...'); // Debug log
        generatePDF(tableClone.outerHTML, 'routes_export.pdf', 'Routes Report', customStyles);
    }

    // Generate PDF from HTML with enhanced error handling
    function generatePDF(tableHTML, filename, title, customStyles) {
        try {
            console.log('generatePDF called with title:', title); // Debug log

            const printWindow = window.open('', '_blank');

            if (!printWindow) {
                window.showToast('Popup blocked. Please allow popups for this site to export PDF.', 'error');
                return;
            }

            const styles = `
                <style>
                    @page { 
                        size: landscape;
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
                        overflow-wrap: break-word;
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
                    td div {
                        word-break: break-word;
                        hyphens: auto;
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
                }, 500);
            };

            // Fallback for browsers that don't support onload
            setTimeout(() => {
                if (printWindow && !printWindow.closed) {
                    printWindow.print();
                }
            }, 1000);

        } catch (error) {
            console.error('Error generating PDF:', error);
            window.showToast('Failed to generate PDF. Please try again.', 'error');
        }
    }

    // Public API
    return {
        exportProducts: exportProductsToPDF,
        exportRoutes: exportRoutesToPDF
    };
})();

// CRITICAL: Global function registration for backward compatibility
// This is what makes the functions available to onclick handlers
window.exportProductsToPDF = function () {
    console.log('Global exportProductsToPDF called'); // Debug log
    return PDFExport.exportProducts();
};

window.exportRoutesToPDF = function () {
    console.log('Global exportRoutesToPDF called'); // Debug log  
    return PDFExport.exportRoutes();
};

// Alternative function name that matches your HTML button
window.exportRouteToPDF = function () {
    console.log('Global exportRouteToPDF called (alternative name)'); // Debug log
    return PDFExport.exportRoutes();
};

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    console.log('PDF Export module loaded and global functions registered');

    // Test that functions are available
    if (typeof window.exportRouteToPDF === 'function') {
        console.log('✓ exportRouteToPDF is available globally');
    } else {
        console.error('✗ exportRouteToPDF is NOT available globally');
    }
});