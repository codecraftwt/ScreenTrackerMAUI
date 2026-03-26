
export function setTheme(isDark) {
    if (isDark) {
        document.body.classList.add('dark-theme');
    } else {
        document.body.classList.remove('dark-theme');
    }
}


export function getSystemTheme() {
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
}