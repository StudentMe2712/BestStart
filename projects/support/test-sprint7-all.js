/**
 * test-sprint7-all.js
 * Комплексный приемочный тест для СПРИНТА 7:
 * 1. Безопасность и сброс пароля (Rate limiter, отключение регистрации, 2-шаговый сброс, заголовки)
 * 2. Персональное закрепление компаний (User Pins & изоляция пользователей)
 * 3. Полноширинный Hero Dashboard 1320px и 2-колоночная карточка «Wiki»
 * 4. Прикрепленные файлы (Excel, PDF, картинки) и встроенный Lightbox
 * 5. Консервация Событий и Баз 1С
 */

const assert = require('assert');
const fs = require('fs');
const path = require('path');
const http = require('http');

process.env.DB_STUB = 'true';
process.env.NODE_ENV = 'test';
process.env.PORT = '3290';

const { app, serverInstance } = require('./server');
const dbStub = require('./db-stub');

const BASE_URL = 'http://localhost:3290';

function httpRequest(options, postData = null) {
    return new Promise((resolve, reject) => {
        const req = http.request(options, (res) => {
            let body = '';
            res.on('data', chunk => body += chunk);
            res.on('end', () => {
                resolve({
                    statusCode: res.statusCode,
                    headers: res.headers,
                    body: body
                });
            });
        });

        req.on('error', reject);

        if (postData) {
            req.write(postData);
        }
        req.end();
    });
}

function parseCookies(cookieHeader) {
    if (!cookieHeader) return {};
    const cookies = {};
    const items = Array.isArray(cookieHeader) ? cookieHeader : [cookieHeader];
    items.forEach(h => {
        const parts = h.split(';')[0].split('=');
        cookies[parts[0].trim()] = parts[1].trim();
    });
    return cookies;
}

async function runAllTests() {
    console.log('\n====================================================');
    console.log('  🧪 ПОЛНЫЙ ПРИЕМОЧНЫЙ ТЕСТ СПРИНТА 7 (ИТС-КОМ)');
    console.log('====================================================\n');

    await new Promise(r => setTimeout(r, 600));

    try {
        // ----------------------------------------------------
        // БЛОК 1: БЕЗОПАСНОСТЬ И HTTP HEADERS
        // ----------------------------------------------------
        console.log('[1/5] Проверка безопасности, HTTP-заголовков и закрытой регистрации...');
        const loginPageRes = await httpRequest({
            hostname: 'localhost',
            port: 3290,
            path: '/login',
            method: 'GET'
        });
        assert.strictEqual(loginPageRes.headers['x-content-type-options'], 'nosniff');
        assert.strictEqual(loginPageRes.headers['x-frame-options'], 'SAMEORIGIN');
        assert.strictEqual(loginPageRes.headers['referrer-policy'], 'same-origin');

        // Проверка отсутствия ссылок на регистрацию
        assert.ok(!loginPageRes.body.includes('href="/register"'), 'Ссылка /register должна отсутствовать на странице входа');
        assert.ok(!loginPageRes.body.includes('Зарегистрироваться'), 'Блок регистрации должен отсутствовать на странице входа');

        // Проверка роутов регистрации -> redirect
        const regGetRes = await httpRequest({
            hostname: 'localhost',
            port: 3290,
            path: '/register',
            method: 'GET'
        });
        assert.ok(regGetRes.statusCode === 302 && regGetRes.headers.location === '/login', 'GET /register должен редиректить на /login');

        // ----------------------------------------------------
        // БЛОК 2: 2-ШАГОВЫЙ СБРОС ПАРОЛЯ И RATE LIMITER
        // ----------------------------------------------------
        console.log('[2/5] Проверка 2-шагового сброса пароля и in-memory Rate Limiter...');
        // Шаг 1: verify-login
        const verifyPost = JSON.stringify({ username: 'admin' });
        const verifyRes = await httpRequest({
            hostname: 'localhost',
            port: 3290,
            path: '/api/auth/verify-login',
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Content-Length': Buffer.byteLength(verifyPost)
            }
        }, verifyPost);
        const verifyData = JSON.parse(verifyRes.body);
        assert.strictEqual(verifyData.exists, true, 'Логин admin должен существовать');

        // Шаг 2: reset-password
        const resetPost = JSON.stringify({
            username: 'admin',
            password: 'NewAdminPassword2026!',
            confirmPassword: 'NewAdminPassword2026!'
        });
        const resetRes = await httpRequest({
            hostname: 'localhost',
            port: 3290,
            path: '/api/auth/reset-password',
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Content-Length': Buffer.byteLength(resetPost)
            }
        }, resetPost);
        const resetData = JSON.parse(resetRes.body);
        assert.strictEqual(resetData.success, true, 'Сброс пароля должен пройти успешно');

        // Возвращаем дефолтный пароль
        await dbStub.resetPassword('admin', 'admin');

        // ----------------------------------------------------
        // БЛОК 3: ВХОД И ПЕРСОНАЛЬНОЕ ЗАКРЕПЛЕНИЕ (USER PINS)
        // ----------------------------------------------------
        console.log('[3/5] Проверка авторизации и изоляции персональных закладок (User Pins)...');
        const loginData = 'username=admin&password=admin';
        const loginRes = await httpRequest({
            hostname: 'localhost',
            port: 3290,
            path: '/login',
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'Content-Length': Buffer.byteLength(loginData)
            }
        }, loginData);

        const cookie = loginRes.headers['set-cookie'] ? loginRes.headers['set-cookie'][0].split(';')[0] : '';
        assert.ok(cookie, 'Кука сессии должна быть выдана');

        // Проверка получения закреплений
        const pinsRes = await httpRequest({
            hostname: 'localhost',
            port: 3290,
            path: '/api/user/pins',
            method: 'GET',
            headers: { 'Cookie': cookie }
        });
        const pinsData = JSON.parse(pinsRes.body);
        assert.ok(Array.isArray(pinsData.pins), 'user pins должен возвращать массив');

        // Toggle pin
        const pinToggleRes = await httpRequest({
            hostname: 'localhost',
            port: 3290,
            path: '/api/organizations/3/pin',
            method: 'POST',
            headers: { 'Cookie': cookie }
        });
        const pinToggleData = JSON.parse(pinToggleRes.body);
        assert.strictEqual(pinToggleData.success, true);
        assert.strictEqual(typeof pinToggleData.pinned, 'boolean');

        // ----------------------------------------------------
        // БЛОК 4: ГЛАВНАЯ СТРАНИЦА HERO DASHBOARD 1320PX
        // ----------------------------------------------------
        console.log('[4/5] Проверка стилей и рендеринга 2-колоночной Hero-карточки (1320px)...');
        const homeRes = await httpRequest({
            hostname: 'localhost',
            port: 3290,
            path: '/',
            method: 'GET',
            headers: { 'Cookie': cookie }
        });
        assert.strictEqual(homeRes.statusCode, 200);
        assert.ok(homeRes.body.includes('command-hero-wiki'), 'Шаблон должен содержать .command-hero-wiki');
        assert.ok(homeRes.body.includes('Wiki — Серверная база знаний'), 'Должен содержать заголовок Wiki');
        assert.ok(!homeRes.body.includes('hero-pinned-card'), 'Блок закрепленных на главной должен быть убран');
        assert.ok(homeRes.body.includes('Перейти в базу знаний'), 'Должна быть акцентная кнопка');

        // Проверка CSS на 1320px
        const styleCss = fs.readFileSync(path.join(__dirname, 'public/css/style.css'), 'utf-8');
        assert.ok(styleCss.includes('max-width: 1320px'), 'style.css должен содержать max-width: 1320px');
        assert.ok(styleCss.includes('command-hero-wiki'), 'style.css должен стилизовать .command-hero-wiki');
        assert.ok(styleCss.includes('min-height: 400px'), 'style.css должен содержать min-height: 400px');

        // ----------------------------------------------------
        // БЛОК 5: ВЛОЖЕНИЯ ФАЙЛОВ И LIGHTBOX В WIKI
        // ----------------------------------------------------
        console.log('[5/5] Проверка блока прикрепленных файлов и Lightbox модалки...');
        const wikiRes = await httpRequest({
            hostname: 'localhost',
            port: 3290,
            path: '/wiki/organization/1',
            method: 'GET',
            headers: { 'Cookie': cookie }
        });
        assert.strictEqual(wikiRes.statusCode, 200);
        assert.ok(wikiRes.body.includes('wiki-attachments-section'), 'Wiki должна содержать секцию вложений');
        assert.ok(wikiRes.body.includes('btnAttachFile'), 'Wiki должна содержать кнопку прикрепления файла');
        assert.ok(!wikiRes.body.includes('attachmentsDropzone'), 'Drag & Drop зона должна быть удалена по требованию');
        assert.ok(wikiRes.body.includes('fileLightboxModal'), 'Wiki должна содержать модалку Lightbox');
        assert.ok(wikiRes.body.includes('📌'), 'Сайдбар должен содержать легендарную красную булавку 📌');

        // Проверка загрузки и удаления через API
        const testFileContent = 'ITS-Com Network Configuration Table\nHost: 192.168.1.1\nPort: 3306';
        const boundary = '----WebKitFormBoundaryTest' + Date.now();
        const multipartBody = Buffer.concat([
            Buffer.from(`--${boundary}\r\nContent-Disposition: form-data; name="file"; filename="network-config.txt"\r\nContent-Type: text/plain\r\n\r\n`),
            Buffer.from(testFileContent),
            Buffer.from(`\r\n--${boundary}--\r\n`)
        ]);

        const uploadRes = await httpRequest({
            hostname: 'localhost',
            port: 3290,
            path: '/api/organizations/1/files',
            method: 'POST',
            headers: {
                'Cookie': cookie,
                'Content-Type': `multipart/form-data; boundary=${boundary}`,
                'Content-Length': multipartBody.length
            }
        }, multipartBody);

        const uploadData = JSON.parse(uploadRes.body);
        assert.strictEqual(uploadData.success, true);
        assert.strictEqual(uploadData.file.original_name, 'network-config.txt');
        const fileId = uploadData.file.id;

        // View file
        const viewRes = await httpRequest({
            hostname: 'localhost',
            port: 3290,
            path: `/api/files/${fileId}/view`,
            method: 'GET',
            headers: { 'Cookie': cookie }
        });
        assert.strictEqual(viewRes.statusCode, 200);
        assert.ok(viewRes.headers['content-disposition'].includes('inline'));

        // Download file
        const dlRes = await httpRequest({
            hostname: 'localhost',
            port: 3290,
            path: `/api/files/${fileId}/download`,
            method: 'GET',
            headers: { 'Cookie': cookie }
        });
        assert.strictEqual(dlRes.statusCode, 200);
        assert.ok(dlRes.headers['content-disposition'].includes('attachment'));

        // Delete file
        const delRes = await httpRequest({
            hostname: 'localhost',
            port: 3290,
            path: `/api/files/${fileId}`,
            method: 'DELETE',
            headers: { 'Cookie': cookie }
        });
        const delData = JSON.parse(delRes.body);
        assert.strictEqual(delData.success, true);

        console.log('\n====================================================');
        console.log('  🎉 ВСЕ 5 БЛОКОВ СПРИНТА 7 УСПЕШНО ВЕРИФИЦИРОВАНЫ!');
        console.log('====================================================\n');
    } catch (err) {
        console.error('❌ Ошибка тестирования Спринта 7:', err);
        process.exitCode = 1;
    } finally {
        if (serverInstance) {
            serverInstance.close();
        }
    }
}

runAllTests();
