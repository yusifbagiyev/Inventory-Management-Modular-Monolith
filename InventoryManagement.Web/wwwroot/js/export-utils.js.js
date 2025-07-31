function exportTableToCSV(tableId, filename) {
    const table = document.getElementById(tableId);
    const rows = table.querySelectorAll('tr');
    let csv = [];

    for (let i = 0; i < rows.length; i++) {
        const row = [];
        const cols = rows[i].querySelectorAll('td, th');

        for (let j = 0; j < cols.length; j++) {
            // Skip action columns
            if (!cols[j].classList.contains('actions-column')) {
                let data = cols[j].innerText.replace(/(\r\n|\n|\r)/gm, '').trim();
                data = data.replace(/"/g, '""');
                row.push('"' + data + '"');
            }
        }
        csv.push(row.join(','));
    }

    const csvContent = csv.join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename + '.csv';
    a.click();
    window.URL.revokeObjectURL(url);
}

function printTable(tableId, title) {
    const printWindow = window.open('', '_blank');
    const table = document.getElementById(tableId).outerHTML;

    printWindow.document.write(`
        <html>
        <head>
            <title>${title}</title>
            <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css">
            <style>
                body { font-family: Arial, sans-serif; }
                .actions-column, .no-print { display: none !important; }
                table { width: 100%; }
                @media print {
                    .no-print { display: none !important; }
                }
            </style>
        </head>
        <body>
            <h2>${title}</h2>
            <p>Printed on: ${new Date().toLocaleString()}</p>
            ${table}
            <script>
                window.onload = function() {
                    window.print();
                    window.close();
                }
            </script>
        </body>
        </html>
    `);
}