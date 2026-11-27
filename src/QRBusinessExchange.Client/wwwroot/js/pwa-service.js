// Service Worker Registration for qrbound PWA
window.pwaService = {
    registration: null,
    updateAvailable: false,

    // Local development: no service worker, so the browser (and the Visual Studio debugger)
    // always runs the files from the current build instead of cached copies.
    isLocalDevelopment: function() {
        return ['localhost', '127.0.0.1', '[::1]'].includes(location.hostname) ||
               location.hostname.endsWith('.localhost');
    },

    // Remove any service worker and caches left from earlier runs. If an old worker served
    // this page, reload once so the page comes straight from the server.
    removeForDevelopment: async function() {
        const wasControlled = !!navigator.serviceWorker.controller;
        const registrations = await navigator.serviceWorker.getRegistrations();
        await Promise.all(registrations.map(r => r.unregister()));
        if ('caches' in window) {
            const names = await caches.keys();
            await Promise.all(names.map(name => caches.delete(name)));
        }

        if (registrations.length > 0) {
            console.log('[PWA] Local development: removed', registrations.length, 'service worker(s) and their caches');
        }

        const reloadKey = 'pwa-dev-sw-removed';
        if (wasControlled && !sessionStorage.getItem(reloadKey)) {
            sessionStorage.setItem(reloadKey, '1');
            location.reload();
        }
    },

    // Register service worker
    register: async function() {
        if (!('serviceWorker' in navigator)) {
            console.warn('Service Worker not supported in this browser');
            return false;
        }

        if (this.isLocalDevelopment()) {
            console.log('[PWA] Local development: service worker not registered');
            await this.removeForDevelopment();
            return false;
        }

        try {
            const registration = await navigator.serviceWorker.register('/service-worker.js', {
                scope: '/'
            });

            this.registration = registration;

            console.log('? Service Worker registered successfully');
            console.log('Scope:', registration.scope);

            // Check for updates
            registration.addEventListener('updatefound', () => {
                const newWorker = registration.installing;
                console.log('Service Worker update found');

                newWorker.addEventListener('statechange', () => {
                    if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                        console.log('New Service Worker installed, update available');
                        this.updateAvailable = true;
                        this.showUpdateNotification();
                    }
                });
            });

            // Auto-update check every hour
            setInterval(() => {
                registration.update();
            }, 60 * 60 * 1000);

            return true;
        } catch (error) {
            console.error('Service Worker registration failed:', error);
            return false;
        }
    },

    // Unregister service worker
    unregister: async function() {
        if (!this.registration) {
            return false;
        }

        try {
            const success = await this.registration.unregister();
            console.log('Service Worker unregistered:', success);
            return success;
        } catch (error) {
            console.error('Service Worker unregister failed:', error);
            return false;
        }
    },

    // Update to new service worker
    update: async function() {
        if (!this.registration) {
            console.warn('No service worker registered');
            return;
        }

        try {
            await this.registration.update();
            console.log('Service Worker update check initiated');
        } catch (error) {
            console.error('Service Worker update failed:', error);
        }
    },

    // Skip waiting and activate new service worker
    skipWaiting: function() {
        if (!this.registration || !this.registration.waiting) {
            return;
        }

        this.registration.waiting.postMessage({ type: 'SKIP_WAITING' });
        
        // Reload page after activation
        let refreshing = false;
        navigator.serviceWorker.addEventListener('controllerchange', () => {
            if (!refreshing) {
                refreshing = true;
                window.location.reload();
            }
        });
    },

    // Show update notification
    showUpdateNotification: function() {
        // Create notification element
        const notification = document.createElement('div');
        notification.id = 'pwa-update-notification';
        notification.innerHTML = `
            <div style="position: fixed; bottom: 20px; right: 20px; background: #1F43AD; color: white; padding: 20px; border-radius: 10px; box-shadow: 0 4px 20px rgba(0,0,0,0.3); z-index: 10000; max-width: 300px;">
                <h4 style="margin: 0 0 10px 0;">Update Available</h4>
                <p style="margin: 0 0 15px 0;">A new version of qrbound is available.</p>
                <button id="pwa-update-btn" style="background: white; color: #1F43AD; border: none; padding: 10px 20px; border-radius: 5px; cursor: pointer; font-weight: bold; margin-right: 10px;">
                    Update Now
                </button>
                <button id="pwa-dismiss-btn" style="background: transparent; color: white; border: 1px solid white; padding: 10px 20px; border-radius: 5px; cursor: pointer;">
                    Later
                </button>
            </div>
        `;

        document.body.appendChild(notification);

        // Update button click
        document.getElementById('pwa-update-btn').addEventListener('click', () => {
            this.skipWaiting();
        });

        // Dismiss button click
        document.getElementById('pwa-dismiss-btn').addEventListener('click', () => {
            notification.remove();
        });
    },

    // Check if app is installed
    isInstalled: function() {
        return window.matchMedia('(display-mode: standalone)').matches ||
               window.navigator.standalone === true;
    },

    // ---------- Install ("Install app") support ----------
    // The UI lives in the Blazor PwaInstallButton component; this only tracks whether the
    // browser can install the app and triggers the native prompt.

    deferredPrompt: null,
    installListeners: [],

    // iPhone / iPad Safari never fires beforeinstallprompt; users install via Share > Add to Home Screen.
    isIos: function() {
        const ua = window.navigator.userAgent || '';
        const iPadOs = navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1;
        return /iphone|ipad|ipod/i.test(ua) || iPadOs;
    },

    // Current install state, read by the Blazor component.
    getInstallState: function() {
        return {
            installed: this.isInstalled(),
            canPrompt: !!this.deferredPrompt,
            isIos: this.isIos()
        };
    },

    // Blazor components register a DotNetObjectReference to hear about changes.
    subscribeInstallState: function(dotNetRef) {
        this.installListeners.push(dotNetRef);
        return this.getInstallState();
    },

    unsubscribeInstallState: function(dotNetRef) {
        this.installListeners = this.installListeners.filter(r => r !== dotNetRef);
    },

    notifyInstallState: function() {
        const state = this.getInstallState();
        this.installListeners.forEach(ref => {
            ref.invokeMethodAsync('OnInstallStateChanged', state).catch(() => { /* component disposed */ });
        });
    },

    // Show the browser's install dialog. Returns 'accepted', 'dismissed' or 'unavailable'.
    showInstallPrompt: async function() {
        const prompt = this.deferredPrompt;
        if (!prompt) {
            return 'unavailable';
        }

        prompt.prompt();
        const result = await prompt.userChoice;
        console.log('[PWA] Install prompt result:', result.outcome);

        // A prompt event can only be used once.
        this.deferredPrompt = null;
        this.notifyInstallState();
        return result.outcome;
    },

    // Setup install prompt
    setupInstallPrompt: function() {
        window.addEventListener('beforeinstallprompt', (e) => {
            console.log('[PWA] Install prompt available');
            e.preventDefault(); // keep it for our own "Install app" button
            this.deferredPrompt = e;
            this.notifyInstallState();
        });

        window.addEventListener('appinstalled', () => {
            console.log('[PWA] App installed successfully');
            this.deferredPrompt = null;
            this.notifyInstallState();
        });
    },

    // Request notification permission
    requestNotificationPermission: async function() {
        if (!('Notification' in window)) {
            console.warn('Notifications not supported');
            return 'unsupported';
        }

        if (Notification.permission === 'granted') {
            return 'granted';
        }

        if (Notification.permission !== 'denied') {
            const permission = await Notification.requestPermission();
            console.log('Notification permission:', permission);
            return permission;
        }

        return Notification.permission;
    },

    // Show notification
    showNotification: function(title, options) {
        if (Notification.permission === 'granted' && this.registration) {
            this.registration.showNotification(title, {
                icon: '/icon-192.svg',
                badge: '/icon-192.svg',
                vibrate: [200, 100, 200],
                ...options
            });
        }
    },

    // Clear all caches
    clearCache: async function() {
        const cacheNames = await caches.keys();
        await Promise.all(cacheNames.map(name => caches.delete(name)));
        console.log('All caches cleared');
        
        if (this.registration && this.registration.active) {
            this.registration.active.postMessage({ type: 'CLEAR_CACHE' });
        }
    },

    // Get cache size
    getCacheSize: async function() {
        if (!('storage' in navigator && 'estimate' in navigator.storage)) {
            return null;
        }

        const estimate = await navigator.storage.estimate();
        return {
            usage: estimate.usage,
            quota: estimate.quota,
            usageInMB: (estimate.usage / (1024 * 1024)).toFixed(2),
            quotaInMB: (estimate.quota / (1024 * 1024)).toFixed(2),
            percentUsed: ((estimate.usage / estimate.quota) * 100).toFixed(2)
        };
    }
};

// Auto-register on load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        console.log('[PWA] DOMContentLoaded - initializing...');
        pwaService.register()
            .then(success => console.log('[PWA] Registration result:', success))
            .catch(err => console.error('[PWA] Registration error:', err));
        pwaService.setupInstallPrompt();
    });
} else {
    console.log('[PWA] Document ready - initializing...');
    pwaService.register()
        .then(success => console.log('[PWA] Registration result:', success))
        .catch(err => console.error('[PWA] Registration error:', err));
    pwaService.setupInstallPrompt();
}

console.log('PWA Service loaded');
