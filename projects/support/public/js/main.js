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

    // Format IP addresses
    safe = safe.replace(/\b(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\b/g, '<span class="wiki-token-ip">$1</span>');

    // Format AnyDesk numbers (e.g. 1 170 691 894 or 1218740083)
    safe = safe.replace(/\b(\d{1,4}\s\d{3}\s\d{3,4}|\d{9,10})\b/g, '<span class="wiki-token-anydesk">$1</span>');

    return safe;
}

function formatLineContent(lineTrimmed) {
    if (!lineTrimmed) return '&nbsp;';

    // 1. Key: Value pattern (e.g., "Anydesk: 987 654 321", "RDP: arg.local\admin")
    var colonIdx = lineTrimmed.indexOf(':');
    if (colonIdx > 0 && colonIdx < 35 && !lineTrimmed.slice(0, colonIdx).includes('http')) {
        var key = lineTrimmed.slice(0, colonIdx).trim();
        var val = lineTrimmed.slice(colonIdx + 1).trim();
        return '<span class="wiki-token-key">' + escapeHtmlText(key) + ':</span> <span class="wiki-token-val">' + formatTokens(val) + '</span>';
    }

    // 2. Value - Description or Description - Value (e.g. "kuadmin123 - Пароль от Ани деск", "кластер серверов - terminal")
    var dashMatch = lineTrimmed.match(/^([^\-–—]+)\s*[\-–—]\s*(.+)$/);
    if (dashMatch) {
        var left = dashMatch[1].trim();
        var right = dashMatch[2].trim();

        // If left is description ("кластер серверов", "сервер базы")
        if (/^(?:кластер|сервер базы|база данных|хост|dns)/i.test(left)) {
            return '<span class="wiki-token-key">' + escapeHtmlText(left) + ':</span> <span class="wiki-token-val">' + formatTokens(right) + '</span>';
        }

        // Default: left is password/token, right is explanation
        return '<span class="wiki-token-val">' + formatTokens(left) + '</span> <span class="wiki-token-key">— ' + escapeHtmlText(right) + '</span>';
    }

    // 3. Standalone line with tokens
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

        html += '<div class="' + lineClasses + '" data-line-index="' + i + '">' +
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
            var textEl = lineEl.querySelector('.wiki-line-text');
            var rawTextToCopy = textEl ? textEl.innerText.trim() : '';
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

    if (editBtnTop) {
        editBtnTop.textContent = _t('wiki.view.btn') || 'Просмотр';
        editBtnTop.classList.add('is-active');
    }
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

    if (editBtnTop) {
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

    if (cleanCur !== cleanBase) {
        if (!confirm('Отменить изменения? Несохраненные правки будут потеряны.')) {
            return;
        }
    }

    // Reset editor content back to initial
    setEditorContent(baseContent);

    // Remove draft from storage if cancelled
    if (currentOrgId) {
        localStorage.removeItem('draft_org_' + currentOrgId.value);
        if (draftStatus) draftStatus.innerHTML = '';
    }

    exitEditMode();
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
    confirmDeleteBtn.addEventListener('click', async function() {
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

        if (!confirm(window.ITS_I18n ? window.ITS_I18n.tf('wiki.confirm.delete', orgNameText) : 'Удалить "' + orgNameText + '"?')) return;

        try {
            var response = await fetch(API_BASE + '/' + orgId, { method: 'DELETE' });
            var data = await response.json();
            if (!response.ok) {
                alert(data.error || 'Ошибка удаления организации');
                return;
            }
            if (deleteOrgModal) deleteOrgModal.style.display = 'none';
            window.location.href = '/wiki';
        } catch (err) {
            console.error('Ошибка удаления:', err);
            alert('Ошибка соединения с сервером');
        }
    });
}

/* ============================================================
   SIDEBAR INSTANT SEARCH & FILTER
   ============================================================ */

if (wikiSearch && wikiOrgList) {
    var allItems = Array.from(wikiOrgList.querySelectorAll('.wiki-list-item'));
    var totalCount = allItems.length;

    function applyFilter() {
        var query = wikiSearch.value.trim().toLowerCase();
        var visibleCount = 0;

        if (wikiSearchClear) {
            wikiSearchClear.style.display = query ? 'flex' : 'none';
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
   COMPANY TABS (Данные / Базы 1С — Спринт 4)
   ============================================================ */

(function initCompanyTabs() {
    var tabDataBtn = document.getElementById('tabBtnData');
    var tab1cBtn   = document.getElementById('tabBtn1c');
    var paneData   = document.getElementById('tabPaneData');
    var pane1c     = document.getElementById('tabPane1c');
    var orgIdInput = document.getElementById('currentOrgId');

    if (!tabDataBtn || !tab1cBtn || !paneData || !pane1c) return;

    var orgId = orgIdInput ? orgIdInput.value : '';

    function switchTab(tabKey, updateUrl) {
        if (tabKey === '1c') {
            tabDataBtn.classList.remove('is-active');
            tabDataBtn.setAttribute('aria-selected', 'false');
            tab1cBtn.classList.add('is-active');
            tab1cBtn.setAttribute('aria-selected', 'true');

            paneData.style.display = 'none';
            pane1c.style.display = 'block';

            if (window.sessionStorage) {
                sessionStorage.setItem('wiki_active_tab', '1c');
                if (orgId) sessionStorage.setItem('wiki_active_tab_' + orgId, '1c');
            }
            if (updateUrl && window.history && history.replaceState) {
                history.replaceState(null, null, '#tab-1c');
            }
        } else {
            tab1cBtn.classList.remove('is-active');
            tab1cBtn.setAttribute('aria-selected', 'false');
            tabDataBtn.classList.add('is-active');
            tabDataBtn.setAttribute('aria-selected', 'true');

            pane1c.style.display = 'none';
            paneData.style.display = 'block';

            if (window.sessionStorage) {
                sessionStorage.setItem('wiki_active_tab', 'data');
                if (orgId) sessionStorage.setItem('wiki_active_tab_' + orgId, 'data');
            }
            if (updateUrl && window.history && history.replaceState) {
                history.replaceState(null, null, '#tab-data');
            }
        }
    }

    tabDataBtn.addEventListener('click', function() { switchTab('data', true); });
    tab1cBtn.addEventListener('click', function() { switchTab('1c', true); });

    // Initial tab detection
    var initialTab = 'data';
    if (window.location.hash === '#tab-1c') {
        initialTab = '1c';
    } else if (window.location.hash === '#tab-data') {
        initialTab = 'data';
    } else if (window.sessionStorage) {
        var savedOrgTab = orgId ? sessionStorage.getItem('wiki_active_tab_' + orgId) : null;
        var savedTab = savedOrgTab || sessionStorage.getItem('wiki_active_tab');
        if (savedTab === '1c') initialTab = '1c';
    }

    switchTab(initialTab, false);

    window.addEventListener('hashchange', function() {
        if (window.location.hash === '#tab-1c') {
            switchTab('1c', false);
        } else if (window.location.hash === '#tab-data') {
            switchTab('data', false);
        }
    });

    window.switchCompanyTab = switchTab;
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
                if (!confirm('Удалить информационную базу "' + name + '"?')) return;

                try {
                    var res = await fetch('/api/databases/' + id, { method: 'DELETE' });
                    var result = await res.json();
                    if (!res.ok) {
                        alert(result.error || 'Ошибка удаления базы');
                        return;
                    }
                    var row = deleteBtn.closest('tr');
                    if (row) row.remove();
                    updateViewCounts();
                    showToast('База 1С успешно удалена');
                } catch (err) {
                    console.error('Ошибка удаления базы 1С:', err);
                    alert('Ошибка соединения с сервером');
                }
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
