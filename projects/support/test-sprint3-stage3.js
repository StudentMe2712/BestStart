process.env.DB_STUB = 'true';
const TEST_PORT = 3292;
process.env.PORT = String(TEST_PORT);

const http = require('http');

async function runTests() {
    console.log('====================================================');
    console.log('  🧪 ТЕСТИРОВАНИЕ СПРИНТ 3: ЭТАП 3');
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

    try {
        // 1. Авторизация
        console.log('[1/6] Авторизация в системе через POST /login...');
        const loginRes = await request('POST', '/login', 'username=admin&password=admin');
        if (loginRes.statusCode !== 302 && loginRes.statusCode !== 200) {
            throw new Error(`Ошибка авторизации, статус: ${loginRes.statusCode}`);
        }
        console.log('   ✓ Успешная авторизация, получен session cookie\n');

        // 2. Проверка страницы /wiki/organization/1 (ALPHUTA)
        console.log('[2/6] Проверка реестра баз 1С на странице /wiki/organization/1...');
        const wikiRes = await request('GET', '/wiki/organization/1');
        if (wikiRes.statusCode !== 200) {
            throw new Error(`Ожидался статус 200 для /wiki/organization/1, получено: ${wikiRes.statusCode}`);
        }
        const wikiHtml = wikiRes.body;

        if (!wikiHtml.includes('Информационные базы 1С')) {
            throw new Error('Секция «Информационные базы 1С» не найдена в wiki.ejs!');
        }
        if (!wikiHtml.includes('id="addDatabaseBtn"')) {
            throw new Error('Кнопка добавления базы #addDatabaseBtn не найдена!');
        }
        if (!wikiHtml.includes('Бухгалтерия для Казахстана (Прод)') || !wikiHtml.includes('3.0.73.1 (БК)')) {
            throw new Error('База 1С ALPHUTA «Бухгалтерия для Казахстана (Прод)» с релизом 3.0.73.1 (БК) не найдена!');
        }
        if (!wikiHtml.includes('Зарплата и управление персоналом') || !wikiHtml.includes('3.1.28.12 (ЗУП)')) {
            throw new Error('База 1С ALPHUTA «Зарплата и управление персоналом» с релизом 3.1.28.12 (ЗУП) не найдена!');
        }
        if (!wikiHtml.includes('db-release-badge')) {
            throw new Error('Класс бейджа релиза .db-release-badge не найден!');
        }
        if (!wikiHtml.includes('id="db1cModal"')) {
            throw new Error('Модальное окно #db1cModal не найдено в wiki.ejs!');
        }
        if (!wikiHtml.includes('/events')) {
            throw new Error('Ссылка на раздел «События» (/events) отсутствует в шапке topbar wiki.ejs!');
        }
        console.log('   ✓ Реестр баз 1С отображается в Wiki с бейджами и кнопкой добавления!\n');

        // 3. Тестирование API эндпоинтов для баз 1С
        console.log('[3/6] Проверка API баз 1С (GET, POST, PUT, DELETE)...');
        
        // 3.1 GET /api/organizations/1/databases
        const getBasesRes = await request('GET', '/api/organizations/1/databases');
        if (getBasesRes.statusCode !== 200) {
            throw new Error(`GET /api/organizations/1/databases вернул статус ${getBasesRes.statusCode}`);
        }
        const basesList = JSON.parse(getBasesRes.body);
        if (!Array.isArray(basesList) || basesList.length < 2) {
            throw new Error('Ожидалось минимум 2 базы у ALPHUTA');
        }
        console.log(`   ✓ GET /api/organizations/1/databases вернул ${basesList.length} баз`);

        // 3.2 POST /api/organizations/1/databases
        const newDbPayload = {
            name: 'Управление торговлей (Тест)',
            release: '2.0.42.1 (УТ)',
            updated_at: '2026-10-22'
        };
        const postBaseRes = await request('POST', '/api/organizations/1/databases', newDbPayload, {
            'Content-Type': 'application/json'
        });
        if (postBaseRes.statusCode !== 201) {
            throw new Error(`POST /api/organizations/1/databases вернул статус ${postBaseRes.statusCode}: ${postBaseRes.body}`);
        }
        const createdDb = JSON.parse(postBaseRes.body);
        if (!createdDb.id || createdDb.name !== newDbPayload.name) {
            throw new Error('Некорректный ответ создания базы 1С');
        }
        console.log(`   ✓ POST /api/organizations/1/databases создал базу ID: ${createdDb.id}`);

        // 3.3 PUT /api/databases/:id
        const updatePayload = {
            name: 'Управление торговлей (Обновлено)',
            release: '2.0.43.1 (УТ)',
            updated_at: '2026-10-22'
        };
        const putBaseRes = await request('PUT', `/api/databases/${createdDb.id}`, updatePayload, {
            'Content-Type': 'application/json'
        });
        if (putBaseRes.statusCode !== 200) {
            throw new Error(`PUT /api/databases/${createdDb.id} вернул статус ${putBaseRes.statusCode}`);
        }
        console.log('   ✓ PUT /api/databases/:id успешно обновил релиз и название');

        // 3.4 DELETE /api/databases/:id
        const deleteBaseRes = await request('DELETE', `/api/databases/${createdDb.id}`);
        if (deleteBaseRes.statusCode !== 200) {
            throw new Error(`DELETE /api/databases/${createdDb.id} вернул статус ${deleteBaseRes.statusCode}`);
        }
        console.log('   ✓ DELETE /api/databases/:id успешно удалил тестовую базу\n');

        // 4. Проверка страницы /events (Журнал релизов 1С)
        console.log('[4/6] Проверка страницы /events (Журнал релизов 1С)...');
        const eventsRes = await request('GET', '/events');
        if (eventsRes.statusCode !== 200) {
            throw new Error(`Ожидался статус 200 для /events, получено: ${eventsRes.statusCode}`);
        }
        const eventsHtml = eventsRes.body;

        if (!eventsHtml.includes('Журнал релизов 1С')) {
            throw new Error('Заголовок «Журнал релизов 1С» не найден на странице /events!');
        }
        if (!eventsHtml.includes('Хронологический регистр обновлений конфигураций клиентов')) {
            throw new Error('Подпись «Хронологический регистр обновлений конфигураций клиентов» не найдена!');
        }
        if (!eventsHtml.includes('id="orgFilter"')) {
            throw new Error('Селектор фильтра по организации #orgFilter не найден!');
        }
        if (!eventsHtml.includes('ALPHUTA') || !eventsHtml.includes('Antarium') || !eventsHtml.includes('ARG')) {
            throw new Error('Организации ALPHUTA / Antarium / ARG не найдены в журнале /events!');
        }
        if (!eventsHtml.includes('3.0.73.1 (БК)') || !eventsHtml.includes('2.4.14.8 (ERP)')) {
            throw new Error('Релизы 1С не найдены в таблице событий!');
        }
        if (eventsHtml.includes('Телеметрия инфраструктуры Zabbix') || eventsHtml.includes('SSL-сертификатов')) {
            throw new Error('Обнаружены посторонние элементы (Zabbix, SSL) в чистом регистре 1С!');
        }
        console.log('   ✓ Страница /events отдает чистый и строгий журнал релизов 1С!\n');

        // 5. Проверка включения баз 1С в Printable PDF (/api/export/pdf/1)
        console.log('[5/6] Проверка отображения баз 1С в /api/export/pdf/1...');
        const pdfRes = await request('GET', '/api/export/pdf/1');
        if (pdfRes.statusCode !== 200) {
            throw new Error(`Ожидался статус 200 для /api/export/pdf/1, получено: ${pdfRes.statusCode}`);
        }
        const pdfHtml = pdfRes.body;
        if (!pdfHtml.includes('Информационные базы 1С')) {
            throw new Error('Таблица «Информационные базы 1С» отсутствует в Printable PDF!');
        }
        if (!pdfHtml.includes('Бухгалтерия для Казахстана (Прод)') || !pdfHtml.includes('3.0.73.1 (БК)')) {
            throw new Error('Конфигурация 1С ALPHUTA отсутствует в Printable PDF!');
        }
        console.log('   ✓ Базы 1С корректно интегрированы в страницу печати PDF!\n');

        // 6. Проверка навигации на Главной странице (/)
        console.log('[6/6] Проверка навигации topbar и кнопки События на Главной (/)...');
        const homeRes = await request('GET', '/');
        if (homeRes.statusCode !== 200) {
            throw new Error(`Ожидался статус 200 для /, получено: ${homeRes.statusCode}`);
        }
        const homeHtml = homeRes.body;
        if (!homeHtml.includes('href="/events"')) {
            throw new Error('Ссылка href="/events" не найдена на главной странице!');
        }
        console.log('   ✓ Навигация на «События» (/events) доступна с главной страницы!\n');

        console.log('====================================================');
        console.log('  🎉 ВСЕ ПРОВЕРКИ ЭТАПА 3 УСПЕШНО ПРОЙДЕНЫ!');
        console.log('====================================================');
        process.exit(0);

    } catch (err) {
        console.error('\n❌ ОШИБКА ТЕСТИРОВАНИЯ:', err.message);
        process.exit(1);
    }
}

runTests();
