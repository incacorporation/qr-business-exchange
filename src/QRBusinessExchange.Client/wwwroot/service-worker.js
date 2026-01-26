// Service Worker for QR Business Exchange PWA
const CACHE_NAME = 'qr-business-exchange-v1';
const STATIC_CACHE = 'qr-business-static-v1';
const RUNTIME_CACHE = 'qr-business-runtime-v1';

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

// Fetch event - serve from cache, fallback to network
self.addEventListener('fetch', event => {
    const { request } = event;
    const url = new URL(request.url);

    // Network-only for API calls
    if (NETWORK_ONLY.some(pattern => url.pathname.includes(pattern))) {
        event.respondWith(
            fetch(request)
                .catch(error => {
                    console.error('[Service Worker] Network request failed:', error);
                    return new Response(
                        JSON.stringify({ error: 'Network unavailable' }),
                        { status: 503, headers: { 'Content-Type': 'application/json' } }
                    );
                })
        );
        return;
    }

    // Cache-first strategy for static assets
    if (request.method === 'GET') {
        event.respondWith(
            caches.match(request)
                .then(cachedResponse => {
                    if (cachedResponse) {
                        console.log('[Service Worker] Serving from cache:', request.url);
                        return cachedResponse;
                    }

                    // Not in cache, fetch from network
                    return fetch(request)
                        .then(response => {
                            // Don't cache non-successful responses
                            if (!response || response.status !== 200 || response.type === 'error') {
                                return response;
                            }

                            // Clone the response
                            const responseToCache = response.clone();

                            // Cache the fetched response
                            caches.open(RUNTIME_CACHE)
                                .then(cache => {
                                    cache.put(request, responseToCache);
                                    console.log('[Service Worker] Cached new resource:', request.url);
                                });

                            return response;
                        })
                        .catch(error => {
                            console.error('[Service Worker] Fetch failed:', error);
                            
                            // Return offline page for navigation requests
                            if (request.mode === 'navigate') {
                                return caches.match('/index.html');
                            }
                            
                            throw error;
                        });
                })
        );
    }
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
    const title = data.title || 'QR Business Exchange';
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
