/**
 * ADS Windows Authentication - Remote Desktop JavaScript
 */

let serviceUrl = '';
let refreshInterval = null;
const REFRESH_RATE = 5000;

// ── Зареждане на сесии ───────────────────────────────────────────────────────

async function loadActiveSessions() {
    try {
        const response = await fetch(serviceUrl + '/api/remotedesktop/sessions');
        if (!response.ok) throw new Error('HTTP ' + response.status);
        const sessions = await response.json();
        displaySessions(sessions);
    } catch (error) {
        displayError('Грешка при зареждане на сесии.');
    }
}

// ── Показване на сесии ───────────────────────────────────────────────────────

function displaySessions(sessions) {
    const container = document.getElementById('sessionsContainer');
    if (!container) return;

    if (!sessions.length) {
        container.innerHTML =
            '<div class="text-center py-5">' +
            '<div style="font-size:3rem;opacity:.3">&#128187;</div>' +
            '<p class="text-muted mt-2 mb-1">Няма активни сесии</p>' +
            '<small class="text-muted">Сесиите се появяват тук когато Monitor стартира RemoteDesktopHost</small>' +
            '</div>';
        return;
    }

    let rows = '';
    sessions.forEach(function(s) {
        const sid     = escapeHtml(s.sessionId);
        const machine = escapeHtml(s.machineName);

        const hostBadge = s.hostConnected
            ? '<span class="badge bg-success">&#9679; Host онлайн</span>'
            : '<span class="badge bg-danger">&#9675; Host офлайн</span>';
        const viewerBadge = s.viewerConnected
            ? '<span class="badge bg-primary">&#9679; Viewer свързан</span>'
            : '<span class="badge bg-secondary">&#9675; Без viewer</span>';
        const lastAct = s.lastActivity ? formatDate(s.lastActivity) : '-';

        rows +=
            '<tr>' +
            '<td><strong>' + machine + '</strong></td>' +
            '<td><code class="session-id" data-session="' + sid + '" title="Клик за копиране" style="cursor:pointer">' + sid + '</code></td>' +
            '<td>' + hostBadge + '</td>' +
            '<td>' + viewerBadge + '</td>' +
            '<td><small class="text-muted">' + lastAct + '</small></td>' +
            '<td class="text-end">' +
            '<a href="/RemoteDesktop/Viewer?sessionId=' + sid + '" class="btn btn-sm btn-primary me-1">' +
            '<i class="bi bi-display"></i> Отвори' +
            '</a>' +
            '<button class="btn btn-sm btn-outline-danger" onclick="deleteSession(\'' + sid + '\')">' +
            '<i class="bi bi-trash"></i>' +
            '</button>' +
            '</td>' +
            '</tr>';
    });

    container.innerHTML =
        '<div class="table-responsive">' +
        '<table class="table table-hover table-sm mb-0">' +
        '<thead class="table-light"><tr>' +
        '<th>Машина</th><th>Session ID</th><th>Host</th><th>Viewer</th><th>Активност</th><th class="text-end">Действия</th>' +
        '</tr></thead>' +
        '<tbody>' + rows + '</tbody>' +
        '</table></div>';

    initSessionIdCopy();
}

// ── Изтриване на сесия ───────────────────────────────────────────────────────

async function deleteSession(sessionId) {
    if (!confirm('Изтрий сесия ' + sessionId + '?')) return;
    try {
        const resp = await fetch('/api/remotedesktop/sessions/' + sessionId, { method: 'DELETE' });
        if (!resp.ok) throw new Error('HTTP ' + resp.status);
        await loadActiveSessions();
    } catch (err) {
        alert('Грешка: ' + err.message);
    }
}

// ── Копиране на Session ID ────────────────────────────────────────────────────

function initSessionIdCopy() {
    document.querySelectorAll('.session-id').forEach(function(el) {
        el.addEventListener('click', async function() {
            try {
                await navigator.clipboard.writeText(this.getAttribute('data-session'));
                const orig = this.textContent;
                this.textContent = 'Копиран!';
                setTimeout(function() { el.textContent = orig; }, 1500);
            } catch (e) {}
        });
    });
}

// ── Помощни функции ──────────────────────────────────────────────────────────

function formatDate(d) {
    if (!d) return '-';
    return new Date(d).toLocaleString('bg-BG', {
        day: '2-digit', month: '2-digit', year: 'numeric',
        hour: '2-digit', minute: '2-digit'
    });
}

function escapeHtml(t) {
    const d = document.createElement('div');
    d.textContent = t || '';
    return d.innerHTML;
}

function displayError(msg) {
    const c = document.getElementById('sessionsContainer');
    if (c) c.innerHTML = '<div class="alert alert-danger m-3">&#9888; ' + escapeHtml(msg) + '</div>';
}

// ── Auto-refresh ─────────────────────────────────────────────────────────────

function startAutoRefresh() {
    if (refreshInterval) clearInterval(refreshInterval);
    loadActiveSessions();
    refreshInterval = setInterval(loadActiveSessions, REFRESH_RATE);
}

function stopAutoRefresh() {
    if (refreshInterval) {
        clearInterval(refreshInterval);
        refreshInterval = null;
    }
}

document.addEventListener('visibilitychange', function() {
    if (document.hidden) stopAutoRefresh(); else startAutoRefresh();
});
window.addEventListener('beforeunload', stopAutoRefresh);

// ── Init ─────────────────────────────────────────────────────────────────────

function initRemoteDesktop(url) {
    serviceUrl = url || '';
    startAutoRefresh();
}

window.initRemoteDesktop   = initRemoteDesktop;
window.loadActiveSessions  = loadActiveSessions;
window.deleteSession       = deleteSession;
