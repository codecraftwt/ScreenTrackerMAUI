//window.localStorageHelper = {
//    saveToLocalStorage: function (key, value) {
//        console.log(`Saving to localStorage. Key: ${key}, Value: ${JSON.stringify(value)}`);
//        localStorage.setItem(key, JSON.stringify(value)); // Store as JSON string
//    },

//    getFromLocalStorage: function (key) {
//        const value = localStorage.getItem(key);
//        console.log(`Retrieved from localStorage. Key: ${key}, Value: ${value}`);
//        return value ? JSON.parse(value) : null; // Parse JSON string to object
//    }
//};


export function getIsOnState() {
    const state = localStorage.getItem('isTrackingOn');
    return state === 'true'; // Convert the stored string back to a boolean
}

export function setIsOnState(state) {
    localStorage.setItem('isTrackingOn', state.toString());
}