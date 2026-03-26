// Helper function definition for theme color determination (must be available globally or imported)
const getTextColor = (isDarkTheme) => {
    // ⭐️ INVERTED LOGIC ⭐️
    // If it's a Dark Theme (isDarkTheme = true), use DARK text color
    if (isDarkTheme === true) return '#444444';

    // If it's a Light Theme (isDarkTheme = false), use LIGHT text color
    return '#c9c9c9';
};


// Accepts five arguments: labels, total data, afk data, active data, and isInitialDarkTheme (replacing the old textColor arg).
window.renderGroupedBarChart = (labels, totalData, afkData, activeData, isInitialDarkTheme) => {
    const canvas = document.getElementById('barChart');
    const ctx = canvas.getContext('2d');

    // Determine the initial color using the function
    const initialTextColor = getTextColor(isInitialDarkTheme);

    // 🚨 FIX: Set the default font family 🚨
    if (typeof Chart !== 'undefined' && Chart.defaults) {
        Chart.defaults.font.family = 'Inter, sans-serif';
    }

    if (!canvas || !ctx) {
        console.error('Canvas element or context not found! Check Blazor component rendering order.');
        return;
    }

    // Destroy the previous chart if it exists
    if (window.barChartInstance) {
        window.barChartInstance.destroy();
    }

    const today = new Date().toISOString().split('T')[0];

    // Define colors for the three datasets (matching your visual requirements)
    const COLORS = {
        total: '#fdbb2d',  // Orange/Yellow (Total Time)
        afk: '#27ae60',    // Green (AFK Time)
        active: '#3498db'  // Blue (Active Time)
    };

    // Plugin 1: for drawing rounded tops on bars
    const roundedBar = {
        id: 'roundedBar',
        beforeDatasetsDraw(chart) {
            const { ctx, data } = chart;
            ctx.save();

            data.datasets.forEach((dataset, datasetIndex) => {
                chart.getDatasetMeta(datasetIndex).data.forEach((bar) => {
                    if (bar.y >= bar.base) return;

                    const radius = 4;
                    const barX = bar.x - bar.width / 2;
                    const barY = bar.y;
                    const barWidth = bar.width;
                    const barHeight = bar.base - bar.y;

                    ctx.beginPath();
                    ctx.fillStyle = dataset.backgroundColor;

                    ctx.moveTo(barX, barY + radius);
                    ctx.arcTo(barX, barY, barX + barWidth, barY, radius);
                    ctx.arcTo(barX + barWidth, barY, barX + barWidth, barY + radius, radius);
                    ctx.lineTo(barX + barWidth, barY + barHeight);
                    ctx.lineTo(barX, barY + barHeight);

                    ctx.closePath();
                    ctx.fill();
                });
            });
            ctx.restore();
        }
    };

    // Create the grouped bar chart
    window.barChartInstance = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [
                {
                    label: 'Total Time (Minutes)',
                    data: totalData,
                    backgroundColor: COLORS.total,
                    barThickness: 15,
                    categoryPercentage: 0.8,
                    barPercentage: 0.9,
                },
                {
                    label: 'AFK Time (Minutes)',
                    data: afkData,
                    backgroundColor: COLORS.afk,
                    barThickness: 15,
                    categoryPercentage: 0.8,
                    barPercentage: 0.9,
                },
                {
                    label: 'Active Time (Minutes)',
                    data: activeData,
                    backgroundColor: COLORS.active,
                    barThickness: 15,
                    categoryPercentage: 0.8,
                    barPercentage: 0.9,
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                x: {
                    grid: { display: false, },
                    title: { display: true, text: 'Date', color: initialTextColor },
                    ticks: { color: initialTextColor },
                    // ⭐️ ADDED: Set initial X-axis line color ⭐️
                    border: { color: initialTextColor },
                    stacked: false
                },
                y: {
                    display: true,
                    beginAtZero: true,
                    grid: { display: false, },
                    title: { display: true, text: 'Duration (Minutes)', color: initialTextColor },
                    ticks: { color: initialTextColor },
                    // ⭐️ ADDED: Set initial Y-axis line color ⭐️
                    border: { color: initialTextColor }
                }
            },
            plugins: {
                legend: {
                    display: true,
                    position: 'right',
                    labels: {
                        color: initialTextColor
                    }
                },
                tooltip: {
                    enabled: true,
                    position: 'nearest',
                    // Set initial tooltip background for contrast
                    backgroundColor: isInitialDarkTheme ? 'rgba(30, 30, 30, 0.95)' : 'rgba(0, 0, 0, 0.8)',
                    titleColor: 'white',
                    bodyColor: 'white',

                    callbacks: {
                        title: (tooltipItems) => {
                            return tooltipItems[0].label === today ? "Today" : tooltipItems[0].label;
                        },
                        label: (tooltipItem) => {
                            const dataValue = tooltipItem.raw;
                            return `${tooltipItem.dataset.label}: ${dataValue.toFixed(2)} minutes`;
                        }
                    }
                }
            },
            hover: {
                mode: 'index',
                intersect: true
            }
        },
        plugins: [roundedBar]
    });
};

// ⭐️ NEW GLOBAL FUNCTION: Updates the chart's text color immediately on theme change. ⭐️
window.updateChartThemeColor = (isDarkTheme) => {
    const chart = window.barChartInstance;
    if (chart) {
        const newColor = getTextColor(isDarkTheme);

        // Update the legend label color
        chart.options.plugins.legend.labels.color = newColor;

        // Update X-axis colors
        chart.options.scales.x.title.color = newColor;
        chart.options.scales.x.ticks.color = newColor;
        // ⭐️ ADDED: Update X-axis line color ⭐️
        chart.options.scales.x.border.color = newColor;

        // Update Y-axis colors
        chart.options.scales.y.title.color = newColor;
        chart.options.scales.y.ticks.color = newColor;
        // ⭐️ ADDED: Update Y-axis line color ⭐️
        chart.options.scales.y.border.color = newColor;

        // Update tooltip background for maximum contrast with the new theme
        chart.options.plugins.tooltip.backgroundColor = isDarkTheme ? 'rgba(30, 30, 30, 0.95)' : 'rgba(0, 0, 0, 0.8)';

        // Apply the changes to the chart
        chart.update();

        console.log('Chart text color updated for theme change to:', newColor, '(isDarkTheme:', isDarkTheme, ')');
    }
};

// **Helper function for development/testing (Keep this if you need local debugging, otherwise remove)**
function generateDynamicData(startDate) {
    const labels = [];
    const totalData = [];
    const afkData = [];
    const activeData = [];
    let currentDate = new Date(startDate);
    const today = new Date();

    while (currentDate.setHours(0, 0, 0, 0) <= today.setHours(0, 0, 0, 0)) {
        const dateString = currentDate.toISOString().split('T')[0];
        labels.push(dateString);

        // Random data generation for demonstration
        const total = Math.floor(Math.random() * 200) + 50;
        const afk = Math.floor(Math.random() * total * 0.4);
        const active = total - afk;

        totalData.push(total);
        afkData.push(afk);
        activeData.push(active);

        currentDate.setDate(currentDate.getDate() + 1);
    }

    return { labels, totalData, afkData, activeData };
}