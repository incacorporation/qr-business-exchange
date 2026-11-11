// Service Worker for qrbound PWA
const CACHE_NAME = 'qr-business-exchange-v3';
const STATIC_CACHE = 'qr-business-static-v3';
const RUNTIME_CACHE = 'qr-business-runtime-v3';

// Files to cache immediately on install
const STATIC_FILES = [
    '/',
    '/index.html',
    '/css/app.css',
    '/icon-192.svg',
    '/manifest.json',
    '/_framework/blazor.webassembly.js'
];

// API endpoints that should always go to network
const NETWORK_ONLY = [
    '/api/'
];

// Install event - cache static files
self.addEventListener('install', event => {
    console.log('[Service Worker] Installing...');
    
    event.waitUntil(
        caches.open(STATIC_CACHE)
            .then(cache => {
                console.log('[Service Worker] Caching static files');
                // Cache files one by one, ignoring failures
                return Promise.allSettled(
                    STATIC_FILES.map(url => 
                        cache.add(url)
                            .then(() => console.log('[Service Worker] Cached:', url))
                            .catch(err => console.warn('[Service Worker] Failed to cache:', url, err))
                    )
                );
            })
            .then(() => {
                console.log('[Service Worker] Skip waiting');
                return self.skipWaiting();
            })
            .catch(error => {
                console.error('[Service Worker] Installation failed:', error);
            })
    );
});

// Activate event - clean up old caches
self.addEventListener('activate', event => {
    console.log('[Service Worker] Activating...');
    
    event.waitUntil(
        caches.keys()
            .then(cacheNames => {
                return Promise.all(
                    cacheNames
                        .filter(name => {
                            // Remove old caches
                            return name !== STATIC_CACHE && 
                                   name !== RUNTIME_CACHE &&
                                   name !== CACHE_NAME;
                        })
                        .map(name => {
                            console.log('[Service Worker] Deleting old cache:', name);
                            return caches.delete(name);
                        })
                );
            })
            .then(() => {
                console.log('[Service Worker] Claiming clients');
                return self.clients.claim();
            })
    );
});

// "name.<10-char hash>.ext" as produced by .NET static asset fingerprinting
const FINGERPRINT = /\.[a-z0-9]{10}\.[a-z0-9]+$/i;

function isFingerprinted(pathname) {
    return pathname.startsWith('/_framework/') && FINGERPRINT.test(pathname);
}

function isCacheable(response) {
    return response && response.status === 200 && response.type === 'basic';
}

function putInCache(request, response) {
    const copy = response.clone();
    caches.open(RUNTIME_CACHE).then(cache => cache.put(request, copy));
}

async function networkFirst(request) {
    try {
        const response = await fetch(request);
        if (isCacheable(response)) {
            putInCache(request, response);
        }
        return response;
    } catch (error) {
        // Offline: fall back to the last good copy; for page loads, the cached app shell.
        const cached = await caches.match(request);
        if (cached) {
            return cached;
        }
        if (request.mode === 'navigate') {
            const shell = await caches.match('/index.html') || await caches.match('/');
            if (shell) {
                return shell;
            }
        }
        throw error;
    }
}

async function cacheFirst(request) {
    const cached = await caches.match(request);
    if (cached) {
        return cached;
    }
    const response = await fetch(request);
    if (isCacheable(response)) {
        putInCache(request, response);
    }
    return response;
}

// Fetch event
self.addEventListener('fetch', event => {
    const { request } = event;
    const url = new URL(request.url);

    // API calls, other origins (the API runs on its own host) and non-GET requests are not
    // intercepted at all: the browser handles them as if there were no service worker.
    // Re-fetching them from here broke CORS/credentialed calls and turned every failure
    // (including real API errors) into a fake 503 "Network unavailable".
    if (url.origin !== self.location.origin ||
        request.method !== 'GET' ||
        NETWORK_ONLY.some(pattern => url.pathname.includes(pattern))) {
        return;
    }

    // Fingerprinted build output (e.g. _framework/App.80opl6u2kt.wasm) never changes under the
    // same name, so it is safe to serve from the cache first.
    if (isFingerprinted(url.pathname)) {
        event.respondWith(cacheFirst(request));
        return;
    }

    // Everything else (pages, index.html, dotnet.js, css, js, images) is network-first: a new build
    // shows up on the next load, and the cache is only used when the network is unavailable.
    event.respondWith(networkFirst(request));
});

// Background sync for offline actions
self.addEventListener('sync', event => {
    console.log('[Service Worker] Background sync:', event.tag);
    
    if (event.tag === 'sync-data') {
        event.waitUntil(syncData());
    }
});

// Push notifications
self.addEventListener('push', event => {
    console.log('[Service Worker] Push notification received');
    
    const data = event.data ? event.data.json() : {};
    const title = data.title || 'qrbound';
    const options = {
        body: data.body || 'You have a new notification',
        icon: '/icon-192.svg',
        badge: '/icon-192.svg',
        vibrate: [200, 100, 200],
        tag: data.tag || 'notification',
        data: data
    };

    event.waitUntil(
        self.registration.showNotification(title, options)
    );
});

// Notification click
self.addEventListener('notificationclick', event => {
    console.log('[Service Worker] Notification clicked');
    
    event.notification.close();
    
    const url = event.notification.data?.url || '/';
    
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true })
            .then(clientList => {
                // Check if there's already a window open
                for (let client of clientList) {
                    if (client.url === url && 'focus' in client) {
                        return client.focus();
                    }
                }
                
                // Open new window
                if (clients.openWindow) {
                    return clients.openWindow(url);
                }
            })
    );
});

// Periodic background sync (requires user permission)
self.addEventListener('periodicsync', event => {
    console.log('[Service Worker] Periodic sync:', event.tag);
    
    if (event.tag === 'sync-nearby-users') {
        event.waitUntil(syncNearbyUsers());
    }
});

// Helper function to sync data
async function syncData() {
    try {
        console.log('[Service Worker] Syncing data...');
        // TODO: Implement actual sync logic
        return Promise.resolve();
    } catch (error) {
        console.error('[Service Worker] Sync failed:', error);
        throw error;
    }
}

// Helper function to sync nearby users
async function syncNearbyUsers() {
    try {
        console.log('[Service Worker] Syncing nearby users...');
        // TODO: Implement actual sync logic
        return Promise.resolve();
    } catch (error) {
        console.error('[Service Worker] Nearby users sync failed:', error);
        throw error;
    }
}

// Message handler for communication with app
self.addEventListener('message', event => {
    console.log('[Service Worker] Message received:', event.data);
    
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
    
    if (event.data && event.data.type === 'CACHE_URLS') {
        const urls = event.data.urls || [];
        event.waitUntil(
            caches.open(RUNTIME_CACHE)
                .then(cache => cache.addAll(urls))
        );
    }
    
    if (event.data && event.data.type === 'CLEAR_CACHE') {
        event.waitUntil(
            caches.keys()
                .then(names => Promise.all(names.map(name => caches.delete(name))))
                .then(() => console.log('[Service Worker] All caches cleared'))
        );
    }
});

console.log('[Service Worker] Loaded');
