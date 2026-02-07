// Copyright (c) Microsoft. All rights reserved.

// =============================================================================
// SwipeApproval JS Interop Module
// =============================================================================
// Handles touch gesture detection and 60fps card animation for the
// SwipeApproval component. Swipe right = approve (green), swipe left = reject
// (red). Calls back into .NET when swipe threshold is reached.
//
// Exports:
//   initSwipe(element, dotNetRef, methodName, threshold) → void
//   dispose() → void
// =============================================================================

/** @type {HTMLElement|null} */
let _element = null;

/** @type {DotNetObject|null} */
let _dotNetRef = null;

/** @type {string|null} */
let _methodName = null;

/** @type {number} Swipe distance threshold in pixels */
let _threshold = 100;

/** @type {number|null} Starting X coordinate of touch */
let _startX = null;

/** @type {number|null} Starting Y coordinate of touch */
let _startY = null;

/** @type {number} Current horizontal offset during swipe */
let _currentX = 0;

/** @type {boolean} Whether we're actively tracking a horizontal swipe */
let _isSwiping = false;

/** @type {boolean} Whether a swipe action has been dispatched (debounce) */
let _actionDispatched = false;

/** @type {boolean} Whether the initial gesture direction has been locked */
let _directionLocked = false;

/** @type {number|null} requestAnimationFrame ID */
let _rafId = null;

/**
 * Initializes swipe gesture handling on the given element.
 *
 * @param {HTMLElement} element - The card element to attach touch handlers to.
 * @param {DotNetObject} dotNetRef - A DotNetObjectReference for calling back into C#.
 * @param {string} methodName - The [JSInvokable] method name to call on swipe complete.
 * @param {number} threshold - Minimum swipe distance in pixels to trigger action.
 */
export function initSwipe(element, dotNetRef, methodName, threshold) {
    dispose();

    _element = element;
    _dotNetRef = dotNetRef;
    _methodName = methodName;
    _threshold = threshold || 100;
    _actionDispatched = false;

    _element.addEventListener("touchstart", _onTouchStart, { passive: true });
    _element.addEventListener("touchmove", _onTouchMove, { passive: false });
    _element.addEventListener("touchend", _onTouchEnd, { passive: true });
    _element.addEventListener("touchcancel", _onTouchCancel, { passive: true });
}

/**
 * Removes all touch event listeners and resets internal state.
 */
export function dispose() {
    if (_element) {
        _element.removeEventListener("touchstart", _onTouchStart);
        _element.removeEventListener("touchmove", _onTouchMove);
        _element.removeEventListener("touchend", _onTouchEnd);
        _element.removeEventListener("touchcancel", _onTouchCancel);
    }

    if (_rafId !== null) {
        cancelAnimationFrame(_rafId);
        _rafId = null;
    }

    _element = null;
    _dotNetRef = null;
    _methodName = null;
    _startX = null;
    _startY = null;
    _currentX = 0;
    _isSwiping = false;
    _actionDispatched = false;
    _directionLocked = false;
}

/**
 * Touch start — record the starting position.
 * @param {TouchEvent} e
 */
function _onTouchStart(e) {
    if (_actionDispatched || !e.touches.length) return;

    const touch = e.touches[0];
    _startX = touch.clientX;
    _startY = touch.clientY;
    _currentX = 0;
    _isSwiping = false;
    _directionLocked = false;

    // Remove any transition for immediate visual feedback
    if (_element) {
        _element.style.transition = "none";
    }
}

/**
 * Touch move — calculate horizontal offset, animate at 60fps, and apply
 * colour overlays. Locks to horizontal-only after initial gesture direction
 * is determined (prevents interference with vertical scrolling).
 * @param {TouchEvent} e
 */
function _onTouchMove(e) {
    if (_startX === null || _startY === null || _actionDispatched || !e.touches.length) return;

    const touch = e.touches[0];
    const deltaX = touch.clientX - _startX;
    const deltaY = touch.clientY - _startY;

    // Lock direction after moving 10px in any direction
    if (!_directionLocked) {
        if (Math.abs(deltaX) > 10 || Math.abs(deltaY) > 10) {
            _directionLocked = true;
            _isSwiping = Math.abs(deltaX) > Math.abs(deltaY);
        }
        if (!_isSwiping) return;
    }

    if (!_isSwiping) return;

    // Prevent vertical scrolling while swiping horizontally
    e.preventDefault();

    _currentX = deltaX;

    // Use requestAnimationFrame for 60fps animation
    if (_rafId === null) {
        _rafId = requestAnimationFrame(_updateVisuals);
    }
}

/**
 * Touch end — check if swipe exceeded threshold and dispatch action.
 * @param {TouchEvent} e
 */
function _onTouchEnd(e) {
    if (_startX === null || _actionDispatched) {
        _resetPosition();
        return;
    }

    if (_isSwiping && Math.abs(_currentX) >= _threshold) {
        // Threshold reached — dispatch the swipe action
        const direction = _currentX > 0 ? "right" : "left";
        _dispatchSwipe(direction);
    } else {
        // Didn't reach threshold — snap back to center
        _resetPosition();
    }

    _startX = null;
    _startY = null;
    _isSwiping = false;
    _directionLocked = false;
}

/**
 * Touch cancel — reset position without triggering any action.
 * @param {TouchEvent} e
 */
function _onTouchCancel(e) {
    _resetPosition();
    _startX = null;
    _startY = null;
    _isSwiping = false;
    _directionLocked = false;
}

/**
 * Updates visual transform and overlay opacity at 60fps.
 */
function _updateVisuals() {
    _rafId = null;

    if (!_element || _actionDispatched) return;

    // Apply horizontal translation and slight rotation for natural feel
    const rotation = _currentX * 0.05; // subtle rotation
    const clampedRotation = Math.max(-15, Math.min(15, rotation));
    _element.style.transform = `translateX(${_currentX}px) rotate(${clampedRotation}deg)`;

    // Update overlay opacity (green for right/approve, red for left/reject)
    const progress = Math.min(Math.abs(_currentX) / _threshold, 1);
    const approveOverlay = _element.querySelector(".swipe-overlay-approve");
    const rejectOverlay = _element.querySelector(".swipe-overlay-reject");

    if (approveOverlay) {
        approveOverlay.style.opacity = _currentX > 0 ? progress * 0.5 : 0;
    }
    if (rejectOverlay) {
        rejectOverlay.style.opacity = _currentX < 0 ? progress * 0.5 : 0;
    }
}

/**
 * Dispatches the swipe action back to .NET and animates the card off-screen.
 * Includes debounce guard to prevent duplicate actions during SignalR latency.
 * @param {string} direction - "right" (approve) or "left" (reject)
 */
function _dispatchSwipe(direction) {
    if (_actionDispatched) return;
    _actionDispatched = true;

    // Animate off-screen
    if (_element) {
        const exitX = direction === "right" ? 500 : -500;
        _element.style.transition = "transform 0.3s ease-out, opacity 0.3s ease-out";
        _element.style.transform = `translateX(${exitX}px) rotate(${direction === "right" ? 15 : -15}deg)`;
        _element.style.opacity = "0";
    }

    // Call .NET after animation completes
    setTimeout(() => {
        if (_dotNetRef && _methodName) {
            _dotNetRef.invokeMethodAsync(_methodName, direction);
        }
    }, 320);
}

/**
 * Resets the card position back to center with a smooth transition.
 */
function _resetPosition() {
    if (!_element) return;

    _element.style.transition = "transform 0.2s ease-out";
    _element.style.transform = "translateX(0) rotate(0deg)";

    const approveOverlay = _element.querySelector(".swipe-overlay-approve");
    const rejectOverlay = _element.querySelector(".swipe-overlay-reject");
    if (approveOverlay) approveOverlay.style.opacity = "0";
    if (rejectOverlay) rejectOverlay.style.opacity = "0";

    _currentX = 0;

    if (_rafId !== null) {
        cancelAnimationFrame(_rafId);
        _rafId = null;
    }
}
