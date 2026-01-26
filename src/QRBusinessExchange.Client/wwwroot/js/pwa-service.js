// Service Worker Registration for QR Business Exchange PWA
window.pwaService = {
    registration: null,
    updateAvailable: false,

    // Register service worker
    register: async function() {
        if (!('serviceWorker' in navigator)) {
            console.warn('Service Worker not supported in this browser');
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
            <div style="position: fixed; bottom: 20px; right: 20px; background: #4A90E2; color: white; padding: 20px; border-radius: 10px; box-shadow: 0 4px 20px rgba(0,0,0,0.3); z-index: 10000; max-width: 300px;">
                <h4 style="margin: 0 0 10px 0;">Update Available</h4>
                <p style="margin: 0 0 15px 0;">A new version of QR Business Exchange is available.</p>
                <button id="pwa-update-btn" style="background: white; color: #4A90E2; border: none; padding: 10px 20px; border-radius: 5px; cursor: pointer; font-weight: bold; margin-right: 10px;">
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

    // Show install prompt
    showInstallPrompt: null,
    
    // Setup install prompt
    setupInstallPrompt: function() {
        let deferredPrompt = null;

        window.addEventListener('beforeinstallprompt', (e) => {
            console.log('Install prompt available');
            e.preventDefault();
            deferredPrompt = e;

            // Show custom install button
            this.showInstallButton(deferredPrompt);
        });

        window.addEventListener('appinstalled', () => {
            console.log('App installed successfully');
            deferredPrompt = null;
        });
    },

    // Show install button
    showInstallButton: function(deferredPrompt) {
        // Create install button
        const installBtn = document.createElement('div');
        installBtn.id = 'pwa-install-prompt';
        installBtn.innerHTML = `
            <div style="position: fixed; bottom: 20px; left: 20px; background: #50C878; color: white; padding: 15px 20px; border-radius: 10px; box-shadow: 0 4px 20px rgba(0,0,0,0.3); z-index: 10000; cursor: pointer; display: flex; align-items: center; gap: 10px;">
                <i class="fas fa-download"></i>
                <span>Install App</span>
            </div>
        `;

        document.body.appendChild(installBtn);

        installBtn.addEventListener('click', async () => {
            if (!deferredPrompt) return;

            deferredPrompt.prompt();
            const result = await deferredPrompt.userChoice;

            console.log('Install prompt result:', result.outcome);
            
            if (result.outcome === 'accepted') {
                console.log('User accepted install');
            } else {
                console.log('User dismissed install');
            }

            deferredPrompt = null;
            installBtn.remove();
        });

        // Auto-hide after 10 seconds
        setTimeout(() => {
            if (installBtn.parentElement) {
                installBtn.remove();
            }
        }, 10000);
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
        
        if (this.registration) {
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
