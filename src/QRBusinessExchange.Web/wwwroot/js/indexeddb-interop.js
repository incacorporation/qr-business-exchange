// IndexedDB Interop for Blazor Server - Chrome Extension Safe
window.indexedDBInterop = (function () {
    let db = null;
    const dbName = 'QRBusinessExchangeDB';
    const storeName = 'appData';
    let initPromise = null;
    let isReady = false;

    // Prevent chrome extension conflicts
    const safePromise = (executorFn) => {
        return new Promise((resolve, reject) => {
            try {
                executorFn(resolve, reject);
            } catch (error) {
                reject(error);
            }
        }).catch(error => {
            console.error('IndexedDB Promise Error:', error);
            throw error;
        });
    };

    function initialize() {
        if (initPromise) {
            return initPromise;
        }

        if (db && isReady) {
            return Promise.resolve();
        }

        initPromise = safePromise((resolve, reject) => {
            if (!window.indexedDB) {
                reject(new Error('IndexedDB not supported'));
                return;
            }

            const request = indexedDB.open(dbName, 1);
            
            const timeout = setTimeout(() => {
                initPromise = null;
                reject(new Error('IndexedDB initialization timeout'));
            }, 10000);

            request.onerror = () => {
                clearTimeout(timeout);
                initPromise = null;
                isReady = false;
                reject(request.error || new Error('IndexedDB open failed'));
            };

            request.onsuccess = () => {
                clearTimeout(timeout);
                db = request.result;
                initPromise = null;
                isReady = true;
                console.log('IndexedDB initialized successfully');
                resolve();
            };

            request.onupgradeneeded = (event) => {
                try {
                    const database = event.target.result;
                    if (!database.objectStoreNames.contains(storeName)) {
                        database.createObjectStore(storeName);
                        console.log('IndexedDB object store created');
                    }
                } catch (error) {
                    clearTimeout(timeout);
                    console.error('Error in onupgradeneeded:', error);
                    reject(error);
                }
            };
        });

        // Return the promise WITHOUT storing it in a variable that could be accessed by extensions
        return initPromise;
    }

    async function ensureInitialized() {
        if (!isReady || !db) {
            await initialize();
        }
    }

    // Synchronous wrapper to prevent async listener issues
    function getItem(key) {
        // Return promise immediately - don't return true then send async response
        return safePromise(async (resolve, reject) => {
            try {
                await ensureInitialized();

                if (!db) {
                    resolve(null);
                    return;
                }

                const transaction = db.transaction([storeName], 'readonly');
                const store = transaction.objectStore(storeName);
                const request = store.get(key);

                const timeout = setTimeout(() => {
                    reject(new Error('Get operation timeout'));
                }, 5000);

                request.onerror = () => {
                    clearTimeout(timeout);
                    reject(request.error);
                };
                
                request.onsuccess = () => {
                    clearTimeout(timeout);
                    resolve(request.result || null);
                };
            } catch (error) {
                console.error('getItem error:', error);
                resolve(null); // Don't reject - just return null
            }
        });
    }

    function setItem(key, value) {
        return safePromise(async (resolve, reject) => {
            try {
                await ensureInitialized();

                if (!db) {
                    resolve(); // Silently succeed if DB not available
                    return;
                }

                const transaction = db.transaction([storeName], 'readwrite');
                const store = transaction.objectStore(storeName);
                const request = store.put(value, key);

                const timeout = setTimeout(() => {
                    resolve(); // Resolve anyway on timeout
                }, 5000);

                request.onerror = () => {
                    clearTimeout(timeout);
                    console.error('setItem error:', request.error);
                    resolve(); // Resolve even on error
                };
                
                request.onsuccess = () => {
                    clearTimeout(timeout);
                    resolve();
                };
            } catch (error) {
                console.error('setItem error:', error);
                resolve(); // Always resolve, never reject
            }
        });
    }

    function removeItem(key) {
        return safePromise(async (resolve, reject) => {
            try {
                await ensureInitialized();

                if (!db) {
                    resolve();
                    return;
                }

                const transaction = db.transaction([storeName], 'readwrite');
                const store = transaction.objectStore(storeName);
                const request = store.delete(key);

                const timeout = setTimeout(() => {
                    resolve();
                }, 5000);

                request.onerror = () => {
                    clearTimeout(timeout);
                    resolve();
                };
                
                request.onsuccess = () => {
                    clearTimeout(timeout);
                    resolve();
                };
            } catch (error) {
                console.error('removeItem error:', error);
                resolve();
            }
        });
    }

    function clear() {
        return safePromise(async (resolve, reject) => {
            try {
                await ensureInitialized();

                if (!db) {
                    resolve();
                    return;
                }

                const transaction = db.transaction([storeName], 'readwrite');
                const store = transaction.objectStore(storeName);
                const request = store.clear();

                const timeout = setTimeout(() => {
                    resolve();
                }, 5000);

                request.onerror = () => {
                    clearTimeout(timeout);
                    resolve();
                };
                
                request.onsuccess = () => {
                    clearTimeout(timeout);
                    resolve();
                };
            } catch (error) {
                console.error('clear error:', error);
                resolve();
            }
        });
    }

    function isInitialized() {
        return isReady && db !== null;
    }

    // Public API - all methods return resolved promises
    return {
        initialize: initialize,
        getItem: getItem,
        setItem: setItem,
        removeItem: removeItem,
        clear: clear,
        isInitialized: isInitialized
    };
})();

// Prevent any listener conflicts
if (window.indexedDBInterop) {
    console.log('IndexedDB interop loaded successfully');
}



