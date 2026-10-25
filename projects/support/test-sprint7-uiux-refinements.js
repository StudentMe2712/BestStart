const assert = require('assert');
const fs = require('fs');
const path = require('path');
const ejs = require('ejs');

console.log('====================================================');
console.log('  🧪 VERIFICATION: SPRINT 7 UI/UX REFINEMENTS');
console.log('====================================================\n');

const wikiEjs = fs.readFileSync(path.join(__dirname, 'views', 'wiki.ejs'), 'utf8');
const mainJs = fs.readFileSync(path.join(__dirname, 'public', 'js', 'main.js'), 'utf8');
const wikiCss = fs.readFileSync(path.join(__dirname, 'public', 'css', 'wiki-page.css'), 'utf8');

// =================================================================
// 1. FULL SUPPORT FOR .DOCX AND .DOC
// =================================================================
console.log('[1/4] Verifying .docx and .doc support...');

// 1.1 In views/wiki.ejs
assert(wikiEjs.includes("ext === 'docx' || ext === 'doc'"), 'wiki.ejs must detect docx and doc extensions');
assert(wikiEjs.includes("iconColor = '#2563eb'"), 'wiki.ejs must use Word blue #2563eb for doc/docx');
assert(wikiEjs.includes('accept=".xlsx,.xls,.csv,.pdf,.docx,.doc,.png,.jpg,.jpeg,.webp,.txt,.zip,.rdp,.xml"'), 'wiki.ejs file input must accept .docx,.doc');

// Test EJS rendering with sample docx and doc files
const sampleFiles = [
    { id: 201, org_id: 1, filename: 'contract.docx', original_name: 'Договор_ИТС.docx', file_size: 45000, mime_type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document' },
    { id: 202, org_id: 1, filename: 'legacy.doc', original_name: 'Архивная_Инструкция.doc', file_size: 98000, mime_type: 'application/msword' }
];

const renderedWiki = ejs.render(wikiEjs, {
    user: { username: 'admin' },
    organizations: [{ id: 1, name: 'АО «Астана-РЭК»' }],
    currentOrg: { id: 1, name: 'АО «Астана-РЭК»', content: 'Тест', updated_at: new Date() },
    databases: [],
    userPins: [],
    files: sampleFiles
});

assert(renderedWiki.includes('data-file-type="doc"'), 'Rendered docx/doc chip must have data-file-type="doc"');
assert(renderedWiki.includes('data-previewable="true"'), 'Rendered docx/doc chip must be previewable');
assert(renderedWiki.includes('color: #2563eb;'), 'Rendered docx/doc chip must use Word blue color #2563eb');

// 1.2 In public/js/main.js
assert(mainJs.includes("ext === 'docx' || ext === 'doc'"), 'main.js must detect docx and doc in getFileTypeInfo');
assert(mainJs.includes("iconColor = '#2563eb'"), 'main.js must set Word blue #2563eb');
assert(mainJs.includes("fileType === 'docx'"), 'main.js must handle fileType docx');
assert(mainJs.includes("data.type === 'docx' || data.type === 'doc'"), 'main.js must handle docx and doc preview types');
assert(mainJs.includes('Не удалось загрузить содержимое документа Word. Попробуйте скачать файл.'), 'main.js must provide friendly Word error message');
assert(mainJs.includes('lightbox-doc-wrap'), 'main.js must render .lightbox-doc-wrap container');
assert(mainJs.includes('lightbox-doc-content'), 'main.js must render .lightbox-doc-content container');
assert(mainJs.includes("data.type === 'doc_fallback'"), 'main.js must handle doc_fallback');
assert(mainJs.includes('lightbox-doc-fallback'), 'main.js must render .lightbox-doc-fallback');
assert(mainJs.includes('Скачать документ'), 'main.js fallback must include prominent download button');

// 1.3 In public/css/wiki-page.css
assert(wikiCss.includes('.lightbox-doc-wrap'), 'wiki-page.css must contain .lightbox-doc-wrap');
assert(wikiCss.includes('.lightbox-doc-content'), 'wiki-page.css must contain .lightbox-doc-content');
assert(wikiCss.includes('max-width: 860px'), 'wiki-page.css must set max-width: 860px for doc content');
assert(wikiCss.includes('min-height: 580px'), 'wiki-page.css must set min-height for paper sheet feel');
assert(wikiCss.includes('.lightbox-doc-fallback'), 'wiki-page.css must contain .lightbox-doc-fallback');

console.log('✓ Requirement 1 (.docx and .doc support) verified successfully.\n');

// =================================================================
// 2. 1:1 ORIGINAL RESOLUTION IMAGE PREVIEW IN LIGHTBOX
// =================================================================
console.log('[2/4] Verifying 1:1 original resolution image preview...');

// 2.1 Button markup in views/wiki.ejs
assert(renderedWiki.includes('id="lightboxZoomBtn"'), 'wiki.ejs must include #lightboxZoomBtn in lightbox controls');
assert(renderedWiki.includes('lightbox-btn--zoom'), 'wiki.ejs must have class lightbox-btn--zoom');
assert(renderedWiki.includes('Масштаб 1:1'), 'wiki.ejs must have zoom button label/title');

// 2.2 JS logic in public/js/main.js
assert(mainJs.includes('lightboxZoomBtn'), 'main.js must reference lightboxZoomBtn');
assert(mainJs.includes('is-actual-size'), 'main.js must toggle is-actual-size class');
assert(mainJs.includes('toggleImageZoom'), 'main.js must define toggleImageZoom');

// 2.3 CSS styling in public/css/wiki-page.css
assert(wikiCss.includes('.lightbox-btn--zoom'), 'wiki-page.css must style .lightbox-btn--zoom');
assert(wikiCss.includes('.lightbox-btn--zoom.is-active'), 'wiki-page.css must style active zoom button');
assert(wikiCss.includes('cursor: zoom-in'), 'wiki-page.css must set cursor: zoom-in on .lightbox-image');
assert(wikiCss.includes('.lightbox-image.is-actual-size'), 'wiki-page.css must style .lightbox-image.is-actual-size');
assert(wikiCss.includes('cursor: zoom-out'), 'wiki-page.css must set cursor: zoom-out for actual size');
assert(wikiCss.includes('max-width: none !important'), 'wiki-page.css must remove max-width constraints on actual size');

console.log('✓ Requirement 2 (1:1 image preview) verified successfully.\n');

// =================================================================
// 3. UNIFIED CONFIRMATION MODAL & REPLACE ALL CONFIRM()
// =================================================================
console.log('[3/4] Verifying unified confirmation modal & confirm() removal...');

// 3.1 Modal markup in views/wiki.ejs
assert(renderedWiki.includes('id="unifiedConfirmModal"'), 'wiki.ejs must include #unifiedConfirmModal');
assert(renderedWiki.includes('id="confirmModalOverlay"'), 'wiki.ejs must include #confirmModalOverlay');
assert(renderedWiki.includes('id="confirmDialogTitle"'), 'wiki.ejs must include #confirmDialogTitle');
assert(renderedWiki.includes('id="confirmDialogDesc"'), 'wiki.ejs must include #confirmDialogDesc');
assert(renderedWiki.includes('id="confirmDialogItemBox"'), 'wiki.ejs must include #confirmDialogItemBox');
assert(renderedWiki.includes('id="confirmDialogItemName"'), 'wiki.ejs must include #confirmDialogItemName');
assert(renderedWiki.includes('id="confirmDialogCancelBtn"'), 'wiki.ejs must include #confirmDialogCancelBtn');
assert(renderedWiki.includes('id="confirmDialogSubmitBtn"'), 'wiki.ejs must include #confirmDialogSubmitBtn');
assert(renderedWiki.includes('id="confirmDialogSpinner"'), 'wiki.ejs must include #confirmDialogSpinner');

// 3.2 Modal JS implementation in public/js/main.js
assert(mainJs.includes('window.showConfirmDialog = function'), 'main.js must define window.showConfirmDialog');
assert(mainJs.includes('confirmDialogSpinner'), 'main.js must manage confirmDialogSpinner');
assert(mainJs.includes('confirmDialogBtnText'), 'main.js must update confirmDialogBtnText');

// 3.3 Zero native confirm() calls in public/js/main.js
const confirmRegex = /\bconfirm\s*\(/g;
const matches = mainJs.match(confirmRegex);
assert.strictEqual(matches, null, 'main.js must NOT contain any native confirm() calls');

// 3.4 Replaced calls in attachments, org deletion, 1C database deletion
assert(mainJs.includes("title: 'Удалить вложение?'"), 'main.js must confirm file attachment deletion with showConfirmDialog');
assert(mainJs.includes("title: 'Удалить организацию?'"), 'main.js must confirm org deletion with showConfirmDialog');
assert(mainJs.includes("title: 'Удалить базу 1С?'"), 'main.js must confirm 1C database deletion with showConfirmDialog');

// 3.5 CSS styling in public/css/wiki-page.css
assert(wikiCss.includes('.confirm-modal'), 'wiki-page.css must contain .confirm-modal');
assert(wikiCss.includes('.confirm-modal__window'), 'wiki-page.css must contain .confirm-modal__window');
assert(wikiCss.includes('.confirm-modal__icon-wrap'), 'wiki-page.css must contain .confirm-modal__icon-wrap');
assert(wikiCss.includes('.btn-confirm-danger'), 'wiki-page.css must contain .btn-confirm-danger');
assert(wikiCss.includes('.btn-confirm-cancel'), 'wiki-page.css must contain .btn-confirm-cancel');
assert(wikiCss.includes('.btn-confirm-spinner'), 'wiki-page.css must contain .btn-confirm-spinner');

console.log('✓ Requirement 3 (Unified confirmation modal) verified successfully.\n');

// =================================================================
// 4. PIN BUTTON VISIBILITY REFINEMENT
// =================================================================
console.log('[4/4] Verifying pin button visibility refinement...');

// 4.1 Hidden by default for unpinned
assert(wikiCss.includes('.wiki-pin-btn {'), 'wiki-page.css must style .wiki-pin-btn');
assert(wikiCss.includes('opacity: 0;'), 'wiki-page.css must set opacity: 0 on unpinned pin button');
assert(wikiCss.includes('visibility: hidden;'), 'wiki-page.css must set visibility: hidden on unpinned pin button');
assert(wikiCss.includes('pointer-events: none;'), 'wiki-page.css must set pointer-events: none on unpinned pin button');

// 4.2 Visible on row hover and focus-within
assert(wikiCss.includes('.wiki-list-item:hover .wiki-pin-btn'), 'wiki-page.css must show pin button on item hover');
assert(wikiCss.includes('.wiki-list-item:focus-within .wiki-pin-btn'), 'wiki-page.css must show pin button on item focus-within');
assert(wikiCss.includes('opacity: 0.75;'), 'wiki-page.css must set opacity: 0.75 on hover');

// 4.3 Pinned state permanently visible
assert(wikiCss.includes('.wiki-pin-btn.is-pinned {'), 'wiki-page.css must style .wiki-pin-btn.is-pinned');
assert(wikiCss.includes('filter: drop-shadow(0 1px 2px rgba(239, 68, 68, 0.45));'), 'wiki-page.css must apply red drop-shadow on pinned pin button');

// 4.4 Touch device support
assert(wikiCss.includes('@media (hover: none)'), 'wiki-page.css must contain @media (hover: none)');

// 4.5 Layout stability: padding-right 42px on .wiki-list-link
assert(wikiCss.includes('padding: 8px 42px 8px 14px;'), 'wiki-page.css must set padding: 8px 42px 8px 14px on .wiki-list-link');

console.log('✓ Requirement 4 (Pin button refinement) verified successfully.\n');

console.log('====================================================');
console.log('  🎉 ALL SPRINT 7 UI/UX REQUIREMENTS PASSED!');
console.log('====================================================');
