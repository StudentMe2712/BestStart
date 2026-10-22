const puppeteer = require('puppeteer');

process.env.DB_STUB = 'true';
const TEST_PORT = 3206;
process.env.PORT = String(TEST_PORT);

async function runStage3Tests() {
    console.log('====================================================');
    console.log('  🧪 ТЕСТИРОВАНИЕ СПРИНТ 6: ЭТАП 3 (МОНОГРАММЫ И UI WIKI)');
    console.log('====================================================\n');

    const { serverInstance, getOrgMonogram } = require('./server');
    await new Promise(resolve => setTimeout(resolve, 800));

    // 1. Модульный тест функции формирования монограмм
    console.log('[1/4] Модульное тестирование getOrgMonogram()...');
    const cases = [
        { name: 'ALPHUTA', expected: 'AL' },
        { name: 'Antarium', expected: 'AN' },
        { name: 'ARG', expected: 'AR' },
        { name: 'Автоперекресток', expected: 'АВ' },
        { name: ' Continental Logistics ', expected: 'CL' },
        { name: 'Garden Schools', expected: 'GS' },
        { name: 'Акмола-Бетон', expected: 'АБ' },
        { name: 'АстанаГенПлан', expected: 'АП' }
    ];

    for (const c of cases) {
        const result = getOrgMonogram(c.name);
        if (result !== c.expected) {
            throw new Error(`Ошибка монограммы для "${c.name}": ожидалось "${c.expected}", получено "${result}"`);
        }
        console.log(`   ✓ "${c.name}" -> [${result}] (совпадает с "${c.expected}")`);
    }
    console.log('   ✓ Все монограммы сформированы корректно!\n');

    const BASE_URL = `http://localhost:${TEST_PORT}`;
    let browser;

    try {
        console.log('[2/4] Запуск Puppeteer Chrome...');
        const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
        browser = await puppeteer.launch({
            headless: 'new',
            executablePath: chromePath,
            args: ['--no-sandbox', '--disable-setuid-sandbox']
        });

        const page = await browser.newPage();
        await page.setViewport({ width: 1920, height: 1080 });

        // Авторизация
        await page.goto(`${BASE_URL}/login`, { waitUntil: 'networkidle0' });
        await page.type('#username', 'admin');
        await page.type('#password', 'admin');
        await Promise.all([
            page.waitForNavigation({ waitUntil: 'networkidle0' }),
            page.click('#submitBtn')
        ]);
        console.log('   ✓ Авторизация успешна');

        // Переход в карточку ALPHUTA (/wiki/organization/1)
        console.log('\n[3/4] Проверка карточки /wiki/organization/1 (ALPHUTA)...');
        await page.goto(`${BASE_URL}/wiki/organization/1`, { waitUntil: 'networkidle0' });

        // Аватар-монограмма
        const avatarEl = await page.$('.org-avatar.org-avatar--monogram');
        if (!avatarEl) {
            throw new Error('Элемент .org-avatar.org-avatar--monogram не найден в DOM!');
        }

        const monogramText = await page.$eval('.org-avatar--monogram .org-monogram-text', el => el.textContent.trim());
        if (monogramText !== 'AL') {
            throw new Error(`Ожидалась монограмма "AL", получено: "${monogramText}"`);
        }
        console.log(`   ✓ Бейдж-монограмма компании отображает "${monogramText}"`);

        // Проверка отсутствия старого силуэта человека
        const oldSilhouette = await page.$('.org-avatar circle[fill="#dde3ea"]');
        if (oldSilhouette) {
            throw new Error('Обнаружен устаревший серый силуэт человека в .org-avatar!');
        }
        console.log('   ✓ Серый силуэт человека удален');

        // Проверка стилей аватара
        const avatarStyles = await page.$eval('.org-avatar--monogram', el => {
            const cs = window.getComputedStyle(el);
            return {
                width: cs.width,
                height: cs.height,
                borderRadius: cs.borderRadius,
                background: cs.background
            };
        });
        console.log(`   - Размеры аватара: ${avatarStyles.width}x${avatarStyles.height}, radius: ${avatarStyles.borderRadius}`);
        if (avatarStyles.width !== '44px' || avatarStyles.height !== '44px') {
            throw new Error(`Ожидался размер 44x44px, получено: ${avatarStyles.width}x${avatarStyles.height}`);
        }

        // Проверка табов на наличие SVG и отсутствие эмодзи
        console.log('\n[4/4] Проверка SVG-иконок в табах и отсутствия номеров строк...');
        const tabDataHtml = await page.$eval('#tabBtnData', el => el.innerHTML);
        const tab1cHtml = await page.$eval('#tabBtn1c', el => el.innerHTML);

        if (!tabDataHtml.includes('<svg') || !tab1cHtml.includes('<svg')) {
            throw new Error('В табах отсутствуют SVG-иконки!');
        }
        if (tabDataHtml.includes('📝') || tabDataHtml.includes('✏️') || tab1cHtml.includes('📦')) {
            throw new Error('В навигационных табах обнаружены недопустимые эмодзи!');
        }
        console.log('   ✓ В табах «Данные» и «Базы 1С» установлены строгие контурные SVG-иконки, эмодзи удалены');

        // Проверка отсутствия .wiki-line-num в .wiki-sheet
        await page.waitForSelector('.wiki-sheet .wiki-line');
        const lineNums = await page.$$('.wiki-sheet .wiki-line-num');
        if (lineNums.length > 0) {
            throw new Error(`В блоке .wiki-sheet обнаружены элементы .wiki-line-num (${lineNums.length} шт.), они должны отсутствовать!`);
        }

        const linesCount = await page.$$eval('.wiki-sheet .wiki-line', els => els.length);
        if (linesCount === 0) {
            throw new Error('Строки заметок в .wiki-sheet не отрендерились!');
        }
        console.log(`   ✓ В блоке .wiki-sheet отсутствуют номера строк (отрендерено ${linesCount} чистых моноширинных строк)`);

        console.log('\n====================================================');
        console.log('  🎉 ВСЕ ТЕСТЫ ЭТАПА 3 СПРИНТА 6 УСПЕШНО ПРОЙДЕНЫ!');
        console.log('====================================================');

    } finally {
        if (browser) await browser.close();
        if (serverInstance) serverInstance.close();
    }
}

runStage3Tests().catch(err => {
    console.error('\n❌ Ошибка тестирования:', err.message);
    process.exit(1);
});
