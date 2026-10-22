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
const smartServerViewer      = document.getElementById('smartServerViewer');
const smartViewerActions     = document.getElementById('smartViewerActions');
const copyAllSummaryBtn      = document.getElementById('copyAllSummaryBtn');
const contentSectionSubtitle = document.getElementById('contentSectionSubtitle');
const floatingToastContainer = document.getElementById('floatingToastContainer');

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
   SMART PARSER: RAW NOTES -> STRUCTURED SERVER CONFIG
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

function parseServerConfig(rawContent, orgName) {
    const rawText = cleanHtmlToText(rawContent).trim();
    const name = orgName || (orgNameInput ? orgNameInput.value : '');

    if (!rawText) {
        return {
            isEmpty: true,
            orgName: name,
            env: /dev|тест|vds|test/i.test(name) ? 'DEV' : 'PROD',
            remote: {},
            database: {},
            hardware: {},
            notes: []
        };
    }

    const lines = rawText.split('\n').map(function(l) { return l.trim(); }).filter(Boolean);
    const consumedIndices = new Set();

    const env = (/dev|тест|vds|test/i.test(name) || /dev|тест|vds|test/i.test(rawText)) ? 'DEV' : 'PROD';

    const remote = {
        anydeskId: '',
        anydeskPass: '',
        rdpHost: '',
        rdpUser: '',
        rdpPass: ''
    };

    const database = {
        dbType: '',
        dbUser: '',
        dbPass: '',
        dbCluster: '',
        onecUser: '',
        onecPass: ''
    };

    const hardware = {
        disks: [],
        iloHost: '',
        iloUser: '',
        iloPass: '',
        iloDns: ''
    };

    const notes = [];

    // 1. Pass: Single-line patterns with prefixes
    for (var i = 0; i < lines.length; i++) {
        var line = lines[i];

        // Anydesk pass like "kuadmin123 - Пароль от Ани деск"
        var anyPassMatch = line.match(/^([^\s]+)\s*[-–—]\s*(?:пароль\s*от\s*ани\s*деск|anydesk\s*pass)/i);
        if (anyPassMatch) {
            remote.anydeskPass = anyPassMatch[1];
            consumedIndices.add(i);
            continue;
        }

        // RDP pass like "Ku@dmin123Q - mstsc"
        var rdpPassMatch = line.match(/^([^\s]+)\s*[-–—]\s*mstsc/i);
        if (rdpPassMatch) {
            remote.rdpPass = rdpPassMatch[1];
            consumedIndices.add(i);
            continue;
        }

        // Anydesk: 987 654 321
        var anyLineMatch = line.match(/^anydesk\s*[:]\s*([0-9\s]{9,15})/i);
        if (anyLineMatch) {
            remote.anydeskId = anyLineMatch[1].trim();
            consumedIndices.add(i);
            continue;
        }

        // RDP: arg.local\admin
        var rdpLineMatch = line.match(/^rdp\s*[:]\s*(.+)/i);
        if (rdpLineMatch) {
            var val = rdpLineMatch[1].trim();
            if (val.includes('\\')) {
                var rdpParts = val.split('\\');
                remote.rdpHost = rdpParts[0];
                remote.rdpUser = val;
            } else {
                remote.rdpUser = val;
            }
            consumedIndices.add(i);
            continue;
        }

        // MS SQL 2019: sa / ArgPass2026
        var dbLineMatch = line.match(/^(MS\s*SQL[^:]*|PostgreSQL|MySQL|MariaDB|Oracle)\s*[:]\s*(.+)/i);
        if (dbLineMatch) {
            database.dbType = dbLineMatch[1].trim();
            var rest = dbLineMatch[2].trim();
            if (rest.includes('/')) {
                var p = rest.split('/');
                database.dbUser = p[0].trim();
                database.dbPass = p[1].trim();
            } else {
                database.dbUser = rest;
            }
            consumedIndices.add(i);
            continue;
        }

        // 1C: USR1CV8 / ArgPass1C!
        var onecLineMatch = line.match(/^(?:1[cс]|1с:предприятие)\s*[:]\s*(.+)/i);
        if (onecLineMatch) {
            var restOnec = onecLineMatch[1].trim();
            if (restOnec.includes('/')) {
                var op = restOnec.split('/');
                database.onecUser = op[0].trim();
                database.onecPass = op[1].trim();
            } else {
                database.onecUser = restOnec;
            }
            consumedIndices.add(i);
            continue;
        }

        // кластер серверов - terminal
        var clusterMatch = line.match(/^кластер\s*серверов\s*[-–—:]\s*(.+)/i);
        if (clusterMatch) {
            database.dbCluster = clusterMatch[1].trim();
            consumedIndices.add(i);
            continue;
        }

        // Сервер базы данных - SQL
        var dbHostMatch = line.match(/^сервер\s*базы\s*данных\s*[-–—:]\s*(.+)/i);
        if (dbHostMatch) {
            database.dbType = (database.dbType ? database.dbType + ' / ' : '') + dbHostMatch[1].trim();
            consumedIndices.add(i);
            continue;
        }
    }

    // 2. Pass: Multi-line blocks (AnyDesk, MS SQL, Disks, USR1CV8, Ilo)
    for (var j = 0; j < lines.length; j++) {
        if (consumedIndices.has(j)) continue;
        var cur = lines[j];

        // AnyDesk standalone header
        if (/^anydesk$/i.test(cur)) {
            consumedIndices.add(j);
            if (j + 1 < lines.length && !consumedIndices.has(j + 1)) {
                var nextLine = lines[j + 1];
                var idMatch = nextLine.match(/([0-9\s]{9,15})/);
                if (idMatch) {
                    remote.anydeskId = idMatch[1].trim();
                    if (nextLine.includes('/')) {
                        var subParts = nextLine.split('/');
                        if (subParts.length >= 2 && !remote.rdpUser) {
                            remote.rdpUser = subParts.slice(1).join('/');
                        }
                    }
                    consumedIndices.add(j + 1);
                    if (j + 2 < lines.length && !consumedIndices.has(j + 2)) {
                        var nextNext = lines[j + 2];
                        if (!/^(ms sql|os |data |ilo|usr1cv8|администратор)/i.test(nextNext)) {
                            if (!remote.anydeskPass) remote.anydeskPass = nextNext;
                            consumedIndices.add(j + 2);
                        }
                    }
                }
            }
            continue;
        }

        // MS SQL block
        if (/^ms\s*sql\b/i.test(cur)) {
            database.dbType = cur;
            consumedIndices.add(j);
            if (j + 1 < lines.length && !consumedIndices.has(j + 1)) {
                database.dbUser = lines[j + 1];
                consumedIndices.add(j + 1);
                if (j + 2 < lines.length && !consumedIndices.has(j + 2)) {
                    database.dbPass = lines[j + 2];
                    consumedIndices.add(j + 2);
                }
            }
            continue;
        }

        // sa user standalone
        if (/^sa$/i.test(cur) && !database.dbUser) {
            database.dbUser = 'sa';
            consumedIndices.add(j);
            if (j + 1 < lines.length && !consumedIndices.has(j + 1)) {
                database.dbPass = lines[j + 1];
                consumedIndices.add(j + 1);
            }
            continue;
        }

        // USR1CV8 standalone
        if (/^usr1cv8$/i.test(cur)) {
            database.onecUser = 'USR1CV8';
            consumedIndices.add(j);
            if (j + 1 < lines.length && !consumedIndices.has(j + 1)) {
                database.onecPass = lines[j + 1];
                consumedIndices.add(j + 1);
            }
            continue;
        }

        // Disks: NVME / RAID / SSD / SATA
        if (/\b(?:nvme|raid\s*\d*|ssd|sata|hdd|sas)\b/i.test(cur)) {
            hardware.disks.push(cur);
            consumedIndices.add(j);
            continue;
        }

        // Ilo block
        if (/^(?:ilo|ipmi)$/i.test(cur)) {
            consumedIndices.add(j);
            var idx = j + 1;
            while (idx < lines.length && idx <= j + 6 && !consumedIndices.has(idx)) {
                var l = lines[idx];
                if (/^(?:dhcp|\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})$/i.test(l)) {
                    hardware.iloHost = l;
                    consumedIndices.add(idx);
                } else if (/^(?:administrator|admin)$/i.test(l)) {
                    hardware.iloUser = l;
                    consumedIndices.add(idx);
                } else if (/^(?:dns|serial|iloczjd)/i.test(l)) {
                    hardware.iloDns = (hardware.iloDns ? hardware.iloDns + ' / ' : '') + l;
                    consumedIndices.add(idx);
                } else if (hardware.iloUser && !hardware.iloPass) {
                    hardware.iloPass = l;
                    consumedIndices.add(idx);
                } else {
                    break;
                }
                idx++;
            }
            continue;
        }

        // Standalone user "Администратор" or domain user
        if (/^(?:администратор|administrator)$/i.test(cur) && !remote.rdpUser) {
            remote.rdpUser = cur;
            consumedIndices.add(j);
            if (j + 1 < lines.length && !consumedIndices.has(j + 1)) {
                var nextL = lines[j + 1];
                if (!/^(anydesk|ms sql|os |ilo|usr1cv8)/i.test(nextL)) {
                    remote.rdpPass = nextL;
                    consumedIndices.add(j + 1);
                }
            }
            continue;
        }

        // Domain user like "antraium.local\администратор"
        if (cur.includes('\\')) {
            remote.rdpUser = cur;
            var parts = cur.split('\\');
            if (parts[0]) remote.rdpHost = parts[0];
            consumedIndices.add(j);
            if (j + 1 < lines.length && !consumedIndices.has(j + 1)) {
                var nextAfterDom = lines[j + 1];
                if (!/^(anydesk|ms sql|os |ilo|usr1cv8|данные|кластер|сервер)/i.test(nextAfterDom)) {
                    if (!remote.rdpPass) remote.rdpPass = nextAfterDom;
                    consumedIndices.add(j + 1);
                }
            }
            continue;
        }

        // Header titles
        if (/^(?:для входа|данные для)/i.test(cur)) {
            consumedIndices.add(j);
            continue;
        }
    }

    // Remaining lines -> notes
    for (var k = 0; k < lines.length; k++) {
        if (!consumedIndices.has(k)) {
            notes.push(lines[k]);
        }
    }

    return {
        isEmpty: false,
        orgName: name,
        env: env,
        remote: remote,
        database: database,
        hardware: hardware,
        notes: notes
    };
}

/* ============================================================
   SMART SERVER VIEWER DOM BUILDER
   ============================================================ */

function renderSmartViewer(data) {
    if (!smartServerViewer) return;
    activeServerData = data;

    if (data.isEmpty) {
        if (copyAllSummaryBtn) copyAllSummaryBtn.style.display = 'none';
        smartServerViewer.innerHTML =
            '<div class="smart-empty-state">' +
                '<svg class="smart-empty-state__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">' +
                    '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>' +
                    '<path d="M12 8v4M12 16h.01" stroke-width="2" stroke-linecap="round"/>' +
                '</svg>' +
                '<h4 class="smart-empty-state__title">Конфигурация сервера пока не заполнена</h4>' +
                '<p class="smart-empty-state__desc">Учетные данные AnyDesk, RDP, SQL, 1C и iLO для этого сервера еще не внесены в базу знаний.</p>' +
                '<button type="button" class="btn-fill-config" id="emptyStateEditBtn">' +
                    '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>' +
                    '<span>Заполнить конфигурацию сервера →</span>' +
                '</button>' +
            '</div>';

        var emptyEditBtn = document.getElementById('emptyStateEditBtn');
        if (emptyEditBtn) {
            emptyEditBtn.addEventListener('click', enterEditMode);
        }
        return;
    }

    if (copyAllSummaryBtn) copyAllSummaryBtn.style.display = 'inline-flex';

    var html = '<div class="smart-grid">';

    // ── Card 1: Remote & RDP ──
    html +=
        '<div class="smart-card" id="cardRemote">' +
            '<div class="smart-card-header">' +
                '<div class="smart-card-header__left">' +
                    '<svg class="smart-card-header__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="3" width="20" height="14" rx="2" ry="2"/><line x1="8" y1="21" x2="16" y2="21"/><line x1="12" y1="17" x2="12" y2="21"/></svg>' +
                    '<h4 class="smart-card-header__title">Удаленный доступ & RDP</h4>' +
                '</div>' +
                '<span class="smart-card-header__badge">RDP / AnyDesk</span>' +
            '</div>' +
            '<div class="smart-card-body">' +
                renderSmartRow('AnyDesk ID', data.remote.anydeskId, false, true) +
                (data.remote.anydeskPass ? renderSmartRow('AnyDesk Пароль', data.remote.anydeskPass, true) : '') +
                renderSmartRow('RDP / mstsc хост', data.remote.rdpHost, false, true) +
                renderSmartRow('Пользователь', data.remote.rdpUser, false) +
                renderSmartRow('Пароль доступа', data.remote.rdpPass, true) +
            '</div>' +
        '</div>';

    // ── Card 2: Database & 1C ──
    html +=
        '<div class="smart-card" id="cardDatabase">' +
            '<div class="smart-card-header">' +
                '<div class="smart-card-header__left">' +
                    '<svg class="smart-card-header__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"/><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"/></svg>' +
                    '<h4 class="smart-card-header__title">Базы данных & 1С:Предприятие</h4>' +
                '</div>' +
                '<span class="smart-card-header__badge">SQL / 1C</span>' +
            '</div>' +
            '<div class="smart-card-body">' +
                renderSmartRow('СУБД', data.database.dbType, false) +
                renderSmartRow('Пользователь СУБД (sa)', data.database.dbUser, false) +
                renderSmartRow('Пароль СУБД', data.database.dbPass, true) +
                renderSmartRow('Кластер / База', data.database.dbCluster, false) +
                renderSmartRow('Служба 1С', data.database.onecUser, false) +
                renderSmartRow('Пароль службы 1С', data.database.onecPass, true) +
            '</div>' +
        '</div>';

    // ── Card 3: Hardware & iLO ──
    var disksContent = '';
    if (data.hardware.disks && data.hardware.disks.length > 0) {
        disksContent = '<div class="smart-disks-list">' +
            data.hardware.disks.map(function(d) {
                return '<div class="smart-disk-item"><span class="smart-disk-dot"></span><span>' + escapeHtmlText(d) + '</span></div>';
            }).join('') +
            '</div>';
    }

    html +=
        '<div class="smart-card" id="cardHardware">' +
            '<div class="smart-card-header">' +
                '<div class="smart-card-header__left">' +
                    '<svg class="smart-card-header__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="4" y="4" width="16" height="16" rx="2" ry="2"/><rect x="9" y="9" width="6" height="6"/><line x1="9" y1="1" x2="9" y2="4"/><line x1="15" y1="1" x2="15" y2="4"/><line x1="9" y1="20" x2="9" y2="23"/><line x1="15" y1="20" x2="15" y2="23"/><line x1="20" y1="9" x2="23" y2="9"/><line x1="20" y1="14" x2="23" y2="14"/><line x1="1" y1="9" x2="4" y2="9"/><line x1="1" y1="14" x2="4" y2="14"/></svg>' +
                    '<h4 class="smart-card-header__title">Аппаратная часть & iLO / IPMI</h4>' +
                '</div>' +
                '<span class="smart-card-header__badge">Hardware</span>' +
            '</div>' +
            '<div class="smart-card-body">' +
                (disksContent ? '<div class="smart-row" style="flex-direction: column; align-items: flex-start; gap: 6px;"><span class="smart-row__label">Дисковая подсистема:</span>' + disksContent + '</div>' : '') +
                renderSmartRow('iLO / IPMI адрес', data.hardware.iloHost, false, true) +
                renderSmartRow('Администратор iLO', data.hardware.iloUser, false) +
                renderSmartRow('Пароль iLO', data.hardware.iloPass, true) +
                renderSmartRow('Серийный номер / DNS', data.hardware.iloDns, false) +
            '</div>' +
        '</div>';

    // ── Card 4: Additional Notes ──
    if (data.notes && data.notes.length > 0) {
        html +=
            '<div class="smart-card smart-card--notes" id="cardNotes">' +
                '<div class="smart-card-header">' +
                    '<div class="smart-card-header__left">' +
                        '<svg class="smart-card-header__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><polyline points="10 9 9 9 8 9"/></svg>' +
                        '<h4 class="smart-card-header__title">Дополнительные параметры и заметки</h4>' +
                    '</div>' +
                    '<span class="smart-card-header__badge">Заметки</span>' +
                '</div>' +
                '<div class="smart-card-body">' +
                    '<div class="smart-notes-content">' + escapeHtmlText(data.notes.join('\n')) + '</div>' +
                '</div>' +
            '</div>';
    }

    html += '</div>'; // end .smart-grid
    smartServerViewer.innerHTML = html;

    bindSmartViewerEvents();
}

function renderSmartRow(label, value, isSecret, isMono) {
    var hasValue = Boolean(value && String(value).trim());
    var safeVal = hasValue ? escapeHtmlText(String(value).trim()) : '';

    var valueHtml = '';
    var actionsHtml = '';

    if (!hasValue) {
        valueHtml = '<span class="smart-val smart-val--empty">Не указано</span>';
    } else if (isSecret) {
        valueHtml = '<span class="smart-val smart-val--masked" data-raw-val="' + safeVal + '">••••••••</span>';
        actionsHtml =
            '<button type="button" class="smart-btn-icon smart-btn-icon--toggle" title="Показать/скрыть пароль" aria-label="Показать/скрыть">' +
                '<svg class="eye-open" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>' +
                '<svg class="eye-closed" style="display:none;" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>' +
            '</button>' +
            '<button type="button" class="smart-btn-icon smart-btn-icon--copy" data-copy-val="' + safeVal + '" data-label="' + escapeHtmlText(label) + '" title="Копировать пароль" aria-label="Копировать">' +
                '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>' +
            '</button>';
    } else {
        var monoClass = isMono ? ' font-mono' : '';
        valueHtml = '<span class="smart-val' + monoClass + '">' + safeVal + '</span>';
        actionsHtml =
            '<button type="button" class="smart-btn-icon smart-btn-icon--copy" data-copy-val="' + safeVal + '" data-label="' + escapeHtmlText(label) + '" title="Копировать ' + escapeHtmlText(label) + '" aria-label="Копировать">' +
                '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>' +
            '</button>';
    }

    return (
        '<div class="smart-row">' +
            '<span class="smart-row__label">' + escapeHtmlText(label) + ':</span>' +
            '<div class="smart-row__value-wrap">' +
                valueHtml +
                actionsHtml +
            '</div>' +
        '</div>'
    );
}

function bindSmartViewerEvents() {
    if (!smartServerViewer) return;

    // Password Eye Toggle
    smartServerViewer.querySelectorAll('.smart-btn-icon--toggle').forEach(function(btn) {
        btn.addEventListener('click', function() {
            var rowWrap = btn.closest('.smart-row__value-wrap');
            if (!rowWrap) return;
            var valEl = rowWrap.querySelector('.smart-val--masked');
            if (!valEl) return;

            var rawVal = valEl.getAttribute('data-raw-val');
            var isRevealed = valEl.classList.contains('is-revealed');
            var eyeOpen = btn.querySelector('.eye-open');
            var eyeClosed = btn.querySelector('.eye-closed');

            if (isRevealed) {
                valEl.textContent = '••••••••';
                valEl.classList.remove('is-revealed');
                if (eyeOpen) eyeOpen.style.display = 'block';
                if (eyeClosed) eyeClosed.style.display = 'none';
            } else {
                valEl.textContent = rawVal;
                valEl.classList.add('is-revealed');
                if (eyeOpen) eyeOpen.style.display = 'none';
                if (eyeClosed) eyeClosed.style.display = 'block';
            }
        });
    });

    // Copy Button
    smartServerViewer.querySelectorAll('.smart-btn-icon--copy').forEach(function(btn) {
        btn.addEventListener('click', function() {
            var copyVal = btn.getAttribute('data-copy-val');
            var label = btn.getAttribute('data-label');
            copyToClipboard(copyVal, btn, label);
        });
    });
}

function generateSummaryText(data) {
    if (!data || data.isEmpty) return '';

    var lines = [];
    lines.push('══════════════════════════════════════════════');
    lines.push('СЕРВЕР: ' + data.orgName + ' [' + data.env + ']');
    lines.push('══════════════════════════════════════════════');

    lines.push('\n[УДАЛЕННЫЙ ДОСТУП & RDP]');
    if (data.remote.anydeskId)   lines.push('AnyDesk ID: ' + data.remote.anydeskId);
    if (data.remote.anydeskPass) lines.push('AnyDesk Пароль: ' + data.remote.anydeskPass);
    if (data.remote.rdpHost)     lines.push('RDP / mstsc хост: ' + data.remote.rdpHost);
    if (data.remote.rdpUser)     lines.push('Пользователь: ' + data.remote.rdpUser);
    if (data.remote.rdpPass)     lines.push('Пароль доступа: ' + data.remote.rdpPass);

    if (data.database.dbType || data.database.dbUser || data.database.onecUser) {
        lines.push('\n[БАЗЫ ДАННЫХ & 1С]');
        if (data.database.dbType)    lines.push('СУБД: ' + data.database.dbType);
        if (data.database.dbUser)    lines.push('Пользователь sa: ' + data.database.dbUser);
        if (data.database.dbPass)    lines.push('Пароль СУБД: ' + data.database.dbPass);
        if (data.database.dbCluster) lines.push('Кластер / База: ' + data.database.dbCluster);
        if (data.database.onecUser)  lines.push('Служба 1С: ' + data.database.onecUser);
        if (data.database.onecPass)  lines.push('Пароль 1С: ' + data.database.onecPass);
    }

    if (data.hardware.iloHost || (data.hardware.disks && data.hardware.disks.length > 0)) {
        lines.push('\n[ЖЕЛЕЗО & iLO / IPMI]');
        if (data.hardware.disks && data.hardware.disks.length > 0) {
            lines.push('Диски: ' + data.hardware.disks.join(' / '));
        }
        if (data.hardware.iloHost) lines.push('iLO Хост/IP: ' + data.hardware.iloHost);
        if (data.hardware.iloUser) lines.push('iLO Пользователь: ' + data.hardware.iloUser);
        if (data.hardware.iloPass) lines.push('iLO Пароль: ' + data.hardware.iloPass);
        if (data.hardware.iloDns)  lines.push('Серийный / DNS: ' + data.hardware.iloDns);
    }

    if (data.notes && data.notes.length > 0) {
        lines.push('\n[ЗАМЕТКИ]');
        lines.push(data.notes.join('\n'));
    }

    lines.push('══════════════════════════════════════════════');
    return lines.join('\n');
}

if (copyAllSummaryBtn) {
    copyAllSummaryBtn.addEventListener('click', function() {
        if (!activeServerData) return;
        var summary = generateSummaryText(activeServerData);
        copyToClipboard(summary, copyAllSummaryBtn, 'Все реквизиты');
    });
}

/* ============================================================
   EDITOR — MODE TOGGLE (EDIT / VIEW / CANCEL)
   ============================================================ */

function enterEditMode() {
    if (!orgEditor) return;
    isEditMode = true;

    if (smartServerViewer) smartServerViewer.style.display = 'none';
    if (smartViewerActions) smartViewerActions.style.display = 'none';

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

    if (contentSectionSubtitle) {
        contentSectionSubtitle.textContent = 'Редактирование параметров';
    }

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

    if (smartServerViewer) smartServerViewer.style.display = 'flex';
    if (smartViewerActions) smartViewerActions.style.display = 'flex';

    if (contentSectionSubtitle) {
        contentSectionSubtitle.textContent = 'Smart Server Viewer';
    }

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

function escapeHtmlText(text) {
    if (!text) return '';
    var d = document.createElement('div');
    d.appendChild(document.createTextNode(text));
    return d.innerHTML;
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

    // Initial render of Smart Viewer
    var initialParsed = parseServerConfig(loadedContent, orgNameInput ? orgNameInput.value : '');
    renderSmartViewer(initialParsed);
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

            // Update local memory and re-parse Smart Viewer
            initialContent = content;
            var updatedData = parseServerConfig(content, name);
            renderSmartViewer(updatedData);

            // Exit edit mode smoothly back to Smart Viewer
            exitEditMode();
            showToast('Конфигурация сервера успешно сохранена!');

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
            var envAttr = item.getAttribute('data-env') || '';
            var nameEl = item.querySelector('.wiki-list-name');
            var text = (nameEl ? nameEl.textContent : nameAttr).toLowerCase();

            var matches = !query || text.includes(query) || nameAttr.includes(query) || envAttr.includes(query);
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
