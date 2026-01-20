// QR код скенер с камера
class QRScanner {
    constructor(videoElement, onScanSuccess) {
        this.video = videoElement;
        this.onScanSuccess = onScanSuccess;
        this.stream = null;
        this.scanning = false;
    }

    async start() {
        try {
            // Заявка за достъп до камерата
            this.stream = await navigator.mediaDevices.getUserMedia({
                video: {
                    facingMode: 'environment', // Задня камера
                    width: { ideal: 1280 },
                    height: { ideal: 720 }
                }
            });

            this.video.srcObject = this.stream;
            this.video.play();
            this.scanning = true;

            // Използване на jsQR библиотека за декодиране
            this.scanQR();
        } catch (error) {
            console.error('Грешка при достъп до камерата:', error);
            throw new Error('Не може да се достъпи камерата. Моля, проверете разрешенията.');
        }
    }

    stop() {
        this.scanning = false;
        if (this.stream) {
            this.stream.getTracks().forEach(track => track.stop());
            this.stream = null;
        }
        if (this.video) {
            this.video.srcObject = null;
        }
    }

    scanQR() {
        if (!this.scanning) return;

        const canvas = document.createElement('canvas');
        const context = canvas.getContext('2d');

        const scanFrame = () => {
            if (!this.scanning || this.video.readyState !== this.video.HAVE_ENOUGH_DATA) {
                requestAnimationFrame(scanFrame);
                return;
            }

            canvas.width = this.video.videoWidth;
            canvas.height = this.video.videoHeight;
            context.drawImage(this.video, 0, 0, canvas.width, canvas.height);

            const imageData = context.getImageData(0, 0, canvas.width, canvas.height);

            // Проверка дали jsQR е зареден
            if (typeof jsQR !== 'undefined') {
                const code = jsQR(imageData.data, imageData.width, imageData.height);

                if (code) {
                    this.stop();
                    this.onScanSuccess(code.data);
                    return;
                }
            }

            requestAnimationFrame(scanFrame);
        };

        scanFrame();
    }
}

// Глобална функция за стартиране на скенер
window.startQRScanner = function(videoElementId, onSuccess) {
    const video = document.getElementById(videoElementId);
    if (!video) {
        throw new Error('Video елементът не е намерен');
    }

    const scanner = new QRScanner(video, onSuccess);
    scanner.start().catch(error => {
        alert(error.message);
    });

    return scanner;
};

