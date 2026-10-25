const http = require('http');
const assert = require('assert');

process.env.DB_STUB = 'true';
const TEST_PORT = 3288;
process.env.PORT = String(TEST_PORT);

async function makeRequest({ method = 'GET', path = '/', headers = {}, body = null }) {
    return new Promise((resolve, reject) => {
        const postData = body ? (typeof body === 'string' ? body : JSON.stringify(body)) : null;
        const reqHeaders = { ...headers };
        if (postData) {
            if (!reqHeaders['Content-Type']) {
                reqHeaders['Content-Type'] = 'application/json';
            }
            reqHeaders['Content-Length'] = Buffer.byteLength(postData);
        }

        const req = http.request({
            hostname: '127.0.0.1',
            port: TEST_PORT,
            path,
            method,
            headers: reqHeaders
        }, (res) => {
            let resData = '';
            res.on('data', chunk => { resData += chunk; });
            res.on('end', () => {
                let json = null;
                try {
                    json = JSON.parse(resData);
                } catch (e) {
                    // not json
                }
                resolve({
                    status: res.statusCode,
                    headers: res.headers,
                    body: resData,
                    json
                });
            });
        });

        req.on('error', reject);
        if (postData) req.write(postData);
        req.end();
    });
}

async function runTests() {
    console.log('====================================================');
    console.log('  🧪 ТЕСТИРОВАНИЕ СПРИНТ 7: АРХИТЕКТУРА И БЭКЕНД');
    console.log('====================================================\n');

    const stub = require('./db-stub');
    const { serverInstance, clearRateLimit } = require('./server');

    await new Promise(resolve => setTimeout(resolve, 800));

    try {
        // ----------------------------------------------------
        // ТЕСТ 1: Модульные тесты checkUserExists и resetPassword в db-stub
        // ----------------------------------------------------
        console.log('[1/6] Тестирование checkUserExists() и resetPassword() в db-stub...');
        const existsAdmin = await stub.checkUserExists('admin');
        assert.strictEqual(existsAdmin.exists, true, 'admin должен существовать');

        const existsAdminUpper = await stub.checkUserExists('ADMIN');
        assert.strictEqual(existsAdminUpper.exists, true, 'ADMIN (регистронезависимо) должен существовать');

        const existsUnknown = await stub.checkUserExists('non_existing_user_xyz');
        assert.strictEqual(existsUnknown.exists, false, 'несуществующий пользователь не должен существовать');

        // Сброс с коротким паролем
        const resetShort = await stub.resetPassword('admin', '123');
        assert.strictEqual(resetShort.success, false, 'пароль < 8 символов должен отклоняться');

        // Корректный сброс
        const resetValid = await stub.resetPassword('admin', 'newPassword2026!');
        assert.strictEqual(resetValid.success, true, 'корректный пароль должен установиться');

        // Проверка входа с новым паролем
        const authNew = await stub.authenticateUser('admin', 'newPassword2026!');
        assert.strictEqual(authNew.success, true, 'вход с новым паролем должен пройти успешно');

        // Возврат пароля обратно на 'admin' для совместимости
        await stub.resetPassword('admin', 'admin12345');
        // Обновим также прямой пароль admin в stub для dev-режима
        const adminUser = (await stub.getAllUsers()).find(u => u.username === 'admin');
        const bcrypt = require('bcrypt');
        adminUser.password_hash = await bcrypt.hash('admin', 10);
        console.log('✓ Методы checkUserExists() и resetPassword() работают корректно.\n');

        // ----------------------------------------------------
        // ТЕСТ 2: Security Headers
        // ----------------------------------------------------
        console.log('[2/6] Проверка HTTP Security Headers...');
        const resLogin = await makeRequest({ method: 'GET', path: '/login' });
        assert.strictEqual(resLogin.headers['x-content-type-options'], 'nosniff', 'Заголовок X-Content-Type-Options должен быть nosniff');
        assert.strictEqual(resLogin.headers['x-frame-options'], 'SAMEORIGIN', 'Заголовок X-Frame-Options должен быть SAMEORIGIN');
        assert.strictEqual(resLogin.headers['referrer-policy'], 'same-origin', 'Заголовок Referrer-Policy должен быть same-origin');
        console.log('✓ Все обязательные security headers присутствуют в ответах.\n');

        // ----------------------------------------------------
        // ТЕСТ 3: Отключение регистрации (GET/POST /register)
        // ----------------------------------------------------
        console.log('[3/6] Проверка отключения регистрации...');
        const resGetReg = await makeRequest({ method: 'GET', path: '/register' });
        assert.strictEqual(resGetReg.status, 302, 'GET /register должен возвращать 302 редирект');
        assert.strictEqual(resGetReg.headers.location, '/login', 'GET /register должен редиректить на /login');

        const resPostReg = await makeRequest({ method: 'POST', path: '/register', body: { username: 'test', password: 'password123' } });
        assert.strictEqual(resPostReg.status, 302, 'POST /register должен возвращать 302 редирект');
        assert.strictEqual(resPostReg.headers.location, '/login', 'POST /register должен редиректить на /login');
        console.log('✓ Маршруты регистрации /register закрыты и перенаправляют на /login.\n');

        // ----------------------------------------------------
        // ТЕСТ 4: API verify-login и reset-password
        // ----------------------------------------------------
        console.log('[4/6] Проверка API /api/auth/verify-login и /api/auth/reset-password...');
        clearRateLimit('127.0.0.1');

        // verify-login существующий
        const resVerifyOk = await makeRequest({
            method: 'POST',
            path: '/api/auth/verify-login',
            body: { username: 'admin' }
        });
        assert.strictEqual(resVerifyOk.status, 200);
        assert.strictEqual(resVerifyOk.json.success, true);
        assert.strictEqual(resVerifyOk.json.exists, true);

        // verify-login несуществующий
        const resVerifyFail = await makeRequest({
            method: 'POST',
            path: '/api/auth/verify-login',
            body: { username: 'not_found_user_999' }
        });
        assert.strictEqual(resVerifyFail.status, 404);
        assert.strictEqual(resVerifyFail.json.success, false);
        assert.strictEqual(resVerifyFail.json.error, 'Пользователь не найден');

        // reset-password несовпадение паролей
        const resResetMismatch = await makeRequest({
            method: 'POST',
            path: '/api/auth/reset-password',
            body: { username: 'admin', password: 'Password123!', confirmPassword: 'MismatchPassword!' }
        });
        assert.strictEqual(resResetMismatch.status, 400);
        assert.strictEqual(resResetMismatch.json.success, false);
        assert.strictEqual(resResetMismatch.json.error, 'Пароли не совпадают');

        // reset-password слишком короткий
        const resResetShort = await makeRequest({
            method: 'POST',
            path: '/api/auth/reset-password',
            body: { username: 'admin', password: '123', confirmPassword: '123' }
        });
        assert.strictEqual(resResetShort.status, 400);
        assert.strictEqual(resResetShort.json.success, false);
        assert(resResetShort.json.error.includes('8'));

        // reset-password успешный
        clearRateLimit('127.0.0.1');
        const resResetOk = await makeRequest({
            method: 'POST',
            path: '/api/auth/reset-password',
            body: { username: 'admin', password: 'newSuperPassword123', confirmPassword: 'newSuperPassword123' }
        });
        assert.strictEqual(resResetOk.status, 200);
        assert.strictEqual(resResetOk.json.success, true);
        assert.strictEqual(resResetOk.json.message, 'Пароль успешно изменен');

        // Проверим логин с новым паролем
        const resLoginOk = await makeRequest({
            method: 'POST',
            path: '/login',
            headers: { 'Accept': 'application/json' },
            body: { username: 'admin', password: 'newSuperPassword123' }
        });
        assert.strictEqual(resLoginOk.status, 200);
        assert.strictEqual(resLoginOk.json.success, true);

        // Возвращаем пароль admin
        await stub.resetPassword('admin', 'admin12345');
        adminUser.password_hash = await bcrypt.hash('admin', 10);
        clearRateLimit('127.0.0.1');
        console.log('✓ API эндпоинты сброса и верификации логина работают по ТЗ.\n');

        // ----------------------------------------------------
        // ТЕСТ 5: Rate Limiter (5 попыток за 15 мин, 429 при превышении)
        // ----------------------------------------------------
        console.log('[5/6] Тестирование in-memory Rate Limiter (5 неудачных попыток -> HTTP 429)...');
        clearRateLimit('127.0.0.1');

        for (let i = 1; i <= 5; i++) {
            const failRes = await makeRequest({
                method: 'POST',
                path: '/api/auth/verify-login',
                body: { username: `bad_user_${i}` }
            });
            assert.strictEqual(failRes.status, 404, `Попытка ${i} должна вернуть 404`);
        }

        // 6-я попытка должна быть заблокирована с 429
        const blockedRes = await makeRequest({
            method: 'POST',
            path: '/api/auth/verify-login',
            body: { username: 'admin' }
        });
        assert.strictEqual(blockedRes.status, 429, '6-я попытка должна вернуть 429 Too Many Requests');
        assert(blockedRes.json.error.includes('Слишком много неудачных попыток'), 'Сообщение об ограничении должно быть на русском');

        // Проверка очистки rate limit
        clearRateLimit('127.0.0.1');
        const unblockedRes = await makeRequest({
            method: 'POST',
            path: '/api/auth/verify-login',
            body: { username: 'admin' }
        });
        assert.strictEqual(unblockedRes.status, 200, 'После очистки лимита доступ должен возобновиться');
        console.log('✓ In-memory Rate Limiter блокирует попытки после 5 неудач со статусом 429.\n');

        // ----------------------------------------------------
        // ТЕСТ 6: Законсервированные роуты (GET /events, API баз 1С)
        // ----------------------------------------------------
        console.log('[6/6] Проверка консервации роутов GET /events и API баз 1С...');
        const resEvents = await makeRequest({ method: 'GET', path: '/events' });
        assert.strictEqual(resEvents.status, 404, 'GET /events должен возвращать 404');

        const resGetDbs = await makeRequest({ method: 'GET', path: '/api/organizations/1/databases' });
        assert.strictEqual(resGetDbs.status, 404, 'GET /api/organizations/:id/databases должен возвращать 404');

        const resPostDbs = await makeRequest({ method: 'POST', path: '/api/organizations/1/databases', body: { name: 'Test DB' } });
        assert.strictEqual(resPostDbs.status, 404, 'POST /api/organizations/:id/databases должен возвращать 404');

        const resPutDb = await makeRequest({ method: 'PUT', path: '/api/databases/1', body: { name: 'Test' } });
        assert.strictEqual(resPutDb.status, 404, 'PUT /api/databases/:id должен возвращать 404');

        const resDelDb = await makeRequest({ method: 'DELETE', path: '/api/databases/1' });
        assert.strictEqual(resDelDb.status, 404, 'DELETE /api/databases/:id должен возвращать 404');
        console.log('✓ Все законсервированные маршруты возвращают 404 Not Found.\n');

        console.log('====================================================');
        console.log('  🎉 ВСЕ ТЕСТЫ СПРИНТА 7 УСПЕШНО ПРОЙДЕНЫ!');
        console.log('====================================================');

    } finally {
        if (serverInstance && serverInstance.close) {
            serverInstance.close();
        }
    }
}

runTests().catch(err => {
    console.error('❌ Ошибка выполнения тестов:', err);
    process.exit(1);
});
