// Copyright (c) Microsoft. All rights reserved.

// =============================================================================
// ViewportService JS Interop Module
// =============================================================================
// Provides viewport width detection and resize event notification for
// responsive layout switching (desktop dual-pane ↔ mobile drawer).
//
// Exports:
//   getViewportWidth() → number
//   onResize(dotNetRef, methodName) → void
//   dispose() → void
// =============================================================================

/** @type {DotNetObject|null} */
let _dotNetRef = null;

/** @type {string|null} */
let _methodName = null;

/** @type {number|null} */
let _resizeTimerId = null;

/** @type {number} Debounce interval in milliseconds */
const DEBOUNCE_MS = 150;

/**
 * Returns the current viewport width in pixels.
 * @returns {number} The window inner width.
 */
export function getViewportWidth() {
    return window.innerWidth;
}

/**
 * Registers a debounced resize listener that invokes a .NET method when
 * the viewport width changes.
 *
 * @param {DotNetObject} dotNetRef - A DotNetObjectReference for calling back into C#.
 * @param {string} methodName - The [JSInvokable] method name to call on resize.
 */
export function onResize(dotNetRef, methodName) {
    // Clean up any previous listener before registering a new one
    dispose();

    _dotNetRef = dotNetRef;
    _methodName = methodName;

    window.addEventListener("resize", _handleResize);
}

/**
 * Removes the resize listener and releases the .NET object reference.
 * Must be called when the Blazor component disposes to prevent memory leaks.
 */
export function dispose() {
    window.removeEventListener("resize", _handleResize);

    if (_resizeTimerId !== null) {
        clearTimeout(_resizeTimerId);
        _resizeTimerId = null;
    }

    _dotNetRef = null;
    _methodName = null;
}

/**
 * Internal debounced resize handler. Waits for DEBOUNCE_MS of inactivity
 * before invoking the .NET callback with the current viewport width.
 */
function _handleResize() {
    if (_resizeTimerId !== null) {
        clearTimeout(_resizeTimerId);
    }

    _resizeTimerId = setTimeout(() => {
        _resizeTimerId = null;

        if (_dotNetRef && _methodName) {
            _dotNetRef.invokeMethodAsync(_methodName, window.innerWidth);
        }
    }, DEBOUNCE_MS);
}
