const assert = require('assert');
const fs = require('fs');
const path = require('path');
const ejs = require('ejs');

console.log('====================================================');
console.log('  🧪 ВЕРИФИКАЦИЯ FRONTEND ЗАДАЧ 1, 2, 3 СПРИНТА 7');
console.log('====================================================\n');

// -----------------------------------------------------------------
// ТЕСТ 1: Проверка CSS стилей и EJS разметки Hero Dashboard (Задача 1)
// -----------------------------------------------------------------
console.log('[1/3] Проверка Главной страницы и Hero Dashboard...');

const styleCss = fs.readFileSync(path.join(__dirname, 'public', 'css', 'style.css'), 'utf8');

// 1.1 Проверка .command-hub
assert(styleCss.includes('max-width: 1320px'), 'style.css должен содержать max-width: 1320px для .command-hub');
assert(styleCss.includes('width: 100%'), 'style.css должен содержать width: 100% для .command-hub');

// 1.2 Проверка .command-hero-wiki
assert(styleCss.includes('.command-hero-wiki'), 'style.css должен содержать класс .command-hero-wiki');
assert(styleCss.includes('min-height: 400px'), 'style.css должен содержать min-height: 400px для .command-hero-wiki');
assert(styleCss.includes('grid-template-columns: 1.25fr 1fr'), 'style.css должен содержать grid-template-columns: 1.25fr 1fr');
assert(styleCss.includes('gap: 48px'), 'style.css должен содержать gap: 48px');
assert(styleCss.includes('padding: 44px 48px'), 'style.css должен содержать padding: 44px 48px');
assert(styleCss.includes('[data-theme="dark"] .command-hero-wiki'), 'style.css должен содержать темную тему для .command-hero-wiki');

// 1.3 Рендеринг views/home.ejs с закрепленными организациями
const homeEjs = fs.readFileSync(path.join(__dirname, 'views', 'home.ejs'), 'utf8');

const sampleOrgs = [
    { id: 1, name: 'АО «Астана-РЭК»' },
    { id: 2, name: 'ТОО «City Transportation Systems»' },
    { id: 3, name: 'АО «Астана Теплотранзит»' },
    { id: 4, name: 'ГКП на ПХВ «Астанагенплан»' }
];

const htmlHomeWithPins = ejs.render(homeEjs, {
    user: { username: 'admin' },
    serverCount: 25,
    totalDatabasesCount: 4,
    currentDateStr: 'Воскресенье, 25 октября 2026 г.',
    userPins: [1, 2],
    pinnedOrgs: [sampleOrgs[0], sampleOrgs[1]],
    organizations: sampleOrgs
});

// Проверка элементов левой колонки
assert(htmlHomeWithPins.includes('Wiki — Серверная база знаний'), 'Должен быть заголовок Wiki — Серверная база знаний');
assert(htmlHomeWithPins.includes('Единое защищенное хранилище технической документации'), 'Должен быть абзац 1');
assert(htmlHomeWithPins.includes('Поддерживает ведение текстовых заметок, прикрепление таблиц Excel'), 'Должен быть абзац 2');
assert(htmlHomeWithPins.includes('25 организаций'), 'Должен быть бейдж с количеством организаций');
assert(htmlHomeWithPins.includes('Вложения Excel / PDF / Схемы'), 'Должен быть бейдж Вложения');
assert(htmlHomeWithPins.includes('Экспорт PDF и TXT'), 'Должен быть бейдж Экспорт');
assert(htmlHomeWithPins.includes('Перейти в базу знаний'), 'Должна быть кнопка перехода в Wiki');
assert(htmlHomeWithPins.includes('href="/wiki"'), 'Кнопка должна вести на /wiki');

// Проверка элементов правой колонки (закрепленные)
assert(htmlHomeWithPins.includes('hero-wiki-illustration'), 'Должна присутствовать SVG-иллюстрация стойки');
assert(htmlHomeWithPins.includes('Мои закрепленные организации 📌'), 'Должен быть заголовок закрепленных');
assert(htmlHomeWithPins.includes('/wiki/organization/1'), 'Должна быть ссылка на закрепленную организацию 1');
assert(htmlHomeWithPins.includes('/wiki/organization/2'), 'Должна быть ссылка на закрепленную организацию 2');

// 1.4 Рендеринг views/home.ejs БЕЗ закрепленных организаций (должен показать подсказку и первые 3 организации)
const htmlHomeNoPins = ejs.render(homeEjs, {
    user: { username: 'admin' },
    serverCount: 25,
    totalDatabasesCount: 4,
    currentDateStr: 'Воскресенье, 25 октября 2026 г.',
    userPins: [],
    pinnedOrgs: [],
    organizations: sampleOrgs
});

assert(htmlHomeNoPins.includes('Вы еще не закрепили организации. Закрепите булавкой 📌 в Wiki:'), 'При отсутствии закреплений должна выводиться подсказка');
assert(htmlHomeNoPins.includes('/wiki/organization/1'), 'Должен быть fallback на организацию 1');
assert(htmlHomeNoPins.includes('/wiki/organization/2'), 'Должен быть fallback на организацию 2');
assert(htmlHomeNoPins.includes('/wiki/organization/3'), 'Должен быть fallback на организацию 3');
assert(!htmlHomeNoPins.includes('/wiki/organization/4'), 'Организация 4 не должна входить в топ-3 fallback');

console.log('✓ Задача 1 (Home Hero Dashboard) успешно верифицирована.\n');

// -----------------------------------------------------------------
// ТЕСТ 2: Проверка сайдбара и закрепления организаций (Задача 2)
// -----------------------------------------------------------------
console.log('[2/3] Проверка сайдбара и механизма закреплений (Wiki Pinning)...');

const wikiCss = fs.readFileSync(path.join(__dirname, 'public', 'css', 'wiki-page.css'), 'utf8');
const mainJs = fs.readFileSync(path.join(__dirname, 'public', 'js', 'main.js'), 'utf8');
const wikiEjs = fs.readFileSync(path.join(__dirname, 'views', 'wiki.ejs'), 'utf8');

// 2.1 CSS для закреплений
assert(wikiCss.includes('.wiki-pin-btn'), 'wiki-page.css должен содержать класс .wiki-pin-btn');
assert(wikiCss.includes('.wiki-pin-btn.is-pinned'), 'wiki-page.css должен содержать активный класс .wiki-pin-btn.is-pinned');
assert(wikiCss.includes('.wiki-list-section-header'), 'wiki-page.css должен содержать .wiki-list-section-header');

// 2.2 Рендеринг views/wiki.ejs с закрепленными организациями
const htmlWikiWithPins = ejs.render(wikiEjs, {
    user: { username: 'admin' },
    organizations: sampleOrgs,
    currentOrg: sampleOrgs[0],
    databases: [],
    userPins: [2], // Закреплена организация 2
    files: []
});

assert(htmlWikiWithPins.includes('ЗАКРЕПЛЕННЫЕ'), 'Должен отображаться заголовок секции ЗАКРЕПЛЕННЫЕ');
assert(htmlWikiWithPins.includes('ВСЕ ОРГАНИЗАЦИИ'), 'Должен отображаться заголовок секции ВСЕ ОРГАНИЗАЦИИ');
assert(htmlWikiWithPins.includes('wiki-pin-btn is-pinned'), 'У закрепленной организации должен быть класс is-pinned');
assert(htmlWikiWithPins.includes('data-org-id="2"'), 'У кнопки должен быть data-org-id="2"');

// 2.3 Рендеринг views/wiki.ejs без закреплений
const htmlWikiNoPins = ejs.render(wikiEjs, {
    user: { username: 'admin' },
    organizations: sampleOrgs,
    currentOrg: sampleOrgs[0],
    databases: [],
    userPins: [],
    files: []
});

assert(!htmlWikiNoPins.includes('ЗАКРЕПЛЕННЫЕ'), 'При отсутствии закреплений не должно быть заголовка ЗАКРЕПЛЕННЫЕ');

// 2.4 JavaScript логика в main.js
assert(mainJs.includes('/api/organizations/'), 'main.js должен содержать вызов API закреплений');
assert(mainJs.includes('/pin'), 'main.js должен обращаться к эндпоинту /pin');
assert(mainJs.includes('reorganizeWikiSidebar'), 'main.js должен содержать функцию реорганизации сайдбара reorganizeWikiSidebar');

console.log('✓ Задача 2 (Wiki Sidebar Pinning) успешно верифицирована.\n');

// -----------------------------------------------------------------
// ТЕСТ 3: Проверка блока вложений и Lightbox модалки (Задача 3)
// -----------------------------------------------------------------
console.log('[3/3] Проверка вложений файлов и Lightbox модалки...');

// 3.1 CSS стили вложений и Lightbox
assert(wikiCss.includes('.wiki-attachments-section'), 'wiki-page.css должен содержать .wiki-attachments-section');
assert(wikiCss.includes('.attachments-dropzone'), 'wiki-page.css должен содержать .attachments-dropzone');
assert(wikiCss.includes('.file-chip'), 'wiki-page.css должен содержать .file-chip');
assert(wikiCss.includes('.file-lightbox-modal'), 'wiki-page.css должен содержать .file-lightbox-modal');
assert(wikiCss.includes('.lightbox-window'), 'wiki-page.css должен содержать .lightbox-window');
assert(wikiCss.includes('.lightbox-image'), 'wiki-page.css должен содержать .lightbox-image');
assert(wikiCss.includes('.lightbox-iframe'), 'wiki-page.css должен содержать .lightbox-iframe');

// 3.2 Рендеринг файлов в views/wiki.ejs
const sampleFiles = [
    { id: 101, org_id: 1, filename: 'table.xlsx', original_name: 'Финансы_2026.xlsx', file_size: 45000, mime_type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' },
    { id: 102, org_id: 1, filename: 'diagram.pdf', original_name: 'Архитектура_Сети.pdf', file_size: 2500000, mime_type: 'application/pdf' },
    { id: 103, org_id: 1, filename: 'screen.png', original_name: 'Скриншот_Сервера.png', file_size: 150000, mime_type: 'image/png' },
    { id: 104, org_id: 1, filename: 'doc.docx', original_name: 'Регламент_Резервирования.docx', file_size: 78000, mime_type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document' },
    { id: 105, org_id: 1, filename: 'backup.zip', original_name: 'Конфиги.zip', file_size: 1048576, mime_type: 'application/zip' }
];

const htmlWikiWithFiles = ejs.render(wikiEjs, {
    user: { username: 'admin' },
    organizations: sampleOrgs,
    currentOrg: sampleOrgs[0],
    databases: [],
    userPins: [],
    files: sampleFiles
});

assert(htmlWikiWithFiles.includes('wiki-attachments-section'), 'HTML должен содержать wiki-attachments-section');
assert(htmlWikiWithFiles.includes('Прикрепленные файлы'), 'HTML должен содержать заголовок Прикрепленные файлы');
assert(htmlWikiWithFiles.includes('filesCountBadge">5</span>'), 'Бейдж количества файлов должен показывать 5');
assert(htmlWikiWithFiles.includes('btnAttachFile'), 'Должна присутствовать кнопка btnAttachFile');
assert(htmlWikiWithFiles.includes('attachmentsDropzone'), 'Должна присутствовать область attachmentsDropzone');
assert(htmlWikiWithFiles.includes('Финансы_2026.xlsx'), 'Должен рендериться файл xlsx');
assert(htmlWikiWithFiles.includes('Архитектура_Сети.pdf'), 'Должен рендериться файл pdf');
assert(htmlWikiWithFiles.includes('Скриншот_Сервера.png'), 'Должен рендериться файл png');
assert(htmlWikiWithFiles.includes('file-lightbox-modal'), 'Должно присутствовать Lightbox модальное окно');
assert(htmlWikiWithFiles.includes('lightboxCloseBtn'), 'Lightbox должен содержать кнопку закрытия');

// 3.3 JS логика Drag & Drop, загрузки, удаления и Lightbox в main.js
assert(mainJs.includes('initAttachmentsAndLightbox'), 'main.js должен содержать initAttachmentsAndLightbox');
assert(mainJs.includes('attachmentsDropzone'), 'main.js должен взаимодействовать с attachmentsDropzone');
assert(mainJs.includes('openLightbox'), 'main.js должен содержать функцию openLightbox');
assert(mainJs.includes('/api/files/'), 'main.js должен совершать запросы к /api/files/');
assert(mainJs.includes('closeLightbox'), 'main.js должен закрывать lightbox');

console.log('✓ Задача 3 (Attachments Block & Lightbox Modal) успешно верифицирована.\n');

console.log('====================================================');
console.log('  🎉 ВСЕ ФРОНТЕНД ЗАДАЧИ 1, 2, 3 УСПЕШНО ВЫПОЛНЕНЫ!');
console.log('====================================================');
