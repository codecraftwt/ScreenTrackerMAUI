function initializeDatePicker(dotNetHelper) {
    const dateInput = document.getElementById('activityDate');

    if (!dateInput) return;

    // Add event listener for when the date changes
    dateInput.addEventListener('change', function () {
        dotNetHelper.invokeMethodAsync('UpdateSelectedDate', this.value);
    });

    // Add custom styling to show which dates have data
    // This is a simplified version - in a real app you might want to use a proper date picker library
    console.log("Date picker initialized");
}

// Function to position dots under the correct dates
function positionDateDots(availableDates) {
    const dateInput = document.getElementById('activityDate');
    const dotsContainer = document.querySelector('.date-dots');

    if (!dateInput || !dotsContainer) return;

    // Clear existing dots
    dotsContainer.innerHTML = '';

    // Get the position and width of the date input
    const rect = dateInput.getBoundingClientRect();
    const inputWidth = rect.width;

    // Calculate positions for dots (simplified approach)
    availableDates.forEach(dateStr => {
        const date = new Date(dateStr);
        const day = date.getDate();
        const daysInMonth = new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate();

        // Calculate position as percentage of month
        const positionPercent = (day / daysInMonth) * 100;

        // Create dot element
        const dot = document.createElement('div');
        dot.className = 'date-dot';
        dot.style.left = `${positionPercent}%`;
        dot.title = date.toLocaleDateString();

        dotsContainer.appendChild(dot);
    });
}