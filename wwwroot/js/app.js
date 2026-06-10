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

window.setSelect2Value = function (elementId, value) {
    var element = $(`#${elementId}`);
    if (element.length) {
        element.val(value).trigger('change');
        console.log('Select2 value set for:', elementId, 'to:', value);
    } else {
        console.error('Element not found for Select2:', elementId);
    }
};

