function exportToPDF(data, filename, title) {
    // This is a placeholder - you'll need to implement actual PDF generation
    // For now, we'll create a formatted HTML that can be saved as PDF

    const printWindow = window.open('', '_blank');
    const htmlContent = generatePDFContent(data, title);

    printWindow.document.write(htmlContent);
    printWindow.document.close();

    // Trigger browser's print dialog which allows saving as PDF
    printWindow.onload = function () {
        printWindow.print();
        printWindow.close();
    };
}

function generatePDFContent(data, title) {
    return `
        <!DOCTYPE html>
        <html>
        <head>
            <title>${title}</title>
            <style>
                body { 
                    font-family: Arial, sans-serif; 
                    margin: 20px;
                }
                h1 { 
                    color: #333; 
                    border-bottom: 2px solid #333;
                    padding-bottom: 10px;
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
                    background-color: #f2f2f2; 
                    font-weight: bold;
                }
                tr:nth-child(even) { 
                    background-color: #f9f9f9;
                }
                .footer {
                    margin-top: 30px;
                    text-align: center;
                    font-size: 12px;
                    color: #666;
                }
            </style>
        </head>
        <body>
            <h1>${title}</h1>
            <p>Generated on: ${new Date().toLocaleString()}</p>
            ${data}
            <div class="footer">
                <p>© ${new Date().getFullYear()} Inventory Management System</p>
            </div>
        </body>
        </html>
    `;
}