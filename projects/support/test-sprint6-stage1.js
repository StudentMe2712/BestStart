const fs = require('fs');
const path = require('path');
const ejs = require('ejs');
const assert = require('assert');

async function testStage1() {
    console.log('====================================================');
    console.log('  🧪 ВЕРИФИКАЦИЯ ЭТАПА 1 СПРИНТА 6');
    console.log('====================================================\n');

    let allPassed = true;

    // 1. Проверка login.ejs рендеринга при isStubMode = false
    console.log('[1/5] Проверка login.ejs при isStubMode = false (хостинг Plesk)...');
    const loginEjsPath = path.join(__dirname, 'views', 'login.ejs');
    const loginTemplate = fs.readFileSync(loginEjsPath, 'utf8');

    const renderedPlesk = ejs.render(loginTemplate, {
        error: null,
        isStubMode: false
    }, { filename: loginEjsPath });

    if (renderedPlesk.includes('id="quickCredsBadge"') || renderedPlesk.includes('quick-creds-badge')) {
        console.error('❌ ОШИБКА: #quickCredsBadge отображается при isStubMode=false!');
        allPassed = false;
    } else {
        console.log('✓ Успех: #quickCredsBadge полностью скрыт при isStubMode=false.');
    }

    // 2. Проверка login.ejs рендеринга при isStubMode = true
    console.log('[2/5] Проверка login.ejs при isStubMode = true (локальный dev-режим)...');
    const renderedDev = ejs.render(loginTemplate, {
        error: null,
        isStubMode: true
    }, { filename: loginEjsPath });

    if (!renderedDev.includes('id="quickCredsBadge"') || !renderedDev.includes('admin / admin')) {
        console.error('❌ ОШИБКА: #quickCredsBadge не найден при isStubMode=true!');
        allPassed = false;
    } else {
        console.log('✓ Успех: #quickCredsBadge отображается при isStubMode=true.');
    }

    // 3. Проверка server.js проброса флага isStubMode
    console.log('[3/5] Проверка проброса isStubMode в server.js...');
    const serverJsContent = fs.readFileSync(path.join(__dirname, 'server.js'), 'utf8');
    const hasGetStub = serverJsContent.includes("res.render('login', { error: null, isStubMode: USE_STUB })");
    const hasPostStub = serverJsContent.includes("isStubMode: USE_STUB");

    if (hasGetStub && hasPostStub) {
        console.log('✓ Успех: server.js корректно пробрасывает isStubMode: USE_STUB в GET и POST /login.');
    } else {
        console.error('❌ ОШИБКА: server.js не содержит требуемой передачи isStubMode: USE_STUB!');
        allPassed = false;
    }

    // 4. Проверка формы #db1cForm и autocomplete="off" в views/wiki.ejs
    console.log('[4/5] Проверка autocomplete="off" и цвета кнопки в views/wiki.ejs...');
    const wikiEjsContent = fs.readFileSync(path.join(__dirname, 'views', 'wiki.ejs'), 'utf8');

    const hasFormAutocomplete = /<form[^>]*id="db1cForm"[^>]*autocomplete="off"/.test(wikiEjsContent) ||
                                /<form[^>]*autocomplete="off"[^>]*id="db1cForm"/.test(wikiEjsContent);
    const hasDbNameInput = wikiEjsContent.includes('id="dbNameInput"') && wikiEjsContent.includes('autocomplete="off"');
    const hasDbDescAutocomplete = /id="dbDescription"[^>]*autocomplete="off"/.test(wikiEjsContent) ||
                                  /autocomplete="off"[^>]*id="dbDescription"/.test(wikiEjsContent);
    const hasDbDateAutocomplete = /id="dbDate"[^>]*autocomplete="off"/.test(wikiEjsContent) ||
                                  /autocomplete="off"[^>]*id="dbDate"/.test(wikiEjsContent);
    const hasBtnPrimary = /<button[^>]*id="saveDb1cBtn"[^>]*class="[^"]*btn-primary[^"]*"/.test(wikiEjsContent) ||
                          /<button[^>]*class="[^"]*btn-primary[^"]*"[^>]*id="saveDb1cBtn"/.test(wikiEjsContent);
    const hasNotBtnSuccess = !/<button[^>]*id="saveDb1cBtn"[^>]*class="[^"]*btn-success[^"]*"/.test(wikiEjsContent);

    if (hasFormAutocomplete && hasDbNameInput && hasDbDescAutocomplete && hasDbDateAutocomplete) {
        console.log('✓ Успех: Форма #db1cForm и поля ввода имеют autocomplete="off".');
    } else {
        console.error('❌ ОШИБКА: Поля модалки базы 1С не содержат autocomplete="off"!');
        allPassed = false;
    }

    if (hasBtnPrimary && hasNotBtnSuccess) {
        console.log('✓ Успех: Кнопка #saveDb1cBtn имеет класс .btn-primary и лишена .btn-success.');
    } else {
        console.error('❌ ОШИБКА: Кнопка #saveDb1cBtn не имеет класса .btn-primary или все еще имеет .btn-success!');
        allPassed = false;
    }

    // 5. Проверка стилей в wiki-page.css и style.css
    console.log('[5/5] Проверка CSS-стилей кнопки и переключателя темы...');
    const wikiCssContent = fs.readFileSync(path.join(__dirname, 'public', 'css', 'wiki-page.css'), 'utf8');
    const styleCssContent = fs.readFileSync(path.join(__dirname, 'public', 'css', 'style.css'), 'utf8');

    const hasSaveBtnStyles = wikiCssContent.includes('#saveDb1cBtn') && wikiCssContent.includes('--brand-primary');
    const hasSvgThemeBtn = styleCssContent.includes('.theme-icon--sun') &&
                           styleCssContent.includes('.theme-icon--moon') &&
                           !styleCssContent.includes('.theme-toggle-btn::before');

    if (hasSaveBtnStyles) {
        console.log('✓ Успех: #saveDb1cBtn стилизована под --brand-primary в wiki-page.css.');
    } else {
        console.error('❌ ОШИБКА: Стили #saveDb1cBtn не содержат --brand-primary в wiki-page.css!');
        allPassed = false;
    }

    if (hasSvgThemeBtn) {
        console.log('✓ Успех: Переключатель темы использует аккуратные векторные SVG-иконки, старый псевдоэлемент ::before удален.');
    } else {
        console.error('❌ ОШИБКА: Переключатель темы все еще использует устаревший псевдоэлемент ::before или скрывает SVG!');
        allPassed = false;
    }

    console.log('\n----------------------------------------------------');
    if (allPassed) {
        console.log('🎉 ВСЕ ПРОВЕРКИ ЭТАПА 1 СПРИНТА 6 УСПЕШНО ПРОЙДЕНЫ!');
        process.exit(0);
    } else {
        console.log('❌ ЧАСТЬ ПРОВЕРОК НЕ ПРОЙДЕНА.');
        process.exit(1);
    }
}

testStage1().catch(err => {
    console.error('Фатальная ошибка тестов:', err);
    process.exit(1);
});
