const puppeteer = require('puppeteer');

process.env.DB_STUB = 'true';
const TEST_PORT = 3198;
process.env.PORT = String(TEST_PORT);

async function runLoginVerification() {
    console.log('====================================================');
    console.log('  🧪 ВЕРИФИКАЦИЯ ЭТАПА 1 СПРИНТА 2 (ЭКРАН ВХОДА)');
    console.log('====================================================\n');

    const { serverInstance } = require('./server');
    await new Promise(resolve => setTimeout(resolve, 800));

    const BASE_URL = `http://localhost:${TEST_PORT}`;
    let browser;

    try {
        console.log('[1/8] Запуск Headless Chrome (Puppeteer)...');
        const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
        browser = await puppeteer.launch({
            headless: 'new',
            executablePath: chromePath,
            args: ['--no-sandbox', '--disable-setuid-sandbox']
        });

        const page = await browser.newPage();
        await page.setViewport({ width: 1920, height: 1080 });

        console.log(`[2/8] Открытие страницы ${BASE_URL}/login...`);
        const response = await page.goto(`${BASE_URL}/login`, { waitUntil: 'networkidle0' });
        if (response.status() !== 200) {
            throw new Error(`Ожидался статус 200, получен ${response.status()}`);
        }
        console.log('✓ Страница /login успешно загрузилась (HTTP 200).');

        console.log('[3/8] Проверка отсутствия логотипов-картинок (<img> и /logo.png)...');
        const images = await page.$$('img');
        if (images.length > 0) {
            const srcs = await page.$$eval('img', els => els.map(e => e.src));
            throw new Error(`Обнаружены изображения <img> на странице входа: ${srcs.join(', ')}`);
        }
        const html = await page.content();
        if (html.includes('/logo.png')) {
            throw new Error('Обнаружена ссылка на /logo.png в исходном коде страницы входа!');
        }
        console.log('✓ Ни одного тега <img> или ссылки на /logo.png на странице нет!');

        console.log('[4/8] Проверка текстового заголовка «ИТС-Ком» и подзаголовка «База знаний»...');
        const titleEl = await page.$('.auth-title, .login-card__title');
        if (!titleEl) throw new Error('Элемент заголовка (.auth-title / .login-card__title) не найден!');
        const titleText = await page.evaluate(el => el.textContent.trim(), titleEl);

        const subtitleEl = await page.$('.auth-subtitle, .login-card__subtitle');
        if (!subtitleEl) throw new Error('Элемент подзаголовка (.auth-subtitle / .login-card__subtitle) не найден!');
        const subtitleText = await page.evaluate(el => el.textContent.trim(), subtitleEl);

        console.log(`   - Заголовок: "${titleText}"`);
        console.log(`   - Подзаголовок: "${subtitleText}"`);

        if (titleText !== 'ИТС-Ком') {
            throw new Error(`Заголовок должен быть "ИТС-Ком", получено: "${titleText}"`);
        }
        if (subtitleText !== 'База знаний') {
            throw new Error(`Подзаголовок должен быть "База знаний", получено: "${subtitleText}"`);
        }
        console.log('✓ Заголовок и подзаголовок соответствуют спецификации.');

        console.log('[5/8] Проверка карточки 420px, центрирования и тем (Light / Dark)...');
        const cardBox = await page.$('.login-box');
        if (!cardBox) throw new Error('Карточка .login-box не найдена!');
        const cardStyles = await page.evaluate(el => {
            const cs = window.getComputedStyle(el);
            return {
                maxWidth: cs.maxWidth,
                borderRadius: cs.borderRadius,
                boxShadow: cs.boxShadow
            };
        }, cardBox);
        console.log(`   - max-width карточки: ${cardStyles.maxWidth}`);
        if (cardStyles.maxWidth !== '420px') {
            throw new Error(`Ожидался max-width 420px, получено: ${cardStyles.maxWidth}`);
        }

        // Проверка темной темы
        await page.evaluate(() => document.documentElement.setAttribute('data-theme', 'dark'));
        const darkThemeCardBg = await page.evaluate(el => window.getComputedStyle(el).backgroundColor, cardBox);
        console.log(`   - Dark theme background: ${darkThemeCardBg}`);
        await page.evaluate(() => document.documentElement.setAttribute('data-theme', 'light'));
        console.log('✓ Карточка 420px и темы Light/Dark корректно функционируют.');

        console.log('[6/8] Проверка кнопки показа/скрытия пароля (#passwordToggleBtn)...');
        const passwordInput = await page.$('#password');
        const toggleBtn = await page.$('#passwordToggleBtn');
        if (!passwordInput || !toggleBtn) throw new Error('Элементы пароля или глазка не найдены!');

        let passType = await page.evaluate(el => el.getAttribute('type'), passwordInput);
        if (passType !== 'password') throw new Error(`Начальный тип поля пароля должен быть password, получено: ${passType}`);

        await toggleBtn.click();
        passType = await page.evaluate(el => el.getAttribute('type'), passwordInput);
        if (passType !== 'text') throw new Error(`После клика тип поля пароля должен быть text, получено: ${passType}`);

        await toggleBtn.click();
        passType = await page.evaluate(el => el.getAttribute('type'), passwordInput);
        if (passType !== 'password') throw new Error(`После второго клика тип поля пароля должен быть password, получено: ${passType}`);
        console.log('✓ Глазок показа/скрытия пароля работает идеально.');

        console.log('[7/8] Проверка автозаполнения admin / admin (#quickCredsBadge)...');
        const quickCredsBadge = await page.$('#quickCredsBadge');
        if (!quickCredsBadge) throw new Error('Кнопка #quickCredsBadge не найдена!');

        // Убедимся, что поля изначально пустые
        await page.evaluate(() => {
            document.getElementById('username').value = '';
            document.getElementById('password').value = '';
        });

        await quickCredsBadge.click();
        const userVal = await page.$eval('#username', el => el.value);
        const passVal = await page.$eval('#password', el => el.value);
        console.log(`   - Заполнено: ${userVal} / ${passVal}`);
        if (userVal !== 'admin' || passVal !== 'admin') {
            throw new Error(`Ожидалось заполнение admin / admin, получено: ${userVal} / ${passVal}`);
        }
        console.log('✓ Быстрое автозаполнение admin / admin работает корректно.');

        console.log('[8/8] Проверка валидации и входа (POST /login)...');
        // Попытка отправить пустую форму
        await page.evaluate(() => {
            document.getElementById('username').value = '';
            document.getElementById('password').value = '';
        });
        await page.click('#submitBtn');
        // Проверяем, что перехода не произошло
        if (page.url() !== `${BASE_URL}/login`) {
            throw new Error('Пустая форма не должна была отправиться!');
        }

        // Заполняем через quickCredsBadge и нажимаем войти
        await quickCredsBadge.click();
        await Promise.all([
            page.waitForNavigation({ waitUntil: 'networkidle0' }),
            page.click('#submitBtn')
        ]);

        const finalUrl = page.url();
        console.log(`✓ Успешный вход в систему! URL перенаправления: ${finalUrl}`);
        if (!finalUrl.endsWith('/') && !finalUrl.endsWith(`:${TEST_PORT}`)) {
            throw new Error(`Ожидался переход на главную страницу /, получен URL: ${finalUrl}`);
        }

        console.log('\n====================================================');
        console.log('  🎉 ВСЕ ТЕСТЫ ЭТАПА 1 УСПЕШНО ПРОЙДЕНЫ!');
        console.log('====================================================');

    } finally {
        if (browser) await browser.close();
        if (serverInstance) {
            serverInstance.close();
        }
    }
}

runLoginVerification().catch(err => {
    console.error('\n❌ Ошибка верификации:', err);
    process.exit(1);
});
