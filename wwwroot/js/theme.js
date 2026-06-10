// ============================================
// THEME MANAGEMENT SYSTEM
// Handles theme switching, persistence, and system theme detection
// ============================================

/**
 * Apply theme to the document body
 * @param {boolean} isDark - true for dark theme, false for light theme
 */
export function setTheme(isDark) {
    if (isDark) {
        document.body.classList.add('dark-theme');
    } else {
        document.body.classList.remove('dark-theme');
    }
}

/**
 * Get current system theme preference
 * @returns {boolean} - true if system prefers dark theme
 */
export function getSystemTheme() {
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
}

/**
 * Get saved theme preference from localStorage
 * @returns {boolean} - true for dark, false for light
 */
export function getSavedTheme() {
    const savedPreference = localStorage.getItem('theme') || 'system';
    
    if (savedPreference === 'dark') {
        return true;
    } else if (savedPreference === 'light') {
        return false;
    }
    // If 'system' or no preference, use system theme
    return getSystemTheme();
}

/**
 * Get saved theme preference string ('system', 'dark', or 'light')
 * @returns {string} - theme preference
 */
export function getSavedThemePreference() {
    return localStorage.getItem('theme') || 'system';
}

/**
 * Save theme preference to localStorage and apply it
 * @param {string} theme - 'system', 'dark', or 'light'
 */
export function saveThemePreference(theme) {
    localStorage.setItem('theme', theme);
    
    // Apply the theme immediately
    if (theme === 'system') {
        setTheme(getSystemTheme());
    } else if (theme === 'dark') {
        setTheme(true);
    } else if (theme === 'light') {
        setTheme(false);
    }
}

/**
 * Apply theme directly (without saving preference)
 * @param {string} theme - 'system', 'dark', or 'light'
 */
export function applyTheme(theme) {
    switch(theme) {
        case 'dark':
            setTheme(true);
            break;
        case 'light':
            setTheme(false);
            break;
        case 'system':
        default:
            setTheme(getSystemTheme());
            break;
    }
}

/**
 * Toggle between dark and light theme
 */
export function toggleTheme() {
    const isCurrentlyDark = document.body.classList.contains('dark-theme');
    setTheme(!isCurrentlyDark);
    
    // Update localStorage preference
    const newTheme = !isCurrentlyDark ? 'dark' : 'light';
    localStorage.setItem('theme', newTheme);
}

// Listen for system theme changes in real-time
window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', e => {
    const savedTheme = localStorage.getItem('theme');
    
    // Only auto-update if user has selected 'system' theme
    if (savedTheme === 'system' || !savedTheme) {
        setTheme(e.matches);
        
        // Notify Blazor if needed
        if (window.DotNet) {
            try {
                DotNet.invokeMethodAsync('ScreenTracker1', 'OnSystemThemeChanged', e.matches);
            } catch (error) {
                console.log('Blazor interop not available:', error);
            }
        }
    }
});

// Initialize theme on script load
(function initializeTheme() {
    const savedTheme = localStorage.getItem('theme') || 'system';
    let isDark = false;
    
    if (savedTheme === 'dark') {
        isDark = true;
    } else if (savedTheme === 'light') {
        isDark = false;
    } else {
        // System theme
        isDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    }
    
    if (isDark) {
        document.body.classList.add('dark-theme');
    } else {
        document.body.classList.remove('dark-theme');
    }
})();