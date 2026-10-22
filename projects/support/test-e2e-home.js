const puppeteer = require('puppeteer');

process.env.DB_STUB = 'true';
const TEST_PORT = 3199;
process.env.PORT = String(TEST_PORT);

async function runE2ETests() {
    console.log('====================================================');
    console.log('  🧪 E2E ТЕСТИРОВАНИЕ КОМАНДНОГО ДАШБОРДА (ЭТАП 3)');
    console.log('====================================================\n');

    // 1. Запуск сервера с stub DB
    const { serverInstance } = require('./server');
    await new Promise(resolve => setTimeout(resolve, 800));

    const BASE_URL = `http://localhost:${TEST_PORT}`;
    let browser;

    try {
        console.log(`[1/7] Запуск Headless Chrome (Puppeteer)...`);
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

        // Авторизация
        console.log(`[2/7] Авторизация в системе через /login (admin / admin)...`);
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

        // 3. Проверка Приветственной панели (Hero Bar)
        console.log(`[3/7] Проверка Приветственной панели (Hero Bar)...`);
        const heroTitle = await page.$eval('.command-hero__title', el => el.textContent.trim());
        const heroGreeting = await page.$eval('.command-hero__user-badge', el => el.textContent.trim());
        const heroStatus = await page.$eval('.command-hero__status-text', el => el.textContent.trim());
        const heroDate = await page.$eval('#heroCurrentDate', el => el.textContent.trim());
        const hasPulseDot = await page.$('.status-pulse-dot');

        console.log(`   - Заголовок: "${heroTitle}"`);
        console.log(`   - Приветствие: "${heroGreeting}"`);
        console.log(`   - Статус: "${heroStatus}"`);
        console.log(`   - Текущая дата: "${heroDate}"`);

        if (!heroTitle.includes('Командный центр ИТС-Ком')) throw new Error('Неверный заголовок Hero Bar');
        if (!heroGreeting.includes('С возвращением') || !heroGreeting.includes('Администратор')) throw new Error('Неверное приветствие пользователя');
        if (!heroStatus.includes('Все узлы в штатном режиме')) throw new Error('Неверный статус системы');
        if (!heroDate || heroDate.length < 5) throw new Error('Отсутствует динамическая дата');
        if (!hasPulseDot) throw new Error('Отсутствует пульсирующий индикатор статуса');
        console.log('✓ Приветственная панель (Hero Bar) полностью соответствует спецификации!');

        // 4. Проверка Command Search Bar и хоткея Ctrl + K
        console.log(`[4/7] Проверка строки быстрого поиска и хоткея Ctrl + K...`);
        const searchInput = await page.$('#commandSearchInput');
        if (!searchInput) throw new Error('Поле поиска #commandSearchInput не найдено');

        // Клик вне поля, затем эмуляция нажатия Ctrl+K
        await page.click('body');
        const isFocusedBefore = await page.evaluate(() => document.activeElement.id === 'commandSearchInput');
        if (isFocusedBefore) throw new Error('Инпут не должен быть в фокусе до нажатия хоткея');

        await page.keyboard.down('Control');
        await page.keyboard.press('KeyK');
        await page.keyboard.up('Control');

        const isFocusedAfter = await page.evaluate(() => document.activeElement.id === 'commandSearchInput');
        if (!isFocusedAfter) throw new Error('Хоткей Ctrl+K не установил фокус в #commandSearchInput');
        console.log('✓ Хоткей Ctrl + K успешно активирует фокус строки поиска!');

        // Ввод поискового запроса и проверка интерактивного выпадающего списка
        await page.type('#commandSearchInput', 'ALPH');
        await page.waitForSelector('#commandSearchResults', { visible: true });

        const resultsCountText = await page.$eval('#searchResultsCount', el => el.textContent);
        const firstResultName = await page.$eval('.dropdown-item__name', el => el.textContent);
        console.log(`   - Результаты поиска: ${resultsCountText}, первый результат: "${firstResultName}"`);

        if (!firstResultName.includes('ALPHUTA')) throw new Error('Ожидался сервер ALPHUTA в результатах поиска');

        // Проверка навигации стрелками (ArrowDown)
        await page.keyboard.press('ArrowDown');
        const hasSelectedClass = await page.$eval('.command-dropdown__item', el => el.classList.contains('is-selected'));
        if (!hasSelectedClass) throw new Error('Стрелка вниз должна выделять элемент .is-selected');

        // Закрытие по Escape
        await page.keyboard.press('Escape');
        const isDropdownHidden = await page.$eval('#commandSearchResults', el => el.style.display === 'none');
        if (!isDropdownHidden) throw new Error('Escape должен скрывать выпадающий список результатов');
        console.log('✓ Интерактивный поиск и клавиатурная навигация работают безупречно!');

        // 5. Проверка Сетки основных виджетов
        console.log(`[5/7] Проверка виджетов Wiki и События/Мониторинг...`);
        const wikiBadge = await page.$eval('#wikiTotalBadge', el => el.textContent.trim());
        const wikiStatus = await page.$eval('.command-widget--wiki .widget-status', el => el.textContent.trim());
        const wikiBtnHref = await page.$eval('#btnOpenWiki', el => el.getAttribute('href'));

        console.log(`   - Wiki бейдж: "${wikiBadge}", статус: "${wikiStatus}", ссылка: "${wikiBtnHref}"`);
        if (!wikiBadge.includes('серверов')) throw new Error('Отсутствует бейдж серверов на карточке Wiki');
        if (!wikiStatus.includes('Активно')) throw new Error('Отсутствует индикатор Активно на карточке Wiki');
        if (wikiBtnHref !== '/wiki') throw new Error('Кнопка Wiki должна вести на /wiki');

        const eventsBadge = await page.$eval('.command-widget--events .widget-badge', el => el.textContent.trim());
        const eventsStatus = await page.$eval('.command-widget--events .widget-status', el => el.textContent.trim());
        const feedItems = await page.$$('.activity-feed-item');
        console.log(`   - Мониторинг бейдж: "${eventsBadge}", статус: "${eventsStatus}", событий в ленте: ${feedItems.length}`);
        if (!eventsBadge.includes('В разработке')) throw new Error('Неверный бейдж Событий');
        if (!eventsStatus.includes('Zabbix')) throw new Error('Неверный статус интеграции Zabbix');
        if (feedItems.length < 4) throw new Error('Ожидалось не менее 4 событий в ленте Zabbix');
        console.log('✓ Сетка основных виджетов полностью соответствует требованиям!');

        // 6. Проверка Блока «Быстрый доступ» (Quick Access Servers) & AnyDesk копирования
        console.log(`[6/7] Проверка блока «Быстрый доступ» и копирования AnyDesk в 1 клик...`);
        const serverCards = await page.$$('.server-card');
        if (serverCards.length !== 4) throw new Error(`Ожидалось 4 карточки быстрых серверов, получено: ${serverCards.length}`);

        const expectedServers = [
            { name: 'ALPHUTA', anydesk: '1 170 691 894', link: '/wiki/organization/1' },
            { name: 'Antarium', anydesk: '1218740083', link: '/wiki/organization/2' },
            { name: 'ARG', anydesk: '987 654 321', link: '/wiki/organization/3' },
            { name: 'VDS', anydesk: '543 210 987', link: '/wiki/organization/14' }
        ];

        for (let i = 0; i < expectedServers.length; i++) {
            const exp = expectedServers[i];
            const name = await serverCards[i].$eval('.server-card__name', el => el.textContent.trim());
            const anydesk = await serverCards[i].$eval('.anydesk-code', el => el.textContent.trim());
            const link = await serverCards[i].$eval('.server-card__link', el => el.getAttribute('href'));

            console.log(`   - Сервер #${i+1}: ${name} | AnyDesk: ${anydesk} | Ссылка: ${link}`);
            if (name !== exp.name) throw new Error(`Неверное имя сервера #${i+1}: ${name} != ${exp.name}`);
            if (anydesk !== exp.anydesk) throw new Error(`Неверный AnyDesk #${i+1}: ${anydesk} != ${exp.anydesk}`);
            if (link !== exp.link) throw new Error(`Неверная ссылка #${i+1}: ${link} != ${exp.link}`);
        }

        // Клик по кнопке копирования AnyDesk на первой карточке
        const copyBtn = await serverCards[0].$('.copy-anydesk-btn');
        await copyBtn.click();

        // Проверка тоста
        await page.waitForSelector('.toast-item', { visible: true });
        const toastText = await page.$eval('.toast-item', el => el.textContent.trim());
        console.log(`   - Всплывающий тост: "${toastText}"`);
        if (!toastText.includes('Скопировано в буфер') || !toastText.includes('1 170 691 894')) {
            throw new Error('Тост не содержит корректного сообщения о копировании AnyDesk ID');
        }

        // Проверка смены текста/галочки на кнопке
        const isCopiedClass = await copyBtn.evaluate(el => el.classList.contains('is-copied'));
        const btnText = await copyBtn.$eval('.copy-btn-text', el => el.textContent.trim());
        console.log(`   - Состояние кнопки: class contains 'is-copied' (${isCopiedClass}), текст: "${btnText}"`);
        if (!isCopiedClass) throw new Error('Кнопка должна содержать класс is-copied');
        if (!btnText.includes('Скопировано')) throw new Error('Кнопка должна менять текст на галочку/Скопировано');
        console.log('✓ Копирование AnyDesk в 1 клик с тостом и анимацией кнопки работает отлично!');

        // 7. Проверка переключения темы (Light/Dark) и адаптивности
        console.log(`[7/7] Проверка темы Dark/Light и адаптивности для 1080p, 2K, 4K...`);
        const themeBtn = await page.$('#themeToggle');
        if (themeBtn) {
            await themeBtn.click();
            const currentTheme = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
            console.log(`   - Переключение темы: data-theme="${currentTheme}"`);
            if (currentTheme !== 'dark') throw new Error('Тема должна переключиться на dark');
        }

        // Проверка разрешений
        const viewports = [
            { w: 1920, h: 1080, name: '1080p Full HD' },
            { w: 2560, h: 1440, name: '2K QHD' },
            { w: 3840, h: 2160, name: '4K UHD' }
        ];

        for (const vp of viewports) {
            await page.setViewport({ width: vp.w, height: vp.h });
            const hubWidth = await page.$eval('.command-hub', el => el.offsetWidth);
            console.log(`   - Разрешение ${vp.name} (${vp.w}x${vp.h}): .command-hub ширина = ${hubWidth}px (макс. 1240px)`);
            if (hubWidth > 1240) throw new Error(`Контейнер превышает 1240px на разрешении ${vp.name}`);
        }
        console.log('✓ Адаптивность и темная тема успешно проверены!');

        console.log('\n====================================================');
        console.log('  🎉 ВСЕ E2E ТЕСТЫ ЭТАПА 3 ПРОЙДЕНЫ УСПЕШНО (7/7)!');
        console.log('====================================================\n');

    } finally {
        if (browser) await browser.close();
        if (serverInstance) serverInstance.close();
    }
}

runE2ETests().catch(err => {
    console.error('❌ E2E Тест завершился с ошибкой:', err);
    process.exit(1);
});
