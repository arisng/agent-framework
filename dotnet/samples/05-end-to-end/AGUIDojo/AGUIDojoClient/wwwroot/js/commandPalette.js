// Copyright (c) Microsoft. All rights reserved.

/**
 * Command Palette keyboard shortcut interop for Blazor Server.
 *
 * Registers a global `keydown` listener for Ctrl+K / Cmd+K that invokes
 * a .NET callback to toggle the command palette dialog. The listener
 * prevents the browser's default action (e.g., Chrome's address bar focus)
 * so the shortcut is captured exclusively by the app.
 *
 * Usage from Blazor:
 *   await JSRuntime.InvokeVoidAsync("commandPaletteInterop.register", dotNetRef);
 *   await JSRuntime.InvokeVoidAsync("commandPaletteInterop.unregister");
 */

window.commandPaletteInterop = {
    /** @type {Function|null} Stored keydown handler for cleanup. */
    _handler: null,

    /** @type {Object|null} Stored .NET object reference for callbacks. */
    _dotNetRef: null,

    /**
     * Registers the global Ctrl+K / Cmd+K keydown listener.
     * Calls the .NET method "TogglePalette" on the provided object reference.
     *
     * @param {Object} dotNetRef - A DotNetObjectReference from Blazor.
     */
    register: function (dotNetRef) {
        // Clean up any existing listener before registering a new one
        this.unregister();

        this._dotNetRef = dotNetRef;
        this._handler = function (e) {
            // Ctrl+K (Windows/Linux) or Cmd+K (macOS)
            if ((e.ctrlKey || e.metaKey) && typeof e.key === 'string' && e.key.toLowerCase() === 'k') {
                e.preventDefault();
                e.stopPropagation();
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync('TogglePalette');
                }
            }
        };

        document.addEventListener('keydown', this._handler, { capture: true });
    },

    /**
     * Programmatically toggles the sidebar by clicking the built-in trigger.
     * Prefer the layout trigger with `data-sidebar="trigger"` to avoid the
     * duplicate rail affordance rendered by the sidebar itself.
     */
    toggleSidebar: function () {
        const trigger = document.querySelector('button[data-sidebar="trigger"]')
            || document.querySelector('button[aria-label="Toggle Sidebar"]');
        if (trigger) {
            trigger.click();
        }
    },

    /**
     * Removes the global keydown listener and releases the .NET reference.
     * Safe to call even if no listener is registered.
     */
    unregister: function () {
        if (this._handler) {
            document.removeEventListener('keydown', this._handler, { capture: true });
            this._handler = null;
        }
        this._dotNetRef = null;
    }
};
