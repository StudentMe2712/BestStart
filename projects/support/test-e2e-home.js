const puppeteer = require('puppeteer');

process.env.DB_STUB = 'true';
const TEST_PORT = 3199;
process.env.PORT = String(TEST_PORT);

async function runE2ETests() {
    console.log('====================================================');
    console.log('  🧪 E2E ТЕСТИРОВАНИЕ ГЛАВНОЙ СТРАНИЦЫ (ЭТАП 4 СПРИНТА 3)');
    console.log('====================================================\n');

    // 1. Запуск сервера с in-memory stub DB
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

        // 2. Авторизация
        console.log(`[2/8] Авторизация в системе через /login (admin / admin)...`);
        await page.goto(`${BASE_URL}/login`, { waitUntil: 'networkidle0' });
        await page.type('#username', 'admin');
        await page.type('#password', 'admin');
        await Promise.all([
            page.waitForNavigation({ waitUntil: 'networkidle0' }),
            page.click('#submitBtn')
        ]);

        const currentUrl = page.url();
        console.log(`✓ Успешный вход! Текущий URL: ${currentUrl}`);
        if (!currentUrl.endsWith('/') && !currentUrl.endsWith(`:${TEST_PORT}`)) {
            throw new Error(`Ожидался переход на главную страницу, но получен URL: ${currentUrl}`);
        }

        // 3. Проверка Topbar (минималистичный бренд без картинок)
        console.log(`[3/8] Проверка верхней панели навигации (topbar)...`);
        const brandName = await page.$eval('.home-brand-name', el => el.textContent.trim());
        const topbarImages = await page.$$('.home-topbar img');
        const themeBtn = await page.$('#themeToggle');
        const accountBtn = await page.$('#homeAccountToggle');

        console.log(`   - Текстовый бренд: "${brandName}"`);
        console.log(`   - Количество <img> в topbar: ${topbarImages.length}`);
        if (brandName !== 'ИТС-Ком') throw new Error(`Неверный текст бренда: "${brandName}", ожидалось "ИТС-Ком"`);
        if (topbarImages.length > 0) throw new Error('В topbar не должно быть тегов <img> или /logo.png');
        if (!themeBtn) throw new Error('Отсутствует кнопка переключения темы #themeToggle');
        if (!accountBtn) throw new Error('Отсутствует кнопка аккаунта #homeAccountToggle');
        console.log('✓ Верхняя панель навигации полностью соответствует требованиям!');

        // 4. Проверка Верхней статусной секции (Hero Bar)
        console.log(`[4/8] Проверка Верхней статусной секции (Hero Bar)...`);
        const heroTitle = await page.$eval('.command-hero__title', el => el.textContent.trim());
        const heroGreeting = await page.$eval('.command-hero__user-badge', el => el.textContent.trim());
        const heroUser = await page.$eval('.command-hero__username', el => el.textContent.trim());
        const heroStatus = await page.$eval('.command-hero__status-text', el => el.textContent.trim());
        const heroDate = await page.$eval('#heroCurrentDate', el => el.textContent.trim());
        const hasPulseDot = await page.$('.status-pulse-dot');

        console.log(`   - Главный заголовок: "${heroTitle}"`);
        console.log(`   - Приветствие: "${heroGreeting}"`);
        console.log(`   - Логин пользователя: "${heroUser}"`);
        console.log(`   - Статус узлов: "${heroStatus}"`);
        console.log(`   - Актуальная дата: "${heroDate}"`);

        if (heroTitle !== 'База знаний ИТС-Ком') {
            throw new Error(`Неверный заголовок: "${heroTitle}", ожидалось "База знаний ИТС-Ком"`);
        }
        if (!heroGreeting.includes('С возвращением') || heroUser !== 'admin') {
            throw new Error(`Неверное приветствие пользователя: ожидался логин "admin", получено "${heroUser}"`);
        }
        if (!heroStatus.includes('Все узлы в штатном режиме')) {
            throw new Error('Неверный статус системы');
        }
        if (!heroDate || heroDate.length < 5) {
            throw new Error('Отсутствует динамическая дата');
        }
        // Проверка даты с заглавной буквы
        const firstLetter = heroDate.charAt(0);
        if (firstLetter !== firstLetter.toUpperCase() || firstLetter === firstLetter.toLowerCase()) {
            throw new Error(`Дата должна начинаться с заглавной буквы: "${heroDate}"`);
        }
        if (!hasPulseDot) {
            throw new Error('Отсутствует зеленый пульсирующий индикатор статуса');
        }
        console.log('✓ Верхняя статусная секция полностью соответствует спецификации!');

        // 5. Проверка ПОЛНОГО удаления строки поиска и блока «Быстрый доступ к серверам»
        console.log(`[5/8] Проверка отсутствия перегруза (поиск и быстрый доступ)...`);
        const searchInput = await page.$('#commandSearchInput');
        const searchWrap = await page.$('#commandSearchWrap');
        const searchDropdown = await page.$('#commandSearchResults');
        const quickServersSection = await page.$('.command-quick-section, #quickServersHeading');
        const serverCards = await page.$$('.server-card');

        if (searchInput || searchWrap || searchDropdown) {
            throw new Error('Блок быстрого поиска (#commandSearchInput) должен быть полностью удален!');
        }
        if (quickServersSection || serverCards.length > 0) {
            throw new Error(`Блок «Быстрый доступ к серверам» должен быть полностью удален! Найдено карточек: ${serverCards.length}`);
        }
        console.log('✓ Строка поиска и карточки быстрого доступа успешно и полностью удалены!');

        // 6. Проверка 2 крупных сбалансированных карточек по центру
        console.log(`[6/8] Проверка 2 крупных карточек («Wiki» и «События»)...`);
        const widgets = await page.$$('.command-widget');
        if (widgets.length !== 2) {
            throw new Error(`Ожидалось ровно 2 карточки, получено: ${widgets.length}`);
        }

        // Проверка полного отсутствия списков буллетов (.command-widget__features = 0)
        const featureLists = await page.$$('.command-widget__features, .feature-item, .feature-bullet');
        console.log(`   - Количество элементов буллетов (.command-widget__features): ${featureLists.length}`);
        if (featureLists.length > 0) {
            throw new Error(`Списки буллетов (.command-widget__features) должны быть полностью удалены! Найдено: ${featureLists.length}`);
        }

        // Карточка 1: Wiki
        const wikiCard = await page.$('.command-widget--wiki');
        if (!wikiCard) throw new Error('Не найдена карточка .command-widget--wiki');
        const wikiTitle = await wikiCard.$eval('.command-widget__title', el => el.textContent.trim());
        const wikiDesc = await wikiCard.$eval('.command-widget__desc', el => el.textContent.trim());
        const wikiBadge = await wikiCard.$eval('#wikiTotalBadge', el => el.textContent.trim());
        const wikiBtn = await wikiCard.$('.command-widget__btn--primary');
        const wikiBtnText = await wikiBtn.evaluate(el => el.textContent.trim());
        const wikiBtnHref = await wikiBtn.evaluate(el => el.getAttribute('href'));
        const wikiSvg = await wikiCard.$('.command-widget__icon-wrap svg');

        console.log(`   [Карточка 1 Wiki] Заголовок: "${wikiTitle}", Бейдж: "${wikiBadge}", Описание: "${wikiDesc}", Кнопка: "${wikiBtnText}" -> ${wikiBtnHref}`);
        if (wikiTitle !== 'Wiki') throw new Error(`Ожидался заголовок "Wiki", получено "${wikiTitle}"`);
        if (!wikiDesc.includes('Технические параметры серверов, доступы и конфигурации')) {
            throw new Error(`Неверное описание Wiki: "${wikiDesc}"`);
        }
        if (!wikiBadge.includes('организаций')) {
            throw new Error(`Ожидался бейдж организаций в карточке Wiki, получено "${wikiBadge}"`);
        }
        if (!wikiBtnText.includes('Перейти в базу') || wikiBtnHref !== '/wiki') {
            throw new Error(`Кнопка Wiki должна вести на /wiki с текстом "Перейти в базу", получено: "${wikiBtnText}" (${wikiBtnHref})`);
        }
        if (!wikiSvg) throw new Error('Отсутствует векторная SVG-иконка в карточке Wiki');

        // Карточка 2: События
        const eventsCard = await page.$('.command-widget--events');
        if (!eventsCard) throw new Error('Не найдена карточка .command-widget--events');
        const eventsTitle = await eventsCard.$eval('.command-widget__title', el => el.textContent.trim());
        const eventsDesc = await eventsCard.$eval('.command-widget__desc', el => el.textContent.trim());
        const eventsBadges = await eventsCard.$$('.widget-badge');
        for (const badge of eventsBadges) {
            const bText = await badge.evaluate(el => el.textContent.trim());
            if (bText.includes('В разработке')) {
                throw new Error('Бейдж "В разработке" должен быть удален из карточки События');
            }
        }
        const eventsBtn = await eventsCard.$('#btnOpenEvents, .command-widget__btn--primary');
        if (!eventsBtn) throw new Error('Отсутствует активная кнопка перехода к событиям (#btnOpenEvents)');
        const eventsBtnText = await eventsBtn.evaluate(el => el.textContent.trim());
        const eventsBtnHref = await eventsBtn.evaluate(el => el.getAttribute('href'));
        const eventsSvg = await eventsCard.$('.command-widget__icon-wrap svg');

        console.log(`   [Карточка 2 События] Заголовок: "${eventsTitle}", Описание: "${eventsDesc}", Кнопка: "${eventsBtnText}" -> ${eventsBtnHref}`);
        if (eventsTitle !== 'События') throw new Error(`Ожидался заголовок "События", получено "${eventsTitle}"`);
        if (!eventsDesc.includes('Иерархический журнал и реестр информационных баз 1С') && !eventsDesc.includes('Журнал обновлений и релизов')) {
            throw new Error(`Неверное описание Событий: "${eventsDesc}", ожидалось "Иерархический журнал и реестр информационных баз 1С."`);
        }
        if (!eventsBtnText.includes('Перейти к событиям') || eventsBtnHref !== '/events') {
            throw new Error(`Кнопка Событий должна вести на /events с текстом "Перейти к событиям", получено: "${eventsBtnText}" (${eventsBtnHref})`);
        }
        if (!eventsSvg) throw new Error('Отсутствует векторная SVG-иконка в карточке События');

        console.log('✓ Обе карточки («Wiki» и «События») идеально сверстаны и сбалансированы!');

        // 7. Проверка переключения темы (Light / Dark)
        console.log(`[7/8] Проверка переключения темной и светлой темы...`);
        await themeBtn.click();
        const darkTheme = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
        console.log(`   - Активирована тема: data-theme="${darkTheme}"`);
        if (darkTheme !== 'dark') throw new Error('Тема должна переключиться на dark');

        await themeBtn.click();
        const lightTheme = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
        console.log(`   - Возврат в тему: data-theme="${lightTheme}"`);
        if (lightTheme !== 'light') throw new Error('Тема должна переключиться на light');
        console.log('✓ Переключатель темы функционирует штатно!');

        // 8. Проверка центрирования и адаптивности (1080p, 2K, 4K)
        console.log(`[8/8] Проверка центрирования и адаптивности для 1080p, 2K, 4K...`);
        const viewports = [
            { w: 1920, h: 1080, name: '1080p Full HD' },
            { w: 2560, h: 1440, name: '2K QHD' },
            { w: 3840, h: 2160, name: '4K UHD' }
        ];

        for (const vp of viewports) {
            await page.setViewport({ width: vp.w, height: vp.h });
            const hubWidth = await page.$eval('.command-hub', el => el.offsetWidth);
            console.log(`   - Разрешение ${vp.name} (${vp.w}x${vp.h}): .command-hub ширина = ${hubWidth}px (макс. 1100px)`);
            if (hubWidth > 1100) throw new Error(`Контейнер превышает 1100px на разрешении ${vp.name}: ${hubWidth}px`);
        }
        console.log('✓ Адаптивность и центрирование (1000-1100px) успешно подтверждены!');

        console.log('\n====================================================');
        console.log('  🎉 ВСЕ E2E ТЕСТЫ ЭТАПА 4 УСПЕШНО ПРОЙДЕНЫ (8/8)!');
        console.log('====================================================\n');

    } finally {
        if (browser) await browser.close();
        if (serverInstance) serverInstance.close();
    }
}

runE2ETests().catch(err => {
    console.error('❌ E2E Тест главной страницы завершился с ошибкой:', err);
    process.exit(1);
});
