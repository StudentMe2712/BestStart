const puppeteer = require('puppeteer');

process.env.DB_STUB = 'true';
const TEST_PORT = 3205;
process.env.PORT = String(TEST_PORT);

async function runStage2EventsVerification() {
    console.log('====================================================');
    console.log('  🧪 ВЕРИФИКАЦИЯ ЭТАПА 2 СПРИНТА 6: ЖУРНАЛ СОБЫТИЙ');
    console.log('====================================================\n');

    const { serverInstance } = require('./server');
    await new Promise(resolve => setTimeout(resolve, 800));

    const BASE_URL = `http://localhost:${TEST_PORT}`;
    let browser;

    try {
        console.log('[1/7] Запуск Puppeteer...');
        const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
        browser = await puppeteer.launch({
            headless: 'new',
            executablePath: chromePath,
            args: ['--no-sandbox', '--disable-setuid-sandbox']
        });

        const page = await browser.newPage();
        await page.setViewport({ width: 2560, height: 1440 }); // 2K Resolution

        // 1. Авторизация
        console.log('[2/7] Авторизация в системе через /login...');
        await page.goto(`${BASE_URL}/login`, { waitUntil: 'networkidle0' });
        await page.type('#username', 'admin');
        await page.type('#password', 'admin');
        await Promise.all([
            page.waitForNavigation({ waitUntil: 'networkidle0' }),
            page.click('#submitBtn')
        ]);
        console.log('   ✓ Авторизация успешна!');

        // 2. Открытие раздела /events
        console.log('[3/7] Переход в раздел /events...');
        await page.goto(`${BASE_URL}/events`, { waitUntil: 'networkidle0' });

        // 3. Проверка переключателя и начального состояния
        console.log('[4/7] Проверка переключателя «Только с базами 1С»...');
        const toggleExists = await page.$('#onlyWithBasesToggle');
        if (!toggleExists) {
            throw new Error('Переключатель #onlyWithBasesToggle не найден на странице!');
        }
        const isCheckedDefault = await page.$eval('#onlyWithBasesToggle', el => el.checked);
        if (!isCheckedDefault) {
            throw new Error('Чекбокс #onlyWithBasesToggle должен быть включен по умолчанию (checked)!');
        }
        console.log('   ✓ Переключатель #onlyWithBasesToggle активен по умолчанию.');

        // Проверка: по умолчанию организации с 0 баз скрыты (display: none)
        const emptyRowsHidden = await page.$$eval('.events-group-row.is-empty', rows => {
            return rows.every(r => r.style.display === 'none' || window.getComputedStyle(r).display === 'none');
        });
        const totalEmptyRows = await page.$$eval('.events-group-row.is-empty', rows => rows.length);
        const visibleNonEmptyRows = await page.$$eval('.events-group-row:not(.is-empty)', rows => {
            return rows.filter(r => window.getComputedStyle(r).display !== 'none').length;
        });

        console.log(`   - Найдено организаций без баз (is-empty): ${totalEmptyRows}`);
        console.log(`   - Видимых организаций с базами: ${visibleNonEmptyRows}`);
        if (!emptyRowsHidden) {
            throw new Error('Пустые организации не были скрыты по умолчанию при включенном чекбоксе!');
        }
        console.log('   ✓ Все организации без баз скрыты (display: none) по умолчанию!');

        // 4. Снятие чекбокса и проверка появления пустых организаций
        console.log('[5/7] Снятие чекбокса: проверка появления организаций с 0 баз...');
        await page.click('#onlyWithBasesToggle');
        await new Promise(r => setTimeout(r, 200));

        const isCheckedAfterClick = await page.$eval('#onlyWithBasesToggle', el => el.checked);
        if (isCheckedAfterClick) {
            throw new Error('Чекбокс должен быть выключен после клика!');
        }

        const visibleEmptyRows = await page.$$eval('.events-group-row.is-empty', rows => {
            return rows.filter(r => window.getComputedStyle(r).display !== 'none').length;
        });
        if (visibleEmptyRows !== totalEmptyRows) {
            throw new Error(`Ожидалось ${totalEmptyRows} видимых пустых организаций, получено: ${visibleEmptyRows}`);
        }
        console.log(`   ✓ После снятия чекбокса отображаются все ${visibleEmptyRows} пустых организаций!`);

        // Проверка сохранения в localStorage
        const storageVal = await page.evaluate(() => localStorage.getItem('events_filter_only_bases'));
        if (storageVal !== 'false') {
            throw new Error(`Ожидалось 'false' в localStorage, получено: '${storageVal}'`);
        }
        console.log(`   ✓ Состояние корректно сохранилось в localStorage: ${storageVal}`);

        // Повторное включение чекбокса
        await page.click('#onlyWithBasesToggle');
        await new Promise(r => setTimeout(r, 200));
        const emptyRowsHiddenAgain = await page.$$eval('.events-group-row.is-empty', rows => {
            return rows.every(r => window.getComputedStyle(r).display === 'none');
        });
        if (!emptyRowsHiddenAgain) {
            throw new Error('Пустые организации не скрылись после повторного включения чекбокса!');
        }
        const storageValTrue = await page.evaluate(() => localStorage.getItem('events_filter_only_bases'));
        if (storageValTrue !== 'true') {
            throw new Error(`Ожидалось 'true' в localStorage, получено: '${storageValTrue}'`);
        }
        console.log('   ✓ Чекбокс успешно повторно скрывает пустые строки и обновляет localStorage!');

        // 5. Проверка ширины max-width: 1400px на 2K экранах
        console.log('[6/7] Проверка max-width: 1400px для .events-container и .events-card...');
        const containerMaxWidth = await page.$eval('.events-container', el => window.getComputedStyle(el).maxWidth);
        const cardMaxWidth = await page.$eval('.events-card', el => window.getComputedStyle(el).maxWidth);

        console.log(`   - .events-container max-width: ${containerMaxWidth}`);
        console.log(`   - .events-card max-width: ${cardMaxWidth}`);

        if (containerMaxWidth !== '1400px') {
            throw new Error(`У .events-container max-width должен быть 1400px, получено: ${containerMaxWidth}`);
        }
        if (cardMaxWidth !== '1400px') {
            throw new Error(`У .events-card max-width должен быть 1400px, получено: ${cardMaxWidth}`);
        }
        console.log('   ✓ Ограничение max-width: 1400px соблюдено идеально!');

        // 6. Проверка тултипа к иконке ↗
        console.log('[7/7] Проверка тултипа title="Перейти в карточку компании в Wiki"...');
        const linksWithCorrectTitle = await page.$$eval('.events-org-link', links => {
            return links.map(l => l.getAttribute('title'));
        });
        if (linksWithCorrectTitle.length === 0) {
            throw new Error('Ссылки .events-org-link не найдены!');
        }
        const allHaveExpectedTitle = linksWithCorrectTitle.every(t => t === 'Перейти в карточку компании в Wiki');
        if (!allHaveExpectedTitle) {
            throw new Error(`Не все ссылки имеют правильный title! Найдено: ${JSON.stringify(linksWithCorrectTitle)}`);
        }
        console.log(`   ✓ Все ${linksWithCorrectTitle.length} ссылок имеют title="Перейти в карточку компании в Wiki"!`);

        console.log('\n====================================================');
        console.log('  🎉 ВСЕ ПРОВЕРКИ ЭТАПА 2 СПРИНТА 6 УСПЕШНО ПРОЙДЕНЫ!');
        console.log('====================================================');

    } catch (err) {
        console.error('\n❌ ОШИБКА ПРИ ТЕСТИРОВАНИИ:', err.message);
        process.exitCode = 1;
    } finally {
        if (browser) await browser.close();
        if (serverInstance && typeof serverInstance.close === 'function') {
            serverInstance.close();
        }
        setTimeout(() => process.exit(process.exitCode || 0), 500);
    }
}

runStage2EventsVerification();
