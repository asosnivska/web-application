const TOKEN_KEY = 'slae_token';
const USER_KEY = 'slae_user';
const $ = (id) => document.getElementById(id);
let timer = null;

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

async function auth(kind) {
    showError();
    try {
        const data = await api('POST', '/api/auth/' + kind, {
            username: $('username').value,
            password: $('password').value
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

async function refresh() {
    try {
        renderTasks(await api('GET', '/api/tasks'));
    } catch (e) { showError(e); }
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

function renderTasks(tasks) {
    const body = $('tasks');
    body.replaceChildren();
    for (const t of tasks) {
        const tr = document.createElement('tr');
        cell(tr, t.id.substring(0, 8));
        cell(tr, t.status);
        const p = document.createElement('progress');
        p.max = 100;
        p.value = t.progress;
        cell(tr, p);
        cell(tr, t.dimension);
        cell(tr, new Date(t.createdAt).toLocaleString());
        const actions = document.createElement('span');
        if (t.status === 'Pending' || t.status === 'InProgress')
            actions.appendChild(button('Скасувати', () => cancelTask(t.id)));
        if (t.status === 'Completed')
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

        if (nums.length !== n + 1 || nums.some(Number.isNaN))
            throw new Error('Кожен рядок має містити ' + (n + 1) + ' чисел: N коефіцієнтів і b');

        matrix.push(nums.slice(0, n));
        vector.push(nums[n]);
    }

    return { dimension: n, matrix: matrix, vector: vector };
}

async function create() {
    showError();

    try {
        const body = {
            epsilon: Number($('epsilon').value),
            dominance: Number($('dominance').value)
        };

        const manual = $('manual').value.trim();

        if (manual)
            Object.assign(body, parseManual(manual));
        else
            body.dimension = Number($('dimension').value);

        await api('POST', '/api/tasks', body);
        refresh();
    } catch (e) {
        showError(e);
    }
}

async function cancelTask(id) {
    try {
        await api('POST', '/api/tasks/' + id + '/cancel');
        refresh();
    } catch (e) { showError(e); }
}

async function showResult(id) {
    try {
        const t = await api('GET', '/api/tasks/' + id);
        const text = JSON.stringify(t.result ?? t.error, null, 2);
        $('result').textContent = text.slice(0, 2000);
    } catch (e) { showError(e); }
}

$('login').onclick = () => auth('login');
$('register').onclick = () => auth('register');
$('logout').onclick = logout;
$('create').onclick = create;
if (localStorage.getItem(TOKEN_KEY)) start(); else view(false);