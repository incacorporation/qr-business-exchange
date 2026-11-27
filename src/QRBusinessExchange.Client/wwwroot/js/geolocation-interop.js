// Geolocation Interop for Blazor WebAssembly
window.geolocationInterop = {
    
    getCurrentPosition: function () {
        return new Promise((resolve, reject) => {
            if (!navigator.geolocation) {
                reject(new Error('Geolocation is not supported by this browser'));
                return;
            }

            const timeout = setTimeout(() => {
                reject(new Error('Geolocation request timeout'));
            }, 10000);

            navigator.geolocation.getCurrentPosition(
                (position) => {
                    clearTimeout(timeout);
                    const location = {
                        latitude: position.coords.latitude,
                        longitude: position.coords.longitude,
                        accuracy: position.coords.accuracy
                    };
                    console.log('Current location obtained:', location);
                    resolve(location);
                },
                (error) => {
                    clearTimeout(timeout);
                    let errorMessage = 'Unable to retrieve location';
                    
                    switch (error.code) {
                        case error.PERMISSION_DENIED:
                            errorMessage = 'User denied geolocation permission';
                            break;
                        case error.POSITION_UNAVAILABLE:
                            errorMessage = 'Location information unavailable';
                            break;
                        case error.TIMEOUT:
                            errorMessage = 'Location request timeout';
                            break;
                    }
                    
                    console.warn('Geolocation error:', errorMessage);
                    reject(new Error(errorMessage));
                },
                {
                    enableHighAccuracy: true,
                    timeout: 8000,
                    maximumAge: 5000 // reuse a fix up to 5 s old (the Dashboard map refreshes every 10 s)
                }
            );
        });
    },

    watchPosition: function (dotNetHelper) {
        if (!navigator.geolocation) {
            console.error('Geolocation not supported');
            return -1;
        }

        const watchId = navigator.geolocation.watchPosition(
            (position) => {
                const location = {
                    latitude: position.coords.latitude,
                    longitude: position.coords.longitude,
                    accuracy: position.coords.accuracy
                };
                
                dotNetHelper.invokeMethodAsync('OnLocationChanged', location)
                    .catch(err => console.error('Error invoking OnLocationChanged:', err));
            },
            (error) => {
                console.error('Watch position error:', error.message);
            },
            {
                enableHighAccuracy: true,
                timeout: 5000,
                maximumAge: 0
            }
        );

        console.log('Started watching position, watchId:', watchId);
        return watchId;
    },

    clearWatch: function (watchId) {
        if (watchId && watchId > 0) {
            navigator.geolocation.clearWatch(watchId);
            console.log('Stopped watching position, watchId:', watchId);
        }
    },

    checkPermission: function () {
        return new Promise((resolve) => {
            if (!navigator.permissions) {
                resolve('unsupported');
                return;
            }

            navigator.permissions.query({ name: 'geolocation' })
                .then((result) => {
                    console.log('Geolocation permission:', result.state);
                    resolve(result.state); // 'granted', 'denied', or 'prompt'
                })
                .catch(() => {
                    resolve('unsupported');
                });
        });
    }
};

console.log('Geolocation interop loaded');
