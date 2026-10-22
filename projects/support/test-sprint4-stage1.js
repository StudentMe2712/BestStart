/**
 * test-sprint4-stage1.js
 * E2E & API Тестирование Спринта 4 (Этап 1):
 * - Отсутствие плашек PROD/DEV и кнопки копирования всех реквизитов
 * - Табы «Данные» и «Базы 1С» в карточке компании
 * - Переключение табов и сохранение состояния
 * - Таблица баз 1С с колонками: Информационная база, Описание, Дата, Действия
 * - Модальное окно и форма с полями: #dbName, #dbDescription, #dbDate
 * - Добавление, редактирование и удаление базы со свободным описанием и динамическим счетчиком #tabDbCount
 */

process.env.DB_STUB = 'true';
const TEST_PORT = 3295;
process.env.PORT = String(TEST_PORT);

const http = require('http');
const puppeteer = require('puppeteer');

async function runTests() {
    console.log('====================================================');
    console.log('  🧪 ТЕСТИРОВАНИЕ СПРИНТ 4: ЭТАП 1 (ТАБЫ И БАЗЫ 1С)');
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
                reqHeaders['Content-Type'] = typeof body === 'object' ? 'application/json' : 'application/x-www-form-urlencoded';
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
            if (body) {
                const sendData = typeof body === 'object' ? JSON.stringify(body) : body;
                req.write(sendData);
            }
            req.end();
        });
    }

    let browser = null;

    try {
        // [1/5] Авторизация через POST /login
        console.log('[1/5] Авторизация в системе через POST /login...');
        const loginRes = await request('POST', '/login', 'username=admin&password=admin');
        if (loginRes.statusCode !== 302 && loginRes.statusCode !== 200) {
            throw new Error(`Ошибка авторизации, статус: ${loginRes.statusCode}`);
        }
        console.log('   ✓ Успешная авторизация, получен session cookie\n');

        // [2/5] Проверка SSR HTML страницы /wiki/organization/1
        console.log('[2/5] Проверка структуры табов и таблицы на /wiki/organization/1...');
        const wikiRes = await request('GET', '/wiki/organization/1');
        if (wikiRes.statusCode !== 200) {
            throw new Error(`Ожидался статус 200 для /wiki/organization/1, получено: ${wikiRes.statusCode}`);
        }
        const html = wikiRes.body;

        // 2.1 Проверка отсутствия бейджей PROD и DEV в сайдбаре и шапке
        if (html.includes('wiki-badge-env') || html.includes('org-header-env-badge')) {
            throw new Error('Обнаружены классы бейджей окружения (wiki-badge-env / org-header-env-badge)!');
        }
        if (html.includes('wiki-badge-env--prod') || html.includes('wiki-badge-env--dev')) {
            throw new Error('Обнаружены модификаторы бейджей окружения (--prod / --dev)!');
        }
        if (/class="[^"]*badge[^"]*"[^>]*>\s*(?:PROD|DEV)\s*</i.test(html)) {
            throw new Error('Обнаружены плашки PROD или DEV!');
        }
        if (html.includes('copyAllBtn') || html.includes('Скопировать все реквизиты')) {
            throw new Error('Кнопка «Скопировать все реквизиты» не должна присутствовать!');
        }
        console.log('   ✓ Плашки PROD/DEV и кнопка «Скопировать все реквизиты» полностью отсутствуют');

        // 2.2 Проверка наличия табов
        if (!html.includes('id="companyTabsBar"')) {
            throw new Error('Контейнер табов #companyTabsBar не найден в wiki.ejs!');
        }
        if (!html.includes('id="tabBtnData"') || !html.includes('Данные')) {
            throw new Error('Таб 1 «Данные» (#tabBtnData) не найден в wiki.ejs!');
        }
        if (!html.includes('id="tabBtn1c"') || !html.includes('Базы 1С')) {
            throw new Error('Таб 2 «Базы 1С» (#tabBtn1c) не найден в wiki.ejs!');
        }
        if (!html.includes('id="tabDbCount"')) {
            throw new Error('Бейдж количества баз на табе (#tabDbCount) не найден!');
        }
        if (!html.includes('id="tabPaneData"') || !html.includes('id="tabPane1c"')) {
            throw new Error('Панели табов #tabPaneData и #tabPane1c не найдены!');
        }
        console.log('   ✓ Табы «Данные» и «Базы 1С» (#tabBtnData, #tabBtn1c, #tabDbCount) присутствуют');

        // 2.3 Проверка колонок таблицы
        if (!html.includes('Информационная база') || !html.includes('Описание') || !html.includes('Дата') || !html.includes('Действия')) {
            throw new Error('Колонки таблицы баз 1С (Информационная база, Описание, Дата, Действия) не соответствуют требованиям!');
        }
        console.log('   ✓ Таблица содержит требуемые колонки: Информационная база, Описание, Дата, Действия');

        // 2.4 Проверка формы добавления
        if (!html.includes('id="db1cModal"')) {
            throw new Error('Модальное окно #db1cModal не найдено!');
        }
        if (!html.includes('id="dbName"')) {
            throw new Error('Поле Информационная база (#dbName) не найдено!');
        }
        if (!html.includes('id="dbDescription"')) {
            throw new Error('Поле Описание (#dbDescription) не найдено!');
        }
        if (!html.includes('id="dbDate"')) {
            throw new Error('Поле Дата (#dbDate) не найдено!');
        }
        console.log('   ✓ Модальное окно содержит поля #dbName, #dbDescription, #dbDate\n');

        // [3/5] Проверка API CRUD со свободным полем description
        console.log('[3/5] Тестирование API баз 1С со свободным описанием description...');
        
        // 3.1 POST
        const postData = {
            name: 'Бухгалтерия (ЦБ Спецстрой)',
            description: '3.0.73.1 (БК), подсистема Строительство + модуль WMS',
            updated_at: '2026-10-22'
        };
        const createRes = await request('POST', '/api/organizations/1/databases', postData);
        if (createRes.statusCode !== 201) {
            throw new Error(`Ошибка создания базы 1С: ${createRes.statusCode} ${createRes.body}`);
        }
        const createdDb = JSON.parse(createRes.body);
        if (createdDb.name !== postData.name || createdDb.description !== postData.description) {
            throw new Error('Созданная база не сохранила description корректно!');
        }
        console.log(`   ✓ POST /api/organizations/1/databases успешно создал базу (ID: ${createdDb.id}) с произвольным описанием`);

        // 3.2 PUT
        const updateData = {
            name: 'Бухгалтерия (ЦБ Спецстрой Обновленная)',
            description: '3.0.74.2 (БК), расширение Налоги и Склад',
            updated_at: '2026-10-22'
        };
        const putRes = await request('PUT', `/api/databases/${createdDb.id}`, updateData);
        if (putRes.statusCode !== 200) {
            throw new Error(`Ошибка обновления базы 1С: ${putRes.statusCode} ${putRes.body}`);
        }
        console.log(`   ✓ PUT /api/databases/${createdDb.id} успешно обновил описание`);

        // 3.3 GET verify
        const getRes = await request('GET', '/api/organizations/1/databases');
        const dbList = JSON.parse(getRes.body);
        const found = dbList.find(d => d.id === createdDb.id);
        if (!found || found.description !== updateData.description) {
            throw new Error('GET не вернул обновленный description!');
        }
        console.log(`   ✓ GET /api/organizations/1/databases подтвердил сохранение description: "${found.description}"`);

        // 3.4 DELETE cleanup
        const delRes = await request('DELETE', `/api/databases/${createdDb.id}`);
        if (delRes.statusCode !== 200) {
            throw new Error(`Ошибка удаления базы 1С: ${delRes.statusCode}`);
        }
        console.log(`   ✓ DELETE /api/databases/${createdDb.id} успешно удалил тестовую базу\n`);

        // [4/5] E2E тест через Headless Puppeteer: переключение табов и интерактивность
        console.log('[4/5] E2E проверка переключения табов в браузере (Puppeteer)...');
        const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
        browser = await puppeteer.launch({
            headless: 'new',
            executablePath: chromePath,
            args: ['--no-sandbox', '--disable-setuid-sandbox']
        });
        const page = await browser.newPage();
        await page.setViewport({ width: 1280, height: 900 });

        // Авторизация
        await page.goto(`${BASE_URL}/login`, { waitUntil: 'networkidle0' });
        await page.type('#username', 'admin');
        await page.type('#password', 'admin');
        await Promise.all([
            page.waitForNavigation({ waitUntil: 'networkidle0' }),
            page.click('#submitBtn')
        ]);

        // Открытие карточки ALPHUTA
        await page.goto(`${BASE_URL}/wiki/organization/1`, { waitUntil: 'networkidle0' });

        // Проверяем начальное состояние: активен Таб 1 «Данные»
        const tabDataActive = await page.$eval('#tabBtnData', el => el.classList.contains('is-active'));
        const tab1cActive = await page.$eval('#tabBtn1c', el => el.classList.contains('is-active'));
        const paneDataVisible = await page.$eval('#tabPaneData', el => window.getComputedStyle(el).display !== 'none');
        const pane1cVisible = await page.$eval('#tabPane1c', el => window.getComputedStyle(el).display !== 'none');

        if (!tabDataActive || tab1cActive) {
            throw new Error('По умолчанию должен быть активен таб «Данные»!');
        }
        if (!paneDataVisible || pane1cVisible) {
            throw new Error('По умолчанию панель #tabPaneData должна быть видима, а #tabPane1c скрыта!');
        }
        console.log('   ✓ Начальное состояние: таб «Данные» активен, панель заметок отображается');

        // Переключаемся на Таб 2 «Базы 1С»
        await page.click('#tabBtn1c');
        await new Promise(r => setTimeout(r, 150));

        const tab1cActiveNow = await page.$eval('#tabBtn1c', el => el.classList.contains('is-active'));
        const pane1cVisibleNow = await page.$eval('#tabPane1c', el => window.getComputedStyle(el).display !== 'none');
        const paneDataVisibleNow = await page.$eval('#tabPaneData', el => window.getComputedStyle(el).display === 'none');
        const currentHash = await page.evaluate(() => window.location.hash);

        if (!tab1cActiveNow || !pane1cVisibleNow || !paneDataVisibleNow) {
            throw new Error('После клика по #tabBtn1c панель #tabPane1c должна стать видимой, а #tabPaneData скрытой!');
        }
        if (currentHash !== '#tab-1c') {
            throw new Error(`Хэш URL должен быть #tab-1c, текущий: ${currentHash}`);
        }
        console.log('   ✓ Клик по табу «Базы 1С» корректно переключил рабочую область и обновил хэш на #tab-1c');

        // Переключаемся обратно на Таб 1 «Данные»
        await page.click('#tabBtnData');
        await new Promise(r => setTimeout(r, 150));
        const backToData = await page.$eval('#tabBtnData', el => el.classList.contains('is-active'));
        const paneDataVisibleBack = await page.$eval('#tabPaneData', el => window.getComputedStyle(el).display !== 'none');
        if (!backToData || !paneDataVisibleBack) {
            throw new Error('Возврат на таб «Данные» не сработал!');
        }
        console.log('   ✓ Возврат на таб «Данные» работает штатно\n');

        // [5/5] E2E тест создания базы через модальное окно на Табе 2
        console.log('[5/5] E2E проверка добавления базы через модальное окно на Табе 2...');
        await page.click('#tabBtn1c');
        await new Promise(r => setTimeout(r, 150));

        const initialBadgeCount = await page.$eval('#tabDbCount', el => parseInt(el.textContent.trim(), 10));

        // Клик «+ Добавить базу»
        await page.click('#addDatabaseBtn');
        await new Promise(r => setTimeout(r, 150));

        const modalVisible = await page.$eval('#db1cModal', el => window.getComputedStyle(el).display !== 'none');
        if (!modalVisible) {
            throw new Error('Модальное окно #db1cModal не открылось по клику на #addDatabaseBtn!');
        }

        // Заполнение полей
        await page.type('#dbName', 'E2E Тестовая База (Строительство)');
        await page.type('#dbDescription', '3.0.73.1 (БК), подсистема Строительство (Тест)');
        
        // Нажимаем Сохранить
        await page.click('#saveDb1cBtn');
        await new Promise(r => setTimeout(r, 500));

        // Проверяем, что окно закрылось
        const modalClosed = await page.$eval('#db1cModal', el => window.getComputedStyle(el).display === 'none');
        if (!modalClosed) {
            throw new Error('Модальное окно не закрылось после сохранения!');
        }

        // Проверяем появление строки в таблице
        const tableText = await page.$eval('#db1cTableBody', el => el.innerText);
        if (!tableText.includes('E2E Тестовая База (Строительство)') || !tableText.includes('3.0.73.1 (БК), подсистема Строительство (Тест)')) {
            throw new Error('Новая база с описанием не появилась в таблице!');
        }
        console.log('   ✓ Новая база 1С с произвольным описанием успешно добавлена в таблицу');

        // Проверяем динамическое обновление счетчика на табе
        const newBadgeCount = await page.$eval('#tabDbCount', el => parseInt(el.textContent.trim(), 10));
        if (newBadgeCount !== initialBadgeCount + 1) {
            throw new Error(`Счетчик таба не обновился! Было: ${initialBadgeCount}, стало: ${newBadgeCount}`);
        }
        console.log(`   ✓ Счетчик баз на табе динамически увеличился: ${initialBadgeCount} -> ${newBadgeCount}`);

        // Проверяем всплывающий тост
        const toastExists = await page.$eval('.floating-toast-container', el => el.innerHTML.includes('База 1С успешно добавлена'));
        if (!toastExists) {
            console.log('   ℹ Тост уже скрылся или контейнер очищен (штатно)');
        } else {
            console.log('   ✓ Всплывающий тост об успешном добавлении отобразился');
        }

        console.log('\n====================================================');
        console.log('  🎉 ВСЕ ПРОВЕРКИ ЭТАПА 1 СПРИНТА 4 УСПЕШНО ПРОЙДЕНЫ!');
        console.log('====================================================');

    } catch (err) {
        console.error('\n❌ ОШИБКА ТЕСТИРОВАНИЯ ЭТАПА 1:', err.message);
        process.exitCode = 1;
    } finally {
        if (browser) await browser.close();
        if (serverInstance && serverInstance.close) {
            serverInstance.close();
        }
        setTimeout(() => process.exit(process.exitCode || 0), 200);
    }
}

runTests();
