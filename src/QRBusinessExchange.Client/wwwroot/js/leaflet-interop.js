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

    // Map pin drawn as SVG in the given colour (no image files to download).
    pinIcon: function (color) {
        // An older copy of the app passes true/false ("is this you") instead of a colour.
        if (color === true) color = '#D4A017';
        const safe = /^#[0-9a-fA-F]{3,8}$/.test(typeof color === 'string' ? color : '') ? color : '#0D6EFD';
        const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="28" height="40" viewBox="0 0 28 40" aria-hidden="true">`
            + `<path d="M14 1C7 1 1.5 6.5 1.5 13.5c0 9.4 12.5 25 12.5 25s12.5-15.6 12.5-25C26.5 6.5 21 1 14 1z" fill="${safe}" stroke="#fff" stroke-width="2"/>`
            + `<circle cx="14" cy="13.5" r="5" fill="#fff"/></svg>`;
        return L.divIcon({ html: svg, className: 'qb-pin', iconSize: [28, 40], iconAnchor: [14, 39], popupAnchor: [0, -34] });
    },

    addMarker: function (lat, lng, popupContent, color) {
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

                // Gold for you, green for your contacts, blue for everyone else (chosen by the caller).
                const marker = L.marker([lat, lng], { icon: self.pinIcon(color), zIndexOffset: (color === true || color === '#D4A017') ? 1000 : 0 }).addTo(self.map);
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

    // The search distance as a circle around you (replaces the previous one).
    setRangeCircle: function (lat, lng, meters) {
        if (!this.map) return;
        if (this.rangeCircle) {
            this.rangeCircle.remove();
        }
        this.rangeCircle = L.circle([lat, lng], {
            radius: meters, color: '#D4A017', weight: 2, fillColor: '#D4A017', fillOpacity: 0.08, interactive: false
        }).addTo(this.map);
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


