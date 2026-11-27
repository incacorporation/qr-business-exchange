// Saves text made in the app (e.g. a CSV report) as a file download.
window.fileDownload = {
    saveText: function (fileName, text, mimeType) {
        const blob = new Blob([text], { type: mimeType || 'text/plain;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }
};
