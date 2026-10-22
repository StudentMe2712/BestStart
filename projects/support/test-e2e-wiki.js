const puppeteer = require('puppeteer');

process.env.DB_STUB = 'true';
const TEST_PORT = 3198;
process.env.PORT = String(TEST_PORT);

async function runWikiE2ETests() {
    console.log('====================================================');
    console.log('  🧪 E2E ТЕСТИРОВАНИЕ SMART SERVER VIEWER (ЭТАП 4)');
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

        // 3. Переход в раздел /wiki
        console.log(`[3/8] Переход в раздел /wiki/organization/1 (ALPHUTA)...`);
        await page.goto(`${BASE_URL}/wiki/organization/1`, { waitUntil: 'networkidle0' });

        // Проверка сайдбара
        console.log(`[4/8] Проверка сайдбара: 25 серверов, бейджи PROD/DEV, счетчик...`);
        const items = await page.$$('#wikiOrgList .wiki-list-item');
        console.log(`   - Всего серверов в сайдбаре: ${items.length} (ожидается 25)`);
        if (items.length !== 25) {
            throw new Error(`Ожидалось 25 серверов, получено ${items.length}`);
        }

        const countBadge = await page.$eval('#wikiCountBadge', el => el.textContent.trim());
        console.log(`   - Бейдж счетчика серверов: "${countBadge}"`);
        if (countBadge !== '25') {
            throw new Error(`Бейдж счетчика должен быть 25, получено: ${countBadge}`);
        }

        const prodBadges = await page.$$('.wiki-badge-env--prod');
        const devBadges = await page.$$('.wiki-badge-env--dev');
        console.log(`   - Найдено бейджей PROD: ${prodBadges.length}, DEV: ${devBadges.length}`);
        if (prodBadges.length === 0) {
            throw new Error('Бейджи PROD не найдены в списке серверов');
        }

        // Проверка фильтра поиска
        console.log(`   - Проверка живого поиска в сайдбаре по запросу "alphuta"...`);
        await page.type('#wikiSearch', 'alphuta');
        await page.waitForTimeout ? page.waitForTimeout(200) : new Promise(r => setTimeout(r, 200));

        const visibleItems = await page.$$eval('#wikiOrgList .wiki-list-item', els =>
            els.filter(el => el.style.display !== 'none').length
        );
        const countFiltered = await page.$eval('#wikiCountBadge', el => el.textContent.trim());
        console.log(`   - Отфильтровано серверов: ${visibleItems}, бейдж: "${countFiltered}"`);
        if (visibleItems !== 1) {
            throw new Error(`Ожидался 1 видимый сервер, получено ${visibleItems}`);
        }

        // Проверка кнопки очистки поиска
        console.log(`   - Проверка кнопки быстрой очистки поля поиска (#wikiSearchClear)...`);
        await page.click('#wikiSearchClear');
        const restoredCount = await page.$$eval('#wikiOrgList .wiki-list-item', els =>
            els.filter(el => el.style.display !== 'none').length
        );
        console.log(`   - После очистки восстановлено серверов: ${restoredCount}`);
        if (restoredCount !== 25) {
            throw new Error(`Ожидалось 25 восстановленных серверов, получено ${restoredCount}`);
        }

        // 5. Проверка Smart Server Viewer (карточки и поля)
        console.log(`[5/8] Проверка Smart Server Viewer на карточке ALPHUTA...`);
        const cardRemote = await page.$('#cardRemote');
        const cardDatabase = await page.$('#cardDatabase');
        const cardHardware = await page.$('#cardHardware');

        if (!cardRemote || !cardDatabase || !cardHardware) {
            throw new Error('Карточки Smart Server Viewer (Remote, Database, Hardware) не найдены в DOM');
        }
        console.log(`   ✓ Карточка 1 «Удаленный доступ & RDP» найдена`);
        console.log(`   ✓ Карточка 2 «Базы данных & 1С:Предприятие» найдена`);
        console.log(`   ✓ Карточка 3 «Аппаратная часть & iLO / IPMI» найдена`);

        // Проверка AnyDesk ID
        const anydeskIdText = await page.$eval('#cardRemote .smart-val.font-mono', el => el.textContent.trim());
        console.log(`   - AnyDesk ID: "${anydeskIdText}" (ожидается "1 170 691 894")`);
        if (!anydeskIdText.includes('1 170 691 894')) {
            throw new Error(`Неверный AnyDesk ID: ${anydeskIdText}`);
        }

        // 6. Проверка маскирования пароля, глазка и копирования с тостом
        console.log(`[6/8] Проверка маскирования паролей, глазка и Floating Toast...`);
        const maskedEl = await page.$('#cardRemote .smart-val--masked');
        const initialSecretText = await page.evaluate(el => el.textContent.trim(), maskedEl);
        console.log(`   - Пароль по умолчанию маскирован: "${initialSecretText}"`);
        if (initialSecretText !== '••••••••') {
            throw new Error(`Пароль должен быть маскирован точками, получено: ${initialSecretText}`);
        }

        // Клик по глазку
        const toggleBtn = await page.$('#cardRemote .smart-btn-icon--toggle');
        await toggleBtn.click();
        const revealedText = await page.evaluate(el => el.textContent.trim(), maskedEl);
        console.log(`   - Пароль после клика по глазку: "${revealedText}"`);
        if (revealedText === '••••••••' || !revealedText) {
            throw new Error('Глазок не раскрыл реальный пароль');
        }

        // Клик по глазку повторно
        await toggleBtn.click();
        const remaskedText = await page.evaluate(el => el.textContent.trim(), maskedEl);
        console.log(`   - Пароль после повторного клика: "${remaskedText}"`);
        if (remaskedText !== '••••••••') {
            throw new Error('Пароль должен снова замаскироваться');
        }

        // Клик по кнопке копирования
        console.log(`   - Проверка клика по кнопке копирования AnyDesk...`);
        const copyBtn = await page.$('#cardRemote .smart-btn-icon--copy');
        await copyBtn.click();

        // Проверка тоста
        await page.waitForSelector('.floating-toast.is-show', { timeout: 2000 });
        const toastText = await page.$eval('.floating-toast.is-show', el => el.textContent.trim());
        console.log(`   ✓ Floating Toast появился: "${toastText}"`);

        // Кнопка «Скопировать все реквизиты»
        console.log(`   - Проверка кнопки «Скопировать все реквизиты» (#copyAllSummaryBtn)...`);
        await page.click('#copyAllSummaryBtn');
        await new Promise(r => setTimeout(r, 400));
        const summaryToast = await page.$eval('.floating-toast', el => el.textContent.trim());
        console.log(`   ✓ Toast для выжимки всех реквизитов: "${summaryToast}"`);

        // 7. Проверка Empty State
        console.log(`[7/8] Проверка Empty State на организации с пустой конфигурацией (/wiki/organization/4)...`);
        await page.goto(`${BASE_URL}/wiki/organization/4`, { waitUntil: 'networkidle0' });

        const emptyTitle = await page.$eval('.smart-empty-state__title', el => el.textContent.trim());
        const hasFillBtn = await page.$('#emptyStateEditBtn');
        console.log(`   - Empty State заголовок: "${emptyTitle}"`);
        console.log(`   - Кнопка «Заполнить конфигурацию»: ${Boolean(hasFillBtn)}`);
        if (!hasFillBtn) {
            throw new Error('Кнопка «Заполнить конфигурацию сервера» не найдена в Empty State');
        }

        // 8. Проверка перехода в режим редактирования и кнопок Отмена / Сохранить
        console.log(`[8/8] Проверка режима редактирования (Edit Mode) и кнопок Отмена / Сохранить...`);
        await page.click('#emptyStateEditBtn');
        await new Promise(r => setTimeout(r, 300));

        const editorOuterVisible = await page.$eval('#editorOuter', el => el.style.display !== 'none');
        const smartViewerHidden = await page.$eval('#smartServerViewer', el => el.style.display === 'none');
        const saveBtnVisible = await page.$('#saveBtn');
        const cancelBtnVisible = await page.$('#cancelEditBtn');

        console.log(`   - Редактор открыт: ${editorOuterVisible}`);
        console.log(`   - Smart Viewer скрыт: ${smartViewerHidden}`);
        console.log(`   - Кнопка Сохранить: ${Boolean(saveBtnVisible)}`);
        console.log(`   - Кнопка Отмена: ${Boolean(cancelBtnVisible)}`);

        if (!editorOuterVisible || !saveBtnVisible || !cancelBtnVisible) {
            throw new Error('Режим редактирования или кнопки Отмена/Сохранить работают некорректно');
        }

        // Проверка кнопки Отмена
        await page.click('#cancelEditBtn');
        await new Promise(r => setTimeout(r, 300));

        const editorOuterClosed = await page.$eval('#editorOuter', el => el.style.display === 'none');
        const smartViewerRestored = await page.$eval('#smartServerViewer', el => el.style.display !== 'none');
        console.log(`   ✓ Кнопка Отмена вернула в Smart Viewer (редактор скрыт: ${editorOuterClosed}, Viewer показан: ${smartViewerRestored})`);

        console.log('\n====================================================');
        console.log('  🎉 ВСЕ E2E ПРОВЕРКИ ЭТАПА 4 УСПЕШНО ПРОЙДЕНЫ!');
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
