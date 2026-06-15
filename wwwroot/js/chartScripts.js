// Helper function definition for theme color determination (must be available globally or imported)
const getTextColor = (isDarkTheme) => {
    // CORRECTED LOGIC
    // If it's a Dark Theme (isDarkTheme = true), use LIGHT text color for dark backgrounds
    if (isDarkTheme === true) return '#f5f5f5';

    // If it's a Light Theme (isDarkTheme = false), use DARK text color for light backgrounds
    return '#212529';
};

const formatAxisLabel = (label) => {
    if (!label) return '';
    const today = new Date().toISOString().split('T')[0];
    if (label === today) return 'Today';
    const yesterday = new Date(Date.now() - 86400000).toISOString().split('T')[0];
    if (label === yesterday) return 'Yesterday';
    const parts = label.split('-');
    if (parts.length === 3) {
        const month = new Date(label).toLocaleString('en', { month: 'short' });
        const day = parseInt(parts[2], 10);
        return `${month} ${day}`;
    }
    return label;
};

// Accepts five arguments: labels, total data, afk data, active data, and isInitialDarkTheme (replacing the old textColor arg).
window.renderGroupedBarChart = (labels, totalData, afkData, activeData, isInitialDarkTheme) => {
    const canvas = document.getElementById('barChart');
    const ctx = canvas.getContext('2d');

    // Auto-detect theme if not provided
    if (isInitialDarkTheme === undefined || isInitialDarkTheme === null) {
        isInitialDarkTheme = document.body.classList.contains('dark-theme');
    }

    // Determine the initial color using the function
    const initialTextColor = getTextColor(isInitialDarkTheme);

    // 🚨 FIX: Set the default font family and size 🚨
    if (typeof Chart !== 'undefined' && Chart.defaults) {
        Chart.defaults.font.family = 'Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
        Chart.defaults.font.size = 14; // Increased font size for better visibility
        Chart.defaults.devicePixelRatio = window.devicePixelRatio || 1; // Ensure crisp rendering
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

    // Plugin for chart background fill
    const chartBackgroundPlugin = {
        id: 'chartBackground',
        beforeDraw(chart) {
            const { ctx, chartArea } = chart;
            if (!chartArea) return;
            ctx.save();
            ctx.fillStyle = chart._chartBackgroundColor || '#ffffff';
            ctx.fillRect(chartArea.left, chartArea.top, chartArea.right - chartArea.left, chartArea.bottom - chartArea.top);
            ctx.restore();
        },
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
            resizeDelay: 200, // Prevent rapid resizing that can stretch text
            animation: {
                duration: 500 // Smoother animation
            },
            layout: {
                padding: 10
            },
            scales: {
                x: {
                    grid: { display: false, },
                    title: { 
                        display: true, 
                        text: 'Date', 
                        color: initialTextColor,
                        font: { 
                            size: 15, 
                            weight: 'bold',
                            family: 'Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif'
                        }
                    },
                    ticks: { 
                        color: initialTextColor,
                        font: { 
                            size: 13,
                            family: 'Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif'
                        },
                        callback: function(value, index) {
                            return formatAxisLabel(labels[index]);
                        }
                    },
                    // ⭐️ ADDED: Set initial X-axis line color ⭐️
                    border: { color: initialTextColor },
                    stacked: false
                },
                y: {
                    display: true,
                    beginAtZero: true,
                    grid: { display: true, color: isInitialDarkTheme ? '#444444' : '#e2e8f0', drawBorder: false },
                    title: { 
                        display: true, 
                        text: 'Duration (Minutes)', 
                        color: initialTextColor,
                        font: { 
                            size: 15, 
                            weight: 'bold',
                            family: 'Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif'
                        }
                    },
                    ticks: { 
                        color: initialTextColor,
                        font: { 
                            size: 13,
                            family: 'Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif'
                        }
                    },
                    // ⭐️ ADDED: Set initial Y-axis line color ⭐️
                    border: { color: initialTextColor }
                }
            },
            plugins: {
                legend: {
                    display: true,
                    position: 'right',
                    labels: {
                        color: initialTextColor,
                        font: { 
                            size: 14, 
                            weight: '500',
                            family: 'Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif'
                        },
                        padding: 15,
                        usePointStyle: true // Better legend display
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
                            const dataValue = Math.round(tooltipItem.raw * 60);
                            const hours = Math.floor(dataValue / 3600);
                            const minutes = Math.floor((dataValue % 3600) / 60);
                            const seconds = dataValue % 60;
                            return `${tooltipItem.dataset.label}: ${hours}h ${minutes}m ${seconds}s`;
                        }
                    }
                }
            },
            hover: {
                mode: 'index',
                intersect: true
            }
        },
        plugins: [roundedBar, chartBackgroundPlugin]
    });

    window.barChartInstance._chartBackgroundColor = isInitialDarkTheme ? '#1e1e1e' : '#ffffff';
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

        // Update grid line colors
        chart.options.scales.y.grid.color = isDarkTheme ? '#444444' : '#e2e8f0';

        // Update chart background fill for the plugin
        chart._chartBackgroundColor = isDarkTheme ? '#1e1e1e' : '#ffffff';

        // Apply the changes to the chart
        chart.update();

        console.log('Chart theme updated for theme change (isDarkTheme:', isDarkTheme, ')');
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