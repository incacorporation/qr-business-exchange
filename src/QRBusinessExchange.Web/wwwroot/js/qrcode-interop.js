// QR Code Interop
window.qrCodeInterop = {
    generateQRCode: function (elementId, text) {
        const element = document.getElementById(elementId);
        if (!element) {
            console.error('Element not found:', elementId);
            return;
        }

        // Clear existing QR code
        element.innerHTML = '';

        // Generate QR code using QRCode.js library
        new QRCode(element, {
            text: text,
            width: 256,
            height: 256,
            colorDark: '#000000',
            colorLight: '#ffffff',
            correctLevel: QRCode.CorrectLevel.H
        });
    },

    downloadQRCode: function (elementId, filename) {
        const element = document.getElementById(elementId);
        if (!element) {
            console.error('Element not found:', elementId);
            return;
        }

        const canvas = element.querySelector('canvas');
        if (!canvas) {
            console.error('QR Code canvas not found');
            return;
        }

        const url = canvas.toDataURL('image/png');
        const link = document.createElement('a');
        link.download = filename;
        link.href = url;
        link.click();
    },

    printQRCode: function (elementId) {
        const element = document.getElementById(elementId);
        if (!element) {
            console.error('Element not found:', elementId);
            return;
        }

        const canvas = element.querySelector('canvas');
        if (!canvas) {
            console.error('QR Code canvas not found');
            return;
        }

        const printWindow = window.open('', '_blank');
        printWindow.document.write('<html><head><title>Print QR Code</title></head><body>');
        printWindow.document.write('<img src="' + canvas.toDataURL('image/png') + '" style="max-width:100%;" />');
        printWindow.document.write('</body></html>');
        printWindow.document.close();
        printWindow.print();
    },

    shareQRCode: async function (url) {
        if (navigator.share) {
            try {
                await navigator.share({
                    title: 'My QR Business Profile',
                    text: 'Check out my business profile!',
                    url: url
                });
            } catch (error) {
                console.log('Error sharing:', error);
                this.fallbackShare(url);
            }
        } else {
            this.fallbackShare(url);
        }
    },

    fallbackShare: function (url) {
        navigator.clipboard.writeText(url).then(() => {
            alert('Profile URL copied to clipboard!');
        }).catch(err => {
            console.error('Failed to copy:', err);
        });
    }
};
