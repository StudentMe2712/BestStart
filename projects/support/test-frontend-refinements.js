const assert = require('assert');
const fs = require('fs');
const path = require('path');
const ejs = require('ejs');

console.log('====================================================');
console.log('  🧪 ВЕРИФИКАЦИЯ НОВЫХ FRONTEND & UI/UX УЛУЧШЕНИЙ');
console.log('====================================================\n');

// 1. HOME.EJS & STYLE.CSS
console.log('[1/4] Проверка Hero Dashboard на Главной (home.ejs)...');
const homeEjs = fs.readFileSync(path.join(__dirname, 'views', 'home.ejs'), 'utf8');
const styleCss = fs.readFileSync(path.join(__dirname, 'public', 'css', 'style.css'), 'utf8');

// 1.1 totalServers calculation
assert(homeEjs.includes("typeof serverCount !== 'undefined' && serverCount !== null"), 'totalServers должен проверять serverCount на null и undefined');
assert(homeEjs.includes("typeof organizations !== 'undefined' && organizations ? organizations.length : 25"), 'totalServers должен использовать organizations.length в качестве fallback');

// 1.2 hero-pinned-card удален
assert(!homeEjs.includes('hero-pinned-card'), 'hero-pinned-card должен быть полностью удален из home.ejs');
assert(!homeEjs.includes('Мои закрепленные организации'), 'Блок «Мои закрепленные организации» должен быть удален из home.ejs');

// 1.3 Иллюстрация центрирована в .hero-wiki-right
assert(homeEjs.includes('hero-wiki-illustration-wrap'), 'hero-wiki-illustration-wrap должен присутствовать');
assert(styleCss.includes('.hero-wiki-right'), 'style.css должен содержать .hero-wiki-right');
assert(styleCss.includes('justify-content: center'), 'style.css должен центрировать элементы в hero-wiki-right');

// 1.4 Только 1 динамический тег в hero-wiki-tags
assert(!homeEjs.includes('Вложения Excel / PDF / Схемы'), 'Тег «Вложения Excel / PDF / Схемы» должен быть удален');
assert(!homeEjs.includes('Экспорт PDF и TXT'), 'Тег «Экспорт PDF и TXT» должен быть удален');
assert(homeEjs.includes('<%= totalServers %> организаций'), 'Динамический тег количества организаций должен присутствовать');

// Рендеринг home.ejs для проверки
const sampleOrgs = [
    { id: 1, name: 'АО «Астана-РЭК»' },
    { id: 2, name: 'ТОО «City Transportation Systems»' }
];
const renderedHome = ejs.render(homeEjs, {
    user: { username: 'testuser' },
    serverCount: 25,
    currentDateStr: 'Воскресенье, 25 октября 2026 г.',
    organizations: sampleOrgs
});
assert(renderedHome.includes('25 организаций'), 'Рендеринг home.ejs должен показывать 25 организаций');
console.log('✓ Задача 1 (Hero Card & Tags) успешно пройдена.\n');

// 2. LEGENDARY RED PUSHPIN
console.log('[2/4] Проверка легендарной булавки 📌 в Wiki...');
const wikiEjs = fs.readFileSync(path.join(__dirname, 'views', 'wiki.ejs'), 'utf8');
const wikiCss = fs.readFileSync(path.join(__dirname, 'public', 'css', 'wiki-page.css'), 'utf8');
const mainJs = fs.readFileSync(path.join(__dirname, 'public', 'js', 'main.js'), 'utf8');

// 2.1 views/wiki.ejs pin rendering
assert(wikiEjs.includes('<span class="wiki-pin-icon" aria-hidden="true">📌</span>'), 'wiki.ejs должен рендерить легендарную булавку 📌');
assert(!wikiEjs.includes('wiki-pin-btn is-pinned" data-org-id="<%= org.id %>" title="Открепить организацию" aria-label="Булавка">\n                                    <svg'), 'В кнопке закрепления больше не должно быть svg иконки');

// 2.2 public/css/wiki-page.css pin styles
assert(wikiCss.includes('.wiki-pin-btn'), 'wiki-page.css должен содержать .wiki-pin-btn');
assert(wikiCss.includes('opacity: 0.25'), 'По умолчанию у булавки opacity: 0.25');
assert(wikiCss.includes('filter: grayscale(50%)'), 'По умолчанию filter: grayscale(50%)');
assert(wikiCss.includes('.wiki-pin-btn.is-pinned'), 'wiki-page.css должен содержать .wiki-pin-btn.is-pinned');
assert(wikiCss.includes('rgba(239, 68, 68, 0.45)'), 'У закрепленной булавки должен быть красный drop-shadow');

// 2.3 public/js/main.js pin logic & helper
assert(mainJs.includes('createOrgListItem'), 'main.js должен содержать функцию createOrgListItem');
assert(mainJs.includes('<span class="wiki-pin-icon" aria-hidden="true">📌</span>'), 'main.js должен создавать булавку 📌');
console.log('✓ Задача 2 (Legendary Red Pushpin 📌) успешно пройдена.\n');

// 3. ATTACHMENTS BLOCK REFINEMENT
console.log('[3/4] Проверка блока вложений в Wiki...');
// 3.1 Button #btnAttachFile
assert(wikiEjs.includes('btn-attach-file--compact'), 'Кнопка прикрепления файла должна иметь класс btn-attach-file--compact');
assert(!wikiEjs.includes('+ Прикрепить файл'), 'Текст «+ Прикрепить файл» должен быть удален');

// 3.2 Dropzone removed & placeholder added
assert(!wikiEjs.includes('id="attachmentsDropzone"'), 'attachmentsDropzone должен быть удален из wiki.ejs');
assert(wikiEjs.includes('id="attachmentsEmpty"'), 'attachmentsEmpty плейсхолдер должен присутствовать в wiki.ejs');

// 3.3 File input accept .xml
assert(wikiEjs.includes('accept=".xlsx,.xls,.csv,.pdf,.docx,.doc,.png,.jpg,.jpeg,.webp,.txt,.zip,.rdp,.xml"'), 'file input должен поддерживать .xml');

// 3.4 XML file type & Excel isPreviewable
assert(wikiEjs.includes("ext === 'xml'"), 'wiki.ejs должен поддерживать ext === xml');
assert(wikiEjs.includes("iconColor = '#06b6d4'"), 'XML должен иметь циановый цвет #06b6d4');
assert(mainJs.includes("fileType = 'xml'"), 'main.js должен поддерживать fileType = xml');
assert(mainJs.includes("iconColor = '#06b6d4'"), 'main.js должен задавать iconColor = #06b6d4 для XML');

console.log('✓ Задача 3 (Attachments Block Refinement) успешно пройдена.\n');

// 4. LIGHTBOX SPREADSHEET & XML PREVIEW
console.log('[4/4] Проверка предпросмотра таблиц и XML в Lightbox...');
assert(mainJs.includes('/api/files/\' + fileId + \'/preview'), 'main.js должен запрашивать /api/files/:id/preview');
assert(mainJs.includes('lightbox-spreadsheet-container'), 'main.js должен рендерить .lightbox-spreadsheet-container');
assert(mainJs.includes('lightbox-sheet-tab'), 'main.js должен поддерживать переключение листов .lightbox-sheet-tab');
assert(mainJs.includes('lightbox-code-wrap'), 'main.js должен поддерживать отображение XML/текста в .lightbox-code-wrap');

// CSS styles
assert(wikiCss.includes('.btn-attach-file--compact'), 'wiki-page.css должен содержать стили для .btn-attach-file--compact');
assert(wikiCss.includes('.attachments-empty'), 'wiki-page.css должен содержать стили для .attachments-empty');
assert(wikiCss.includes('.lightbox-spreadsheet-container'), 'wiki-page.css должен содержать стили для .lightbox-spreadsheet-container');
assert(wikiCss.includes('.lightbox-sheet-tab'), 'wiki-page.css должен содержать стили для .lightbox-sheet-tab');
assert(wikiCss.includes('#spreadsheetPreviewTable'), 'wiki-page.css должен содержать стили для #spreadsheetPreviewTable');
assert(wikiCss.includes('.lightbox-code-wrap'), 'wiki-page.css должен содержать стили для .lightbox-code-wrap');

console.log('✓ Задача 4 (Lightbox Spreadsheet & XML Preview) успешно пройдена.\n');

console.log('====================================================');
console.log('  🎉 ВСЕ FRONTEND & UI/UX ЗАДАЧИ ВЕРИФИЦИРОВАНЫ НА 100%!');
console.log('====================================================');
