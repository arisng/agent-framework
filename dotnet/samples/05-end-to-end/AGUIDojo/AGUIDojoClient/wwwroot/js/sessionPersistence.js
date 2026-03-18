// =============================================================================
// Session Persistence JS Interop Module
// =============================================================================
// Provides tiered browser storage for session persistence:
//   L1 - localStorage for lightweight metadata and active session ID
//   L2 - IndexedDB for full conversation trees (message history)
//
// All methods are exposed on window.sessionPersistence for Blazor JS interop.
// =============================================================================

const DB_NAME = 'aguidojo-sessions';
const DB_VERSION = 1;
const STORE_NAME = 'conversations';
const META_KEY = 'session-metadata';
const ACTIVE_SESSION_KEY = 'active-session-id';

/** @type {Promise<IDBDatabase>|null} */
let dbPromise = null;

/**
 * Opens (or reuses) the IndexedDB database.
 * @returns {Promise<IDBDatabase>}
 */
function openDb() {
    if (dbPromise) return dbPromise;
    dbPromise = new Promise(function (resolve, reject) {
        var request = indexedDB.open(DB_NAME, DB_VERSION);
        request.onupgradeneeded = function (e) {
            var db = e.target.result;
            if (!db.objectStoreNames.contains(STORE_NAME)) {
                db.createObjectStore(STORE_NAME, { keyPath: 'sessionId' });
            }
        };
        request.onsuccess = function (e) { resolve(e.target.result); };
        request.onerror = function (e) { reject(e.target.error); };
    });
    return dbPromise;
}

window.sessionPersistence = {
    // =========================================================================
    // L1 — localStorage (metadata + active session)
    // =========================================================================

    /**
     * Save session metadata JSON to localStorage.
     * @param {string} metadataJson
     */
    saveMetadata: function (metadataJson) {
        try { localStorage.setItem(META_KEY, metadataJson); }
        catch (e) { console.warn('[sessionPersistence] saveMetadata failed:', e); }
    },

    /**
     * Load session metadata JSON from localStorage.
     * @returns {string|null}
     */
    loadMetadata: function () {
        try { return localStorage.getItem(META_KEY); }
        catch (e) { return null; }
    },

    /**
     * Save active session ID to localStorage.
     * @param {string} sessionId
     */
    saveActiveSessionId: function (sessionId) {
        try { localStorage.setItem(ACTIVE_SESSION_KEY, sessionId); }
        catch (e) { console.warn('[sessionPersistence] saveActiveSessionId failed:', e); }
    },

    /**
     * Load active session ID from localStorage.
     * @returns {string|null}
     */
    loadActiveSessionId: function () {
        try { return localStorage.getItem(ACTIVE_SESSION_KEY); }
        catch (e) { return null; }
    },

    // =========================================================================
    // L2 — IndexedDB (full conversation trees)
    // =========================================================================

    /**
     * Save a conversation tree JSON to IndexedDB.
     * @param {string} sessionId
     * @param {string} treeJson
     */
    saveConversation: async function (sessionId, treeJson) {
        try {
            var db = await openDb();
            var tx = db.transaction(STORE_NAME, 'readwrite');
            var store = tx.objectStore(STORE_NAME);
            await new Promise(function (resolve, reject) {
                var req = store.put({ sessionId: sessionId, tree: treeJson, updatedAt: Date.now() });
                req.onsuccess = resolve;
                req.onerror = function () { reject(req.error); };
            });
        } catch (e) { console.warn('[sessionPersistence] saveConversation failed:', e); }
    },

    /**
     * Load a conversation tree JSON from IndexedDB.
     * @param {string} sessionId
     * @returns {Promise<string|null>}
     */
    loadConversation: async function (sessionId) {
        try {
            var db = await openDb();
            var tx = db.transaction(STORE_NAME, 'readonly');
            var store = tx.objectStore(STORE_NAME);
            return new Promise(function (resolve) {
                var req = store.get(sessionId);
                req.onsuccess = function () { resolve(req.result ? req.result.tree : null); };
                req.onerror = function () { resolve(null); };
            });
        } catch (e) { return null; }
    },

    /**
     * Load all stored session IDs and their timestamps from IndexedDB.
     * @returns {Promise<string>} JSON array of {sessionId, updatedAt}
     */
    loadAllSessionIds: async function () {
        try {
            var db = await openDb();
            var tx = db.transaction(STORE_NAME, 'readonly');
            var store = tx.objectStore(STORE_NAME);
            return new Promise(function (resolve) {
                var req = store.getAll();
                req.onsuccess = function () {
                    var results = req.result.map(function (r) {
                        return { sessionId: r.sessionId, updatedAt: r.updatedAt };
                    });
                    resolve(JSON.stringify(results));
                };
                req.onerror = function () { resolve('[]'); };
            });
        } catch (e) { return '[]'; }
    },

    /**
     * Delete a conversation from IndexedDB.
     * @param {string} sessionId
     */
    deleteConversation: async function (sessionId) {
        try {
            var db = await openDb();
            var tx = db.transaction(STORE_NAME, 'readwrite');
            tx.objectStore(STORE_NAME).delete(sessionId);
        } catch (e) { console.warn('[sessionPersistence] deleteConversation failed:', e); }
    }
};
