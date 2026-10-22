/**
 * test-sprint4-stage2.js
 * E2E & Интеграционное тестирование Спринта 4 (Этапы 2, 3 и 4):
 * 1. Главная страница (Дашборд):
 *    - Верхняя плашка с приветствием, логином текущего пользователя, заголовком «База знаний ИТС-Ком» и датой.
 *    - Две сбалансированные карточки без списков буллетов:
 *      * «Wiki»: строго 1 предложение: «Технические параметры серверов, доступы и конфигурации.»
 *      * «События»: строго 1 предложение: «Иерархический журнал и реестр информационных баз 1С.»
 * 2. Страница входа (/login):
 *    - Отсутствие logo.png, текстовый заголовок «ИТС-Ком» и форма авторизации.
 * 3. Иерархический журнал баз в разделе «События» (/events):
 *    - SSR-структура таблицы и аккордеона.
 *    - Верхняя панель управления: #btnExpandAll, #btnCollapseAll, живой поиск #eventsSearchInput, бейдж «Всего: X компаний, Y баз 1С».
 *    - Строки 1-го уровня (Организация): шеврон (▾/▸), иконка 🏢, жирный заголовок со ссылкой в Wiki, бейдж количества («2 базы»).
 *    - Строки 2-го уровня (Базы 1С): отступ дерева └─, колонки: Организация, Информационная база, Описание, Дата.
 *    - E2E Puppeteer:
 *      * Клик по строке группы сворачивает / разворачивает ее базы и меняет шеврон (▾ -> ▸ -> ▾).
 *      * Кнопка «Свернуть все» сворачивает все группы.
 *      * Кнопка «Развернуть все» раскрывает все группы.
 *      * Мгновенный живой поиск фильтрует дерево по названию компании, базы и описанию.
 * 4. Экспорт PDF и TXT:
 *    - /api/export/pdf/:id — чистый Printable View с @media print, window.print(), данными и таблицей баз 1С с описанием.
 *    - /api/export/txt/:id — чистый TXT со свободными заметками и перечнем информационных баз.
 */

process.env.DB_STUB = 'true';
const TEST_PORT = 3296;
process.env.PORT = String(TEST_PORT);

const http = require('http');
const puppeteer = require('puppeteer');

async function runTests() {
    console.log('======================================================================');
    console.log('  🧪 ТЕСТИРОВАНИЕ СПРИНТ 4: ЭТАПЫ 2, 3, 4 (ИЕРАРХИЯ, ДАШБОРД, ЭКСПОРТ)');
    console.log('======================================================================\n');

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
        // -------------------------------------------------------------
        // [1/6] Авторизация в системе через POST /login
        // -------------------------------------------------------------
        console.log('[1/6] Авторизация в системе через POST /login...');
        const loginRes = await request('POST', '/login', 'username=admin&password=admin');
        if (loginRes.statusCode !== 302 && loginRes.statusCode !== 200) {
            throw new Error(`Ошибка авторизации, статус: ${loginRes.statusCode}`);
        }
        console.log('   ✓ Авторизация успешна, cookie сессии получен\n');

        // -------------------------------------------------------------
        // [2/6] Проверка Главной страницы (Дашборд) и формы входа
        // -------------------------------------------------------------
        console.log('[2/6] Проверка лаконичного Дашборда (/) и экрана логина (/login)...');
        
        // 2.1 Форма входа (без сессии)
        const unauthLoginRes = await new Promise((resolve, reject) => {
            const req = http.request(new URL('/login', BASE_URL), { method: 'GET' }, (res) => {
                let data = '';
                res.on('data', chunk => data += chunk);
                res.on('end', () => resolve({ statusCode: res.statusCode, body: data }));
            });
            req.on('error', reject);
            req.end();
        });
        const loginHtml = unauthLoginRes.body;
        if (loginHtml.includes('logo.png')) {
            throw new Error('На странице входа обнаружена картинка logo.png!');
        }
        if (!loginHtml.includes('ИТС-Ком') || !loginHtml.includes('id="username"') || !loginHtml.includes('id="password"')) {
            throw new Error('Страница входа не содержит текстовый заголовок «ИТС-Ком» или рабочую форму!');
        }
        console.log('   ✓ Страница /login: без логотипа-картинки, текстовый заголовок «ИТС-Ком», рабочая форма');

        // 2.2 Главная страница
        const homeRes = await request('GET', '/');
        if (homeRes.statusCode !== 200) {
            throw new Error(`Ожидался статус 200 для /, получено: ${homeRes.statusCode}`);
        }
        const homeHtml = homeRes.body;

        // Плашка героя
        if (!homeHtml.includes('С возвращением,') || !homeHtml.includes('admin')) {
            throw new Error('Верхняя плашка не содержит приветствие с логином пользователя «admin»!');
        }
        if (!homeHtml.includes('База знаний ИТС-Ком')) {
            throw new Error('Верхняя плашка не содержит заголовок «База знаний ИТС-Ком»!');
        }
        if (!homeHtml.includes('Все узлы в штатном режиме')) {
            throw new Error('Верхняя плашка не содержит статус телеметрии!');
        }

        // Карточки
        if (!homeHtml.includes('Технические параметры серверов, доступы и конфигурации.')) {
            throw new Error('Описание карточки Wiki не соответствует регламенту!');
        }
        if (!homeHtml.includes('Иерархический журнал и реестр информационных баз 1С.')) {
            throw new Error('Описание карточки «События» не соответствует регламенту («Иерархический журнал и реестр информационных баз 1С.»)!');
        }
        if (homeHtml.includes('<ul') && homeHtml.includes('command-widget')) {
            throw new Error('Карточки главной страницы содержат списки/буллеты!');
        }
        console.log('   ✓ Дашборд: приветствие «С возвращением, admin», заголовок «База знаний ИТС-Ком»');
        console.log('   ✓ Карточки Wiki и События строго по 1 предложению, без списков буллетов\n');

        // -------------------------------------------------------------
        // [3/6] Проверка роута /events (SSR структура)
        // -------------------------------------------------------------
        console.log('[3/6] Проверка SSR структуры журнала «События» (/events)...');
        const eventsRes = await request('GET', '/events');
        if (eventsRes.statusCode !== 200) {
            throw new Error(`Ожидался статус 200 для /events, получено: ${eventsRes.statusCode}`);
        }
        const eventsHtml = eventsRes.body;

        // Элементы верхней панели управления
        if (!eventsHtml.includes('id="btnExpandAll"') || !eventsHtml.includes('id="btnCollapseAll"')) {
            throw new Error('Кнопки быстрого управления #btnExpandAll / #btnCollapseAll не найдены!');
        }
        if (!eventsHtml.includes('id="eventsSearchInput"')) {
            throw new Error('Поле мгновенного живого поиска #eventsSearchInput не найдено!');
        }
        if (!eventsHtml.includes('id="eventsStatsBadge"') || !eventsHtml.includes('компаний') || !eventsHtml.includes('баз 1С')) {
            throw new Error('Информационный бейдж «Всего: X компаний, Y баз 1С» не найден!');
        }

        // Колонки таблицы
        if (!eventsHtml.includes('Организация') || !eventsHtml.includes('Информационная база') || !eventsHtml.includes('Описание') || !eventsHtml.includes('Дата')) {
            throw new Error('Заголовки колонок таблицы не соответствуют спецификации (Организация, Информационная база, Описание, Дата)!');
        }

        // Дерево и строки
        if (!eventsHtml.includes('events-group-row') || !eventsHtml.includes('events-chevron')) {
            throw new Error('Строки групп 1-го уровня .events-group-row или шевроны не найдены!');
        }
        if (!eventsHtml.includes('tree-indent') || !eventsHtml.includes('└─')) {
            throw new Error('Иерархический отступ дерева tree-indent с линией └─ не найден!');
        }
        if (!eventsHtml.includes('ALPHUTA') || !eventsHtml.includes('Бухгалтерия для Казахстана (Прод)')) {
            throw new Error('Данные компаний или баз 1С не найдены в SSR шаблоне /events!');
        }
        console.log('   ✓ Верхняя панель: #btnExpandAll, #btnCollapseAll, #eventsSearchInput, бейдж статистики');
        console.log('   ✓ Таблица содержит требуемые 4 колонки: Организация, Информационная база, Описание, Дата');
        console.log('   ✓ Родительские строки групп (Организация) и дочерние строки (Базы 1С с отступом └─) сформированы\n');

        // -------------------------------------------------------------
        // [4/6] E2E в браузере Puppeteer: Древовидная интерактивность
        // -------------------------------------------------------------
        console.log('[4/6] E2E тестирование в Puppeteer (раскрытие/сворачивание, поиск, кнопки)...');
        const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
        browser = await puppeteer.launch({
            headless: 'new',
            executablePath: chromePath,
            args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-dev-shm-usage']
        });
        const page = await browser.newPage();
        await page.setViewport({ width: 1280, height: 800 });

        // Передача session cookie в браузер
        const cookiePair = sessionCookie.split(';')[0].split('=');
        await page.setCookie({
            name: cookiePair[0].trim(),
            value: cookiePair[1].trim(),
            domain: 'localhost',
            path: '/'
        });

        await page.goto(`${BASE_URL}/events`, { waitUntil: 'domcontentloaded' });

        // 4.1 Проверка состояния по умолчанию: группы развернуты
        const initialGroupState = await page.evaluate(() => {
            const group = document.querySelector('.events-group-row[data-name="ALPHUTA"]');
            const chevron = group.querySelector('.events-chevron');
            const childRows = Array.from(document.querySelectorAll('.events-child-row[data-parent-org-id="' + group.getAttribute('data-org-id') + '"]'));
            const countBadge = group.querySelector('.events-count-badge');
            return {
                isExpanded: group.classList.contains('is-expanded'),
                chevronText: chevron.textContent.trim(),
                visibleChildrenCount: childRows.filter(r => r.style.display !== 'none').length,
                countBadgeText: countBadge ? countBadge.textContent.trim() : ''
            };
        });
        if (!initialGroupState.isExpanded || initialGroupState.chevronText !== '▾') {
            throw new Error(`Ожидалось, что группа ALPHUTA развернута (▾), получено: ${initialGroupState.chevronText}`);
        }
        if (initialGroupState.visibleChildrenCount < 2) {
            throw new Error(`Ожидалось минимум 2 видимые дочерние базы у ALPHUTA, получено: ${initialGroupState.visibleChildrenCount}`);
        }
        if (!initialGroupState.countBadgeText.includes('баз')) {
            throw new Error(`Бейдж количества баз некорректен: "${initialGroupState.countBadgeText}"`);
        }
        console.log(`   ✓ По умолчанию группа ALPHUTA развернута (шеврон ▾, бейдж: "${initialGroupState.countBadgeText}", видимых баз: ${initialGroupState.visibleChildrenCount})`);

        // 4.2 Проверка клика по строке группы: сворачивание
        await page.click('.events-group-row[data-name="ALPHUTA"]');
        await new Promise(r => setTimeout(r, 100));

        const collapsedState = await page.evaluate(() => {
            const group = document.querySelector('.events-group-row[data-name="ALPHUTA"]');
            const chevron = group.querySelector('.events-chevron');
            const childRows = Array.from(document.querySelectorAll('.events-child-row[data-parent-org-id="' + group.getAttribute('data-org-id') + '"]'));
            return {
                isCollapsed: group.classList.contains('is-collapsed'),
                chevronText: chevron.textContent.trim(),
                visibleChildrenCount: childRows.filter(r => r.style.display !== 'none').length
            };
        });
        if (!collapsedState.isCollapsed || collapsedState.chevronText !== '▸') {
            throw new Error(`После клика ожидался свернутый шеврон ▸, получено: ${collapsedState.chevronText}`);
        }
        if (collapsedState.visibleChildrenCount !== 0) {
            throw new Error(`После сворачивания дочерние строки не скрыты! Видимых: ${collapsedState.visibleChildrenCount}`);
        }
        console.log('   ✓ Клик по строке ALPHUTA успешно свернул базы компании (шеврон ▸, дочерние строки скрыты)');

        // 4.3 Повторный клик: разворачивание обратно
        await page.click('.events-group-row[data-name="ALPHUTA"]');
        await new Promise(r => setTimeout(r, 100));
        const reExpandedState = await page.evaluate(() => {
            const group = document.querySelector('.events-group-row[data-name="ALPHUTA"]');
            const chevron = group.querySelector('.events-chevron');
            const childRows = Array.from(document.querySelectorAll('.events-child-row[data-parent-org-id="' + group.getAttribute('data-org-id') + '"]'));
            return {
                isExpanded: group.classList.contains('is-expanded'),
                chevronText: chevron.textContent.trim(),
                visibleChildrenCount: childRows.filter(r => r.style.display !== 'none').length
            };
        });
        if (!reExpandedState.isExpanded || reExpandedState.chevronText !== '▾' || reExpandedState.visibleChildrenCount < 2) {
            throw new Error('Повторный клик не восстановил раскрытое состояние группы ALPHUTA!');
        }
        console.log('   ✓ Повторный клик успешно вернул группу в развернутое состояние (шеврон ▾)');

        // 4.4 Кнопка «Свернуть все» (#btnCollapseAll)
        await page.click('#btnCollapseAll');
        await new Promise(r => setTimeout(r, 100));
        const allCollapsed = await page.evaluate(() => {
            const groups = Array.from(document.querySelectorAll('.events-group-row'));
            const children = Array.from(document.querySelectorAll('.events-child-row'));
            const allHaveCollapsedClass = groups.every(g => g.classList.contains('is-collapsed'));
            const allChevronsRight = groups.every(g => g.querySelector('.events-chevron').textContent.trim() === '▸');
            const anyChildVisible = children.some(c => c.style.display !== 'none');
            return allHaveCollapsedClass && allChevronsRight && !anyChildVisible;
        });
        if (!allCollapsed) {
            throw new Error('Кнопка «Свернуть все» не перевела все группы в свернутое состояние!');
        }
        console.log('   ✓ Кнопка «Свернуть все» (#btnCollapseAll) свернула абсолютно все группы компании');

        // 4.5 Кнопка «Развернуть все» (#btnExpandAll)
        await page.click('#btnExpandAll');
        await new Promise(r => setTimeout(r, 100));
        const allExpanded = await page.evaluate(() => {
            const groups = Array.from(document.querySelectorAll('.events-group-row'));
            const allHaveExpandedClass = groups.every(g => g.classList.contains('is-expanded'));
            const allChevronsDown = groups.every(g => g.querySelector('.events-chevron').textContent.trim() === '▾');
            return allHaveExpandedClass && allChevronsDown;
        });
        if (!allExpanded) {
            throw new Error('Кнопка «Развернуть все» не раскрыла все группы!');
        }
        console.log('   ✓ Кнопка «Развернуть все» (#btnExpandAll) успешно раскрыла все группы');

        // 4.6 Мгновенный живой поиск (#eventsSearchInput)
        console.log('   - Тестирование живого поиска...');
        
        // Поиск по описанию/релизу «Строительство»
        await page.type('#eventsSearchInput', 'Строительство');
        await new Promise(r => setTimeout(r, 150));
        const searchResult1 = await page.evaluate(() => {
            const visibleGroups = Array.from(document.querySelectorAll('.events-group-row')).filter(g => g.style.display !== 'none');
            const visibleChildren = Array.from(document.querySelectorAll('.events-child-row')).filter(c => c.style.display !== 'none');
            return {
                groupNames: visibleGroups.map(g => g.getAttribute('data-name')),
                childBases: visibleChildren.map(c => c.getAttribute('data-db-name'))
            };
        });
        if (!searchResult1.groupNames.includes('ALPHUTA') || !searchResult1.childBases.some(b => b && b.includes('Бухгалтерия'))) {
            throw new Error(`Живой поиск по «Строительство» вернул неверные результаты: ${JSON.stringify(searchResult1)}`);
        }
        console.log('   ✓ Поиск по тексту описания («Строительство») вывел целевую базу ALPHUTA');

        // Очистка поиска и поиск по названию базы «ERP»
        await page.evaluate(() => document.getElementById('eventsSearchInput').value = '');
        await page.type('#eventsSearchInput', 'ERP');
        await new Promise(r => setTimeout(r, 150));
        const searchResult2 = await page.evaluate(() => {
            const visibleGroups = Array.from(document.querySelectorAll('.events-group-row')).filter(g => g.style.display !== 'none');
            const visibleChildren = Array.from(document.querySelectorAll('.events-child-row')).filter(c => c.style.display !== 'none');
            return {
                groupNames: visibleGroups.map(g => g.getAttribute('data-name')),
                childBases: visibleChildren.map(c => c.getAttribute('data-db-name'))
            };
        });
        if (!searchResult2.groupNames.includes('Antarium') || !searchResult2.childBases.some(b => b && b.includes('ERP'))) {
            throw new Error(`Живой поиск по «ERP» вернул неверные результаты: ${JSON.stringify(searchResult2)}`);
        }
        console.log('   ✓ Поиск по наименованию базы («ERP») вывел компанию Antarium');

        // Очистка поиска
        await page.evaluate(() => {
            const input = document.getElementById('eventsSearchInput');
            input.value = '';
            input.dispatchEvent(new Event('input'));
        });
        await new Promise(r => setTimeout(r, 100));

        await browser.close();
        browser = null;
        console.log('   ✓ E2E интерактивность дерева значений 1С работает безупречно!\n');

        // -------------------------------------------------------------
        // [5/6] Проверка экспорта Printable PDF (/api/export/pdf/:id)
        // -------------------------------------------------------------
        console.log('[5/6] Проверка роута экспорта Printable PDF /api/export/pdf/1...');
        const pdfRes = await request('GET', '/api/export/pdf/1');
        if (pdfRes.statusCode !== 200) {
            throw new Error(`Ожидался статус 200 для /api/export/pdf/1, получено: ${pdfRes.statusCode}`);
        }
        const pdfHtml = pdfRes.body;
        if (!pdfHtml.includes('@media print') || !pdfHtml.includes('window.print()')) {
            throw new Error('Страница экспорта PDF не содержит стили @media print или скрипт автопечати window.print()!');
        }
        if (!pdfHtml.includes('Параметры и заметки сервера') || !pdfHtml.includes('Информационные базы 1С')) {
            throw new Error('Печатный вид не содержит обе секции (Данные и Базы 1С)!');
        }
        if (!pdfHtml.includes('Информационная база') || !pdfHtml.includes('Описание') || !pdfHtml.includes('Дата обновления')) {
            throw new Error('Таблица баз 1С в PDF не содержит обновленные колонки «Информационная база», «Описание», «Дата обновления»!');
        }
        if (!pdfHtml.includes('Бухгалтерия для Казахстана (Прод)') || !pdfHtml.includes('3.0.73.1 (БК)')) {
            throw new Error('Данные баз 1С ALPHUTA отсутствуют в PDF!');
        }
        console.log('   ✓ Роут /api/export/pdf/1: чистый Printable View, @media print, авто-вызов window.print()');
        console.log('   ✓ Включает оба таба («Данные» и таблицу «Базы 1С» с описанием)\n');

        // -------------------------------------------------------------
        // [6/6] Проверка экспорта чистого TXT (/api/export/txt/:id)
        // -------------------------------------------------------------
        console.log('[6/6] Проверка роута выгрузки TXT /api/export/txt/1...');
        const txtRes = await request('GET', '/api/export/txt/1');
        if (txtRes.statusCode !== 200) {
            throw new Error(`Ожидался статус 200 для /api/export/txt/1, получено: ${txtRes.statusCode}`);
        }
        const txtContentType = txtRes.headers['content-type'] || '';
        if (!txtContentType.includes('text/plain')) {
            throw new Error(`Ожидался Content-Type text/plain, получено: ${txtContentType}`);
        }
        const txtBody = txtRes.body;
        if (!txtBody.includes('ИНФОРМАЦИОННЫЕ БАЗЫ 1С:') || !txtBody.includes('Бухгалтерия для Казахстана (Прод)')) {
            throw new Error('TXT выгрузка не содержит структурированный перечень баз 1С!');
        }
        if (!txtBody.includes('Описание:') || !txtBody.includes('3.0.73.1 (БК)')) {
            throw new Error('TXT выгрузка не содержит описания баз!');
        }
        console.log(`   ✓ Content-Type: ${txtContentType}`);
        console.log('   ✓ TXT выгрузка включает параметры сервера и структурированный перечень баз 1С\n');

        console.log('======================================================================');
        console.log('  🎉 ВСЕ ПРОВЕРКИ ЭТАПОВ 2, 3 И 4 СПРИНТА 4 УСПЕШНО ПРОЙДЕНЫ!');
        console.log('======================================================================');
        process.exit(0);

    } catch (err) {
        console.error('\n❌ ОШИБКА ТЕСТИРОВАНИЯ:', err.message);
        if (browser) {
            try { await browser.close(); } catch (e) {}
        }
        process.exit(1);
    }
}

runTests();
