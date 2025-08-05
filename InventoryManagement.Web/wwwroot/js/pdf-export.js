function exportToPDF(tableHTML, filename, title) {
    const printWindow = window.open('', '_blank');

    // Enhanced styles for 4-column layout
    const styles = `
        <style>
            @page { 
                size: landscape; 
                margin: 1cm;
            }
            body { 
                font-family: Arial, sans-serif; 
                font-size: 12px;
            }
            h1 { 
                text-align: center; 
                margin-bottom: 20px;
            }
            table { 
                width: 100%; 
                border-collapse: collapse;
                table-layout: fixed;
            }
            /* Column width distribution */
            th:nth-child(1) { width: 15%; }  /* Code */
            th:nth-child(2) { width: 35%; }  /* Product Details */
            th:nth-child(3) { width: 30%; }  /* Location */
            th:nth-child(4) { width: 20%; }  /* Status */
            
            th, td { 
                border: 1px solid #ddd; 
                padding: 8px; 
                text-align: left; 
                word-wrap: break-word;
            }
            th { 
                background-color: #f8f9fa; 
                font-weight: bold; 
            }
            tr:nth-child(even) { 
                background-color: #f2f2f2; 
            }
            .badge {
                padding: 2px 6px;
                border-radius: 3px;
                font-size: 10px;
                font-weight: bold;
                display: inline-block;
                margin: 2px;
            }
            .bg-success { background-color: #28a745; color: white; }
            .bg-danger { background-color: #dc3545; color: white; }
            .bg-info { background-color: #17a2b8; color: white; }
            .bg-warning { background-color: #ffc107; color: black; }
            .bg-secondary { background-color: #6c757d; color: white; }
            @media print {
                body { margin: 0; }
            }
        </style>
    `;

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
            <h1>${title}</h1>
            <p>Generated on: ${new Date().toLocaleString()}</p>
            ${tableHTML}
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
        }, 250);
    };
}