/**
 * CleanShare URL - Content Script
 * Модуль синхронного перехвата копирования и отображения Toast-уведомлений на странице.
 */

(function () {
  // Защита от повторной инициализации
  if (window.__cleanshare_injected) return;
  window.__cleanshare_injected = true;

  const TOAST_DURATION_MS = 2500;
  const TOAST_EXIT_ANIM_MS = 260;

  // ============================================================================
  // 1. ДВИЖОК ОЧИСТКИ URL ДЛЯ СИНХРОННОГО ПЕРЕХВАТА (SYNCHRONOUS ENGINE)
  // ============================================================================

  const GLOBAL_TRACKING_PARAMS = new Set([
    'utm_source', 'utm_medium', 'utm_campaign', 'utm_term', 'utm_content', 'utm_id', 'utm_name',
    'utm_reader', 'utm_referrer', 'utm_pubreferrer', 'utm_viz_id', 'utm_source_platform',
    'utm_creative_format', 'utm_marketing_tactic', 'ref', 'source', 'share', 'origin', 'ref_src', 'ref_url',
    'gclid', 'gclsrc', 'dclid', 'gbraid', 'wbraid', 'gad_source', '_ga', '_gl',
    'ga_source', 'ga_medium', 'ga_term', 'ga_content', 'ga_campaign', 'ga_place',
    'fbclid', 'fbadid', 'fb_action_ids', 'fb_action_types', 'fb_source', 'fb_ref',
    'igsh', 'igshid', 'yclid', 'ym_debug', '_openstat', 'from_source', 'from_global',
    'msclkid', 'ms_clkid', 'twclid', 'ttclid', 'li_fat_id', 'trk', 'trkEmail',
    'epik', 'pp', 'mc_cid', 'mc_eid', 'recipient_id', 'campaign_id',
    'ml_subscriber', 'ml_subscriber_hash', 'mkt_tok', '_hsenc', '_hsmi', 'hsCtaTracking',
    'algo_pvid', 'algo_expid', 'btsid', 'ws_ab_test', 'spm', 'scm',
    'aff_platform', 'aff_trace_key', 'aff_short_key', 'click_id', 'clickid', 'tracking_id', 'visitor_id'
  ]);

  const TRACKING_PREFIXES = ['utm_', 'ga_', 'fb_', 'sc_', 'pd_rd_', 'pf_rd_', 'algo_'];

  const DOMAIN_RULES = [
    {
      test: /(?:youtube\.com|youtu\.be|music\.youtube\.com)$/i,
      stripParams: [
        'list', 'index', 'ab_channel', 'start_radio', 'rv', 'si', 'feature', 'app', 'pp',
        'embeds_referring_euri', 'embeds_referring_origin', 'source_ve_path', 'theme', 'widget_referrer'
      ]
    },
    {
      test: /spotify\.com$/i,
      stripParams: ['si', 'context']
    },
    {
      test: /(?:twitter\.com|x\.com)$/i,
      stripParams: ['s', 't', 'ref_src', 'ref_url', 'cn']
    },
    {
      test: /(?:reddit\.com|redd\.it)$/i,
      stripParams: ['rdt_cid', 'share_id', 'ref', 'ref_source']
    },
    {
      test: /tiktok\.com$/i,
      stripParams: ['_r', '_t', 'is_from_webapp', 'sender_device', 'share_item_id']
    },
    {
      test: /(?:amazon\.[a-z.]+|amzn\.to)$/i,
      stripParams: ['tag', 'linkcode', 'camp', 'creative', 'creativeasin', 'ref_', '_encoding', 'keywords'],
      cleanPath: (pathname) => pathname.replace(/\/ref=[^/?#]+/i, '')
    },
    {
      test: /(?:aliexpress\.[a-z.]+|aliexpress\.ru)$/i,
      stripParams: ['spm', 'scm', '_t', 'sk', 'aff_trace_key']
    },
    {
      test: /(?:t\.me|telegram\.me)$/i,
      stripParams: ['startattach']
    }
  ];

  const URL_REGEX = /(?:https?:\/\/|www\.)[^\s"'<>()[\]{}]+/gi;

  function unwrapRedirect(urlObj) {
    const hostname = urlObj.hostname.toLowerCase();
    if (hostname.includes('google.') && urlObj.pathname === '/url') {
      const target = urlObj.searchParams.get('url') || urlObj.searchParams.get('q');
      if (target && target.startsWith('http')) return target;
    }
    if (hostname.includes('youtube.com') && urlObj.pathname === '/redirect') {
      const target = urlObj.searchParams.get('q');
      if (target && target.startsWith('http')) return target;
    }
    return null;
  }

  function isTrackingParam(key, hostname) {
    const lowerKey = key.toLowerCase();
    if (GLOBAL_TRACKING_PARAMS.has(lowerKey)) return true;
    for (const prefix of TRACKING_PREFIXES) {
      if (lowerKey.startsWith(prefix)) return true;
    }
    for (const rule of DOMAIN_RULES) {
      if (rule.test.test(hostname)) {
        if (rule.stripParams && rule.stripParams.includes(lowerKey)) return true;
      }
    }
    return false;
  }

  function cleanUrlSync(inputUrl) {
    if (!inputUrl || typeof inputUrl !== 'string') {
      return { originalUrl: inputUrl, cleanedUrl: inputUrl, isCleaned: false, removedParams: [], removedCount: 0 };
    }

    let raw = inputUrl.trim();
    const hadNoScheme = !/^https?:\/\//i.test(raw);
    if (hadNoScheme) raw = 'https://' + raw;

    let urlObj;
    try {
      urlObj = new URL(raw);
    } catch (e) {
      return { originalUrl: inputUrl, cleanedUrl: inputUrl, isCleaned: false, removedParams: [], removedCount: 0 };
    }

    const unwrapped = unwrapRedirect(urlObj);
    if (unwrapped) {
      const nestedResult = cleanUrlSync(unwrapped);
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
    const paramsToKeep = [];

    for (const [key, value] of urlObj.searchParams.entries()) {
      if (isTrackingParam(key, hostname)) {
        removedParams.push(key);
      } else {
        paramsToKeep.push([key, value]);
      }
    }

    urlObj.search = '';
    for (const [key, value] of paramsToKeep) {
      urlObj.searchParams.append(key, value);
    }

    for (const rule of DOMAIN_RULES) {
      if (rule.test.test(hostname) && typeof rule.cleanPath === 'function') {
        const oldPath = urlObj.pathname;
        urlObj.pathname = rule.cleanPath(urlObj.pathname);
        if (oldPath !== urlObj.pathname) removedParams.push('path_ref');
      }
    }

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
        urlObj.hash = `${hashPath}?${new URLSearchParams(hashParamsToKeep).toString()}`;
      } else {
        urlObj.hash = hashPath;
      }
    }

    let finalUrl = urlObj.toString().replace(/\?$/, '');
    const isCleaned = removedParams.length > 0 || finalUrl !== inputUrl;

    return {
      originalUrl: inputUrl,
      cleanedUrl: finalUrl,
      isCleaned: isCleaned,
      removedParams: removedParams,
      removedCount: removedParams.length
    };
  }

  function cleanTextSync(text) {
    if (!text || typeof text !== 'string') {
      return { originalText: text, cleanedText: text, totalCleanedUrls: 0, totalRemovedParams: 0 };
    }
    let totalCleanedUrls = 0;
    let totalRemovedParams = 0;

    const cleanedText = text.replace(URL_REGEX, (matchedUrl) => {
      const res = cleanUrlSync(matchedUrl);
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
  // 2. СИСТЕМА ОТОБРАЖЕНИЯ TOAST-УВЕДОМЛЕНИЙ
  // ============================================================================

  function getToastContainer() {
    let container = document.getElementById('cleanshare-toast-container');
    if (!container) {
      container = document.createElement('div');
      container.id = 'cleanshare-toast-container';
      document.documentElement.appendChild(container);
    }
    return container;
  }

  function showCleanShareToast(options = {}) {
    const {
      title = 'CleanShare URL',
      message = 'Ссылка очищена и скопирована!',
      trackersRemoved = 0,
      sampleUrl = ''
    } = options;

    const container = getToastContainer();
    const toast = document.createElement('div');
    toast.className = 'cleanshare-toast';

    const iconHtml = `
      <div class="cleanshare-toast-icon-wrapper">
        <svg class="cleanshare-toast-icon-svg" viewBox="0 0 24 24">
          <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>
          <path d="m9 12 2 2 4-4"/>
        </svg>
      </div>
    `;

    let badgeHtml = '';
    if (trackersRemoved > 0) {
      badgeHtml = `<span class="cleanshare-toast-badge">✨ Удалено меток: ${trackersRemoved}</span>`;
    }

    let urlPreviewHtml = '';
    if (sampleUrl) {
      const cleanDisplay = sampleUrl.length > 44 ? sampleUrl.slice(0, 42) + '...' : sampleUrl;
      urlPreviewHtml = `<div class="cleanshare-toast-url-preview" title="${escapeHtml(sampleUrl)}">${escapeHtml(cleanDisplay)}</div>`;
    }

    toast.innerHTML = `
      ${iconHtml}
      <div class="cleanshare-toast-body">
        <div class="cleanshare-toast-header">
          <span class="cleanshare-toast-title">${escapeHtml(title)}</span>
          <button class="cleanshare-toast-close" title="Закрыть">&times;</button>
        </div>
        <p class="cleanshare-toast-desc">${escapeHtml(message)}</p>
        ${badgeHtml}
        ${urlPreviewHtml}
      </div>
      <div class="cleanshare-toast-progress"></div>
    `;

    container.appendChild(toast);

    let isClosed = false;
    const closeToast = () => {
      if (isClosed) return;
      isClosed = true;
      toast.classList.add('cleanshare-toast-closing');
      setTimeout(() => {
        if (toast.parentNode) {
          toast.parentNode.removeChild(toast);
        }
      }, TOAST_EXIT_ANIM_MS);
    };

    const closeBtn = toast.querySelector('.cleanshare-toast-close');
    if (closeBtn) {
      closeBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        closeToast();
      });
    }

    setTimeout(closeToast, TOAST_DURATION_MS);
  }

  function escapeHtml(str) {
    if (!str) return '';
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  // ============================================================================
  // 3. СИНХРОННЫЙ ПЕРЕХВАТ СОБЫТИЯ КОПИРОВАНИЯ (INSTANT CLIPBOARD INTERCEPTION)
  // ============================================================================

  document.addEventListener('copy', (event) => {
    let selectedText = '';
    const activeEl = document.activeElement;
    
    if (activeEl && (activeEl.tagName === 'INPUT' || activeEl.tagName === 'TEXTAREA')) {
      const start = activeEl.selectionStart || 0;
      const end = activeEl.selectionEnd || 0;
      selectedText = activeEl.value.substring(start, end);
    }
    
    if (!selectedText && window.getSelection) {
      selectedText = window.getSelection().toString();
    }

    if (!selectedText || !selectedText.trim()) return;

    const rawText = selectedText.trim();
    const hasUrl = /(?:https?:\/\/|www\.)[^\s"'<>]+/i.test(rawText);
    if (!hasUrl) return;

    // Синхронная очистка ссылки
    const cleanResult = cleanTextSync(rawText);

    // Если трекеры были найдены и удалены
    if (cleanResult.totalCleanedUrls > 0 && cleanResult.totalRemovedParams > 0) {
      if (event.clipboardData) {
        event.clipboardData.setData('text/plain', cleanResult.cleanedText);
        event.preventDefault(); // Предотвращаем запись исходного грязного текста
      }

      // Показываем красивый Toast
      showCleanShareToast({
        title: 'CleanShare URL',
        message: 'Ссылка очищена при копировании!',
        trackersRemoved: cleanResult.totalRemovedParams,
        sampleUrl: cleanResult.cleanedText
      });

      // Асинхронно обновляем статистику в background worker
      try {
        chrome.runtime.sendMessage({
          action: 'RECORD_STATS',
          count: cleanResult.totalCleanedUrls,
          trackers: cleanResult.totalRemovedParams,
          sample: cleanResult.cleanedText.slice(0, 100)
        });
      } catch (e) {}
    }
  }, true);

  // ============================================================================
  // 4. СЛУШАТЕЛЬ СООБЩЕНИЙ ОТ BACKGROUND SERVICE WORKER
  // ============================================================================

  chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (!message) return;

    if (message.action === 'SHOW_TOAST') {
      // Показываем тост только если были удалены трекеры
      if (message.trackersRemoved > 0 || message.forceShow) {
        showCleanShareToast({
          title: message.title || 'CleanShare URL',
          message: message.message || 'Ссылка очищена и скопирована!',
          trackersRemoved: message.trackersRemoved || 0,
          sampleUrl: message.sampleUrl || ''
        });
      }
      sendResponse({ received: true });
    }
  });

})();
