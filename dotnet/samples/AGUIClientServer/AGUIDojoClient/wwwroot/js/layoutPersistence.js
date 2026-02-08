// Copyright (c) Microsoft. All rights reserved.

// =============================================================================
// Layout Persistence JS Interop Module
// =============================================================================
// Observes ResizablePanel style changes via MutationObserver and persists
// the splitter position to localStorage with a 500ms debounce.
// Only operates on desktop viewports — mobile layout ignores persisted values.
//
// Exports:
//   getPersistedLayout() → { contextSize: number, canvasSize: number } | null
//   initLayoutPersistence(panelGroupElement) → void
//   dispose() → void
// =============================================================================

/** @type {MutationObserver|null} */
let _observer = null;

/** @type {number|null} */
let _debounceTimerId = null;

/** @type {string} localStorage key for the splitter position */
const STORAGE_KEY = 'layout-splitter-position';

/** @type {number} Debounce interval in milliseconds */
const DEBOUNCE_MS = 500;

/**
 * Reads the persisted splitter position from localStorage.
 *
 * @returns {{ contextSize: number, canvasSize: number } | null}
 *   The persisted layout sizes, or `null` if no valid data is stored.
 */
export function getPersistedLayout() {
    try {
        var stored = localStorage.getItem(STORAGE_KEY);
        if (stored) {
            var parsed = JSON.parse(stored);
            if (typeof parsed.contextSize === 'number' &&
                typeof parsed.canvasSize === 'number' &&
                parsed.contextSize > 0 && parsed.canvasSize > 0) {
                return parsed;
            }
        }
    } catch (_) {
        // Invalid or missing data — fall through.
    }
    return null;
}

/**
 * Initializes layout persistence by observing inline style attribute changes
 * on the `.context-panel` and `.canvas-panel` elements within the given
 * panel group container. When the BlazorBlueprint ResizablePanelGroup
 * updates panel sizes during a drag operation, the MutationObserver fires
 * and debounces a save to localStorage.
 *
 * @param {HTMLElement} panelGroupElement — The wrapper element
 *   (e.g., `div.dual-pane-root`) containing the ResizablePanelGroup.
 */
export function initLayoutPersistence(panelGroupElement) {
    if (!panelGroupElement) {
        return;
    }

    // Clean up any previous observer
    dispose();

    var contextPanel = panelGroupElement.querySelector('.context-panel');
    var canvasPanel = panelGroupElement.querySelector('.canvas-panel');

    if (!contextPanel || !canvasPanel) {
        return;
    }

    // Observe inline style changes on both panels. The ResizablePanelGroup
    // sets flex-basis (or equivalent) via inline style during resize.
    _observer = new MutationObserver(function () {
        if (_debounceTimerId !== null) {
            clearTimeout(_debounceTimerId);
        }
        _debounceTimerId = setTimeout(function () {
            _debounceTimerId = null;
            _savePanelSizes(contextPanel, canvasPanel);
        }, DEBOUNCE_MS);
    });

    _observer.observe(contextPanel, { attributes: true, attributeFilter: ['style'] });
    _observer.observe(canvasPanel, { attributes: true, attributeFilter: ['style'] });
}

/**
 * Reads current flex-basis percentages from panel inline styles and
 * persists them to localStorage.
 *
 * @param {HTMLElement} contextPanel — The context (left) panel element.
 * @param {HTMLElement} canvasPanel — The canvas (right) panel element.
 */
function _savePanelSizes(contextPanel, canvasPanel) {
    var contextBasis = contextPanel.style.flexBasis;
    var canvasBasis = canvasPanel.style.flexBasis;

    if (!contextBasis || !canvasBasis) {
        return;
    }

    var contextSize = parseFloat(contextBasis);
    var canvasSize = parseFloat(canvasBasis);

    if (!isNaN(contextSize) && !isNaN(canvasSize) &&
        contextSize > 0 && canvasSize > 0) {
        localStorage.setItem(STORAGE_KEY, JSON.stringify({
            contextSize: Math.round(contextSize * 100) / 100,
            canvasSize: Math.round(canvasSize * 100) / 100
        }));
    }
}

/**
 * Disposes the MutationObserver and cancels any pending debounce timer.
 * Must be called when the Blazor component disposes to prevent memory leaks.
 */
export function dispose() {
    if (_observer) {
        _observer.disconnect();
        _observer = null;
    }

    if (_debounceTimerId !== null) {
        clearTimeout(_debounceTimerId);
        _debounceTimerId = null;
    }
}
