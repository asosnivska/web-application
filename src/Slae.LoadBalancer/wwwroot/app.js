const TOKEN_KEY = 'slae_token';
const USER_KEY = 'slae_user';
const $ = (id) => document.getElementById(id);
let timer = null;

const LIMITS = {
    maxDimension: 3000,
    maxManualDimension: 500,
    minEpsilon: 1e-12,
    maxEpsilon: 0.1,
    minDominance: 1.0001,
    maxDominance: 10,
    maxIterations: 200000,
    maxOperations: 4.5e11
};

function checkInput(n, epsilon, dominance, isManual) {
    const maxN = isManual ? LIMITS.maxManualDimension : LIMITS.maxDimension;
    if (!Number.isInteger(n) || n < 1 || n > maxN)
        throw new Error('Розмір N має бути цілим числом від 1 до ' + maxN);
    if (!Number.isFinite(epsilon) || epsilon < LIMITS.minEpsilon || epsilon > LIMITS.maxEpsilon)
        throw new Error('Точність має бути від ' + LIMITS.minEpsilon + ' до ' + LIMITS.maxEpsilon);
    if (!isManual && (!Number.isFinite(dominance) || dominance < LIMITS.minDominance || dominance > LIMITS.maxDominance))
        throw new Error('Домінування має бути від ' + LIMITS.minDominance + ' до ' + LIMITS.maxDominance);
}

function showError(e) {
    $('msg').textContent = e ? e.message : '';
}

function errorText(status, data) {
    if (data && data.errors) return Object.values(data.errors).flat().join('; ');
    if (data && data.title) return data.title;
    return 'Помилка HTTP ' + status;
}

async function api(method, path, body) {
    const headers = {};
    if (body) headers['Content-Type'] = 'application/json';
    const token = localStorage.getItem(TOKEN_KEY);
    if (token) headers['Authorization'] = 'Bearer ' + token;
    const res = await fetch(path, {
        method: method,
        headers: headers,
        body: body ? JSON.stringify(body) : undefined
    });
    $('server').textContent = 'Відповів сервер: ' + (res.headers.get('X-Served-By') || '?');
    if (res.status === 401 && token) logout();
    let data = null;
    try { data = await res.json(); } catch { }
    if (!res.ok) throw new Error(errorText(res.status, data));
    return data;
}

function checkCredentials(username, password) {
    if (!/^[a-zA-Z0-9_]{3,50}$/.test(username))
        throw new Error('Логін: від 3 до 50 символів, лише латинські літери, цифри та _');
    if (password.length < 8)
        throw new Error('Пароль має містити щонайменше 8 символів');
    if (password.length > 128)
        throw new Error('Пароль занадто довгий (максимум 128 символів)');
}

async function auth(kind) {
    showError();
    try {
        const username = $('username').value;
        const password = $('password').value;
        if (kind === 'register') checkCredentials(username, password);
        const data = await api('POST', '/api/auth/' + kind, {
            username: username,
            password: password
        });
        localStorage.setItem(TOKEN_KEY, data.token);
        localStorage.setItem(USER_KEY, data.username);
        $('password').value = '';
        start();
    } catch (e) { showError(e); }
}

function logout() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    clearInterval(timer);
    view(false);
}

function view(loggedIn) {
    $('auth').hidden = loggedIn;
    $('app').hidden = !loggedIn;
}

function start() {
    view(true);
    $('who').textContent = localStorage.getItem(USER_KEY);
    refresh();
    clearInterval(timer);
    timer = setInterval(refresh, 2000);
}

let refreshFailed = false;

async function refresh() {
    try {
        renderTasks(await api('GET', '/api/tasks?take=50'));
        if (refreshFailed) {
            showError();
            refreshFailed = false;
        }
    } catch (e) {
        refreshFailed = true;
        showError(e);
    }
}

function cell(tr, content) {
    const td = document.createElement('td');
    if (content instanceof Node) td.appendChild(content);
    else td.textContent = content;
    tr.appendChild(td);
}

function button(text, onclick) {
    const b = document.createElement('button');
    b.textContent = text;
    b.onclick = onclick;
    return b;
}

function statusText(t) {
    if (t.status === 'Pending' && t.queuePosition) {
        const wait = t.estimatedWaitSeconds > 0
            ? ', очікування ≈ ' + t.estimatedWaitSeconds + ' с'
            : ', скоро почнеться';
        return 'В черзі: ' + t.queuePosition + wait;
    }
    return t.status;
}

const cancelling = new Set();

function renderTasks(tasks) {
    const body = $('tasks');
    body.replaceChildren();
    for (const t of tasks) {
        const tr = document.createElement('tr');
        cell(tr, t.id.substring(0, 8));
        cell(tr, statusText(t));        const p = document.createElement('progress');
        p.max = 100;
        p.value = t.progress;
        cell(tr, p);
        cell(tr, t.dimension);
        cell(tr, new Date(t.createdAt).toLocaleString());
        const actions = document.createElement('span');
        if (t.status === 'Pending' || t.status === 'InProgress') {
            const isCancelling = cancelling.has(t.id);
            const b = button(isCancelling ? 'Скасовується…' : 'Скасувати', () => cancelTask(t.id));
            b.disabled = isCancelling;
            actions.appendChild(b);
        } else {
            cancelling.delete(t.id);
        }
        if (t.status === 'Completed' || t.status === 'Failed')
            actions.appendChild(button('Результат', () => showResult(t.id)));
        cell(tr, actions);
        body.appendChild(tr);
    }
}

function parseManual(text) {
    const rows = text.trim().split(/\r?\n/).filter(r => r.trim() !== '');
    const n = rows.length;
    const matrix = [];
    const vector = [];   

    for (const row of rows) {
        const nums = row.trim().split(/[\s,;]+/).map(Number);

        if (nums.length !== n + 1 || nums.some(x => !Number.isFinite(x)))
            throw new Error('У кожному рядку має бути чисел: ' + (n + 1) + ' (N коефіцієнтів і вільний член b)');

        matrix.push(nums.slice(0, n));
        vector.push(nums[n]);
    }

    return { dimension: n, matrix: matrix, vector: vector };
}

async function create() {
    showError();

    try {
        const epsilon = Number($('epsilon').value);
        const dominance = Number($('dominance').value);
        const manual = $('manual').value.trim();
        const body = { epsilon: epsilon };

        if (manual) {
            Object.assign(body, parseManual(manual));
        } else {
            body.dimension = Number($('dimension').value);
            body.dominance = dominance;
        }

        checkInput(body.dimension, epsilon, dominance, !!manual);

        body.maxIterations = Math.min(
            LIMITS.maxIterations,
            Math.floor(LIMITS.maxOperations / (body.dimension * body.dimension)));

        await api('POST', '/api/tasks', body);
        refresh();
    } catch (e) {
        showError(e);
    }
}

async function cancelTask(id) {
    try {
        showError();
        cancelling.add(id);
        await api('POST', '/api/tasks/' + id + '/cancel');
        refresh();
    } catch (e) {
        cancelling.delete(id);
        showError(e);
    }
}

async function showResult(id) {
    try {
        const t = await api('GET', '/api/tasks/' + id);
        const box = $('result');

        if (t.error) {
            box.textContent = 'Помилка: ' + t.error;
            box.className = 'error';
        } else {
            box.textContent = JSON.stringify(t.result, null, 2).slice(0, 2000);
            box.className = '';
        }
    } catch (e) { showError(e); }
}

$('login').onclick = () => auth('login');
$('register').onclick = () => auth('register');
$('logout').onclick = logout;
$('create').onclick = create;
if (localStorage.getItem(TOKEN_KEY)) start(); else view(false);