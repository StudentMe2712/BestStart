/**
 * QuickTranslate Background Service Worker (Manifest V3)
 * Proxies translation requests to bypass CORS restrictions on content scripts.
 */

chrome.runtime.onInstalled.addListener(() => {
  console.log('[QuickTranslate] Background Service Worker initialized.');
});

/**
 * Handles incoming messages from content scripts
 */
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.action === 'translate') {
    handleTranslation(request.text, request.targetLang || 'ru')
      .then((result) => {
        sendResponse({ success: true, ...result });
      })
      .catch((error) => {
        console.error('[QuickTranslate] Translation error:', error);
        sendResponse({
          success: false,
          error: error.message || 'Ошибка получения перевода'
        });
      });

    // Return true to indicate that response is sent asynchronously
    return true;
  }
});

/**
 * Translates text with a multi-tier fallback pipeline for maximum reliability
 */
async function handleTranslation(text, targetLang = 'ru') {
  const trimmed = (text || '').trim();
  if (!trimmed) {
    throw new Error('Пустой текст для перевода');
  }

  // Tier 1: Google Translate API (gtx client)
  try {
    const gtxUrl = `https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=${encodeURIComponent(targetLang)}&dt=t&q=${encodeURIComponent(trimmed)}`;
    const response = await fetch(gtxUrl, {
      method: 'GET',
      headers: { 'Accept': 'application/json' }
    });

    if (response.ok) {
      const data = await response.json();
      if (Array.isArray(data) && Array.isArray(data[0])) {
        const translatedSegments = data[0]
          .map((item) => (item && item[0] ? item[0] : ''))
          .filter(Boolean);
        
        if (translatedSegments.length > 0) {
          const detectedLang = data[2] || (data[8] && data[8][0] && data[8][0][0]) || 'auto';
          return {
            translation: translatedSegments.join(''),
            detectedLang: detectedLang,
            originalText: trimmed,
            provider: 'google-gtx'
          };
        }
      }
    }
  } catch (err) {
    console.warn('[QuickTranslate] Tier 1 (gtx) failed:', err.message);
  }

  // Tier 2: Google Translate API (Chrome Extension dict endpoint)
  try {
    const dictUrl = `https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=auto&tl=${encodeURIComponent(targetLang)}&q=${encodeURIComponent(trimmed)}`;
    const response = await fetch(dictUrl, {
      method: 'GET',
      headers: { 'Accept': 'application/json' }
    });

    if (response.ok) {
      const data = await response.json();
      if (Array.isArray(data)) {
        if (typeof data[0] === 'string') {
          return {
            translation: data[0],
            detectedLang: data[1] || 'auto',
            originalText: trimmed,
            provider: 'google-dict'
          };
        }
        if (Array.isArray(data[0])) {
          const translation = data.map((item) => (Array.isArray(item) ? item[0] : item)).join(' ');
          const detectedLang = (data[0] && data[0][1]) || 'auto';
          return {
            translation: translation,
            detectedLang: detectedLang,
            originalText: trimmed,
            provider: 'google-dict'
          };
        }
      }
    }
  } catch (err) {
    console.warn('[QuickTranslate] Tier 2 (dict) failed:', err.message);
  }

  // Tier 3: MyMemory Public Translation API Fallback
  try {
    const myMemoryUrl = `https://api.mymemory.translated.net/get?q=${encodeURIComponent(trimmed)}&langpair=auto|${encodeURIComponent(targetLang)}`;
    const response = await fetch(myMemoryUrl);

    if (response.ok) {
      const data = await response.json();
      if (data && data.responseData && data.responseData.translatedText) {
        return {
          translation: data.responseData.translatedText,
          detectedLang: 'auto',
          originalText: trimmed,
          provider: 'mymemory'
        };
      }
    }
  } catch (err) {
    console.warn('[QuickTranslate] Tier 3 (mymemory) failed:', err.message);
  }

  throw new Error('Не удалось получить ответ от сервиса перевода. Проверьте интернет-соединение.');
}
