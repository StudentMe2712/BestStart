const http = require('http');
const assert = require('assert');
const fs = require('fs');
const path = require('path');

process.env.DB_STUB = 'true';
const TEST_PORT = 3289;
process.env.PORT = String(TEST_PORT);

let sessionCookie = '';

async function makeRequest({ method = 'GET', path = '/', headers = {}, body = null }) {
    return new Promise((resolve, reject) => {
        const reqHeaders = { ...headers };
        if (sessionCookie && !reqHeaders['Cookie']) {
            reqHeaders['Cookie'] = sessionCookie;
        }

        let postBuffer = null;
        if (body) {
            if (Buffer.isBuffer(body)) {
                postBuffer = body;
            } else if (typeof body === 'string') {
                postBuffer = Buffer.from(body);
            } else {
                postBuffer = Buffer.from(JSON.stringify(body));
                if (!reqHeaders['Content-Type']) {
                    reqHeaders['Content-Type'] = 'application/json';
                }
            }
            reqHeaders['Content-Length'] = postBuffer.length;
        }

        const req = http.request({
            hostname: '127.0.0.1',
            port: TEST_PORT,
            path,
            method,
            headers: reqHeaders
        }, (res) => {
            if (res.headers['set-cookie']) {
                const cookies = res.headers['set-cookie'].map(c => c.split(';')[0]);
                sessionCookie = cookies.join('; ');
            }

            const chunks = [];
            res.on('data', chunk => { chunks.push(chunk); });
            res.on('end', () => {
                const buffer = Buffer.concat(chunks);
                const resData = buffer.toString('utf8');
                let json = null;
                try {
                    json = JSON.parse(resData);
                } catch (e) {
                    // not JSON
                }
                resolve({
                    status: res.statusCode,
                    headers: res.headers,
                    body: resData,
                    buffer,
                    json
                });
            });
        });

        req.on('error', reject);
        if (postBuffer) req.write(postBuffer);
        req.end();
    });
}

function createMultipartBody(fieldName, filename, mimeType, fileContentBuffer) {
    const boundary = '----NodeFormBoundary' + Math.random().toString(36).substring(2);
    const header = Buffer.from(
        `--${boundary}\r\n` +
        `Content-Disposition: form-data; name="${fieldName}"; filename="${filename}"\r\n` +
        `Content-Type: ${mimeType}\r\n\r\n`
    );
    const footer = Buffer.from(`\r\n--${boundary}--\r\n`);
    const body = Buffer.concat([header, fileContentBuffer, footer]);
    return {
        boundary,
        body,
        contentType: `multipart/form-data; boundary=${boundary}`
    };
}

async function runTests() {
    console.log('====================================================');
    console.log('  🧪 ТЕСТИРОВАНИЕ СПРИНТ 7: ЭТАП 2 И ЭТАП 4 БЭКЕНД');
    console.log('====================================================\n');

    const stub = require('./db-stub');
    const { serverInstance } = require('./server');

    await new Promise(resolve => setTimeout(resolve, 800));

    try {
        // ----------------------------------------------------
        // ТЕСТ 1: Модульные тесты userPinsDB в db-stub
        // ----------------------------------------------------
        console.log('[1/8] Тестирование userPinsDB в db-stub...');
        const initialPins = await stub.userPinsDB.getUserPins(1);
        assert.deepStrictEqual(initialPins, [1, 2], 'У администратора по умолчанию должны быть закрепления [1, 2]');

        // Закрепляем организацию 3
        const pinRes1 = await stub.userPinsDB.toggleUserPin(1, 3);
        assert.strictEqual(pinRes1.pinned, true, 'Организация 3 должна стать закрепленной');
        assert(pinRes1.pins.includes(3), 'Список закреплений должен содержать 3');

        // Открепляем организацию 1
        const pinRes2 = await stub.userPinsDB.toggleUserPin(1, 1);
        assert.strictEqual(pinRes2.pinned, false, 'Организация 1 должна быть откреплена');
        assert(!pinRes2.pins.includes(1), 'Список закреплений не должен содержать 1');

        // Возвращаем в исходное состояние [1, 2]
        await stub.userPinsDB.toggleUserPin(1, 1); // закрепляем 1 обратно
        await stub.userPinsDB.toggleUserPin(1, 3); // открепляем 3
        const restoredPins = await stub.userPinsDB.getUserPins(1);
        assert.deepStrictEqual(restoredPins, [2, 1], 'Закрепления должны вернуться к [2, 1]');
        console.log('✓ Методы userPinsDB.getUserPins() и toggleUserPin() работают корректно.\n');

        // ----------------------------------------------------
        // ТЕСТ 2: Модульные тесты filesDB в db-stub
        // ----------------------------------------------------
        console.log('[2/8] Тестирование filesDB в db-stub...');
        const testFileRecord = await stub.filesDB.create({
            org_id: 1,
            filename: 'test-123.pdf',
            original_name: 'Схема_Сети.pdf',
            mime_type: 'application/pdf',
            file_size: 1024,
            uploaded_by: 'admin'
        });
        assert(testFileRecord.id, 'Файл должен получить id');
        assert.strictEqual(testFileRecord.org_id, 1);
        assert.strictEqual(testFileRecord.original_name, 'Схема_Сети.pdf');

        const orgFiles = await stub.filesDB.getByOrgId(1);
        assert.strictEqual(orgFiles.length, 1, 'В организации 1 должен быть 1 файл');
        assert.strictEqual(orgFiles[0].id, testFileRecord.id);

        const singleFile = await stub.filesDB.getById(testFileRecord.id);
        assert.strictEqual(singleFile.filename, 'test-123.pdf');

        await stub.filesDB.delete(testFileRecord.id);
        const afterDelete = await stub.filesDB.getById(testFileRecord.id);
        assert.strictEqual(afterDelete, null, 'После удаления файл не должен находиться');
        console.log('✓ Методы filesDB (create, getByOrgId, getById, delete) работают корректно.\n');

        // ----------------------------------------------------
        // ТЕСТ 3: Аутентификация для сессионных запросов
        // ----------------------------------------------------
        console.log('[3/8] Вход в систему (POST /login) для проверки защищенных API...');
        const loginRes = await makeRequest({
            method: 'POST',
            path: '/login',
            headers: { 'Accept': 'application/json' },
            body: { username: 'admin', password: 'admin' }
        });
        assert.strictEqual(loginRes.status, 200);
        assert(sessionCookie.includes('connect.sid'), 'Сессионная кука должна быть установлена');
        console.log('✓ Авторизация успешна, получена сессия.\n');

        // ----------------------------------------------------
        // ТЕСТ 4: API User Pins (GET /api/user/pins, POST /api/organizations/:id/pin)
        // ----------------------------------------------------
        console.log('[4/8] Проверка API закреплений организаций (pins)...');
        const resGetPins = await makeRequest({ method: 'GET', path: '/api/user/pins' });
        assert.strictEqual(resGetPins.status, 200);
        assert.strictEqual(resGetPins.json.success, true);
        assert(Array.isArray(resGetPins.json.pins));

        // Toggle pin организации 5
        const resTogglePin = await makeRequest({ method: 'POST', path: '/api/organizations/5/pin' });
        assert.strictEqual(resTogglePin.status, 200);
        assert.strictEqual(resTogglePin.json.success, true);
        assert.strictEqual(resTogglePin.json.pinned, true);
        assert(resTogglePin.json.pins.includes(5));

        // Toggle pin организации 5 обратно (открепление)
        const resUntogglePin = await makeRequest({ method: 'POST', path: '/api/organizations/5/pin' });
        assert.strictEqual(resUntogglePin.status, 200);
        assert.strictEqual(resUntogglePin.json.success, true);
        assert.strictEqual(resUntogglePin.json.pinned, false);
        assert(!resUntogglePin.json.pins.includes(5));
        console.log('✓ API эндпоинты /api/user/pins и /api/organizations/:id/pin работают корректно.\n');

        // ----------------------------------------------------
        // ТЕСТ 5: Загрузка файла через Multer (POST /api/organizations/:id/files)
        // ----------------------------------------------------
        console.log('[5/8] Проверка загрузки файла (POST /api/organizations/:id/files)...');
        const dummyPdfContent = Buffer.from('%PDF-1.4 Mock PDF Content For Testing Purposes');
        const multipart = createMultipartBody('file', 'network_config.pdf', 'application/pdf', dummyPdfContent);

        const uploadRes = await makeRequest({
            method: 'POST',
            path: '/api/organizations/1/files',
            headers: {
                'Content-Type': multipart.contentType
            },
            body: multipart.body
        });
        assert.strictEqual(uploadRes.status, 200);
        assert.strictEqual(uploadRes.json.success, true);
        const uploadedFile = uploadRes.json.file;
        assert(uploadedFile.id, 'Загруженный файл должен иметь id');
        assert.strictEqual(uploadedFile.org_id, 1);
        assert.strictEqual(uploadedFile.original_name, 'network_config.pdf');
        assert.strictEqual(uploadedFile.mime_type, 'application/pdf');

        // Проверяем физическое наличие файла на диске в uploads/org_1/
        const diskPath = path.join(__dirname, 'uploads', 'org_1', uploadedFile.filename);
        assert(fs.existsSync(diskPath), `Файл должен существовать на диске по пути ${diskPath}`);
        console.log('✓ Файл успешно загружен через Multer и сохранен на диске.\n');

        // ----------------------------------------------------
        // ТЕСТ 6: Список файлов, Inline View, Download и Delete
        // ----------------------------------------------------
        console.log('[6/8] Проверка получения списка, просмотра, скачивания и удаления файла...');
        // Список файлов организации
        const listRes = await makeRequest({ method: 'GET', path: '/api/organizations/1/files' });
        assert.strictEqual(listRes.status, 200);
        assert.strictEqual(listRes.json.success, true);
        assert(listRes.json.files.some(f => f.id === uploadedFile.id), 'Файл должен быть в списке файлов организации');

        // Просмотр файла (inline)
        const viewRes = await makeRequest({ method: 'GET', path: `/api/files/${uploadedFile.id}/view` });
        assert.strictEqual(viewRes.status, 200);
        assert.strictEqual(viewRes.headers['content-type'], 'application/pdf');
        assert(viewRes.headers['content-disposition'].includes('inline'));
        assert(viewRes.buffer.includes(Buffer.from('%PDF-1.4')));

        // Скачивание файла (download)
        const downloadRes = await makeRequest({ method: 'GET', path: `/api/files/${uploadedFile.id}/download` });
        assert.strictEqual(downloadRes.status, 200);
        assert(downloadRes.headers['content-disposition'].includes('attachment'));
        assert(downloadRes.headers['content-disposition'].includes('network_config.pdf'));

        // Удаление файла
        const delRes = await makeRequest({ method: 'DELETE', path: `/api/files/${uploadedFile.id}` });
        assert.strictEqual(delRes.status, 200);
        assert.strictEqual(delRes.json.success, true);
        assert(!fs.existsSync(diskPath), 'Файл должен быть удален с диска');

        // Проверка 404 после удаления
        const view404 = await makeRequest({ method: 'GET', path: `/api/files/${uploadedFile.id}/view` });
        assert.strictEqual(view404.status, 404);
        console.log('✓ Просмотр (view), скачивание (download) и удаление (delete) работают корректно.\n');

        // ----------------------------------------------------
        // ТЕСТ 7: Валидация Multer (запрещенные расширения и отсутствие файла)
        // ----------------------------------------------------
        console.log('[7/8] Проверка валидации Multer (безопасность расширений и пустые запросы)...');
        // Запрещенное расширение (.exe)
        const badExeMultipart = createMultipartBody('file', 'malware.exe', 'application/octet-stream', Buffer.from('MZ...'));
        const badExeRes = await makeRequest({
            method: 'POST',
            path: '/api/organizations/1/files',
            headers: {
                'Content-Type': badExeMultipart.contentType
            },
            body: badExeMultipart.body
        });
        assert.strictEqual(badExeRes.status, 400, 'Загрузка .exe должна возвращать HTTP 400');
        assert(badExeRes.json.error.includes('Недопустимый тип файла'));

        // Запрос без прикрепленного файла
        const noFileRes = await makeRequest({
            method: 'POST',
            path: '/api/organizations/1/files',
            body: {}
        });
        assert.strictEqual(noFileRes.status, 400, 'Запрос без файла должен возвращать HTTP 400');
        console.log('✓ Валидация расширений и проверка наличия файла работают корректно.\n');

        // ----------------------------------------------------
        // ТЕСТ 8: Рендеринг маршрутов с данными закреплений и файлов (GET /, GET /wiki, GET /wiki/organization/:id)
        // ----------------------------------------------------
        console.log('[8/8] Проверка рендеринга страниц с userPins и files...');
        const homeRes = await makeRequest({ method: 'GET', path: '/' });
        assert.strictEqual(homeRes.status, 200, 'Главная страница должна рендериться успешно (200)');

        const wikiRes = await makeRequest({ method: 'GET', path: '/wiki' });
        assert.strictEqual(wikiRes.status, 200, 'Страница Wiki должна рендериться успешно (200)');

        const orgRes = await makeRequest({ method: 'GET', path: '/wiki/organization/1' });
        assert.strictEqual(orgRes.status, 200, 'Карточка организации должна рендериться успешно (200)');
        console.log('✓ Маршруты успешно интегрированы с новыми данными.\n');

        console.log('====================================================');
        console.log('  🎉 ВСЕ ТЕСТЫ ЭТАПОВ 2 И 4 УСПЕШНО ПРОЙДЕНЫ!');
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
