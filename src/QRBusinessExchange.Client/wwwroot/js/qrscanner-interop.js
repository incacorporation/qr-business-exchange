// Camera QR scanner for Pages/Scan.razor.
// Uses the browser's built-in BarcodeDetector where it exists (Chrome/Android) and falls back to jsQR,
// loaded on demand from jsDelivr with a pinned integrity hash (Safari/iOS has no BarcodeDetector).
window.qrScanner = (() => {
    const JSQR_URL = 'https://cdn.jsdelivr.net/npm/jsqr@1.4.0/dist/jsQR.js';
    const JSQR_INTEGRITY = 'sha256-vEDIoVGWI2sjFNsIVvcsoLSZgM1UE7jIUqc0n1/uCFk=';
    const MOBILE_QUERY = '(max-width: 991.98px)'; // same breakpoint as the app's mobile layout (Bootstrap lg)
    const SCAN_INTERVAL_MS = 200;

    let stream = null;
    let timer = null;
    let jsQrLoading = null;

    function loadJsQr() {
        if (window.jsQR) return Promise.resolve();
        if (!jsQrLoading) {
            jsQrLoading = new Promise((resolve, reject) => {
                const s = document.createElement('script');
                s.src = JSQR_URL;
                s.integrity = JSQR_INTEGRITY;
                s.crossOrigin = 'anonymous';
                s.onload = () => resolve();
                s.onerror = () => { jsQrLoading = null; reject(new Error('DecoderUnavailable')); };
                document.head.appendChild(s);
            });
        }
        return jsQrLoading;
    }

    async function createDecoder() {
        if ('BarcodeDetector' in window) {
            try {
                const formats = await window.BarcodeDetector.getSupportedFormats();
                if (formats.includes('qr_code')) {
                    const detector = new window.BarcodeDetector({ formats: ['qr_code'] });
                    return async video => {
                        const codes = await detector.detect(video);
                        return codes.length ? codes[0].rawValue : null;
                    };
                }
            } catch { /* fall back to jsQR */ }
        }

        await loadJsQr();
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d', { willReadFrequently: true });
        let pass = 0;
        return async video => {
            const w = video.videoWidth, h = video.videoHeight;
            if (!w || !h) return null;
            // Alternate between the centre square (what the on-screen frame shows, at full detail, so
            // dense codes on a monitor keep enough pixels per module) and the whole picture (codes held
            // off-centre). Each is capped at 1024px so decoding stays quick on phones.
            const centre = (pass++ % 2) === 0;
            const side = Math.min(w, h);
            const [sx, sy, sw, sh] = centre ? [(w - side) / 2, (h - side) / 2, side, side] : [0, 0, w, h];
            const scale = Math.min(1, 1024 / Math.max(sw, sh));
            canvas.width = Math.round(sw * scale);
            canvas.height = Math.round(sh * scale);
            ctx.drawImage(video, sx, sy, sw, sh, 0, 0, canvas.width, canvas.height);
            const img = ctx.getImageData(0, 0, canvas.width, canvas.height);
            const code = window.jsQR(img.data, img.width, img.height, { inversionAttempts: 'attemptBoth' });
            return code ? code.data : null;
        };
    }

    // Without a size, many phones hand the browser 640x480: too little detail for a QR code on a
    // monitor. Ask for HD (the browser picks the closest the camera supports).
    function openCamera(video) {
        return navigator.mediaDevices.getUserMedia({
            video: { ...video, width: { ideal: 1920 }, height: { ideal: 1080 } },
            audio: false
        });
    }

    // iPhone Pro models: Safari's default back camera is the main lens, which can't focus closer than
    // about 20 cm, so a code on a nearby screen stays blurry. Safari also offers the "Back Triple Camera"
    // (or "Back Dual Wide Camera"), which switches to the ultra-wide/macro lens up close like the Camera
    // app does. Camera names can only be read after permission is granted, so check after opening one.
    // Names are English on most devices; anything else keeps the default camera.
    async function preferAutoSwitchingCamera(current) {
        let best;
        try {
            const devices = (await navigator.mediaDevices.enumerateDevices()).filter(d => d.kind === 'videoinput');
            best = devices.find(d => /triple camera/i.test(d.label)) ?? devices.find(d => /dual wide camera/i.test(d.label));
            const currentId = current.getVideoTracks()[0]?.getSettings().deviceId;
            if (!best || !best.deviceId || best.deviceId === currentId) {
                return current;
            }
        } catch {
            return current;
        }

        current.getTracks().forEach(t => t.stop()); // iOS allows only one open camera at a time
        try {
            return await openCamera({ deviceId: { exact: best.deviceId } });
        } catch {
            try {
                return await openCamera({ facingMode: { ideal: 'environment' } }); // put the default back
            } catch (e) {
                throw new Error(e && e.name ? e.name : 'CameraError');
            }
        }
    }

    // Bumped by every stop(); a start() that was still opening the camera when stop() ran sees the change
    // and closes the camera it just opened (e.g. returning from the photo picker on iPhone).
    let generation = 0;

    function stop() {
        generation++;
        if (timer) { clearTimeout(timer); timer = null; }
        if (stream) { stream.getTracks().forEach(t => t.stop()); stream = null; }
    }

    // Starts the rear camera in <video> and calls dotnet.OnQrDetected(text) once, for the first code seen.
    // Throws an Error whose message is a reason code the page turns into friendly text.
    async function start(video, dotnet) {
        stop();
        const myGeneration = generation;
        if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
            throw new Error('Unsupported');
        }

        const decode = await createDecoder();
        let opened;
        try {
            opened = await openCamera({ facingMode: { ideal: 'environment' } });
        } catch (e) {
            throw new Error(e && e.name ? e.name : 'CameraError'); // NotAllowedError, NotFoundError, NotReadableError...
        }
        opened = await preferAutoSwitchingCamera(opened);
        if (myGeneration !== generation) {
            opened.getTracks().forEach(t => t.stop()); // stopped while opening: don't leave the camera on
            return;
        }
        stream = opened;

        // Keep refocusing as the phone moves closer to the screen, where the camera supports it.
        try {
            const track = stream.getVideoTracks()[0];
            const caps = track.getCapabilities ? track.getCapabilities() : {};
            if (caps.focusMode && caps.focusMode.includes('continuous')) {
                await track.applyConstraints({ advanced: [{ focusMode: 'continuous' }] });
            }
        } catch { /* optional */ }

        video.srcObject = stream;
        video.setAttribute('playsinline', ''); // iOS: play inline instead of full screen
        video.muted = true;
        await video.play();

        const tick = async () => {
            if (!stream) return;
            let text = null;
            try { text = await decode(video); } catch { /* frame not ready */ }
            if (!stream) return;
            if (text) {
                stop();
                await dotnet.invokeMethodAsync('OnQrDetected', text);
                return;
            }
            timer = setTimeout(tick, SCAN_INTERVAL_MS);
        };
        tick();
    }

    // Turn the camera off while the tab is in the background (so its light doesn't stay on), and back on
    // when the user returns, if it was scanning.
    let paused = null;
    document.addEventListener('visibilitychange', () => {
        if (document.hidden && stream) {
            paused = { video: lastVideo, dotnet: lastDotnet };
            stop();
        } else if (!document.hidden && paused) {
            const { video, dotnet } = paused;
            paused = null;
            if (video.isConnected) {
                start(video, dotnet).catch(e => dotnet.invokeMethodAsync('OnScannerError', e.message));
            }
        }
    });

    let lastVideo = null, lastDotnet = null;

    // Reads a QR code from a photo (bytes of a JPEG/PNG from "Take a photo instead"). The phone's own
    // camera app focuses close up properly, so this works where the live view can't. Phone photos are
    // large (24MP+), so try the whole picture and then centre crops, each scaled to a decodable size.
    async function decodeImage(bytes) {
        const bitmap = await createImageBitmap(new Blob([bytes]));
        try {
            let detector = null;
            if ('BarcodeDetector' in window) {
                try {
                    if ((await window.BarcodeDetector.getSupportedFormats()).includes('qr_code')) {
                        detector = new window.BarcodeDetector({ formats: ['qr_code'] });
                    }
                } catch { /* use jsQR */ }
            }
            if (!detector) await loadJsQr();

            const canvas = document.createElement('canvas');
            const ctx = canvas.getContext('2d', { willReadFrequently: true });
            const w = bitmap.width, h = bitmap.height;
            for (const [fraction, maxSide] of [[1, 1600], [0.6, 1400], [0.35, 1200], [1, 2400]]) {
                const sw = w * fraction, sh = h * fraction;
                const scale = Math.min(1, maxSide / Math.max(sw, sh));
                canvas.width = Math.round(sw * scale);
                canvas.height = Math.round(sh * scale);
                ctx.drawImage(bitmap, (w - sw) / 2, (h - sh) / 2, sw, sh, 0, 0, canvas.width, canvas.height);
                if (detector) {
                    const codes = await detector.detect(canvas);
                    if (codes.length) return codes[0].rawValue;
                } else {
                    const img = ctx.getImageData(0, 0, canvas.width, canvas.height);
                    const code = window.jsQR(img.data, img.width, img.height, { inversionAttempts: 'attemptBoth' });
                    if (code) return code.data;
                }
            }
            return null;
        } finally {
            bitmap.close?.();
        }
    }

    return {
        isMobile: () => window.matchMedia(MOBILE_QUERY).matches,
        decodeImage,
        start: (video, dotnet) => { lastVideo = video; lastDotnet = dotnet; return start(video, dotnet); },
        stop: () => { paused = null; lastVideo = lastDotnet = null; stop(); }
    };
})();
