/**
 * test-sprint5-stage2.js
 * E2E & Интеграционное тестирование Спринта 5 (Этап 2 — Строгое 1С-дерево в «Событиях»):
 * 1. Отсутствие верхнего хаоса:
 *    - Нет кнопок «Развернуть все» (#btnExpandAll) и «Свернуть все» (#btnCollapseAll).
 *    - Нет живого поиска (#eventsSearchInput).
 *    - Нет статистического бейджа (#eventsStatsBadge).
 *    - Нет выпадающего селектора (#orgFilter).
 *    - Заголовок h1 «Журнал релизов 1С», подзаголовок «Реестр информационных баз организаций-клиентов».
 * 2. Отсутствие иконки здания 🏢 перед названиями компаний.
 * 3. Состояние по умолчанию:
 *    - Абсолютно все группы организаций свернуты (.is-collapsed).
 *    - Все дочерние строки баз скрыты (display: none).
 * 4. Квадратный 1С-узел переключения ([+] / [-]):
 *    - Клик по организации с базами (ALPHUTA): [+] плавно меняется на [-], дочерние базы отображаются.
 *    - Повторный клик: [-] меняется обратно на [+], дочерние базы скрываются.
 * 5. Организации с 0 баз:
 *    - Плюсик [+] отсутствует (вместо него пустой отступ .tree-node-1c-spacer).
 *    - Серые заглушки («Базы 1С еще не привязаны...») полностью отсутствуют в DOM.
 *    - Отображается компактный бейдж «0 баз».
 */

process.env.DB_STUB = 'true';
const TEST_PORT = 3297;
process.env.PORT = String(TEST_PORT);

const fs = require('fs');
const http = require('http');
const puppeteer = require('puppeteer');

async function runTests() {
    console.log('======================================================================');
    console.log('  🧪 ТЕСТИРОВАНИЕ СПРИНТ 5: ЭТАП 2 (СТРОГОЕ 1С-ДЕРЕВО В «СОБЫТИЯХ»)');
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
        // [1/5] Авторизация в системе
        // -------------------------------------------------------------
        console.log('[1/5] Авторизация в системе через POST /login...');
        const loginRes = await request('POST', '/login', 'username=admin&password=admin');
        if (loginRes.statusCode !== 302 && loginRes.statusCode !== 200) {
            throw new Error(`Ошибка авторизации, статус: ${loginRes.statusCode}`);
        }
        console.log('   ✓ Авторизация успешна, cookie сессии получен\n');

        // -------------------------------------------------------------
        // [2/5] SSR-проверка разметки /events (Минимализм шапки, отсутствие хаоса)
        // -------------------------------------------------------------
        console.log('[2/5] SSR-проверка HTML страницы /events...');
        const eventsRes = await request('GET', '/events');
        if (eventsRes.statusCode !== 200) {
            throw new Error(`Ожидался статус 200 для /events, получено: ${eventsRes.statusCode}`);
        }
        const eventsHtml = eventsRes.body;

        // 2.1 Проверка заголовков
        if (!eventsHtml.includes('Журнал релизов 1С')) {
            throw new Error('Заголовок раздела «Журнал релизов 1С» не найден!');
        }
        if (!eventsHtml.includes('Реестр информационных баз организаций-клиентов')) {
            throw new Error('Подзаголовок «Реестр информационных баз организаций-клиентов» не найден!');
        }
        console.log('   ✓ Заголовок «Журнал релизов 1С» и подзаголовок «Реестр информационных баз организаций-клиентов» присутствуют');

        // 2.2 Проверка отсутствия верхнего хаоса
        if (eventsHtml.includes('id="btnExpandAll"') || eventsHtml.includes('id="btnCollapseAll"')) {
            throw new Error('Кнопки «Развернуть все» / «Свернуть все» все еще присутствуют на странице!');
        }
        if (eventsHtml.includes('id="eventsSearchInput"')) {
            throw new Error('Поле живого поиска #eventsSearchInput все еще присутствует на странице!');
        }
        if (eventsHtml.includes('id="eventsStatsBadge"')) {
            throw new Error('Статистический бейдж #eventsStatsBadge все еще присутствует на странице!');
        }
        if (eventsHtml.includes('id="orgFilter"')) {
            throw new Error('Селектор #orgFilter все еще присутствует на странице!');
        }
        console.log('   ✓ Полный верхний хаос удален: нет кнопок развертывания, поиска, бейджей статистики и селектора');

        // 2.3 Проверка отсутствия иконки 🏢
        if (eventsHtml.includes('🏢')) {
            throw new Error('Иконка здания 🏢 обнаружена в разметке событий!');
        }
        console.log('   ✓ Иконка здания 🏢 отсутствует перед названиями компаний');

        // 2.4 Проверка отсутствия серых строк «Базы 1С еще не привязаны...»
        if (eventsHtml.includes('Базы 1С еще не привязаны к этой организации')) {
            throw new Error('Обнаружены серые заглушки «Базы 1С еще не привязаны...»!');
        }
        console.log('   ✓ Заглушки отсутствующих баз не выводятся в DOM');

        // 2.5 Проверка колонок таблицы
        if (!eventsHtml.includes('Организация') || !eventsHtml.includes('Информационная база') || !eventsHtml.includes('Описание') || !eventsHtml.includes('Дата')) {
            throw new Error('Колонки Организация / Информационная база / Описание / Дата не найдены в таблице!');
        }
        console.log('   ✓ Колонки таблицы: Организация, Информационная база, Описание, Дата\n');

        // -------------------------------------------------------------
        // [3/5] E2E в браузере Puppeteer: Начальное свернутое состояние
        // -------------------------------------------------------------
        console.log('[3/5] E2E Puppeteer: Проверка начального состояния дерева...');
        const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
        browser = await puppeteer.launch({
            headless: 'new',
            executablePath: fs.existsSync(chromePath) ? chromePath : undefined,
            args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-dev-shm-usage']
        });
        const page = await browser.newPage();
        await page.setViewport({ width: 1280, height: 800 });

        const cookiePair = sessionCookie.split(';')[0].split('=');
        await page.setCookie({
            name: cookiePair[0].trim(),
            value: cookiePair[1].trim(),
            domain: 'localhost',
            path: '/'
        });

        await page.goto(`${BASE_URL}/events`, { waitUntil: 'domcontentloaded' });

        const initialTreeState = await page.evaluate(() => {
            const groupRows = Array.from(document.querySelectorAll('.events-group-row'));
            const childRows = Array.from(document.querySelectorAll('.events-child-row'));
            const allCollapsed = groupRows.every(r => r.classList.contains('is-collapsed') || r.classList.contains('is-empty'));
            const allChildrenHidden = childRows.every(r => r.style.display === 'none' || window.getComputedStyle(r).display === 'none');

            // ALPHUTA (has databases)
            const alphutaRow = document.querySelector('.events-group-row[data-name="ALPHUTA"]');
            const alphutaToggle = alphutaRow ? alphutaRow.querySelector('.tree-node-1c-toggle') : null;

            return {
                totalGroups: groupRows.length,
                totalChildren: childRows.length,
                allCollapsed,
                allChildrenHidden,
                alphutaHasToggle: !!alphutaToggle,
                alphutaToggleText: alphutaToggle ? alphutaToggle.textContent.trim() : null
            };
        });

        if (!initialTreeState.allCollapsed) {
            throw new Error('Не все организации свернуты по умолчанию!');
        }
        if (!initialTreeState.allChildrenHidden) {
            throw new Error('Некоторые дочерние строки баз видны по умолчанию!');
        }
        if (!initialTreeState.alphutaHasToggle || initialTreeState.alphutaToggleText !== '[+]') {
            throw new Error(`У ALPHUTA должен быть плюсик [+], получено: "${initialTreeState.alphutaToggleText}"`);
        }
        console.log(`   ✓ Все ${initialTreeState.totalGroups} организаций по умолчанию свернуты, ${initialTreeState.totalChildren} строк баз скрыты`);
        console.log('   ✓ У организации ALPHUTA отображается квадратный 1С-плюсик [+]\n');

        // -------------------------------------------------------------
        // [4/5] E2E: Раскрытие и сворачивание узла 1С ([+] <-> [-])
        // -------------------------------------------------------------
        console.log('[4/5] E2E Puppeteer: Клик по организации с базами (ALPHUTA)...');

        // Клик 1: Раскрытие
        await page.click('.events-group-row[data-name="ALPHUTA"]');
        await new Promise(r => setTimeout(r, 100));

        const expandedState = await page.evaluate(() => {
            const group = document.querySelector('.events-group-row[data-name="ALPHUTA"]');
            const toggle = group.querySelector('.tree-node-1c-toggle');
            const childRows = Array.from(document.querySelectorAll('.events-child-row[data-parent-org-id="' + group.getAttribute('data-org-id') + '"]'));
            return {
                isExpanded: group.classList.contains('is-expanded'),
                toggleText: toggle ? toggle.textContent.trim() : null,
                visibleChildrenCount: childRows.filter(r => r.style.display !== 'none').length
            };
        });

        if (!expandedState.isExpanded) {
            throw new Error('ALPHUTA не получила класс .is-expanded после клика!');
        }
        if (expandedState.toggleText !== '[-]') {
            throw new Error(`Плюсик должен был смениться на [-], получено: "${expandedState.toggleText}"`);
        }
        if (expandedState.visibleChildrenCount === 0) {
            throw new Error('Строки баз ALPHUTA не отобразились после раскрытия!');
        }
        console.log(`   ✓ ALPHUTA раскрыта: значок сменился на [-], отображаются ${expandedState.visibleChildrenCount} баз 1С`);

        // Клик 2: Сворачивание обратно
        await page.click('.events-group-row[data-name="ALPHUTA"]');
        await new Promise(r => setTimeout(r, 100));

        const reCollapsedState = await page.evaluate(() => {
            const group = document.querySelector('.events-group-row[data-name="ALPHUTA"]');
            const toggle = group.querySelector('.tree-node-1c-toggle');
            const childRows = Array.from(document.querySelectorAll('.events-child-row[data-parent-org-id="' + group.getAttribute('data-org-id') + '"]'));
            return {
                isCollapsed: group.classList.contains('is-collapsed'),
                toggleText: toggle ? toggle.textContent.trim() : null,
                visibleChildrenCount: childRows.filter(r => r.style.display !== 'none').length
            };
        });

        if (!reCollapsedState.isCollapsed) {
            throw new Error('ALPHUTA не получила класс .is-collapsed после повторного клика!');
        }
        if (reCollapsedState.toggleText !== '[+]') {
            throw new Error(`Значок должен был вернуться в [+], получено: "${reCollapsedState.toggleText}"`);
        }
        if (reCollapsedState.visibleChildrenCount !== 0) {
            throw new Error('Строки баз ALPHUTA не скрылись после сворачивания!');
        }
        console.log('   ✓ ALPHUTA успешно свернута: значок вернулся в [+], строки баз скрыты\n');

        // -------------------------------------------------------------
        // [5/5] E2E: Проверка организации с 0 баз
        // -------------------------------------------------------------
        console.log('[5/5] E2E Puppeteer: Проверка организации с 0 баз (Continental Logistics)...');
        const emptyOrgState = await page.evaluate(() => {
            const org = document.querySelector('.events-group-row[data-name="Continental Logistics"]');
            if (!org) return null;
            const toggle = org.querySelector('.tree-node-1c-toggle');
            const spacer = org.querySelector('.tree-node-1c-spacer');
            const countBadge = org.querySelector('.events-count-badge');
            const orgId = org.getAttribute('data-org-id');
            const childRows = document.querySelectorAll('.events-child-row[data-parent-org-id="' + orgId + '"]');

            return {
                hasToggle: !!toggle,
                hasSpacer: !!spacer,
                badgeText: countBadge ? countBadge.textContent.trim() : null,
                childCount: childRows.length,
                hasEmptyClass: org.classList.contains('is-empty')
            };
        });

        if (!emptyOrgState) {
            throw new Error('Организация «Continental Logistics» не найдена в дереве!');
        }
        if (emptyOrgState.hasToggle) {
            throw new Error('У организации с 0 баз не должно быть плюсика [+]!');
        }
        if (!emptyOrgState.hasSpacer) {
            throw new Error('У организации с 0 баз должен быть отступ .tree-node-1c-spacer!');
        }
        if (emptyOrgState.badgeText !== '0 баз') {
            throw new Error(`Ожидался бейдж «0 баз», получено: "${emptyOrgState.badgeText}"`);
        }
        if (emptyOrgState.childCount !== 0) {
            throw new Error('У организации с 0 баз не должно быть никаких дочерних строк в таблице!');
        }
        console.log(`   ✓ Continental Logistics: плюсик [+] отсутствует, отступ .tree-node-1c-spacer на месте`);
        console.log(`   ✓ Бейдж «${emptyOrgState.badgeText}», дочерних строк 0`);

        // Клик по строке без баз не должен вызывать ошибок или раскрытия
        await page.click('.events-group-row[data-name="Continental Logistics"]');
        await new Promise(r => setTimeout(r, 100));

        const afterClickState = await page.evaluate(() => {
            const org = document.querySelector('.events-group-row[data-name="Continental Logistics"]');
            return {
                isExpanded: org.classList.contains('is-expanded'),
                toggle: org.querySelector('.tree-node-1c-toggle')
            };
        });
        if (afterClickState.isExpanded || afterClickState.toggle) {
            throw new Error('Строка с 0 баз раскрылась при клике!');
        }
        console.log('   ✓ Клик по организации с 0 баз не раскрывает узел и не ломает дерево');

        console.log('\n======================================================================');
        console.log('  🎉 ВСЕ ПРОВЕРКИ ЭТАПА 2 СПРИНТА 5 УСПЕШНО ПРОЙДЕНЫ!');
        console.log('======================================================================');

    } finally {
        if (browser) {
            await browser.close();
        }
        if (serverInstance && typeof serverInstance.close === 'function') {
            serverInstance.close();
        }
    }
}

runTests().catch(err => {
    console.error('\n❌ ОШИБКА ТЕСТИРОВАНИЯ:', err.message);
    process.exit(1);
});
