// Chart.js Interop for Blazor Server
window.chartInterop = (function () {
    let chartInstance = null;

    function createChart(canvasId, labels, data) {
        return new Promise((resolve, reject) => {
            try {
                const timeout = setTimeout(() => {
                    reject(new Error('Chart creation timeout'));
                }, 5000);

                const ctx = document.getElementById(canvasId);
                if (!ctx) {
                    clearTimeout(timeout);
                    console.error('Canvas element not found:', canvasId);
                    reject(new Error('Canvas element not found: ' + canvasId));
                    return;
                }

                // Destroy existing chart if it exists
                if (chartInstance) {
                    try {
                        chartInstance.destroy();
                        chartInstance = null;
                    } catch (e) {
                        console.warn('Error destroying previous chart:', e);
                    }
                }

                chartInstance = new Chart(ctx, {
                    type: 'bar',
                    data: {
                        labels: labels,
                        datasets: [{
                            label: 'Usage Statistics',
                            data: data,
                            backgroundColor: [
                                'rgba(54, 162, 235, 0.5)',
                                'rgba(75, 192, 192, 0.5)',
                                'rgba(255, 206, 86, 0.5)'
                            ],
                            borderColor: [
                                'rgba(54, 162, 235, 1)',
                                'rgba(75, 192, 192, 1)',
                                'rgba(255, 206, 86, 1)'
                            ],
                            borderWidth: 2
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        scales: {
                            y: {
                                beginAtZero: true,
                                ticks: {
                                    stepSize: 1
                                }
                            }
                        },
                        plugins: {
                            legend: {
                                display: false
                            }
                        }
                    }
                });

                clearTimeout(timeout);
                console.log('Chart created successfully:', canvasId);
                resolve();
            } catch (error) {
                console.error('Chart creation error:', error);
                reject(error);
            }
        });
    }

    function destroyChart() {
        try {
            if (chartInstance) {
                chartInstance.destroy();
                chartInstance = null;
                console.log('Chart destroyed');
            }
        } catch (error) {
            console.error('Error destroying chart:', error);
        }
    }

    // Public API
    return {
        createChart: createChart,
        destroyChart: destroyChart
    };
})();

console.log('Chart interop loaded');

