// Copyright (c) Microsoft. All rights reserved.

/**
 * Theme interop module for Blazor Server.
 *
 * Exposes three methods on `window.themeInterop` for toggling the
 * `.dark` class on the `<html>` element and persisting the user's
 * preference in localStorage under the key `'theme'`.
 *
 * Supported theme values: `'light'` | `'dark'`.
 */

window.themeInterop = {
    /**
     * Returns the persisted theme string from localStorage.
     * Falls back to `'light'` when no value has been stored yet.
     * @returns {string} `'light'` or `'dark'`
     */
    getTheme: function () {
        return localStorage.getItem('theme') || 'light';
    },

    /**
     * Applies the given theme by toggling the `.dark` class on `<html>`
     * and persists the choice to localStorage.
     * @param {string} theme - `'light'` or `'dark'`
     */
    setTheme: function (theme) {
        if (theme === 'dark') {
            document.documentElement.classList.add('dark');
        } else {
            document.documentElement.classList.remove('dark');
        }
        localStorage.setItem('theme', theme);
    },

    /**
     * Toggles between `'light'` and `'dark'`, persists the new value,
     * and returns it so the caller can update its own state.
     * @returns {string} The theme after toggling (`'light'` or `'dark'`).
     */
    toggleTheme: function () {
        var current = this.getTheme();
        var next = current === 'dark' ? 'light' : 'dark';
        this.setTheme(next);
        return next;
    }
};
