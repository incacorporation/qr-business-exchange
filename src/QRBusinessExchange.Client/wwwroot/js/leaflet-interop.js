// Leaflet Map Interop for Blazor WebAssembly
window.leafletInterop = {
    map: null,
    markers: [],

    initializeMap: function (elementId, lat, lng, zoom) {
        const self = this;
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
                if (self.map) {
                    try {
                        console.log('Removing existing map instance...');
                        self.clearMarkers();
                        self.map.remove();
                        self.map = null;
                    } catch (e) {
                        console.warn('Error removing previous map:', e);
                    }
                }

                // Clear any orphaned map instances attached to this element
                try {
                    element._leaflet_id = undefined;
                    element.innerHTML = '';
                } catch (e) {
                    console.warn('Error clearing element:', e);
                }

                // Check if Leaflet is loaded
                if (typeof L === 'undefined') {
                    clearTimeout(timeout);
                    reject(new Error('Leaflet library not loaded'));
                    return;
                }

                // Create map
                self.map = L.map(elementId).setView([lat, lng], zoom);

                // Add tile layer
                L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
                    maxZoom: 19
                }).addTo(self.map);

                clearTimeout(timeout);
                console.log('Map initialized successfully:', elementId);
                resolve();
            } catch (error) {
                console.error('Map initialization error:', error);
                reject(error);
            }
        });
    },

    addMarker: function (lat, lng, popupContent, isCurrentUser) {
        const self = this;
        return new Promise((resolve, reject) => {
            try {
                if (!self.map) {
                    console.warn('Map not initialized, marker not added');
                    resolve(); // Don't reject, just resolve without adding marker
                    return;
                }

                const timeout = setTimeout(() => {
                    reject(new Error('Add marker timeout'));
                }, 3000);

                // Create custom icon for current user
                let markerOptions = {};
                if (isCurrentUser) {
                    markerOptions = {
                        icon: L.icon({
                            iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-2x-blue.png',
                            shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-shadow.png',
                            iconSize: [25, 41],
                            iconAnchor: [12, 41],
                            popupAnchor: [1, -34],
                            shadowSize: [41, 41]
                        })
                    };
                }

                const marker = L.marker([lat, lng], markerOptions).addTo(self.map);
                self.markers.push(marker);
                
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
    },

    clearMarkers: function () {
        const self = this;
        try {
            if (self.map) {
                self.markers.forEach(marker => {
                    try {
                        marker.remove();
                    } catch (e) {
                        console.warn('Error removing marker:', e);
                    }
                });
                self.markers = [];
                console.log('Markers cleared');
            }
        } catch (error) {
            console.error('Error clearing markers:', error);
        }
    },

    setView: function (lat, lng, zoom) {
        const self = this;
        try {
            if (self.map) {
                self.map.setView([lat, lng], zoom);
                console.log('Map view updated');
            }
        } catch (error) {
            console.error('Error setting map view:', error);
        }
    },

    destroyMap: function () {
        const self = this;
        try {
            if (self.map) {
                self.clearMarkers();
                self.map.remove();
                self.map = null;
                console.log('Map destroyed');
            }
        } catch (error) {
            console.error('Error destroying map:', error);
        }
    },

    fitBounds: function (bounds) {
        const self = this;
        try {
            if (self.map && bounds && bounds.length === 2) {
                // bounds should be [[lat1, lng1], [lat2, lng2]]
                self.map.fitBounds(bounds);
                console.log('Map bounds fitted');
            }
        } catch (error) {
            console.error('Error fitting bounds:', error);
        }
    }
};

console.log('Leaflet interop loaded');
console.log('Available methods:', Object.keys(window.leafletInterop));


