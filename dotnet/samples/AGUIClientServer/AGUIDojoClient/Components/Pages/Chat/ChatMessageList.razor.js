// The following logic provides auto-scroll behavior for the chat messages list.
// If you don't want that behavior, you can simply not load this module.

window.customElements.define('chat-messages', class ChatMessages extends HTMLElement {
    static _isFirstAutoScroll = true;

    connectedCallback() {
        this._observer = new MutationObserver(mutations => this._scheduleAutoScroll(mutations));
        this._observer.observe(this, { childList: true, attributes: true });
    }

    disconnectedCallback() {
        this._observer.disconnect();
    }

    _scheduleAutoScroll(mutations) {
        // Debounce the calls in case multiple DOM updates occur together
        cancelAnimationFrame(this._nextAutoScroll);
        this._nextAutoScroll = requestAnimationFrame(() => {
            const addedUserMessage = mutations.some(m => Array.from(m.addedNodes).some(n => n.parentElement === this && n.classList?.contains('user-message')));
            const elem = this.lastElementChild;
            if (elem && (ChatMessages._isFirstAutoScroll || addedUserMessage || this._elemIsNearScrollBoundary(elem, 300))) {
                elem.scrollIntoView({ behavior: ChatMessages._isFirstAutoScroll ? 'instant' : 'smooth', block: 'end' });
                ChatMessages._isFirstAutoScroll = false;
            }
        });
    }

    _elemIsNearScrollBoundary(elem, threshold) {
        const scrollParent = this._getScrollParent(this);
        if (!scrollParent) return true; // Default to scrolling if no parent found

        const maxScrollPos = scrollParent.scrollHeight - scrollParent.clientHeight;
        const remainingScrollDistance = maxScrollPos - scrollParent.scrollTop;
        return remainingScrollDistance < elem.offsetHeight + threshold;
    }

    _getScrollParent(node) {
        if (!node || node === document.body) return window;
        const style = getComputedStyle(node);
        const overflowY = style.overflowY;
        if (overflowY === 'scroll' || overflowY === 'auto') return node;
        return this._getScrollParent(node.parentElement);
    }
});
