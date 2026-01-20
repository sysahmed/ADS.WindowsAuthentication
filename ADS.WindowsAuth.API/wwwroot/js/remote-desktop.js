/**
 * ADS Windows Authentication - Remote Desktop JavaScript
 * Session management and real-time updates
 */

// ============================================
// CONFIGURATION
// ============================================
let serviceUrl = '';
let refreshInterval = null;
const REFRESH_RATE = 5000; // 5 seconds

// ============================================
// SESSION LOADING
// ============================================
async function loadActiveSessions() {
    try {
        const response = await fetch(`${serviceUrl}/api/remotedesktop/sessions`);

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
        }

        const sessions = await response.json();
        displaySessions(sessions);
    } catch (error) {
        console.error('Грешка при зареждане на сесии:', error);
        displayError('Грешка при зареждане на сесии. Моля, опитайте отново.');
    }
}

// ============================================
// SESSION DISPLAY
// ============================================
function displaySessions(sessions) {
    const container = document.getElementById('sessionsContainer');
    if (!container) return;

    if (sessions.length === 0) {
        container.innerHTML = `
            <div class="text-center py-5">
                <div style="font-size: 4rem; opacity: 0.3;">🖥️</div>
                <p class="text-muted mt-3">Няма активни сесии</p>
                <small class="text-muted">Сесиите ще се появят тук, когато някой се свърже</small>
            </div>
        `;
        return;
    }

    let html = '<div class="table-responsive"><table class="table table-hover">';
    html += `
        <thead>
            <tr>
                <th>Session ID</th>
                <th>Машина</th>
                <th>Статус</th>
                <th>Създадена</th>
                <th>Действия</th>
            </tr>
        </thead>
        <tbody>
    `;

    sessions.forEach(session => {
        const createdAt = formatSessionDate(session.createdAt);
        const status = getStatusBadge(session.isActive);
        const sessionId = escapeHtml(session.sessionId);
        const machineName = escapeHtml(session.machineName);

        html += `
            <tr class="fade-in">
                <td>
                    <code class="session-id" data-session="${sessionId}" title="Кликнете за копиране">
                        ${sessionId.substring(0, 8)}...
                    </code>
                </td>
                <td><strong>${machineName}</strong></td>
                <td>${status}</td>
                <td>${createdAt}</td>
                <td>
                    <a href="/RemoteDesktop/Viewer?sessionId=${sessionId}" 
                       class="btn btn-sm btn-primary">
                        🖥️ Отвори
                    </a>
                </td>
            </tr>
        `;
    });

    html += '</tbody></table></div>';
    container.innerHTML = html;

    // Add click-to-copy for session IDs
    initSessionIdCopy();
}

// ============================================
// UTILITY FUNCTIONS
// ============================================
function getStatusBadge(isActive) {
    if (isActive) {
        return '<span class="badge badge-success badge-pulse">● Активна</span>';
    } else {
        return '<span class="badge badge-secondary">○ Неактивна</span>';
    }
}

function formatSessionDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleString('bg-BG', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
    });
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function displayError(message) {
    const container = document.getElementById('sessionsContainer');
    if (container) {
        container.innerHTML = `
            <div class="alert alert-danger" role="alert">
                <strong>⚠️ Грешка:</strong> ${escapeHtml(message)}
            </div>
        `;
    }
}

// ============================================
// SESSION ID COPY
// ============================================
function initSessionIdCopy() {
    const sessionIds = document.querySelectorAll('.session-id');
    sessionIds.forEach(el => {
        el.style.cursor = 'pointer';
        el.addEventListener('click', async function () {
            const sessionId = this.getAttribute('data-session');
            if (window.ADS && window.ADS.copyToClipboard) {
                await window.ADS.copyToClipboard(sessionId);
            } else {
                // Fallback
                try {
                    await navigator.clipboard.writeText(sessionId);
                    if (window.toast) {
                        window.toast.success('Session ID копиран!');
                    }
                } catch (err) {
                    console.error('Грешка при копиране:', err);
                }
            }
        });
    });
}

// ============================================
// AUTO-REFRESH
// ============================================
function startAutoRefresh() {
    // Clear existing interval
    if (refreshInterval) {
        clearInterval(refreshInterval);
    }

    // Load immediately
    loadActiveSessions();

    // Then refresh periodically
    refreshInterval = setInterval(loadActiveSessions, REFRESH_RATE);
}

function stopAutoRefresh() {
    if (refreshInterval) {
        clearInterval(refreshInterval);
        refreshInterval = null;
    }
}

// ============================================
// PAGE VISIBILITY
// ============================================
document.addEventListener('visibilitychange', function () {
    if (document.hidden) {
        stopAutoRefresh();
    } else {
        startAutoRefresh();
    }
});

// ============================================
// INITIALIZATION
// ============================================
function initRemoteDesktop(url) {
    serviceUrl = url || '';
    console.log('🖥️ Remote Desktop initialized with URL:', serviceUrl);

    startAutoRefresh();
}

// Cleanup on page unload
window.addEventListener('beforeunload', function () {
    stopAutoRefresh();
});

// Export for use in views
window.initRemoteDesktop = initRemoteDesktop;
