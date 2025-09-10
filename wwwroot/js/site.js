window.renderBarChart = () => {
    console.log("Chart.js function is being called!");
    const ctx = document.getElementById('barChart').getContext('2d');
    new Chart(ctx, {
        type: 'bar',  // Bar chart type
        data: {
            labels: ['January', 'February', 'March', 'April'],  // X-axis labels
            datasets: [{
                label: 'Sample Bar Chart',
                data: [65, 59, 80, 81],  // Data for each bar
                backgroundColor: ['#FF5733', '#33FF57', '#3357FF', '#FF33A6'],  // Background colors for bars
                borderColor: ['#FF5733', '#33FF57', '#3357FF', '#FF33A6'],  // Border colors for bars
                borderWidth: 1
            }]
        },
        options: {
            responsive: true,
            scales: {
                y: {
                    beginAtZero: true // Ensure Y-axis starts from 0
                }
            }
        });
};
