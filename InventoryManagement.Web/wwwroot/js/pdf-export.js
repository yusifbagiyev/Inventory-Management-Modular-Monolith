function exportTableToPDF(tableId, filename, title) {
    const table = document.getElementById(tableId);
    if (!table) {
        showToast('Table not found for export', 'error');
        return;
    }

    // Clone the table
    const tableClone = table.cloneNode(true);

    // Remove unwanted columns (checkboxes, actions)
    const headersToRemove = ['Actions', 'Select'];
    const headers = tableClone.querySelectorAll('th');
    const indicesToRemove = [];

    headers.forEach((header, index) => {
        const headerText = header.textContent.trim();
        if (headersToRemove.some(h => headerText.includes(h)) ||
            header.querySelector('input[type="checkbox"]')) {
            indicesToRemove.push(index);
            header.remove();
        }
    });

    // Remove corresponding cells
    tableClone.querySelectorAll('tr').forEach(row => {
        const cells = row.querySelectorAll('td');
        indicesToRemove.reverse().forEach(index => {
            if (cells[index]) cells[index].remove();
        });
    });

    // Remove any remaining checkboxes
    tableClone.querySelectorAll('input[type="checkbox"]').forEach(cb => {
        cb.closest('td, th')?.remove();
    });

    // Create print window
    const printWindow = window.open('', '_blank');

    const css = `
        <style>
            @page { 
                size: landscape; 
                margin: 1cm;
            }
            body { 
                font-family: Arial, sans-serif; 
                font-size: 12px;
                color: #333;
            }
            h1 { 
                text-align: center; 
                color: #333; 
                margin-bottom: 20px;
            }
            .header-info {
                text-align: center;
                margin-bottom: 20px;
                color: #666;
            }
            table { 
                width: 100%; 
                border-collapse: collapse; 
                margin-top: 20px; 
            }
            th, td { 
                border: 1px solid #ddd; 
                padding: 8px; 
                text-align: left; 
            }
            th { 
                background-color: #f8f9fa; 
                font-weight: bold; 
                color: #495057;
            }
            tr:nth-child(even) { 
                background-color: #f2f2f2; 
            }
            .badge {
                padding: 2px 6px;
                border-radius: 3px;
                font-size: 10px;
                font-weight: bold;
            }
            .bg-primary { background-color: #007bff; color: white; }
            .bg-success { background-color: #28a745; color: white; }
            .bg-warning { background-color: #ffc107; color: black; }
            .bg-danger { background-color: #dc3545; color: white; }
            .bg-info { background-color: #17a2b8; color: white; }
            .no-print { display: none !important; }
            @media print {
                body { margin: 0; }
                table { page-break-inside: auto; }
                tr { page-break-inside: avoid; page-break-after: auto; }
                thead { display: table-header-group; }
            }
        </style>
    `;

    const content = `
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="UTF-8">
            <title>${title}</title>
            ${css}
        </head>
        <body>
            <h1>${title}</h1>
            <div class="header-info">
                <p>Generated on: ${new Date().toLocaleString('en-US', {
        timeZone: 'Asia/Baku',
        year: 'numeric',
        month: 'long',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    })}</p>
                <p>Total Records: ${tableClone.querySelectorAll('tbody tr').length}</p>
            </div>
            ${tableClone.outerHTML}
        </body>
        </html>
    `;

    printWindow.document.write(content);
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

function exportProductsToPDF() {
    exportTableToPDF('productsTable', 'products_export.pdf', 'Product Inventory Report');
}

function exportRoutesToPDF() {
    exportTableToPDF('routesTable', 'routes_export.pdf', 'Routes Report');
}