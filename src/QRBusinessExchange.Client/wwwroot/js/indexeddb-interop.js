// IndexedDB Interop for Blazor WebAssembly
// Uses localStorage as fallback for cross-browser compatibility
window.indexedDBInterop = {
    storageType: 'localStorage', // Can be 'indexedDB' or 'localStorage'
    db: null,
    dbName: 'QRBusinessExchangeDB',
    storeName: 'appData',

    initialize: function () {
        return new Promise((resolve, reject) => {
            try {
                // Check if IndexedDB is available
                if (window.indexedDB) {
                    const request = indexedDB.open(this.dbName, 1);
                    
                    const timeout = setTimeout(() => {
                        console.warn('IndexedDB timeout, falling back to localStorage');
                        this.storageType = 'localStorage';
                        resolve();
                    }, 5000);

                    request.onerror = () => {
                        clearTimeout(timeout);
                        console.warn('IndexedDB error, falling back to localStorage:', request.error);
                        this.storageType = 'localStorage';
                        resolve();
                    };

                    request.onsuccess = () => {
                        clearTimeout(timeout);
                        this.db = request.result;
                        this.storageType = 'indexedDB';
                        console.log('IndexedDB initialized successfully');
                        resolve();
                    };

                    request.onupgradeneeded = (event) => {
                        try {
                            const db = event.target.result;
                            if (!db.objectStoreNames.contains(this.storeName)) {
                                db.createObjectStore(this.storeName);
                                console.log('IndexedDB object store created');
                            }
                        } catch (error) {
                            clearTimeout(timeout);
                            console.error('Error in onupgradeneeded:', error);
                            this.storageType = 'localStorage';
                            resolve();
                        }
                    };
                } else {
                    // IndexedDB not supported, use localStorage
                    console.log('IndexedDB not supported, using localStorage');
                    this.storageType = 'localStorage';
                    resolve();
                }
            } catch (error) {
                console.error('IndexedDB initialization error, using localStorage:', error);
                this.storageType = 'localStorage';
                resolve();
            }
        });
    },

    getItem: function (key) {
        return new Promise((resolve, reject) => {
            try {
                if (this.storageType === 'localStorage') {
                    const value = localStorage.getItem(key);
                    resolve(value);
                } else if (this.db) {
                    const transaction = this.db.transaction([this.storeName], 'readonly');
                    const store = transaction.objectStore(this.storeName);
                    const request = store.get(key);

                    request.onsuccess = () => resolve(request.result || null);
                    request.onerror = () => {
                        console.error('IndexedDB get error:', request.error);
                        // Fallback to localStorage
                        const value = localStorage.getItem(key);
                        resolve(value);
                    };
                } else {
                    const value = localStorage.getItem(key);
                    resolve(value);
                }
            } catch (error) {
                console.error('getItem error:', error);
                resolve(null);
            }
        });
    },

    setItem: function (key, value) {
        return new Promise((resolve, reject) => {
            try {
                if (this.storageType === 'localStorage') {
                    localStorage.setItem(key, value);
                    console.log('Saved to localStorage:', key);
                    resolve();
                } else if (this.db) {
                    const transaction = this.db.transaction([this.storeName], 'readwrite');
                    const store = transaction.objectStore(this.storeName);
                    const request = store.put(value, key);

                    request.onsuccess = () => {
                        console.log('Saved to IndexedDB:', key);
                        resolve();
                    };
                    request.onerror = () => {
                        console.error('IndexedDB set error:', request.error);
                        // Fallback to localStorage
                        localStorage.setItem(key, value);
                        console.log('Fell back to localStorage:', key);
                        resolve();
                    };
                } else {
                    localStorage.setItem(key, value);
                    console.log('Saved to localStorage:', key);
                    resolve();
                }
            } catch (error) {
                console.error('setItem error:', error);
                // Try localStorage as last resort
                try {
                    localStorage.setItem(key, value);
                    resolve();
                } catch (e) {
                    reject(e);
                }
            }
        });
    },

    removeItem: function (key) {
        return new Promise((resolve, reject) => {
            try {
                if (this.storageType === 'localStorage') {
                    localStorage.removeItem(key);
                    resolve();
                } else if (this.db) {
                    const transaction = this.db.transaction([this.storeName], 'readwrite');
                    const store = transaction.objectStore(this.storeName);
                    const request = store.delete(key);

                    request.onsuccess = () => resolve();
                    request.onerror = () => {
                        localStorage.removeItem(key);
                        resolve();
                    };
                } else {
                    localStorage.removeItem(key);
                    resolve();
                }
            } catch (error) {
                console.error('removeItem error:', error);
                resolve();
            }
        });
    },

    clear: function () {
        return new Promise((resolve, reject) => {
            try {
                if (this.storageType === 'localStorage') {
                    localStorage.clear();
                    resolve();
                } else if (this.db) {
                    const transaction = this.db.transaction([this.storeName], 'readwrite');
                    const store = transaction.objectStore(this.storeName);
                    const request = store.clear();

                    request.onsuccess = () => resolve();
                    request.onerror = () => {
                        localStorage.clear();
                        resolve();
                    };
                } else {
                    localStorage.clear();
                    resolve();
                }
            } catch (error) {
                console.error('clear error:', error);
                resolve();
            }
        });
    }
};

// Auto-initialize
window.indexedDBInterop.initialize().then(() => {
    console.log('IndexedDB interop ready, using:', window.indexedDBInterop.storageType);
}).catch(error => {
    console.error('IndexedDB interop initialization failed:', error);
});

