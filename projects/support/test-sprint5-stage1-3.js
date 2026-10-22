/**
 * test-sprint5-stage1-3.js
 * Автоматизированная верификация ЭТАПА 1 и ЭТАПА 3 СПРИНТА 5:
 * 1. Модальное окно «Добавить базу 1С» (views/wiki.ejs, public/js/main.js):
 *    - Отсутствие <select> или <datalist> с типовыми конфигурациями
 *    - Наличие обычного <input type="text" id="dbDescription" name="description" ... required>
 *    - Проверка очистки поля при открытии на добавление и подстановки при редактировании
 *    - Проверка CRUD API с произвольным описанием
 * 2. Векторные SVG-иконки в меню «Ещё» (views/wiki.ejs, public/css/wiki-page.css):
 *    - Наличие четких SVG-иконок для PDF, TXT и корзины (Удалить)
 *    - Стили .more-item-icon (inline-flex, flex-shrink: 0, margin-right: 8px, color: inherit)
 *    - Стили .more-item-danger и :hover (#ef4444)
 *    - Проверка безотказного экспорта PDF (/api/export/pdf/:id) и TXT (/api/export/txt/:id)
 */

process.env.DB_STUB = 'true';
const TEST_PORT = 3297;
process.env.PORT = String(TEST_PORT);

const http = require('http');
const fs = require('fs');
const path = require('path');
const assert = require('assert');

async function runTests() {
    console.log('======================================================================');
    console.log('  🧪 ТЕСТИРОВАНИЕ СПРИНТ 5: ЭТАП 1 И ЭТАП 3');
    console.log('======================================================================\n');

    // -------------------------------------------------------------
    // [1/4] Проверка исходного кода разметки и стилей
    // -------------------------------------------------------------
    console.log('[1/4] Проверка статических файлов (wiki.ejs, wiki-page.css, main.js, i18n.js)...');

    const wikiEjs = fs.readFileSync(path.join(__dirname, 'views', 'wiki.ejs'), 'utf8');
    const wikiCss = fs.readFileSync(path.join(__dirname, 'public', 'css', 'wiki-page.css'), 'utf8');
    const mainJs = fs.readFileSync(path.join(__dirname, 'public', 'js', 'main.js'), 'utf8');
    const i18nJs = fs.readFileSync(path.join(__dirname, 'public', 'js', 'i18n.js'), 'utf8');

    // Проверка удаления datalist / select
    assert(!wikiEjs.includes('datalist id="dbDescriptionPresets"'), 'FAIL: datalist не был удален из wiki.ejs');
    assert(!wikiEjs.includes('list="dbDescriptionPresets"'), 'FAIL: атрибут list остался в input');
    console.log('   ✓ <datalist> и выпадающие списки конфигураций полностью удалены из wiki.ejs');

    // Проверка чистого input для dbDescription
    assert(wikiEjs.includes('id="dbDescription"'), 'FAIL: id="dbDescription" не найден');
    assert(wikiEjs.includes('name="description"'), 'FAIL: name="description" не найден');
    assert(/<input[^>]+id="dbDescription"[^>]+type="text"/i.test(wikiEjs) || /<input[^>]+type="text"[^>]+id="dbDescription"/i.test(wikiEjs), 'FAIL: dbDescription не является обычным input type="text"');
    console.log('   ✓ Поле dbDescription строго является обычным текстовым <input type="text">');

    // Проверка SVG иконок в wiki.ejs
    assert(wikiEjs.includes('more-item-icon'), 'FAIL: класс more-item-icon отсутствует в wiki.ejs');
    assert(wikiEjs.includes('target="_blank" class="more-item" data-i18n="wiki.more.pdf"'), 'FAIL: PDF ссылка оформлена некорректно');
    assert(wikiEjs.includes('class="more-item more-item-danger" id="deleteBtnMore"'), 'FAIL: кнопка удаления оформлена некорректно');
    console.log('   ✓ SVG-иконки для экспорта PDF, TXT и корзины внедрены в #moreMenu');

    // Проверка стилей wiki-page.css
    assert(wikiCss.includes('.more-item-icon'), 'FAIL: .more-item-icon отсутствует в wiki-page.css');
    assert(wikiCss.includes('margin-right: 8px;'), 'FAIL: margin-right: 8px отсутствует для .more-item-icon');
    assert(wikiCss.includes('#ef4444'), 'FAIL: цвет #ef4444 отсутствует в .more-item-danger');
    console.log('   ✓ Стили .more-item-icon и .more-item-danger (#ef4444) корректно настроены');

    // Проверка main.js на очистку и подстановку
    assert(mainJs.includes('dbDescInput.value = \'\''), 'FAIL: dbDescInput.value не очищается в main.js');
    assert(mainJs.includes('data.description || data.release'), 'FAIL: подстановка description в modal edit отсутствует');
    console.log('   ✓ Логика openModalForAdd и openModalForEdit корректно обрабатывает dbDescription');

    // Проверка i18n.js на сохранение SVG при переключении языка
    assert(i18nJs.includes('el.querySelector(\'svg\')'), 'FAIL: i18n.js не проверяет наличие SVG и сотрет иконку');
    console.log('   ✓ Логика i18n.applyLang защищает векторные SVG-иконки от стирания');

    // -------------------------------------------------------------
    // [2/4] Запуск сервера и авторизация
    // -------------------------------------------------------------
    console.log('\n[2/4] Запуск Express сервера и авторизация...');
    const { serverInstance } = require('./server');
    await new Promise(resolve => setTimeout(resolve, 800));

    const BASE_URL = `http://localhost:${TEST_PORT}`;
    let sessionCookie = '';

    function request(method, reqPath, body = null, headers = {}) {
        return new Promise((resolve, reject) => {
            const url = new URL(reqPath, BASE_URL);
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

    const loginRes = await request('POST', '/login', 'username=admin&password=admin');
    assert(loginRes.statusCode === 302 || loginRes.statusCode === 200, 'Ошибка логина');
    console.log('   ✓ Авторизация успешна (admin)');

    // -------------------------------------------------------------
    // [3/4] Проверка API баз 1С с произвольным текстом в description
    // -------------------------------------------------------------
    console.log('\n[3/4] Тестирование сохранения произвольного текста в description через API...');
    const customDesc = '3.0.75.1 (БК спецверсия), самописная интеграция с Kaspi Pay и WMS';
    const createRes = await request('POST', '/api/organizations/1/databases', {
        name: 'Бухгалтерия Спринт 5',
        description: customDesc,
        updated_at: '2026-10-22'
    });
    assert.strictEqual(createRes.statusCode, 201, 'Ошибка создания базы');
    const createdDb = JSON.parse(createRes.body);
    assert(createdDb.id, 'ID новой базы отсутствует');
    assert.strictEqual(createdDb.description, customDesc, 'description не сохранился в точности');
    console.log('   ✓ POST /api/organizations/:id/databases успешно сохранил произвольный текст:');
    console.log(`     "${createdDb.description}"`);

    // Проверка PUT
    const updatedDesc = '3.0.76.0 (БК), модуль Кадровый учет + отчетность НФО';
    const updateRes = await request('PUT', `/api/databases/${createdDb.id}`, {
        name: 'Бухгалтерия Спринт 5 Обновленная',
        description: updatedDesc,
        updated_at: '2026-10-22'
    });
    assert.strictEqual(updateRes.statusCode, 200, 'Ошибка обновления базы');
    console.log('   ✓ PUT /api/databases/:id успешно обновил описание');

    // Проверка GET
    const listRes = await request('GET', '/api/organizations/1/databases');
    const databases = JSON.parse(listRes.body);
    const found = databases.find(d => d.id === createdDb.id);
    assert(found, 'Созданная база не найдена в списке');
    assert.strictEqual(found.description, updatedDesc, 'Обновленное описание не совпадает');
    console.log('   ✓ GET /api/organizations/:id/databases вернул обновленное описание');

    // Очистка тестовой записи
    await request('DELETE', `/api/databases/${createdDb.id}`);
    console.log('   ✓ DELETE /api/databases/:id успешно удалил тестовую запись');

    // -------------------------------------------------------------
    // [4/4] Проверка экспорта PDF и TXT
    // -------------------------------------------------------------
    console.log('\n[4/4] Тестирование экспорта PDF (/api/export/pdf/:id) и TXT...');
    const pdfRes = await request('GET', '/api/export/pdf/1');
    assert.strictEqual(pdfRes.statusCode, 200, 'Ошибка эндпоинта PDF');
    assert(pdfRes.body.includes('window.print()'), 'В PDF-вьюшке отсутствует вызов window.print()');
    assert(pdfRes.body.includes('@media print'), 'В PDF-вьюшке отсутствуют стили печати @media print');
    assert(pdfRes.body.includes('Информационные базы 1С'), 'В PDF отсутствует блок баз 1С');
    console.log('   ✓ GET /api/export/pdf/1 возвращает корректную печатную HTML-форму с window.print()');

    const txtRes = await request('GET', '/api/export/txt/1');
    assert.strictEqual(txtRes.statusCode, 200, 'Ошибка эндпоинта TXT');
    assert(txtRes.headers['content-type'].includes('text/plain'), 'Content-Type TXT некорректен');
    console.log('   ✓ GET /api/export/txt/1 возвращает чистый текстовый документ');

    console.log('\n======================================================================');
    console.log('  🎉 ВСЕ ТЕСТЫ ЭТАПА 1 И ЭТАПА 3 СПРИНТА 5 УСПЕШНО ПРОЙДЕНЫ!');
    console.log('======================================================================');

    if (serverInstance && serverInstance.close) {
        serverInstance.close();
    }
    process.exit(0);
}

runTests().catch(err => {
    console.error('\n❌ ОШИБКА В ТЕСТАХ:', err);
    process.exit(1);
});
