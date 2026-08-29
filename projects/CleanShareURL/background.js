/**
 * CleanShare URL - Background Service Worker (Manifest V3)
 * Модуль очистки ссылок от маркетинговых трекеров, аналитики, UTM-меток и параметров плейлистов.
 */

// ============================================================================
// 1. КОНФИГУРАЦИЯ И ПРАВИЛА ОЧИСТКИ
// ============================================================================

/**
 * Универсальный список query-параметров отслеживания (black-list)
 */
const GLOBAL_TRACKING_PARAMS = new Set([
  // Стандартные трекеры и параметры шаринга
  'utm_source',
  'utm_medium',
  'utm_campaign',
  'utm_term',
  'utm_content',
  'utm_id',
  'utm_name',
  'utm_reader',
  'utm_referrer',
  'utm_pubreferrer',
  'utm_viz_id',
  'utm_source_platform',
  'utm_creative_format',
  'utm_marketing_tactic',
  'ref',
  'source',
  'share',
  'origin',
  'ref_src',
  'ref_url',

  // Google Ads / Analytics
  'gclid',
  'gclsrc',
  'dclid',
  'gbraid',
  'wbraid',
  'gad_source',
  '_ga',
  '_gl',
  'ga_source',
  'ga_medium',
  'ga_term',
  'ga_content',
  'ga_campaign',
  'ga_place',

  // Facebook / Meta / Instagram
  'fbclid',
  'fbadid',
  'fb_action_ids',
  'fb_action_types',
  'fb_source',
  'fb_ref',
  'igsh',
  'igshid',

  // Yandex
  'yclid',
  'ym_debug',
  '_openstat',
  'from_source',
  'from_global',

  // Microsoft / Bing
  'msclkid',
  'ms_clkid',

  // Twitter / X
  'twclid',

  // TikTok
  'ttclid',

  // LinkedIn
  'li_fat_id',
  'trk',
  'trkEmail',

  // Pinterest
  'epik',
  'pp',

  // Email / CRM / Marketing Automation
  'mc_cid',
  'mc_eid',
  'recipient_id',
  'campaign_id',
  'ml_subscriber',
  'ml_subscriber_hash',
  'mkt_tok',
  '_hsenc',
  '_hsmi',
  'hsCtaTracking',

  // E-commerce & general affiliate tracking
  'algo_pvid',
  'algo_expid',
  'btsid',
  'ws_ab_test',
  'spm',
  'scm',
  'aff_platform',
  'aff_trace_key',
  'aff_short_key',
  'click_id',
  'clickid',
  'tracking_id',
  'visitor_id'
]);

/**
 * Префиксы параметров отслеживания (удаляются любые параметры, начинающиеся с этих префиксов)
 */
const TRACKING_PREFIXES = [
  'utm_',
  'ga_',
  'fb_',
  'sc_',
  'pd_rd_',
  'pf_rd_',
  'algo_'
];

/**
 * Доменно-специфичные правила фильтрации
 */
const DOMAIN_RULES = [
  // YouTube & YouTube Music & Shorts
  {
    test: /(?:youtube\.com|youtu\.be|music\.youtube\.com)$/i,
    // Удаляем все трекеры, рефералы и параметры плейлистов, оставляя строго чистый ID видео (v=) и таймкод (t= / start=)
    stripParams: [
      'list',
      'index',
      'ab_channel',
      'start_radio',
      'rv',
      'si',
      'feature',
      'app',
      'pp',
      'embeds_referring_euri',
      'embeds_referring_origin',
      'source_ve_path',
      'theme',
      'widget_referrer'
    ]
  },
  // Spotify
  {
    test: /spotify\.com$/i,
    stripParams: ['si', 'context']
  },
  // Twitter / X
  {
    test: /(?:twitter\.com|x\.com)$/i,
    stripParams: ['s', 't', 'ref_src', 'ref_url', 'cn']
  },
  // Reddit
  {
    test: /(?:reddit\.com|redd\.it)$/i,
    stripParams: ['rdt_cid', 'share_id', 'ref', 'ref_source']
  },
  // TikTok
  {
    test: /tiktok\.com$/i,
    stripParams: ['_r', '_t', 'is_from_webapp', 'sender_device', 'share_item_id']
  },
  // Amazon
  {
    test: /(?:amazon\.[a-z.]+|amzn\.to)$/i,
    stripParams: ['tag', 'linkcode', 'camp', 'creative', 'creativeasin', 'ref_', '_encoding', 'keywords'],
    cleanPath: (pathname) => {
      // Удаляем суффиксы /ref=... в путях товаров Amazon
      return pathname.replace(/\/ref=[^/?#]+/i, '');
    }
  },
  // AliExpress
  {
    test: /(?:aliexpress\.[a-z.]+|aliexpress\.ru)$/i,
    stripParams: ['spm', 'scm', '_t', 'sk', 'aff_trace_key']
  },
  // Telegram
  {
    test: /(?:t\.me|telegram\.me)$/i,
    stripParams: ['startattach']
  }
];

/**
 * Регулярное выражение для обнаружения URL в произвольном тексте
 */
const URL_REGEX = /(?:https?:\/\/|www\.)[^\s"'<>()[\]{}]+/gi;

// ============================================================================
// 2. ДВИЖОК ОЧИСТКИ URL (CLEANING ENGINE)
// ============================================================================

/**
 * Разворачивает промежуточный редирект (например, Google / YouTube redirect)
 * @param {URL} urlObj 
 * @returns {string|null} Распакованный целевой URL или null
 */
function unwrapRedirect(urlObj) {
  const hostname = urlObj.hostname.toLowerCase();
  
  // Google Redirect: https://www.google.com/url?q=https://example.com/
  if (hostname.includes('google.') && urlObj.pathname === '/url') {
    const target = urlObj.searchParams.get('url') || urlObj.searchParams.get('q');
    if (target && target.startsWith('http')) {
      return target;
    }
  }

  // YouTube Redirect: https://www.youtube.com/redirect?q=https://example.com/
  if (hostname.includes('youtube.com') && urlObj.pathname === '/redirect') {
    const target = urlObj.searchParams.get('q');
    if (target && target.startsWith('http')) {
      return target;
    }
  }

  return null;
}

/**
 * Проверяет, является ли query-параметр трекером или параметром плейлиста для данного домена
 * @param {string} key 
 * @param {string} hostname 
 * @returns {boolean}
 */
function isTrackingParam(key, hostname) {
  const lowerKey = key.toLowerCase();

  // 1. Прямое совпадение с глобальным черным списком (utm_*, ref, source, share, fbclid, gclid, etc.)
  if (GLOBAL_TRACKING_PARAMS.has(lowerKey)) {
    return true;
  }

  // 2. Проверка по префиксам (utm_*, ga_*, etc.)
  for (const prefix of TRACKING_PREFIXES) {
    if (lowerKey.startsWith(prefix)) {
      return true;
    }
  }

  // 3. Доменно-специфичные параметры (для YouTube: list, index, ab_channel, si, etc.)
  for (const rule of DOMAIN_RULES) {
    if (rule.test.test(hostname)) {
      if (rule.stripParams && rule.stripParams.includes(lowerKey)) {
        return true;
      }
    }
  }

  return false;
}

/**
 * Очищает единичный URL от трекеров, параметров плейлистов и реферальных меток
 * @param {string} inputUrl Исходный URL
 * @returns {{ originalUrl: string, cleanedUrl: string, isCleaned: boolean, removedParams: string[], removedCount: number }}
 */
function cleanUrl(inputUrl) {
  if (!inputUrl || typeof inputUrl !== 'string') {
    return { originalUrl: inputUrl, cleanedUrl: inputUrl, isCleaned: false, removedParams: [], removedCount: 0 };
  }

  let raw = inputUrl.trim();
  const hadNoScheme = !/^https?:\/\//i.test(raw);
  if (hadNoScheme) {
    raw = 'https://' + raw;
  }

  let urlObj;
  try {
    urlObj = new URL(raw);
  } catch (e) {
    // Невалидный URL - возвращаем без изменений
    return { originalUrl: inputUrl, cleanedUrl: inputUrl, isCleaned: false, removedParams: [], removedCount: 0 };
  }

  // 1. Проверка на промежуточный редирект (Google / YouTube unwrapper)
  const unwrapped = unwrapRedirect(urlObj);
  if (unwrapped) {
    const nestedResult = cleanUrl(unwrapped);
    return {
      originalUrl: inputUrl,
      cleanedUrl: nestedResult.cleanedUrl,
      isCleaned: true,
      removedParams: ['redirect_wrapper', ...nestedResult.removedParams],
      removedCount: 1 + nestedResult.removedCount
    };
  }

  const hostname = urlObj.hostname.toLowerCase();
  const removedParams = [];

  // 2. Очистка query-параметров
  const paramsToKeep = [];
  for (const [key, value] of urlObj.searchParams.entries()) {
    if (isTrackingParam(key, hostname)) {
      removedParams.push(key);
    } else {
      paramsToKeep.push([key, value]);
    }
  }

  // Пересобираем searchParams
  urlObj.search = '';
  for (const [key, value] of paramsToKeep) {
    urlObj.searchParams.append(key, value);
  }

  // 3. Доменно-специфичная очистка путей (например, Amazon /ref=...)
  for (const rule of DOMAIN_RULES) {
    if (rule.test.test(hostname) && typeof rule.cleanPath === 'function') {
      const oldPath = urlObj.pathname;
      urlObj.pathname = rule.cleanPath(urlObj.pathname);
      if (oldPath !== urlObj.pathname) {
        removedParams.push('path_ref');
      }
    }
  }

  // 4. Очистка фрагмента (hash), если внутри hash содержатся параметры трекинга (e.g. #/route?utm_source=...)
  if (urlObj.hash && urlObj.hash.includes('?')) {
    const [hashPath, hashQuery] = urlObj.hash.split('?');
    const hashParams = new URLSearchParams(hashQuery);
    const hashParamsToKeep = [];
    
    for (const [key, value] of hashParams.entries()) {
      if (isTrackingParam(key, hostname)) {
        removedParams.push(`hash_${key}`);
      } else {
        hashParamsToKeep.push([key, value]);
      }
    }

    if (hashParamsToKeep.length > 0) {
      const newHashQuery = new URLSearchParams(hashParamsToKeep).toString();
      urlObj.hash = `${hashPath}?${newHashQuery}`;
    } else {
      urlObj.hash = hashPath;
    }
  }

  let finalUrl = urlObj.toString();

  // Удаляем trailing '?' если параметров не осталось
  finalUrl = finalUrl.replace(/\?$/, '');

  const isCleaned = removedParams.length > 0 || finalUrl !== inputUrl;

  return {
    originalUrl: inputUrl,
    cleanedUrl: finalUrl,
    isCleaned: isCleaned,
    removedParams: removedParams,
    removedCount: removedParams.length
  };
}

/**
 * Очищает все URL, найденные в произвольном тексте
 * @param {string} text Текст со ссылками
 * @returns {{ originalText: string, cleanedText: string, totalCleanedUrls: number, totalRemovedParams: number }}
 */
function cleanTextWithUrls(text) {
  if (!text || typeof text !== 'string') {
    return { originalText: text, cleanedText: text, totalCleanedUrls: 0, totalRemovedParams: 0 };
  }

  let totalCleanedUrls = 0;
  let totalRemovedParams = 0;

  const cleanedText = text.replace(URL_REGEX, (matchedUrl) => {
    const res = cleanUrl(matchedUrl);
    if (res.isCleaned) {
      totalCleanedUrls++;
      totalRemovedParams += res.removedCount;
      return res.cleanedUrl;
    }
    return matchedUrl;
  });

  return {
    originalText: text,
    cleanedText: cleanedText,
    totalCleanedUrls: totalCleanedUrls,
    totalRemovedParams: totalRemovedParams
  };
}

// ============================================================================
// 3. УПРАВЛЕНИЕ СТАТИСТИКОЙ (STORAGE)
// ============================================================================

/**
 * Обновляет статистику очищенных ссылок в chrome.storage.local
 * @param {number} urlsCount 
 * @param {number} trackersCount 
 * @param {string} [cleanedUrlSample]
 */
async function recordStats(urlsCount, trackersCount, cleanedUrlSample) {
  try {
    const data = await chrome.storage.local.get({
      totalCleanedLinks: 0,
      totalTrackersRemoved: 0,
      recentCleaned: []
    });

    const totalCleanedLinks = (data.totalCleanedLinks || 0) + urlsCount;
    const totalTrackersRemoved = (data.totalTrackersRemoved || 0) + trackersCount;

    const recentCleaned = Array.isArray(data.recentCleaned) ? data.recentCleaned : [];
    if (cleanedUrlSample) {
      recentCleaned.unshift({
        url: cleanedUrlSample,
        trackersRemoved: trackersCount,
        timestamp: Date.now()
      });
      if (recentCleaned.length > 30) {
        recentCleaned.pop();
      }
    }

    await chrome.storage.local.set({
      totalCleanedLinks,
      totalTrackersRemoved,
      recentCleaned
    });
  } catch (err) {
    console.error('[CleanShare] Ошибка записи статистики в storage:', err);
  }
}

// ============================================================================
// 4. КОНТЕКСТНОЕ МЕНЮ И ЗАПИСЬ В БУФЕР ОБМЕНА
// ============================================================================

function initContextMenus() {
  chrome.contextMenus.removeAll(() => {
    chrome.contextMenus.create({
      id: 'cleanshare_clean_link',
      title: '✨ Скопировать чистую ссылку (CleanShare)',
      contexts: ['link']
    });

    chrome.contextMenus.create({
      id: 'cleanshare_clean_page',
      title: '✨ Очистить и скопировать URL страницы',
      contexts: ['page']
    });

    chrome.contextMenus.create({
      id: 'cleanshare_clean_selection',
      title: '✨ Очистить ссылки в выделенном тексте',
      contexts: ['selection']
    });
  });
}

/**
 * Записывает чистый текст в буфер обмена на активной вкладке и показывает Toast (только если была очистка)
 * @param {number} tabId 
 * @param {string} textToCopy 
 * @param {Object} toastPayload 
 */
async function copyToClipboardInTab(tabId, textToCopy, toastPayload = {}) {
  try {
    // 1. Показываем Toast только если были удалены трекеры (чтобы не спамить)
    if (tabId && toastPayload.showToast) {
      chrome.tabs.sendMessage(tabId, {
        action: 'SHOW_TOAST',
        title: toastPayload.title || 'CleanShare URL',
        message: toastPayload.message || 'Ссылка очищена и скопирована!',
        trackersRemoved: toastPayload.trackersRemoved || 0,
        sampleUrl: toastPayload.sampleUrl || textToCopy
      }).catch(() => {
        // Игнорируем ошибку, если страница не поддерживает content script
      });
    }

    // 2. Надежная запись в системный буфер обмена с fallback через textarea
    await chrome.scripting.executeScript({
      target: { tabId: tabId },
      func: (text) => {
        if (navigator.clipboard && navigator.clipboard.writeText) {
          navigator.clipboard.writeText(text).catch(() => {
            fallbackCopy(text);
          });
        } else {
          fallbackCopy(text);
        }

        function fallbackCopy(str) {
          const ta = document.createElement('textarea');
          ta.value = str;
          ta.style.position = 'fixed';
          ta.style.opacity = '0';
          ta.style.left = '-9999px';
          document.body.appendChild(ta);
          ta.focus();
          ta.select();
          try {
            document.execCommand('copy');
          } catch (err) {
            console.warn('[CleanShare] Не удалось скопировать через execCommand:', err);
          }
          document.body.removeChild(ta);
        }
      },
      args: [textToCopy]
    });
  } catch (e) {
    console.warn('[CleanShare] Ошибка executeScript для копирования:', e);
  }
}

chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  if (info.menuItemId === 'cleanshare_clean_link' && info.linkUrl) {
    const cleanResult = cleanUrl(info.linkUrl);
    if (tab && tab.id) {
      await copyToClipboardInTab(tab.id, cleanResult.cleanedUrl, {
        title: 'CleanShare URL',
        message: 'Ссылка очищена и скопирована!',
        trackersRemoved: cleanResult.removedCount,
        sampleUrl: cleanResult.cleanedUrl,
        showToast: cleanResult.isCleaned // Показываем тост только если ссылка была очищена
      });
    }
    if (cleanResult.isCleaned) {
      await recordStats(1, cleanResult.removedCount, cleanResult.cleanedUrl);
    }
  } else if (info.menuItemId === 'cleanshare_clean_page' && tab && tab.url) {
    const cleanResult = cleanUrl(tab.url);
    if (tab.id) {
      await copyToClipboardInTab(tab.id, cleanResult.cleanedUrl, {
        title: 'CleanShare URL',
        message: 'URL страницы очищен и скопирован!',
        trackersRemoved: cleanResult.removedCount,
        sampleUrl: cleanResult.cleanedUrl,
        showToast: cleanResult.isCleaned
      });
    }
    if (cleanResult.isCleaned) {
      await recordStats(1, cleanResult.removedCount, cleanResult.cleanedUrl);
    }
  } else if (info.menuItemId === 'cleanshare_clean_selection' && info.selectionText && tab && tab.id) {
    const textResult = cleanTextWithUrls(info.selectionText);
    if (tab.id) {
      await copyToClipboardInTab(tab.id, textResult.cleanedText, {
        title: 'CleanShare URL',
        message: `Очищено ссылок: ${textResult.totalCleanedUrls}!`,
        trackersRemoved: textResult.totalRemovedParams,
        sampleUrl: textResult.cleanedText.slice(0, 80),
        showToast: textResult.totalCleanedUrls > 0
      });
    }
    if (textResult.totalCleanedUrls > 0) {
      await recordStats(textResult.totalCleanedUrls, textResult.totalRemovedParams, textResult.cleanedText.slice(0, 100));
    }
  }
});

// ============================================================================
// 5. ОБРАБОТЧИК СООБЩЕНИЙ (RUNTIME MESSAGES)
// ============================================================================

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (!message || !message.action) {
    return false;
  }

  switch (message.action) {
    case 'CLEAN_URL': {
      const result = cleanUrl(message.url);
      if (result.isCleaned && message.recordStat !== false) {
        recordStats(1, result.removedCount, result.cleanedUrl);
      }
      sendResponse(result);
      break;
    }

    case 'CLEAN_TEXT': {
      const result = cleanTextWithUrls(message.text);
      if (result.totalCleanedUrls > 0 && message.recordStat !== false) {
        recordStats(result.totalCleanedUrls, result.totalRemovedParams, result.cleanedText.slice(0, 100));
      }
      sendResponse(result);
      break;
    }

    case 'RECORD_STATS': {
      recordStats(message.count || 1, message.trackers || 1, message.sample || '');
      sendResponse({ success: true });
      break;
    }

    case 'GET_STATS': {
      chrome.storage.local.get({
        totalCleanedLinks: 0,
        totalTrackersRemoved: 0,
        recentCleaned: []
      }).then(data => {
        sendResponse(data);
      });
      return true;
    }

    case 'RESET_STATS': {
      chrome.storage.local.set({
        totalCleanedLinks: 0,
        totalTrackersRemoved: 0,
        recentCleaned: []
      }).then(() => {
        sendResponse({ success: true });
      });
      return true;
    }

    default:
      sendResponse({ error: `Неизвестное действие: ${message.action}` });
      break;
  }

  return true;
});

// ============================================================================
// 6. ЖИЗНЕННЫЙ ЦИКЛ РАСШИРЕНИЯ (LIFECYCLE)
// ============================================================================

chrome.runtime.onInstalled.addListener((details) => {
  console.log(`[CleanShare URL] Расширение установлено/обновлено (причина: ${details.reason})`);
  initContextMenus();
});

chrome.runtime.onStartup.addListener(() => {
  initContextMenus();
});
