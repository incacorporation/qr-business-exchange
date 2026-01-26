// QR Code Interop for Blazor Server
window.qrCodeInterop = (function () {
    
    function generateQRCode(elementId, text) {
        return new Promise((resolve, reject) => {
            try {
                const timeout = setTimeout(() => {
                    reject(new Error('QR code generation timeout'));
                }, 5000);

                const element = document.getElementById(elementId);
                if (!element) {
                    clearTimeout(timeout);
                    reject(new Error('Element not found: ' + elementId));
                    return;
                }

                // Check if QRCode library is loaded
                if (typeof QRCode === 'undefined') {
                    clearTimeout(timeout);
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
    }

    function downloadQRCode(elementId, filename) {
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
    }

    function printQRCode(elementId) {
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
    }

    async function shareQRCode(url) {
        try {
            if (navigator.share) {
                await navigator.share({
                    title: 'My QR Business Profile',
                    text: 'Check out my business profile!',
                    url: url
                });
                console.log('QR code shared successfully');
            } else {
                await fallbackShare(url);
            }
        } catch (error) {
            console.log('Share error or cancelled:', error);
            // Try fallback
            try {
                await fallbackShare(url);
            } catch (fallbackError) {
                console.error('Fallback share error:', fallbackError);
                throw fallbackError;
            }
        }
    }

    async function fallbackShare(url) {
        try {
            await navigator.clipboard.writeText(url);
            alert('Profile URL copied to clipboard!');
            console.log('URL copied to clipboard');
        } catch (error) {
            console.error('Failed to copy URL:', error);
            throw error;
        }
    }

    // Public API
    return {
        generateQRCode: generateQRCode,
        downloadQRCode: downloadQRCode,
        printQRCode: printQRCode,
        shareQRCode: shareQRCode
    };
})();

console.log('QR Code interop loaded');

