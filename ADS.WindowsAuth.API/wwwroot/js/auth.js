/**
 * ADS Windows Authentication - Auth Page JavaScript
 * QR Scanner and authentication logic
 */

// ============================================
// QR SCANNER MANAGEMENT
// ============================================
let qrScanner = null;

// Initialize QR Scanner when modal is shown
function initQRScanner() {
    const scanBtn = document.getElementById('scanQRBtn');
    const modal = document.getElementById('qrScannerModal');
    const closeBtn = document.getElementById('closeScannerBtn');

    if (!scanBtn || !modal) return;

    // Open scanner modal
    scanBtn.addEventListener('click', function () {
        const bsModal = new bootstrap.Modal(modal);
        bsModal.show();

        // Start scanner after modal animation
        setTimeout(() => {
            try {
                qrScanner = window.startQRScanner('qrVideo', handleQRScan);
            } catch (error) {
                console.error('Грешка при стартиране на скенера:', error);
                document.getElementById('qrScannerError').textContent = error.message;
                document.getElementById('qrScannerError').style.display = 'block';
            }
        }, 300);
    });

    // Stop scanner when modal is closed
    modal.addEventListener('hidden.bs.modal', function () {
        if (qrScanner) {
            qrScanner.stop();
            qrScanner = null;
        }
    });
}

// Handle QR code scan result
function handleQRScan(qrData) {
    console.log('QR код сканиран:', qrData);

    let token = extractToken(qrData);

    if (token) {
        document.getElementById('tokenInput').value = token;
        document.getElementById('manualToken').value = token;

        // Load session info
        loadSessionInfo(token);

        // Close modal
        const modal = bootstrap.Modal.getInstance(document.getElementById('qrScannerModal'));
        if (modal) modal.hide();

        // Show success toast
        if (window.toast) {
            window.toast.success('QR кодът е сканиран успешно!');
        } else {
            alert('QR кодът е сканиран успешно!');
        }
    } else {
        if (window.toast) {
            window.toast.error('Невалиден QR код. Моля, опитайте отново.');
        } else {
            alert('Невалиден QR код. Моля, опитайте отново.\nСканирани данни: ' + qrData.substring(0, 50));
        }
    }
}

// Extract token from QR data
function extractToken(qrData) {
    let token = null;

    // Try parsing as URL
    try {
        const url = new URL(qrData);
        token = url.searchParams.get('token');

        // If no token in query string, try extracting from path
        if (!token && url.pathname.includes('/auth')) {
            const pathParts = url.pathname.split('/');
            const authIndex = pathParts.indexOf('auth');
            if (authIndex >= 0 && pathParts.length > authIndex + 1) {
                token = pathParts[authIndex + 1];
            }
        }
    } catch (e) {
        // Not a valid URL, try as direct token
        console.log('Не е URL, опитваме като директно токен');
    }

    // Try matching token pattern
    if (!token) {
        const tokenMatch = qrData.match(/token[=:]([a-zA-Z0-9\-_]+)/i);
        if (tokenMatch) {
            token = tokenMatch[1];
        } else if (qrData.length > 10 && qrData.length < 200) {
            // Assume entire value is token if reasonable length
            token = qrData.trim();
        }
    }

    return token;
}

// ============================================
// MANUAL TOKEN INPUT
// ============================================
function initManualTokenInput() {
    const loadBtn = document.getElementById('loadTokenBtn');
    const manualInput = document.getElementById('manualToken');

    if (!loadBtn || !manualInput) return;

    loadBtn.addEventListener('click', function () {
        const token = manualInput.value.trim();
        if (token) {
            document.getElementById('tokenInput').value = token;
            loadSessionInfo(token);
        } else {
            if (window.toast) {
                window.toast.warning('Моля, въведете токен');
            } else {
                alert('Моля, въведете токен');
            }
        }
    });

    // Load on Enter key
    manualInput.addEventListener('keypress', function (e) {
        if (e.key === 'Enter') {
            loadBtn.click();
        }
    });
}

// ============================================
// SESSION INFO LOADING
// ============================================
async function loadSessionInfo(token) {
    if (window.loading) {
        window.loading.show('Зареждане на информация...');
    }

    try {
        const response = await fetch(`/api/auth/session/token/${token}`);

        if (response.ok) {
            const data = await response.json();

            // Fill form fields
            const usernameInput = document.getElementById('username');
            const domainInput = document.getElementById('domain');

            if (usernameInput) usernameInput.value = data.username || '';

            // Домейнът идва от appsettings (defaultDomain hidden field), НЕ от session.domain
            // session.domain може да е machine name ("AHMEDITDESK") ако е създадена от Credential Provider
            const defaultDomain = document.getElementById('defaultDomain')?.value || '';
            if (domainInput) {
                // Приоритет: 1) appsettings домейн, 2) текущата стойност, 3) session домейн
                domainInput.value = defaultDomain || domainInput.value || data.domain || '';
            }

            // Update machine info display
            updateMachineInfo(data);

            // Focus password field
            const passwordInput = document.getElementById('password');
            if (passwordInput) {
                setTimeout(() => passwordInput.focus(), 100);
            }

            if (window.toast) {
                window.toast.success('Информацията е заредена успешно');
            }
        } else {
            if (window.toast) {
                window.toast.error('Сесията не е намерена или е изтекла');
            } else {
                alert('Сесията не е намерена или е изтекла');
            }
        }
    } catch (error) {
        console.error('Грешка при зареждане на информация:', error);
        if (window.toast) {
            window.toast.error('Грешка при зареждане на информация');
        }
    } finally {
        if (window.loading) {
            window.loading.hide();
        }
    }
}

// Update machine info display
function updateMachineInfo(data) {
    let machineInfo = document.querySelector('.machine-info');

    if (!machineInfo) {
        // Create machine info element if it doesn't exist
        const form = document.getElementById('authForm');
        if (form) {
            machineInfo = document.createElement('div');
            machineInfo.className = 'machine-info fade-in';
            form.parentNode.insertBefore(machineInfo, form);
        }
    }

    if (machineInfo) {
        // Показваме домейна от appsettings, не machine name от сесията
        const defaultDomain = document.getElementById('defaultDomain')?.value || data.domain || '';
        machineInfo.innerHTML = `
            <p class="mb-1"><strong>Компютър:</strong> ${data.machineName || 'N/A'}</p>
            <p class="mb-0"><strong>Потребител:</strong> ${data.username || ''}@${defaultDomain}</p>
        `;
        machineInfo.style.display = 'block';
    }
}

// ============================================
// FORM ENHANCEMENTS
// ============================================
function initFormEnhancements() {
    const usernameInput = document.getElementById('username');
    const passwordInput = document.getElementById('password');
    const authForm = document.getElementById('authForm');

    // Auto-focus password on Enter in username
    if (usernameInput && passwordInput) {
        usernameInput.addEventListener('keypress', function (e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                passwordInput.focus();
            }
        });
    }

    // Form submission with loading state
    if (authForm) {
        authForm.addEventListener('submit', function (e) {
            const submitBtn = authForm.querySelector('button[type="submit"]');
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Влизане...';

                // Re-enable after 5 seconds as fallback
                setTimeout(() => {
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = '🔓 Вход';
                }, 5000);
            }
        });
    }
}

// ============================================
// INITIALIZATION
// ============================================
document.addEventListener('DOMContentLoaded', function () {
    console.log('🔐 Auth page initialized');

    initQRScanner();
    initManualTokenInput();
    initFormEnhancements();

    // Auto-load session if token is present in URL
    const urlParams = new URLSearchParams(window.location.search);
    const urlToken = urlParams.get('token');
    if (urlToken) {
        document.getElementById('tokenInput').value = urlToken;
        document.getElementById('manualToken').value = urlToken;
        loadSessionInfo(urlToken);
    }
});
