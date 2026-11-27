// Tells the app when it should reload the signed-in user's profile from the server:
// when the tab or installed app comes back to the foreground (the profile may have been changed on
// another device meanwhile), and when another tab of this browser saved a newer profile.

// Ensure this module is defined globally
if (!window.appVisibility) {
    window.appVisibility = (() => {
        let dotnet = null;

        function onVisibility() {
            if (dotnet && document.visibilityState === 'visible') {
                dotnet.invokeMethodAsync('OnAppVisible').catch(() => { });
            }
        }

        function onStorage(e) {
            if (dotnet && e.key === 'currentUser') {
                dotnet.invokeMethodAsync('OnProfileChangedElsewhere').catch(() => { });
            }
        }

        return {
            watch(ref) {
                dotnet = ref;
                document.addEventListener('visibilitychange', onVisibility);
                window.addEventListener('storage', onStorage);
            },
            unwatch() {
                dotnet = null;
                document.removeEventListener('visibilitychange', onVisibility);
                window.removeEventListener('storage', onStorage);
            }
        };
    })();

    // Log that the module loaded successfully
    if (typeof console !== 'undefined' && console.log) {
        console.log('App Visibility interop loaded');
    }
}
