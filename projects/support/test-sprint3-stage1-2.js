process.env.DB_STUB = 'true';
const TEST_PORT = 3291;
process.env.PORT = String(TEST_PORT);

const http = require('http');

async function runTests() {
    console.log('====================================================');
    console.log('  🧪 ТЕСТИРОВАНИЕ СПРИНТ 3: ЭТАП 1 И ЭТАП 2');
    console.log('====================================================\n');

    const { serverInstance } = require('./server');
    await new Promise(resolve => setTimeout(resolve, 800));

    const BASE_URL = `http://localhost:${TEST_PORT}`;
    let sessionCookie = '';

    function request(method, path, body = null, headers = {}) {
        return new Promise((resolve, reject) => {
            const url = new URL(path, BASE_URL);
            const reqHeaders = { ...headers };
            if (sessionCookie) reqHeaders['Cookie'] = sessionCookie;
            if (body && !reqHeaders['Content-Type']) {
                reqHeaders['Content-Type'] = 'application/x-www-form-urlencoded';
            }

            const req = http.request(url, { method, headers: reqHeaders }, (res) => {
                let data = '';
                res.on('data', chunk => data += chunk);
                res.on('end', () => {
                    const setCookie = res.headers['set-cookie'];
                    if (setCookie) {
                        sessionCookie = setCookie.map(c => c.split(';')[0]).join('; ');
                    }
                    resolve({
                        statusCode: res.statusCode,
                        headers: res.headers,
                        body: data
                    });
                });
            });

            req.on('error', reject);
            if (body) req.write(body);
            req.end();
        });
    }

    try {
        // 1. Авторизация
        console.log('[1/4] Авторизация в системе через POST /login...');
        const loginRes = await request('POST', '/login', 'username=admin&password=admin');
        if (loginRes.statusCode !== 302 && loginRes.statusCode !== 200) {
            throw new Error(`Ошибка авторизации, статус: ${loginRes.statusCode}`);
        }
        console.log('   ✓ Успешная авторизация, получен session cookie\n');

        // 2. Проверка маршрута /wiki/organization/1 (Этап 1: Очистка Wiki)
        console.log('[2/4] Проверка страницы организации /wiki/organization/1...');
        const wikiRes = await request('GET', '/wiki/organization/1');
        if (wikiRes.statusCode !== 200) {
            throw new Error(`Ожидался статус 200 для /wiki/organization/1, получено: ${wikiRes.statusCode}`);
        }

        const html = wikiRes.body;

        // 2.1 Проверка отсутствия бейджей PROD и DEV в сайдбаре и шапке
        console.log('   - Проверка отсутствия бейджей контура PROD / DEV...');
        if (html.includes('wiki-badge-env') || html.includes('org-header-env-badge')) {
            throw new Error('Обнаружены классы бейджей окружения (wiki-badge-env / org-header-env-badge)!');
        }
        if (html.includes('wiki-badge-env--prod') || html.includes('wiki-badge-env--dev')) {
            throw new Error('Обнаружены модификаторы бейджей окружения (--prod / --dev)!');
        }
        // Убедимся, что в сайдбаре нет текста >PROD< или >DEV<
        const hasProdBadgeText = />\s*PROD\s*</i.test(html);
        const hasDevBadgeText = />\s*DEV\s*</i.test(html);
        if (hasProdBadgeText || hasDevBadgeText) {
            throw new Error('В HTML найдены бейджи PROD / DEV!');
        }
        console.log('   ✓ Никаких плашек PROD / DEV в шапке и сайдбаре!');

        // 2.2 Проверка отсутствия кнопки «Скопировать все реквизиты» и .content-header-bar
        console.log('   - Проверка удаления кнопки «Скопировать все реквизиты» и .content-header-bar...');
        if (html.includes('copyAllBtn') || html.includes('Скопировать все реквизиты')) {
            throw new Error('Обнаружена кнопка copyAllBtn или текст «Скопировать все реквизиты»!');
        }
        if (html.includes('content-header-bar')) {
            throw new Error('Обнаружен устаревший контейнер .content-header-bar!');
        }
        console.log('   ✓ Кнопка «Скопировать все реквизиты» и бар заголовка полностью удалены!');

        // 2.3 Проверка чистоты и строгости шапки
        console.log('   - Проверка элементов строгой шапки (#orgTitle, #orgSubtitle, #editBtnTop, #moreBtnTop)...');
        if (!html.includes('id="orgTitle"') || !html.includes('ALPHUTA')) {
            throw new Error('Заголовок организации #orgTitle с именем ALPHUTA не найден!');
        }
        if (!html.includes('id="orgSubtitle"') || !html.includes('Последнее изменение:')) {
            throw new Error('Подпись #orgSubtitle с «Последнее изменение:» не найдена!');
        }
        if (!html.includes('id="editBtnTop"')) {
            throw new Error('Кнопка редактирования #editBtnTop не найдена!');
        }
        if (!html.includes('id="moreBtnTop"')) {
            throw new Error('Кнопка меню «Ещё» #moreBtnTop не найдена!');
        }
        if (!html.includes('/api/export/pdf/1') || !html.includes('/api/export/txt/1')) {
            throw new Error('Ссылки на экспорт PDF / TXT в меню «Ещё» не найдены!');
        }
        if (!html.includes('org-nav-arrows')) {
            throw new Error('Кнопки навигации ‹ и › не найдены!');
        }
        console.log('   ✓ Шапка организации чистая и содержит только регламентные элементы!\n');

        // 3. Проверка экспорта в PDF (/api/export/pdf/1) (Этап 2: Исправление экспорта PDF)
        console.log('[3/4] Проверка маршрута экспорта в PDF /api/export/pdf/1...');
        const pdfRes = await request('GET', '/api/export/pdf/1');
        if (pdfRes.statusCode !== 200) {
            throw new Error(`Ожидался статус 200 для /api/export/pdf/1, получено: ${pdfRes.statusCode}`);
        }
        const pdfContentType = pdfRes.headers['content-type'] || '';
        if (!pdfContentType.includes('text/html')) {
            throw new Error(`Ожидался Content-Type text/html для страницы печати, получено: ${pdfContentType}`);
        }
        const pdfHtml = pdfRes.body;
        if (!pdfHtml.includes('window.print()')) {
            throw new Error('Страница предварительного просмотра не содержит вызов window.print()!');
        }
        if (!pdfHtml.includes('ALPHUTA')) {
            throw new Error('Страница печати не содержит название компании (ALPHUTA)!');
        }
        if (!pdfHtml.includes('Дата экспорта:') || !pdfHtml.includes('Автор:')) {
            throw new Error('Страница печати не содержит метаданные (Дата экспорта, Автор)!');
        }
        if (!pdfHtml.includes('@media print')) {
            throw new Error('Страница печати не содержит стилей @media print!');
        }
        if (!pdfHtml.includes('print-btn') || !pdfHtml.includes('print-back-link')) {
            throw new Error('Кнопка «Напечатать / Сохранить как PDF» или ссылка «← Вернуться в Wiki» не найдены!');
        }
        if (!pdfHtml.includes('1 170 691 894') || !pdfHtml.includes('MS SQL 2019')) {
            throw new Error('Страница печати не содержит сохраненные параметры сервера!');
        }
        console.log('   ✓ Роут /api/export/pdf/1 отдает полноценный Printable View без падений Puppeteer!\n');

        // 4. Проверка экспорта в TXT (/api/export/txt/1) (Этап 2: Исправление экспорта TXT)
        console.log('[4/4] Проверка маршрута экспорта в TXT /api/export/txt/1...');
        const txtRes = await request('GET', '/api/export/txt/1');
        if (txtRes.statusCode !== 200) {
            throw new Error(`Ожидался статус 200 для /api/export/txt/1, получено: ${txtRes.statusCode}`);
        }
        const txtContentType = txtRes.headers['content-type'] || '';
        if (!txtContentType.includes('text/plain')) {
            throw new Error(`Ожидался Content-Type text/plain, получено: ${txtContentType}`);
        }
        const txtContentDisp = txtRes.headers['content-disposition'] || '';
        if (!txtContentDisp.includes('attachment') || !txtContentDisp.includes('.txt')) {
            throw new Error(`Некорректный заголовок Content-Disposition: ${txtContentDisp}`);
        }
        const txtBody = txtRes.body;
        if (!txtBody.includes('1 170 691 894') || !txtBody.includes('MS SQL 2019')) {
            throw new Error('TXT-файл не содержит ожидаемые параметры сервера!');
        }
        console.log(`   ✓ Content-Type: ${txtContentType}`);
        console.log(`   ✓ Content-Disposition: ${txtContentDisp}`);
        console.log(`   ✓ Тело файла содержит ${txtBody.length} байт текста`);
        console.log('   ✓ Роут /api/export/txt/1 работает безупречно!\n');

        console.log('====================================================');
        console.log('  🎉 ВСЕ ПРОВЕРКИ ЭТАПОВ 1 И 2 УСПЕШНО ПРОЙДЕНЫ!');
        console.log('====================================================');
        process.exit(0);

    } catch (err) {
        console.error('\n❌ ОШИБКА ТЕСТИРОВАНИЯ:', err.message);
        process.exit(1);
    }
}

runTests();
