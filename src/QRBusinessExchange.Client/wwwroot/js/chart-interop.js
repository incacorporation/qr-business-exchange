// Chart.js Interop for Blazor WebAssembly
window.chartInterop = {
    chartInstance: null,

    createChart: function (canvasId, labels, data) {
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

                // Check if Chart.js is loaded
                if (typeof Chart === 'undefined') {
                    clearTimeout(timeout);
                    console.error('Chart.js library not loaded');
                    reject(new Error('Chart.js library not loaded'));
                    return;
                }

                // Destroy existing chart if it exists
                if (this.chartInstance) {
                    try {
                        this.chartInstance.destroy();
                        this.chartInstance = null;
                    } catch (e) {
                        console.warn('Error destroying previous chart:', e);
                    }
                }

                // Brand palette (matches wwwroot/css/app.css): one series = one color.
                // The most recent bar is full brand blue; earlier bars use the lighter periwinkle.
                const brand = '#1F43AD';
                const brandSoft = '#AFC0F2';
                const ink = '#5A6480';
                const grid = '#E3E7F0';
                const lastIndex = (data || []).length - 1;
                const barColors = (data || []).map((_, i) => i === lastIndex ? brand : brandSoft);

                Chart.defaults.font.family = "'Inter', 'Segoe UI', system-ui, sans-serif";
                Chart.defaults.color = ink;

                // Create new chart
                this.chartInstance = new Chart(ctx, {
                    type: 'bar',
                    data: {
                        labels: labels,
                        datasets: [{
                            label: 'Usage Statistics',
                            data: data,
                            backgroundColor: barColors,
                            hoverBackgroundColor: brand,
                            borderWidth: 0,
                            borderRadius: 6,
                            maxBarThickness: 56
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        scales: {
                            x: {
                                grid: { display: false },
                                border: { color: grid }
                            },
                            y: {
                                beginAtZero: true,
                                border: { display: false },
                                grid: { color: grid },
                                ticks: {
                                    stepSize: 1,
                                    precision: 0
                                }
                            }
                        },
                        plugins: {
                            legend: {
                                display: false
                            },
                            tooltip: {
                                backgroundColor: '#0B1638',
                                padding: 10,
                                cornerRadius: 8,
                                displayColors: false
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
    },

    destroyChart: function () {
        try {
            if (this.chartInstance) {
                this.chartInstance.destroy();
                this.chartInstance = null;
                console.log('Chart destroyed');
            }
        } catch (error) {
            console.error('Error destroying chart:', error);
        }
    }
};

console.log('Chart interop loaded');

