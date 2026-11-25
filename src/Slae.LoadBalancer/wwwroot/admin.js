const byId = (id) => document.getElementById(id);
const token = localStorage.getItem('slae_token');
const role = token && JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))).role;
if (role !== 'Admin') location.replace('index.html');

function fill(id, rows) {
    byId(id).replaceChildren(...rows.map(r => {
        const tr = document.createElement('tr');
        r.forEach(v => {
            const td = document.createElement('td');
            td.textContent = v;
            tr.appendChild(td);
        });
        return tr;
    }));
}

async function refreshAdmin() {
    try {
        const res = await fetch('/api/admin/overview', { headers: { Authorization: 'Bearer ' + token } });
        if (res.status === 401 || res.status === 403) return location.replace('index.html');
        const o = await res.json();
        const lb = await (await fetch('/lb/status')).json();

        fill('apis', lb.map(s => [s.url, s.healthy ? 'працює' : 'недоступний', s.totalRequests]));

        const running = o.running.map(t => [t.id.substring(0, 8), t.username, t.dimension,
            'Рахується', t.progress + '%', t.serverId]);
        const queued = o.queue.map((t, i) => [t.id.substring(0, 8), t.username, t.dimension,
        'В черзі: ' + (i + 1), '—', '—']);
        fill('tasks', running.concat(queued));
        byId('msg').textContent = '';
    } catch (e) {
        byId('msg').textContent = 'Не вдалося оновити дані';
    }
}

refreshAdmin();
setInterval(refreshAdmin, 2000);
