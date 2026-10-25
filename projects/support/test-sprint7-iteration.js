const http = require('http');
const assert = require('assert');
const fs = require('fs');
const path = require('path');
const XLSX = require('xlsx');

process.env.DB_STUB = 'true';
const TEST_PORT = 3295;
process.env.PORT = String(TEST_PORT);

let sessionCookie = '';

async function makeRequest({ method = 'GET', path: reqPath = '/', headers = {}, body = null }) {
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
            path: reqPath,
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
                } catch (e) {}
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

async function runIterationTests() {
    console.log('====================================================');
    console.log('  🧪 ТЕСТИРОВАНИЕ ИТЕРАЦИИ СПРИНТА 7: ФИКСЫ И PREVIEW API');
    console.log('====================================================\n');

    const stub = require('./db-stub');
    const { serverInstance } = require('./server');

    await new Promise(resolve => setTimeout(resolve, 800));

    try {
        // ----------------------------------------------------
        // Авторизация
        // ----------------------------------------------------
        console.log('[1/5] Авторизация администратора...');
        const loginRes = await makeRequest({
            method: 'POST',
            path: '/login',
            headers: { 'Accept': 'application/json' },
            body: { username: 'admin', password: 'admin' }
        });
        assert.strictEqual(loginRes.status, 200);
        console.log('✓ Авторизация успешна.\n');

        // ----------------------------------------------------
        // ТЕСТ 1: Заголовки безопасности X-Frame-Options: SAMEORIGIN & CSP
        // ----------------------------------------------------
        console.log('[2/5] Проверка X-Frame-Options: SAMEORIGIN и frame-ancestors...');
        const homeRes = await makeRequest({ method: 'GET', path: '/' });
        assert.strictEqual(homeRes.headers['x-frame-options'], 'SAMEORIGIN', 'Глобальный X-Frame-Options должен быть SAMEORIGIN');
        assert.strictEqual(homeRes.headers['referrer-policy'], 'same-origin');

        // ----------------------------------------------------
        // ТЕСТ 2: Загрузка файла с русским именем (исправление кодировки Multer)
        // ----------------------------------------------------
        console.log('[3/5] Проверка исправления кодировки русских имен файлов...');
        const cyrillicName = 'Смета_ИТС_2026.txt';
        const fileContent = Buffer.from('Тестовое содержимое файла сметы');

        const cyrillicMultipart = createMultipartBody('file', cyrillicName, 'text/plain', fileContent);
        const uploadCyrillicRes = await makeRequest({
            method: 'POST',
            path: '/api/organizations/1/files',
            headers: {
                'Content-Type': cyrillicMultipart.contentType
            },
            body: cyrillicMultipart.body
        });

        assert.strictEqual(uploadCyrillicRes.status, 200);
        assert.strictEqual(uploadCyrillicRes.json.success, true);
        assert.strictEqual(uploadCyrillicRes.json.file.original_name, cyrillicName, 'Имя файла должно корректно восстановиться из UTF-8 latin1');
        const cyrillicFileId = uploadCyrillicRes.json.file.id;

        // Проверка заголовков в /view для iframe Lightbox
        const viewRes = await makeRequest({ method: 'GET', path: `/api/files/${cyrillicFileId}/view` });
        assert.strictEqual(viewRes.status, 200);
        assert.strictEqual(viewRes.headers['x-frame-options'], 'SAMEORIGIN');
        assert.strictEqual(viewRes.headers['content-security-policy'], "frame-ancestors 'self'");
        console.log('✓ Русские имена файлов и заголовки iframe работают корректно.\n');

        // ----------------------------------------------------
        // ТЕСТ 3: Загрузка и Preview XML (.xml)
        // ----------------------------------------------------
        console.log('[4/5] Проверка поддержки и предварительного просмотра XML (.xml)...');
        const xmlContent = '<?xml version="1.0" encoding="UTF-8"?><config><server host="10.0.0.1" port="8080"/></config>';
        const xmlMultipart = createMultipartBody('file', 'server_config.xml', 'application/xml', Buffer.from(xmlContent));
        const uploadXml = await makeRequest({
            method: 'POST',
            path: '/api/organizations/1/files',
            headers: {
                'Content-Type': xmlMultipart.contentType
            },
            body: xmlMultipart.body
        });

        assert.strictEqual(uploadXml.status, 200);
        assert.strictEqual(uploadXml.json.success, true);
        const xmlFileId = uploadXml.json.file.id;

        const xmlPreviewRes = await makeRequest({ method: 'GET', path: `/api/files/${xmlFileId}/preview` });
        assert.strictEqual(xmlPreviewRes.status, 200);
        assert.strictEqual(xmlPreviewRes.json.success, true);
        assert.strictEqual(xmlPreviewRes.json.type, 'xml');
        assert.strictEqual(xmlPreviewRes.json.content, xmlContent);
        console.log('✓ Файлы XML загружаются и парсятся эндпоинтом /preview.\n');

        // ----------------------------------------------------
        // ТЕСТ 4: Preview для Excel (.xlsx), PDF, Image и Unsupported
        // ----------------------------------------------------
        console.log('[5/5] Проверка Preview API для Excel, PDF, изображений и неподдерживаемых файлов...');
        // 4.1 Создание и загрузка валидного Excel через XLSX
        const wb = XLSX.utils.book_new();
        const wsData = [
            ['Сервер', 'IP Адрес', 'Статус'],
            ['SRV-1C-01', '192.168.1.10', 'Online'],
            ['SRV-DB-02', '192.168.1.20', 'Online']
        ];
        const ws = XLSX.utils.aoa_to_sheet(wsData);
        XLSX.utils.book_append_sheet(wb, ws, 'Серверы');
        const excelBuffer = XLSX.write(wb, { type: 'buffer', bookType: 'xlsx' });

        const excelMultipart = createMultipartBody('file', 'Servers_Inventory.xlsx', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', excelBuffer);
        const uploadExcel = await makeRequest({
            method: 'POST',
            path: '/api/organizations/1/files',
            headers: {
                'Content-Type': excelMultipart.contentType
            },
            body: excelMultipart.body
        });
        assert.strictEqual(uploadExcel.status, 200);
        const excelFileId = uploadExcel.json.file.id;

        const excelPreviewRes = await makeRequest({ method: 'GET', path: `/api/files/${excelFileId}/preview` });
        assert.strictEqual(excelPreviewRes.status, 200);
        assert.strictEqual(excelPreviewRes.json.success, true);
        assert.strictEqual(excelPreviewRes.json.type, 'excel');
        assert(excelPreviewRes.json.html.includes('SRV-1C-01'), 'HTML таблица должна содержать данные Excel');
        assert.deepStrictEqual(excelPreviewRes.json.sheetNames, ['Серверы']);
        assert.strictEqual(excelPreviewRes.json.currentSheet, 'Серверы');

        // 4.2 PDF preview
        const pdfContent = Buffer.from('%PDF-1.4 Mock PDF');
        const pdfMultipart = createMultipartBody('file', 'Guide.pdf', 'application/pdf', pdfContent);
        const uploadPdf = await makeRequest({
            method: 'POST',
            path: '/api/organizations/1/files',
            headers: {
                'Content-Type': pdfMultipart.contentType
            },
            body: pdfMultipart.body
        });
        const pdfFileId = uploadPdf.json.file.id;
        const pdfPreviewRes = await makeRequest({ method: 'GET', path: `/api/files/${pdfFileId}/preview` });
        assert.strictEqual(pdfPreviewRes.json.type, 'pdf');
        assert.strictEqual(pdfPreviewRes.json.viewUrl, `/api/files/${pdfFileId}/view`);

        // 4.3 Image preview
        const pngContent = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
        const pngMultipart = createMultipartBody('file', 'schema.png', 'image/png', pngContent);
        const uploadPng = await makeRequest({
            method: 'POST',
            path: '/api/organizations/1/files',
            headers: {
                'Content-Type': pngMultipart.contentType
            },
            body: pngMultipart.body
        });
        const pngFileId = uploadPng.json.file.id;
        const pngPreviewRes = await makeRequest({ method: 'GET', path: `/api/files/${pngFileId}/preview` });
        assert.strictEqual(pngPreviewRes.json.type, 'image');
        assert.strictEqual(pngPreviewRes.json.viewUrl, `/api/files/${pngFileId}/view`);

        // 4.4 Unsupported preview (например, .rdp или .zip)
        const zipContent = Buffer.from('PK mock zip');
        const zipMultipart = createMultipartBody('file', 'archive.zip', 'application/zip', zipContent);
        const uploadZip = await makeRequest({
            method: 'POST',
            path: '/api/organizations/1/files',
            headers: {
                'Content-Type': zipMultipart.contentType
            },
            body: zipMultipart.body
        });
        const zipFileId = uploadZip.json.file.id;
        const zipPreviewRes = await makeRequest({ method: 'GET', path: `/api/files/${zipFileId}/preview` });
        assert.strictEqual(zipPreviewRes.json.success, false);
        assert.strictEqual(zipPreviewRes.json.type, 'unsupported');
        assert.strictEqual(zipPreviewRes.json.downloadUrl, `/api/files/${zipFileId}/download`);

        console.log('✓ Preview API корректно обрабатывает все типы файлов (Excel, XML, PDF, Image, Unsupported).\n');

        // Очистка созданных тестовых файлов
        await makeRequest({ method: 'DELETE', path: `/api/files/${cyrillicFileId}` });
        await makeRequest({ method: 'DELETE', path: `/api/files/${xmlFileId}` });
        await makeRequest({ method: 'DELETE', path: `/api/files/${excelFileId}` });
        await makeRequest({ method: 'DELETE', path: `/api/files/${pdfFileId}` });
        await makeRequest({ method: 'DELETE', path: `/api/files/${zipFileId}` });

        console.log('====================================================');
        console.log('  🎉 ВСЕ ФИКСЫ ИТЕРАЦИИ СПРИНТА 7 УСПЕШНО ПРОТЕСТИРОВАНЫ!');
        console.log('====================================================');
    } finally {
        if (serverInstance && serverInstance.close) {
            serverInstance.close();
        }
    }
}

runIterationTests().catch(err => {
    console.error('❌ Ошибка выполнения тестов:', err);
    process.exit(1);
});
