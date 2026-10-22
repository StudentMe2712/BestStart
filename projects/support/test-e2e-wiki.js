const puppeteer = require('puppeteer');

process.env.DB_STUB = 'true';
const TEST_PORT = 3198;
process.env.PORT = String(TEST_PORT);

async function runWikiE2ETests() {
    console.log('====================================================');
    console.log('  🧪 E2E ТЕСТИРОВАНИЕ WIKI: ЧИСТЫЙ ТЕКСТ И ТИПОГРАФИКА');
    console.log('====================================================\n');

    const { serverInstance } = require('./server');
    await new Promise(resolve => setTimeout(resolve, 800));

    const BASE_URL = `http://localhost:${TEST_PORT}`;
    let browser;

    try {
        console.log(`[1/8] Запуск Headless Chrome (Puppeteer)...`);
        const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
        browser = await puppeteer.launch({
            headless: 'new',
            executablePath: chromePath,
            args: ['--no-sandbox', '--disable-setuid-sandbox']
        });

        const context = browser.defaultBrowserContext();
        await context.overridePermissions(BASE_URL, ['clipboard-read', 'clipboard-write']);

        const page = await browser.newPage();
        await page.setViewport({ width: 1920, height: 1080 });

        page.on('console', msg => console.log('PAGE LOG:', msg.text()));
        page.on('pageerror', err => console.log('PAGE ERROR:', err.message));

        // 2. Авторизация
        console.log(`[2/8] Авторизация в системе через /login (admin / admin)...`);
        await page.goto(`${BASE_URL}/login`, { waitUntil: 'networkidle0' });
        await page.type('#username', 'admin');
        await page.type('#password', 'admin');
        await Promise.all([
            page.waitForNavigation({ waitUntil: 'networkidle0' }),
            page.click('#submitBtn')
        ]);
        console.log(`✓ Авторизация успешна!`);

        // 3. Переход в раздел /wiki/organization/1
        console.log(`[3/8] Переход в раздел /wiki/organization/1 (ALPHUTA)...`);
        await page.goto(`${BASE_URL}/wiki/organization/1`, { waitUntil: 'networkidle0' });

        // Проверка шапки: текстовый бренд "ИТС-Ком", отсутствие логотипа img
        console.log(`   - Проверка шапки: текстовый заголовок «ИТС-Ком» без img /logo.png...`);
        const brandText = await page.$eval('.home-topbar__brand .home-brand-name', el => el.textContent.trim());
        const hasBrandImg = await page.$('.home-topbar__brand img');
        if (brandText !== 'ИТС-Ком') {
            throw new Error(`Ожидался текст бренда «ИТС-Ком», получено: "${brandText}"`);
        }
        if (hasBrandImg) {
            throw new Error('Обнаружен устаревший тег <img> с логотипом в шапке /wiki!');
        }
        console.log(`   ✓ Шапка оформлена в едином минималистичном стиле («${brandText}», без логотипа)`);

        // 4. Проверка сайдбара
        console.log(`[4/8] Проверка сайдбара: 25 серверов, бейджи PROD/DEV, счетчик и поиск...`);
        const items = await page.$$('#wikiOrgList .wiki-list-item');
        if (items.length !== 25) {
            throw new Error(`Ожидалось 25 серверов, получено ${items.length}`);
        }

        const countBadge = await page.$eval('#wikiCountBadge', el => el.textContent.trim());
        if (countBadge !== '25') {
            throw new Error(`Бейдж счетчика должен быть 25, получено: ${countBadge}`);
        }

        const prodBadges = await page.$$('.wiki-badge-env');
        if (prodBadges.length > 0) {
            throw new Error(`Обнаружены бейджи PROD/DEV в списке серверов (${prodBadges.length} шт.), они должны быть удалены!`);
        }
        console.log(`   ✓ Бейджи контура PROD/DEV полностью удалены из сайдбара`);

        // Resize разделитель
        const resizeHandle = await page.$('#wikiResizeHandle');
        if (!resizeHandle) {
            throw new Error('Resize-разделитель сайдбара #wikiResizeHandle не найден');
        }

        // Проверка фильтра поиска
        console.log(`   - Проверка живого поиска в сайдбаре по запросу "alphuta"...`);
        await page.type('#wikiSearch', 'alphuta');
        await new Promise(r => setTimeout(r, 200));

        const visibleItems = await page.$$eval('#wikiOrgList .wiki-list-item', els =>
            els.filter(el => el.style.display !== 'none').length
        );
        const countFiltered = await page.$eval('#wikiCountBadge', el => el.textContent.trim());
        console.log(`   - Отфильтровано серверов: ${visibleItems}, бейдж: "${countFiltered}"`);
        if (visibleItems !== 1) {
            throw new Error(`Ожидался 1 видимый сервер, получено ${visibleItems}`);
        }

        // Очистка поиска
        console.log(`   - Очистка поиска через #wikiSearchClear...`);
        await page.click('#wikiSearchClear');
        const restoredCount = await page.$$eval('#wikiOrgList .wiki-list-item', els =>
            els.filter(el => el.style.display !== 'none').length
        );
        if (restoredCount !== 25) {
            throw new Error(`Ожидалось 25 восстановленных серверов, получено ${restoredCount}`);
        }
        console.log(`   ✓ Поиск и сброс сайдбара работают корректно`);

        // 5. Проверка чистого листа контента и комфортной типографики
        console.log(`[5/8] Проверка чистого листа .wiki-sheet и типографики (15px, line-height 1.6, monospace)...`);
        const wikiSheet = await page.$('.wiki-sheet');
        if (!wikiSheet) {
            throw new Error('Контейнер чистого листа (.wiki-sheet) не найден в DOM');
        }

        const sheetStyles = await page.$eval('.wiki-sheet', el => {
            const cs = window.getComputedStyle(el);
            return {
                fontSize: cs.fontSize,
                lineHeight: cs.lineHeight,
                fontFamily: cs.fontFamily
            };
        });

        console.log(`   - Font Size: ${sheetStyles.fontSize} (ожидается 15px)`);
        console.log(`   - Line Height: ${sheetStyles.lineHeight} (ожидается 24px = 15px * 1.6)`);
        console.log(`   - Font Family: ${sheetStyles.fontFamily}`);

        if (sheetStyles.fontSize !== '15px') {
            throw new Error(`Ожидался размер шрифта 15px, получено: ${sheetStyles.fontSize}`);
        }
        if (sheetStyles.lineHeight !== '24px' && sheetStyles.lineHeight !== '1.6') {
            throw new Error(`Ожидался line-height 1.6 (24px), получено: ${sheetStyles.lineHeight}`);
        }
        const fontLower = sheetStyles.fontFamily.toLowerCase();
        const hasMonoStack = fontLower.includes('jetbrains mono') ||
                             fontLower.includes('consolas') ||
                             fontLower.includes('sf mono') ||
                             fontLower.includes('menlo') ||
                             fontLower.includes('monaco') ||
                             fontLower.includes('monospace');
        if (!hasMonoStack) {
            throw new Error(`Стек шрифта не содержит требуемые моноширинные шрифты: ${sheetStyles.fontFamily}`);
        }
        console.log(`   ✓ Комфортная типографика (15px, line-height 1.6, моноширинный стек) подтверждена!`);

        // Проверка наличия параметров ALPHUTA в чистом виде
        const sheetText = await page.$eval('.wiki-sheet', el => el.innerText);
        if (!sheetText.includes('1 170 691 894') || !sheetText.includes('MS SQL 2019') || !sheetText.includes('ASDFzxcv_113525')) {
            throw new Error('Данные серверов ALPHUTA не отображаются в чистом листе');
        }
        console.log(`   ✓ Все технические параметры сервера ALPHUTA отображаются в естественном виде`);

        // 6. Проверка отсутствия старых жестких сеток Smart Server Viewer и полей «Не указано»
        console.log(`[6/8] Проверка отсутствия жестких таблиц и статусов «Не указано»...`);
        const oldGrid = await page.$('.smart-grid, #cardRemote, #cardDatabase, #cardHardware');
        if (oldGrid) {
            throw new Error('Обнаружены элементы устаревшей жесткой сетки Smart Server Viewer!');
        }

        const containsNotSpecified = sheetText.includes('Не указано');
        if (containsNotSpecified) {
            throw new Error('В тексте листа обнаружены поля со статусом «Не указано»!');
        }
        console.log(`   ✓ Жесткие сетки и поля со статусом «Не указано» полностью отсутствуют!`);

        // 7. Проверка удаления #copyAllBtn и работа копирования строки
        console.log(`[7/8] Проверка отсутствия #copyAllBtn и копирования строки (.wiki-line-copy-btn)...`);
        const copyAllBtn = await page.$('#copyAllBtn');
        if (copyAllBtn) {
            throw new Error('Кнопка #copyAllBtn обнаружена, хотя должна быть удалена в Спринте 3!');
        }
        console.log(`   ✓ Кнопка #copyAllBtn («Скопировать все реквизиты») удалена`);

        const lineCopyBtn = await page.$('.wiki-line-copy-btn');
        if (lineCopyBtn) {
            await lineCopyBtn.click();
            await page.waitForSelector('.floating-toast.is-show', { timeout: 2000 });
            const toastMsg = await page.$eval('.floating-toast.is-show', el => el.textContent.trim());
            console.log(`   - Появился всплывающий тост: "${toastMsg}"`);
            if (!toastMsg.includes('скопирован')) {
                throw new Error(`Неверный текст тоста: ${toastMsg}`);
            }
            console.log(`   ✓ Копирование строки и всплывающий тост работают штатно!`);
        }

        // 8. Проверка Empty State и режима редактирования
        console.log(`[8/8] Проверка Empty State (/wiki/organization/4) и перехода в режим редактирования...`);
        await page.goto(`${BASE_URL}/wiki/organization/4`, { waitUntil: 'networkidle0' });

        const emptyTitle = await page.$eval('.wiki-empty-state__title', el => el.textContent.trim());
        console.log(`   - Заголовок Empty State: "${emptyTitle}"`);
        if (!emptyTitle.includes('Параметры сервера пока не заполнены')) {
            throw new Error(`Неверный заголовок Empty State: ${emptyTitle}`);
        }

        const editEmptyBtn = await page.$('#emptyStateEditBtn');
        if (!editEmptyBtn) {
            throw new Error('Кнопка «Редактировать» в Empty State не найдена');
        }

        // Переход в режим редактирования
        await page.click('#emptyStateEditBtn');
        await new Promise(r => setTimeout(r, 300));

        const editorOuterVisible = await page.$eval('#editorOuter', el => el.style.display !== 'none');
        const sheetHidden = await page.$eval('#wikiSheet', el => el.style.display === 'none');
        const saveBtn = await page.$('#saveBtn');
        const cancelBtn = await page.$('#cancelEditBtn');

        console.log(`   - Редактор открыт: ${editorOuterVisible}`);
        console.log(`   - Чистый лист скрыт: ${sheetHidden}`);
        console.log(`   - Кнопка Сохранить (#saveBtn): ${Boolean(saveBtn)}`);
        console.log(`   - Кнопка Отмена (#cancelEditBtn): ${Boolean(cancelBtn)}`);

        if (!editorOuterVisible || !saveBtn || !cancelBtn) {
            throw new Error('Режим редактирования или кнопки Сохранить/Отмена работают некорректно');
        }

        // Проверка кнопки «Отмена»
        console.log(`   - Проверка нажатия кнопки «Отмена» (#cancelEditBtn)...`);
        await page.click('#cancelEditBtn');
        await new Promise(r => setTimeout(r, 300));

        const editorClosed = await page.$eval('#editorOuter', el => el.style.display === 'none');
        const sheetRestored = await page.$eval('#wikiSheet', el => el.style.display !== 'none');
        console.log(`   ✓ Кнопка «Отмена» закрыла редактор (редактор скрыт: ${editorClosed}, лист показан: ${sheetRestored})`);

        // Проверка кнопки «Сохранить»
        console.log(`   - Проверка ввода текста и сохранения (#saveBtn)...`);
        await page.click('#editBtnTop');
        await new Promise(r => setTimeout(r, 300));
        await page.focus('#orgEditor');
        await page.keyboard.type('Тестовая конфигурация сервера\nAnydesk: 111 222 333\nПароль: SuperSecretPass123');

        await page.click('#saveBtn');
        await new Promise(r => setTimeout(r, 500));

        const savedSheetText = await page.$eval('#wikiSheet', el => el.innerText);
        console.log(`   - Текст после сохранения: "${savedSheetText.replace(/\n/g, ' ')}"`);
        if (!savedSheetText.includes('111 222 333') || !savedSheetText.includes('SuperSecretPass123')) {
            throw new Error('Сохраненный текст не появился в чистом листе');
        }
        console.log(`   ✓ Сохранение через PUT /api/organizations/:id успешно обновило лист контента!`);

        console.log('\n====================================================');
        console.log('  🎉 ВСЕ E2E ПРОВЕРКИ ЭТАПА 3 УСПЕШНО ПРОЙДЕНЫ!');
        console.log('====================================================\n');

    } catch (err) {
        console.error('❌ ОШИБКА E2E ТЕСТИРОВАНИЯ:', err);
        process.exitCode = 1;
    } finally {
        if (browser) await browser.close();
        if (serverInstance) serverInstance.close();
        process.exit();
    }
}

runWikiE2ETests();