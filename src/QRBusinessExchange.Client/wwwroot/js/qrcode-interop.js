// QR Code Interop for Blazor WebAssembly
window.qrCodeInterop = {
    
    generateQRCode: function (elementId, text) {
        return new Promise((resolve, reject) => {
            try {
                const timeout = setTimeout(() => {
                    reject(new Error('QR code generation timeout'));
                }, 5000);

                const element = document.getElementById(elementId);
                if (!element) {
                    clearTimeout(timeout);
                    console.error('Element not found:', elementId);
                    reject(new Error('Element not found: ' + elementId));
                    return;
                }

                // Check if QRCode library is loaded
                if (typeof QRCode === 'undefined') {
                    clearTimeout(timeout);
                    console.error('QRCode library not loaded');
                    reject(new Error('QRCode library not loaded'));
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

                clearTimeout(timeout);
                console.log('QR code generated successfully:', elementId);
                resolve();
            } catch (error) {
                console.error('QR code generation error:', error);
                reject(error);
            }
        });
    },

    downloadQRCode: function (elementId, filename) {
        return new Promise((resolve, reject) => {
            try {
                const timeout = setTimeout(() => {
                    reject(new Error('QR code download timeout'));
                }, 3000);

                const element = document.getElementById(elementId);
                if (!element) {
                    clearTimeout(timeout);
                    reject(new Error('Element not found: ' + elementId));
                    return;
                }

                const canvas = element.querySelector('canvas');
                if (!canvas) {
                    clearTimeout(timeout);
                    reject(new Error('QR Code canvas not found'));
                    return;
                }

                const url = canvas.toDataURL('image/png');
                const link = document.createElement('a');
                link.download = filename || 'qrcode.png';
                link.href = url;
                link.click();

                clearTimeout(timeout);
                console.log('QR code downloaded:', filename);
                resolve();
            } catch (error) {
                console.error('QR code download error:', error);
                reject(error);
            }
        });
    },

    printQRCode: function (elementId) {
        return new Promise((resolve, reject) => {
            try {
                const timeout = setTimeout(() => {
                    reject(new Error('QR code print timeout'));
                }, 3000);

                const element = document.getElementById(elementId);
                if (!element) {
                    clearTimeout(timeout);
                    reject(new Error('Element not found: ' + elementId));
                    return;
                }

                const canvas = element.querySelector('canvas');
                if (!canvas) {
                    clearTimeout(timeout);
                    reject(new Error('QR Code canvas not found'));
                    return;
                }

                const printWindow = window.open('', '_blank');
                if (!printWindow) {
                    clearTimeout(timeout);
                    reject(new Error('Could not open print window'));
                    return;
                }

                printWindow.document.write('<html><head><title>Print QR Code</title></head><body>');
                printWindow.document.write('<img src="' + canvas.toDataURL('image/png') + '" style="max-width:100%;" />');
                printWindow.document.write('</body></html>');
                printWindow.document.close();
                printWindow.print();

                clearTimeout(timeout);
                console.log('QR code printed');
                resolve();
            } catch (error) {
                console.error('QR code print error:', error);
                reject(error);
            }
        });
    },

    shareQRCode: function (url) {
        return new Promise(async (resolve, reject) => {
            try {
                if (navigator.share) {
                    await navigator.share({
                        title: 'My QR Business Profile',
                        text: 'Check out my business profile!',
                        url: url
                    });
                    console.log('QR code shared successfully');
                    resolve();
                } else {
                    // Fallback to clipboard
                    await this.fallbackShare(url);
                    resolve();
                }
            } catch (error) {
                console.log('Share error or cancelled:', error);
                // Try fallback
                try {
                    await this.fallbackShare(url);
                    resolve();
                } catch (fallbackError) {
                    console.error('Fallback share error:', fallbackError);
                    reject(fallbackError);
                }
            }
        });
    },

    fallbackShare: function (url) {
        return new Promise(async (resolve, reject) => {
            try {
                await navigator.clipboard.writeText(url);
                alert('Profile URL copied to clipboard!');
                console.log('URL copied to clipboard');
                resolve();
            } catch (error) {
                console.error('Failed to copy URL:', error);
                reject(error);
            }
        });
    }
};

console.log('QR Code interop loaded');

