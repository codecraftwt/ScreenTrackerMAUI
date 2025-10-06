

export function getIsOnState() {
    const state = localStorage.getItem('isTrackingOn');
    return state === 'true'; 
}

export function setIsOnState(state) {
    localStorage.setItem('isTrackingOn', state.toString());
}

export function setSelectedAdmin(adminUsername) {
    localStorage.setItem("selectedAdmin", adminUsername);
}

export function getSelectedAdmin() {
    return localStorage.getItem("selectedAdmin");
}

export function setSelectedUser(username) {
    localStorage.setItem("selectedUser", username);
}

export function getSelectedUser() {
    return localStorage.getItem("selectedUser");
}
