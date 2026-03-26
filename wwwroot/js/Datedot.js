function initializeDatePicker(dotNetHelper) {
    const dateInput = document.getElementById('activityDate');

    if (!dateInput) return;


    dateInput.addEventListener('change', function () {
        dotNetHelper.invokeMethodAsync('UpdateSelectedDate', this.value);
    });

    console.log("Date picker initialized");
}


function positionDateDots(availableDates) {
    const dateInput = document.getElementById('activityDate');
    const dotsContainer = document.querySelector('.date-dots');

    if (!dateInput || !dotsContainer) return;

   
    dotsContainer.innerHTML = '';

 
    const rect = dateInput.getBoundingClientRect();
    const inputWidth = rect.width;


    availableDates.forEach(dateStr => {
        const date = new Date(dateStr);
        const day = date.getDate();
        const daysInMonth = new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate();

      
        const positionPercent = (day / daysInMonth) * 100;
        const dot = document.createElement('div');
        dot.className = 'date-dot';
        dot.style.left = `${positionPercent}%`;
        dot.title = date.toLocaleDateString();

        dotsContainer.appendChild(dot);
    });
}