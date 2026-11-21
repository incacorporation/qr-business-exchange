// Leaflet Map Interop for Blazor Server
window.leafletInterop = (function () {
    let map = null;
    let markers = [];

    function initializeMap(elementId, lat, lng, zoom) {
        return new Promise((resolve, reject) => {
            try {
                const timeout = setTimeout(() => {
                    reject(new Error('Map initialization timeout'));
                }, 10000);

                const element = document.getElementById(elementId);
                if (!element) {
                    clearTimeout(timeout);
                    reject(new Error('Map element not found: ' + elementId));
                    return;
                }

                // Remove existing map if it exists
                if (map) {
                    try {
                        map.remove();
                        map = null;
                        markers = [];
                    } catch (e) {
                        console.warn('Error removing previous map:', e);
                    }
                }

                // Check if Leaflet is loaded
                if (typeof L === 'undefined') {
                    clearTimeout(timeout);
                    reject(new Error('Leaflet library not loaded'));
                    return;
                }

                map = L.map(elementId).setView([lat, lng], zoom);

                L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
                    maxZoom: 19
                }).addTo(map);

                clearTimeout(timeout);
                console.log('Map initialized successfully:', elementId);
                resolve();
            } catch (error) {
                console.error('Map initialization error:', error);
                reject(error);
            }
        });
    }

    function addMarker(lat, lng, popupContent, isCurrentUser) {
        return new Promise((resolve, reject) => {
            try {
                if (!map) {
                    reject(new Error('Map not initialized'));
                    return;
                }

                const timeout = setTimeout(() => {
                    reject(new Error('Add marker timeout'));
                }, 3000);

                const markerOptions = isCurrentUser ? {
                    icon: L.icon({
                        iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-blue.png',
                        shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-shadow.png',
                        iconSize: [25, 41],
                        iconAnchor: [12, 41],
                        popupAnchor: [1, -34],
                        shadowSize: [41, 41]
                    })
                } : {};

                const marker = L.marker([lat, lng], markerOptions).addTo(map);
                markers.push(marker);
                
                if (popupContent) {
                    marker.bindPopup(popupContent);
                }

                clearTimeout(timeout);
                resolve();
            } catch (error) {
                console.error('Add marker error:', error);
                reject(error);
            }
        });
    }

    function clearMarkers() {
        try {
            if (map) {
                markers.forEach(marker => {
                    try {
                        marker.remove();
                    } catch (e) {
                        console.warn('Error removing marker:', e);
                    }
                });
                markers = [];
                console.log('Markers cleared');
            }
        } catch (error) {
            console.error('Error clearing markers:', error);
        }
    }

    function destroyMap() {
        try {
            if (map) {
                clearMarkers();
                map.remove();
                map = null;
                console.log('Map destroyed');
            }
        } catch (error) {
            console.error('Error destroying map:', error);
        }
    }

    // Public API
    return {
        initializeMap: initializeMap,
        addMarker: addMarker,
        clearMarkers: clearMarkers,
        destroyMap: destroyMap
    };
})();

console.log('Leaflet interop loaded');

