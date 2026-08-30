// ── Localization (i18n) runtime ─────────────────────────────────────
// Loaded after i18n.strings.js and before newtab.js / popup.js. Exposes a
// global `I18N` object.
//
// This file is the RUNTIME only -- it has no string tables of its own.
// window.MZ_STRINGS / MZ_LOCALE_LABELS / MZ_PRICING come from i18n.strings.js,
// which is generated from locales/{lang}/{common,extension}.json by
// i18n-tools/sync-extension-locales.js. To add a language, add a code to
// locales/_locales.json, translate the JSON files, regenerate, and this file
// needs zero changes -- see i18n-tools/ADDING_A_LANGUAGE.md.
//
// UI language is SEPARATE from S.locale (which controls date/time/temperature
// formatting derived from timezone). Language is chosen by browser language,
// with an optional manual override persisted in localStorage under `mz-lang`
// (values: 'auto' | <locale code>). localStorage is shared across the
// extension origin, so the popup and the new-tab page stay in sync.
(function () {
  'use strict';

  const STR = window.MZ_STRINGS || {};
  const LOCALE_LABELS = window.MZ_LOCALE_LABELS || {};
  const PRICING = window.MZ_PRICING || {};
  const SUPPORTED = Object.keys(STR);
  const LANG_KEY = 'mz-lang';

  function detectLang() {
    const nav = (navigator.languages && navigator.languages[0]) || navigator.language || 'en';
    const low = String(nav).toLowerCase();
    return SUPPORTED.find(code => low.startsWith(code)) || (SUPPORTED.includes('en') ? 'en' : SUPPORTED[0]);
  }

  function resolveLang() {
    let override;
    try { override = localStorage.getItem(LANG_KEY); } catch (e) {}
    if (SUPPORTED.includes(override)) return override;
    return detectLang();
  }

  const lang = resolveLang();

  // ── Checkout entry point ──
  // The extension always opens ONE URL we control; that page must do an instant
  // server-side 302 to the real provider (lava.top / Stripe / Pix …) based on the
  // `plan` + `lang` params (and, better, the visitor's region/IP). Keeping the
  // provider OFF the extension lets us swap it, run promos, or fix a broken link
  // WITHOUT shipping a new version through Web Store review.
  const PAY_URL = 'https://markmez.com/pay';

  function interpolate(str, params) {
    if (!params) return str;
    return str.replace(/\{(\w+)\}/g, (m, k) => (params[k] != null ? params[k] : m));
  }

  // A translation value is "plural-shaped" if it's a plain object whose keys
  // are all CLDR plural categories (as opposed to a string, an array of
  // strings like month names, or some other structured value).
  const PLURAL_CATEGORIES = ['zero', 'one', 'two', 'few', 'many', 'other'];
  function isPluralShape(v) {
    return !!v && typeof v === 'object' && !Array.isArray(v) &&
      Object.keys(v).length > 0 && Object.keys(v).every(k => PLURAL_CATEGORIES.includes(k));
  }

  const pluralRulesCache = {};
  function pluralCategory(locale, n) {
    if (!pluralRulesCache[locale]) {
      try { pluralRulesCache[locale] = new Intl.PluralRules(locale); }
      catch (e) { pluralRulesCache[locale] = new Intl.PluralRules('en'); }
    }
    return pluralRulesCache[locale].select(n);
  }

  // Looks up `key`, falling back to English if the current language is
  // missing it (should not normally happen -- i18n-tools/check-keys.js
  // enforces key parity across locales -- but keeps the UI from ever
  // showing a raw key name if a translation slips through incomplete).
  function t(key, params) {
    let v = STR[lang] ? STR[lang][key] : undefined;
    let vLang = lang;
    if (v === undefined) { v = STR.en ? STR.en[key] : undefined; vLang = 'en'; }
    if (v === undefined) return key;
    if (Array.isArray(v)) return v;
    if (isPluralShape(v)) {
      const n = params && typeof params.n === 'number' ? params.n : 0;
      const cat = pluralCategory(vLang, n);
      const str = v[cat] !== undefined ? v[cat] : v.other;
      return interpolate(str, params);
    }
    if (typeof v === 'string') return interpolate(v, params);
    return v;
  }

  // Apply translations to static markup.
  //  data-i18n           → textContent
  //  data-i18n-html      → innerHTML (for strings containing <br>)
  //  data-i18n-ph        → placeholder attribute
  //  data-i18n-title     → title attribute
  //  data-i18n-aria      → aria-label attribute
  function applyStatic(root) {
    root = root || document;
    root.querySelectorAll('[data-i18n]').forEach(el => { el.textContent = t(el.getAttribute('data-i18n')); });
    root.querySelectorAll('[data-i18n-html]').forEach(el => { el.innerHTML = t(el.getAttribute('data-i18n-html')); });
    root.querySelectorAll('[data-i18n-ph]').forEach(el => { el.setAttribute('placeholder', t(el.getAttribute('data-i18n-ph'))); });
    root.querySelectorAll('[data-i18n-title]').forEach(el => { el.setAttribute('title', t(el.getAttribute('data-i18n-title'))); });
    root.querySelectorAll('[data-i18n-aria]').forEach(el => { el.setAttribute('aria-label', t(el.getAttribute('data-i18n-aria'))); });
  }

  // Persist a language override and reload so everything re-renders.
  //  'auto' | <locale code>
  function setLang(val) {
    try {
      if (val === 'auto') localStorage.removeItem(LANG_KEY);
      else localStorage.setItem(LANG_KEY, val);
    } catch (e) {}
    location.reload();
  }

  function currentPref() {
    try {
      const o = localStorage.getItem(LANG_KEY);
      return SUPPORTED.includes(o) ? o : 'auto';
    } catch (e) { return 'auto'; }
  }

  // Endonym label for a locale code (e.g. 'de' → 'Deutsch'). These are
  // literal strings, never translated -- a German speaker sees "English"
  // in the language picker, not "Englisch".
  function localeLabel(code) { return LOCALE_LABELS[code] || code; }

  try { document.documentElement.lang = lang; } catch (e) {}

  window.I18N = { lang, t, applyStatic, setLang, currentPref, localeLabel, SUPPORTED };
})();
