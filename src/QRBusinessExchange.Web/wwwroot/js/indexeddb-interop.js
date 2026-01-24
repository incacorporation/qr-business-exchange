// IndexedDB Interop
window.indexedDBInterop = {
    db: null,
    dbName: 'QRBusinessExchangeDB',
    storeName: 'appData',

    initialize: function () {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(this.dbName, 1);

            request.onerror = () => reject(request.error);
            request.onsuccess = () => {
                this.db = request.result;
                resolve();
            };

            request.onupgradeneeded = (event) => {
                const db = event.target.result;
                if (!db.objectStoreNames.contains(this.storeName)) {
                    db.createObjectStore(this.storeName);
                }
            };
        });
    },

    getItem: function (key) {
        return new Promise((resolve, reject) => {
            if (!this.db) {
                this.initialize().then(() => this.getItem(key)).then(resolve).catch(reject);
                return;
            }

            const transaction = this.db.transaction([this.storeName], 'readonly');
            const store = transaction.objectStore(this.storeName);
            const request = store.get(key);

            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve(request.result);
        });
    },

    setItem: function (key, value) {
        return new Promise((resolve, reject) => {
            if (!this.db) {
                this.initialize().then(() => this.setItem(key, value)).then(resolve).catch(reject);
                return;
            }

            const transaction = this.db.transaction([this.storeName], 'readwrite');
            const store = transaction.objectStore(this.storeName);
            const request = store.put(value, key);

            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve();
        });
    },

    removeItem: function (key) {
        return new Promise((resolve, reject) => {
            if (!this.db) {
                this.initialize().then(() => this.removeItem(key)).then(resolve).catch(reject);
                return;
            }

            const transaction = this.db.transaction([this.storeName], 'readwrite');
            const store = transaction.objectStore(this.storeName);
            const request = store.delete(key);

            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve();
        });
    },

    clear: function () {
        return new Promise((resolve, reject) => {
            if (!this.db) {
                this.initialize().then(() => this.clear()).then(resolve).catch(reject);
                return;
            }

            const transaction = this.db.transaction([this.storeName], 'readwrite');
            const store = transaction.objectStore(this.storeName);
            const request = store.clear();

            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve();
        });
    }
};
