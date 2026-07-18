// Force reload images with cache buster
window.forceReloadImages = function () {
    try {
        var images = document.querySelectorAll('img[data-img-id]');
        console.log('[JS] Force reloading ' + images.length + ' images');

        images.forEach(function (img, index) {
            var src = img.getAttribute('src');
            if (src && src.startsWith('http')) {
                // Add cache buster to force reload
                var separator = src.indexOf('?') === -1 ? '?' : '&';
                var newSrc = src + separator + 't=' + Date.now() + '_' + index;
                img.src = newSrc;
                console.log('[JS] Reloaded image #' + img.dataset.imgId);
            }
        });
    } catch (error) {
        console.error('[JS] Error reloading images:', error);
    }
};

// Check if images are loaded correctly
window.checkImageStatus = function () {
    var images = document.querySelectorAll('img[data-img-id]');
    console.log('[JS] Checking ' + images.length + ' images');

    images.forEach(function (img) {
        var src = img.src;
        var id = img.dataset.imgId;
        var naturalWidth = img.naturalWidth;
        var complete = img.complete;

        console.log('[JS] Image #' + id + ' - src: ' + src.substring(0, 60) + '... - loaded: ' + complete + ' - width: ' + naturalWidth);

        if (!complete || naturalWidth === 0) {
            console.warn('[JS] Image #' + id + ' failed to load');
        }
    });
};