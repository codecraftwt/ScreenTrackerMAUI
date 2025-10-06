window.initializeSelect2 = function (elementId, componentInstance) {
    const maxRetries = 20;
    let retries = 0;

    const interval = setInterval(() => {
        var element = $(`#${elementId}`);
        if (element.length) {
            clearInterval(interval);
            element.select2();
            console.log('Select2 successfully initialized for:', elementId);

           
            element.on('change', function () {
                var value = $(this).val();
                componentInstance.invokeMethodAsync('OnSelect2Changed', elementId, value);
            });
        } else if (retries >= maxRetries) {
            clearInterval(interval);
            console.error(`Failed to initialize Select2 for element: ${elementId} after ${maxRetries} retries.`);
        }
        retries++;
    }, 50);
};

