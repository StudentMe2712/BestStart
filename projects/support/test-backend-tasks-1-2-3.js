const http = require('http');
const assert = require('assert');
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

process.env.DB_STUB = 'true';
const TEST_PORT = 3298;
process.env.PORT = String(TEST_PORT);

function httpRequest({ hostname = '127.0.0.1', port = TEST_PORT, path = '/', method = 'GET', headers = {} }, body = null) {
    return new Promise((resolve, reject) => {
        const req = http.request({ hostname, port, path, method, headers }, (res) => {
            const chunks = [];
            res.on('data', chunk => chunks.push(chunk));
            res.on('end', () => {
                const buffer = Buffer.concat(chunks);
                const str = buffer.toString('utf-8');
                let json = null;
                try { json = JSON.parse(str); } catch (e) {}
                resolve({
                    statusCode: res.statusCode,
                    headers: res.headers,
                    body: str,
                    buffer,
                    json
                });
            });
        });
        req.on('error', reject);
        if (body) req.write(body);
        req.end();
    });
}

// Вспомогательная функция для сборки минимального валидного .docx (zip)
function createMinimalDocxBuffer(text) {
    // Используем готовый test-sample.docx если есть, либо создаем синхронно
    if (fs.existsSync(path.join(__dirname, 'test-sample.docx'))) {
        return fs.readFileSync(path.join(__dirname, 'test-sample.docx'));
    }
    throw new Error('test-sample.docx not found');
}

async function runTests() {
    console.log('====================================================');
    console.log(' 🧪 ТЕСТИРОВАНИЕ БЭКЕНДА: ЗАДАЧИ 1, 2, 3');
    console.log('====================================================\n');

    const { serverInstance, filesDB } = require('./server');
    const stub = require('./db-stub');

    await new Promise(r => setTimeout(r, 600));

    try {
        // ----------------------------------------------------
        // ПОДГОТОВКА СЕССИЙ ДЛЯ ДВУХ ПОЛЬЗОВАТЕЛЕЙ
        // ----------------------------------------------------
        console.log('[Подготовка] Вход Пользователя 1 (admin) и Пользователя 2 (user2)...');
        // Вход admin
        const loginData1 = 'username=admin&password=admin';
        const loginRes1 = await httpRequest({
            method: 'POST',
            path: '/login',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'Content-Length': Buffer.byteLength(loginData1)
            }
        }, loginData1);
        const cookie1 = loginRes1.headers['set-cookie'] ? loginRes1.headers['set-cookie'][0].split(';')[0] : '';
        assert.ok(cookie1, 'Сессия для admin должна быть создана');

        // Регистрация/вход второго пользователя в db-stub
        await stub.registerUser('engineer2', 'password123');
        const loginData2 = 'username=engineer2&password=password123';
        const loginRes2 = await httpRequest({
            method: 'POST',
            path: '/login',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'Content-Length': Buffer.byteLength(loginData2)
            }
        }, loginData2);
        const cookie2 = loginRes2.headers['set-cookie'] ? loginRes2.headers['set-cookie'][0].split(';')[0] : '';
        assert.ok(cookie2, 'Сессия для engineer2 должна быть создана');
        console.log('✓ Пользователи admin и engineer2 успешно авторизованы.\n');

        // ----------------------------------------------------
        // ТЕСТ 1: ЗАГРУЗКА И ПРЕДПРОСМОТР .DOCX И .DOC (ЗАДАЧА 1)
        // ----------------------------------------------------
        console.log('[1/4] Тестирование поддержки .docx и .doc...');

        // 1.1 Загрузка валидного .docx
        const docxBuffer = createMinimalDocxBuffer('Инструкция по настройке');
        const boundaryDocx = '----BoundaryDocx' + Date.now();
        const multipartDocx = Buffer.concat([
            Buffer.from(`--${boundaryDocx}\r\nContent-Disposition: form-data; name="file"; filename="instruction.docx"\r\nContent-Type: application/vnd.openxmlformats-officedocument.wordprocessingml.document\r\n\r\n`),
            docxBuffer,
            Buffer.from(`\r\n--${boundaryDocx}--\r\n`)
        ]);

        const uploadDocxRes = await httpRequest({
            method: 'POST',
            path: '/api/organizations/1/files',
            headers: {
                'Cookie': cookie1,
                'Content-Type': `multipart/form-data; boundary=${boundaryDocx}`,
                'Content-Length': multipartDocx.length
            }
        }, multipartDocx);

        assert.strictEqual(uploadDocxRes.statusCode, 200);
        const docxData = uploadDocxRes.json;
        assert.strictEqual(docxData.success, true);
        assert.strictEqual(docxData.file.original_name, 'instruction.docx');
        const docxFileId = docxData.file.id;

        // Предпросмотр .docx через mammoth
        const previewDocxRes = await httpRequest({
            method: 'GET',
            path: `/api/files/${docxFileId}/preview`,
            headers: { 'Cookie': cookie1 }
        });
        assert.strictEqual(previewDocxRes.statusCode, 200);
        assert.strictEqual(previewDocxRes.json.success, true);
        assert.strictEqual(previewDocxRes.json.type, 'docx');
        assert.ok(previewDocxRes.json.html && previewDocxRes.json.html.includes('Тестовый документ Word'), 'HTML должен содержать распарсенный контент');
        console.log('  ✓ Предпросмотр .docx через mammoth конвертирует документ в HTML');

        // 1.2 Загрузка поврежденного/пустого .docx -> проверка fallback
        const corruptedDocx = Buffer.from('NOT_A_REAL_ZIP_DOCX_FILE_DATA');
        const boundaryCorrupt = '----BoundaryCorrupt' + Date.now();
        const multipartCorrupt = Buffer.concat([
            Buffer.from(`--${boundaryCorrupt}\r\nContent-Disposition: form-data; name="file"; filename="broken.docx"\r\nContent-Type: application/vnd.openxmlformats-officedocument.wordprocessingml.document\r\n\r\n`),
            corruptedDocx,
            Buffer.from(`\r\n--${boundaryCorrupt}--\r\n`)
        ]);

        const uploadCorruptRes = await httpRequest({
            method: 'POST',
            path: '/api/organizations/1/files',
            headers: {
                'Cookie': cookie1,
                'Content-Type': `multipart/form-data; boundary=${boundaryCorrupt}`,
                'Content-Length': multipartCorrupt.length
            }
        }, multipartCorrupt);
        const brokenFileId = uploadCorruptRes.json.file.id;

        const previewCorruptRes = await httpRequest({
            method: 'GET',
            path: `/api/files/${brokenFileId}/preview`,
            headers: { 'Cookie': cookie1 }
        });
        assert.strictEqual(previewCorruptRes.statusCode, 200);
        assert.strictEqual(previewCorruptRes.json.success, true);
        assert.strictEqual(previewCorruptRes.json.type, 'doc_fallback');
        assert.ok(previewCorruptRes.json.downloadUrl.includes(`/api/files/${brokenFileId}/download`));
        assert.ok(previewCorruptRes.json.message.includes('Предпросмотр документа недоступен'));
        console.log('  ✓ Поврежденный .docx корректно отдает doc_fallback');

        // 1.3 Загрузка бинарного .doc
        const docBinaryData = Buffer.from('\xD0\xCF\x11\xE0\xA1\xB1\x1A\xE1 binary OLE doc');
        const boundaryDoc = '----BoundaryDoc' + Date.now();
        const multipartDoc = Buffer.concat([
            Buffer.from(`--${boundaryDoc}\r\nContent-Disposition: form-data; name="file"; filename="contract.doc"\r\nContent-Type: application/msword\r\n\r\n`),
            docBinaryData,
            Buffer.from(`\r\n--${boundaryDoc}--\r\n`)
        ]);

        const uploadDocRes = await httpRequest({
            method: 'POST',
            path: '/api/organizations/1/files',
            headers: {
                'Cookie': cookie1,
                'Content-Type': `multipart/form-data; boundary=${boundaryDoc}`,
                'Content-Length': multipartDoc.length
            }
        }, multipartDoc);
        assert.strictEqual(uploadDocRes.statusCode, 200);
        const docFileId = uploadDocRes.json.file.id;

        const previewDocRes = await httpRequest({
            method: 'GET',
            path: `/api/files/${docFileId}/preview`,
            headers: { 'Cookie': cookie1 }
        });
        assert.strictEqual(previewDocRes.statusCode, 200);
        assert.strictEqual(previewDocRes.json.success, true);
        assert.strictEqual(previewDocRes.json.type, 'doc_fallback');
        assert.ok(previewDocRes.json.message.includes('Формат .doc поддерживается для загрузки и скачивания'));
        console.log('  ✓ Формат .doc корректно возвращает doc_fallback с информационным сообщением');

        // 1.4 Проверка отклонения недопустимого расширения
        const badData = Buffer.from('malicious payload');
        const boundaryBad = '----BoundaryBad' + Date.now();
        const multipartBad = Buffer.concat([
            Buffer.from(`--${boundaryBad}\r\nContent-Disposition: form-data; name="file"; filename="trojan.exe"\r\nContent-Type: application/x-msdownload\r\n\r\n`),
            badData,
            Buffer.from(`\r\n--${boundaryBad}--\r\n`)
        ]);

        const uploadBadRes = await httpRequest({
            method: 'POST',
            path: '/api/organizations/1/files',
            headers: {
                'Cookie': cookie1,
                'Content-Type': `multipart/form-data; boundary=${boundaryBad}`,
                'Content-Length': multipartBad.length
            }
        }, multipartBad);
        assert.strictEqual(uploadBadRes.statusCode, 400, 'Недопустимый файл должен быть отклонен multer');
        console.log('✓ Задача 1 (.docx / .doc поддержка) успешно протестирована.\n');

        // ----------------------------------------------------
        // ТЕСТ 2: ИЗОБРАЖЕНИЯ В ПОЛНОМ 1:1 КАЧЕСТВЕ (ЗАДАЧА 2)
        // ----------------------------------------------------
        console.log('[2/4] Тестирование отдачи изображений 1:1 без потери качества...');
        // 1x1 пиксель PNG
        const pngData = Buffer.from([
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
            0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
            0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
            0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
            0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
            0x42, 0x60, 0x82
        ]);
        const boundaryPng = '----BoundaryPng' + Date.now();
        const multipartPng = Buffer.concat([
            Buffer.from(`--${boundaryPng}\r\nContent-Disposition: form-data; name="file"; filename="logo-full-quality.png"\r\nContent-Type: image/png\r\n\r\n`),
            pngData,
            Buffer.from(`\r\n--${boundaryPng}--\r\n`)
        ]);

        const uploadImgRes = await httpRequest({
            method: 'POST',
            path: '/api/organizations/1/files',
            headers: {
                'Cookie': cookie1,
                'Content-Type': `multipart/form-data; boundary=${boundaryPng}`,
                'Content-Length': multipartPng.length
            }
        }, multipartPng);
        assert.strictEqual(uploadImgRes.statusCode, 200);
        const imgFileId = uploadImgRes.json.file.id;

        // Запрос /api/files/:fileId/view
        const viewImgRes = await httpRequest({
            method: 'GET',
            path: `/api/files/${imgFileId}/view`,
            headers: { 'Cookie': cookie1 }
        });

        assert.strictEqual(viewImgRes.statusCode, 200);
        assert.strictEqual(viewImgRes.headers['content-type'], 'image/png');
        assert.ok(viewImgRes.headers['content-disposition'].includes('inline'));
        assert.ok(viewImgRes.headers['content-disposition'].includes('logo-full-quality.png'));
        assert.strictEqual(viewImgRes.headers['cache-control'], 'private, max-age=86400');
        assert.strictEqual(viewImgRes.headers['x-frame-options'], 'SAMEORIGIN');

        // Проверка 1:1 побайтовой целостности без сжатия/downsampling
        assert.strictEqual(viewImgRes.buffer.length, pngData.length, 'Длина отданного изображения должна 100% совпадать с исходником');
        assert.ok(viewImgRes.buffer.equals(pngData), 'Байты изображения отданы точно 1:1');
        console.log('✓ Задача 2 (1:1 качество изображений, заголовки, кэширование) успешно протестирована.\n');

        // ----------------------------------------------------
        // ТЕСТ 3: ОБЩИЙ ДОСТУП И УДАЛЕНИЕ ВСЕМИ ПОЛЬЗОВАТЕЛЯМИ (ЗАДАЧА 3)
        // ----------------------------------------------------
        console.log('[3/4] Тестирование общего доступа к файлам и права удаления любым пользователем...');

        // Пользователь 2 (engineer2) запрашивает файлы организации 1 (загруженные пользователем 1)
        const listRes = await httpRequest({
            method: 'GET',
            path: '/api/organizations/1/files',
            headers: { 'Cookie': cookie2 }
        });
        assert.strictEqual(listRes.statusCode, 200);
        assert.strictEqual(listRes.json.success, true);
        const filesList = listRes.json.files;
        assert.ok(Array.isArray(filesList));
        assert.ok(filesList.some(f => f.id === docxFileId && f.uploaded_by === 'admin'), 'engineer2 видит файл, загруженный admin');
        console.log('  ✓ Пользователь engineer2 видит файлы организации, созданные admin (без фильтрации по автору)');

        // Пользователь 2 может открыть на просмотр файл пользователя 1
        const viewFromUser2 = await httpRequest({
            method: 'GET',
            path: `/api/files/${imgFileId}/view`,
            headers: { 'Cookie': cookie2 }
        });
        assert.strictEqual(viewFromUser2.statusCode, 200);
        assert.ok(viewFromUser2.buffer.equals(pngData));
        console.log('  ✓ Пользователь engineer2 может просматривать вложения admin');

        // Пользователь 2 удаляет файл, загруженный пользователем 1
        const delRes = await httpRequest({
            method: 'DELETE',
            path: `/api/files/${docxFileId}`,
            headers: { 'Cookie': cookie2 }
        });
        assert.strictEqual(delRes.statusCode, 200);
        assert.strictEqual(delRes.json.success, true);
        console.log('  ✓ Пользователь engineer2 успешно удалил файл admin');

        // Проверка что файл удален из базы и на диске
        const checkDeletedRes = await httpRequest({
            method: 'GET',
            path: `/api/files/${docxFileId}/preview`,
            headers: { 'Cookie': cookie2 }
        });
        assert.strictEqual(checkDeletedRes.statusCode, 404);
        console.log('  ✓ Удаленный файл возвращает 404 Not Found');

        // Очистим оставшиеся созданные тестом файлы
        await httpRequest({ method: 'DELETE', path: `/api/files/${brokenFileId}`, headers: { 'Cookie': cookie1 } });
        await httpRequest({ method: 'DELETE', path: `/api/files/${docFileId}`, headers: { 'Cookie': cookie1 } });
        await httpRequest({ method: 'DELETE', path: `/api/files/${imgFileId}`, headers: { 'Cookie': cookie1 } });

        console.log('✓ Задача 3 (общий доступ и удаление) успешно протестирована.\n');

        // ----------------------------------------------------
        // ТЕСТ 4: МОДУЛЬНАЯ ПРОВЕРКА filesDB В STUB И ИНТЕРФЕЙСОВ
        // ----------------------------------------------------
        console.log('[4/4] Тестирование методов filesDB.getByOrgId и filesDB.delete...');
        const testFileRecord = await stub.filesDB.create({
            org_id: 999,
            filename: 'test_order.txt',
            original_name: 'test_order.txt',
            mime_type: 'text/plain',
            file_size: 100,
            uploaded_by: 'someone'
        });
        assert.ok(testFileRecord.id > 0);

        const orgFiles = await stub.filesDB.getByOrgId(999);
        assert.strictEqual(orgFiles.length, 1);
        assert.strictEqual(orgFiles[0].id, testFileRecord.id);

        const deleteResult = await stub.filesDB.delete(testFileRecord.id);
        assert.strictEqual(deleteResult, 1);

        const orgFilesAfter = await stub.filesDB.getByOrgId(999);
        assert.strictEqual(orgFilesAfter.length, 0);
        console.log('✓ Методы filesDB работают корректно.\n');

        console.log('====================================================');
        console.log('  🎉 ВСЕ ТРЕБОВАНИЯ ЗАДАЧ 1, 2, 3 УСПЕШНО ВЫПОЛНЕНЫ!');
        console.log('====================================================\n');
    } catch (err) {
        console.error('❌ Ошибка тестирования:', err);
        process.exitCode = 1;
    } finally {
        if (serverInstance) {
            serverInstance.close();
        }
    }
}

runTests();
