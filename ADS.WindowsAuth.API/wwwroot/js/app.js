/**
 * ADS Windows Authentication - Main App JavaScript
 * Core utilities and UI interactions
 */

// ============================================
// TOAST NOTIFICATION SYSTEM
// ============================================
class ToastManager {
    constructor() {
        this.container = this.createContainer();
    }

    createContainer() {
        let container = document.getElementById('toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toast-container';
            container.style.cssText = `
                position: fixed;
                top: 20px;
                right: 20px;
                z-index: 9999;
                display: flex;
                flex-direction: column;
                gap: 10px;
            `;
            document.body.appendChild(container);
        }
        return container;
    }

    show(message, type = 'info', duration = 3000) {
        const toast = document.createElement('div');
        toast.className = `toast toast-${type}`;
        
        const colors = {
            success: '#4ade80',
            error: '#f87171',
            warning: '#fbbf24',
            info: '#60a5fa'
        };

        const icons = {
            success: '✓',
            error: '✕',
            warning: '⚠',
            info: 'ℹ'
        };

        toast.style.cssText = `
            background: white;
            border-left: 4px solid ${colors[type] || colors.info};
            border-radius: 12px;
            padding: 16px 20px;
            box-shadow: 0 10px 25px rgba(0,0,0,0.15);
            display: flex;
            align-items: center;
            gap: 12px;
            min-width: 300px;
            max-width: 400px;
            animation: slideInRight 0.3s ease-out;
            cursor: pointer;
        `;

        toast.innerHTML = `
            <span style="
                display: inline-flex;
                align-items: center;
                justify-content: center;
                width: 24px;
                height: 24px;
                border-radius: 50%;
                background: ${colors[type] || colors.info};
                color: white;
                font-weight: bold;
                font-size: 14px;
            ">${icons[type] || icons.info}</span>
            <span style="flex: 1; color: #334155; font-weight: 500;">${message}</span>
        `;

        this.container.appendChild(toast);

        // Auto remove
        setTimeout(() => {
            toast.style.animation = 'slideOutRight 0.3s ease-out';
            setTimeout(() => toast.remove(), 300);
        }, duration);

        // Click to dismiss
        toast.addEventListener('click', () => {
            toast.style.animation = 'slideOutRight 0.3s ease-out';
            setTimeout(() => toast.remove(), 300);
        });
    }

    success(message, duration) {
        this.show(message, 'success', duration);
    }

    error(message, duration) {
        this.show(message, 'error', duration);
    }

    warning(message, duration) {
        this.show(message, 'warning', duration);
    }

    info(message, duration) {
        this.show(message, 'info', duration);
    }
}

// Add toast animations to document
const style = document.createElement('style');
style.textContent = `
    @keyframes slideInRight {
        from {
            opacity: 0;
            transform: translateX(100px);
        }
        to {
            opacity: 1;
            transform: translateX(0);
        }
    }
    
    @keyframes slideOutRight {
        from {
            opacity: 1;
            transform: translateX(0);
        }
        to {
            opacity: 0;
            transform: translateX(100px);
        }
    }
`;
document.head.appendChild(style);

// Global toast instance
window.toast = new ToastManager();

// ============================================
// LOADING OVERLAY
// ============================================
class LoadingManager {
    constructor() {
        this.overlay = this.createOverlay();
    }

    createOverlay() {
        let overlay = document.getElementById('loading-overlay');
        if (!overlay) {
            overlay = document.createElement('div');
            overlay.id = 'loading-overlay';
            overlay.className = 'loading-overlay';
            overlay.innerHTML = `
                <div style="text-align: center;">
                    <div class="spinner-border" style="width: 3rem; height: 3rem; color: white;"></div>
                    <p style="color: white; margin-top: 1rem; font-weight: 600;" id="loading-message">Зареждане...</p>
                </div>
            `;
            document.body.appendChild(overlay);
        }
        return overlay;
    }

    show(message = 'Зареждане...') {
        const messageEl = this.overlay.querySelector('#loading-message');
        if (messageEl) messageEl.textContent = message;
        this.overlay.classList.add('active');
    }

    hide() {
        this.overlay.classList.remove('active');
    }
}

// Global loading instance
window.loading = new LoadingManager();

// ============================================
// SMOOTH SCROLL
// ============================================
document.addEventListener('DOMContentLoaded', () => {
    // Smooth scroll for anchor links
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            const href = this.getAttribute('href');
            if (href !== '#' && href !== '#!') {
                e.preventDefault();
                const target = document.querySelector(href);
                if (target) {
                    target.scrollIntoView({
                        behavior: 'smooth',
                        block: 'start'
                    });
                }
            }
        });
    });

    // Add fade-in animation to cards
    const cards = document.querySelectorAll('.card, .glass-card');
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.style.animation = 'fadeIn 0.6s ease-out';
            }
        });
    }, { threshold: 0.1 });

    cards.forEach(card => observer.observe(card));
});

// ============================================
// FORM ENHANCEMENTS
// ============================================
document.addEventListener('DOMContentLoaded', () => {
    // Add floating label effect
    const inputs = document.querySelectorAll('.form-control');
    inputs.forEach(input => {
        input.addEventListener('focus', () => {
            input.parentElement?.classList.add('focused');
        });
        
        input.addEventListener('blur', () => {
            if (!input.value) {
                input.parentElement?.classList.remove('focused');
            }
        });
    });

    // Form validation feedback
    const forms = document.querySelectorAll('form');
    forms.forEach(form => {
        form.addEventListener('submit', (e) => {
            const requiredInputs = form.querySelectorAll('[required]');
            let isValid = true;

            requiredInputs.forEach(input => {
                if (!input.value.trim()) {
                    isValid = false;
                    input.style.borderColor = '#f87171';
                    input.addEventListener('input', () => {
                        input.style.borderColor = '';
                    }, { once: true });
                }
            });

            if (!isValid) {
                e.preventDefault();
                toast.error('Моля, попълнете всички задължителни полета');
            }
        });
    });
});

// ============================================
// UTILITY FUNCTIONS
// ============================================

/**
 * Format date to Bulgarian locale
 */
function formatDate(date) {
    return new Date(date).toLocaleString('bg-BG', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
    });
}

/**
 * Copy text to clipboard
 */
async function copyToClipboard(text) {
    try {
        await navigator.clipboard.writeText(text);
        toast.success('Копирано в клипборда!');
        return true;
    } catch (err) {
        toast.error('Грешка при копиране');
        return false;
    }
}

/**
 * Debounce function
 */
function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

/**
 * Throttle function
 */
function throttle(func, limit) {
    let inThrottle;
    return function(...args) {
        if (!inThrottle) {
            func.apply(this, args);
            inThrottle = true;
            setTimeout(() => inThrottle = false, limit);
        }
    };
}

// ============================================
// EXPORT UTILITIES
// ============================================
window.ADS = {
    toast: window.toast,
    loading: window.loading,
    formatDate,
    copyToClipboard,
    debounce,
    throttle
};

console.log('🚀 ADS Windows Authentication - App initialized');
