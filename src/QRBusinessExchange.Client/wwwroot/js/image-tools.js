// Prepares a chosen profile picture for upload: phone photos are often 3-8 MB and 4000 px wide, far
// more than a profile picture needs, so large JPEG/PNG images are scaled down (longest side 800 px).
// PNG stays PNG so logos keep their transparency; GIF is left alone so animations survive.
window.imageTools = (() => {
    const MaxSide = 800;
    const SmallEnough = 300 * 1024; // already-small pictures are sent as they are

    async function prepare(bytes, contentType) {
        const input = new Uint8Array(bytes);
        if (contentType === 'image/gif' || typeof createImageBitmap !== 'function') {
            return { data: input, contentType };
        }

        let bitmap;
        try {
            bitmap = await createImageBitmap(new Blob([input], { type: contentType }));
        } catch {
            return { data: input, contentType }; // the server checks the file either way
        }

        const scale = Math.min(1, MaxSide / Math.max(bitmap.width, bitmap.height));
        if (scale === 1 && input.byteLength <= SmallEnough) {
            bitmap.close();
            return { data: input, contentType };
        }

        const canvas = document.createElement('canvas');
        canvas.width = Math.max(1, Math.round(bitmap.width * scale));
        canvas.height = Math.max(1, Math.round(bitmap.height * scale));
        canvas.getContext('2d').drawImage(bitmap, 0, 0, canvas.width, canvas.height);
        bitmap.close();

        const outType = contentType === 'image/png' ? 'image/png' : 'image/jpeg';
        const blob = await new Promise(resolve => canvas.toBlob(resolve, outType, 0.88));
        if (!blob || blob.size >= input.byteLength) {
            return { data: input, contentType }; // resizing didn't help
        }
        return { data: new Uint8Array(await blob.arrayBuffer()), contentType: outType };
    }

    return { prepare };
})();
