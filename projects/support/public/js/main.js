/* ============================================================
   WIKI — main.js (Smart Server Viewer & Enterprise Wiki)
   ============================================================ */

const API_BASE = '/api/organizations';

// ── Modal: create org ──────────────────────────────────────
const addOrgBtn      = document.getElementById('addOrgBtn');
const addOrgModal    = document.getElementById('addOrgModal');
const closeModal     = document.getElementById('closeModal');
const cancelModalBtn = document.getElementById('cancelModalBtn');
const createOrgBtn   = document.getElementById('createOrgBtn');
const newOrgName     = document.getElementById('newOrgName');
const newOrgContent  = document.getElementById('newOrgContent');

// ── Modal: delete org ──────────────────────────────────────
const deleteOrgModal  = document.getElementById('deleteOrgModal');
const closeDeleteModal= document.getElementById('closeDeleteModal');
const cancelDeleteBtn = document.getElementById('cancelDeleteBtn');
const confirmDeleteBtn= document.getElementById('confirmDeleteBtn');
const deleteOrgSelect = document.getElementById('deleteOrgSelect');
const deleteOrgId     = document.getElementById('deleteOrgId');
const deleteOrgName   = document.getElementById('deleteOrgName');
const deleteBtnMore   = document.getElementById('deleteBtnMore');

// ── Editor & Viewer ────────────────────────────────────────
const editBtnTop             = document.getElementById('editBtnTop');
const orgEditActions         = document.getElementById('orgEditActions');
const saveBtn                = document.getElementById('saveBtn');
const cancelEditBtn          = document.getElementById('cancelEditBtn');
const currentOrgId           = document.getElementById('currentOrgId');
const orgNameInput           = document.getElementById('orgNameInput');
const orgEditor              = document.getElementById('orgEditor');
const editorOuter            = document.getElementById('editorOuter');
const editorToolbar          = document.getElementById('editorToolbar');
const editorBottomBar        = document.getElementById('editorBottomBar');
const draftStatus            = document.getElementById('draftStatus');
const formatSelect           = document.getElementById('formatSelect');
const linkBtn                = document.getElementById('linkBtn');
const wikiSheet              = document.getElementById('wikiSheet');
const floatingToastContainer = document.getElementById('floatingToastContainer');

// Backwards compatibility aliases
const smartServerViewer      = wikiSheet;

// ── More menu ──────────────────────────────────────────────
const moreBtnTop = document.getElementById('moreBtnTop');
const moreMenu   = document.getElementById('moreMenu');

// ── Search & Filter ────────────────────────────────────────
const wikiSearch          = document.getElementById('wikiSearch');
const wikiSearchClear     = document.getElementById('wikiSearchClear');
const wikiCountBadge      = document.getElementById('wikiCountBadge');
const wikiSearchNoResults = document.getElementById('wikiSearchNoResults');
const wikiOrgList         = document.getElementById('wikiOrgList');

var isEditMode = false;
var activeServerData = null;

function _t(key) { return window.ITS_I18n ? window.ITS_I18n.t(key) : key; }

/* ============================================================
   TOAST NOTIFICATIONS
   ============================================================ */

function showToast(message, type) {
    var container = document.getElementById('floatingToastContainer');
    if (!container) {
        container = document.createElement('div');
        container.className = 'floating-toast-container';
        container.id = 'floatingToastContainer';
        document.body.appendChild(container);
    }

    var toast = document.createElement('div');
    toast.className = 'floating-toast';
    toast.setAttribute('role', 'alert');
    toast.innerHTML =
        '<svg class="floating-toast__icon" viewBox="0 0 20 20" fill="none">' +
        '<circle cx="10" cy="10" r="9" stroke="currentColor" stroke-width="1.6"/>' +
        '<path d="M6 10L8.5 12.5L14 7" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>' +
        '</svg>' +
        '<span>' + escapeHtmlText(message) + '</span>';

    container.appendChild(toast);

    // Force reflow for CSS transition, then add is-show
    void toast.offsetHeight;
    toast.classList.add('is-show');

    setTimeout(function() {
        toast.classList.remove('is-show');
        setTimeout(function() {
            if (toast.parentNode) toast.parentNode.removeChild(toast);
        }, 300);
    }, 2500);
}

/* ============================================================
   UNIFIED CONFIRMATION MODAL (Sprint 7)
   ============================================================ */

window.showConfirmDialog = function(options) {
    options = options || {};
    var modal = document.getElementById('unifiedConfirmModal');
    if (!modal) {
        if (typeof options.onConfirm === 'function') options.onConfirm();
        return;
    }

    var overlay = document.getElementById('confirmModalOverlay');
    var titleEl = document.getElementById('confirmDialogTitle');
    var descEl = document.getElementById('confirmDialogDesc');
    var itemBoxEl = document.getElementById('confirmDialogItemBox');
    var itemNameEl = document.getElementById('confirmDialogItemName');
    var cancelBtn = document.getElementById('confirmDialogCancelBtn');
    var submitBtn = document.getElementById('confirmDialogSubmitBtn');
    var spinner = document.getElementById('confirmDialogSpinner');
    var btnText = document.getElementById('confirmDialogBtnText');

    if (titleEl) titleEl.textContent = options.title || 'Удалить объект?';
    if (descEl) descEl.textContent = options.message || 'Вы действительно хотите удалить этот объект?';

    if (options.itemName && itemNameEl && itemBoxEl) {
        itemNameEl.textContent = options.itemName;
        itemBoxEl.style.display = 'flex';
    } else if (itemBoxEl) {
        itemBoxEl.style.display = 'none';
    }

    var originalConfirmText = options.confirmText || 'Удалить';
    if (btnText) btnText.textContent = originalConfirmText;
    var originalCancelText = options.cancelText || (window.ITS_I18n ? window.ITS_I18n.t('confirm.cancel.btn') : 'Отмена');
    if (cancelBtn) cancelBtn.textContent = originalCancelText;
    if (spinner) spinner.style.display = 'none';
    if (submitBtn) submitBtn.disabled = false;
    if (cancelBtn) cancelBtn.disabled = false;

    function closeModal() {
        modal.style.display = 'none';
        cleanup();
    }

    function onKeyDown(e) {
        if (e.key === 'Escape') {
            e.preventDefault();
            closeModal();
        }
    }

    function onOverlayClick() {
        closeModal();
    }

    function onCancelClick() {
        closeModal();
    }

    async function onSubmitClick() {
        if (!options.onConfirm) {
            closeModal();
            return;
        }

        if (submitBtn) submitBtn.disabled = true;
        if (cancelBtn) cancelBtn.disabled = true;
        if (spinner) spinner.style.display = 'inline-block';
        if (btnText) {
            btnText.textContent = options.loadingText || (originalConfirmText === 'Удалить' ? 'Удаление...' : (originalConfirmText + '...'));
        }

        try {
            var result = options.onConfirm();
            if (result && typeof result.then === 'function') {
                await result;
            }
            closeModal();
        } catch (err) {
            console.error('Ошибка выполнения подтверждения:', err);
            if (submitBtn) submitBtn.disabled = false;
            if (cancelBtn) cancelBtn.disabled = false;
            if (spinner) spinner.style.display = 'none';
            if (btnText) btnText.textContent = originalConfirmText;
            if (typeof showToast === 'function') {
                showToast((err && err.message) || 'Произошла ошибка при выполнении действия', 'error');
            }
        }
    }

    function cleanup() {
        document.removeEventListener('keydown', onKeyDown);
        if (overlay) overlay.removeEventListener('click', onOverlayClick);
        if (cancelBtn) cancelBtn.removeEventListener('click', onCancelClick);
        if (submitBtn) submitBtn.removeEventListener('click', onSubmitClick);
    }

    document.addEventListener('keydown', onKeyDown);
    if (overlay) overlay.addEventListener('click', onOverlayClick);
    if (cancelBtn) cancelBtn.addEventListener('click', onCancelClick);
    if (submitBtn) submitBtn.addEventListener('click', onSubmitClick);

    var windowEl = modal.querySelector('.confirm-modal__window');
    if (windowEl) {
        windowEl.onclick = function(e) { e.stopPropagation(); };
    }

    modal.style.display = 'flex';
    if (cancelBtn) cancelBtn.focus();
};

/* ============================================================
   CLIPBOARD HELPER
   ============================================================ */

async function copyToClipboard(text, triggerBtn, fieldName) {
    if (!text) {
        showToast('Поле пустое, нечего копировать', 'warn');
        return;
    }

    var copied = false;
    if (navigator.clipboard && window.isSecureContext) {
        try {
            await navigator.clipboard.writeText(text);
            copied = true;
        } catch (e) {
            // fallback to execCommand below
        }
    }

    if (!copied) {
        try {
            var textArea = document.createElement('textarea');
            textArea.value = text;
            textArea.style.position = 'fixed';
            textArea.style.left = '-999999px';
            textArea.style.top = '-999999px';
            document.body.appendChild(textArea);
            textArea.focus();
            textArea.select();
            document.execCommand('copy');
            document.body.removeChild(textArea);
            copied = true;
        } catch (err) {
            console.error('Ошибка копирования:', err);
        }
    }

    if (triggerBtn) {
        triggerBtn.classList.add('is-copied');
        var originalHtml = triggerBtn.innerHTML;
        triggerBtn.innerHTML =
            '<svg viewBox="0 0 20 20" fill="none" width="14" height="14">' +
            '<path d="M5 10L8 13L15 6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>' +
            '</svg>';

        setTimeout(function() {
            triggerBtn.classList.remove('is-copied');
            triggerBtn.innerHTML = originalHtml;
        }, 1500);
    }

    var label = fieldName ? (fieldName + ' скопирован в буфер!') : 'Скопировано в буфер!';
    showToast(label);
}

/* ============================================================
   CLEAN TEXT WIKI & COMFORTABLE TYPOGRAPHY
   ============================================================ */

function cleanHtmlToText(html) {
    if (!html) return '';
    return html
        .replace(/<style[^>]*>[\s\S]*?<\/style>/gi, '')
        .replace(/<script[^>]*>[\s\S]*?<\/script>/gi, '')
        .replace(/<br\s*[\/]?>/gi, '\n')
        .replace(/<\/p>/gi, '\n')
        .replace(/<\/div>/gi, '\n')
        .replace(/<\/li>/gi, '\n')
        .replace(/<[^>]+>/g, '')
        .replace(/&nbsp;/g, ' ')
        .replace(/&amp;/g, '&')
        .replace(/&lt;/g, '<')
        .replace(/&gt;/g, '>')
        .replace(/&quot;/g, '"')
        .replace(/&#39;/g, "'");
}

function escapeHtmlText(text) {
    if (!text) return '';
    var d = document.createElement('div');
    d.appendChild(document.createTextNode(text));
    return d.innerHTML;
}

function formatTokens(str) {
    if (!str) return '';
    var safe = escapeHtmlText(str);

    // Extract URLs to placeholders so IP/AnyDesk formatting doesn't corrupt URLs or href attributes
    var urls = [];
    safe = safe.replace(/https?:\/\/[^\s<>"']+/g, function(url) {
        var idx = urls.length;
        urls.push(url);
        return '___URL_TOKEN_' + idx + '___';
    });

    // Format IP addresses
    safe = safe.replace(/\b(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\b/g, '<span class="wiki-token-ip">$1</span>');

    // Format AnyDesk numbers (e.g. 1 170 691 894 or 1218740083)
    safe = safe.replace(/\b(\d{1,4}\s\d{3}\s\d{3,4}|\d{9,10})\b/g, '<span class="wiki-token-anydesk">$1</span>');

    // Restore URLs as clickable links
    if (urls.length > 0) {
        safe = safe.replace(/___URL_TOKEN_(\d+)___/g, function(match, index) {
            var url = urls[parseInt(index, 10)];
            return '<a href="' + url + '" target="_blank" rel="noopener noreferrer" class="wiki-token-link">' + url + '</a>';
        });
    }

    return safe;
}

function formatLineContent(lineTrimmed) {
    if (!lineTrimmed) return '&nbsp;';

    // 1. Key: Value pattern (e.g., "Anydesk: 987 654 321", "RDP: arg.local\admin")
    var colonIdx = lineTrimmed.indexOf(':');
    var isProtocolColon = colonIdx > 0 && (
        lineTrimmed.slice(colonIdx, colonIdx + 3) === '://' ||
        /^https?$/i.test(lineTrimmed.slice(0, colonIdx).trim()) ||
        /https?:\/\//i.test(lineTrimmed.slice(0, colonIdx + 3))
    );
    var isIpPort = colonIdx > 0 && /^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(lineTrimmed.slice(0, colonIdx).trim());

    if (colonIdx > 0 && colonIdx < 35 && !isProtocolColon && !isIpPort) {
        var key = lineTrimmed.slice(0, colonIdx).trim();
        var val = lineTrimmed.slice(colonIdx + 1).trim();
        return '<span class="wiki-token-key">' + escapeHtmlText(key) + ':</span> <span class="wiki-token-val">' + formatTokens(val) + '</span>';
    }

    // 2. Standalone line with tokens (hyphens strictly preserved, no dash replacement)
    return formatTokens(lineTrimmed);
}

/* ============================================================
   WIKI CONTENT SHEET BUILDER
   ============================================================ */

function renderWikiSheet(rawContent) {
    if (!wikiSheet) return;

    var clean = cleanHtmlToText(rawContent).trim();

    if (!clean) {
        wikiSheet.innerHTML =
            '<div class="wiki-empty-state" id="wikiEmptyState">' +
                '<div class="wiki-empty-state__icon-wrap">' +
                    '<svg class="wiki-empty-state__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6">' +
                        '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>' +
                        '<polyline points="14 2 14 8 20 8"/>' +
                        '<line x1="12" y1="18" x2="12" y2="12"/>' +
                        '<line x1="9" y1="15" x2="15" y2="15"/>' +
                    '</svg>' +
                '</div>' +
                '<h4 class="wiki-empty-state__title">Параметры сервера пока не заполнены</h4>' +
                '<p class="wiki-empty-state__desc">Заметки, доступы и технические реквизиты для этого сервера пока не внесены в базу знаний.</p>' +
                '<button type="button" class="btn-fill-config" id="emptyStateEditBtn">' +
                    '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">' +
                        '<path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>' +
                        '<path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>' +
                    '</svg>' +
                    '<span>Редактировать</span>' +
                '</button>' +
            '</div>';

        var emptyBtn = document.getElementById('emptyStateEditBtn');
        if (emptyBtn) {
            emptyBtn.addEventListener('click', enterEditMode);
        }
        return;
    }

    var lines = clean.split('\n');
    var html = '<div class="wiki-lines-container">';

    for (var i = 0; i < lines.length; i++) {
        var lineRaw = lines[i];
        var lineTrimmed = lineRaw.trim();

        if (!lineTrimmed) {
            html += '<div class="wiki-line wiki-line--empty">' +
                        '<div class="wiki-line-text">&nbsp;</div>' +
                    '</div>';
            continue;
        }

        var isHeader = false;
        if (/^\[[^\]]+\]$/.test(lineTrimmed) ||
            /^(?:ms\s*sql\b|anydesk$|ilo$|ipmi$|usr1cv8$|для входа|данные для|кластер|сервер базы|учетные данные)/i.test(lineTrimmed) ||
            (lineTrimmed.length < 35 && lineTrimmed === lineTrimmed.toUpperCase() && /[A-ZА-Я]/.test(lineTrimmed) && !lineTrimmed.includes('@') && !lineTrimmed.includes('.'))) {
            isHeader = true;
        }

        var formattedText = formatLineContent(lineTrimmed);
        var lineClasses = 'wiki-line' + (isHeader ? ' wiki-line--header' : '');

        html += '<div class="' + lineClasses + '" data-line-index="' + i + '" data-raw="' + escapeHtmlText(lineTrimmed).replace(/"/g, '&quot;') + '">' +
                    '<div class="wiki-line-text">' + formattedText + '</div>' +
                    '<button type="button" class="wiki-line-copy-btn" title="Копировать строку" aria-label="Копировать">' +
                        '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">' +
                            '<rect x="9" y="9" width="13" height="13" rx="2" ry="2"/>' +
                            '<path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/>' +
                        '</svg>' +
                    '</button>' +
                '</div>';
    }

    html += '</div>';
    wikiSheet.innerHTML = html;

    // Bind line copy buttons
    wikiSheet.querySelectorAll('.wiki-line-copy-btn').forEach(function(btn) {
        btn.addEventListener('click', function(e) {
            e.stopPropagation();
            var lineEl = btn.closest('.wiki-line');
            if (!lineEl) return;
            var rawTextToCopy = lineEl.getAttribute('data-raw');
            if (rawTextToCopy === null || rawTextToCopy === undefined) {
                var textEl = lineEl.querySelector('.wiki-line-text');
                rawTextToCopy = textEl ? textEl.innerText.trim() : '';
            }
            copyToClipboard(rawTextToCopy, btn, 'Строка');
        });
    });
}

/* ============================================================
   EDITOR — MODE TOGGLE (EDIT / VIEW / CANCEL)
   ============================================================ */

function enterEditMode() {
    if (!orgEditor) return;
    if (typeof window.switchCompanyTab === 'function') {
        window.switchCompanyTab('data', true);
    }
    isEditMode = true;

    if (wikiSheet) wikiSheet.style.display = 'none';

    if (editorOuter) {
        editorOuter.style.display = 'block';
        editorOuter.classList.add('is-editing');
    }
    if (editorToolbar) editorToolbar.classList.add('is-visible');
    if (editorBottomBar) {
        editorBottomBar.style.display = 'flex';
        editorBottomBar.classList.add('is-visible');
    }

    orgEditor.contentEditable = 'true';
    orgEditor.focus();

    if (editBtnTop) editBtnTop.style.display = 'none';
    if (orgEditActions) orgEditActions.style.display = 'inline-flex';
}

function exitEditMode() {
    if (!orgEditor) return;
    isEditMode = false;

    orgEditor.contentEditable = 'false';

    if (editorOuter) {
        editorOuter.style.display = 'none';
        editorOuter.classList.remove('is-editing');
    }
    if (editorToolbar) editorToolbar.classList.remove('is-visible');
    if (editorBottomBar) {
        editorBottomBar.style.display = 'none';
        editorBottomBar.classList.remove('is-visible');
    }

    if (wikiSheet) wikiSheet.style.display = 'block';

    if (orgEditActions) orgEditActions.style.display = 'none';
    if (editBtnTop) {
        editBtnTop.style.display = '';
        editBtnTop.textContent = _t('wiki.edit.btn') || 'Редактировать';
        editBtnTop.classList.remove('is-active');
    }
}

function cancelEditMode() {
    if (!orgEditor) return;

    var currentContent = orgEditor.innerHTML;
    var baseContent = typeof initialContent !== 'undefined' ? initialContent : '';

    var cleanCur = cleanHtmlToText(currentContent).trim();
    var cleanBase = cleanHtmlToText(baseContent).trim();

    function doResetAndExit() {
        setEditorContent(baseContent);
        if (currentOrgId) {
            localStorage.removeItem('draft_org_' + currentOrgId.value);
            if (draftStatus) draftStatus.innerHTML = '';
        }
        exitEditMode();
    }

    if (cleanCur !== cleanBase) {
        window.showConfirmDialog({
            title: 'Отменить изменения?',
            message: 'Несохраненные правки будут потеряны. Вы действительно хотите выйти?',
            confirmText: 'Отменить правки',
            onConfirm: function() {
                doResetAndExit();
            }
        });
        return;
    }

    doResetAndExit();
}

function setEditorContent(content) {
    if (!orgEditor) return;
    if (!content || !content.trim()) {
        orgEditor.innerHTML = '';
    } else if (/<[a-z][\s\S]*?>/i.test(content)) {
        orgEditor.innerHTML = content;
    } else {
        var lines = content.split('\n');
        orgEditor.innerHTML = lines.map(function(line) {
            return line.trim()
                ? '<p>' + escapeHtmlText(line) + '</p>'
                : '<p><br></p>';
        }).join('');
    }
}

/* ── Init View & Content ── */
if (orgEditor && typeof initialContent !== 'undefined') {
    var loadedContent = initialContent;

    // Check for draft
    if (currentOrgId) {
        var draftKey = 'draft_org_' + currentOrgId.value;
        var draft = localStorage.getItem(draftKey);
        if (draft) {
            loadedContent = draft;
            if (draftStatus) {
                draftStatus.innerHTML =
                    '<svg width="14" height="14" viewBox="0 0 14 14" fill="none">' +
                    '<circle cx="7" cy="7" r="6" stroke="#22a050" stroke-width="1.5"/>' +
                    '<path d="M4 7L6 9L10 5" stroke="#22a050" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>' +
                    '</svg> ' + (_t('wiki.draft.loaded') || 'Черновик восстановлен');
            }
        }
    }

    setEditorContent(loadedContent);

    // Initial render of pure Wiki Sheet
    renderWikiSheet(loadedContent);
}

if (editBtnTop) {
    editBtnTop.addEventListener('click', function() {
        if (isEditMode) {
            exitEditMode();
        } else {
            if (typeof window.switchCompanyTab === 'function') {
                window.switchCompanyTab('data', true);
            }
            enterEditMode();
        }
    });
}

if (cancelEditBtn) {
    cancelEditBtn.addEventListener('click', cancelEditMode);
}

/* ── Toolbar command buttons ── */
document.querySelectorAll('.tb-btn[data-cmd]').forEach(function(btn) {
    btn.addEventListener('mousedown', function(e) {
        e.preventDefault();
        document.execCommand(this.dataset.cmd, false, null);
        if (orgEditor) orgEditor.focus();
    });
});

if (formatSelect) {
    formatSelect.addEventListener('change', function() {
        document.execCommand('formatBlock', false, '<' + this.value + '>');
        if (orgEditor) orgEditor.focus();
    });
}

if (linkBtn) {
    linkBtn.addEventListener('mousedown', function(e) {
        e.preventDefault();
        var url = prompt(_t('wiki.prompt.linkUrl') || 'Введите URL ссылки:');
        if (url && url.trim()) {
            document.execCommand('createLink', false, url.trim());
        }
        if (orgEditor) orgEditor.focus();
    });
}

/* ── Autosave draft to localStorage ── */
var autosaveTimer;
if (orgEditor && currentOrgId) {
    orgEditor.addEventListener('input', function() {
        clearTimeout(autosaveTimer);
        autosaveTimer = setTimeout(saveDraft, 3000);
    });
}

function saveDraft() {
    if (!orgEditor || !currentOrgId) return;
    var key = 'draft_org_' + currentOrgId.value;
    localStorage.setItem(key, orgEditor.innerHTML);
    if (draftStatus) {
        var now = new Date();
        var hm = now.getHours().toString().padStart(2, '0') + ':' +
                 now.getMinutes().toString().padStart(2, '0');
        draftStatus.innerHTML =
            '<svg width="14" height="14" viewBox="0 0 14 14" fill="none">' +
            '<circle cx="7" cy="7" r="6" stroke="#22a050" stroke-width="1.5"/>' +
            '<path d="M4 7L6 9L10 5" stroke="#22a050" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>' +
            '</svg> ' + (_t('wiki.draft.saved') || 'Черновик сохранён') + ' ' + hm;
    }
}

/* ============================================================
   SAVE ACTION
   ============================================================ */

if (saveBtn) {
    saveBtn.addEventListener('click', async function() {
        if (!currentOrgId || !orgEditor) return;

        var id      = currentOrgId.value;
        var name    = orgNameInput ? orgNameInput.value.trim() : '';
        var content = orgEditor.innerHTML;

        if (!name) {
            alert('Название организации не может быть пустым');
            return;
        }

        var originalText = saveBtn.textContent;
        saveBtn.textContent = _t('wiki.save.saving') || 'Сохранение...';
        saveBtn.disabled = true;

        try {
            var response = await fetch(API_BASE + '/' + id, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name: name, content: content })
            });
            var data = await response.json();

            saveBtn.textContent = originalText;
            saveBtn.disabled = false;

            if (!response.ok) {
                alert(data.error || 'Ошибка обновления организации');
                return;
            }

            // Clear local draft
            localStorage.removeItem('draft_org_' + id);
            if (draftStatus) draftStatus.innerHTML = '';

            // Update local memory and re-render pure Wiki Sheet
            initialContent = content;
            renderWikiSheet(content);

            // Update subtitle and lastModified timestamps
            var orgSubtitle = document.getElementById('orgSubtitle');
            var lastModified = document.getElementById('lastModified');
            var now = new Date();
            var dateStr = now.toLocaleString('ru-RU', {
                day: '2-digit', month: '2-digit', year: 'numeric',
                hour: '2-digit', minute: '2-digit'
            });
            var metaUser = document.querySelector('meta[name="app-user"]');
            var authorName = metaUser ? metaUser.getAttribute('content') : '';
            if (authorName === 'admin') authorName = 'Администратор';

            if (orgSubtitle) {
                orgSubtitle.textContent = 'Последнее изменение: ' + dateStr + (authorName ? ' • ' + authorName : '');
            }
            if (lastModified) {
                lastModified.innerHTML = '<span data-i18n="wiki.last.modified">Последнее изменение:</span> ' + (authorName || '') + ' ' + dateStr;
            }

            // Exit edit mode smoothly back to Sheet
            exitEditMode();
            showToast('✓ Параметры сервера успешно сохранены!');

        } catch (err) {
            saveBtn.textContent = originalText;
            saveBtn.disabled = false;
            console.error('Ошибка сохранения:', err);
            alert('Ошибка соединения с сервером');
        }
    });
}

/* Ctrl+S */
document.addEventListener('keydown', function(e) {
    if (e.ctrlKey && e.key === 's') {
        e.preventDefault();
        if (saveBtn && isEditMode) saveBtn.click();
    }
});

/* ============================================================
   MORE MENU
   ============================================================ */

if (moreBtnTop && moreMenu) {
    moreBtnTop.addEventListener('click', function(e) {
        e.stopPropagation();
        moreMenu.classList.toggle('is-open');
    });
    document.addEventListener('click', function(e) {
        if (moreMenu && !moreMenu.contains(e.target) && e.target !== moreBtnTop) {
            moreMenu.classList.remove('is-open');
        }
    });
}

if (deleteBtnMore) {
    deleteBtnMore.addEventListener('click', function() {
        if (moreMenu) moreMenu.classList.remove('is-open');
        if (deleteOrgModal) deleteOrgModal.style.display = 'flex';
    });
}

/* ============================================================
   CREATE ORG MODAL
   ============================================================ */

if (addOrgBtn) {
    addOrgBtn.addEventListener('click', function() {
        if (addOrgModal) {
            addOrgModal.style.display = 'flex';
            if (newOrgName) { newOrgName.value = ''; newOrgName.focus(); }
            if (newOrgContent) newOrgContent.value = '';
        }
    });
}

if (closeModal) {
    closeModal.addEventListener('click', function() {
        if (addOrgModal) addOrgModal.style.display = 'none';
    });
}

if (cancelModalBtn) {
    cancelModalBtn.addEventListener('click', function() {
        if (addOrgModal) addOrgModal.style.display = 'none';
    });
}

if (addOrgModal) {
    addOrgModal.addEventListener('click', function(e) {
        if (e.target === addOrgModal) addOrgModal.style.display = 'none';
    });
}

if (createOrgBtn) {
    createOrgBtn.addEventListener('click', async function() {
        var name = newOrgName ? newOrgName.value.trim() : '';
        if (!name) {
            alert(_t('wiki.alert.nameRequired') || 'Введите название');
            return;
        }
        try {
            var response = await fetch(API_BASE, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    name: name,
                    content: newOrgContent ? newOrgContent.value : ''
                })
            });
            var data = await response.json();
            if (!response.ok) {
                alert(data.error || 'Ошибка создания организации');
                return;
            }
            window.location.href = '/wiki/organization/' + data.id;
        } catch (err) {
            console.error('Ошибка создания:', err);
            alert('Ошибка соединения с сервером');
        }
    });
}

if (newOrgName) {
    newOrgName.addEventListener('keypress', function(e) {
        if (e.key === 'Enter' && createOrgBtn) createOrgBtn.click();
    });
}

/* ============================================================
   DELETE ORG MODAL
   ============================================================ */

if (closeDeleteModal) {
    closeDeleteModal.addEventListener('click', function() {
        if (deleteOrgModal) deleteOrgModal.style.display = 'none';
    });
}

if (cancelDeleteBtn) {
    cancelDeleteBtn.addEventListener('click', function() {
        if (deleteOrgModal) deleteOrgModal.style.display = 'none';
    });
}

if (deleteOrgModal) {
    deleteOrgModal.addEventListener('click', function(e) {
        if (e.target === deleteOrgModal) deleteOrgModal.style.display = 'none';
    });
}

if (confirmDeleteBtn) {
    confirmDeleteBtn.addEventListener('click', function() {
        var orgId   = null;
        var orgNameText = '';

        if (deleteOrgId && deleteOrgId.value) {
            orgId = deleteOrgId.value;
            orgNameText = deleteOrgName ? deleteOrgName.textContent : 'эту организацию';
        } else if (deleteOrgSelect && deleteOrgSelect.value) {
            orgId = deleteOrgSelect.value;
            orgNameText = deleteOrgSelect.options[deleteOrgSelect.selectedIndex].text;
        } else {
            alert(_t('wiki.alert.selectOrg') || 'Выберите организацию');
            return;
        }

        window.showConfirmDialog({
            title: 'Удалить организацию?',
            message: 'Вы действительно хотите удалить организацию?',
            itemName: orgNameText,
            confirmText: 'Удалить организацию',
            onConfirm: async function() {
                var response = await fetch(API_BASE + '/' + orgId, { method: 'DELETE' });
                var data = await response.json();
                if (!response.ok) {
                    throw new Error(data.error || 'Ошибка удаления организации');
                }
                if (deleteOrgModal) deleteOrgModal.style.display = 'none';
                window.location.href = '/wiki';
            }
        });
    });
}

/* ============================================================
   SIDEBAR INSTANT SEARCH & FILTER
   ============================================================ */

if (wikiSearch && wikiOrgList) {
    function applyFilter() {
        var query = wikiSearch.value.trim().toLowerCase();
        var allItems = Array.from(wikiOrgList.querySelectorAll('.wiki-list-item'));
        var totalCount = allItems.length;
        var visibleCount = 0;

        if (wikiSearchClear) {
            wikiSearchClear.style.display = query ? 'flex' : 'none';
        }

        var sectionHeaders = wikiOrgList.querySelectorAll('.wiki-list-section-header');
        if (query) {
            sectionHeaders.forEach(function(h) { h.style.display = 'none'; });
        } else {
            sectionHeaders.forEach(function(h) { h.style.display = ''; });
        }

        allItems.forEach(function(item) {
            var nameAttr = item.getAttribute('data-name') || '';
            var nameEl = item.querySelector('.wiki-list-name');
            var text = (nameEl ? nameEl.textContent : nameAttr).toLowerCase();

            var matches = !query || text.includes(query) || nameAttr.includes(query);
            if (matches) {
                item.style.display = '';
                visibleCount++;
            } else {
                item.style.display = 'none';
            }
        });

        if (wikiSearchNoResults) {
            wikiSearchNoResults.style.display = (visibleCount === 0 && totalCount > 0) ? 'block' : 'none';
        }

        if (wikiCountBadge) {
            if (query && visibleCount !== totalCount) {
                wikiCountBadge.textContent = visibleCount + ' / ' + totalCount;
            } else {
                wikiCountBadge.textContent = String(totalCount);
            }
        }
    }

    window.refreshWikiFilter = applyFilter;

    wikiSearch.addEventListener('input', applyFilter);

    if (wikiSearchClear) {
        wikiSearchClear.addEventListener('click', function() {
            wikiSearch.value = '';
            applyFilter();
            wikiSearch.focus();
        });
    }
}

/* ============================================================
   COMPANY TABS (Данные / Файлы — Спринт 9)
   ============================================================ */

(function initCompanyTabs() {
    var tabBtnData    = document.getElementById('tabBtnData');
    var tabBtnFiles   = document.getElementById('tabBtnFiles');
    var tabPanelData  = document.getElementById('tabPanelData') || document.getElementById('tabPaneData');
    var tabPanelFiles = document.getElementById('tabPanelFiles') || document.getElementById('tabPaneFiles');
    var orgIdInput    = document.getElementById('currentOrgId');

    function switchTab(tabKey, updateUrl) {
        if (!tabBtnData || !tabBtnFiles || !tabPanelData || !tabPanelFiles) return;
        var orgId = orgIdInput ? orgIdInput.value : '';

        if (tabKey === 'files') {
            tabBtnFiles.classList.add('is-active');
            tabBtnFiles.setAttribute('aria-selected', 'true');
            tabBtnData.classList.remove('is-active');
            tabBtnData.setAttribute('aria-selected', 'false');

            tabPanelFiles.style.display = 'block';
            tabPanelData.style.display = 'none';

            if (window.sessionStorage) {
                sessionStorage.setItem('wiki_active_tab', 'files');
                if (orgId) sessionStorage.setItem('wiki_active_tab_' + orgId, 'files');
            }
            if (updateUrl && window.history && history.replaceState) {
                history.replaceState(null, '', '#files');
            }
        } else {
            tabBtnData.classList.add('is-active');
            tabBtnData.setAttribute('aria-selected', 'true');
            tabBtnFiles.classList.remove('is-active');
            tabBtnFiles.setAttribute('aria-selected', 'false');

            tabPanelData.style.display = 'block';
            tabPanelFiles.style.display = 'none';

            if (window.sessionStorage) {
                sessionStorage.setItem('wiki_active_tab', 'data');
                if (orgId) sessionStorage.setItem('wiki_active_tab_' + orgId, 'data');
            }
            if (updateUrl && window.history && history.replaceState) {
                history.replaceState(null, '', window.location.pathname);
            }
        }
    }

    window.switchCompanyTab = switchTab;

    if (!tabBtnData || !tabBtnFiles || !tabPanelData || !tabPanelFiles) return;

    var orgId = orgIdInput ? orgIdInput.value : '';

    tabBtnData.addEventListener('click', function() { switchTab('data', true); });
    tabBtnFiles.addEventListener('click', function() { switchTab('files', true); });

    // Initial detection on page load
    var initialTab = 'data';
    var hash = window.location.hash;
    if (hash === '#files' || hash === '#tab-files') {
        initialTab = 'files';
    } else if (hash === '#data' || hash === '#tab-data') {
        initialTab = 'data';
    } else if (window.sessionStorage) {
        var savedOrgTab = orgId ? sessionStorage.getItem('wiki_active_tab_' + orgId) : null;
        var savedTab = savedOrgTab || sessionStorage.getItem('wiki_active_tab');
        if (savedTab === 'files') initialTab = 'files';
    }

    switchTab(initialTab, false);

    window.addEventListener('hashchange', function() {
        var currentHash = window.location.hash;
        if (currentHash === '#files' || currentHash === '#tab-files') {
            switchTab('files', false);
        } else {
            switchTab('data', false);
        }
    });
})();

/* ============================================================
   1C DATABASES REGISTRY (AJAX CRUD — Спринт 3/4)
   ============================================================ */

(function initDatabases1C() {
    var addDbBtn       = document.getElementById('addDatabaseBtn');
    var dbModal        = document.getElementById('db1cModal');
    var closeDbModal   = document.getElementById('closeDb1cModal');
    var cancelDbModal  = document.getElementById('cancelDb1cBtn');
    var dbForm         = document.getElementById('db1cForm');
    var dbIdInput      = document.getElementById('dbId') || document.getElementById('db1cId');
    var dbNameInput    = document.getElementById('dbNameInput') || document.getElementById('dbName') || document.getElementById('db1cName');
    var dbDescInput    = document.getElementById('dbDescription') || document.getElementById('db1cRelease');
    var dbDateInput    = document.getElementById('dbDate') || document.getElementById('db1cDate');
    var dbModalTitle   = document.getElementById('db1cModalTitle');
    var tableWrap      = document.getElementById('db1cTableWrap');
    var tableBody      = document.getElementById('db1cTableBody');
    var emptyState     = document.getElementById('db1cEmptyState');
    var countBadge     = document.getElementById('db1cCountBadge');
    var tabDbCount     = document.getElementById('tabDbCount');
    var orgIdInput     = document.getElementById('currentOrgId');

    if (!dbModal || !orgIdInput) return;

    var orgId = orgIdInput.value;

    function getTodayString() {
        var d = new Date();
        var month = '' + (d.getMonth() + 1);
        var day = '' + d.getDate();
        var year = d.getFullYear();
        if (month.length < 2) month = '0' + month;
        if (day.length < 2) day = '0' + day;
        return [year, month, day].join('-');
    }

    function openModalForAdd() {
        if (!dbForm) return;
        dbForm.reset();
        if (dbIdInput) dbIdInput.value = '';
        var altId = document.getElementById('db1cId');
        if (altId) altId.value = '';
        if (dbNameInput) dbNameInput.value = '';
        if (dbDescInput) dbDescInput.value = '';
        if (dbDateInput) dbDateInput.value = getTodayString();
        if (dbModalTitle) dbModalTitle.textContent = 'Добавить базу 1С';
        dbModal.style.display = 'flex';
        if (dbNameInput) setTimeout(function() { dbNameInput.focus(); }, 50);
    }

    function openModalForEdit(data) {
        if (!dbForm) return;
        if (dbIdInput) dbIdInput.value = data.id || '';
        var altId = document.getElementById('db1cId');
        if (altId) altId.value = data.id || '';
        if (dbNameInput) dbNameInput.value = data.name || '';
        if (dbDescInput) dbDescInput.value = data.description || data.release || '';
        if (dbDateInput) dbDateInput.value = data.date || getTodayString();
        if (dbModalTitle) dbModalTitle.textContent = 'Редактировать базу 1С';
        dbModal.style.display = 'flex';
        if (dbNameInput) setTimeout(function() { dbNameInput.focus(); }, 50);
    }

    function closeModal() {
        if (dbModal) dbModal.style.display = 'none';
    }

    function updateViewCounts() {
        if (!tableBody) return;
        var rows = tableBody.querySelectorAll('tr');
        var count = rows.length;
        if (countBadge) countBadge.textContent = String(count);
        var tabBadge = document.getElementById('tabDbCount');
        if (tabBadge) tabBadge.textContent = String(count);
        if (count > 0) {
            if (tableWrap) tableWrap.style.display = '';
            if (emptyState) emptyState.style.display = 'none';
        } else {
            if (tableWrap) tableWrap.style.display = 'none';
            if (emptyState) emptyState.style.display = '';
        }
    }

    function formatDateRu(dateStr) {
        if (!dateStr) return '—';
        var d = new Date(dateStr);
        if (isNaN(d.getTime())) return dateStr;
        return d.toLocaleDateString('ru-RU');
    }

    function createRowHtml(db) {
        var formattedDate = formatDateRu(db.updated_at);
        var isoDate = db.updated_at ? new Date(db.updated_at).toISOString().split('T')[0] : '';
        var desc = db.description || db.release || '—';
        var safeDesc = escapeHtmlText(desc === '—' ? '' : desc);
        return '<td class="db-name-cell"><span class="db-name-text">' + escapeHtmlText(db.name) + '</span></td>' +
               '<td class="db-release-cell"><span class="db-release-badge db-desc-text">' + escapeHtmlText(desc) + '</span></td>' +
               '<td class="db-date-cell">' + formattedDate + '</td>' +
               '<td class="db-actions-cell">' +
               '  <button type="button" class="btn-db-action btn-db-edit" title="Редактировать базу" data-id="' + db.id + '" data-name="' + escapeHtmlText(db.name) + '" data-description="' + safeDesc + '" data-release="' + safeDesc + '" data-date="' + isoDate + '">✏️</button>' +
               '  <button type="button" class="btn-db-action btn-db-delete" title="Удалить базу" data-id="' + db.id + '" data-name="' + escapeHtmlText(db.name) + '">🗑</button>' +
               '</td>';
    }

    if (addDbBtn) {
        addDbBtn.addEventListener('click', openModalForAdd);
    }

    if (closeDbModal) closeDbModal.addEventListener('click', closeModal);
    if (cancelDbModal) cancelDbModal.addEventListener('click', closeModal);

    window.addEventListener('click', function(e) {
        if (e.target === dbModal) closeModal();
    });

    // Delegated edit & delete clicks
    if (tableBody) {
        tableBody.addEventListener('click', async function(e) {
            var editBtn = e.target.closest('.btn-db-edit');
            var deleteBtn = e.target.closest('.btn-db-delete');

            if (editBtn) {
                var data = {
                    id: editBtn.getAttribute('data-id'),
                    name: editBtn.getAttribute('data-name'),
                    description: editBtn.getAttribute('data-description') || editBtn.getAttribute('data-release') || '',
                    release: editBtn.getAttribute('data-release') || editBtn.getAttribute('data-description') || '',
                    date: editBtn.getAttribute('data-date')
                };
                openModalForEdit(data);
                return;
            }

            if (deleteBtn) {
                var id = deleteBtn.getAttribute('data-id');
                var name = deleteBtn.getAttribute('data-name') || 'базу';
                window.showConfirmDialog({
                    title: 'Удалить базу 1С?',
                    message: 'Вы действительно хотите удалить базу?',
                    itemName: name,
                    confirmText: 'Удалить базу',
                    onConfirm: async function() {
                        var res = await fetch('/api/databases/' + id, { method: 'DELETE' });
                        var result = await res.json();
                        if (!res.ok) {
                            throw new Error(result.error || 'Ошибка удаления базы');
                        }
                        var row = deleteBtn.closest('tr');
                        if (row) row.remove();
                        updateViewCounts();
                        showToast('База 1С успешно удалена');
                    }
                });
                return;
            }
        });
    }

    // Submit form (Create or Update)
    if (dbForm) {
        dbForm.addEventListener('submit', async function(e) {
            e.preventDefault();
            var id = dbIdInput ? dbIdInput.value : '';
            var name = dbNameInput ? dbNameInput.value.trim() : '';
            var desc = dbDescInput ? dbDescInput.value.trim() : '';
            var updated_at = dbDateInput ? dbDateInput.value : '';

            if (!name) {
                alert('Введите наименование базы');
                return;
            }

            try {
                var isEdit = Boolean(id);
                var url = isEdit ? ('/api/databases/' + id) : ('/api/organizations/' + orgId + '/databases');
                var method = isEdit ? 'PUT' : 'POST';

                var res = await fetch(url, {
                    method: method,
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ name: name, description: desc, release: desc, updated_at: updated_at })
                });

                var data = await res.json();
                if (!res.ok) {
                    alert(data.error || 'Ошибка сохранения базы');
                    return;
                }

                if (isEdit) {
                    var existingRow = tableBody.querySelector('tr[data-id="' + id + '"]');
                    if (existingRow) {
                        existingRow.innerHTML = createRowHtml({ id: id, name: name, description: desc, release: desc, updated_at: updated_at });
                    }
                    showToast('База 1С успешно обновлена');
                } else {
                    var newRow = document.createElement('tr');
                    newRow.setAttribute('data-id', data.id);
                    newRow.innerHTML = createRowHtml({ id: data.id, name: name, description: desc, release: desc, updated_at: updated_at });
                    tableBody.insertBefore(newRow, tableBody.firstChild);
                    showToast('База 1С успешно добавлена');
                }

                closeModal();
                updateViewCounts();
            } catch (err) {
                console.error('Ошибка сохранения базы 1С:', err);
                alert('Ошибка соединения с сервером');
            }
        });
    }
})();

/* ============================================================
   WIKI SIDEBAR PINNING (Sprint 7)
   ============================================================ */

function getAppUsername() {
    var meta = document.querySelector('meta[name="app-user"]');
    var val = meta ? meta.getAttribute('content') : '';
    return (val && val.trim()) ? val.trim() : 'user';
}

function getPinnedCacheKey() {
    return 'pinned_orgs_' + getAppUsername();
}

document.addEventListener('click', async function (e) {
    var pinBtn = e.target.closest('.wiki-pin-btn');
    if (!pinBtn) return;

    e.preventDefault();
    e.stopPropagation();

    var orgId = pinBtn.getAttribute('data-org-id');
    if (!orgId) return;

    try {
        var res = await fetch('/api/organizations/' + orgId + '/pin', {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json'
            }
        });
        var data = await res.json();
        if (data && data.success) {
            var isPinned = !!data.pinned;
            var unpinnedTitle = window.ITS_I18n ? window.ITS_I18n.t('toast.unpinned') : 'Открепить организацию';
            var pinnedTitle = window.ITS_I18n ? window.ITS_I18n.t('toast.pinned') : 'Закрепить организацию';
            if (isPinned) {
                pinBtn.classList.add('is-pinned');
                pinBtn.setAttribute('title', unpinnedTitle);
                showToast(window.ITS_I18n ? window.ITS_I18n.t('toast.pinned') : 'Организация закреплена');
            } else {
                pinBtn.classList.remove('is-pinned');
                pinBtn.setAttribute('title', pinnedTitle);
                showToast(window.ITS_I18n ? window.ITS_I18n.t('toast.unpinned') : 'Организация откреплена');
            }
            if (!pinBtn.querySelector('.wiki-pin-icon')) {
                pinBtn.innerHTML = '<span class="wiki-pin-icon" aria-hidden="true">📌</span>';
            }
            if (data.pins) {
                try {
                    localStorage.setItem(getPinnedCacheKey(), JSON.stringify(data.pins));
                } catch (errCache) {}
            }
            reorganizeWikiSidebar(data.pins || []);
        } else {
            showToast((data && data.error) || 'Ошибка закрепления');
        }
    } catch (err) {
        console.error('Error toggling pin:', err);
        showToast('Ошибка при закреплении организации');
    }
});

function createOrgListItem(org, isPinned, isActive) {
    var li = document.createElement('li');
    li.className = 'wiki-list-item';
    li.setAttribute('data-name', (org.name || '').toLowerCase());
    li.setAttribute('data-org-id', org.id);
    var pinTitle = isPinned 
        ? (window.ITS_I18n ? window.ITS_I18n.t('toast.unpinned') : 'Открепить организацию')
        : (window.ITS_I18n ? window.ITS_I18n.t('toast.pinned') : 'Закрепить организацию');
    li.innerHTML = '<a href="/wiki/organization/' + org.id + '" class="wiki-list-link' + (isActive ? ' is-active' : '') + '">' +
        '<span class="wiki-active-bar" aria-hidden="true"></span>' +
        '<svg class="wiki-list-chevron" viewBox="0 0 8 12" fill="none" width="7" height="11">' +
        '<path d="M1.5 1.5L6 6L1.5 10.5" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>' +
        '</svg>' +
        '<span class="wiki-list-name">' + escapeHtmlText(org.name) + '</span>' +
        '</a>' +
        '<button type="button" class="wiki-pin-btn' + (isPinned ? ' is-pinned' : '') + '" data-org-id="' + org.id + '" title="' + pinTitle + '" aria-label="Булавка">' +
        '<span class="wiki-pin-icon" aria-hidden="true">📌</span>' +
        '</button>';
    return li;
}

function reorganizeWikiSidebar(pins) {
    var list = document.getElementById('wikiOrgList');
    if (!list) return;

    var noResults = document.getElementById('wikiSearchNoResults');
    var items = Array.from(list.querySelectorAll('.wiki-list-item'));
    if (!items.length) return;

    if (pins && Array.isArray(pins)) {
        try {
            localStorage.setItem(getPinnedCacheKey(), JSON.stringify(pins));
        } catch (e) {}
    } else {
        try {
            var cached = localStorage.getItem(getPinnedCacheKey());
            if (cached) pins = JSON.parse(cached);
        } catch (e) {}
    }

    var pinsNum = (pins || []).map(Number);
    var pinnedItems = [];
    var otherItems = [];

    var unpinnedTitle = window.ITS_I18n ? window.ITS_I18n.t('toast.unpinned') : 'Открепить организацию';
    var pinnedTitle = window.ITS_I18n ? window.ITS_I18n.t('toast.pinned') : 'Закрепить организацию';

    items.forEach(function(item) {
        var btn = item.querySelector('.wiki-pin-btn');
        var id = btn ? Number(btn.getAttribute('data-org-id')) : 0;
        var isPinned = pinsNum.includes(id);

        if (btn) {
            if (isPinned) {
                btn.classList.add('is-pinned');
                btn.setAttribute('title', unpinnedTitle);
            } else {
                btn.classList.remove('is-pinned');
                btn.setAttribute('title', pinnedTitle);
            }
            if (!btn.querySelector('.wiki-pin-icon')) {
                btn.innerHTML = '<span class="wiki-pin-icon" aria-hidden="true">📌</span>';
            }
        }

        if (isPinned) {
            pinnedItems.push(item);
        } else {
            otherItems.push(item);
        }
    });

    // Remove existing headers
    var oldHeaders = list.querySelectorAll('.wiki-list-section-header');
    oldHeaders.forEach(function(h) { h.remove(); });

    // Detach all items
    items.forEach(function(item) { item.remove(); });

    var pinnedHeaderLabel = window.ITS_I18n ? window.ITS_I18n.t('wiki.sidebar.pinned') : 'ЗАКРЕПЛЕННЫЕ';
    var allHeaderLabel = window.ITS_I18n ? window.ITS_I18n.t('wiki.sidebar.all') : 'ВСЕ ОРГАНИЗАЦИИ';

    if (pinnedItems.length > 0) {
        var pHeader = document.createElement('li');
        pHeader.className = 'wiki-list-section-header';
        pHeader.id = 'pinnedSectionHeader';
        pHeader.innerHTML = '<span data-i18n="wiki.sidebar.pinned">' + pinnedHeaderLabel + '</span>';
        list.appendChild(pHeader);

        pinnedItems.forEach(function(item) {
            list.appendChild(item);
        });

        var aHeader = document.createElement('li');
        aHeader.className = 'wiki-list-section-header';
        aHeader.id = 'allSectionHeader';
        aHeader.innerHTML = '<span data-i18n="wiki.sidebar.all">' + allHeaderLabel + '</span>';
        list.appendChild(aHeader);

        otherItems.forEach(function(item) {
            list.appendChild(item);
        });
    } else {
        otherItems.forEach(function(item) {
            list.appendChild(item);
        });
    }

    if (noResults) {
        list.appendChild(noResults);
    }

    if (window.refreshWikiFilter) {
        window.refreshWikiFilter();
    }
}

/* ============================================================
   ATTACHMENTS & LIGHTBOX MODAL (Sprint 7)
   ============================================================ */

(function initAttachmentsAndLightbox() {
    var attachmentsSection = document.getElementById('wikiAttachmentsSection');
    var dropzone = document.getElementById('attachmentsDropzone');
    var fileInput = document.getElementById('fileInputUpload');
    var btnAttach = document.getElementById('btnAttachFile');
    var attachmentsList = document.getElementById('attachmentsList');
    var filesCountBadge = document.getElementById('filesCountBadge');
    var filesTabCounter = document.getElementById('filesTabCounter');
    var attachmentsEmpty = document.getElementById('attachmentsEmpty');
    var uploadFileBtn = document.getElementById('uploadFileBtn');
    var orgIdInput = document.getElementById('currentOrgId');

    // Lightbox elements
    var lightboxModal = document.getElementById('fileLightboxModal');
    var lightboxOverlay = document.getElementById('lightboxOverlay');
    var lightboxCloseBtn = document.getElementById('lightboxCloseBtn');
    var lightboxFileName = document.getElementById('lightboxFileName');
    var lightboxDownloadBtn = document.getElementById('lightboxDownloadBtn');
    var lightboxZoomBtn = document.getElementById('lightboxZoomBtn');
    var lightboxBody = document.getElementById('lightboxBody');
    var lightboxEditActions = document.getElementById('lightboxEditActions');
    var lightboxCancelBtn = document.getElementById('lightboxCancelBtn');
    var lightboxSaveBtn = document.getElementById('lightboxSaveBtn');

    // Lightbox editing state (Sprint 11)
    var currentFileId = null;
    var currentFileOriginalContent = '';
    var currentFileIsText = false;
    var isSavingFile = false;

    if (!attachmentsSection) return;

    var currentOrgId = orgIdInput ? Number(orgIdInput.value) : null;
    if (!currentOrgId) return;

    function getFileTypeInfo(originalName, fileSize) {
        var ext = (originalName ? originalName.split('.').pop() : '').toLowerCase();
        var fileType = 'file';
        var iconColor = '#64748b';
        var isPreviewable = false;

        var TEXT_FORMATS = ['txt', 'xml', 'json', 'csv', 'conf', 'ini', 'rdp', 'sql', 'log', 'env', 'yml', 'yaml'];

        if (['xlsx', 'xls'].includes(ext)) {
            fileType = 'excel';
            iconColor = '#10b981';
            isPreviewable = true;
        } else if (ext === 'xml') {
            fileType = 'xml';
            iconColor = '#06b6d4';
            isPreviewable = true;
        } else if (TEXT_FORMATS.includes(ext)) {
            fileType = 'text';
            iconColor = '#6366f1';
            isPreviewable = true;
        } else if (ext === 'pdf') {
            fileType = 'pdf';
            iconColor = '#ef4444';
            isPreviewable = true;
        } else if (['png', 'jpg', 'jpeg', 'webp'].includes(ext)) {
            fileType = 'image';
            iconColor = '#3b82f6';
            isPreviewable = true;
        } else if (ext === 'docx' || ext === 'doc') {
            fileType = 'doc';
            iconColor = '#2563eb';
            isPreviewable = true;
        } else if (['zip'].includes(ext)) {
            fileType = 'archive';
            iconColor = '#f59e0b';
        }

        var size = fileSize || 0;
        var sizeFormatted = '';
        if (size < 1024) {
            sizeFormatted = size + ' Б';
        } else if (size < 1024 * 1024) {
            sizeFormatted = (size / 1024).toFixed(1) + ' КБ';
        } else {
            sizeFormatted = (size / (1024 * 1024)).toFixed(1) + ' МБ';
        }

        return { ext: ext, type: fileType, color: iconColor, isPreviewable: isPreviewable, sizeFormatted: sizeFormatted };
    }

    function getFileIconSvg(fileType) {
        if (fileType === 'excel') {
            return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="8" y1="13" x2="16" y2="17"/><line x1="16" y1="13" x2="8" y2="17"/></svg>';
        } else if (fileType === 'xml') {
            return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="16 18 22 12 16 6"/><polyline points="8 6 2 12 8 18"/></svg>';
        } else if (fileType === 'pdf') {
            return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><path d="M10 12h2a1 1 0 0 1 1 1v1a1 1 0 0 1-1 1h-2v-4z"/></svg>';
        } else if (fileType === 'image') {
            return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="18" height="18" rx="2" ry="2"/><circle cx="8.5" cy="8.5" r="1.5"/><polyline points="21 15 16 10 5 21"/></svg>';
        } else if (fileType === 'doc' || fileType === 'docx') {
            return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><line x1="10" y1="9" x2="8" y2="9"/></svg>';
        } else if (fileType === 'text') {
            return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>';
        } else if (fileType === 'archive') {
            return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="21 8 21 21 3 21 3 8"/><rect x="1" y="3" width="22" height="5"/><line x1="10" y1="12" x2="14" y2="12"/></svg>';
        } else {
            return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>';
        }
    }

    function createFileChipElement(file) {
        var info = getFileTypeInfo(file.original_name, file.file_size);
        var chip = document.createElement('div');
        chip.className = 'file-chip file-chip--' + info.type;
        chip.setAttribute('data-file-id', file.id);
        chip.setAttribute('data-file-name', file.original_name);
        chip.setAttribute('data-file-type', info.type);
        chip.setAttribute('data-previewable', info.isPreviewable ? 'true' : 'false');

        var previewTitle = window.ITS_I18n ? window.ITS_I18n.t('wiki.files.preview') : 'Нажмите для предпросмотра';
        var downloadTitle = window.ITS_I18n ? window.ITS_I18n.t('wiki.files.download') : 'Скачать файл';
        var deleteTitle = window.ITS_I18n ? window.ITS_I18n.t('wiki.files.delete') : 'Удалить файл';

        var previewAttr = info.isPreviewable ? ' role="button" tabindex="0" title="' + previewTitle + '" data-i18n-title="wiki.files.preview"' : '';
        var clickableClass = info.isPreviewable ? ' is-clickable' : '';
        var currentLang = localStorage.getItem('ui_lang') || 'ru';
        var dateLocale = currentLang === 'kk' ? 'kk-KZ' : 'ru-RU';
        var fileDate = file.created_at ? new Date(file.created_at).toLocaleDateString(dateLocale) : '';

        chip.innerHTML =
            '<div class="file-chip-main' + clickableClass + '"' + previewAttr + '>' +
                '<span class="file-chip-icon" style="color: ' + info.color + ';">' +
                    getFileIconSvg(info.type) +
                '</span>' +
                '<div class="file-chip-info">' +
                    '<span class="file-chip-name" title="' + escapeHtmlText(file.original_name) + '">' + escapeHtmlText(file.original_name) + '</span>' +
                    '<div class="file-chip-details">' +
                        '<span class="file-chip-size">' + info.sizeFormatted + '</span>' +
                        '<span class="file-chip-date">• ' + (fileDate || 'только что') + '</span>' +
                    '</div>' +
                '</div>' +
            '</div>' +
            '<div class="file-chip-actions">' +
                '<a href="/api/files/' + file.id + '/download" class="file-chip-btn file-chip-btn--download" title="' + downloadTitle + '" data-i18n-title="wiki.files.download" download="' + escapeHtmlText(file.original_name) + '">' +
                    '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>' +
                '</a>' +
                '<button type="button" class="file-chip-btn file-chip-btn--delete" data-file-id="' + file.id + '" data-file-name="' + escapeHtmlText(file.original_name) + '" title="' + deleteTitle + '" data-i18n-title="wiki.files.delete">' +
                    '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg>' +
                '</button>' +
            '</div>';

        return chip;
    }

    function updateBadgeCount(delta) {
        var current = 0;
        if (filesCountBadge) {
            current = parseInt(filesCountBadge.textContent, 10) || 0;
        } else if (filesTabCounter) {
            current = parseInt(filesTabCounter.textContent, 10) || 0;
        }
        var next = Math.max(0, current + delta);

        if (filesCountBadge) {
            filesCountBadge.textContent = String(next);
        }
        if (filesTabCounter) {
            filesTabCounter.textContent = String(next);
            if (next > 0) {
                filesTabCounter.classList.add('has-items');
                filesTabCounter.classList.add('has-files');
            } else {
                filesTabCounter.classList.remove('has-items');
                filesTabCounter.classList.remove('has-files');
            }
        }

        var emptyEl = attachmentsEmpty || document.getElementById('attachmentsEmpty');
        if (emptyEl) {
            if (next === 0) {
                emptyEl.style.display = '';
            } else {
                emptyEl.style.display = 'none';
            }
        }
    }

    async function uploadFiles(files) {
        if (!files || !files.length) return;
        var maxBytes = 25 * 1024 * 1024; // 25 MB

        for (var i = 0; i < files.length; i++) {
            var file = files[i];
            if (file.size > maxBytes) {
                showToast('Файл "' + file.name + '" превышает 25 МБ');
                continue;
            }

            var formData = new FormData();
            formData.append('file', file);

            try {
                var res = await fetch('/api/organizations/' + currentOrgId + '/files', {
                    method: 'POST',
                    body: formData
                });
                var data = await res.json();
                if (res.ok && data && data.success && data.file) {
                    var chip = createFileChipElement(data.file);
                    if (attachmentsList) {
                        attachmentsList.appendChild(chip);
                    }
                    var emptyEl = attachmentsEmpty || document.getElementById('attachmentsEmpty');
                    if (emptyEl) emptyEl.style.display = 'none';
                    updateBadgeCount(1);
                    showToast(window.ITS_I18n ? window.ITS_I18n.t('toast.fileUploaded') : 'Файл прикреплен');
                } else {
                    var errMsg = (data && data.error) ? data.error : 'Ошибка загрузки файла';
                    showToast(errMsg);
                }
            } catch (err) {
                console.error('Ошибка загрузки файла:', err);
                showToast('Ошибка при загрузке файла');
            }
        }
    }

    // Empty-state upload button click -> trigger hidden file input
    if (uploadFileBtn && fileInput) {
        uploadFileBtn.addEventListener('click', function(e) {
            e.preventDefault();
            fileInput.click();
        });
    }

    // Attach button click -> trigger hidden file input
    if (btnAttach && fileInput) {
        btnAttach.addEventListener('click', function(e) {
            e.preventDefault();
            fileInput.click();
        });
    }

    // File input change
    if (fileInput) {
        fileInput.addEventListener('change', function() {
            if (this.files && this.files.length) {
                uploadFiles(this.files);
                this.value = '';
            }
        });
    }

    // Dropzone click -> trigger hidden file input
    if (dropzone && fileInput) {
        dropzone.addEventListener('click', function(e) {
            if (e.target !== fileInput) {
                fileInput.click();
            }
        });

        // Drag & Drop events
        ['dragenter', 'dragover'].forEach(function(eventName) {
            dropzone.addEventListener(eventName, function(e) {
                e.preventDefault();
                e.stopPropagation();
                dropzone.classList.add('is-dragover');
            });
        });

        ['dragleave', 'drop'].forEach(function(eventName) {
            dropzone.addEventListener(eventName, function(e) {
                e.preventDefault();
                e.stopPropagation();
                dropzone.classList.remove('is-dragover');
            });
        });

        dropzone.addEventListener('drop', function(e) {
            e.preventDefault();
            e.stopPropagation();
            dropzone.classList.remove('is-dragover');
            var dt = e.dataTransfer;
            if (dt && dt.files && dt.files.length) {
                uploadFiles(dt.files);
            }
        });
    }

    // Delegated actions inside attachmentsList: Download, Delete, Lightbox Preview
    if (attachmentsList) {
        attachmentsList.addEventListener('click', async function(e) {
            var deleteBtn = e.target.closest('.file-chip-btn--delete');
            var mainClickable = e.target.closest('.file-chip-main.is-clickable');

            if (deleteBtn) {
                e.preventDefault();
                e.stopPropagation();
                var fileId = deleteBtn.getAttribute('data-file-id');
                var fileName = deleteBtn.getAttribute('data-file-name') || 'файл';

                window.showConfirmDialog({
                    title: window.ITS_I18n ? window.ITS_I18n.t('confirm.delete.title') : 'Удалить вложение?',
                    message: window.ITS_I18n ? window.ITS_I18n.t('confirm.delete.desc') : 'Вы действительно хотите удалить прикрепленный файл?',
                    itemName: fileName,
                    confirmText: window.ITS_I18n ? window.ITS_I18n.t('confirm.delete.btn') : 'Удалить',
                    cancelText: window.ITS_I18n ? window.ITS_I18n.t('confirm.cancel.btn') : 'Отмена',
                    onConfirm: async function() {
                        var res = await fetch('/api/files/' + fileId, { method: 'DELETE' });
                        var data = await res.json();
                        if (res.ok && data && data.success) {
                            var chip = deleteBtn.closest('.file-chip');
                            if (chip) chip.remove();
                            updateBadgeCount(-1);
                            var emptyEl = attachmentsEmpty || document.getElementById('attachmentsEmpty');
                            if (emptyEl && attachmentsList && attachmentsList.querySelectorAll('.file-chip').length === 0) {
                                emptyEl.style.display = '';
                            }
                            showToast(window.ITS_I18n ? window.ITS_I18n.t('toast.fileDeleted') : 'Файл удален');
                        } else {
                            throw new Error((data && data.error) || 'Ошибка удаления файла');
                        }
                    }
                });
                return;
            }

            if (mainClickable) {
                var chip = mainClickable.closest('.file-chip');
                if (!chip) return;
                var fileId = chip.getAttribute('data-file-id');
                var fileType = chip.getAttribute('data-file-type');
                var fileName = chip.getAttribute('data-file-name');
                openLightbox(fileId, fileName, fileType);
            }
        });
    }

    // Lightbox modal functions
    function openLightbox(fileId, fileName, fileType) {
        if (!lightboxModal) return;

        // Reset editing state for new file
        currentFileId = fileId;
        currentFileOriginalContent = '';
        currentFileIsText = false;
        isSavingFile = false;
        if (lightboxEditActions) lightboxEditActions.style.display = 'none';

        var ext = (fileName ? fileName.split('.').pop() : '').toLowerCase();
        var isWordDoc = fileType === 'doc' || fileType === 'docx' || ext === 'docx' || ext === 'doc';
        var TEXT_FORMATS = ['txt', 'xml', 'json', 'csv', 'conf', 'ini', 'rdp', 'sql', 'log', 'env', 'yml', 'yaml'];
        var isTextFile = fileType === 'text' || fileType === 'xml' || TEXT_FORMATS.includes(ext);

        if (lightboxFileName) lightboxFileName.textContent = fileName || 'Просмотр файла';
        if (lightboxDownloadBtn) {
            lightboxDownloadBtn.href = '/api/files/' + fileId + '/download';
            lightboxDownloadBtn.setAttribute('download', fileName || '');
        }

        if (lightboxBody) {
            lightboxBody.innerHTML = '';

            if (fileType === 'image') {
                currentFileIsText = false;
                if (lightboxZoomBtn) {
                    lightboxZoomBtn.style.display = 'inline-flex';
                    lightboxZoomBtn.classList.remove('is-active');
                    lightboxZoomBtn.setAttribute('title', 'Переключить масштаб 1:1 (Исходный размер) / По размеру окна');
                }
                lightboxBody.innerHTML = '<img class="lightbox-image" src="/api/files/' + fileId + '/view" alt="' + escapeHtmlText(fileName) + '">';
                var img = lightboxBody.querySelector('.lightbox-image');
                function toggleImageZoom() {
                    if (!img) return;
                    var isActual = img.classList.toggle('is-actual-size');
                    if (lightboxZoomBtn) {
                        lightboxZoomBtn.classList.toggle('is-active', isActual);
                        lightboxZoomBtn.setAttribute('title', isActual ? 'По размеру окна' : 'Переключить масштаб 1:1 (Исходный размер) / По размеру окна');
                    }
                }
                if (img) img.addEventListener('click', toggleImageZoom);
                if (lightboxZoomBtn) lightboxZoomBtn.onclick = toggleImageZoom;
            } else {
                if (lightboxZoomBtn) {
                    lightboxZoomBtn.style.display = 'none';
                    lightboxZoomBtn.classList.remove('is-active');
                    lightboxZoomBtn.onclick = null;
                }

                if (fileType === 'pdf') {
                    currentFileIsText = false;
                    lightboxBody.innerHTML = '<iframe class="lightbox-iframe" src="/api/files/' + fileId + '/view"></iframe>';
                } else if (fileType === 'excel' || fileType === 'xml' || fileType === 'doc' || fileType === 'docx' || fileType === 'text' || isWordDoc || isTextFile) {
                    lightboxBody.innerHTML = '<div class="lightbox-loading"><span class="lightbox-spinner"></span><span>Загрузка предпросмотра...</span></div>';
                    fetch('/api/files/' + fileId + '/preview')
                        .then(function(r) { return r.json(); })
                        .then(function(data) {
                            if (!data.success) {
                                if (isWordDoc) {
                                    var docName = (data && data.originalName) || fileName || 'Документ Word';
                                    var dlUrl = (data && data.downloadUrl) || ('/api/files/' + fileId + '/download');
                                    var errorMsg = (data && data.error) || 'Не удалось загрузить содержимое документа Word. Попробуйте скачать файл.';
                                    lightboxBody.innerHTML =
                                        '<div class="lightbox-doc-wrap">' +
                                            '<div class="lightbox-doc-fallback">' +
                                                '<div class="lightbox-doc-fallback__icon">' +
                                                    '<svg width="56" height="56" viewBox="0 0 24 24" fill="none" stroke="#2563eb" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">' +
                                                        '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>' +
                                                        '<polyline points="14 2 14 8 20 8"/>' +
                                                        '<line x1="16" y1="13" x2="8" y2="13"/>' +
                                                        '<line x1="16" y1="17" x2="8" y2="17"/>' +
                                                        '<line x1="10" y1="9" x2="8" y2="9"/>' +
                                                    '</svg>' +
                                                '</div>' +
                                                '<h3 class="lightbox-doc-fallback__title">' + escapeHtmlText(docName) + '</h3>' +
                                                '<p class="lightbox-doc-fallback__desc">' + escapeHtmlText(errorMsg) + '</p>' +
                                                '<a href="' + dlUrl + '" class="lightbox-doc-fallback__btn" download="' + escapeHtmlText(docName) + '">' +
                                                    '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' +
                                                        '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/>' +
                                                        '<polyline points="7 10 12 15 17 10"/>' +
                                                        '<line x1="12" y1="15" x2="12" y2="3"/>' +
                                                    '</svg>' +
                                                    '<span>Скачать документ</span>' +
                                                '</a>' +
                                            '</div>' +
                                        '</div>';
                                    return;
                                }
                                var defaultErr = 'Формат не поддерживается';
                                lightboxBody.innerHTML = '<div class="lightbox-error"><span>Не удалось загрузить предпросмотр: ' + escapeHtmlText((data && data.error) || defaultErr) + '</span><br><a href="/api/files/' + fileId + '/download" class="lightbox-error-btn">Скачать файл</a></div>';
                                return;
                            }
                            if (data.type === 'docx' || data.type === 'doc') {
                                currentFileIsText = false;
                                lightboxBody.innerHTML = '<div class="lightbox-doc-wrap"><div class="lightbox-doc-content">' + (data.html || '<p><em>Документ пуст</em></p>') + '</div></div>';
                            } else if (data.type === 'doc_fallback') {
                                currentFileIsText = false;
                                var docName = data.originalName || fileName || 'Документ Word';
                                var docMsg = data.message || 'Формат .doc не поддерживает интерактивный веб-предпросмотр. Скачайте документ для открытия в Microsoft Word.';
                                var dlUrl = data.downloadUrl || ('/api/files/' + fileId + '/download');
                                lightboxBody.innerHTML =
                                    '<div class="lightbox-doc-wrap">' +
                                        '<div class="lightbox-doc-fallback">' +
                                            '<div class="lightbox-doc-fallback__icon">' +
                                                '<svg width="56" height="56" viewBox="0 0 24 24" fill="none" stroke="#2563eb" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">' +
                                                    '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>' +
                                                    '<polyline points="14 2 14 8 20 8"/>' +
                                                    '<line x1="16" y1="13" x2="8" y2="13"/>' +
                                                    '<line x1="16" y1="17" x2="8" y2="17"/>' +
                                                    '<line x1="10" y1="9" x2="8" y2="9"/>' +
                                                '</svg>' +
                                            '</div>' +
                                            '<h3 class="lightbox-doc-fallback__title">' + escapeHtmlText(docName) + '</h3>' +
                                            '<p class="lightbox-doc-fallback__desc">' + escapeHtmlText(docMsg) + '</p>' +
                                            '<a href="' + dlUrl + '" class="lightbox-doc-fallback__btn" download="' + escapeHtmlText(docName) + '">' +
                                                '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' +
                                                    '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/>' +
                                                    '<polyline points="7 10 12 15 17 10"/>' +
                                                    '<line x1="12" y1="15" x2="12" y2="3"/>' +
                                                '</svg>' +
                                                '<span>Скачать документ</span>' +
                                            '</a>' +
                                        '</div>' +
                                    '</div>';
                            } else if (data.type === 'excel') {
                                currentFileIsText = false;
                                // Render sheet selector if multiple sheets, and spreadsheet table
                                var html = '<div class="lightbox-spreadsheet-container">';
                                if (data.sheetNames && data.sheetNames.length > 1) {
                                    html += '<div class="lightbox-sheet-tabs">';
                                    data.sheetNames.forEach(function(s) {
                                        html += '<button type="button" class="lightbox-sheet-tab' + (s === data.currentSheet ? ' is-active' : '') + '" data-sheet="' + escapeHtmlText(s) + '">' + escapeHtmlText(s) + '</button>';
                                    });
                                    html += '</div>';
                                }
                                html += '<div class="lightbox-table-scroll">' + data.html + '</div>';
                                html += '</div>';
                                lightboxBody.innerHTML = html;

                                // Sheet tab switcher
                                var tabs = lightboxBody.querySelectorAll('.lightbox-sheet-tab');
                                tabs.forEach(function(tab) {
                                    tab.addEventListener('click', function() {
                                        var sheetName = this.getAttribute('data-sheet');
                                        fetch('/api/files/' + fileId + '/preview?sheet=' + encodeURIComponent(sheetName))
                                            .then(function(r) { return r.json(); })
                                            .then(function(resData) {
                                                if (resData.success) {
                                                    var scrollEl = lightboxBody.querySelector('.lightbox-table-scroll');
                                                    if (scrollEl) scrollEl.innerHTML = resData.html;
                                                    tabs.forEach(function(t) { t.classList.remove('is-active'); });
                                                    tab.classList.add('is-active');
                                                }
                                            });
                                    });
                                });
                            } else if (data.type === 'xml' || data.type === 'text') {
                                // Редактируемый текстовый файл (Спринт 11)
                                currentFileOriginalContent = typeof data.content === 'string' ? data.content : '';
                                currentFileIsText = true;

                                lightboxBody.innerHTML = '<div class="lightbox-editor-wrap"><textarea class="lightbox-file-editor" id="lightboxFileEditor" spellcheck="false"></textarea></div>';
                                var editor = document.getElementById('lightboxFileEditor');
                                if (editor) {
                                    editor.value = currentFileOriginalContent;

                                    editor.addEventListener('input', function() {
                                        var isDirty = editor.value !== currentFileOriginalContent;
                                        if (lightboxEditActions) {
                                            lightboxEditActions.style.display = isDirty ? 'inline-flex' : 'none';
                                        }
                                    });

                                    editor.addEventListener('keydown', function(e) {
                                        if (e.key === 'Tab') {
                                            e.preventDefault();
                                            var start = this.selectionStart;
                                            var end = this.selectionEnd;
                                            this.value = this.value.substring(0, start) + '    ' + this.value.substring(end);
                                            this.selectionStart = this.selectionEnd = start + 4;
                                            this.dispatchEvent(new Event('input'));
                                        } else if ((e.ctrlKey || e.metaKey) && (e.key === 's' || e.key === 'S' || e.key === 'ы' || e.key === 'Ы')) {
                                            e.preventDefault();
                                            if (editor.value !== currentFileOriginalContent) {
                                                saveCurrentFile();
                                            }
                                        }
                                    });
                                }
                            }
                        })
                        .catch(function(err) {
                            if (isWordDoc) {
                                var docName = fileName || 'Документ Word';
                                var dlUrl = '/api/files/' + fileId + '/download';
                                lightboxBody.innerHTML =
                                    '<div class="lightbox-doc-wrap">' +
                                        '<div class="lightbox-doc-fallback">' +
                                            '<div class="lightbox-doc-fallback__icon">' +
                                                '<svg width="56" height="56" viewBox="0 0 24 24" fill="none" stroke="#2563eb" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">' +
                                                    '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>' +
                                                    '<polyline points="14 2 14 8 20 8"/>' +
                                                    '<line x1="16" y1="13" x2="8" y2="13"/>' +
                                                    '<line x1="16" y1="17" x2="8" y2="17"/>' +
                                                    '<line x1="10" y1="9" x2="8" y2="9"/>' +
                                                '</svg>' +
                                            '</div>' +
                                            '<h3 class="lightbox-doc-fallback__title">' + escapeHtmlText(docName) + '</h3>' +
                                            '<p class="lightbox-doc-fallback__desc">Не удалось загрузить содержимое документа Word. Попробуйте скачать файл.</p>' +
                                            '<a href="' + dlUrl + '" class="lightbox-doc-fallback__btn" download="' + escapeHtmlText(docName) + '">' +
                                                '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' +
                                                    '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/>' +
                                                    '<polyline points="7 10 12 15 17 10"/>' +
                                                    '<line x1="12" y1="15" x2="12" y2="3"/>' +
                                                '</svg>' +
                                                '<span>Скачать документ</span>' +
                                            '</a>' +
                                        '</div>' +
                                    '</div>';
                                return;
                            }
                            lightboxBody.innerHTML = '<div class="lightbox-error"><span>Ошибка сети при загрузке предпросмотра</span><br><a href="/api/files/' + fileId + '/download" class="lightbox-error-btn">Скачать файл</a></div>';
                        });
                } else {
                    currentFileIsText = false;
                    if (isWordDoc) {
                        var docName = fileName || 'Документ Word';
                        var dlUrl = '/api/files/' + fileId + '/download';
                        lightboxBody.innerHTML =
                            '<div class="lightbox-doc-wrap">' +
                                '<div class="lightbox-doc-fallback">' +
                                    '<div class="lightbox-doc-fallback__icon">' +
                                        '<svg width="56" height="56" viewBox="0 0 24 24" fill="none" stroke="#2563eb" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">' +
                                            '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>' +
                                            '<polyline points="14 2 14 8 20 8"/>' +
                                            '<line x1="16" y1="13" x2="8" y2="13"/>' +
                                            '<line x1="16" y1="17" x2="8" y2="17"/>' +
                                            '<line x1="10" y1="9" x2="8" y2="9"/>' +
                                        '</svg>' +
                                    '</div>' +
                                    '<h3 class="lightbox-doc-fallback__title">' + escapeHtmlText(docName) + '</h3>' +
                                    '<p class="lightbox-doc-fallback__desc">Предпросмотр документа Word недоступен. Скачайте файл для открытия в Word.</p>' +
                                    '<a href="' + dlUrl + '" class="lightbox-doc-fallback__btn" download="' + escapeHtmlText(docName) + '">' +
                                        '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' +
                                            '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/>' +
                                            '<polyline points="7 10 12 15 17 10"/>' +
                                            '<line x1="12" y1="15" x2="12" y2="3"/>' +
                                        '</svg>' +
                                        '<span>Скачать документ</span>' +
                                    '</a>' +
                                '</div>' +
                            '</div>';
                    } else {
                        lightboxBody.innerHTML = '<div class="lightbox-error"><span>Предпросмотр недоступен для данного формата.</span><br><a href="/api/files/' + fileId + '/download" class="lightbox-error-btn">Скачать файл</a></div>';
                    }
                }
            }
        }

        lightboxModal.style.display = 'flex';
        document.body.style.overflow = 'hidden';
    }

    // Сохранить изменения текстового файла в лайтбоксе (Спринт 11)
    async function saveCurrentFile() {
        if (isSavingFile || !currentFileIsText || !currentFileId) return;
        var editor = document.getElementById('lightboxFileEditor');
        if (!editor) return;

        isSavingFile = true;
        if (lightboxSaveBtn) lightboxSaveBtn.disabled = true;

        try {
            var res = await fetch('/api/files/' + currentFileId + '/content', {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ content: editor.value })
            });
            var data = await res.json();
            if (res.ok && data && data.success) {
                currentFileOriginalContent = editor.value;
                if (lightboxEditActions) {
                    lightboxEditActions.style.display = 'none';
                }
                var msg = window.ITS_I18n ? window.ITS_I18n.t('lightbox.savedToast') : 'Файл успешно сохранен';
                showToast(msg);

                // Обновить размер файла в карточке файла (.file-chip)
                if (typeof data.newSize === 'number') {
                    var chip = document.querySelector('.file-chip[data-file-id="' + currentFileId + '"]');
                    if (chip) {
                        var sizeEl = chip.querySelector('.file-chip-size');
                        if (sizeEl) {
                            var fileNameAttr = chip.getAttribute('data-file-name') || '';
                            var sizeInfo = getFileTypeInfo(fileNameAttr, data.newSize);
                            sizeEl.textContent = sizeInfo.sizeFormatted;
                        }
                    }
                }
            } else {
                var errMsg = (data && data.error) || 'Ошибка при сохранении файла';
                showToast(errMsg, 'error');
            }
        } catch (err) {
            console.error('Ошибка сохранения файла:', err);
            showToast('Ошибка сети при сохранении файла', 'error');
        } finally {
            isSavingFile = false;
            if (lightboxSaveBtn) lightboxSaveBtn.disabled = false;
        }
    }

    // Обработчики кнопок редактирования лайтбокса
    if (lightboxCancelBtn) {
        lightboxCancelBtn.addEventListener('click', function(e) {
            e.preventDefault();
            var editor = document.getElementById('lightboxFileEditor');
            if (editor) {
                editor.value = currentFileOriginalContent;
            }
            if (lightboxEditActions) {
                lightboxEditActions.style.display = 'none';
            }
        });
    }

    if (lightboxSaveBtn) {
        lightboxSaveBtn.addEventListener('click', function(e) {
            e.preventDefault();
            saveCurrentFile();
        });
    }

    // Защита от закрытия лайтбокса при наличии несохраненных правок (Спринт 11)
    function requestCloseLightbox() {
        var editor = document.getElementById('lightboxFileEditor');
        var isDirty = currentFileIsText && editor && editor.value !== currentFileOriginalContent;

        if (isDirty) {
            window.showConfirmDialog({
                title: window.ITS_I18n ? window.ITS_I18n.t('lightbox.unsavedTitle') : 'Несохраненные изменения',
                message: window.ITS_I18n ? window.ITS_I18n.t('lightbox.unsavedConfirmDesc') : 'В файле есть несохраненные правки. Вы уверены, что хотите закрыть предпросмотр без сохранения?',
                confirmText: window.ITS_I18n ? window.ITS_I18n.t('lightbox.unsavedConfirm') : 'Закрыть без сохранения',
                cancelText: window.ITS_I18n ? window.ITS_I18n.t('lightbox.unsavedContinue') : 'Продолжить редактирование',
                onConfirm: function() {
                    currentFileIsText = false;
                    currentFileOriginalContent = '';
                    if (lightboxEditActions) lightboxEditActions.style.display = 'none';
                    closeLightbox();
                }
            });
            return;
        }

        closeLightbox();
    }

    function closeLightbox() {
        if (!lightboxModal) return;
        lightboxModal.style.display = 'none';
        document.body.style.overflow = '';
        currentFileIsText = false;
        currentFileOriginalContent = '';
        currentFileId = null;
        isSavingFile = false;
        if (lightboxEditActions) lightboxEditActions.style.display = 'none';
        if (lightboxZoomBtn) {
            lightboxZoomBtn.style.display = 'none';
            lightboxZoomBtn.classList.remove('is-active');
            lightboxZoomBtn.onclick = null;
        }
        if (lightboxBody) lightboxBody.innerHTML = '';
    }

    if (lightboxCloseBtn) {
        lightboxCloseBtn.addEventListener('click', requestCloseLightbox);
    }

    if (lightboxOverlay) {
        lightboxOverlay.addEventListener('click', requestCloseLightbox);
    }

    document.addEventListener('keydown', function(e) {
        if (e.key === 'Escape' && lightboxModal && lightboxModal.style.display === 'flex') {
            var confirmModal = document.getElementById('unifiedConfirmModal');
            if (confirmModal && confirmModal.style.display === 'flex') {
                return;
            }
            requestCloseLightbox();
        }
    });
})();
