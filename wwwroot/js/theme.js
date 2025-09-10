// wwwroot/js/theme.js

// Sets or removes the dark-theme class on the body element.
export function setTheme(isDark) {
    if (isDark) {
        document.body.classList.add('dark-theme');
    } else {
        document.body.classList.remove('dark-theme');
    }
}

// Checks if the user's operating system is set to dark mode.
export function getSystemTheme() {
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
}