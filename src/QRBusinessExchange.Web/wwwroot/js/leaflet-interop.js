// Leaflet Map Interop
window.leafletInterop = {
    map: null,

    initializeMap: function (elementId, lat, lng, zoom) {
        if (this.map) {
            this.map.remove();
        }

        this.map = L.map(elementId).setView([lat, lng], zoom);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
            maxZoom: 19
        }).addTo(this.map);
    },

    addMarker: function (lat, lng, popupContent, isCurrentUser) {
        if (!this.map) {
            console.error('Map not initialized');
            return;
        }

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

        const marker = L.marker([lat, lng], markerOptions).addTo(this.map);
        
        if (popupContent) {
            marker.bindPopup(popupContent);
        }

        return marker;
    },

    clearMarkers: function () {
        if (this.map) {
            this.map.eachLayer(function (layer) {
                if (layer instanceof L.Marker) {
                    layer.remove();
                }
            });
        }
    }
};
