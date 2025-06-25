// Chart.js configurations and utilities
const chartColors = {
    primary: '#3B82F6',
    success: '#10B981',
    warning: '#F59E0B',
    danger: '#EF4444',
    info: '#06B6D4',
    secondary: '#6B7280',
    dark: '#1F2937',
    light: '#F3F4F6'
};

// Default chart options
const defaultChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
        legend: {
            position: 'bottom',
            labels: {
                padding: 20,
                usePointStyle: true,
                font: {
                    size: 12
                }
            }
        },
        tooltip: {
            backgroundColor: 'rgba(0, 0, 0, 0.8)',
            padding: 12,
            cornerRadius: 8,
            titleFont: {
                size: 14,
                weight: 'bold'
            },
            bodyFont: {
                size: 13
            }
        }
    }
};

// Initialize dashboard charts
function initializeDashboardCharts() {
    // Products by Department Chart
    createDepartmentChart();

    // Category Distribution Chart
    createCategoryChart();

    // Monthly Transfers Chart
    createTransfersChart();

    // Worker Performance Chart
    createWorkerChart();
}

function createDepartmentChart() {
    const ctx = document.getElementById('departmentChart');
    if (!ctx) return;

    $.get('/api/statistics/departments', function (data) {
        new Chart(ctx, {
            type: 'bar',
            data: {
                labels: data.map(d => d.name),
                datasets: [{
                    label: 'Product Count',
                    data: data.map(d => d.productCount),
                    backgroundColor: chartColors.primary,
                    borderColor: chartColors.primary,
                    borderWidth: 0,
                    borderRadius: 8,
                    maxBarThickness: 50
                }, {
                    label: 'Active Workers',
                    data: data.map(d => d.workerCount),
                    backgroundColor: chartColors.success,
                    borderColor: chartColors.success,
                    borderWidth: 0,
                    borderRadius: 8,
                    maxBarThickness: 50
                }]
            },
            options: {
                ...defaultChartOptions,
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            stepSize: 5
                        }
                    }
                }
            }
        });
    });
}

function createCategoryChart() {
    const ctx = document.getElementById('categoryChart');
    if (!ctx) return;

    $.get('/api/statistics/categories', function (data) {
        new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: data.map(c => c.name),
                datasets: [{
                    data: data.map(c => c.count),
                    backgroundColor: [
                        chartColors.primary,
                        chartColors.success,
                        chartColors.warning,
                        chartColors.danger,
                        chartColors.info,
                        chartColors.secondary
                    ],
                    borderWidth: 0
                }]
            },
            options: {
                ...defaultChartOptions,
                cutout: '60%',
                plugins: {
                    ...defaultChartOptions.plugins,
                    datalabels: {
                        display: true,
                        color: '#fff',
                        font: {
                            weight: 'bold',
                            size: 14
                        },
                        formatter: (value, context) => {
                            const sum = context.dataset.data.reduce((a, b) => a + b, 0);
                            const percentage = ((value / sum) * 100).toFixed(1);
                            return percentage + '%';
                        }
                    }
                }
            }
        });
    });
}

function createTransfersChart() {
    const ctx = document.getElementById('transfersChart');
    if (!ctx) return;

    $.get('/api/statistics/transfers/monthly', function (data) {
        new Chart(ctx, {
            type: 'line',
            data: {
                labels: data.map(d => d.month),
                datasets: [{
                    label: 'Completed',
                    data: data.map(d => d.completed),
                    borderColor: chartColors.success,
                    backgroundColor: chartColors.success + '20',
                    tension: 0.3,
                    fill: true
                }, {
                    label: 'Pending',
                    data: data.map(d => d.pending),
                    borderColor: chartColors.warning,
                    backgroundColor: chartColors.warning + '20',
                    tension: 0.3,
                    fill: true
                }]
            },
            options: {
                ...defaultChartOptions,
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            }
        });
    });
}

function createWorkerChart() {
    const ctx = document.getElementById('workerChart');
    if (!ctx) return;

    $.get('/api/statistics/workers/performance', function (data) {
        const topWorkers = data.slice(0, 10); // Top 10 workers

        new Chart(ctx, {
            type: 'horizontalBar',
            data: {
                labels: topWorkers.map(w => w.name),
                datasets: [{
                    label: 'Products Assigned',
                    data: topWorkers.map(w => w.productCount),
                    backgroundColor: chartColors.info,
                    borderRadius: 4
                }]
            },
            options: {
                ...defaultChartOptions,
                indexAxis: 'y',
                scales: {
                    x: {
                        beginAtZero: true
                    }
                }
            }
        });
    });
}

// Export chart as image
function exportChart(chartId, filename) {
    const chart = Chart.getChart(chartId);
    if (chart) {
        const url = chart.toBase64Image();
        const link = document.createElement('a');
        link.download = filename || 'chart.png';
        link.href = url;
        link.click();
    }
}

// Refresh chart data
function refreshChart(chartId, endpoint) {
    const chart = Chart.getChart(chartId);
    if (chart) {
        $.get(endpoint, function (data) {
            chart.data = data;
            chart.update();
        });
    }
}

// Initialize real-time chart updates
function initializeRealTimeCharts() {
    // Update charts every 30 seconds
    setInterval(function () {
        refreshChart('departmentChart', '/api/statistics/departments');
        refreshChart('transfersChart', '/api/statistics/transfers/monthly');
    }, 30000);
}

// Custom chart plugin for background patterns
Chart.register({
    id: 'customBackground',
    beforeDraw: (chart, args, options) => {
        const { ctx } = chart;
        ctx.save();
        ctx.globalCompositeOperation = 'destination-over';
        ctx.fillStyle = options.color || '#f8f9fa';
        ctx.fillRect(0, 0, chart.width, chart.height);
        ctx.restore();
    }
});