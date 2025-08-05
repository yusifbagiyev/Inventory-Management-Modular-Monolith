// Simple PDF export using browser print functionality
function exportToPDF(tableHtml, filename, title) {
    // Create a new window for printing
    const printWindow = window.open('', '_blank');

    const css = `
        <style>
            body { font-family: Arial, sans-serif; }
            table { width: 100%; border-collapse: collapse; margin-top: 20px; }
            th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
            th { background-color: #f8f9fa; font-weight: bold; }
            tr:nth-child(even) { background-color: #f2f2f2; }
            h1 { text-align: center; color: #333; }
            .no-print { display: none; }
            @media print {
                body { margin: 0; }
                table { page-break-inside: auto; }
                tr { page-break-inside: avoid; page-break-after: auto; }
            }
        </style>
    `;

    const content = `
        <!DOCTYPE html>
        <html>
        <head>
            <title>${title}</title>
            ${css}
        </head>
        <body>
            <h1>${title}</h1>
            <p>Generated on: ${new Date().toLocaleString()}</p>
            ${tableHtml}
        </body>
        </html>
    `;

    printWindow.document.write(content);
    printWindow.document.close();

    // Wait for content to load then print
    printWindow.onload = function () {
        printWindow.print();
        // Close the window after printing
        printWindow.onafterprint = function () {
            printWindow.close();
        };
    };
}

function exportProductsToPDF() {
    const table = document.getElementById('productsTable').cloneNode(true);

    // Remove action columns
    const headers = table.querySelectorAll('th');
    const actionIndex = Array.from(headers).findIndex(th => th.textContent.includes('Actions'));
    if (actionIndex > -1) {
        headers[actionIndex].remove();
        table.querySelectorAll('tr').forEach(row => {
            const cells = row.querySelectorAll('td');
            if (cells[actionIndex]) cells[actionIndex].remove();
        });
    }

    // Remove checkboxes
    table.querySelectorAll('input[type="checkbox"]').forEach(cb => {
        cb.closest('th, td').remove();
    });

    exportToPDF(table.outerHTML, 'products_export.pdf', 'Product Inventory Report');
}

function exportRoutesToPDF() {
    const table = document.getElementById('routesTable').cloneNode(true);

    // Remove action and checkbox columns
    table.querySelectorAll('.actions-column').forEach(el => el.remove());
    table.querySelectorAll('input[type="checkbox"]').forEach(cb => {
        cb.closest('th, td').remove();
    });

    exportToPDF(table.outerHTML, 'routes_export.pdf', 'Routes Report');
}