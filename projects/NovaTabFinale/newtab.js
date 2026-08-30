
// Localization shortcut. i18n.js loads before this file.
const T = (k, p) => (window.I18N ? I18N.t(k, p) : k);
const track = () => {};
const trackOnce = () => {};
const trackDaily = () => {};

// Apply translations to static markup.
(function () {
  try { I18N.applyStatic(document); } catch (e) {}
})();

// ── State ──
let S = {};

const DEFAULTS = {
  pages: [{ id: 'p1', name: T('page.home'), order: 0 }],
  boards: [],
  trash: { boards: [], bookmarks: [] },
  themeStyle: { boardColorHex: '#ffffff', boardOpacity: 55, boardBlur: 12, accentHex: '#e07a4a', isDark: false, textScale: 1, textBold: false },
  bookmarks: [],
  activePage: 'p1',
  navSearchEnabled: false
};

function genId() { return '_' + Math.random().toString(36).slice(2, 10); }

function detectLocale() {
  const lang = navigator.language || 'en';
  const tz = Intl.DateTimeFormat().resolvedOptions().timeZone || '';

  // Time format + date order: language is the right signal (display preference)
  let timeFormat = '24h';
  try {
    if (new Intl.DateTimeFormat(lang, { hour: 'numeric' }).resolvedOptions().hour12) timeFormat = '12h';
  } catch(e) {}

  let dateFormat = 'DMY';
  try {
    const parts = new Intl.DateTimeFormat(lang).formatToParts(new Date(2024, 0, 31));
    const order = parts.filter(p => ['day','month','year'].includes(p.type)).map(p => p.type[0]);
    if (order[0] === 'm') dateFormat = 'MDY';
    else if (order[0] === 'y') dateFormat = 'YMD';
  } catch(e) {}

  // Week start: timezone only — Intl.Locale uses browser language, not actual location
  // Europe + Africa → Monday; everywhere else → Sunday
  const weekStart = /^(Europe|Africa)/.test(tz) ? 1 : 0;

  // Temperature: whitelist of US IANA timezones → imperial; everything else → metric
  const US_TZ = new Set([
    'America/New_York','America/Detroit','America/Kentucky/Louisville','America/Kentucky/Monticello',
    'America/Indiana/Indianapolis','America/Indiana/Vincennes','America/Indiana/Winamac',
    'America/Indiana/Marengo','America/Indiana/Petersburg','America/Indiana/Vevay',
    'America/Chicago','America/Indiana/Tell_City','America/Indiana/Knox','America/Menominee',
    'America/North_Dakota/Center','America/North_Dakota/New_Salem','America/North_Dakota/Beulah',
    'America/Denver','America/Boise','America/Los_Angeles','America/Juneau','America/Sitka',
    'America/Metlakatla','America/Yakutat','America/Anchorage','America/Nome','America/Adak',
    'America/Phoenix','Pacific/Honolulu','America/Puerto_Rico','Pacific/Guam','Pacific/Saipan',
    'Pacific/Pago_Pago','America/St_Thomas',
  ]);
  const tempUnit = US_TZ.has(tz) ? 'imperial' : 'metric';

  return { timeFormat, dateFormat, weekStart, tempUnit, _v: 3 };
}

function getLayoutParams() {
  const GAP = 14;
  const MIN_W = 190;                 // narrowest a board may shrink to
  // The floating sidebar (settings / menu) is fixed ~64px in from the right edge.
  // The grid is centered, so we reserve a symmetric band on BOTH sides — the grid
  // then can never slide under those buttons, on any screen width.
  const SIDE_RESERVE = 76;           // 64px sidebar zone + ~12px breathing gap
  const usable = Math.max(MIN_W, window.innerWidth - 2 * SIDE_RESERVE);
  const requestedW = S.boardWidth || 260;

  // Absolute column ceiling: how many boards fit even at the minimum width.
  const maxCols = Math.max(1, Math.floor((usable + GAP) / (MIN_W + GAP)));
  const manual = S.maxBoardCols;
  const numCols = (manual && manual > 0)
    ? Math.min(manual, maxCols)      // count is user-fixed; only the screen ceiling caps it
    : Math.min(maxCols, Math.max(1, Math.floor((usable + GAP) / (requestedW + GAP))));

  // Clamp width so numCols boards always fit the usable band (never overlap the
  // sidebar). This is what makes "4 columns" allow a wider board than "5 columns":
  // fewer columns → more room each → the cap rises automatically.
  const fitW = Math.floor((usable - (numCols - 1) * GAP) / numCols);
  const BOARD_W = Math.max(MIN_W, Math.min(requestedW, fitW));

  return { BOARD_W, GAP, numCols, autoCols: maxCols, fitW };
}

// Проставляет отсутствующие поля/дефолты в S и мигрирует доски. Вызывается при
// загрузке И после входа (когда S заменяется снапшотом/облаком, где части полей
// может не быть — иначе, напр., S.locale окажется undefined и настройки упадут).
function _normalizeState() {
  if (!Array.isArray(S.pages) || !S.pages.length) S.pages = JSON.parse(JSON.stringify(DEFAULTS.pages));
  if (!Array.isArray(S.boards)) S.boards = [];
  if (!Array.isArray(S.bookmarks)) S.bookmarks = [];
  if (!S.activePage || !S.pages.find(p => p.id === S.activePage)) {
    S.activePage = S.pages[0]?.id || null;
  }
  if (!S.trash) S.trash = { boards: [], bookmarks: [] };
  if (!S.focusStats) S.focusStats = [];
  if (!S.pomTimers) S.pomTimers = {};
  if (!S.weather) S.weather = { enabled: false, city: '', units: 'metric', lat: null, lon: null, cache: {} };
  if (!S.themeStyle) S.themeStyle = { boardColorHex:'#ffffff', boardOpacity:55, boardBlur:12, accentHex:'#e07a4a', isDark:false, textScale:1, textBold:false };
  if (!S.user) S.user = { name: '', email: '', avatar: '', signedIn: false };
  if (S.openInNewTab === undefined) S.openInNewTab = true;
  if (S.incognito === undefined) S.incognito = false;
  if (S.clockEnabled === undefined) S.clockEnabled = false;
  if (S.navSearchEnabled === undefined) S.navSearchEnabled = false;
  if (S.hideExtraBookmarks === undefined) S.hideExtraBookmarks = false;
  if (!S.maxBookmarksShown) S.maxBookmarksShown = 5;
  if (S.showDescriptions === undefined) S.showDescriptions = true;
  if (S.sidebarAlwaysExpanded === undefined) S.sidebarAlwaysExpanded = false;
  if (!S.quickSaveBoard) S.quickSaveBoard = '';
  if (!S.locale || S.locale._v !== 3) S.locale = detectLocale();
  // Migrate boards to col/row model
  S.boards.forEach((b, i) => {
    // Per-board customization was removed — drop any leftover style data.
    if (b.boardStyle) delete b.boardStyle;
    if (b.col == null) {
      if (b.x != null) {
        b.col = Math.round(b.x / 274);
        b.row = Math.round(b.y / 220);
      } else {
        const idx = b.order != null ? b.order : i;
        b.col = idx % 4;
        b.row = Math.floor(idx / 4);
      }
      delete b.x; delete b.y; delete b.order;
    }
  });
}

// A delete is applied locally (and rendered) the instant the user clicks it, but
// the write that persists it is debounced (see saveState below) and other open
// newtab tabs each hold their own in-memory copy of the same data. If another
// tab — mid-edit, or just slow to reconcile — flushes its (still pre-delete)
// copy in between, that full-snapshot write silently resurrects whatever this
// tab just removed. Tombstoning the id for a short window closes that race:
// any snapshot loaded while the tombstone is live has the id stripped back out,
// and if that actually changed anything we re-save to heal storage for everyone.
// Two separate scopes: an id removed from the main lists (deleted, or moved to
// trash) must stay out of boards/bookmarks/pages, but must NOT be stripped back
// out of trash — it may legitimately live there. An id removed from trash
// (emptied/permanently deleted) is the reverse. Restoring an id clears both.
const _tombstoneMain  = new Map(); // id -> deletion timestamp
const _tombstoneTrash = new Map();
const TOMBSTONE_TTL = 15000;
function tombstoneMain(ids)  { const now = Date.now(); ids.forEach(id => { if (id) _tombstoneMain.set(id, now); }); }
function tombstoneTrash(ids) { const now = Date.now(); ids.forEach(id => { if (id) _tombstoneTrash.set(id, now); }); }
function unTombstone(id) { _tombstoneMain.delete(id); _tombstoneTrash.delete(id); }
function _sweepExpired(map) {
  const now = Date.now();
  for (const [id, ts] of map) { if (now - ts > TOMBSTONE_TTL) map.delete(id); }
}
function _pruneTombstoned(state) {
  _sweepExpired(_tombstoneMain);
  _sweepExpired(_tombstoneTrash);
  if (!_tombstoneMain.size && !_tombstoneTrash.size) return false;
  let changed = false;
  const strip = (arr, map) => {
    if (!Array.isArray(arr) || !map.size) return arr;
    const kept = arr.filter(x => !map.has(x.id));
    if (kept.length !== arr.length) changed = true;
    return kept;
  };
  if (state.boards)    state.boards    = strip(state.boards, _tombstoneMain);
  if (state.bookmarks) state.bookmarks = strip(state.bookmarks, _tombstoneMain);
  if (state.pages) {
    const prunedPages = strip(state.pages, _tombstoneMain);
    if (prunedPages.length) state.pages = prunedPages; // never leave zero pages
  }
  if (state.trash) {
    if (state.trash.boards)    state.trash.boards    = strip(state.trash.boards, _tombstoneTrash);
    if (state.trash.bookmarks) state.trash.bookmarks = strip(state.trash.bookmarks, _tombstoneTrash);
  }
  return changed;
}

function loadState() {
  return new Promise(resolve => {
    chrome.storage.local.get('appState', res => {
      S = res.appState ? res.appState : JSON.parse(JSON.stringify(DEFAULTS));
      if (_pruneTombstoned(S)) saveState(); // heal storage: persist the correction
      _normalizeState();
      applyThemeStyle(S.themeStyle);
      resolve();
    });
  });
}

// Unique per open tab, stamped onto every write we make so the storage.onChanged
// listener can tell our own writes apart from external ones (e.g. the Quick Save
// popup) without a racy boolean flag.
const _tabId = 'tab_' + Math.random().toString(36).slice(2);

let _saveTimer = null;
function saveState() {
  clearTimeout(_saveTimer);
  _saveTimer = setTimeout(() => {
    S._writer = _tabId;
    chrome.storage.local.set({ appState: S });
  }, 300);
}

// ── Deferred reconcile ──
// Cross-tab live updates re-render the whole page. If that fires
// while the user is mid-interaction — typing a new board name or search query,
// editing a popup field, dragging, or with a popup/menu open — the rebuild would
// destroy the focused input (losing focus + typed text) or detach a popup's
// anchor (sending it to the top-left corner). So while the user is busy we hold
// only the NEWEST reconcile and run it once they go idle.
let _deferredReconcile = null;

function isUserBusy() {
  const ae = document.activeElement;
  if (ae && (ae.tagName === 'INPUT' || ae.tagName === 'TEXTAREA' || ae.tagName === 'SELECT' || ae.isContentEditable)) return true;
  if (_dragId) return true;
  if (document.querySelector('.bk-popup, .board-menu, .focus-stats-popup, .nsb-eng-popup, .pom-settings-popup')) return true;
  return false;
}

function reconcileOrDefer(fn) {
  if (isUserBusy()) { _deferredReconcile = fn; return; }
  fn();
}

function flushDeferredReconcile() {
  if (_deferredReconcile && !isUserBusy()) {
    const fn = _deferredReconcile;
    _deferredReconcile = null;
    fn();
  }
}
// Retry the flush right after the user finishes an interaction.
document.addEventListener('focusout', () => setTimeout(flushDeferredReconcile, 200));
document.addEventListener('dragend',  () => setTimeout(flushDeferredReconcile, 200));
document.addEventListener('click',    () => setTimeout(flushDeferredReconcile, 200));

// Reflect external writes to local appState (e.g. Quick Save from the toolbar
// popup, or another open tab) so the page updates live instead of needing a
// manual refresh — and so our next saveState() doesn't clobber the addition.
chrome.storage.onChanged.addListener((changes, area) => {
  if (area !== 'local' || !changes.appState) return;
  const nv = changes.appState.newValue;
  if (!nv || nv._writer === _tabId) return; // our own write
  reconcileOrDefer(() => {
    loadState().then(() => {
      renderAll();
    });
  });
});

// ── Favicon ──
// Each service should show its real icon, so we build a chain of sources and
// walk it via onerror. The old single fallback was google.com/s2/favicons,
// which collapses every Google product (Gmail, Calendar, …) to a generic "G".
const MULTI_PART_TLDS = [
  'co.uk','com.au','co.jp','co.nz','co.za','com.br','com.mx',
  'co.in','org.uk','gov.uk','ac.uk','com.tr','com.ar','com.sg'
];

function getRootDomain(host) {
  const t = host.split('.');
  if (t.length <= 2) return host;
  const last2 = t.slice(-2).join('.');
  if (MULTI_PART_TLDS.includes(last2)) return t.length <= 3 ? host : t.slice(-3).join('.');
  return last2;
}

// Google products can't be resolved from their bare domain — gmail.com,
// mail.google.com, etc. all report a generic "G" to every favicon service.
// So we hardcode the real product icons (same approach as competitors). Keys
// are hostname, or "hostname/firstPathSegment" for products that share a host
// (docs.google.com/spreadsheets, …). Checked before any network source.
const KNOWN_FAVICONS = {
  'gmail.com':                    'https://ssl.gstatic.com/images/branding/product/2x/gmail_2020q4_48dp.png',
  'mail.google.com':             'https://ssl.gstatic.com/images/branding/product/2x/gmail_2020q4_48dp.png',
  'calendar.google.com':         'https://ssl.gstatic.com/images/branding/product/2x/calendar_2020q4_48dp.png',
  'drive.google.com':            'https://ssl.gstatic.com/images/branding/product/2x/drive_2020q4_48dp.png',
  'meet.google.com':             'https://ssl.gstatic.com/images/branding/product/2x/meet_2020q4_48dp.png',
  'chat.google.com':             'https://ssl.gstatic.com/images/branding/product/2x/chat_2020q4_48dp.png',
  'keep.google.com':             'https://ssl.gstatic.com/images/branding/product/2x/keep_2020q4_48dp.png',
  'photos.google.com':           'https://ssl.gstatic.com/images/branding/product/2x/photos_48dp.png',
  'contacts.google.com':         'https://www.gstatic.com/images/branding/product/1x/contacts_2022_48dp.png',
  'translate.google.com':        'https://ssl.gstatic.com/images/branding/product/2x/translate_48dp.png',
  'maps.google.com':             'https://www.gstatic.com/images/branding/product/2x/maps_48dp.png',
  'google.com/maps':             'https://www.gstatic.com/images/branding/product/2x/maps_48dp.png',
  'www.google.com/maps':         'https://www.gstatic.com/images/branding/product/2x/maps_48dp.png',
  'docs.google.com':             'https://ssl.gstatic.com/docs/documents/images/kix-favicon7.ico',
  'docs.google.com/document':    'https://ssl.gstatic.com/docs/documents/images/kix-favicon7.ico',
  'docs.google.com/spreadsheets':'https://ssl.gstatic.com/docs/spreadsheets/favicon3.ico',
  'docs.google.com/presentation':'https://ssl.gstatic.com/docs/presentations/images/favicon5.ico',
  'docs.google.com/forms':       'https://ssl.gstatic.com/docs/forms/device_home/android_192.png',
  'google.com':                  'https://ssl.gstatic.com/images/branding/product/1x/googleg_48dp.png',
  'www.google.com':              'https://ssl.gstatic.com/images/branding/product/1x/googleg_48dp.png',
};

function knownFavicon(u) {
  const host = u.hostname.toLowerCase();
  const seg1 = u.pathname.split('/').filter(Boolean)[0];
  if (seg1 && KNOWN_FAVICONS[host + '/' + seg1]) return KNOWN_FAVICONS[host + '/' + seg1];
  return KNOWN_FAVICONS[host] || null;
}

// Chrome's own favicon cache (needs "favicon" permission). Only knows icons for
// sites the user has actually visited, so it's the LAST resort — useful when a
// site serves no public favicon.ico but the browser cached one. It always
// yields an image (a default globe when nothing is cached) and never fires
// onerror, so it must come last in the chain.
function chromeFaviconUrl(pageUrl, size = 32) {
  try {
    const u = new URL(chrome.runtime.getURL('/_favicon/'));
    u.searchParams.set('pageUrl', pageUrl);
    u.searchParams.set('size', String(size));
    return u.toString();
  } catch { return ''; }
}

// Google's faviconV2 resolves the FULL page URL the way Chrome does, so every
// Google product on its own subdomain (gemini/mail/drive/calendar/photos/…)
// returns its real icon — not the generic google.com "G" that the old
// domain-based s2 endpoint gave. (Path-only products like www.google.com/maps
// still need the hardcoded map, since this keys off the origin.)
function faviconV2Url(pageUrl, size = 64) {
  return 'https://t1.gstatic.com/faviconV2?client=SOCIAL&type=FAVICON&fallback_opts=TYPE,SIZE,URL&size='
    + size + '&url=' + encodeURIComponent(pageUrl);
}

function faviconCandidates(url) {
  const list = [];
  try {
    const u = new URL(url);
    const host = u.hostname;
    const root = getRootDomain(host);
    // 1. Hardcoded brand icons (Google products) — works without ever visiting.
    const known = knownFavicon(u);
    if (known) list.push(known);
    // 2. The site's own favicon — best when present, no visit needed.
    list.push(u.origin + '/favicon.ico');
    // 3. faviconV2 by full URL: resolves Google subdomain products correctly.
    //    Must come before the root-domain / ddg-root fallbacks below, which for
    //    a subdomain (gemini.google.com) would return the parent google.com "G".
    list.push(faviconV2Url(url));
    list.push('https://icons.duckduckgo.com/ip3/' + host + '.ico');
    if (root && root !== host) list.push('https://' + root + '/favicon.ico');
    if (root && root !== host) list.push('https://icons.duckduckgo.com/ip3/' + root + '.ico');
    // 4. Chrome's cache as a final guaranteed image (real icon if visited).
    const chromeUrl = chromeFaviconUrl(url);
    if (chromeUrl) list.push(chromeUrl);
  } catch {}
  return list.filter(Boolean);
}

// Favicons are cached as data URLs in chrome.storage.local. Boards re-render on
// every drag/page switch (recreating these <img>s) and the whole page reloads
// each time a new tab opens — without a cache, setFavicon re-walks the network
// chain every time and the first candidate's 404 → swap flickers. Storing the
// actual bytes means a cached icon paints instantly, offline, with no network.
const FAVICON_STORE_KEY = 'faviconCache';
const FAVICON_CACHE_VERSION = 4;               // bump to discard & rebuild the cache
const FAVICON_TTL = 1000 * 60 * 60 * 24 * 30; // refresh icons older than 30 days
const FAVICON_PX = 48;                         // re-encode every icon to this size
const _faviconCache = new Map();   // cacheKey -> data: URL
const _faviconTime = new Map();    // cacheKey -> timestamp (ms)
const _faviconResolving = new Set();
let _faviconDirty = false;
let _faviconSaveTimer = null;

// Key by hostname so all bookmarks of a site share one icon, but keep path
// granularity where we have per-path icons (docs.google.com/spreadsheets, …).
function faviconCacheKey(url) {
  try {
    const u = new URL(url);
    const host = u.hostname.toLowerCase();
    const seg1 = u.pathname.split('/').filter(Boolean)[0];
    if (seg1 && KNOWN_FAVICONS[host + '/' + seg1]) return host + '/' + seg1;
    return host;
  } catch { return url; }
}

function loadFaviconCache() {
  return new Promise(resolve => {
    try {
      chrome.storage.local.get(FAVICON_STORE_KEY, res => {
        const obj = res && res[FAVICON_STORE_KEY];
        // Discard the whole cache on a version bump so entries poisoned by older
        // logic get rebuilt cleanly instead of breaking a site forever.
        if (obj && typeof obj === 'object' && obj.__v === FAVICON_CACHE_VERSION) {
          for (const k in obj) {
            if (k === '__v') continue;
            const v = obj[k];
            if (typeof v === 'string') { _faviconCache.set(k, v); _faviconTime.set(k, 0); }
            else if (v && typeof v.d === 'string') { _faviconCache.set(k, v.d); _faviconTime.set(k, v.t || 0); }
          }
        }
        resolve();
      });
    } catch { resolve(); }
  });
}

function scheduleFaviconSave() {
  if (_faviconSaveTimer) return;
  _faviconSaveTimer = setTimeout(() => {
    _faviconSaveTimer = null;
    if (!_faviconDirty) return;
    _faviconDirty = false;
    const obj = { __v: FAVICON_CACHE_VERSION };
    for (const [k, v] of _faviconCache) obj[k] = { d: v, t: _faviconTime.get(k) || 0 };
    try { chrome.storage.local.set({ [FAVICON_STORE_KEY]: obj }); } catch {}
  }, 2000);
}

function evictFavicon(key) {
  if (!_faviconCache.has(key)) return;
  _faviconCache.delete(key);
  _faviconTime.delete(key);
  _faviconDirty = true;
  scheduleFaviconSave();
}

function blobToDataUrl(blob) {
  return new Promise((resolve, reject) => {
    const fr = new FileReader();
    fr.onload = () => resolve(fr.result);
    fr.onerror = reject;
    fr.readAsDataURL(blob);
  });
}

// Fetch one icon source and re-encode it to a small PNG data URL. Returns null
// if the source can't be fetched, decoded, or renders fully transparent. We
// ALWAYS go through PNG: caching the raw bytes of some .ico files (a 2-colour
// icon Chrome decodes to transparency, or a malformed oversized one) produced
// an entry that loaded but showed nothing in the extension — the regression
// behind "site X stopped showing its favicon".
async function reencodeFaviconToPng(src) {
  try {
    const res = await fetch(src);
    if (!res.ok) return null;
    const blob = await res.blob();
    if (!blob.size || !/^image\//.test(blob.type)) return null;
    const bmp = await createImageBitmap(blob); // throws on undecodable bytes
    const canvas = new OffscreenCanvas(FAVICON_PX, FAVICON_PX);
    const ctx = canvas.getContext('2d');
    ctx.drawImage(bmp, 0, 0, FAVICON_PX, FAVICON_PX);
    bmp.close && bmp.close();
    const px = ctx.getImageData(0, 0, FAVICON_PX, FAVICON_PX).data;
    let visible = false;
    for (let p = 3; p < px.length; p += 4) { if (px[p] !== 0) { visible = true; break; } }
    if (!visible) return null;
    const out = await canvas.convertToBlob({ type: 'image/png' });
    if (!out.size || out.size > 200000) return null;
    const dataUrl = await blobToDataUrl(out);
    return (typeof dataUrl === 'string' && dataUrl.startsWith('data:image')) ? dataUrl : null;
  } catch { return null; }
}

// Resolve a bookmark's favicon for caching: walk the candidate sources and keep
// the first that re-encodes to a visible PNG. So if a site's own favicon.ico is
// undecodable, we fall through to DuckDuckGo / Google which serve clean PNGs.
async function resolveAndCacheFavicon(key, url, force = false) {
  const have = _faviconCache.get(key);
  if (!force && typeof have === 'string' && have.startsWith('data:')) return;
  if (_faviconResolving.has(key)) return;
  _faviconResolving.add(key);
  try {
    const cands = faviconCandidates(url).filter(c => !c.startsWith('data:'));
    for (const src of cands) {
      const dataUrl = await reencodeFaviconToPng(src);
      if (dataUrl) {
        _faviconCache.set(key, dataUrl);
        _faviconTime.set(key, Date.now());
        _faviconDirty = true;
        scheduleFaviconSave();
        return;
      }
    }
  } finally { _faviconResolving.delete(key); }
}

// Re-resolve a stale icon in the background (handles sites that rebrand). The
// displayed icon keeps using the cached copy; only the stored bytes update.
function maybeRefreshFavicon(key, url) {
  if (Date.now() - (_faviconTime.get(key) || 0) < FAVICON_TTL) return;
  // Mark as fresh now so a failed refresh won't retry on every render.
  _faviconTime.set(key, Date.now());
  _faviconDirty = true;
  scheduleFaviconSave();
  resolveAndCacheFavicon(key, url, true);
}

function setFavicon(img, url) {
  const key = faviconCacheKey(url);
  let candidates = faviconCandidates(url);
  const cached = _faviconCache.get(key);
  if (cached) {
    candidates = [cached, ...candidates.filter(c => c !== cached)];
    maybeRefreshFavicon(key, url);
  }
  let i = 0;
  function next() {
    if (i >= candidates.length) { img.onerror = null; img.onload = null; img.style.visibility = 'hidden'; return; }
    img.src = candidates[i++];
  }
  img.onload = () => {
    // Something is on screen; make sure a clean PNG is cached for next time.
    if (!_faviconCache.has(key)) resolveAndCacheFavicon(key, url);
  };
  img.onerror = () => {
    // A cached entry that no longer loads is poison — drop it and re-resolve.
    if (cached && img.src === cached) evictFavicon(key);
    next();
  };
  next();
}

// ── Render ──
function updateNavLayout() {
  const nav = document.getElementById('pagesNav');
  if (!nav) return;

  const navLeft = nav.getBoundingClientRect().left;
  let rightBound = null;

  const nsbBar = document.querySelector('.nsb-bar');
  if (nsbBar) {
    const r = nsbBar.getBoundingClientRect();
    if (r.width > 0) rightBound = r.left;
  }

  const widgets = document.getElementById('topWidgets');
  if (widgets) {
    const wLeft = widgets.getBoundingClientRect().left;
    rightBound = rightBound !== null ? Math.min(rightBound, wLeft) : wLeft;
  }

  if (rightBound !== null && rightBound > navLeft) {
    nav.style.maxWidth = Math.max(80, rightBound - navLeft - 16) + 'px';
  } else {
    nav.style.maxWidth = '';
  }

  _updateScrollThumb?.();
}

function syncLayout() {
  const topbar = document.querySelector('.topbar');
  if (!topbar) return;
  const colItems = document.querySelectorAll('.boards-columns > .board-column');
  if (!colItems.length) { topbar.style.width = ''; topbar.style.marginLeft = ''; updateNavLayout(); return; }
  const first = colItems[0].getBoundingClientRect();
  const last  = colItems[colItems.length - 1].getBoundingClientRect();
  // Round to whole pixels: the search bar is centered in here via CSS grid, and a
  // fractional topbar width/offset puts it at a sub-pixel position, which makes
  // its backdrop-filter pill render a faint seam along the rounded edges.
  topbar.style.width = Math.round(last.right - first.left) + 'px';
  topbar.style.marginLeft = Math.round(first.left) + 'px';
  updateNavLayout();
}

function renderAll() { renderPages(); renderBoards(); renderNavSearch(); requestAnimationFrame(syncLayout); }

function renderPages() {
  const nav = document.getElementById('pagesNav');
  const prevScroll = nav.querySelector('.tabs-group')?.scrollLeft || 0;
  nav.innerHTML = '';


  const group = document.createElement('div');
  group.className = 'tabs-group';

  let _dragPageId = null;

  function clearDropIndicators() {
    group.querySelectorAll('.tab-drop-indicator').forEach(el => el.remove());
    group.querySelectorAll('.page-tab').forEach(t => t.classList.remove('drag-over', 'board-drop-target'));
  }

  [...S.pages].sort((a, b) => a.order - b.order).forEach(page => {
    const tab = document.createElement('div');
    tab.className = 'page-tab' + (page.id === S.activePage ? ' active' : '');
    tab.dataset.id = page.id;
    tab.draggable = true;

    const name = document.createElement('span');
    name.className = 'page-tab-name';
    name.textContent = page.name;
    name.addEventListener('dblclick', e => { e.stopPropagation(); startPageRename(page.id, name); });
    tab.appendChild(name);

    tab.addEventListener('click', () => switchPage(page.id));
    tab.addEventListener('contextmenu', e => {
      e.preventDefault();
      e.stopPropagation();
      showPageMenu(page.id, e.clientX, e.clientY);
    });

    tab.addEventListener('dragstart', e => {
      _dragPageId = page.id;
      e.dataTransfer.effectAllowed = 'move';
      setTimeout(() => tab.classList.add('dragging'), 0);
    });

    tab.addEventListener('dragend', () => {
      _dragPageId = null;
      tab.classList.remove('dragging');
      clearDropIndicators();
    });

    // A board being dragged (global _dragId, set in buildBoard's dragstart) is a
    // separate case from reordering page tabs (_dragPageId above): hovering it over
    // another page's tab should offer to move the board there, not reorder tabs.
    tab.addEventListener('dragover', e => {
      if (_dragId) {
        if (page.id === S.activePage) return;
        e.preventDefault();
        e.dataTransfer.dropEffect = 'move';
        tab.classList.add('board-drop-target');
        return;
      }
      if (!_dragPageId || _dragPageId === page.id) return;
      e.preventDefault();
      clearDropIndicators();
      const rect = tab.getBoundingClientRect();
      const before = e.clientX < rect.left + rect.width / 2;
      const indicator = document.createElement('div');
      indicator.className = 'tab-drop-indicator';
      group.insertBefore(indicator, before ? tab : tab.nextSibling);
    });

    tab.addEventListener('dragleave', e => {
      if (!tab.contains(e.relatedTarget)) tab.classList.remove('board-drop-target');
    });

    tab.addEventListener('drop', e => {
      if (_dragId) {
        e.preventDefault();
        tab.classList.remove('board-drop-target');
        const droppedBoardId = _dragId;
        _dragId = null;
        if (page.id !== S.activePage) {
          moveBoardToPage(droppedBoardId, page.id);
          S.activePage = page.id;
          saveState();
          renderAll();
        }
        return;
      }
      e.preventDefault();
      if (!_dragPageId || _dragPageId === page.id) return;
      clearDropIndicators();
      const rect = tab.getBoundingClientRect();
      const before = e.clientX < rect.left + rect.width / 2;
      const sorted = [...S.pages].sort((a, b) => a.order - b.order);
      const fromIdx = sorted.findIndex(p => p.id === _dragPageId);
      const [dragged] = sorted.splice(fromIdx, 1);
      const toIdx = sorted.findIndex(p => p.id === page.id);
      sorted.splice(before ? toIdx : toIdx + 1, 0, dragged);
      sorted.forEach((p, i) => { p.order = i; });
      saveState();
      renderPages();
    });

    group.appendChild(tab);
  });

  const addBtn = document.createElement('button');
  addBtn.className = 'add-page-btn';
  addBtn.title = T('tip.newPage');
  addBtn.setAttribute('data-tour', 'add-page');
  addBtn.innerHTML = `<span style="font-size:20px;line-height:1;font-weight:300;">+</span>`;
  addBtn.addEventListener('click', addPage);
  group.appendChild(addBtn);

  nav.appendChild(group);

  // Scrollbar inside tabs-group
  const scrollBar = document.createElement('div');
  scrollBar.className = 'tabs-scroll-bar';
  const scrollThumb = document.createElement('div');
  scrollThumb.className = 'tabs-scroll-thumb';
  scrollBar.appendChild(scrollThumb);
  nav.appendChild(scrollBar);

  function updateScrollThumb() {
    const visible = group.clientWidth;
    const total = group.scrollWidth;
    const ratio = visible / total;
    const hasScroll = ratio < 0.999;
    scrollBar.style.opacity = hasScroll ? '1' : '0';
    scrollThumb.style.width = (ratio * 100) + '%';
    scrollThumb.style.left = ((group.scrollLeft / total) * 100) + '%';
    const atEnd = group.scrollLeft + visible >= total - 2;
    nav.classList.toggle('has-overflow', hasScroll && !atEnd);
  }
  _updateScrollThumb = updateScrollThumb;

  group.addEventListener('scroll', updateScrollThumb);

  // Click on track → jump to position
  scrollBar.addEventListener('mousedown', e => {
    if (e.target === scrollThumb) return;
    const rect = scrollBar.getBoundingClientRect();
    const pct = (e.clientX - rect.left) / rect.width;
    group.scrollLeft = pct * group.scrollWidth - group.clientWidth / 2;
    e.preventDefault();
  });

  // Drag thumb
  scrollThumb.addEventListener('mousedown', e => {
    e.preventDefault();
    const startX = e.clientX;
    const startScroll = group.scrollLeft;
    const barW = scrollBar.clientWidth;
    const total = group.scrollWidth;
    function onMove(ev) {
      const dx = ev.clientX - startX;
      group.scrollLeft = startScroll + (dx / barW) * total;
    }
    function onUp() {
      document.removeEventListener('mousemove', onMove);
      document.removeEventListener('mouseup', onUp);
    }
    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onUp);
  });

  requestAnimationFrame(() => {
    group.scrollLeft = prevScroll;
    updateNavLayout();
    updateScrollThumb();
  });
}

function renderNavSearch() {
  const el = document.getElementById('navSearchBar');
  if (!el) return;
  if (!S.navSearchEnabled) { el.innerHTML = ''; closeNsbEnginePopup(); return; }

  const curEngId = S.navSearchEngine || 'google';
  const curEng = SEARCH_ENGINES.find(e => e.id === curEngId) || SEARCH_ENGINES[1];

  // Already built and still enabled: just refresh the engine icon in place. A full
  // teardown+rebuild here would drop focus and any in-progress typing every time
  // renderAll() runs for an unrelated reason (switching pages, dragging a board…).
  const existingBar = el.querySelector('.nsb-bar');
  if (existingBar) {
    const engBtn = existingBar.querySelector('.nsb-eng-logo');
    if (engBtn && engBtn.dataset.engineId !== curEng.id) {
      engBtn.dataset.engineId = curEng.id;
      engBtn.title = `Engine: ${curEng.name}`;
      engBtn.innerHTML = '';
      engBtn.appendChild(nsbEngineIcon(curEng, 16));
    }
    return;
  }

  el.innerHTML = '';

  const bar = document.createElement('div');
  bar.className = 'nsb-bar';

  // Static search icon (left)
  bar.insertAdjacentHTML('beforeend', `<svg class="nsb-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>`);

  // Text input
  const input = document.createElement('input');
  input.className = 'nsb-input';
  input.type = 'text';
  input.placeholder = T('search.widgetPlaceholder');
  input.autocomplete = 'off';
  input.spellcheck = false;
  input.addEventListener('keydown', e => {
    if (e.key === 'Enter' && input.value.trim()) {
      nsbDoSearch(input.value.trim());
      input.value = '';
    }
    if (e.key === 'Escape') { closeNsbEnginePopup(); input.blur(); }
  });
  bar.appendChild(input);

  // Engine icon button (right) — shows current engine, click opens picker
  const engBtn = document.createElement('button');
  engBtn.className = 'nsb-eng-logo';
  engBtn.dataset.engineId = curEng.id;
  engBtn.title = `Engine: ${curEng.name}`;
  engBtn.appendChild(nsbEngineIcon(curEng, 16));
  engBtn.addEventListener('click', e => { e.stopPropagation(); openNsbEnginePopup(engBtn); });
  bar.appendChild(engBtn);

  // Clicking anywhere in the pill (padding, gaps around the icon) that isn't the
  // input or the engine button should still focus the input — no dead zones.
  bar.addEventListener('mousedown', e => {
    if (e.target === input || e.target === engBtn || engBtn.contains(e.target)) return;
    e.preventDefault();
    input.focus();
  });

  el.appendChild(bar);
}

function closeNsbEnginePopup() {
  if (_nsbEngPopup) { _nsbEngPopup.remove(); _nsbEngPopup = null; }
}

function openNsbEnginePopup(engBtn) {
  closeNsbEnginePopup();

  const popup = document.createElement('div');
  popup.className = 'nsb-eng-popup';
  popup.style.visibility = 'hidden';

  SEARCH_ENGINES.forEach(eng => {
    const opt = document.createElement('button');
    opt.className = 'nsb-eng-opt' + (eng.id === (S.navSearchEngine || 'google') ? ' active' : '');
    opt.appendChild(nsbEngineIcon(eng, 18));
    const label = document.createElement('span');
    label.textContent = eng.name;
    opt.appendChild(label);
    opt.addEventListener('click', e => {
      e.stopPropagation();
      S.navSearchEngine = eng.id;
      saveState();
      closeNsbEnginePopup();
      renderNavSearch();
      requestAnimationFrame(syncLayout);
    });
    popup.appendChild(opt);
  });

  _nsbEngPopup = popup;
  document.body.appendChild(popup);

  // Position after layout — visibility:hidden prevents flash
  const btnRect = engBtn.getBoundingClientRect();
  const popW = popup.offsetWidth;
  const popH = popup.offsetHeight;
  let left = btnRect.left;
  let top  = btnRect.bottom + 8;
  if (left + popW > window.innerWidth - 8) left = window.innerWidth - popW - 8;
  if (top  + popH > window.innerHeight - 8) top = btnRect.top - popH - 8;
  popup.style.left = left + 'px';
  popup.style.top  = top  + 'px';
  popup.style.visibility = '';

  const outsideClick = ev => {
    if (!popup.contains(ev.target) && ev.target !== engBtn) {
      closeNsbEnginePopup();
      document.removeEventListener('click', outsideClick, true);
    }
  };
  setTimeout(() => document.addEventListener('click', outsideClick, true), 0);
}


function activateColDropZones(sourceCol) {
  const ba = document.getElementById('boardsArea');
  const baRect = ba ? ba.getBoundingClientRect() : { top: 0, bottom: window.innerHeight };
  const bottomOffset = window.innerHeight - baRect.bottom;
  const colEls = document.querySelectorAll('.board-column');

  document.querySelectorAll('.col-drop-zone').forEach(z => {
    const c = parseInt(z.dataset.col);
    if (c === sourceCol) return;
    const colEl = colEls[c];
    if (!colEl) return;
    const colRect = colEl.getBoundingClientRect();
    // Start zone from the grid-cell (+ button) position, clamped to visible area
    const gridCell = colEl.querySelector('.grid-cell');
    const naturalTop = gridCell ? gridCell.getBoundingClientRect().top : colRect.bottom;
    const top = Math.max(baRect.top + 8, Math.min(naturalTop, baRect.bottom - 60));
    z.classList.add('active');
    z.style.cssText = `position:fixed;left:${colRect.left}px;width:${colRect.width}px;top:${top}px;bottom:${bottomOffset + 8}px;`;
  });
}
function deactivateColDropZones() {
  document.querySelectorAll('.col-drop-zone').forEach(z => {
    z.classList.remove('active', 'hover');
    z.style.cssText = '';
  });
}


function renderBoards() {
  const area = document.getElementById('boardsArea');
  area.innerHTML = '';

  const { BOARD_W, GAP, numCols } = getLayoutParams();

  const pageBoards = S.boards.filter(b => b.pageId === S.activePage);

  const container = document.createElement('div');
  container.className = 'boards-columns';
  container.style.setProperty('--board-w', BOARD_W + 'px');

  for (let c = 0; c < numCols; c++) {
    const col = document.createElement('div');
    col.className = 'board-column';
    const colBoards = pageBoards.filter(b => (b.col >= numCols ? numCols - 1 : b.col) === c).sort((a, b) => a.row - b.row);
    colBoards.forEach(board => {
      if (board.type === 'calendar') col.appendChild(buildCalendarBoard(board));
      else if (board.type === 'pomodoro') col.appendChild(buildPomodoroBoard(board));
      else if (board.type === 'notes') col.appendChild(buildNotesBoard(board));
      else if (board.type === 'search') col.appendChild(buildSearchBoard(board));
      else col.appendChild(buildBoard(board));
    });
    const dropZone = document.createElement('div');
    dropZone.className = 'col-drop-zone';
    dropZone.dataset.col = c;
    dropZone.addEventListener('dragover', e => { e.preventDefault(); dropZone.classList.add('hover'); });
    dropZone.addEventListener('dragleave', e => { if (!dropZone.contains(e.relatedTarget)) dropZone.classList.remove('hover'); });
    dropZone.addEventListener('drop', e => {
      e.preventDefault();
      dropZone.classList.remove('hover', 'active');
      if (_dragId) { moveBoardTo(_dragId, c, 9999); _dragId = null; }
    });
    col.appendChild(dropZone);
    col.appendChild(createCell(c, colBoards.length, true));

    // Gap drop: fires only in the spaces between boards (boards call stopPropagation)
    col.addEventListener('dragover', e => {
      if (!_dragId) return;
      e.preventDefault();
      const boards = [...col.querySelectorAll('.board')].filter(b => b.dataset.id !== _dragId);
      if (!boards.length) return;
      let best = null, bestBefore = true, bestDist = Infinity;
      boards.forEach(b => {
        const r = b.getBoundingClientRect();
        const dTop = Math.abs(e.clientY - r.top);
        const dBot = Math.abs(e.clientY - r.bottom);
        if (dTop < bestDist) { bestDist = dTop; best = b; bestBefore = true; }
        if (dBot < bestDist) { bestDist = dBot; best = b; bestBefore = false; }
      });
      document.querySelectorAll('.board.drop-before,.board.drop-after')
        .forEach(b => b.classList.remove('drop-before', 'drop-after'));
      if (best) {
        best.classList.add(bestBefore ? 'drop-before' : 'drop-after');
        _dropTarget = { id: best.dataset.id, before: bestBefore };
      }
    });
    col.addEventListener('dragleave', e => {
      if (col.contains(e.relatedTarget)) return;
      document.querySelectorAll('.board.drop-before,.board.drop-after')
        .forEach(b => b.classList.remove('drop-before', 'drop-after'));
      _dropTarget = null;
    });
    col.addEventListener('drop', e => {
      if (!_dragId) return;
      e.preventDefault();
      document.querySelectorAll('.board.drop-before,.board.drop-after')
        .forEach(b => b.classList.remove('drop-before', 'drop-after'));
      if (_dropTarget) insertBoardAt(_dragId, _dropTarget.id, _dropTarget.before);
      else moveBoardTo(_dragId, c, 9999);
      _dragId = null; _dropTarget = null;
    });

    container.appendChild(col);
  }

  area.appendChild(container);

  // Center the columns with an INTEGER margin so boards land on whole pixels
  // (sub-pixel positions trigger the backdrop-filter edge-halo). Done here
  // synchronously — not in syncLayout — so dragging never flashes boards left.
  const colsW = numCols * BOARD_W + (numCols - 1) * GAP;
  const offset = Math.max(0, Math.round((area.clientWidth - colsW) / 2));
  container.style.marginLeft = offset + 'px';

  updateFocusStats();
}

function createCell(col, row, canAdd) {
  const cell = document.createElement('div');
  cell.className = 'grid-cell' + (canAdd ? ' can-add' : '');
  const plus = document.createElement('span');
  plus.className = 'cell-plus';
  plus.textContent = '+';
  cell.appendChild(plus);
  if (canAdd) cell.addEventListener('click', () => addBoardAt(col, row));
  cell.addEventListener('dragover', e => { e.preventDefault(); cell.classList.add('drag-over'); });
  cell.addEventListener('dragleave', e => { if (!cell.contains(e.relatedTarget)) cell.classList.remove('drag-over'); });
  cell.addEventListener('drop', e => {
    e.preventDefault();
    cell.classList.remove('drag-over');
    if (_dragId) { moveBoardTo(_dragId, col, row); _dragId = null; }
  });
  return cell;
}

function buildBoard(board) {
  const el = document.createElement('div');
  el.className = 'board';
  el.dataset.id = board.id;
  const blurBg = document.createElement('div');
  blurBg.className = 'board-blur-bg';
  el.appendChild(blurBg);
  const accentBar = document.createElement('div');
  accentBar.className = 'board-accent-bar';
  el.appendChild(accentBar);

  const hdr = document.createElement('div');
  hdr.className = 'board-header';

  el.addEventListener('dragover', e => {
    if (!_dragBkId) return;
    e.preventDefault(); e.stopPropagation();
    el.classList.add('bk-drop-target');
  });
  el.addEventListener('dragleave', e => {
    if (!el.contains(e.relatedTarget)) el.classList.remove('bk-drop-target');
  });
  el.addEventListener('drop', e => {
    e.preventDefault(); e.stopPropagation();
    el.classList.remove('bk-drop-target');
    if (_dragBkId && _dragBkId !== board.id) {
      const bk = S.bookmarks.find(b => b.id === _dragBkId);
      if (bk) {
        bk.boardId = board.id;
        bk.order = S.bookmarks.filter(b => b.boardId === board.id && b.id !== bk.id).length;
        saveState(); renderBoards();
      }
      _dragBkId = null;
    }
  });

  el.addEventListener('mousedown', e => {
    if (e.target.tagName === 'BUTTON' || e.target.tagName === 'INPUT') return;
    if (e.target.closest('.link-item') || e.target.closest('.add-link-row')) return;
    el.draggable = true;
  });
  el.addEventListener('dragstart', e => {
    _dragId = board.id;
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', board.id);
    setTimeout(() => el.classList.add('is-dragging'), 0);
    activateColDropZones(board.col);
  });
  el.addEventListener('dragend', () => {
    el.draggable = false;
    el.classList.remove('is-dragging');
    document.querySelectorAll('.board.drop-before,.board.drop-after')
      .forEach(b => b.classList.remove('drop-before', 'drop-after'));
    _dropTarget = null;
    deactivateColDropZones();
    if (_dragId) { _dragId = null; renderBoards(); }
  });

  const titleEl = document.createElement('span');
  titleEl.className = 'board-title';
  titleEl.textContent = board.name;
  titleEl.addEventListener('dblclick', () => startBoardRename(board.id, titleEl));

  const addLinkBtn = document.createElement('button');
  addLinkBtn.className = 'board-add-link-btn';
  addLinkBtn.textContent = '+';
  addLinkBtn.title = T('tip.addLink');
  addLinkBtn.setAttribute('data-tour', 'add-link');
  addLinkBtn.addEventListener('click', e => { e.stopPropagation(); showAddLinkInput(board.id, addLinkBtn); });

  const menuBtn = document.createElement('button');
  menuBtn.className = 'board-menu-btn';
  menuBtn.textContent = '···';
  menuBtn.addEventListener('click', e => { e.stopPropagation(); showBoardMenu(board.id, menuBtn); });

  hdr.appendChild(titleEl);
  hdr.appendChild(addLinkBtn);
  hdr.appendChild(menuBtn);
  el.appendChild(hdr);


  const boardBks = [...S.bookmarks]
    .filter(bk => bk.boardId === board.id)
    .sort((a, b) => a.order - b.order);
  const isExpanded = _expandedBoards.has(board.id);
  const maxShow = (S.hideExtraBookmarks && !isExpanded) ? (S.maxBookmarksShown || 5) : boardBks.length;
  boardBks.slice(0, maxShow).forEach(bk => el.appendChild(buildBookmark(bk)));
  if (S.hideExtraBookmarks) {
    if (boardBks.length > maxShow) {
      const moreBtn = document.createElement('button');
      moreBtn.className = 'bk-show-more-btn';
      moreBtn.textContent = T('board.more', { n: boardBks.length - maxShow });
      moreBtn.addEventListener('click', e => {
        e.stopPropagation();
        _expandedBoards.add(board.id);
        renderBoards();
      });
      el.appendChild(moreBtn);
    } else if (isExpanded && boardBks.length > (S.maxBookmarksShown || 5)) {
      const hideBtn = document.createElement('button');
      hideBtn.className = 'bk-show-more-btn';
      hideBtn.textContent = T('board.showLess');
      hideBtn.addEventListener('click', e => {
        e.stopPropagation();
        _expandedBoards.delete(board.id);
        renderBoards();
      });
      el.appendChild(hideBtn);
    }
  }

  el.addEventListener('dragover', e => {
    if (!_dragId || _dragId === board.id) return;
    e.preventDefault(); e.stopPropagation();
    const before = e.clientY < el.getBoundingClientRect().top + el.offsetHeight / 2;
    document.querySelectorAll('.board.drop-before,.board.drop-after')
      .forEach(b => b.classList.remove('drop-before', 'drop-after'));
    el.classList.add(before ? 'drop-before' : 'drop-after');
    _dropTarget = { id: board.id, before };
  });
  el.addEventListener('dragleave', e => {
    if (_dragId && !el.contains(e.relatedTarget))
      el.classList.remove('drop-before', 'drop-after');
  });
  el.addEventListener('drop', e => {
    if (!_dragId || _dragId === board.id) return;
    e.preventDefault(); e.stopPropagation();
    el.classList.remove('drop-before', 'drop-after');
    if (_dropTarget) { insertBoardAt(_dragId, _dropTarget.id, _dropTarget.before); }
    _dragId = null; _dropTarget = null;
  });

  applyBoardStyle(el, board);
  return el;
}

function buildBookmark(bk) {
  const el = document.createElement('a');
  el.className = 'link-item';
  el.href = bk.url;
  el.target = S.openInNewTab !== false ? '_blank' : '_self';
  const img = document.createElement('img');
  img.className = 'favicon';
  setFavicon(img, bk.url);

  const info = document.createElement('div');
  info.className = 'link-info';

  const title = document.createElement('span');
  title.className = 'link-title';
  title.textContent = bk.title;
  info.appendChild(title);

  if (bk.description) {
    const desc = document.createElement('span');
    desc.className = 'link-desc';
    desc.textContent = bk.description;
    info.appendChild(desc);
  }

  const menuBtn = document.createElement('button');
  menuBtn.className = 'link-menu-btn';
  menuBtn.textContent = '⋮';
  menuBtn.addEventListener('click', e => { e.preventDefault(); e.stopPropagation(); showBookmarkMenu(bk.id, menuBtn); });

  el.addEventListener('contextmenu', e => { e.preventDefault(); e.stopPropagation(); showBookmarkMenu(bk.id, menuBtn, e.clientX, e.clientY); });

  el.addEventListener('mouseenter', () => {
    try {
      const origin = new URL(bk.url).origin;
      if (!document.querySelector(`link[data-pre="${origin}"]`)) {
        const l = document.createElement('link');
        l.rel = 'preconnect'; l.href = origin; l.dataset.pre = origin;
        document.head.appendChild(l);
      }
    } catch {}
  });
  el.addEventListener('mousedown', e => { if (e.target.closest('button')) return; el.draggable = true; });
  el.addEventListener('dragstart', e => {
    _dragBkId = bk.id;
    e.dataTransfer.effectAllowed = 'move';
    e.stopPropagation();
    setTimeout(() => el.classList.add('bk-dragging'), 0);
  });
  el.addEventListener('dragend', () => {
    el.draggable = false;
    el.classList.remove('bk-dragging');
    document.querySelectorAll('.board.bk-drop-target').forEach(b => b.classList.remove('bk-drop-target'));
    document.querySelectorAll('.link-item.bk-drop-before,.link-item.bk-drop-after')
      .forEach(b => b.classList.remove('bk-drop-before', 'bk-drop-after'));
    _bkDropTarget = null;
    if (_dragBkId) { _dragBkId = null; renderBoards(); }
  });

  el.addEventListener('dragover', e => {
    if (!_dragBkId || _dragBkId === bk.id) return;
    e.preventDefault(); e.stopPropagation();
    const before = e.clientY < el.getBoundingClientRect().top + el.offsetHeight / 2;
    document.querySelectorAll('.link-item.bk-drop-before,.link-item.bk-drop-after')
      .forEach(b => b.classList.remove('bk-drop-before', 'bk-drop-after'));
    el.classList.add(before ? 'bk-drop-before' : 'bk-drop-after');
    _bkDropTarget = { id: bk.id, before };
  });
  el.addEventListener('dragleave', e => {
    if (_dragBkId && !el.contains(e.relatedTarget))
      el.classList.remove('bk-drop-before', 'bk-drop-after');
  });
  el.addEventListener('drop', e => {
    if (!_dragBkId || _dragBkId === bk.id) return;
    e.preventDefault(); e.stopPropagation();
    el.classList.remove('bk-drop-before', 'bk-drop-after');
    if (_bkDropTarget) { reorderBookmark(_dragBkId, _bkDropTarget.id, _bkDropTarget.before); }
    _dragBkId = null; _bkDropTarget = null;
  });

  el.appendChild(img); el.appendChild(info); el.appendChild(menuBtn);
  return el;
}

// ── Bookmark popup helpers ──
function _placePopup(popup, anchor) {
  document.body.appendChild(popup);
  const r = anchor.getBoundingClientRect();
  popup.style.top  = Math.min(r.bottom + 6, window.innerHeight - 220) + 'px';
  popup.style.left = Math.min(r.left, window.innerWidth - 236) + 'px';
}

function _popupInput(parent, val, placeholder) {
  const inp = document.createElement('input');
  inp.className = 'add-link-input';
  inp.value = val || '';
  inp.placeholder = placeholder;
  parent.appendChild(inp);
  return inp;
}

function _popupBtns(parent, onCancel, onPrimary, primaryLabel) {
  const row = document.createElement('div');
  row.className = 'bk-popup-btns';
  const cancel = document.createElement('button');
  cancel.className = 'bk-popup-btn';
  cancel.textContent = T('common.cancel');
  cancel.addEventListener('click', onCancel);
  const primary = document.createElement('button');
  primary.className = 'bk-popup-btn bk-popup-btn-primary';
  primary.textContent = primaryLabel;
  primary.addEventListener('click', onPrimary);
  row.appendChild(cancel); row.appendChild(primary);
  parent.appendChild(row);
  return primary;
}

function _outsideClose(popup, exclude) {
  setTimeout(() => {
    const h = e => {
      if (!popup.contains(e.target) && (!exclude || !exclude.contains(e.target))) {
        popup.remove(); document.removeEventListener('click', h);
      }
    };
    document.addEventListener('click', h);
  }, 0);
}

// ── Add link (step 1 → step 2) ──
function showAddLinkInput(boardId, anchor) {
  document.querySelector('.bk-popup')?.remove();

  const popup = document.createElement('div');
  popup.className = 'bk-edit-popup bk-popup';

  const urlInput = _popupInput(popup, '', T('addlink.pasteUrl'));
  _popupBtns(popup, () => popup.remove(), proceed, T('addlink.add'));
  _placePopup(popup, anchor);
  urlInput.focus();

  function proceed() {
    const raw = urlInput.value.trim();
    if (!raw) { urlInput.focus(); return; }
    const url = /^https?:\/\//.test(raw) ? raw : 'https://' + raw;
    popup.remove();
    showAddLinkStep2(boardId, anchor, url);
  }
  urlInput.addEventListener('keydown', e => { if (e.key === 'Enter') proceed(); if (e.key === 'Escape') popup.remove(); });
  // No outside-click close here: an accidental click outside would discard the
  // typed URL. Dismiss via Esc or the Cancel button instead.
}

function showAddLinkStep2(boardId, anchor, url) {
  const popup = document.createElement('div');
  popup.className = 'bk-edit-popup bk-popup';

  const urlInput  = _popupInput(popup, url, T('popup.url'));
  // Pre-fill the name with the hostname; the user can rename it before saving.
  let autoName = url; try { autoName = new URL(url).hostname.replace('www.', ''); } catch {}
  const nameInput = _popupInput(popup, autoName, T('addlink.name'));
  const descInput = _popupInput(popup, '', T('addlink.descOptional'));

  function save() {
    const finalUrl = urlInput.value.trim();
    if (!finalUrl) return;
    const u = /^https?:\/\//.test(finalUrl) ? finalUrl : 'https://' + finalUrl;
    let auto = u; try { auto = new URL(u).hostname.replace('www.', ''); } catch {}
    addBookmark(boardId, u, nameInput.value.trim() || auto, descInput.value.trim() || undefined);
    popup.remove();
  }

  _popupBtns(popup, () => popup.remove(), save, T('addlink.add'));
  _placePopup(popup, anchor);
  nameInput.focus(); nameInput.select();

  [nameInput, descInput].forEach(inp =>
    inp.addEventListener('keydown', e => { if (e.key === 'Enter') save(); if (e.key === 'Escape') popup.remove(); })
  );
  urlInput.addEventListener('keydown', e => { if (e.key === 'Escape') popup.remove(); });
  // No outside-click close: don't discard a typed name/description by accident.
  // Dismiss via Esc or the Cancel button instead.
}

// ── Bookmark context menu ──
// Shared icon set for context menus — stroke uses currentColor so both themes work.
const MENU_ICONS = {
  open:      '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/><polyline points="15 3 21 3 21 9"/><line x1="10" y1="14" x2="21" y2="3"/></svg>',
  incognito: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>',
  edit:      '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.12 2.12 0 0 1 3 3L12 15l-4 1 1-4z"/></svg>',
  rename:    '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="4 7 4 4 20 4 20 7"/><line x1="9" y1="20" x2="15" y2="20"/><line x1="12" y1="4" x2="12" y2="20"/></svg>',
  openAll:   '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="12 2 2 7 12 12 22 7 12 2"/><polyline points="2 17 12 22 22 17"/><polyline points="2 12 12 17 22 12"/></svg>',
  trash:     '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/><path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/></svg>',
  customize: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="4" y1="21" x2="4" y2="14"/><line x1="4" y1="10" x2="4" y2="3"/><line x1="12" y1="21" x2="12" y2="12"/><line x1="12" y1="8" x2="12" y2="3"/><line x1="20" y1="21" x2="20" y2="16"/><line x1="20" y1="12" x2="20" y2="3"/><line x1="1" y1="14" x2="7" y2="14"/><line x1="9" y1="8" x2="15" y2="8"/><line x1="17" y1="16" x2="23" y2="16"/></svg>',
  chevron:   '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="6 9 12 15 18 9"/></svg>',
};

// Builds a context-menu shell with a consistent icon + label row style.
// Returns { menu, item, sep, expandable }. `icon` is one of MENU_ICONS (optional).
function createMenu() {
  const menu = document.createElement('div');
  menu.className = 'board-menu';
  function item(label, icon, cls, cb) {
    const el = document.createElement('div');
    el.className = 'board-menu-item' + (cls ? ' ' + cls : '');
    const ic = document.createElement('span');
    ic.className = 'board-menu-icon';
    if (icon) ic.innerHTML = icon;
    const lb = document.createElement('span');
    lb.className = 'board-menu-label';
    lb.textContent = label;
    el.appendChild(ic); el.appendChild(lb);
    el.addEventListener('click', () => { closeMenu(); cb(); });
    menu.appendChild(el);
  }
  function sep() {
    const el = document.createElement('div');
    el.className = 'board-menu-sep';
    menu.appendChild(el);
  }
  // A row that looks like a normal item but expands a panel below it, inside
  // the same popup, instead of closing the menu. `buildPanel(innerEl)` fills
  // the panel; clicks inside the row/panel are kept from bubbling to
  // document so the outside-click-closes-menu listener leaves it alone.
  function expandable(label, icon, buildPanel) {
    const row = document.createElement('div');
    row.className = 'board-menu-item board-menu-expand-row';
    const ic = document.createElement('span');
    ic.className = 'board-menu-icon';
    if (icon) ic.innerHTML = icon;
    const lb = document.createElement('span');
    lb.className = 'board-menu-label';
    lb.textContent = label;
    const chev = document.createElement('span');
    chev.className = 'board-menu-chevron';
    chev.innerHTML = MENU_ICONS.chevron;
    row.appendChild(ic); row.appendChild(lb); row.appendChild(chev);

    const panel = document.createElement('div');
    panel.className = 'board-menu-panel';
    const inner = document.createElement('div');
    inner.className = 'board-menu-panel-inner';
    panel.appendChild(inner);
    buildPanel(inner);

    row.addEventListener('click', e => {
      e.stopPropagation();
      const open = !panel.classList.contains('open');
      panel.classList.toggle('open', open);
      row.classList.toggle('open', open);
    });
    panel.addEventListener('click', e => e.stopPropagation());

    menu.appendChild(row);
    menu.appendChild(panel);
  }
  return { menu, item, sep, expandable };
}

function showBookmarkMenu(bkId, anchor, cx, cy) {
  closeMenu();
  const { menu, item, sep } = createMenu();
  _menu = menu;

  const bkForMenu = S.bookmarks.find(b => b.id === bkId);
  if (bkForMenu?.url) {
    item(T('menu.open'),           MENU_ICONS.open,      '', () => chrome.tabs.create({ url: bkForMenu.url, active: false }));
    item(T('menu.openIncognito'),  MENU_ICONS.incognito, '', () => chrome.windows.create({ url: bkForMenu.url, incognito: true }));
    sep();
  }
  item(T('menu.edit'),   MENU_ICONS.edit,  '',       () => showBookmarkEdit(bkId, anchor));
  item(T('menu.delete'), MENU_ICONS.trash, 'danger', () => deleteBookmark(bkId));

  document.body.appendChild(menu);

  const mw = menu.offsetWidth + 12;
  if (cx !== undefined && cy !== undefined) {
    menu.style.left = Math.min(cx, window.innerWidth - mw) + 'px';
    menu.style.top  = Math.min(cy, window.innerHeight - menu.offsetHeight - 8) + 'px';
  } else {
    const r = anchor.getBoundingClientRect();
    menu.style.left = Math.min(r.left, window.innerWidth - mw) + 'px';
    menu.style.top  = (r.bottom + 4) + 'px';
  }
  setTimeout(() => document.addEventListener('click', closeMenu, { once: true }), 0);
}

// ── Edit bookmark ──
function showBookmarkEdit(bkId, anchor) {
  document.querySelector('.bk-popup')?.remove();
  const bk = S.bookmarks.find(b => b.id === bkId);
  if (!bk) return;

  const popup = document.createElement('div');
  popup.className = 'bk-edit-popup bk-popup';

  const urlInput  = _popupInput(popup, bk.url,         T('popup.url'));
  const nameInput = _popupInput(popup, bk.title,        T('addlink.name'));
  const descInput = _popupInput(popup, bk.description,  T('addlink.descOptional'));

  function save() {
    const raw = urlInput.value.trim(); if (!raw) return;
    bk.url = /^https?:\/\//.test(raw) ? raw : 'https://' + raw;
    let auto = bk.url; try { auto = new URL(bk.url).hostname.replace('www.', ''); } catch {}
    bk.title = nameInput.value.trim() || auto;
    const d = descInput.value.trim();
    if (d) bk.description = d; else delete bk.description;
    saveState(); renderBoards(); popup.remove();
  }

  _popupBtns(popup, () => popup.remove(), save, T('common.save'));
  _placePopup(popup, anchor);
  nameInput.focus();

  [urlInput, nameInput, descInput].forEach(inp =>
    inp.addEventListener('keydown', e => { if (e.key === 'Enter') save(); if (e.key === 'Escape') popup.remove(); })
  );
  // No outside-click close: don't discard edits by an accidental click outside.
  // Dismiss via Esc or the Cancel button instead.
}

// ── Board menu ──
let _menu = null;
let _dragId = null, _dragBkId = null, _dropTarget = null, _bkDropTarget = null;
let _updateScrollThumb = null;
let _nsbEngPopup = null;

document.addEventListener('wheel', e => {
  if (!_dragId && !_dragBkId) return;
  const ba = document.getElementById('boardsArea');
  if (ba) { ba.scrollTop += e.deltaY; e.preventDefault(); }
}, { passive: false });
let _calendarState = {};
let _pomodoroState = {};
let _expandedBoards = new Set();
function closeMenu() { if (_menu) { _menu.remove(); _menu = null; } }

// Per-board customization was removed (caused backdrop-filter light-stripe
// artifacts with many customized boards). This now only clears any leftover
// inline styling so old boards fall back to the default theme appearance.
function applyBoardStyle(el, board) {
  const textVars = ['--board-text','--board-text-secondary','--board-text-dim','--board-text-hover','--board-hover-bg'];
  el.style.removeProperty('background'); el.style.removeProperty('backdrop-filter');
  el.style.removeProperty('-webkit-backdrop-filter'); el.style.removeProperty('border-color');
  textVars.forEach(v => el.style.removeProperty(v));
  el.classList.remove('board-custom-light');

  // Accent shape + color, set from the board menu's "Customize" panel.
  // Corner and outline are independent and can both be on at once.
  el.classList.toggle('accent-corner', !!board.accentCorner);
  el.classList.toggle('accent-outline', !!board.accentOutline);
  // "From theme" bulk outline (see renderAppearanceTab): the same crisp ring,
  // driven live by --board-outline-theme-color instead of a pinned hex — see
  // the .accent-outline.accent-theme rule in style.css.
  el.classList.toggle('accent-theme', !!board.accentTheme);
  if (board.accentColor && (board.accentCorner || board.accentOutline)) {
    el.style.setProperty('--board-accent', board.accentColor);
  } else {
    el.style.removeProperty('--board-accent');
  }
}

// Preset accent colors offered in the board menu's "Customize" panel, plus a
// free-form picker. Keep in sync visually with ACCENT_DEFAULT below.
const ACCENT_COLORS = ['#9b87f5', '#2dd4bf', '#f2a93b', '#f2618b', '#5b9df9', '#8bc34a'];
const ACCENT_DEFAULT = ACCENT_COLORS[0];

// '#9b87f5' -> '155,135,245', for rgba() fills that need to work as a light
// wash in both themes (a flat var(--accent) background would be too loud,
// and color-mix() isn't used anywhere else in this file).
function hexToRgb(hex) {
  const m = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex || '');
  return m ? `${parseInt(m[1], 16)},${parseInt(m[2], 16)},${parseInt(m[3], 16)}` : '155,135,245';
}

// ── Bulk board outline: single source of truth for the effective color/
// opacity, shared by applyThemeStyle's live CSS var and the Appearance tab's
// UI (see renderAppearanceTab) — so what the settings panel shows is always
// exactly what's actually painted on the boards, with no separate "apply"
// step to fall out of sync.
function outlineEffectiveHex() {
  return S.outlineColorOverride || S.themeStyle?.boardColorHex || '#ffffff';
}
function outlineEffectiveOpacityPct() {
  if (S.outlineOpacityOverride != null) return S.outlineOpacityOverride;
  // A picked color defaults to fully solid, matching the per-board
  // "Customize" outline's plain hex; the theme-tracked ring keeps the
  // softer derived-from-board-opacity default.
  return S.outlineColorOverride ? 100 : Math.round(Math.min(75, (S.themeStyle?.boardOpacity ?? 5) * 2));
}

// Fills `inner` with the corner/outline toggles + color swatches for one
// board. Corner and outline are independent — both can be on at once.
function buildAccentPanel(inner, board) {
  function setPreviewVar(hex) {
    inner.style.setProperty('--menu-accent-preview', hex);
    inner.style.setProperty('--menu-accent-preview-rgb', hexToRgb(hex));
  }
  function syncSwatches() {
    colorRow.querySelectorAll('.menu-swatch').forEach(s => s.classList.toggle('active', s.dataset.hex === board.accentColor));
  }
  function liveUpdate() {
    saveState();
    const boardEl = document.querySelector(`.board[data-id="${board.id}"]`);
    if (boardEl) applyBoardStyle(boardEl, board);
    setPreviewVar(board.accentColor || ACCENT_DEFAULT);
  }

  const shapeRow = document.createElement('div');
  shapeRow.className = 'menu-shape-row';

  function shapeBtn(key, label, previewCls) {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'shape-opt' + (board[key] ? ' active' : '');
    const prev = document.createElement('span');
    prev.className = 'shape-preview ' + previewCls;
    const lb = document.createElement('span');
    lb.className = 'shape-opt-label';
    lb.textContent = label;
    btn.appendChild(prev); btn.appendChild(lb);
    btn.addEventListener('click', () => {
      board[key] = !board[key];
      // First time either shape is turned on with no color chosen yet: pick
      // a default so the swatches/ring reflect what's actually showing on
      // the board, instead of looking "on" with nothing selected below.
      // Also drops any "set by the bulk outline button" markers, since this
      // is now a deliberate manual choice — the bulk button's "Add outline"
      // must never overwrite it again, and a leftover "from theme" flag
      // would otherwise keep overriding this freshly-assigned manual color.
      if (board[key] && !board.accentColor) {
        board.accentColor = ACCENT_DEFAULT;
        delete board.accentTheme; delete board.accentBulk;
        syncSwatches();
      }
      btn.classList.toggle('active', board[key]);
      liveUpdate();
    });
    return btn;
  }
  shapeRow.appendChild(shapeBtn('accentCorner', T('accent.shapeCorner'), 'p-corner'));
  shapeRow.appendChild(shapeBtn('accentOutline', T('accent.shapeOutline'), 'p-outline'));
  inner.appendChild(shapeRow);

  const colorRow = document.createElement('div');
  colorRow.className = 'menu-color-row';

  function setColor(hex) {
    board.accentColor = hex;
    // A manual pick is a deliberate choice — take this board out of the
    // bulk outline button's reach for good, and drop any leftover "from
    // theme" flag.
    delete board.accentTheme; delete board.accentBulk;
    syncSwatches();
    liveUpdate();
  }
  ACCENT_COLORS.forEach(hex => {
    const sw = document.createElement('button');
    sw.type = 'button';
    sw.className = 'menu-swatch' + (board.accentColor === hex ? ' active' : '');
    sw.style.background = hex;
    sw.dataset.hex = hex;
    sw.addEventListener('click', () => setColor(hex));
    colorRow.appendChild(sw);
  });

  const customLabel = document.createElement('label');
  customLabel.className = 'menu-swatch-custom';
  customLabel.title = T('accent.customColor');
  customLabel.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>';
  const colorInput = document.createElement('input');
  colorInput.type = 'color';
  colorInput.value = board.accentColor || ACCENT_DEFAULT;
  colorInput.addEventListener('input', () => {
    colorRow.querySelectorAll('.menu-swatch').forEach(s => s.classList.remove('active'));
    setColor(colorInput.value);
  });
  customLabel.appendChild(colorInput);
  colorRow.appendChild(customLabel);
  inner.appendChild(colorRow);

  setPreviewVar(board.accentColor || ACCENT_DEFAULT);
}

function showBoardMenu(boardId, anchor) {
  closeMenu();
  const { menu, item, sep, expandable } = createMenu();

  const board = S.boards.find(b => b.id === boardId);
  const isCalOrPom = board && (board.type === 'calendar' || board.type === 'pomodoro');
  const isNotes = board && (board.type === 'notes' || board.type === 'search');
  let hasAction = false;
  if (!isCalOrPom) {
    item(T('menu.rename'), MENU_ICONS.rename, '', () => {
      const boardEl = document.querySelector(`.board[data-id="${boardId}"]`);
      if (boardEl) startBoardRename(boardId, boardEl.querySelector('.board-title'));
    });
    hasAction = true;
  }
  if (!isCalOrPom && !isNotes) {
    item(T('menu.openAll'), MENU_ICONS.openAll, '', () => {
      S.bookmarks.filter(bk => bk.boardId === boardId).forEach(bk => window.open(bk.url, '_blank'));
    });
    hasAction = true;
  }
  if (hasAction) sep();
  expandable(T('menu.customize'), MENU_ICONS.customize, inner => buildAccentPanel(inner, board));
  sep();
  item(T('menu.deleteBoard'), MENU_ICONS.trash, 'danger', () => deleteBoard(boardId));

  document.body.appendChild(menu);
  _menu = menu;

  const r = anchor.getBoundingClientRect();
  menu.style.top = (r.bottom + 4) + 'px';
  menu.style.left = Math.min(r.left, window.innerWidth - menu.offsetWidth - 12) + 'px';

  setTimeout(() => document.addEventListener('click', closeMenu, { once: true }), 0);
}

function showPageMenu(pageId, x, y) {
  closeMenu();
  const { menu, item, sep } = createMenu();

  item(T('menu.rename'), MENU_ICONS.rename, '', () => {
    const tab = document.querySelector(`.page-tab[data-id="${pageId}"]`);
    if (tab) startPageRename(pageId, tab.querySelector('.page-tab-name'));
  });
  if (S.pages.length > 1) {
    sep();
    item(T('menu.delete'), MENU_ICONS.trash, 'danger', () => deletePage(pageId));
  }

  document.body.appendChild(menu);
  _menu = menu;

  menu.style.top = Math.min(y + 6, window.innerHeight - menu.offsetHeight - 8) + 'px';
  menu.style.left = Math.min(x, window.innerWidth - menu.offsetWidth - 12) + 'px';

  setTimeout(() => document.addEventListener('click', closeMenu, { once: true }), 0);
}

// ── Inline rename ──
function startBoardRename(boardId, titleEl, opts = {}) {
  const board = S.boards.find(b => b.id === boardId);
  if (!board || !titleEl || titleEl.tagName === 'INPUT') return;
  const isNew = !!opts.isNew;

  const input = document.createElement('input');
  input.className = 'board-title-input';
  // Stop Chrome autofill from previewing a saved value in the freshly-focused
  // field, which tinted the placeholder pale blue (:-internal-autofill-previewed).
  input.setAttribute('autocomplete', 'off');
  input.spellcheck = false;
  input.value = board.name;
  if (isNew) input.placeholder = T('board.new');
  titleEl.replaceWith(input);
  input.focus(); input.select();

  let done = false;
  function discard() {
    // A brand-new board left unnamed is dropped — protects against accidental
    // clicks on "+" and means an empty board never gets saved.
    done = true;
    S.boards = S.boards.filter(b => b.id !== boardId);
    saveState();
    renderBoards();
  }
  function commit() {
    if (done) return;
    const name = input.value.trim();
    if (isNew && !name) { discard(); return; }
    done = true;
    board.name = name || board.name;
    saveState();
    const newEl = document.createElement('span');
    newEl.className = 'board-title';
    newEl.textContent = board.name;
    newEl.addEventListener('dblclick', () => startBoardRename(boardId, newEl));
    input.replaceWith(newEl);
  }
  input.addEventListener('blur', commit);
  input.addEventListener('keydown', e => {
    if (e.key === 'Enter') { e.preventDefault(); input.blur(); }
    if (e.key === 'Escape') {
      input.removeEventListener('blur', commit);
      if (isNew) { discard(); return; }
      input.value = board.name; commit(); // restore original name
    }
  });
}

function startPageRename(pageId, nameEl) {
  const page = S.pages.find(p => p.id === pageId);
  if (!page) return;

  const input = document.createElement('input');
  input.style.cssText = 'background:none;border:none;border-bottom:1px solid rgba(255,255,255,0.3);color:inherit;font:inherit;outline:none;width:80px;padding:0;';
  input.value = page.name;
  nameEl.replaceWith(input);
  input.focus(); input.select();

  let done = false;
  function commit() {
    if (done) return; done = true;
    page.name = input.value.trim() || page.name;
    saveState();
    nameEl.textContent = page.name;
    input.replaceWith(nameEl);
  }
  input.addEventListener('blur', commit);
  input.addEventListener('keydown', e => {
    if (e.key === 'Enter') { e.preventDefault(); input.blur(); }
    if (e.key === 'Escape') { done = true; input.value = page.name; input.blur(); commit(); }
  });
}

// ── CRUD ──
function addPage() {
  const maxOrder = S.pages.length ? Math.max(...S.pages.map(p => p.order)) : -1;
  const page = { id: genId(), name: T('page.new'), order: maxOrder + 1 };
  S.pages.push(page);
  S.activePage = page.id;
  saveState(); renderAll();
  setTimeout(() => {
    const tab = document.querySelector(`.page-tab[data-id="${page.id}"]`);
    if (tab) startPageRename(page.id, tab.querySelector('.page-tab-name'));
  }, 50);
}

function deletePage(pageId) {
  if (S.pages.length <= 1) return;
  const boardIds = S.boards.filter(b => b.pageId === pageId).map(b => b.id);
  const bkIds = S.bookmarks.filter(bk => boardIds.includes(bk.boardId)).map(bk => bk.id);
  S.bookmarks = S.bookmarks.filter(bk => !boardIds.includes(bk.boardId));
  S.boards = S.boards.filter(b => b.pageId !== pageId);
  S.pages = S.pages.filter(p => p.id !== pageId);
  if (S.activePage === pageId) S.activePage = S.pages[0].id;
  tombstoneMain([pageId, ...boardIds, ...bkIds]);
  saveState(); renderAll();
}

function switchPage(pageId) {
  if (S.activePage === pageId) return;
  S.activePage = pageId;
  saveState(); renderAll();
}

function addBoardAt(col, row) {
  const board = { id: genId(), pageId: S.activePage, name: '', col, row };
  S.boards.push(board);
  // Not saved yet: persists only once the user types a name (see startBoardRename).
  renderBoards();
  const boardEl = document.querySelector(`.board[data-id="${board.id}"]`);
  if (boardEl) startBoardRename(board.id, boardEl.querySelector('.board-title'), { isNew: true });
}

function compactColumn(col) {
  S.boards
    .filter(b => b.pageId === S.activePage && b.col === col)
    .sort((a, b) => a.row - b.row)
    .forEach((b, i) => { b.row = i; });
}

function moveBoardTo(boardId, col, row) {
  const board = S.boards.find(b => b.id === boardId);
  if (!board) return;
  const sourceCol = board.col;
  board.col = col; board.row = row;
  compactColumn(col);
  if (col !== sourceCol) compactColumn(sourceCol);
  saveState(); renderBoards();
}

function moveBoardToPage(boardId, pageId) {
  const board = S.boards.find(b => b.id === boardId);
  if (!board || board.pageId === pageId) return;
  const oldPageId = board.pageId;
  const col = board.col;
  board.row = S.boards.filter(b => b.pageId === pageId && b.col === col).length;
  board.pageId = pageId;
  S.boards
    .filter(b => b.pageId === oldPageId && b.col === col)
    .sort((a, b) => a.row - b.row)
    .forEach((b, i) => { b.row = i; });
}

function insertBoardAt(draggedId, targetId, before) {
  if (draggedId === targetId) return;
  const dragged = S.boards.find(b => b.id === draggedId);
  const target  = S.boards.find(b => b.id === targetId);
  if (!dragged || !target) return;
  const sourceCol = dragged.col;
  const colBoards = S.boards
    .filter(b => b.pageId === S.activePage && b.col === target.col && b.id !== draggedId)
    .sort((a, b) => a.row - b.row);
  const idx = colBoards.findIndex(b => b.id === targetId);
  dragged.col = target.col;
  colBoards.splice(before ? idx : idx + 1, 0, dragged);
  colBoards.forEach((b, i) => { b.row = i; });
  if (sourceCol !== target.col) compactColumn(sourceCol);
  saveState(); renderBoards();
}

function reorderBookmark(draggedId, targetId, before) {
  if (draggedId === targetId) return;
  const dragged = S.bookmarks.find(b => b.id === draggedId);
  const target  = S.bookmarks.find(b => b.id === targetId);
  if (!dragged || !target) return;
  dragged.boardId = target.boardId;
  const boardBks = S.bookmarks
    .filter(b => b.boardId === target.boardId && b.id !== draggedId)
    .sort((a, b) => a.order - b.order);
  const idx = boardBks.findIndex(b => b.id === targetId);
  boardBks.splice(before ? idx : idx + 1, 0, dragged);
  boardBks.forEach((b, i) => { b.order = i; });
  saveState(); renderBoards();
}

function findFreePosition() {
  const { numCols } = getLayoutParams();
  const occupied = new Set(S.boards.filter(b => b.pageId === S.activePage).map(b => `${b.col},${b.row}`));
  for (let row = 0; row < 100; row++) {
    for (let col = 0; col < numCols; col++) {
      if (!occupied.has(`${col},${row}`)) return { col, row };
    }
  }
  return { col: 0, row: 0 };
}

let _pomAudioCtx = null;
function playPomSound(type) {
  try {
    if (!_pomAudioCtx) _pomAudioCtx = new (window.AudioContext || window.webkitAudioContext)();
    const ctx = _pomAudioCtx;
    // resume() must run synchronously inside user gesture; if context just started
    // currentTime is 0 so schedule slightly ahead to give the clock time to tick
    const needsWarmup = ctx.state !== 'running';
    ctx.resume();
    const t = ctx.currentTime + (needsWarmup ? 0.15 : 0);
    if (type === 'start') {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = 'sine';
      osc.connect(gain); gain.connect(ctx.destination);
      osc.frequency.setValueAtTime(600, t);
      gain.gain.setValueAtTime(0.2, t);
      gain.gain.exponentialRampToValueAtTime(0.001, t + 0.12);
      osc.start(t); osc.stop(t + 0.12);
    } else {
      [[523, 0], [659, 0.18], [784, 0.36]].forEach(([freq, delay]) => {
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = 'sine';
        osc.connect(gain); gain.connect(ctx.destination);
        osc.frequency.setValueAtTime(freq, t + delay);
        gain.gain.setValueAtTime(0.35, t + delay);
        gain.gain.exponentialRampToValueAtTime(0.001, t + delay + 0.6);
        osc.start(t + delay); osc.stop(t + delay + 0.6);
      });
    }
  } catch(e) {}
}

function addPomodoro() {
  const { col, row } = findFreePosition();
  const board = { id: genId(), pageId: S.activePage, name: T('widget.pomodoro'), type: 'pomodoro', col, row };
  S.boards.push(board);
  saveState(); renderBoards();
}

const POM_PLAY  = `<svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><polygon points="8,3 20,12 8,21"/></svg>`;
const POM_PAUSE = `<svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><rect x="6" y="4" width="4" height="16"/><rect x="14" y="4" width="4" height="16"/></svg>`;
const POM_RESET = `<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"/><polyline points="3,3 3,8 8,8"/></svg>`;
const POM_SKIP  = `<svg width="13" height="13" viewBox="0 0 24 24" fill="currentColor"><polygon points="4,4 14,12 4,20" stroke="none"/><rect x="17" y="4" width="3" height="16" rx="1" stroke="none"/></svg>`;
const POM_GEAR  = `<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>`;

function pomFmt(s) { return String(Math.floor(s/60)).padStart(2,'0') + ':' + String(s%60).padStart(2,'0'); }

function getPomSettings(board) {
  return Object.assign({ focus: 25, short: 5, long: 15, cycle: 4 }, board.pomSettings || {});
}

function savePomTimer(boardId) {
  const ps = _pomodoroState[boardId];
  if (!ps || !S.pomTimers) return;
  S.pomTimers[boardId] = {
    phase: ps.phase, sessions: ps.sessions,
    timeLeft: ps.timeLeft, running: ps.running,
    startedAt: ps.startedAt || null, startedTimeLeft: ps.startedTimeLeft != null ? ps.startedTimeLeft : null
  };
  saveState();
}

function getPomState(id, focusMins) {
  if (!_pomodoroState[id]) {
    const saved = S.pomTimers?.[id];
    if (saved) {
      let timeLeft = saved.timeLeft ?? ((focusMins || 25) * 60);
      // Recalculate from wall-clock if timer was running when page was closed
      if (saved.running && saved.startedAt && saved.startedTimeLeft != null) {
        const elapsed = Math.floor((Date.now() - saved.startedAt) / 1000);
        timeLeft = Math.max(0, saved.startedTimeLeft - elapsed);
      }
      let phase = saved.phase || 'work';
      let sessions = saved.sessions || 0;
      const wasRunning = !!(saved.running && timeLeft > 0);
      // Phase completed while away — record focus time and reset
      if (saved.running && timeLeft <= 0 && saved.phase === 'work') {
        if (!S.focusStats) S.focusStats = [];
        S.focusStats.push({ ts: Date.now(), mins: Math.round((saved.startedTimeLeft || (focusMins || 25) * 60) / 60) });
        sessions++; phase = 'work'; timeLeft = (focusMins || 25) * 60;
      } else if (saved.running && timeLeft <= 0) {
        phase = 'work'; timeLeft = (focusMins || 25) * 60;
      }
      _pomodoroState[id] = {
        phase, viewPhase: phase, timeLeft, running: wasRunning, sessions,
        interval: null,
        startedAt: wasRunning ? saved.startedAt : null,
        startedTimeLeft: wasRunning ? saved.startedTimeLeft : null
      };
    } else {
      _pomodoroState[id] = { phase:'work', viewPhase:'work', timeLeft:(focusMins||25)*60, running:false, sessions:0, interval:null, startedAt:null, startedTimeLeft:null };
    }
  } else if (!_pomodoroState[id].viewPhase) {
    _pomodoroState[id].viewPhase = _pomodoroState[id].phase;
  }
  return _pomodoroState[id];
}

function getFocusByDay(days) {
  const now = new Date();
  const result = [];
  for (let i = days - 1; i >= 0; i--) {
    const d = new Date(now.getFullYear(), now.getMonth(), now.getDate() - i);
    const start = d.getTime();
    let mins = (S.focusStats || []).filter(s => s.ts >= start && s.ts < start + 86400000).reduce((a, s) => a + s.mins, 0);
    if (i === 0) {
      // Add in-progress/paused work time to today
      Object.entries(_pomodoroState).forEach(([boardId, ps]) => {
        if (ps.phase === 'work') {
          const b = S.boards.find(b => b.id === boardId);
          if (b) mins += Math.floor(Math.max(0, getPomSettings(b).focus * 60 - ps.timeLeft) / 60);
        }
      });
    }
    result.push({ date: d, mins });
  }
  return result;
}

function getFocusByMonth(months) {
  const now = new Date();
  const result = [];
  for (let i = months - 1; i >= 0; i--) {
    const d   = new Date(now.getFullYear(), now.getMonth() - i, 1);
    const end = new Date(now.getFullYear(), now.getMonth() - i + 1, 1);
    const mins = (S.focusStats || []).filter(s => s.ts >= d.getTime() && s.ts < end.getTime()).reduce((a, s) => a + s.mins, 0);
    result.push({ date: d, mins });
  }
  return result;
}

// ── Clock widget ──

function renderClockWidget() {
  const el = document.getElementById('clockWidget');
  if (!el) return;
  el.style.display = S.clockEnabled ? '' : 'none';
}

function tickClock() {
  const timeEl = document.getElementById('clockTime');
  const dateEl = document.getElementById('clockDate');
  if (!timeEl) return;

  const now = new Date();
  const loc = S.locale || {};
  const use12 = loc.timeFormat === '12h';

  let h = now.getHours(), m = now.getMinutes();
  if (use12) {
    const ampm = h >= 12 ? 'PM' : 'AM';
    h = h % 12 || 12;
    timeEl.innerHTML = `${h}:${String(m).padStart(2,'0')}<span class="clock-ampm">${ampm}</span>`;
  } else {
    timeEl.textContent = `${String(h).padStart(2,'0')}:${String(m).padStart(2,'0')}`;
  }

  const DAY = T('clock.days');
  const MON = T('cal.monShort');
  const fmt = loc.dateFormat || 'DMY';
  const d = now.getDate(), mo = MON[now.getMonth()], wd = DAY[now.getDay()];
  let dateStr;
  if (fmt === 'MDY') dateStr = `${wd}, ${MON[now.getMonth()]} ${d}`;
  else               dateStr = `${wd}, ${d} ${mo}`;

  dateEl.textContent = dateStr;
}

let _clockInterval = null;
function startClock() {
  renderClockWidget();
  tickClock();
  const now = new Date();
  const msToNextMin = (60 - now.getSeconds()) * 1000 - now.getMilliseconds();
  setTimeout(() => {
    tickClock();
    _clockInterval = setInterval(tickClock, 60000);
  }, msToNextMin);
}

// ── Weather widget ──

function weatherIcon(code) {
  if (code === 0) return '☀️';
  if (code <= 2) return '🌤️';
  if (code <= 3) return '☁️';
  if (code <= 48) return '🌫️';
  if (code <= 57) return '🌦️';
  if (code <= 67) return '🌧️';
  if (code <= 77) return '❄️';
  if (code <= 82) return '🌦️';
  if (code <= 86) return '🌨️';
  return '⛈️';
}

function weatherDesc(code) {
  if (code === 0) return T('weather.condition.clearSky');
  if (code === 1) return T('weather.condition.mainlyClear');
  if (code === 2) return T('weather.condition.partlyCloudy');
  if (code === 3) return T('weather.condition.overcast');
  if (code <= 48) return T('weather.condition.fog');
  if (code <= 55) return T('weather.condition.drizzle');
  if (code <= 57) return T('weather.condition.freezingDrizzle');
  if (code <= 65) return T('weather.condition.rain');
  if (code <= 67) return T('weather.condition.freezingRain');
  if (code <= 75) return T('weather.condition.snow');
  if (code <= 77) return T('weather.condition.snowGrains');
  if (code <= 82) return T('weather.condition.rainShowers');
  if (code <= 86) return T('weather.condition.snowShowers');
  if (code === 95) return T('weather.condition.thunderstorm');
  return T('weather.condition.thunderstormHail');
}

function renderWeatherWidget() {
  const el = document.getElementById('weatherWidget');
  if (!el) return;
  const w = S.weather;
  if (!w?.enabled) { el.style.display = 'none'; return; }
  el.style.display = '';

  const c = w.cache || {};
  if (c.temp == null) {
    el.innerHTML = `<div class="focus-today-label">${T('widget.weather')}</div><div class="focus-today-value">-</div>`;
    return;
  }

  const isF = (S.locale?.tempUnit ?? w.units) === 'imperial';
  const toF = v => Math.round(v * 9 / 5 + 32);
  const temp = isF ? toF(Math.round(c.temp)) : Math.round(c.temp);
  const unit = isF ? '°F' : '°C';
  const label = c.name || T('widget.weather');

  el.innerHTML = `<div class="focus-today-label">${label}</div><div class="focus-today-value">${weatherIcon(c.code)} ${temp}${unit}</div>`;
}

async function fetchWeatherData(force) {
  const w = S.weather;
  if (!w?.enabled) return;

  const CACHE_MS = 30 * 60 * 1000;
  const c = w.cache || {};
  if (!force && c.temp != null && Date.now() - (c.ts || 0) < CACHE_MS) {
    renderWeatherWidget();
    return;
  }

  try {
    let lat = w.lat, lon = w.lon;

    if (!lat || !lon) {
      if (w.city) {
        const geoLang = (window.I18N && I18N.lang) || 'en';
        const geo = await fetch(
          `https://geocoding-api.open-meteo.com/v1/search?name=${encodeURIComponent(w.city)}&count=1&language=${geoLang}&format=json`
        ).then(r => r.json());
        if (geo.results?.length) {
          const r = geo.results[0];
          lat = w.lat = r.latitude;
          lon = w.lon = r.longitude;
          if (!w.cache) w.cache = {};
          w.cache.name = r.name;
        }
      }
    }

    if (lat == null || lon == null) { renderWeatherWidget(); return; }

    const data = await fetch(
      `https://api.open-meteo.com/v1/forecast?latitude=${lat}&longitude=${lon}&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m&wind_speed_unit=kmh&timezone=auto`
    ).then(r => r.json());

    const cur = data.current;
    if (!w.cache) w.cache = {};
    Object.assign(w.cache, {
      temp: cur.temperature_2m,
      feels: cur.apparent_temperature,
      code: cur.weather_code,
      wind: cur.wind_speed_10m,
      ts: Date.now(),
      name: w.cache.name || w.city
    });

    saveState();
    renderWeatherWidget();
  } catch (e) {
    renderWeatherWidget();
  }
}

function showWeatherPopup() {
  document.querySelector('.weather-popup')?.remove();
  const w = S.weather;
  const c = w?.cache || {};

  const popup = document.createElement('div');
  popup.className = 'focus-stats-popup weather-popup';

  const isF = (S.locale?.tempUnit ?? w.units) === 'imperial';
  const toF = v => Math.round(v * 9 / 5 + 32);
  const fmt = v => isF ? `${toF(Math.round(v))}°F` : `${Math.round(v)}°C`;
  const name = c.name || w.city || T('widget.weather');

  const hdr = document.createElement('div');
  hdr.className = 'focus-stats-popup-header';
  const titleEl = document.createElement('span');
  titleEl.className = 'focus-stats-popup-title';
  titleEl.textContent = name;
  const closeBtn = document.createElement('button');
  closeBtn.className = 'focus-stats-popup-close';
  closeBtn.textContent = '×';
  closeBtn.addEventListener('click', () => popup.remove());
  hdr.appendChild(titleEl); hdr.appendChild(closeBtn);
  popup.appendChild(hdr);

  const body = document.createElement('div');
  body.className = 'weather-popup-body';

  if (c.temp == null) {
    body.innerHTML = `<div class="weather-popup-desc">${T('weather.loading')}</div>`;
  } else {
    const main = document.createElement('div');
    main.className = 'weather-popup-main';
    main.innerHTML = `
      <span class="weather-popup-icon">${weatherIcon(c.code)}</span>
      <div>
        <div class="weather-popup-temp">${fmt(c.temp)}</div>
        <div class="weather-popup-desc">${weatherDesc(c.code)}</div>
      </div>`;
    body.appendChild(main);

    const meta = document.createElement('div');
    meta.className = 'weather-popup-meta';
    meta.innerHTML = `<span>${T('weather.feels', { v: fmt(c.feels) })}</span><span>${T('weather.wind', { v: Math.round(c.wind) })}</span>`;
    body.appendChild(meta);

    const updated = document.createElement('div');
    updated.className = 'weather-popup-updated';
    updated.textContent = c.ts ? T('weather.updated', { time: new Date(c.ts).toLocaleTimeString(I18N.lang, { hour: '2-digit', minute: '2-digit' }) }) : '';
    body.appendChild(updated);
  }

  const refreshBtn = document.createElement('button');
  refreshBtn.className = 'weather-popup-refresh';
  refreshBtn.textContent = T('weather.refresh');
  refreshBtn.addEventListener('click', () => {
    S.weather.lat = null; S.weather.lon = null;
    if (S.weather.cache) S.weather.cache.ts = 0;
    popup.remove();
    fetchWeatherData(true).then(() => showWeatherPopup());
  });
  body.appendChild(refreshBtn);

  popup.appendChild(body);
  document.body.appendChild(popup);

  const el = document.getElementById('weatherWidget');
  const rect = el.getBoundingClientRect();
  popup.style.top = (rect.bottom + 8) + 'px';
  popup.style.right = (window.innerWidth - rect.right) + 'px';

  _outsideClose(popup, el);
}

function updateFocusStats() {
  const el = document.getElementById('focusStats');
  if (!el) return;
  const hasPom = S.boards && S.boards.some(b => b.type === 'pomodoro');
  el.style.display = hasPom ? '' : 'none';
  if (!hasPom) return;

  const now = new Date();
  const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
  const completedMins = (S.focusStats || []).filter(s => s.ts >= todayStart).reduce((a, s) => a + s.mins, 0);

  // Include in-progress AND paused work time (any elapsed seconds in current work phase)
  let inProgressSecs = 0;
  Object.entries(_pomodoroState).forEach(([boardId, ps]) => {
    if (ps.phase === 'work') {
      const b = S.boards.find(b => b.id === boardId);
      if (b) {
        const st = getPomSettings(b);
        inProgressSecs += Math.max(0, st.focus * 60 - ps.timeLeft);
      }
    }
  });

  const totalMins = Math.floor((completedMins * 60 + inProgressSecs) / 60);
  const h = Math.floor(totalMins / 60);
  const m = totalMins % 60;
  let val;
  if (!totalMins)  val = '0m';
  else if (h)      val = `${h}h ${m}m`;
  else             val = `${m}m`;

  el.innerHTML = `<div class="focus-today-label">${T('focus.today')}</div><div class="focus-today-value">${val}</div>`;
}

function showFocusStatsPopup() {
  document.querySelector('.focus-stats-popup')?.remove();
  const popup = document.createElement('div');
  popup.className = 'focus-stats-popup';

  const hdr = document.createElement('div');
  hdr.className = 'focus-stats-popup-header';
  const title = document.createElement('span');
  title.className = 'focus-stats-popup-title';
  title.textContent = T('focus.stats');
  const closeBtn = document.createElement('button');
  closeBtn.className = 'focus-stats-popup-close';
  closeBtn.textContent = '×';
  closeBtn.addEventListener('click', () => popup.remove());
  hdr.appendChild(title); hdr.appendChild(closeBtn);
  popup.appendChild(hdr);

  const toggle = document.createElement('div');
  toggle.className = 'focus-stats-toggle';
  const weekBtn  = document.createElement('button');
  weekBtn.className  = 'focus-stats-toggle-btn active';
  weekBtn.textContent = T('focus.week');
  const monthBtn = document.createElement('button');
  monthBtn.className = 'focus-stats-toggle-btn';
  monthBtn.textContent = T('focus.months');
  toggle.appendChild(weekBtn); toggle.appendChild(monthBtn);
  popup.appendChild(toggle);

  const chartEl = document.createElement('div');
  chartEl.className = 'focus-chart';
  popup.appendChild(chartEl);

  let view = 'week';
  const DAY_SHORT = T('cal.dayShort');
  const MON_SHORT = T('cal.monShort');
  const MAX_BAR_H = 78;
  const now = new Date();

  function renderChart() {
    chartEl.innerHTML = '';
    const data = view === 'week' ? getFocusByDay(7) : getFocusByMonth(6);
    const totalMins = data.reduce((a, d) => a + d.mins, 0);

    if (!totalMins) {
      const empty = document.createElement('div');
      empty.className = 'focus-chart-empty';
      empty.textContent = T('focus.none');
      chartEl.appendChild(empty);
      return;
    }

    const maxMins = Math.max(...data.map(d => d.mins));
    data.forEach((item, i) => {
      const isNow = view === 'week'
        ? i === data.length - 1
        : item.date.getMonth() === now.getMonth() && item.date.getFullYear() === now.getFullYear();

      const col = document.createElement('div');
      col.className = 'focus-chart-col' + (isNow ? ' today' : '');

      const topLabel = document.createElement('div');
      topLabel.className = 'focus-chart-top-label';
      if (item.mins) {
        const h = Math.floor(item.mins / 60);
        topLabel.textContent = h ? `${h}h` : `${item.mins}m`;
      }

      const track = document.createElement('div');
      track.className = 'focus-chart-track';
      const bar = document.createElement('div');
      bar.className = 'focus-chart-bar';
      bar.style.height = (item.mins ? Math.max(3, Math.round((item.mins / maxMins) * MAX_BAR_H)) : 0) + 'px';
      track.appendChild(bar);

      const label = document.createElement('div');
      label.className = 'focus-chart-label';
      label.textContent = view === 'week' ? DAY_SHORT[item.date.getDay()] : MON_SHORT[item.date.getMonth()];

      col.appendChild(topLabel); col.appendChild(track); col.appendChild(label);
      chartEl.appendChild(col);
    });
  }

  weekBtn.addEventListener('click', () => {
    view = 'week'; weekBtn.classList.add('active'); monthBtn.classList.remove('active'); renderChart();
  });
  monthBtn.addEventListener('click', () => {
    view = 'month'; monthBtn.classList.add('active'); weekBtn.classList.remove('active'); renderChart();
  });

  renderChart();
  // Keep chart up-to-date while popup is open
  const chartRefresh = setInterval(() => { if (document.contains(popup)) renderChart(); else clearInterval(chartRefresh); }, 5000);
  closeBtn.addEventListener('click', () => clearInterval(chartRefresh));

  document.body.appendChild(popup);

  const statsEl = document.getElementById('focusStats');
  const rect = statsEl.getBoundingClientRect();
  popup.style.top   = (rect.bottom + 8) + 'px';
  popup.style.right = (window.innerWidth - rect.right) + 'px';

  _outsideClose(popup, document.getElementById('focusStats'));
}

function showPomodoroSettings(boardId, anchor) {
  document.querySelector('.pom-settings-popup')?.remove();
  const board = S.boards.find(b => b.id === boardId);
  if (!board) return;
  const s = getPomSettings(board);
  const popup = document.createElement('div');
  popup.className = 'bk-edit-popup pom-settings-popup';

  function row(label, key, val) {
    const wrap = document.createElement('div');
    wrap.className = 'pom-setting-row';
    const lbl = document.createElement('span');
    lbl.className = 'pom-setting-label';
    lbl.textContent = label;
    const inp = document.createElement('input');
    inp.type = 'number'; inp.min = 1; inp.max = 120;
    inp.className = 'add-link-input pom-setting-input';
    inp.value = val; inp.dataset.key = key;
    wrap.appendChild(lbl); wrap.appendChild(inp);
    popup.appendChild(wrap);
    return inp;
  }

  const focusInp = row(T('pom.focusMin'),  'focus', s.focus);
  const shortInp = row(T('pom.shortMin'),  'short', s.short);
  const longInp  = row(T('pom.longMin'),   'long',  s.long);
  const cycleInp = row(T('pom.longAfter'), 'cycle', s.cycle);

  _popupBtns(popup, () => popup.remove(), () => {
    const newS = {
      focus: Math.max(1, parseInt(focusInp.value)||25),
      short: Math.max(1, parseInt(shortInp.value)||5),
      long:  Math.max(1, parseInt(longInp.value)||15),
      cycle: Math.max(1, parseInt(cycleInp.value)||4)
    };
    board.pomSettings = newS;
    const ps = _pomodoroState[boardId];
    if (ps && !ps.running) {
      // Not running — reset to new focus time immediately
      ps.phase = 'work'; ps.viewPhase = 'work'; ps.timeLeft = newS.focus * 60;
    }
    // If running — current slot finishes with old duration, new settings apply next cycle
    saveState(); renderBoards();
    popup.remove();
  }, T('common.save'));

  _placePopup(popup, anchor);
  focusInp.focus(); focusInp.select();
  _outsideClose(popup);
}

function buildPomodoroBoard(board) {
  const el = document.createElement('div');
  el.className = 'board';
  el.dataset.id = board.id;
  const blurBg = document.createElement('div');
  blurBg.className = 'board-blur-bg';
  el.appendChild(blurBg);
  const accentBar = document.createElement('div');
  accentBar.className = 'board-accent-bar';
  el.appendChild(accentBar);

  const settings = getPomSettings(board);
  const phases = {
    work:  { label: T('pom.focus'), mins: settings.focus },
    short: { label: T('pom.short'), mins: settings.short },
    long:  { label: T('pom.long'),  mins: settings.long  }
  };
  const ps = getPomState(board.id, settings.focus);

  // ── Header ──
  const hdr = document.createElement('div');
  hdr.className = 'board-header';
  const titleEl = document.createElement('span');
  titleEl.className = 'board-title';
  titleEl.textContent = board.name || T('pom.title');
  const settingsBtn = document.createElement('button');
  settingsBtn.className = 'board-add-link-btn';
  settingsBtn.innerHTML = POM_GEAR;
  settingsBtn.title = T('side.settings');
  settingsBtn.addEventListener('click', e => { e.stopPropagation(); showPomodoroSettings(board.id, settingsBtn); });
  const menuBtn = document.createElement('button');
  menuBtn.className = 'board-menu-btn';
  menuBtn.textContent = '···';
  menuBtn.addEventListener('click', e => { e.stopPropagation(); showBoardMenu(board.id, menuBtn); });
  hdr.appendChild(titleEl); hdr.appendChild(settingsBtn); hdr.appendChild(menuBtn);
  el.appendChild(hdr);

  // ── Phase tabs ──
  const phasesEl = document.createElement('div');
  phasesEl.className = 'pom-phases';
  Object.entries(phases).forEach(([key, ph]) => {
    const btn = document.createElement('button');
    btn.className = 'pom-phase-btn' + (ps.viewPhase === key ? ' active' : '');
    btn.textContent = ph.label;
    btn.addEventListener('click', e => {
      e.stopPropagation();
      ps.viewPhase = key;
      renderBoards();
    });
    phasesEl.appendChild(btn);
  });
  el.appendChild(phasesEl);

  // ── Timer ──
  const timerEl = document.createElement('div');
  timerEl.className = 'pom-timer';
  // Show viewPhase time: if viewing a different phase than running, show that phase's full duration
  timerEl.textContent = pomFmt(ps.viewPhase === ps.phase ? ps.timeLeft : phases[ps.viewPhase].mins * 60);
  el.appendChild(timerEl);

  // ── Session dots ──
  const cycle = settings.cycle;
  const dotsEl = document.createElement('div');
  dotsEl.className = 'pom-dots';
  for (let i = 0; i < cycle; i++) {
    const dot = document.createElement('span');
    dot.className = 'pom-dot' + (i < ps.sessions % cycle ? ' active' : '');
    dotsEl.appendChild(dot);
  }
  el.appendChild(dotsEl);

  // ── Controls ──
  const ctrlEl = document.createElement('div');
  ctrlEl.className = 'pom-controls';

  const resetBtn = document.createElement('button');
  resetBtn.className = 'pom-ctrl-btn'; resetBtn.innerHTML = POM_RESET; resetBtn.title = T('tip.reset');

  const playBtn = document.createElement('button');
  playBtn.className = 'pom-ctrl-btn pom-play-btn';
  playBtn.innerHTML = ps.running ? POM_PAUSE : POM_PLAY;

  const skipBtn = document.createElement('button');
  skipBtn.className = 'pom-ctrl-btn'; skipBtn.innerHTML = POM_SKIP; skipBtn.title = T('tip.skip');

  function tick() {
    // Wall-clock based: accurate even when tab was inactive or page reloaded
    if (ps.startedAt != null) {
      const elapsed = Math.floor((Date.now() - ps.startedAt) / 1000);
      ps.timeLeft = Math.max(0, ps.startedTimeLeft - elapsed);
    } else {
      ps.timeLeft = Math.max(0, ps.timeLeft - 1);
    }
    if (ps.viewPhase === ps.phase) {
      const t = document.querySelector(`.board[data-id="${board.id}"] .pom-timer`);
      if (t) t.textContent = pomFmt(ps.timeLeft);
    }
    updateFocusStats();
    if (ps.timeLeft <= 0) {
      clearInterval(ps.interval); ps.interval = null; ps.running = false;
      ps.startedAt = null; ps.startedTimeLeft = null;
      playPomSound('end');
      if (ps.phase === 'work') {
        S.focusStats.push({ ts: Date.now(), mins: settings.focus });
        ps.sessions++;
        ps.phase = ps.sessions % cycle === 0 ? 'long' : 'short';
      } else ps.phase = 'work';
      ps.viewPhase = ps.phase;
      ps.timeLeft = phases[ps.phase].mins * 60;
      savePomTimer(board.id);
      renderBoards();
    }
  }

  // Auto-restart interval if timer was running when page was closed/refreshed
  if (ps.running && !ps.interval) {
    ps.interval = setInterval(tick, 1000);
  }

  playBtn.addEventListener('click', e => {
    e.stopPropagation();
    if (ps.viewPhase !== ps.phase) {
      if (ps.interval) { clearInterval(ps.interval); ps.interval = null; }
      ps.phase = ps.viewPhase;
      ps.timeLeft = phases[ps.phase].mins * 60;
      ps.running = true;
      ps.startedAt = Date.now(); ps.startedTimeLeft = ps.timeLeft;
      ps.interval = setInterval(tick, 1000);
      playBtn.innerHTML = POM_PAUSE;
      playPomSound('start');
    } else if (ps.running) {
      clearInterval(ps.interval); ps.interval = null; ps.running = false;
      ps.startedAt = null; ps.startedTimeLeft = null;
      playBtn.innerHTML = POM_PLAY;
      updateFocusStats();
    } else {
      ps.running = true;
      ps.startedAt = Date.now(); ps.startedTimeLeft = ps.timeLeft;
      ps.interval = setInterval(tick, 1000);
      playBtn.innerHTML = POM_PAUSE;
      playPomSound('start');
    }
    savePomTimer(board.id);
  });

  resetBtn.addEventListener('click', e => {
    e.stopPropagation();
    if (ps.interval) { clearInterval(ps.interval); ps.interval = null; }

    // Сохраняем прошедшее рабочее время в статистику перед сбросом
    if (ps.phase === 'work') {
      const fullSecs = settings.focus * 60;
      const elapsedSecs = fullSecs - ps.timeLeft;
      if (elapsedSecs >= 60) {
        if (!S.focusStats) S.focusStats = [];
        S.focusStats.push({ ts: Date.now(), mins: Math.floor(elapsedSecs / 60) });
      }
    }

    ps.running = false; ps.startedAt = null; ps.startedTimeLeft = null;
    ps.phase = ps.viewPhase;
    ps.timeLeft = phases[ps.viewPhase].mins * 60;
    timerEl.textContent = pomFmt(ps.timeLeft);
    playBtn.innerHTML = POM_PLAY;
    savePomTimer(board.id);
    updateFocusStats();
  });

  skipBtn.addEventListener('click', e => {
    e.stopPropagation();
    if (ps.interval) { clearInterval(ps.interval); ps.interval = null; }
    ps.startedAt = null; ps.startedTimeLeft = null;
    if (ps.phase === 'work') {
      const elapsedMins = Math.max(1, Math.round((settings.focus * 60 - ps.timeLeft) / 60));
      S.focusStats.push({ ts: Date.now(), mins: elapsedMins });
      ps.sessions++;
      ps.phase = ps.sessions % cycle === 0 ? 'long' : 'short';
    } else ps.phase = 'work';
    ps.viewPhase = ps.phase;
    ps.timeLeft = phases[ps.phase].mins * 60; ps.running = false;
    savePomTimer(board.id);
    renderBoards();
  });

  ctrlEl.appendChild(resetBtn); ctrlEl.appendChild(playBtn); ctrlEl.appendChild(skipBtn);
  el.appendChild(ctrlEl);

  // ── Drag ──
  el.addEventListener('mousedown', e => { if (e.target.closest('button')) return; el.draggable = true; });
  el.addEventListener('dragstart', e => {
    _dragId = board.id; e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', board.id);
    setTimeout(() => el.classList.add('is-dragging'), 0);
    activateColDropZones(board.col);
  });
  el.addEventListener('dragend', () => {
    el.draggable = false; el.classList.remove('is-dragging');
    document.querySelectorAll('.board.drop-before,.board.drop-after').forEach(b => b.classList.remove('drop-before','drop-after'));
    _dropTarget = null; deactivateColDropZones(); if (_dragId) { _dragId = null; renderBoards(); }
  });
  applyBoardStyle(el, board);
  el.addEventListener('dragover', e => {
    if (!_dragId || _dragId === board.id) return;
    e.preventDefault(); e.stopPropagation();
    const before = e.clientY < el.getBoundingClientRect().top + el.offsetHeight / 2;
    document.querySelectorAll('.board.drop-before,.board.drop-after').forEach(b => b.classList.remove('drop-before','drop-after'));
    el.classList.add(before ? 'drop-before' : 'drop-after');
    _dropTarget = { id: board.id, before };
  });
  el.addEventListener('dragleave', e => {
    if (_dragId && !el.contains(e.relatedTarget)) el.classList.remove('drop-before','drop-after');
  });
  el.addEventListener('drop', e => {
    if (!_dragId || _dragId === board.id) return;
    e.preventDefault(); e.stopPropagation();
    el.classList.remove('drop-before','drop-after');
    if (_dropTarget) insertBoardAt(_dragId, _dropTarget.id, _dropTarget.before);
    _dragId = null; _dropTarget = null;
  });

  return el;
}

function addBoard() {
  const { col, row } = findFreePosition();
  addBoardAt(col, row);
}

function addCalendar() {
  const { col, row } = findFreePosition();
  const board = { id: genId(), pageId: S.activePage, name: T('widget.calendar'), type: 'calendar', col, row };
  const now = new Date();
  _calendarState[board.id] = { year: now.getFullYear(), month: now.getMonth() };
  S.boards.push(board);
  saveState(); renderBoards();
}

const SEARCH_ENGINES = [
  { id: 'default', name: T('search.defaultEngine'), url: null,                                   domain: null             },
  { id: 'google',  name: 'Google',          url: 'https://www.google.com/search?q=',             domain: 'google.com'     },
  { id: 'yandex',  name: 'Yandex',          url: 'https://yandex.ru/search/?text=',              domain: 'yandex.ru'      },
  { id: 'bing',    name: 'Bing',            url: 'https://www.bing.com/search?q=',               domain: 'bing.com'       },
  { id: 'ddg',     name: 'DuckDuckGo',      url: 'https://duckduckgo.com/?q=',                   domain: 'duckduckgo.com' },
  { id: 'youtube', name: 'YouTube',         url: 'https://www.youtube.com/results?search_query=',domain: 'youtube.com'    },
  { id: 'ecosia',  name: 'Ecosia',          url: 'https://www.ecosia.org/search?q=',             domain: 'ecosia.org'     },
];

function nsbFaviconUrl(domain) {
  return `https://www.google.com/s2/favicons?domain=${domain}&sz=32`;
}

// Returns img or span element for engine icon
function nsbEngineIcon(eng, size) {
  size = size || 16;
  if (eng.id === 'default') {
    const wrap = document.createElement('span');
    wrap.style.cssText = `width:${size}px;height:${size}px;display:flex;align-items:center;justify-content:center;flex-shrink:0;opacity:0.6;`;
    wrap.innerHTML = `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><path d="M2 12h20"/><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"/></svg>`;
    return wrap;
  }
  const img = document.createElement('img');
  img.src = nsbFaviconUrl(eng.domain);
  img.width = size;
  img.height = size;
  img.style.cssText = `border-radius:${size <= 16 ? 3 : 4}px;display:block;flex-shrink:0;`;
  return img;
}

function nsbDoSearch(query) {
  const eng = SEARCH_ENGINES.find(e => e.id === (S.navSearchEngine || 'google')) || SEARCH_ENGINES[1];
  if (eng.id === 'default') {
    if (typeof chrome !== 'undefined' && chrome.search?.query) {
      chrome.search.query({ text: query, disposition: 'NEW_TAB' });
    } else {
      chrome.tabs.create({ url: 'https://www.google.com/search?q=' + encodeURIComponent(query) });
    }
  } else {
    chrome.tabs.create({ url: eng.url + encodeURIComponent(query) });
  }
}

function addSearch() {
  const { col, row } = findFreePosition();
  const board = { id: genId(), pageId: S.activePage, name: T('widget.search'), type: 'search', col, row, searchEngine: 'google' };
  S.boards.push(board);
  saveState(); renderBoards();
  setTimeout(() => {
    const el = document.querySelector(`.board[data-id="${board.id}"] .search-widget-input`);
    if (el) el.focus();
  }, 60);
}

function buildSearchBoard(board) {
  const el = document.createElement('div');
  el.className = 'board';
  el.dataset.id = board.id;
  const blurBg = document.createElement('div');
  blurBg.className = 'board-blur-bg';
  el.appendChild(blurBg);
  const accentBar = document.createElement('div');
  accentBar.className = 'board-accent-bar';
  el.appendChild(accentBar);

  const hdr = document.createElement('div');
  hdr.className = 'board-header';
  const title = document.createElement('span');
  title.className = 'board-title';
  title.textContent = board.name;
  title.addEventListener('dblclick', () => startBoardRename(board.id, title));
  const menuBtn = document.createElement('button');
  menuBtn.className = 'board-menu-btn';
  menuBtn.textContent = '···';
  menuBtn.addEventListener('click', e => { e.stopPropagation(); showBoardMenu(board.id, menuBtn); });
  hdr.appendChild(title);
  hdr.appendChild(menuBtn);
  el.appendChild(hdr);

  // Search input row
  const inputWrap = document.createElement('div');
  inputWrap.className = 'search-widget-wrap';
  inputWrap.innerHTML = `<svg class="search-widget-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>`;
  const input = document.createElement('input');
  input.className = 'search-widget-input';
  input.type = 'text';
  input.placeholder = T('search.widgetPlaceholder');
  input.addEventListener('mousedown', e => e.stopPropagation());
  input.addEventListener('keydown', e => {
    if (e.key === 'Enter' && input.value.trim()) {
      const engine = SEARCH_ENGINES.find(en => en.id === (board.searchEngine || 'google')) || SEARCH_ENGINES[1];
      chrome.tabs.create({ url: engine.url + encodeURIComponent(input.value.trim()) });
      input.value = '';
    }
  });
  inputWrap.appendChild(input);
  // Clicking the icon or the padding/gap around it should still focus the input.
  inputWrap.addEventListener('mousedown', e => {
    if (e.target === input) return;
    e.preventDefault();
    e.stopPropagation();
    input.focus();
  });
  el.appendChild(inputWrap);

  // Engine selector (no 'default' option — board widget uses explicit URLs)
  const engines = document.createElement('div');
  engines.className = 'search-widget-engines';
  SEARCH_ENGINES.filter(e => e.id !== 'default').forEach(eng => {
    const btn = document.createElement('button');
    btn.className = 'search-engine-btn' + (eng.id === (board.searchEngine || 'google') ? ' active' : '');
    btn.textContent = eng.name;
    btn.title = eng.name;
    btn.addEventListener('click', e => {
      e.stopPropagation();
      board.searchEngine = eng.id;
      saveState();
      engines.querySelectorAll('.search-engine-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
    });
    engines.appendChild(btn);
  });
  el.appendChild(engines);

  // Drag
  el.addEventListener('mousedown', e => {
    if (e.target.closest('button') || e.target.tagName === 'INPUT') return;
    el.draggable = true;
  });
  el.addEventListener('dragstart', e => {
    _dragId = board.id; e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', board.id);
    setTimeout(() => el.classList.add('is-dragging'), 0);
    activateColDropZones(board.col);
  });
  el.addEventListener('dragend', () => {
    el.draggable = false; el.classList.remove('is-dragging');
    document.querySelectorAll('.board.drop-before,.board.drop-after')
      .forEach(b => b.classList.remove('drop-before','drop-after'));
    _dropTarget = null; deactivateColDropZones();
    if (_dragId) { _dragId = null; renderBoards(); }
  });
  el.addEventListener('dragover', e => {
    if (!_dragId || _dragId === board.id) return;
    e.preventDefault(); e.stopPropagation();
    const before = e.clientY < el.getBoundingClientRect().top + el.offsetHeight / 2;
    document.querySelectorAll('.board.drop-before,.board.drop-after')
      .forEach(b => b.classList.remove('drop-before','drop-after'));
    el.classList.add(before ? 'drop-before' : 'drop-after');
    _dropTarget = { id: board.id, before };
  });
  el.addEventListener('dragleave', e => {
    if (_dragId && !el.contains(e.relatedTarget)) el.classList.remove('drop-before','drop-after');
  });
  el.addEventListener('drop', e => {
    if (!_dragId || _dragId === board.id) return;
    e.preventDefault(); e.stopPropagation();
    el.classList.remove('drop-before','drop-after');
    if (_dropTarget) insertBoardAt(_dragId, _dropTarget.id, _dropTarget.before);
    _dragId = null; _dropTarget = null;
  });

  applyBoardStyle(el, board);
  return el;
}

function addNotes() {
  const { col, row } = findFreePosition();
  const board = { id: genId(), pageId: S.activePage, name: T('widget.notes'), type: 'notes', col, row, noteContent: '' };
  S.boards.push(board);
  saveState(); renderBoards();
  setTimeout(() => {
    const el = document.querySelector(`.board[data-id="${board.id}"] .notes-textarea`);
    if (el) el.focus();
  }, 60);
}

function buildNotesBoard(board) {
  const el = document.createElement('div');
  el.className = 'board';
  el.dataset.id = board.id;
  const blurBg = document.createElement('div');
  blurBg.className = 'board-blur-bg';
  el.appendChild(blurBg);
  const accentBar = document.createElement('div');
  accentBar.className = 'board-accent-bar';
  el.appendChild(accentBar);

  const hdr = document.createElement('div');
  hdr.className = 'board-header';

  const title = document.createElement('span');
  title.className = 'board-title';
  title.textContent = board.name;
  title.addEventListener('dblclick', () => startBoardRename(board.id, title));

  const menuBtn = document.createElement('button');
  menuBtn.className = 'board-menu-btn';
  menuBtn.textContent = '···';
  menuBtn.addEventListener('click', e => { e.stopPropagation(); showBoardMenu(board.id, menuBtn); });

  hdr.appendChild(title);
  hdr.appendChild(menuBtn);
  el.appendChild(hdr);

  const textarea = document.createElement('textarea');
  textarea.className = 'notes-textarea';
  textarea.placeholder = T('notes.placeholder');
  textarea.value = board.noteContent || '';
  textarea.spellcheck = false;
  if (board.noteHeight) textarea.style.height = board.noteHeight + 'px';

  let _saveTimer;
  textarea.addEventListener('input', () => {
    board.noteContent = textarea.value;
    clearTimeout(_saveTimer);
    _saveTimer = setTimeout(() => saveState(), 600);
  });
  textarea.addEventListener('mousedown', e => e.stopPropagation());
  textarea.addEventListener('dragstart', e => e.preventDefault());

  el.appendChild(textarea);

  const resizeHandle = document.createElement('div');
  resizeHandle.className = 'notes-resize-handle';
  resizeHandle.innerHTML = `<svg width="12" height="12" viewBox="0 0 12 12" fill="none" xmlns="http://www.w3.org/2000/svg">
    <circle cx="10" cy="6" r="1.2" fill="currentColor"/>
    <circle cx="10" cy="10" r="1.2" fill="currentColor"/>
    <circle cx="6" cy="10" r="1.2" fill="currentColor"/>
  </svg>`;
  resizeHandle.addEventListener('mousedown', e => {
    e.preventDefault(); e.stopPropagation();
    const startY = e.clientY;
    const startH = textarea.offsetHeight;
    function onMove(ev) {
      const h = Math.max(60, startH + (ev.clientY - startY));
      textarea.style.height = h + 'px';
      board.noteHeight = h;
    }
    function onUp() {
      document.removeEventListener('mousemove', onMove);
      document.removeEventListener('mouseup', onUp);
      saveState();
    }
    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onUp);
  });
  el.appendChild(resizeHandle);

  el.addEventListener('mousedown', e => {
    if (e.target.closest('button') || e.target.tagName === 'TEXTAREA') return;
    el.draggable = true;
  });
  el.addEventListener('dragstart', e => {
    _dragId = board.id; e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', board.id);
    setTimeout(() => el.classList.add('is-dragging'), 0);
    activateColDropZones(board.col);
  });
  el.addEventListener('dragend', () => {
    el.draggable = false; el.classList.remove('is-dragging');
    document.querySelectorAll('.board.drop-before,.board.drop-after')
      .forEach(b => b.classList.remove('drop-before','drop-after'));
    _dropTarget = null; deactivateColDropZones();
    if (_dragId) { _dragId = null; document.activeElement?.blur(); renderBoards(); }
  });
  el.addEventListener('dragover', e => {
    if (!_dragId || _dragId === board.id) return;
    e.preventDefault(); e.stopPropagation();
    const before = e.clientY < el.getBoundingClientRect().top + el.offsetHeight / 2;
    document.querySelectorAll('.board.drop-before,.board.drop-after')
      .forEach(b => b.classList.remove('drop-before','drop-after'));
    el.classList.add(before ? 'drop-before' : 'drop-after');
    _dropTarget = { id: board.id, before };
  });
  el.addEventListener('dragleave', e => {
    if (_dragId && !el.contains(e.relatedTarget)) el.classList.remove('drop-before','drop-after');
  });
  el.addEventListener('drop', e => {
    if (!_dragId || _dragId === board.id) return;
    e.preventDefault(); e.stopPropagation();
    el.classList.remove('drop-before','drop-after');
    if (_dropTarget) insertBoardAt(_dragId, _dropTarget.id, _dropTarget.before);
    _dragId = null; _dropTarget = null;
  });

  applyBoardStyle(el, board);
  return el;
}

const CAL_MONTHS = (window.I18N ? I18N.t('cal.months') : ['January','February','March','April','May','June','July','August','September','October','November','December']);

function buildCalendarBoard(board) {
  const el = document.createElement('div');
  el.className = 'board';
  el.dataset.id = board.id;
  const blurBg = document.createElement('div');
  blurBg.className = 'board-blur-bg';
  el.appendChild(blurBg);
  const accentBar = document.createElement('div');
  accentBar.className = 'board-accent-bar';
  el.appendChild(accentBar);

  if (!_calendarState[board.id]) {
    const now = new Date();
    _calendarState[board.id] = { year: now.getFullYear(), month: now.getMonth() };
  }
  const cs = _calendarState[board.id];

  // ── Header ──
  const hdr = document.createElement('div');
  hdr.className = 'board-header';

  const prevBtn = document.createElement('button');
  prevBtn.className = 'cal-nav-btn';
  prevBtn.innerHTML = `<svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="15 18 9 12 15 6"/></svg>`;
  prevBtn.addEventListener('click', e => {
    e.stopPropagation();
    cs.month--; if (cs.month < 0) { cs.month = 11; cs.year--; }
    renderBoards();
  });

  const titleEl = document.createElement('span');
  titleEl.className = 'board-title';
  titleEl.textContent = CAL_MONTHS[cs.month] + ' ' + cs.year;

  const nextBtn = document.createElement('button');
  nextBtn.className = 'cal-nav-btn';
  nextBtn.innerHTML = `<svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="9 18 15 12 9 6"/></svg>`;
  nextBtn.addEventListener('click', e => {
    e.stopPropagation();
    cs.month++; if (cs.month > 11) { cs.month = 0; cs.year++; }
    renderBoards();
  });

  const menuBtn = document.createElement('button');
  menuBtn.className = 'board-menu-btn';
  menuBtn.textContent = '···';
  menuBtn.addEventListener('click', e => { e.stopPropagation(); showBoardMenu(board.id, menuBtn); });

  hdr.appendChild(prevBtn);
  hdr.appendChild(titleEl);
  hdr.appendChild(nextBtn);
  hdr.appendChild(menuBtn);
  el.appendChild(hdr);

  // ── Day names row ──
  const _wkStart = S.locale?.weekStart ?? 1; // 0=Sun, 1=Mon
  const _ds = T('cal.dayShort'); // Sunday-first
  const CAL_DAYS = _wkStart === 0 ? _ds.slice() : [..._ds.slice(1), _ds[0]];
  const daysRow = document.createElement('div');
  daysRow.className = 'cal-days-row';
  CAL_DAYS.forEach(d => {
    const s = document.createElement('span');
    s.className = 'cal-day-name';
    s.textContent = d;
    daysRow.appendChild(s);
  });
  el.appendChild(daysRow);

  // ── Day grid ──
  const grid = document.createElement('div');
  grid.className = 'cal-grid';
  const today = new Date();
  const firstDow = new Date(cs.year, cs.month, 1).getDay(); // 0=Sun
  const startOffset = _wkStart === 0 ? firstDow : (firstDow === 0 ? 6 : firstDow - 1);
  const daysInMonth = new Date(cs.year, cs.month + 1, 0).getDate();

  for (let i = 0; i < startOffset; i++) {
    const blank = document.createElement('span');
    blank.className = 'cal-day cal-day-blank';
    grid.appendChild(blank);
  }
  for (let d = 1; d <= daysInMonth; d++) {
    const cell = document.createElement('span');
    cell.className = 'cal-day';
    const dow = (startOffset + d - 1) % 7;
    const isWeekend = _wkStart === 0 ? (dow === 0 || dow === 6) : dow >= 5;
    if (isWeekend) cell.classList.add('cal-day-weekend');
    if (d === today.getDate() && cs.month === today.getMonth() && cs.year === today.getFullYear())
      cell.classList.add('cal-day-today');
    const numEl = document.createElement('span');
    numEl.className = 'cal-day-num';
    numEl.textContent = d;
    cell.appendChild(numEl);
    const dayEvents = (cs.events || []).filter(ev => ev.date === d);
    if (dayEvents.length) {
      const dots = document.createElement('span');
      dots.className = 'cal-event-dots';
      dayEvents.slice(0, 3).forEach(ev => {
        const dot = document.createElement('span');
        dot.className = 'cal-event-dot';
        if (ev.color) dot.style.background = ev.color;
        dots.appendChild(dot);
      });
      cell.appendChild(dots);
    }
    grid.appendChild(cell);
  }
  el.appendChild(grid);

  // ── Drag (same as buildBoard) ──
  el.addEventListener('mousedown', e => { if (e.target.closest('button')) return; el.draggable = true; });
  el.addEventListener('dragstart', e => {
    _dragId = board.id; e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setData('text/plain', board.id);
    setTimeout(() => el.classList.add('is-dragging'), 0);
    activateColDropZones(board.col);
  });
  el.addEventListener('dragend', () => {
    el.draggable = false; el.classList.remove('is-dragging');
    document.querySelectorAll('.board.drop-before,.board.drop-after')
      .forEach(b => b.classList.remove('drop-before','drop-after'));
    _dropTarget = null; deactivateColDropZones();
    if (_dragId) { _dragId = null; renderBoards(); }
  });
  el.addEventListener('dragover', e => {
    if (!_dragId || _dragId === board.id) return;
    e.preventDefault(); e.stopPropagation();
    const before = e.clientY < el.getBoundingClientRect().top + el.offsetHeight / 2;
    document.querySelectorAll('.board.drop-before,.board.drop-after')
      .forEach(b => b.classList.remove('drop-before','drop-after'));
    el.classList.add(before ? 'drop-before' : 'drop-after');
    _dropTarget = { id: board.id, before };
  });
  el.addEventListener('dragleave', e => {
    if (_dragId && !el.contains(e.relatedTarget)) el.classList.remove('drop-before','drop-after');
  });
  el.addEventListener('drop', e => {
    if (!_dragId || _dragId === board.id) return;
    e.preventDefault(); e.stopPropagation();
    el.classList.remove('drop-before','drop-after');
    if (_dropTarget) insertBoardAt(_dragId, _dropTarget.id, _dropTarget.before);
    _dragId = null; _dropTarget = null;
  });

  applyBoardStyle(el, board);
  return el;
}

function deleteBoard(boardId) {
  const board = S.boards.find(b => b.id === boardId);
  if (!board) return;
  if (_pomodoroState[boardId]?.interval) { clearInterval(_pomodoroState[boardId].interval); }
  delete _pomodoroState[boardId];
  const isWidget = board.type === 'calendar' || board.type === 'pomodoro' || board.type === 'notes' || board.type === 'search';
  const boardBkIds = S.bookmarks.filter(bk => bk.boardId === boardId).map(bk => bk.id);
  const hasLinks = boardBkIds.length > 0;
  if (!isWidget && hasLinks) {
    const now = Date.now();
    S.trash.boards.push({ ...board, deletedAt: now });
    S.bookmarks.filter(bk => bk.boardId === boardId).forEach(bk =>
      S.trash.bookmarks.push({ ...bk, deletedAt: now })
    );
  }
  S.boards = S.boards.filter(b => b.id !== boardId);
  S.bookmarks = S.bookmarks.filter(bk => bk.boardId !== boardId);
  tombstoneMain([boardId, ...boardBkIds]);
  saveState(); renderBoards();
}

function addBookmark(boardId, url, title, description) {
  const count = S.bookmarks.filter(bk => bk.boardId === boardId).length;
  const bk = { id: genId(), boardId, url, title, order: count };
  if (description) bk.description = description;
  S.bookmarks.push(bk);
  saveState(); renderBoards();
}

function deleteBookmark(bkId) {
  const bk = S.bookmarks.find(b => b.id === bkId);
  if (!bk) return;
  S.trash.bookmarks.push({ ...bk, deletedAt: Date.now() });
  S.bookmarks = S.bookmarks.filter(b => b.id !== bkId);
  tombstoneMain([bkId]);
  saveState(); renderBoards();
}


document.getElementById('addBoardFab').addEventListener('click', addBoard);
document.getElementById('focusStats').addEventListener('click', () => {
  const existing = document.querySelector('.focus-stats-popup');
  if (existing) existing.remove();
  else showFocusStatsPopup();
});

const sidebar = document.getElementById('sidebar');
function openSidebar() { sidebar.classList.add('is-open'); }
function closeSidebar() { sidebar.classList.remove('is-open'); }
function closeAll() {
  closeSidebar();
  document.getElementById('widgetGallery').classList.remove('open');
}

document.getElementById('menuSideBtn').addEventListener('click', e => {
  e.stopPropagation();
  sidebar.classList.contains('is-open') ? closeSidebar() : openSidebar();
});
document.addEventListener('click', e => {
  if (sidebar.classList.contains('is-open') && !sidebar.contains(e.target)) closeSidebar();
});

document.getElementById('searchSideBtn').addEventListener('click', () => { closeSidebar(); openSearch(); });
document.getElementById('mpWallpaper').addEventListener('click', () => {
  closeSidebar(); openWallpaperModal();
});

document.getElementById('mpWidgets').addEventListener('click', e => {
  e.stopPropagation();
  const gallery = document.getElementById('widgetGallery');
  const opening = !gallery.classList.contains('open');
  gallery.classList.toggle('open', opening);
});
document.getElementById('mpImport').addEventListener('click', () => {
  closeSidebar(); openImportModal();
});
function exportUserData() {
  const blob = new Blob([JSON.stringify(S, null, 2)], { type: 'application/json' });
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = 'newtab-data.json';
  a.click();
  URL.revokeObjectURL(a.href);
}
document.getElementById('mpTrash').addEventListener('click', () => { closeSidebar(); openTrash(); });

document.addEventListener('click', e => {
  const wg = document.getElementById('widgetGallery');
  if (wg.classList.contains('open') && !wg.contains(e.target) && !document.getElementById('mpWidgets').contains(e.target))
    wg.classList.remove('open');
});
document.getElementById('wcBoard').querySelector('.widget-add-btn').addEventListener('click', () => {
  addBoard();
  document.getElementById('widgetGallery').classList.remove('open');
});

document.getElementById('wcNotes').querySelector('.widget-add-btn').addEventListener('click', () => {
  addNotes();
  document.getElementById('widgetGallery').classList.remove('open');
});
document.getElementById('wcCalendar').querySelector('.widget-add-btn').addEventListener('click', () => {
  addCalendar();
  document.getElementById('widgetGallery').classList.remove('open');
});
document.getElementById('wcPomodoro').querySelector('.widget-add-btn').addEventListener('click', () => {
  addPomodoro();
  document.getElementById('widgetGallery').classList.remove('open');
});

// ── Weather card in widget gallery ──
function syncWeatherCard() {
  const toggle = document.getElementById('weatherToggle');
  const config = document.getElementById('weatherCardConfig');
  const cityInput = document.getElementById('weatherCityInput');
  if (!toggle) return;
  const enabled = !!S.weather?.enabled;
  toggle.classList.toggle('on', enabled);
  config.style.display = enabled ? '' : 'none';
  cityInput.value = S.weather?.city || '';
  _wcHideSuggest();
}

document.getElementById('weatherToggle').addEventListener('click', () => {
  S.weather.enabled = !S.weather.enabled;
  saveState();
  syncWeatherCard();
  renderWeatherWidget();
  if (S.weather.enabled && S.weather.cache?.temp == null) fetchWeatherData();
});

document.getElementById('weatherCityApply').addEventListener('click', () => {
  const v = document.getElementById('weatherCityInput').value.trim();
  if (!v) return;
  _wcHideSuggest();
  S.weather.city = v;
  S.weather.lat = null; S.weather.lon = null;
  if (S.weather.cache) S.weather.cache.ts = 0;
  saveState();
  fetchWeatherData(true);
});

// ── City autocomplete ──
// As the user types we query Open-Meteo geocoding (up to 5 hits) in the current
// UI language, so Cyrillic / any-language names resolve. Picking a suggestion
// stores lat/lon directly, so we never depend on re-geocoding a raw string.
let _wcTimer = null;
let _wcSeq = 0;      // guards against out-of-order responses
let _wcResults = []; // current suggestions
let _wcActive = -1;  // highlighted index for keyboard nav

function _wcHideSuggest() {
  const box = document.getElementById('weatherCitySuggest');
  if (box) { box.style.display = 'none'; box.replaceChildren(); }
  _wcResults = []; _wcActive = -1;
}

function _wcRenderSuggest(results) {
  const box = document.getElementById('weatherCitySuggest');
  if (!box) return;
  _wcResults = results; _wcActive = -1;
  box.replaceChildren();
  if (!results.length) { box.style.display = 'none'; return; }
  results.forEach((r, i) => {
    const li = document.createElement('li');
    li.textContent = r.name;
    const parts = [r.admin1, r.country].filter(Boolean).join(', ');
    if (parts) {
      const sub = document.createElement('span');
      sub.className = 'wcs-sub';
      sub.textContent = '  ' + parts;
      li.appendChild(sub);
    }
    // mousedown (not click) so it fires before the input's blur hides the list.
    li.addEventListener('mousedown', e => { e.preventDefault(); _wcPick(i); });
    box.appendChild(li);
  });
  box.style.display = '';
}

function _wcHighlight(idx) {
  const box = document.getElementById('weatherCitySuggest');
  if (!box) return;
  const items = box.querySelectorAll('li');
  if (!items.length) return;
  _wcActive = (idx + items.length) % items.length;
  items.forEach((li, i) => li.classList.toggle('active', i === _wcActive));
}

function _wcPick(i) {
  const r = _wcResults[i];
  if (!r) return;
  document.getElementById('weatherCityInput').value = r.name;
  S.weather.city = r.name;
  S.weather.lat = r.latitude;
  S.weather.lon = r.longitude;
  if (!S.weather.cache) S.weather.cache = {};
  S.weather.cache.name = r.name;
  S.weather.cache.ts = 0;
  _wcHideSuggest();
  saveState();
  fetchWeatherData(true);
}

async function _wcQuery(q) {
  const seq = ++_wcSeq;
  const lang = (window.I18N && I18N.lang) || 'en';
  let results = [];
  try {
    const geo = await fetch(
      `https://geocoding-api.open-meteo.com/v1/search?name=${encodeURIComponent(q)}&count=5&language=${lang}&format=json`
    ).then(r => r.json());
    results = (geo.results || []).slice(0, 5);
  } catch (e) { results = []; }
  if (seq !== _wcSeq) return; // a newer keystroke already fired
  _wcRenderSuggest(results);
}

document.getElementById('weatherCityInput').addEventListener('input', e => {
  const q = e.target.value.trim();
  clearTimeout(_wcTimer);
  if (q.length < 2) { _wcHideSuggest(); return; }
  _wcTimer = setTimeout(() => _wcQuery(q), 250);
});

document.getElementById('weatherCityInput').addEventListener('keydown', e => {
  const open = _wcResults.length > 0;
  if (e.key === 'ArrowDown' && open) { e.preventDefault(); _wcHighlight(_wcActive + 1); }
  else if (e.key === 'ArrowUp' && open) { e.preventDefault(); _wcHighlight(_wcActive - 1); }
  else if (e.key === 'Escape') { _wcHideSuggest(); }
  else if (e.key === 'Enter') {
    if (open && _wcActive >= 0) { e.preventDefault(); _wcPick(_wcActive); }
    else document.getElementById('weatherCityApply').click();
  }
});

document.getElementById('weatherCityInput').addEventListener('blur', () => {
  setTimeout(_wcHideSuggest, 120); // let a suggestion mousedown land first
});

document.getElementById('mpWidgets').addEventListener('click', syncWeatherCard, { capture: true });

// ── Nav search toggle in widget gallery ──
function syncNavSearchCard() {
  const toggle = document.getElementById('navSearchToggle');
  if (!toggle) return;
  toggle.classList.toggle('on', !!S.navSearchEnabled);
}

document.getElementById('navSearchToggle').addEventListener('click', () => {
  S.navSearchEnabled = !S.navSearchEnabled;
  saveState();
  syncNavSearchCard();
  renderNavSearch();
  requestAnimationFrame(syncLayout);
});

document.getElementById('mpWidgets').addEventListener('click', syncNavSearchCard, { capture: true });

// ── Clock toggle in widget gallery ──
function syncClockCard() {
  const toggle = document.getElementById('clockToggle');
  if (!toggle) return;
  toggle.classList.toggle('on', !!S.clockEnabled);
}

document.getElementById('clockToggle').addEventListener('click', () => {
  S.clockEnabled = !S.clockEnabled;
  saveState();
  syncClockCard();
  renderClockWidget();
});

document.getElementById('mpWidgets').addEventListener('click', syncClockCard, { capture: true });

// ── Search ──
let _searchIdx = -1;

function openSearch() {
  document.getElementById('searchOverlay').classList.add('open');
  const inp = document.getElementById('searchInput');
  inp.value = '';
  document.getElementById('searchResults').innerHTML = '';
  _searchIdx = -1;
  setTimeout(() => inp.focus(), 30);
}
function closeSearch() {
  document.getElementById('searchOverlay').classList.remove('open');
}
function runSearch(q) {
  q = q.trim().toLowerCase();
  const box = document.getElementById('searchResults');
  box.innerHTML = '';
  _searchIdx = -1;
  if (!q) return;
  const hits = S.bookmarks.filter(bk =>
    bk.title.toLowerCase().includes(q) || bk.url.toLowerCase().includes(q)
  ).slice(0, 24);
  if (!hits.length) { box.innerHTML = `<div class="search-empty">${T('search.noResults')}</div>`; return; }
  hits.forEach((bk, i) => {
    const board = S.boards.find(b => b.id === bk.boardId);
    const el = document.createElement('div');
    el.className = 'search-result';
    const img = document.createElement('img');
    img.className = 'search-result-favicon';
    setFavicon(img, bk.url);
    const info = document.createElement('div');
    info.className = 'search-result-info';
    info.innerHTML = `<div class="search-result-title">${bk.title}</div><div class="search-result-meta">${board ? board.name : ''}</div>`;
    el.appendChild(img); el.appendChild(info);
    el.addEventListener('click', () => { window.open(bk.url, '_blank'); closeSearch(); });
    el.addEventListener('mouseover', () => setSearchIdx(i));
    box.appendChild(el);
  });
}
function setSearchIdx(i) {
  const items = document.querySelectorAll('.search-result');
  items.forEach(el => el.classList.remove('active'));
  _searchIdx = Math.max(0, Math.min(i, items.length - 1));
  if (items[_searchIdx]) { items[_searchIdx].classList.add('active'); items[_searchIdx].scrollIntoView({ block: 'nearest' }); }
}

document.querySelector('.search-input-wrap').addEventListener('mousedown', e => {
  if (e.target.tagName === 'INPUT') return;
  e.preventDefault();
  document.getElementById('searchInput').focus();
});
document.getElementById('searchInput').addEventListener('input', e => runSearch(e.target.value));
document.getElementById('searchInput').addEventListener('keydown', e => {
  const items = document.querySelectorAll('.search-result');
  if (e.key === 'ArrowDown') { e.preventDefault(); setSearchIdx(_searchIdx + 1); }
  else if (e.key === 'ArrowUp') { e.preventDefault(); setSearchIdx(_searchIdx - 1); }
  else if (e.key === 'Enter' && items[_searchIdx]) items[_searchIdx].click();
  else if (e.key === 'Escape') closeSearch();
});
document.getElementById('searchOverlay').addEventListener('click', e => { if (e.target === e.currentTarget) closeSearch(); });
document.addEventListener('keydown', e => {
  if ((e.ctrlKey || e.metaKey) && e.key === 'k') { e.preventDefault(); openSearch(); }
});

// ── Trash ──
function openTrash() {
  renderTrash();
  document.getElementById('trashOverlay').classList.add('open');
}
function closeTrash() { showTrashConfirm(false); document.getElementById('trashOverlay').classList.remove('open'); }

function renderTrash() {
  const list = document.getElementById('trashList');
  list.innerHTML = '';
  const boards = S.trash.boards || [];
  const bks = (S.trash.bookmarks || []).filter(bk => !boards.find(b => b.id === bk.boardId));
  if (!boards.length && !bks.length) {
    list.innerHTML = `<div class="trash-empty-msg">${T('trash.isEmpty')}</div>`; return;
  }
  boards.forEach(board => {
    const el = document.createElement('div');
    el.className = 'trash-item';
    el.innerHTML = `<div class="trash-item-info"><div class="trash-item-title">📋 ${board.name}</div><div class="trash-item-meta">${T('trash.boardMeta', { n: S.trash.bookmarks.filter(bk => bk.boardId === board.id).length })}</div></div>`;
    const btn = document.createElement('button');
    btn.className = 'trash-item-restore';
    btn.textContent = T('trash.restore');
    btn.addEventListener('click', () => { restoreBoard(board.id); renderTrash(); });
    const del = document.createElement('button');
    del.className = 'trash-item-delete';
    del.textContent = '✕';
    del.title = T('tip.deletePermanently');
    del.addEventListener('click', () => { deleteBoardForever(board.id); renderTrash(); });
    el.appendChild(btn); el.appendChild(del);
    list.appendChild(el);
  });
  bks.forEach(bk => {
    const el = document.createElement('div');
    el.className = 'trash-item';
    const img = document.createElement('img');
    img.className = 'trash-item-icon';
    setFavicon(img, bk.url);
    const info = document.createElement('div');
    info.className = 'trash-item-info';
    info.innerHTML = `<div class="trash-item-title">${bk.title}</div><div class="trash-item-meta">${bk.url}</div>`;
    const btn = document.createElement('button');
    btn.className = 'trash-item-restore';
    btn.textContent = T('trash.restore');
    btn.addEventListener('click', () => { restoreBookmark(bk.id); renderTrash(); });
    const del = document.createElement('button');
    del.className = 'trash-item-delete';
    del.textContent = '✕';
    del.title = T('tip.deletePermanently');
    del.addEventListener('click', () => { deleteBookmarkForever(bk.id); renderTrash(); });
    el.appendChild(img); el.appendChild(info); el.appendChild(btn); el.appendChild(del);
    list.appendChild(el);
  });
}

function restoreBoard(boardId) {
  const board = S.trash.boards.find(b => b.id === boardId);
  if (!board) return;
  const { deletedAt, ...clean } = board;
  S.boards.push(clean);
  unTombstone(boardId);
  const restoredBkIds = [];
  S.trash.bookmarks.filter(bk => bk.boardId === boardId).forEach(bk => {
    const { deletedAt: _, ...cleanBk } = bk;
    S.bookmarks.push(cleanBk);
    restoredBkIds.push(bk.id);
  });
  restoredBkIds.forEach(unTombstone);
  S.trash.boards = S.trash.boards.filter(b => b.id !== boardId);
  S.trash.bookmarks = S.trash.bookmarks.filter(bk => bk.boardId !== boardId);
  saveState(); renderBoards();
}

function restoreBookmark(bkId) {
  const bk = S.trash.bookmarks.find(b => b.id === bkId);
  if (!bk) return;
  const { deletedAt, ...clean } = bk;
  S.bookmarks.push(clean);
  unTombstone(bkId);
  S.trash.bookmarks = S.trash.bookmarks.filter(b => b.id !== bkId);
  saveState(); renderBoards();
}

function deleteBoardForever(boardId) {
  const bkIds = S.trash.bookmarks.filter(bk => bk.boardId === boardId).map(bk => bk.id);
  S.trash.boards = S.trash.boards.filter(b => b.id !== boardId);
  S.trash.bookmarks = S.trash.bookmarks.filter(bk => bk.boardId !== boardId);
  tombstoneTrash([boardId, ...bkIds]);
  saveState();
}

function deleteBookmarkForever(bkId) {
  S.trash.bookmarks = S.trash.bookmarks.filter(b => b.id !== bkId);
  tombstoneTrash([bkId]);
  saveState();
}

document.getElementById('trashCloseBtn').addEventListener('click', closeTrash);
document.getElementById('trashOverlay').addEventListener('click', e => { if (e.target === e.currentTarget) closeTrash(); });
function showTrashConfirm(show) {
  document.getElementById('trashConfirm').style.display = show ? 'flex' : 'none';
  document.getElementById('trashEmptyBtn').style.display = show ? 'none' : '';
}
document.getElementById('trashEmptyBtn').addEventListener('click', () => showTrashConfirm(true));
document.getElementById('trashConfirmCancel').addEventListener('click', () => showTrashConfirm(false));
document.getElementById('trashConfirmYes').addEventListener('click', () => {
  showTrashConfirm(false);
  tombstoneTrash([
    ...S.trash.boards.map(b => b.id),
    ...S.trash.bookmarks.map(bk => bk.id),
  ]);
  S.trash = { boards: [], bookmarks: [] };
  saveState(); renderTrash();
});

// ── Import bookmarks ──
// Tour integration: set when the import modal is opened from the onboarding tour.
let _tourPausedForImport = false;
let _tourDidImport = false;

function openImportModal() {
  const list = document.getElementById('importList');
  list.innerHTML = `<div class="import-msg">${T('import.loading')}</div>`;
  document.getElementById('importOverlay').classList.add('open');

  chrome.bookmarks.getTree().then(tree => {
    const folders = [];
    function traverse(nodes, parentName) {
      for (const node of nodes) {
        if (!node.url && node.children) {
          const bks = node.children.filter(n => n.url);
          if (bks.length > 0) folders.push({ name: node.title || T('import.untitled'), parentName, bks });
          traverse(node.children, node.title || '');
        }
      }
    }
    traverse(tree[0]?.children || [], '');

    list.innerHTML = '';
    if (!folders.length) {
      list.innerHTML = `<div class="import-msg">${T('import.noFolders')}</div>`;
      return;
    }
    folders.forEach(folder => {
      const el = document.createElement('div');
      el.className = 'import-item';
      const meta = (folder.parentName ? folder.parentName + ' · ' : '') + T('import.bookmarksMeta', { n: folder.bks.length });
      el.innerHTML = `
        <div class="import-item-info">
          <div class="import-item-name">${folder.name}</div>
          <div class="import-item-meta">${meta}</div>
        </div>`;
      const btn = document.createElement('button');
      btn.className = 'import-item-btn';
      btn.textContent = T('import.action');
      btn.addEventListener('click', () => {
        importBookmarkFolder(folder.name, folder.bks);
        closeImportModal();
      });
      el.appendChild(btn);
      list.appendChild(el);
    });
  }).catch(err => {
    list.innerHTML = `<div class="import-msg">${T('import.failed')}</div>`;
    console.error('Bookmarks API error:', err);
  });
}

function closeImportModal() {
  document.getElementById('importOverlay').classList.remove('open');
  if (_tourPausedForImport) _resumeTourAfterImport();
}

function importBookmarkFolder(name, bks) {
  if (!S.bookmarks) S.bookmarks = [];
  const { numCols } = getLayoutParams();
  const pageBoards = S.boards.filter(b => b.pageId === S.activePage);

  // Pick first column where at least a board header (~60px) would still be on screen
  const availH = window.innerHeight - 50; // minus topbar
  const colEls = document.querySelectorAll('#boardsArea .board-column');
  let col = numCols - 1; // fallback: last column
  for (let c = 0; c < numCols; c++) {
    const colH = c < colEls.length ? colEls[c].getBoundingClientRect().height : 0;
    if (colH + 60 <= availH) { col = c; break; }
  }
  const row = pageBoards.filter(b => b.col === col).reduce((m, b) => Math.max(m, b.row), -1) + 1;
  const board = { id: genId(), pageId: S.activePage, name, col, row };
  S.boards.push(board);
  bks.forEach((bk, i) => {
    let title = bk.title || bk.url;
    try { if (!bk.title) title = new URL(bk.url).hostname.replace('www.', ''); } catch {}
    S.bookmarks.push({ id: genId(), boardId: board.id, url: bk.url, title, order: i });
  });
  if (_tourPausedForImport) _tourDidImport = true;
  saveState();
  renderBoards();
  // Scroll to the newly created board so user can see it
  requestAnimationFrame(() => {
    const el = document.querySelector(`.board[data-id="${board.id}"]`);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'center' });
  });
}

document.getElementById('importCloseBtn').addEventListener('click', closeImportModal);
document.getElementById('importOverlay').addEventListener('click', e => { if (e.target === e.currentTarget) closeImportModal(); });

// ── Import boards from a JSON file ──
// Expected shape: { "boards": [ { "name": "...", "bookmarks": [ { "url": "...", "title": "..." } ] } ] }
// "title" is optional — falls back to the URL's hostname, same as the Chrome bookmarks import above.
function importBoardsFromJson(data) {
  if (!data || !Array.isArray(data.boards)) return false;
  let ok = false;
  data.boards.forEach(entry => {
    if (!entry || typeof entry.name !== 'string') return;
    const bks = (entry.bookmarks || []).filter(bk => bk && typeof bk.url === 'string');
    if (!bks.length) return;
    importBookmarkFolder(entry.name, bks);
    ok = true;
  });
  return ok;
}

document.getElementById('importJsonLink').addEventListener('click', () => {
  document.getElementById('importJsonInput').click();
});
document.getElementById('importJsonInput').addEventListener('change', e => {
  const file = e.target.files[0];
  e.target.value = '';
  if (!file) return;
  const reader = new FileReader();
  reader.onload = () => {
    let data = null;
    try { data = JSON.parse(reader.result); } catch {}
    if (importBoardsFromJson(data)) {
      closeImportModal();
    } else {
      document.getElementById('importList').innerHTML = `<div class="import-msg">${T('import.jsonFailed')}</div>`;
    }
  };
  reader.readAsText(file);
});


let _resizeTimer;
window.addEventListener('resize', () => { clearTimeout(_resizeTimer); _resizeTimer = setTimeout(() => { renderBoards(); syncLayout(); }, 100); });

// When returning to the tab, immediately correct all running timer displays
document.addEventListener('visibilitychange', () => {
  if (document.hidden) return;
  Object.entries(_pomodoroState).forEach(([boardId, ps]) => {
    if (!ps.running || ps.startedAt == null) return;
    const elapsed = Math.floor((Date.now() - ps.startedAt) / 1000);
    ps.timeLeft = Math.max(0, ps.startedTimeLeft - elapsed);
    const t = document.querySelector(`.board[data-id="${boardId}"] .pom-timer`);
    if (t) t.textContent = pomFmt(ps.timeLeft);
    if (ps.timeLeft <= 0) {
      // Phase completed while away — re-render to handle transition
      renderBoards();
    }
  });
  updateFocusStats();
});

// ── Theme utils ──
function hexToRgb(hex) {
  hex = hex.replace('#','');
  if (hex.length === 3) hex = hex.split('').map(c => c+c).join('');
  const n = parseInt(hex, 16);
  return { r:(n>>16)&255, g:(n>>8)&255, b:n&255 };
}
function rgbToHex(r, g, b) {
  return '#' + [r,g,b].map(v => Math.round(Math.max(0,Math.min(255,v))).toString(16).padStart(2,'0')).join('');
}
function colorBrightness(r, g, b) { return (r*299 + g*587 + b*114) / 1000; }

function analyzeWallpaper(dataUrl) {
  return new Promise(resolve => {
    const img = new Image();
    img.onload = () => {
      const W=100, H=60, canvas=document.createElement('canvas');
      canvas.width=W; canvas.height=H;
      const ctx=canvas.getContext('2d');
      ctx.drawImage(img,0,0,W,H);
      const d=ctx.getImageData(0,0,W,H).data;
      let tr=0,tg=0,tb=0, maxScore=0, ar=128,ag=128,ab=128;
      const n=d.length/4;
      for (let i=0; i<d.length; i+=4) {
        const r=d[i],g=d[i+1],b=d[i+2];
        tr+=r; tg+=g; tb+=b;
        const mx=Math.max(r,g,b), mn=Math.min(r,g,b);
        const sat=mx===0?0:(mx-mn)/mx;
        const lum=mx/255;
        const score=sat*(lum>0.2&&lum<0.85?1:0);
        if (score>maxScore) { maxScore=score; ar=r; ag=g; ab=b; }
      }
      const avgR=tr/n, avgG=tg/n, avgB=tb/n;
      const brightness=colorBrightness(avgR,avgG,avgB);
      resolve({
        isDark: brightness < 140,
        accent: maxScore > 0.15 ? rgbToHex(ar,ag,ab) : '#6eb5d4',
        avgRgb: { r: Math.round(avgR), g: Math.round(avgG), b: Math.round(avgB) }
      });
    };
    img.onerror = () => resolve({ isDark:true, accent:'#ffffff', avg:'#0d1117' });
    img.src = dataUrl;
  });
}

function applyThemeStyle(ts) {
  const root = document.documentElement;
  const {r,g,b} = hexToRgb(ts.boardColorHex||'#ffffff');
  root.style.setProperty('--board-rgb', `${r},${g},${b}`);
  root.style.setProperty('--board-alpha', ((ts.boardOpacity ?? 5) / 100).toFixed(3));
  root.style.setProperty('--board-blur', (ts.boardBlur ?? 12) + 'px');
  const borderAlpha = Math.min(0.35, ((ts.boardOpacity ?? 5) / 100) * 3).toFixed(3);
  root.style.setProperty('--board-border', `rgba(${r},${g},${b},${borderAlpha})`);
  // Bulk board outline (see renderAppearanceTab): one live CSS var drives
  // every board this feature controls, whether its color is a manual pick
  // or tracking the theme — so any change here (color, opacity, or the
  // board color/opacity it falls back to) shows up on those boards
  // immediately, no separate "apply" step needed. A plain color, no
  // blur/shadow trickery: that read as a diffuse shadow, not an outline.
  const {r: outR, g: outG, b: outB} = hexToRgb(outlineEffectiveHex());
  const themeOutlineAlpha = (outlineEffectiveOpacityPct() / 100).toFixed(3);
  root.style.setProperty('--board-outline-theme-color', `rgba(${outR},${outG},${outB},${themeOutlineAlpha})`);
  const {r:ar,g:ag,b:ab} = hexToRgb(ts.accentHex||'#ffffff');
  root.style.setProperty('--accent-color', ts.accentHex||'#ffffff');
  root.style.setProperty('--accent-tab-bg', `rgba(${ar},${ag},${ab},0.8)`);
  root.style.setProperty('--accent-tab-border', `rgba(${ar},${ag},${ab},0.95)`);
  root.style.setProperty('--accent-tab-text', colorBrightness(ar,ag,ab) > 160 ? 'rgba(0,0,0,0.85)' : '#fff');

  // Board text color: blend board color with actual bg brightness (light/dark theme)
  const alpha = (ts.boardOpacity ?? 5) / 100;
  const hoverAlpha = Math.min(0.28, alpha + 0.05).toFixed(3);
  root.style.setProperty('--board-alpha-hover', hoverAlpha);
  const bgBrightness = ts.isDark === false ? 230 : 60;
  const effectiveBrightness = colorBrightness(r, g, b) * alpha + bgBrightness * (1 - alpha);
  const boardIsLight = effectiveBrightness > 128;
  if (boardIsLight) {
    root.style.setProperty('--board-text', 'rgba(0,0,0,0.85)');
    root.style.setProperty('--board-text-secondary', 'rgba(0,0,0,0.65)');
    root.style.setProperty('--board-text-dim', 'rgba(0,0,0,0.3)');
    root.style.setProperty('--board-text-hover', 'rgba(0,0,0,1)');
    root.style.setProperty('--board-hover-bg', 'rgba(0,0,0,0.07)');
  } else {
    root.style.setProperty('--board-text', 'rgba(255,255,255,0.9)');
    root.style.setProperty('--board-text-secondary', 'rgba(255,255,255,0.65)');
    root.style.setProperty('--board-text-dim', 'rgba(255,255,255,0.28)');
    root.style.setProperty('--board-text-hover', '#fff');
    root.style.setProperty('--board-hover-bg', 'rgba(255,255,255,0.07)');
  }

  // Global board text typography (size scale + weight), applied to all boards via
  // CSS variables — see .link-item / .link-title / .board-title / .link-desc.
  root.style.setProperty('--board-text-scale', ts.textScale || 1);
  root.style.setProperty('--link-weight', ts.textBold ? '600' : '400');

  document.body.classList.toggle('theme-light', ts.isDark === false);

  // Search bar (.nsb-bar): independent color/opacity/blur/size, set only once the
  // user customizes it in Settings — until then --nsb-* stays unset and the CSS
  // var() fallback to --board-* keeps it looking exactly like the boards.
  const hasCustomNsb = ts.navSearchColorHex != null || ts.navSearchOpacity != null || ts.navSearchBlur != null;
  if (hasCustomNsb) {
    const {r: nr, g: ng, b: nb} = hexToRgb(ts.navSearchColorHex ?? ts.boardColorHex ?? '#ffffff');
    const nsAlpha = (ts.navSearchOpacity ?? ts.boardOpacity ?? 5) / 100;
    root.style.setProperty('--nsb-rgb', `${nr},${ng},${nb}`);
    root.style.setProperty('--nsb-alpha', nsAlpha.toFixed(3));
    root.style.setProperty('--nsb-blur', (ts.navSearchBlur ?? ts.boardBlur ?? 12) + 'px');
    const nsEffectiveBrightness = colorBrightness(nr, ng, nb) * nsAlpha + bgBrightness * (1 - nsAlpha);
    if (nsEffectiveBrightness > 128) {
      root.style.setProperty('--nsb-text', 'rgba(0,0,0,0.85)');
      root.style.setProperty('--nsb-text-dim', 'rgba(0,0,0,0.3)');
    } else {
      root.style.setProperty('--nsb-text', 'rgba(255,255,255,0.9)');
      root.style.setProperty('--nsb-text-dim', 'rgba(255,255,255,0.28)');
    }
  } else {
    ['--nsb-rgb', '--nsb-alpha', '--nsb-blur', '--nsb-text', '--nsb-text-dim'].forEach(v => root.style.removeProperty(v));
  }
  if (ts.navSearchWidth) root.style.setProperty('--nsb-w', ts.navSearchWidth + 'px');
  else root.style.removeProperty('--nsb-w');
}

// Text size/weight are a global user preference, independent of the wallpaper.
// Wallpaper changes rebuild themeStyle from scratch, so overlay the current text
// prefs (mutating the passed object) to keep them sticky across wallpaper switches.
function withTextPrefs(ts) {
  ts.textScale = S.themeStyle?.textScale ?? 1;
  ts.textBold  = S.themeStyle?.textBold ?? false;
  // Search bar style is a user override independent of the wallpaper — keep it
  // sticky across wallpaper switches the same way text prefs are.
  if (S.themeStyle?.navSearchColorHex != null) ts.navSearchColorHex = S.themeStyle.navSearchColorHex;
  if (S.themeStyle?.navSearchOpacity != null) ts.navSearchOpacity = S.themeStyle.navSearchOpacity;
  if (S.themeStyle?.navSearchBlur != null) ts.navSearchBlur = S.themeStyle.navSearchBlur;
  if (S.themeStyle?.navSearchWidth != null) ts.navSearchWidth = S.themeStyle.navSearchWidth;
  return ts;
}

// ── Style editor ──
let _sePrevTheme = null;

function openStyleEditor(ts) {
  _sePrevTheme = JSON.parse(JSON.stringify(S.themeStyle||{}));
  document.getElementById('seAccentPicker').value = ts.accentHex||'#ffffff';
  document.getElementById('seBoardPicker').value = ts.boardColorHex||'#ffffff';
  document.getElementById('seOpacitySlider').value = ts.boardOpacity||5;
  document.getElementById('seBlurSlider').value = ts.boardBlur||12;
  document.getElementById('seSubtitle').textContent = ts.isDark===false ? T('se.lightDetected') : T('se.darkDetected');
  updateSeLabels(ts);
  setSeg('seTextScale', ts.textScale || 1);
  setSeg('seTextWeight', ts.textBold ? 1 : 0);
  const modal = document.querySelector('.se-modal');
  modal.style.left = '50%';
  modal.style.top = '50%';
  modal.style.transform = 'translate(-50%, -50%)';
  document.getElementById('seOverlay').classList.add('open');
  updateSeSliderFills();
}

// Drag header
(function() {
  const header = document.querySelector('.se-header');
  header.addEventListener('mousedown', e => {
    const modal = document.querySelector('.se-modal');
    const rect = modal.getBoundingClientRect();
    modal.style.transform = 'none';
    modal.style.left = rect.left + 'px';
    modal.style.top = rect.top + 'px';
    const ox = e.clientX - rect.left, oy = e.clientY - rect.top;
    function onMove(e) {
      modal.style.left = Math.max(0, Math.min(window.innerWidth - rect.width, e.clientX - ox)) + 'px';
      modal.style.top  = Math.max(0, Math.min(window.innerHeight - 80, e.clientY - oy)) + 'px';
    }
    function onUp() { document.removeEventListener('mousemove', onMove); document.removeEventListener('mouseup', onUp); }
    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onUp);
    e.preventDefault();
  });
}());

// Settings modal drag
(function() {
  const header = document.querySelector('.settings-header');
  header.addEventListener('mousedown', e => {
    if (e.target.closest('button')) return;
    const modal = document.querySelector('.settings-modal');
    const rect = modal.getBoundingClientRect();
    modal.style.transform = 'none';
    modal.style.left = rect.left + 'px';
    modal.style.top = rect.top + 'px';
    const ox = e.clientX - rect.left, oy = e.clientY - rect.top;
    function onMove(ev) {
      modal.style.left = Math.max(0, Math.min(window.innerWidth - rect.width, ev.clientX - ox)) + 'px';
      modal.style.top  = Math.max(0, Math.min(window.innerHeight - 80, ev.clientY - oy)) + 'px';
    }
    function onUp() { document.removeEventListener('mousemove', onMove); document.removeEventListener('mouseup', onUp); }
    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onUp);
    e.preventDefault();
  });
}());

function updateSeLabels(ts) {
  const acc = ts.accentHex || '#ffffff', brd = ts.boardColorHex || '#ffffff';
  document.getElementById('seAccentHex').textContent = acc;
  document.getElementById('seBoardHex').textContent = brd;
  document.getElementById('seAccentSwatch').style.background = acc;
  document.getElementById('seBoardSwatch').style.background = brd;
  document.getElementById('seOpacityVal').textContent = (ts.boardOpacity ?? 5) + '%';
  document.getElementById('seBlurVal').textContent = (ts.boardBlur ?? 12) + 'px';
}

function setSeg(groupId, val) {
  const g = document.getElementById(groupId);
  if (!g) return;
  const target = String(val);
  g.querySelectorAll('.se-seg-btn').forEach(b => b.classList.toggle('active', b.dataset.val === target));
}

function seCurrentValues() {
  const scaleBtn = document.querySelector('#seTextScale .se-seg-btn.active');
  const boldBtn  = document.querySelector('#seTextWeight .se-seg-btn.active');
  return {
    accentHex: document.getElementById('seAccentPicker').value,
    boardColorHex: document.getElementById('seBoardPicker').value,
    boardOpacity: +document.getElementById('seOpacitySlider').value,
    boardBlur: +document.getElementById('seBlurSlider').value,
    isDark: S.themeStyle?.isDark !== false,
    textScale: scaleBtn ? +scaleBtn.dataset.val : 1,
    textBold: boldBtn ? boldBtn.dataset.val === '1' : false
  };
}

function updateSeSliderFills() {
  [
    { id: 'seOpacitySlider', min: 0, max: 100 },
    { id: 'seBlurSlider',    min: 0, max: 40  }
  ].forEach(({ id, min, max }) => {
    const el = document.getElementById(id);
    const pct = (el.value - min) / (max - min) * 100;
    el.style.background = `linear-gradient(to right, var(--accent-color,#fff) ${pct}%, rgba(255,255,255,0.12) ${pct}%)`;
  });
}

function seApply() { const ts = seCurrentValues(); updateSeLabels(ts); applyThemeStyle(ts); updateSeSliderFills(); }

['seAccentPicker','seBoardPicker','seOpacitySlider','seBlurSlider'].forEach(id => {
  const el = document.getElementById(id);
  el.addEventListener('input', seApply);
  el.addEventListener('change', seApply);
});

['seTextScale','seTextWeight'].forEach(groupId => {
  document.getElementById(groupId).addEventListener('click', e => {
    const btn = e.target.closest('.se-seg-btn');
    if (!btn) return;
    btn.parentElement.querySelectorAll('.se-seg-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    seApply();
  });
});

document.getElementById('seSaveBtn').addEventListener('click', () => {
  S.themeStyle = seCurrentValues();
  saveState();
  document.getElementById('seOverlay').classList.remove('open');
});
document.getElementById('seCancelBtn').addEventListener('click', () => {
  if (_sePrevTheme) applyThemeStyle(_sePrevTheme);
  document.getElementById('seOverlay').classList.remove('open');
});
document.getElementById('seResetBtn').addEventListener('click', () => {
  const def = { boardColorHex:'#ffffff', boardOpacity:5, boardBlur:12, accentHex:'#ffffff', isDark: S.themeStyle?.isDark ?? true, textScale:1, textBold:false };
  document.getElementById('seAccentPicker').value = def.accentHex;
  document.getElementById('seBoardPicker').value = def.boardColorHex;
  document.getElementById('seOpacitySlider').value = def.boardOpacity;
  document.getElementById('seBlurSlider').value = def.boardBlur;
  setSeg('seTextScale', 1);
  setSeg('seTextWeight', 0);
  updateSeLabels(def);
  applyThemeStyle(def);
  updateSeSliderFills();
});

// ── Wallpaper ──
const BUILTIN_WALLPAPERS = [
  {
    id: 'b-forest', name: 'Forest', dark: true,
    css: 'radial-gradient(ellipse at 0% 100%,#1b5e20 0,transparent 55%),radial-gradient(ellipse at 100% 0%,#004d40 0,transparent 55%),radial-gradient(ellipse at 50% 50%,#021a05 0,transparent 60%),linear-gradient(#010602,#010602)',
    accent: '#34d399', board: '#021008', opacity: 18, blur: 14,
  },
  {
    id: 'b-arctic', name: 'Arctic', dark: false,
    css: 'radial-gradient(ellipse at 50% 0%,#b3e5fc 0,transparent 65%),radial-gradient(ellipse at 0% 100%,#bbdefb 0,transparent 55%),radial-gradient(ellipse at 100% 50%,#e1f5fe 0,transparent 55%),linear-gradient(#f0f8ff,#f0f8ff)',
    accent: '#0284c7', board: '#ffffff', opacity: 60, blur: 12,
  },
];

const BUNDLED_WALLPAPERS = [
  { id: 'bw-01', file: 'wallpapers/01.png' },
  { id: 'bw-02', file: 'wallpapers/02.jpg' },
  { id: 'bw-03', file: 'wallpapers/03.jpg' },
  { id: 'bw-04', file: 'wallpapers/04.jpg' },
  { id: 'bw-05', file: 'wallpapers/05.jpg' },
  { id: 'bw-06', file: 'wallpapers/06.png' },
  { id: 'bw-07', file: 'wallpapers/07.png' },
  { id: 'bw-08', file: 'wallpapers/08.jpg' },
  { id: 'bw-09', file: 'wallpapers/09.jpg' },
  { id: 'bw-10', file: 'wallpapers/10.jpg' },
  { id: 'bw-11', file: 'wallpapers/11.jpg' },
  { id: 'bw-12', file: 'wallpapers/12.jpg' },
  { id: 'bw-13', file: 'wallpapers/13.jpg' },
  { id: 'bw-14', file: 'wallpapers/14.jpg' },
];

function createThumb(dataUrl) {
  return new Promise(async resolve => {
    try {
      const img = new Image();
      img.src = dataUrl;
      await img.decode();                       // ensure decode before drawing
      // 16:10 to match the .wp-thumb tile (no cropping), and high enough res
      // that the tile never has to upscale — kills the banding/edge lines that
      // a 120×78 q0.6 JPEG produced on smooth gradients (sky, water).
      const W = 256, H = 160;
      const canvas = document.createElement('canvas');
      canvas.width = W; canvas.height = H;
      const ctx = canvas.getContext('2d');
      ctx.imageSmoothingQuality = 'high';
      const scale = Math.max(W / img.width, H / img.height);
      const sw = W / scale, sh = H / scale;
      ctx.drawImage(img, (img.width - sw) / 2, (img.height - sh) / 2, sw, sh, 0, 0, W, H);
      resolve(canvas.toDataURL('image/jpeg', 0.85));
    } catch { resolve(null); }
  });
}

function analyzeAndStyle(imageData, histId) {
  analyzeWallpaper(imageData).then(analysis => {
    const {r, g, b} = analysis.avgRgb;
    const boardColorHex = analysis.isDark
      ? rgbToHex(Math.round(r * 0.3), Math.round(g * 0.3), Math.round(b * 0.3))
      : '#ffffff';
    const ts = { isDark: analysis.isDark, accentHex: analysis.accent, boardColorHex,
      boardOpacity: analysis.isDark ? 20 : 60, boardBlur: 12 };
    withTextPrefs(ts); S.themeStyle = ts; applyThemeStyle(ts);
    if (histId) {
      S.currentWallId = histId;
      const entry = (S.wallpaperHistory || []).find(h => h.id === histId);
      if (entry) entry.themeStyle = JSON.parse(JSON.stringify(ts));
    }
    saveState();
    openStyleEditor(ts);
  });
}

function addToWallpaperHistory(type, data, thumb, name) {
  if (!S.wallpaperHistory) S.wallpaperHistory = [];
  const id = genId();
  while (S.wallpaperHistory.length >= 20) {
    const old = S.wallpaperHistory.pop();
    deleteFromDB('hwp_' + old.id);
  }
  S.wallpaperHistory.unshift({ id, type, thumb, name });
  saveState();
  saveToDB('hwp_' + id, data);
  return id;
}

function applyHistoryItem(item) {
  getFromDB('hwp_' + item.id).then(async data => {
    if (!data) return;
    closeWallpaperModal();
    S.currentWallId = item.id;
    if (item.type === 'image') {
      showImage(data); saveToDB('type', 'image'); saveToDB('data', data);
      try { localStorage.setItem('ntwp-data', data); } catch {}
      localStorage.setItem('ntwp-type', 'image');
      if (item.themeStyle) { S.themeStyle = withTextPrefs(JSON.parse(JSON.stringify(item.themeStyle))); applyThemeStyle(S.themeStyle); saveState(); }
      else analyzeAndStyle(data, item.id);
    } else if (item.type === 'video') {
      showVideo(data); saveToDB('type', 'video'); saveToDB('data', data);
      localStorage.setItem('ntwp-type', 'video'); localStorage.removeItem('ntwp-data');
      if (item.themeStyle) { S.themeStyle = withTextPrefs(JSON.parse(JSON.stringify(item.themeStyle))); applyThemeStyle(S.themeStyle); saveState(); }
      else { const frame = await captureVideoFrame(document.getElementById('video-bg')); analyzeAndStyle(frame, item.id); }
    }
  });
}

function applyBuiltinWallpaper(preset) {
  showGradient(preset.css);
  saveToDB('type', 'gradient'); saveToDB('data', preset.css);
  localStorage.removeItem('ntwp-data'); localStorage.setItem('ntwp-type', 'gradient');
  const ts = {
    isDark:        preset.dark,
    accentHex:     preset.accent  ?? (preset.dark ? '#7c8cff' : '#e07a4a'),
    boardColorHex: preset.board   ?? (preset.dark ? '#1a1a3e' : '#ffffff'),
    boardOpacity:  preset.opacity ?? (preset.dark ? 16 : 55),
    boardBlur:     preset.blur    ?? 12,
  };
  withTextPrefs(ts); S.themeStyle = ts; applyThemeStyle(ts); saveState();
  closeWallpaperModal();
}

function applyBundledWallpaper(wp, opts) {
  opts = opts || {};
  const url = chrome.runtime.getURL(wp.file);
  showImage(url);
  saveToDB('type', 'bundled'); saveToDB('data', wp.file);
  localStorage.setItem('ntwp-type', 'bundled');
  try { localStorage.setItem('ntwp-data', url); } catch {}
  if (!opts.silent) closeWallpaperModal();
  fetch(url).then(r => {
    if (!r.ok) throw new Error('HTTP ' + r.status);
    return r.blob();
  }).then(blob => {
    const fr = new FileReader();
    fr.onload = e => {
      analyzeWallpaper(e.target.result).then(analysis => {
        const {r, g, b} = analysis.avgRgb;
        const boardColorHex = analysis.isDark
          ? rgbToHex(Math.round(r * 0.3), Math.round(g * 0.3), Math.round(b * 0.3))
          : '#ffffff';
        const ts = { isDark: analysis.isDark, accentHex: analysis.accent, boardColorHex,
          boardOpacity: analysis.isDark ? 20 : 60, boardBlur: 12 };
        withTextPrefs(ts); S.themeStyle = ts; applyThemeStyle(ts); saveState();
        if (!opts.silent) openStyleEditor(ts);
      }).catch(() => {});
    };
    fr.readAsDataURL(blob);
  }).catch(err => {
    console.warn('[NovaTab] Could not load wallpaper image for color analysis:', err);
  });
}

function removeWallpaper() {
  document.getElementById('photo-bg').style.backgroundImage = '';
  document.getElementById('photo-bg').classList.remove('active');
  const video = document.getElementById('video-bg');
  if (video._blobUrl) { URL.revokeObjectURL(video._blobUrl); video._blobUrl = null; }
  video.classList.remove('active'); video.src = '';
  deleteFromDB('type'); deleteFromDB('data');
  localStorage.removeItem('ntwp-type'); localStorage.removeItem('ntwp-data');
  S.currentWallId = null; saveState();
  closeWallpaperModal();
}

function buildWallpaperBody() {
  const body = document.getElementById('wpBody');
  body.innerHTML = '';

  // Upload zone
  const zone = document.createElement('div');
  zone.className = 'wp-upload-zone';
  zone.innerHTML = `<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><polyline points="16 16 12 12 8 16"/><line x1="12" y1="12" x2="12" y2="21"/><path d="M20.39 18.39A5 5 0 0 0 18 9h-1.26A8 8 0 1 0 3 16.3"/></svg><div class="wp-upload-text">${T('wp.upload')}</div><div class="wp-upload-sub">JPG · PNG · MP4</div>`;
  zone.addEventListener('click', () => {
    document.getElementById('fileInput').click();
  });
  body.appendChild(zone);

  // Presets: bundled photos + built-in gradients in one section
  const presetSec = document.createElement('div');
  presetSec.className = 'wp-section';
  presetSec.innerHTML = `<div class="wp-section-label">${T('wp.presets')}</div>`;
  const presetGrid = document.createElement('div');
  presetGrid.className = 'wp-thumb-grid';
  BUNDLED_WALLPAPERS.forEach(wp => {
    const t = document.createElement('div');
    t.className = 'wp-thumb';
    const url = chrome.runtime.getURL(wp.file);
    t.style.backgroundImage = `url(${url})`;
    t.style.backgroundSize = 'cover';
    t.style.backgroundPosition = 'center';
    t.addEventListener('click', () => applyBundledWallpaper(wp));
    presetGrid.appendChild(t);
  });
  [...BUILTIN_WALLPAPERS].sort((a, b) => (b.dark ? 1 : 0) - (a.dark ? 1 : 0)).forEach(p => {
    const t = document.createElement('div');
    t.className = 'wp-thumb';
    t.style.backgroundImage = p.css;
    t.title = p.name;
    t.addEventListener('click', () => applyBuiltinWallpaper(p));
    presetGrid.appendChild(t);
  });
  presetSec.appendChild(presetGrid);
  body.appendChild(presetSec);

  // User uploads history
  const history = S.wallpaperHistory || [];
  if (history.length > 0) {
    const histSec = document.createElement('div');
    histSec.className = 'wp-section';
    histSec.innerHTML = `<div class="wp-section-label">${T('wp.myUploads')}</div>`;
    const histGrid = document.createElement('div');
    histGrid.className = 'wp-thumb-grid' + (history.length > 8 ? ' scrollable' : '');
    history.forEach(item => {
      const t = document.createElement('div');
      t.className = 'wp-thumb';
      if (item.thumb) { t.style.backgroundImage = `url(${item.thumb})`; t.style.backgroundSize = 'cover'; t.style.backgroundPosition = 'center'; }
      t.title = item.name || '';
      t.addEventListener('click', () => applyHistoryItem(item));
      const del = document.createElement('button');
      del.className = 'wp-thumb-del';
      del.textContent = '×';
      del.title = T('tip.remove');
      del.addEventListener('click', e => {
        e.stopPropagation();
        S.wallpaperHistory = (S.wallpaperHistory || []).filter(h => h.id !== item.id);
        deleteFromDB('hwp_' + item.id);
        saveState();
        buildWallpaperBody();
      });
      t.appendChild(del);
      histGrid.appendChild(t);
    });
    histSec.appendChild(histGrid);
    body.appendChild(histSec);
  }

  // Find wallpapers
  const linksSec = document.createElement('div');
  linksSec.className = 'wp-section';
  const findLabel = document.createElement('div');
  findLabel.className = 'wp-section-label';
  findLabel.textContent = T('wp.find');
  const findBtn = document.createElement('button');
  findBtn.className = 'wp-find-btn';
  findBtn.innerHTML = `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg><span>${T('wp.findBtn')}</span><svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/><polyline points="15 3 21 3 21 9"/><line x1="10" y1="14" x2="21" y2="3"/></svg>`;
  findBtn.addEventListener('click', () => {
    chrome.tabs.create({ url: T('wp.findUrl') });
  });
  linksSec.appendChild(findLabel);
  linksSec.appendChild(findBtn);
  body.appendChild(linksSec);

}

function openWallpaperModal() {
  buildWallpaperBody();
  document.getElementById('wpOverlay').classList.add('open');
}
function closeWallpaperModal() {
  document.getElementById('wpOverlay').classList.remove('open');
}

document.getElementById('wpCloseBtn').addEventListener('click', closeWallpaperModal);
document.getElementById('wpOverlay').addEventListener('click', e => { if (e.target === e.currentTarget) closeWallpaperModal(); });

const DB_NAME = 'newtab-db', DB_VERSION = 1, STORE = 'wallpapers';

function openDB() {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, DB_VERSION);
    req.onupgradeneeded = e => e.target.result.createObjectStore(STORE);
    req.onsuccess = e => resolve(e.target.result);
    req.onerror = () => reject(req.error);
  });
}
function saveToDB(key, value) {
  return openDB().then(db => new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, 'readwrite');
    tx.objectStore(STORE).put(value, key);
    tx.oncomplete = resolve; tx.onerror = () => reject(tx.error);
  }));
}
function getFromDB(key) {
  return openDB().then(db => new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, 'readonly');
    const req = tx.objectStore(STORE).get(key);
    req.onsuccess = () => resolve(req.result); req.onerror = () => reject(req.error);
  }));
}
function deleteFromDB(key) {
  return openDB().then(db => new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, 'readwrite');
    tx.objectStore(STORE).delete(key);
    tx.oncomplete = resolve; tx.onerror = () => reject(tx.error);
  }));
}

function removePreloadStyle() {
  const el = document.getElementById('wp-preload');
  if (el) el.parentNode.removeChild(el);
}

function showImage(url) {
  const bg = document.getElementById('photo-bg');
  document.getElementById('video-bg').classList.remove('active');
  document.getElementById('video-bg').src = '';
  bg.style.backgroundImage = 'url(' + url + ')';
  bg.classList.add('active');
}
function showVideo(data) {
  const video = document.getElementById('video-bg');
  document.getElementById('photo-bg').classList.remove('active');
  if (video._blobUrl) { URL.revokeObjectURL(video._blobUrl); video._blobUrl = null; }
  if (data instanceof Blob) {
    video._blobUrl = URL.createObjectURL(data);
    video.src = video._blobUrl;
  } else {
    video.src = data;
  }
  video.classList.add('active');
}
function showGradient(css) {
  const bg = document.getElementById('photo-bg');
  document.getElementById('video-bg').classList.remove('active');
  document.getElementById('video-bg').src = '';
  bg.style.backgroundImage = css;
  bg.classList.add('active');
}

function loadSavedWallpaper() {
  const preload = window._wpPreload;
  if (preload) {
    delete window._wpPreload;
    preload.then(({ type, data }) => {
      if (type === 'image' && data) showImage(data);
      else if (type === 'bundled' && data) showImage(data);
      else if (type === 'video' && data) showVideo(data);
      else if (type === 'gradient' && data) showGradient(data);
    }).finally(() => { removePreloadStyle(); document.documentElement.style.opacity = ''; });
    return;
  }
  getFromDB('type').then(type => {
    if (type === 'image') return getFromDB('data').then(data => { if (data) showImage(data); });
    if (type === 'video') return getFromDB('data').then(data => { if (data) showVideo(data); });
    if (type === 'gradient') return getFromDB('data').then(data => { if (data) showGradient(data); });
    if (type === 'bundled') return getFromDB('data').then(file => {
      if (file) showImage(chrome.runtime.getURL(file));
    });
    // New user — apply default wallpaper silently
    applyBundledWallpaper(BUNDLED_WALLPAPERS[0], { silent: true });
  }).finally(() => {
    removePreloadStyle();
    document.documentElement.style.opacity = '';
  });
}

function captureVideoFrame(videoEl) {
  return new Promise(resolve => {
    const W = 200, H = 120;

    // Draw the CURRENT frame and return { dataUrl, luma }. luma is the mean
    // brightness (0–255) so we can reject black frames (intros / fade-ins).
    function draw() {
      const canvas = document.createElement('canvas');
      canvas.width = W; canvas.height = H;
      const ctx = canvas.getContext('2d');
      ctx.drawImage(videoEl, 0, 0, W, H);
      let s = 0;
      const d = ctx.getImageData(0, 0, W, H).data;
      for (let i = 0; i < d.length; i += 4) s += (d[i] + d[i + 1] + d[i + 2]) / 3;
      return { dataUrl: canvas.toDataURL('image/jpeg', 0.9), luma: s / (d.length / 4) };
    }

    // Run cb only after a real frame has been PRESENTED. seeked alone fires
    // before Chrome paints the new frame, which is what produced black grabs.
    function onFrame(cb) {
      if (videoEl.requestVideoFrameCallback) videoEl.requestVideoFrameCallback(() => cb());
      else requestAnimationFrame(() => requestAnimationFrame(cb));
    }

    const dur = videoEl.duration || 0;
    const times = dur > 2 ? [1, dur * 0.25, dur * 0.5] : [0];

    let i = 0, best = null, bestLuma = -1;
    function tryNext() {
      if (i >= times.length) { resolve(best); return; }
      const t = times[i++];
      const grab = () => onFrame(() => {
        const { dataUrl, luma } = draw();
        if (luma > bestLuma) { bestLuma = luma; best = dataUrl; }
        if (luma >= 12) resolve(best);   // bright enough — keep it
        else tryNext();                  // black frame — try another moment
      });
      if (Math.abs(videoEl.currentTime - t) < 0.05) grab();
      else { videoEl.addEventListener('seeked', grab, { once: true }); videoEl.currentTime = t; }
    }

    // HAVE_CURRENT_DATA (2): at least one frame is decoded and drawable.
    if (videoEl.readyState >= 2) tryNext();
    else videoEl.addEventListener('loadeddata', tryNext, { once: true });
  });
}

document.getElementById('fileInput').addEventListener('change', async e => {
  const file = e.target.files[0]; if (!file) return;
  e.target.value = '';
  const isVideo = file.type.startsWith('video/');
  closeWallpaperModal();
  if (isVideo) {
    // Store raw Blob — no base64 overhead, createObjectURL is instant
    showVideo(file);
    saveToDB('type', 'video'); saveToDB('data', file);
    localStorage.setItem('ntwp-type', 'video'); localStorage.removeItem('ntwp-data');
    const frame = await captureVideoFrame(document.getElementById('video-bg'));
    const thumb = await createThumb(frame);
    const histId = addToWallpaperHistory('video', file, thumb, file.name);
    analyzeAndStyle(frame, histId);
  } else {
    const reader = new FileReader();
    reader.onload = async ev => {
      const data = ev.target.result;
      showImage(data); saveToDB('type', 'image'); saveToDB('data', data);
      try { localStorage.setItem('ntwp-data', data); } catch {}
      localStorage.setItem('ntwp-type', 'image');
      const thumb = await createThumb(data);
      const histId = addToWallpaperHistory('image', data, thumb, file.name);
      analyzeAndStyle(data, histId);
    };
    reader.readAsDataURL(file);
  }
});

// ── Onboarding ──
function checkOnboarding() {
  if (!localStorage.getItem('mz-tour-done')) {
    setTimeout(() => startTour(), 400);
  }
}

// ── Settings ──
function _settingsOutsideClick(e) {
  if (!_settingsOpen) return;
  const modal = document.querySelector('.settings-modal');
  if (modal && !modal.contains(e.target)) closeSettingsModal();
}

let _settingsInitStyle = null;
let _settingsOpen = false;
let _settingsActiveTab = 'account';

function openSettingsModal() {
  const overlay = document.getElementById('settingsOverlay');
  overlay.style.display = 'block';
  _settingsOpen = true;
  _settingsInitStyle = JSON.parse(JSON.stringify(S.themeStyle || {}));
  try {
    renderSettingsNav();
    renderSettingsTabContent();
  } catch (err) {
    console.error('[Markmez] Settings render error:', err);
  }
  setTimeout(() => document.addEventListener('click', _settingsOutsideClick), 0);
}

function closeSettingsModal() {
  _settingsOpen = false;
  document.getElementById('settingsOverlay').style.display = 'none';
  document.removeEventListener('click', _settingsOutsideClick);
  const modal = document.querySelector('.settings-modal');
  if (modal) { modal.style.left = ''; modal.style.top = ''; modal.style.transform = ''; }
}

// ── Settings: shared, stateless DOM-builder helpers (module scope — built once,
// reused across every tab render instead of being redeclared on every render). ──
function section(title) {
  const s = document.createElement('div');
  s.className = 'st-section';
  const h = document.createElement('div');
  h.className = 'st-section-title';
  h.textContent = title;
  s.appendChild(h);
  return s;
}
function row(label, control) {
  const r = document.createElement('div');
  r.className = 'st-row';
  const l = document.createElement('span');
  l.className = 'st-row-label';
  l.textContent = label;
  r.appendChild(l);
  r.appendChild(control);
  return r;
}
function toggle(val, onChange) {
  const btn = document.createElement('button');
  btn.className = 'st-toggle' + (val ? ' on' : '');
  btn.innerHTML = '<span class="st-toggle-knob"></span>';
  btn.addEventListener('click', () => {
    const next = !btn.classList.contains('on');
    btn.classList.toggle('on', next);
    onChange(next);
  });
  return btn;
}
function btnGroup(options, current, onChange) {
  const wrap = document.createElement('div');
  wrap.className = 'st-btn-group';
  options.forEach(({ value, label: lbl }) => {
    const b = document.createElement('button');
    b.className = 'st-group-btn' + (current === value ? ' active' : '');
    b.textContent = lbl;
    b.addEventListener('click', () => {
      wrap.querySelectorAll('.st-group-btn').forEach(x => x.classList.remove('active'));
      b.classList.add('active');
      onChange(value);
    });
    wrap.appendChild(b);
  });
  return wrap;
}
function stColorField(label, currentHex, onChange) {
  const wrap = document.createElement('div');
  wrap.className = 'st-color-field';
  const l = document.createElement('span'); l.className = 'st-row-label'; l.textContent = label;
  const lbl = document.createElement('label'); lbl.style.cssText = 'display:block;cursor:pointer;position:relative;margin-top:6px;';
  const swatch = document.createElement('div'); swatch.className = 'st-color-swatch';
  swatch.style.background = currentHex;
  const picker = document.createElement('input');
  picker.type = 'color'; picker.value = currentHex;
  picker.style.cssText = 'position:absolute;opacity:0;width:0;height:0;';
  const onInput = () => { swatch.style.background = picker.value; onChange(picker.value); };
  picker.addEventListener('input', onInput); picker.addEventListener('change', onInput);
  lbl.appendChild(swatch); lbl.appendChild(picker);
  wrap.appendChild(l); wrap.appendChild(lbl);
  return wrap;
}
function stSliderField(label, min, max, step, current, unit, onChange) {
  const wrap = document.createElement('div');
  wrap.className = 'st-slider-field';
  const top = document.createElement('div');
  top.className = 'st-color-field-top';
  const l = document.createElement('span'); l.className = 'st-row-label'; l.textContent = label;
  const valSpan = document.createElement('span'); valSpan.className = 'se-hex-val'; valSpan.textContent = current + unit;
  top.appendChild(l); top.appendChild(valSpan);
  const slider = document.createElement('input');
  slider.type = 'range'; slider.className = 'se-slider st-slider';
  slider.min = min; slider.max = max; slider.step = step; slider.value = current;
  const updateFill = () => {
    const pct = (slider.value - min) / (max - min) * 100;
    slider.style.background = `linear-gradient(to right, var(--accent-color,#fff) ${pct}%, rgba(255,255,255,0.12) ${pct}%)`;
  };
  updateFill();
  slider.addEventListener('input', () => { valSpan.textContent = slider.value + unit; updateFill(); onChange(+slider.value); });
  wrap.appendChild(top); wrap.appendChild(slider);
  return wrap;
}

const ICON_GENERAL = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="4" y1="6" x2="20" y2="6"/><circle cx="9" cy="6" r="2"/><line x1="4" y1="12" x2="20" y2="12"/><circle cx="15" cy="12" r="2"/><line x1="4" y1="18" x2="20" y2="18"/><circle cx="11" cy="18" r="2"/></svg>';
const ICON_APPEARANCE = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8.5" cy="8.5" r="1.5"/><polyline points="21 15 16 10 5 21"/></svg>';
const ICON_LANGUAGE = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/><line x1="3" y1="12" x2="21" y2="12"/><path d="M12 3a14 14 0 0 1 0 18 14 14 0 0 1 0-18z"/></svg>';
const ICON_ACCOUNT = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="8" r="4"/><path d="M4 21c0-4 4-6 8-6s8 2 8 6"/></svg>';
const ICON_SUPPORT = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/><line x1="12" y1="16" x2="12" y2="11"/><line x1="12" y1="7.5" x2="12" y2="7.51"/></svg>';
const ICON_EXPORT = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>';

const SETTINGS_TABS = [
  { id: 'account',    labelKey: 'st.account',        icon: ICON_ACCOUNT,    render: renderAccountTab },
  { id: 'general',    labelKey: 'st.general',       icon: ICON_GENERAL,    render: renderGeneralTab },
  { id: 'appearance', labelKey: 'st.appearance',     icon: ICON_APPEARANCE, render: renderAppearanceTab },
  { id: 'language',   labelKey: 'st.languageRegion', icon: ICON_LANGUAGE,   render: renderLanguageTab },
  { id: 'support',    labelKey: 'st.support',        icon: ICON_SUPPORT,    render: renderSupportTab },
];

function renderSettingsNav() {
  const nav = document.getElementById('settingsNav');
  if (!nav) return;
  nav.innerHTML = '';
  SETTINGS_TABS.forEach(tab => {
    const btn = document.createElement('button');
    btn.className = 'settings-nav-item' + (tab.id === _settingsActiveTab ? ' active' : '');
    btn.innerHTML = `${tab.icon}<span>${T(tab.labelKey)}</span>`;
    btn.addEventListener('click', () => {
      if (_settingsActiveTab === tab.id) return;
      _settingsActiveTab = tab.id;
      nav.querySelectorAll('.settings-nav-item').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      renderSettingsTabContent();
    });
    nav.appendChild(btn);
  });
  const exportBtn = document.createElement('button');
  exportBtn.className = 'settings-nav-item settings-nav-export';
  exportBtn.innerHTML = `${ICON_EXPORT}<span>${T('st.download')}</span>`;
  exportBtn.addEventListener('click', exportUserData);
  nav.appendChild(exportBtn);
}

function renderSettingsTabContent() {
  const body = document.getElementById('settingsBody');
  if (!body) return;
  body.innerHTML = '';
  const tab = SETTINGS_TABS.find(t => t.id === _settingsActiveTab) || SETTINGS_TABS[0];
  tab.render(body);
  body.scrollTop = 0;
}

// ── General: Behavior + Layout + Sidebar ──
function renderGeneralTab(body) {
  const behSec = section(T('st.behavior'));
  behSec.appendChild(row(T('st.openNewTab'), toggle(
    S.openInNewTab !== false,
    val => {
      S.openInNewTab = val;
      saveState();
      document.querySelectorAll('a.link-item').forEach(a => { a.target = val ? '_blank' : '_self'; });
    }
  )));

  // Hide extra bookmarks with count sub-option
  const hideRow = document.createElement('div');
  hideRow.className = 'st-row';
  const hideLabel = document.createElement('span');
  hideLabel.className = 'st-row-label';
  hideLabel.textContent = T('st.hideExtra');
  const hideRight = document.createElement('div');
  hideRight.style.cssText = 'display:flex;align-items:center;gap:8px;';
  const countSel = document.createElement('select');
  countSel.className = 'st-select';
  countSel.style.display = S.hideExtraBookmarks ? '' : 'none';
  [5, 10, 15, 20].forEach(n => {
    const opt = document.createElement('option');
    opt.value = n; opt.textContent = T('st.showN', { n });
    if (n === S.maxBookmarksShown) opt.selected = true;
    countSel.appendChild(opt);
  });
  countSel.addEventListener('change', () => {
    S.maxBookmarksShown = +countSel.value; saveState(); renderBoards();
  });
  const hideToggle = toggle(!!S.hideExtraBookmarks, val => {
    S.hideExtraBookmarks = val;
    countSel.style.display = val ? '' : 'none';
    saveState(); renderBoards();
  });
  hideRight.appendChild(countSel);
  hideRight.appendChild(hideToggle);
  hideRow.appendChild(hideLabel);
  hideRow.appendChild(hideRight);
  behSec.appendChild(hideRow);

  behSec.appendChild(row(T('st.showDescriptions'), toggle(
    !!S.showDescriptions,
    val => { S.showDescriptions = val; applyDescriptionsMode(); saveState(); }
  )));
  body.appendChild(behSec);

  // ── Layout ──
  const layoutSec = section(T('st.layout'));

  // Max board columns (Auto = fit to window width). The <select> always shows
  // the EFFECTIVE column count (capped to what the window fits), never a chosen
  // number that can't physically be displayed.
  const colsSel = document.createElement('select');
  colsSel.className = 'st-select';
  const autoOpt = document.createElement('option');
  autoOpt.value = 'auto'; autoOpt.textContent = T('st.colsAuto');
  if (!S.maxBoardCols) autoOpt.selected = true;
  colsSel.appendChild(autoOpt);
  const effSel = S.maxBoardCols ? Math.min(S.maxBoardCols, getLayoutParams().autoCols) : null;
  [4, 5, 6, 7, 8, 9].forEach(n => {
    const opt = document.createElement('option');
    opt.value = n; opt.textContent = n;
    if (n === effSel) opt.selected = true;
    colsSel.appendChild(opt);
  });

  // One-off hint: shown only right after the user picks a count that can't fit
  // the current window. It is NOT restored when settings reopen — it's a passing
  // notice, not a persistent warning. Dismissable, and re-appears on a new pick.
  const colsHint = document.createElement('div');
  colsHint.className = 'st-field-hint';
  colsHint.style.display = 'none';
  const colsHintText = document.createElement('span');
  const colsHintClose = document.createElement('button');
  colsHintClose.className = 'st-field-hint-close';
  colsHintClose.textContent = '×';
  colsHintClose.setAttribute('aria-label', T('common.close'));
  colsHintClose.addEventListener('click', () => { colsHint.style.display = 'none'; });
  colsHint.appendChild(colsHintText);
  colsHint.appendChild(colsHintClose);
  // Re-snap the visible selection + hint to what the current width can fit.
  // showHint=true only right after an explicit column pick (a passing notice).
  const refreshColsDisplay = (showHint) => {
    const fits = getLayoutParams().autoCols;
    if (S.maxBoardCols) colsSel.value = String(Math.min(S.maxBoardCols, fits));
    if (showHint && S.maxBoardCols && S.maxBoardCols > fits) {
      colsHintText.textContent = T('st.colsHint', { n: fits });
      colsHint.style.display = '';
    } else {
      colsHint.style.display = 'none';
    }
  };
  colsSel.addEventListener('change', () => {
    S.maxBoardCols = colsSel.value === 'auto' ? null : +colsSel.value;
    saveState(); renderBoards();
    refreshColsDisplay(true);
    bwSync();   // the fit-cap changed → refresh the width slider's ceiling
  });
  layoutSec.appendChild(row(T('st.boardColumns'), colsSel));
  layoutSec.appendChild(colsHint);

  // Board width — independent of the column count. The slider's MAX is the widest
  // the current column count actually allows (the fit-cap), so the handle can't be
  // dragged past the point where boards stop growing and the px readout stays true.
  // S.boardWidth keeps the desired value, so fewer columns auto-widen the boards.
  const BW_MIN = 190, BW_AUTO_MAX = 380;
  const bwWrap = document.createElement('div');
  bwWrap.className = 'st-slider-field';
  const bwTop = document.createElement('div');
  bwTop.className = 'st-color-field-top';
  const bwLabel = document.createElement('span');
  bwLabel.className = 'st-row-label'; bwLabel.textContent = T('st.boardWidth');
  const bwVal = document.createElement('span');
  bwVal.className = 'se-hex-val';
  bwTop.appendChild(bwLabel); bwTop.appendChild(bwVal);
  const bwSlider = document.createElement('input');
  // step=1 so the max (an arbitrary fit-cap like 329px) is always reachable and
  // the handle travels the full track — a step of 10 would stop short of a
  // non-round max, leaving a visible gap at the right end.
  bwSlider.type = 'range'; bwSlider.className = 'se-slider st-slider';
  bwSlider.min = BW_MIN; bwSlider.step = 1;
  const bwFill = () => {
    const min = +bwSlider.min, max = +bwSlider.max;
    const pct = max > min ? (bwSlider.value - min) / (max - min) * 100 : 100;
    bwSlider.style.background = `linear-gradient(to right, var(--accent-color,#fff) ${pct}%, rgba(255,255,255,0.12) ${pct}%)`;
  };
  // Reconcile the slider with the current fit-cap (called on open + on column change).
  function bwSync() {
    const cap = S.maxBoardCols ? getLayoutParams().fitW : BW_AUTO_MAX;
    bwSlider.max = Math.max(BW_MIN, cap);
    const shown = Math.min(S.boardWidth || 260, +bwSlider.max);
    bwSlider.value = shown;
    bwVal.textContent = shown + 'px';
    bwFill();
  }
  bwSlider.addEventListener('input', () => {
    S.boardWidth = +bwSlider.value;
    bwVal.textContent = bwSlider.value + 'px';
    bwFill();
    saveState(); renderBoards(); refreshColsDisplay(false);
  });
  bwWrap.appendChild(bwTop); bwWrap.appendChild(bwSlider);
  bwSync();
  layoutSec.appendChild(bwWrap);
  body.appendChild(layoutSec);

  // ── Sidebar ──
  const sbSec = section(T('st.sidebar'));
  sbSec.appendChild(row(T('st.alwaysShow'), toggle(
    !!S.sidebarAlwaysExpanded,
    val => { S.sidebarAlwaysExpanded = val; applySidebarMode(); saveState(); }
  )));
  body.appendChild(sbSec);

  // ── Quick Save ──
  const qsSec = section(T('st.quickSave'));
  const qsBoards = S.boards.filter(b => b.type !== 'calendar' && b.type !== 'pomodoro' && b.type !== 'notes' && b.type !== 'search');
  if (qsBoards.length) {
    const qsPages = S.pages || [];
    const curQsBoard = qsBoards.find(b => b.id === S.quickSaveBoard);
    let curQsPageId = curQsBoard?.pageId || qsPages[0]?.id;

    if (qsPages.length > 1) {
      const pageRow = document.createElement('div');
      pageRow.className = 'st-row';
      const pageLabel = document.createElement('span');
      pageLabel.className = 'st-row-label';
      pageLabel.textContent = T('st.saveToPage');
      const pageSel = document.createElement('select');
      pageSel.className = 'st-select';
      qsPages.forEach(p => {
        const opt = document.createElement('option');
        opt.value = p.id; opt.textContent = p.name;
        if (p.id === curQsPageId) opt.selected = true;
        pageSel.appendChild(opt);
      });
      pageRow.appendChild(pageLabel);
      pageRow.appendChild(pageSel);
      qsSec.appendChild(pageRow);

      const boardRow = document.createElement('div');
      boardRow.className = 'st-row';
      const boardLabel = document.createElement('span');
      boardLabel.className = 'st-row-label';
      boardLabel.textContent = T('st.saveToBoard');
      const boardSel = document.createElement('select');
      boardSel.className = 'st-select';

      function refreshQsBoards(pageId) {
        boardSel.innerHTML = '';
        qsBoards.filter(b => b.pageId === pageId).forEach(b => {
          const opt = document.createElement('option');
          opt.value = b.id; opt.textContent = b.name;
          if (b.id === S.quickSaveBoard) opt.selected = true;
          boardSel.appendChild(opt);
        });
        if (!S.quickSaveBoard || !boardSel.querySelector(`[value="${S.quickSaveBoard}"]`)) {
          S.quickSaveBoard = boardSel.value; saveState();
        }
      }
      refreshQsBoards(curQsPageId);
      pageSel.addEventListener('change', () => { refreshQsBoards(pageSel.value); });
      boardSel.addEventListener('change', () => { S.quickSaveBoard = boardSel.value; saveState(); });
      boardRow.appendChild(boardLabel);
      boardRow.appendChild(boardSel);
      qsSec.appendChild(boardRow);
    } else {
      const destRow = document.createElement('div');
      destRow.className = 'st-row';
      const destLabel = document.createElement('span');
      destLabel.className = 'st-row-label';
      destLabel.textContent = T('st.saveToBoard');
      const destSel = document.createElement('select');
      destSel.className = 'st-select';
      qsBoards.forEach(b => {
        const opt = document.createElement('option');
        opt.value = b.id; opt.textContent = b.name;
        if (b.id === S.quickSaveBoard || (!S.quickSaveBoard && b === qsBoards[0])) opt.selected = true;
        destSel.appendChild(opt);
      });
      if (!S.quickSaveBoard) { S.quickSaveBoard = qsBoards[0].id; saveState(); }
      destSel.addEventListener('change', () => { S.quickSaveBoard = destSel.value; saveState(); });
      destRow.appendChild(destLabel);
      destRow.appendChild(destSel);
      qsSec.appendChild(destRow);
    }
  }
  const popupRow = document.createElement('div');
  popupRow.className = 'st-row';
  popupRow.innerHTML = `<span class="st-row-label">${T('st.shortcut')}</span>`;
  const popupRight = document.createElement('div');
  popupRight.style.cssText = 'display:flex;align-items:center;gap:8px;';
  const popupKbd = document.createElement('kbd');
  popupKbd.className = 'st-kbd';
  popupKbd.textContent = T('st.notSet');
  if (typeof chrome !== 'undefined' && chrome.commands?.getAll) {
    try {
      chrome.commands.getAll(cmds => {
        const cmd = (cmds || []).find(c => c.name === '_execute_action');
        popupKbd.textContent = cmd?.shortcut || T('st.notSet');
      });
    } catch (_) {}
  }
  const changeBtn = document.createElement('button');
  changeBtn.className = 'st-action-btn';
  changeBtn.style.cssText = 'width:auto;padding:4px 10px;margin:0;font-size:11px;';
  changeBtn.textContent = T('st.change');
  changeBtn.addEventListener('click', () => { chrome.tabs.create({ url: 'chrome://extensions/shortcuts' }); });
  popupRight.appendChild(popupKbd);
  popupRight.appendChild(changeBtn);
  popupRow.appendChild(popupRight);
  qsSec.appendChild(popupRow);
  body.appendChild(qsSec);
}

// ── Appearance: Board + Search bar + Text ──
function renderAppearanceTab(body) {
  const ts = S.themeStyle;

  const applyAndSave = () => {
    applyThemeStyle(S.themeStyle);
    // Persist style changes to the active wallpaper history entry
    if (S.currentWallId) {
      const entry = (S.wallpaperHistory || []).find(h => h.id === S.currentWallId);
      if (entry) entry.themeStyle = JSON.parse(JSON.stringify(S.themeStyle));
    }
    saveState();
  };

  // ── Board ──
  const boardSec = section(T('st.board'));
  const colorPair = document.createElement('div');
  colorPair.className = 'st-color-pair';
  colorPair.appendChild(stColorField(T('st.primaryColor'), ts.accentHex || '#ffffff',
    v => { S.themeStyle.accentHex = v; applyAndSave(); }));
  colorPair.appendChild(stColorField(T('st.boardColor'), ts.boardColorHex || '#ffffff',
    v => { S.themeStyle.boardColorHex = v; applyAndSave(); }));
  boardSec.appendChild(colorPair);
  boardSec.appendChild(stSliderField(T('st.opacity'), 0, 100, 1, ts.boardOpacity ?? 5, '%',
    v => { S.themeStyle.boardOpacity = v; applyAndSave(); }));
  boardSec.appendChild(stSliderField(T('st.blur'), 0, 40, 1, ts.boardBlur ?? 12, 'px',
    v => { S.themeStyle.boardBlur = v; applyAndSave(); }));

  // Кнопки Cancel / Reset
  const styleBtnRow = document.createElement('div');
  styleBtnRow.style.cssText = 'display:flex;gap:8px;margin-top:4px;';
  const cancelStyleBtn = document.createElement('button');
  cancelStyleBtn.className = 'st-btn';
  cancelStyleBtn.textContent = T('common.cancel');
  cancelStyleBtn.addEventListener('click', e => {
    e.stopPropagation();
    if (_settingsInitStyle) { S.themeStyle = JSON.parse(JSON.stringify(_settingsInitStyle)); applyThemeStyle(S.themeStyle); saveState(); renderSettingsTabContent(); }
  });
  const resetStyleBtn = document.createElement('button');
  resetStyleBtn.className = 'st-btn';
  resetStyleBtn.textContent = T('common.reset');
  resetStyleBtn.addEventListener('click', e => {
    e.stopPropagation();
    S.themeStyle = { ...S.themeStyle, boardColorHex: '#ffffff', boardOpacity: 5, boardBlur: 12, accentHex: '#ffffff' };
    applyThemeStyle(S.themeStyle); saveState(); renderSettingsTabContent();
  });
  styleBtnRow.appendChild(cancelStyleBtn); styleBtnRow.appendChild(resetStyleBtn);
  boardSec.appendChild(styleBtnRow);
  body.appendChild(boardSec);

  // ── Search bar ── independent color/opacity/blur/size, inherited from the
  // board style (colorPair/opacity/blur above) until the user overrides it here.
  const nsbSec = section(T('st.searchBar'));
  const nsbColorPair = document.createElement('div');
  nsbColorPair.className = 'st-color-pair';
  nsbColorPair.appendChild(stColorField(T('st.searchBarColor'), ts.navSearchColorHex ?? ts.boardColorHex ?? '#ffffff',
    v => { S.themeStyle.navSearchColorHex = v; applyAndSave(); }));
  nsbSec.appendChild(nsbColorPair);
  nsbSec.appendChild(stSliderField(T('st.opacity'), 0, 100, 1, ts.navSearchOpacity ?? ts.boardOpacity ?? 5, '%',
    v => { S.themeStyle.navSearchOpacity = v; applyAndSave(); }));
  nsbSec.appendChild(stSliderField(T('st.blur'), 0, 40, 1, ts.navSearchBlur ?? ts.boardBlur ?? 12, 'px',
    v => { S.themeStyle.navSearchBlur = v; applyAndSave(); }));
  nsbSec.appendChild(stSliderField(T('st.searchBarWidth'), 240, 480, 10, ts.navSearchWidth ?? 340, 'px',
    v => { S.themeStyle.navSearchWidth = v; applyAndSave(); requestAnimationFrame(syncLayout); }));
  const nsbMatchBtn = document.createElement('button');
  nsbMatchBtn.className = 'st-link';
  nsbMatchBtn.textContent = T('st.matchBoardStyle');
  nsbMatchBtn.addEventListener('click', e => {
    e.stopPropagation();
    ['navSearchColorHex', 'navSearchOpacity', 'navSearchBlur', 'navSearchWidth']
      .forEach(k => delete S.themeStyle[k]);
    applyAndSave(); renderSettingsTabContent(); requestAnimationFrame(syncLayout);
  });
  nsbSec.appendChild(nsbMatchBtn);
  body.appendChild(nsbSec);

  // ── Text ──
  const textSec = section(T('st.boardText'));
  textSec.appendChild(row(T('st.size'), btnGroup(
    [{ value: 0.9, label: 'S' }, { value: 1, label: 'M' }, { value: 1.15, label: 'L' }],
    ts.textScale ?? 1, v => { S.themeStyle.textScale = v; applyAndSave(); })));
  textSec.appendChild(row(T('st.weight'), btnGroup(
    [{ value: false, label: T('common.normal') }, { value: true, label: T('common.bold') }],
    ts.textBold ?? false, v => { S.themeStyle.textBold = v; applyAndSave(); })));
  body.appendChild(textSec);

  // ── Board accents: bulk outline ── touching color or opacity here turns
  // the outline on for every board that doesn't already have one (skipping
  // only ones customized by hand from their own "Customize" menu — no
  // accentBulk marker, see setColor/shapeBtn in buildAccentPanel) and keeps
  // it live from then on: every board this feature controls is driven by
  // the shared --board-outline-theme-color CSS var (see applyThemeStyle),
  // so further changes show up on them immediately, no "apply" step. Leave
  // the color override unset and that var tracks the board's own color
  // live — the same inherit-until-overridden pattern as "Match board
  // style" above; pick a color and it becomes a fixed override (still fed
  // through the same live var), and "Match board style" clears it again.
  const accentSec = section(T('st.bulkOutline'));

  // One-time migration off the old manual/auto toggle so returning users
  // keep whatever they'd picked.
  if (S.outlineColorOverride === undefined) {
    S.outlineColorOverride = (S.bulkOutlineMode === 'manual' && S.bulkOutlineColor) ? S.bulkOutlineColor : null;
  }
  if (S.outlineOpacityOverride === undefined) {
    S.outlineOpacityOverride = (S.bulkOutlineMode === 'manual') ? 100 : null;
  }

  // Flips each board's flags at most once (cheap, and idempotent after
  // that) and only rebuilds the board grid (renderBoards) the first time
  // it actually changes something — every later color/opacity tweak, even
  // fired continuously while dragging inside the color picker, just
  // updates the live CSS var via applyThemeStyle instead of re-rendering
  // every board on each tick.
  function enableBulkOutline() {
    let changed = false;
    (S.boards || []).forEach(bd => {
      if (bd.accentOutline && !bd.accentBulk) return;
      if (!bd.accentOutline || !bd.accentBulk || !bd.accentTheme) changed = true;
      bd.accentOutline = true;
      bd.accentBulk = true;
      bd.accentTheme = true;
    });
    applyThemeStyle(S.themeStyle); saveState();
    if (changed) renderBoards();
  }

  // The color/opacity fields are rebuilt only when the override is reset
  // (values need to jump back to the theme-derived fallback) — never on
  // their own input events, since that would tear down and recreate the
  // native <input type="color"> mid-drag and cancel the pick.
  // Undoes only what this section itself turned on (b.accentBulk) — a board
  // customized by hand from its own "Customize" menu never carries that
  // marker (see setColor/shapeBtn in buildAccentPanel), so it's untouched.
  function removeBulkOutline() {
    let changed = false;
    (S.boards || []).forEach(bd => {
      if (bd.accentBulk && bd.accentOutline) { bd.accentOutline = false; changed = true; }
    });
    saveState();
    if (changed) renderBoards();
  }

  const accentBody = document.createElement('div');
  const renderAccentBody = () => {
    accentBody.innerHTML = '';

    const linkRow = document.createElement('div');
    linkRow.style.cssText = 'display:flex;justify-content:space-between;align-items:center;margin-top:2px;';

    const matchBtn = document.createElement('button');
    matchBtn.type = 'button';
    matchBtn.className = 'st-link';
    matchBtn.textContent = T('st.matchBoardStyle');
    matchBtn.addEventListener('click', e => {
      e.stopPropagation();
      S.outlineColorOverride = null; S.outlineOpacityOverride = null;
      enableBulkOutline(); renderAccentBody();
    });

    const resetBtn = document.createElement('button');
    resetBtn.type = 'button';
    resetBtn.className = 'st-link';
    resetBtn.textContent = T('st.outlineRemoveAll');
    resetBtn.addEventListener('click', e => {
      e.stopPropagation();
      removeBulkOutline();
    });

    linkRow.appendChild(matchBtn);
    linkRow.appendChild(resetBtn);

    const opacityField = stSliderField(T('st.opacity'), 0, 100, 1, outlineEffectiveOpacityPct(), '%', v => {
      S.outlineOpacityOverride = v; enableBulkOutline();
    });
    const opacitySlider = opacityField.querySelector('input[type=range]');
    const opacityVal = opacityField.querySelector('.se-hex-val');

    accentBody.appendChild(stColorField(T('st.outlineColor'), outlineEffectiveHex(), v => {
      S.outlineColorOverride = v; enableBulkOutline();
      // Picking a color for the first time snaps the still-untouched opacity
      // slider to its new solid default, so what's shown matches what's
      // actually now painted on the boards — never leaves it displaying a
      // stale value from the theme-tracked default.
      if (S.outlineOpacityOverride == null) {
        const pct = outlineEffectiveOpacityPct();
        opacitySlider.value = pct;
        opacityVal.textContent = pct + '%';
        opacitySlider.style.background = `linear-gradient(to right, var(--accent-color,#fff) ${pct}%, rgba(255,255,255,0.12) ${pct}%)`;
      }
    }));
    accentBody.appendChild(opacityField);
    accentBody.appendChild(linkRow);
  };
  renderAccentBody();
  accentSec.appendChild(accentBody);

  body.appendChild(accentSec);
}

// ── Language & Region: Language + Formatting ──
function renderLanguageTab(body) {
  const langSec = section(T('st.language'));
  langSec.appendChild(btnGroup(
    I18N.SUPPORTED.map(code => ({ value: code, label: I18N.localeLabel(code) })),
    I18N.lang,
    val => { if (val !== I18N.lang) I18N.setLang(val); } // setLang persists + reloads
  ));
  body.appendChild(langSec);

  const fmtSec = section(T('st.formatting'));
  const loc = S.locale;

  const autoRow = document.createElement('div');
  autoRow.className = 'st-row';
  const autoBtn = document.createElement('button');
  autoBtn.className = 'st-btn';
  autoBtn.textContent = T('st.autoDetect');
  autoBtn.addEventListener('click', e => {
    e.stopPropagation();
    S.locale = detectLocale();
    saveState();
    renderBoards();
    renderWeatherWidget();
    renderSettingsTabContent();
  });
  autoRow.appendChild(autoBtn);
  fmtSec.appendChild(autoRow);

  fmtSec.appendChild(row(T('st.timeFormat'), btnGroup(
    [{ value: '24h', label: '24h' }, { value: '12h', label: '12h AM/PM' }],
    loc.timeFormat || '24h',
    val => { loc.timeFormat = val; saveState(); tickClock(); }
  )));
  fmtSec.appendChild(row(T('st.dateFormat'), btnGroup(
    [{ value: 'DMY', label: 'DD/MM/YY' }, { value: 'MDY', label: 'MM/DD/YY' }, { value: 'YMD', label: 'YY-MM-DD' }],
    loc.dateFormat || 'DMY',
    val => { loc.dateFormat = val; saveState(); tickClock(); }
  )));
  fmtSec.appendChild(row(T('st.weekStart'), btnGroup(
    [{ value: 1, label: T('st.monday') }, { value: 0, label: T('st.sunday') }],
    loc.weekStart ?? 1,
    val => { loc.weekStart = val; saveState(); renderBoards(); }
  )));
  fmtSec.appendChild(row(T('st.temperature'), btnGroup(
    [{ value: 'metric', label: '°C' }, { value: 'imperial', label: '°F' }],
    loc.tempUnit || 'metric',
    val => { loc.tempUnit = val; saveState(); renderWeatherWidget(); }
  )));
  body.appendChild(fmtSec);
}

// ── Account ──
function renderAccountTab(body) {
  const accSec = section(T('st.account'));
  const note = document.createElement('p');
  note.className = 'st-plan-guest';
  note.style.margin = '0';
  note.textContent = 'NovaTab работает в локальном режиме. Данные хранятся только на вашем устройстве.';
  accSec.appendChild(note);
  body.appendChild(accSec);
}

// ── Support ──
function renderSupportTab(body) {
  const supSec = section(T('st.support'));
  const appVersion = chrome.runtime?.getManifest?.().version || '1.3.0';
  const versionRow = document.createElement('div');
  versionRow.className = 'st-row';
  versionRow.innerHTML = `<span class="st-row-label">${T('st.version')}</span><span class="st-row-value">${appVersion}</span>`;
  supSec.appendChild(versionRow);
  const contactRow = document.createElement('div');
  contactRow.className = 'st-row';
  contactRow.innerHTML = `<span class="st-row-label">${T('st.contact')}</span><a class="st-link" href="mailto:markmezapp@gmail.com">markmezapp@gmail.com</a>`;
  supSec.appendChild(contactRow);
  body.appendChild(supSec);
}

function applySidebarMode() {
  document.getElementById('sidebar').classList.toggle('always-open', !!S.sidebarAlwaysExpanded);
}
function applyDescriptionsMode() {
  document.body.classList.toggle('show-descriptions', !!S.showDescriptions);
}

document.getElementById('settingsCloseBtn').addEventListener('click', closeSettingsModal);
document.getElementById('settingsSideBtn').addEventListener('click', e => {
  e.stopPropagation();
  if (_settingsOpen) {
    closeSettingsModal();
  } else {
    closeSidebar(); openSettingsModal();
  }
});

// ── Резервная база цитат (на случай оффлайна или падения API) ──
const FALLBACK_QUOTES = [
  { text: "В минуту нерешительности действуй быстро и старайся сделать первый шаг, хотя бы и лишний.", author: "Лев Толстой" },
  { text: "Никогда не ошибается тот, кто ничего не делает.", author: "Теодор Рузвельт" },
  { text: "Сложнее всего начать действовать, все остальное зависит только от упорства.", author: "Амелия Эрхарт" },
  { text: "То, что мы знаем, — это капля, а то, чего мы не знаем, — это океан.", author: "Исаак Ньютон" },
  { text: "Успех — это способность шагать от одной неудачи к другой, не теряя энтузиазма.", author: "Уинстон Черчилль" }
];

async function renderRandomQuote() {
  const quoteText = document.getElementById('quoteText');
  const quoteAuthor = document.getElementById('quoteAuthor');
  const widget = document.getElementById('quoteWidget');
  
  if (!quoteText || !quoteAuthor || !widget) return;

  // Изначально скрываем виджет, чтобы текст не "прыгал" при загрузке
  widget.style.opacity = '0'; 

  // По умолчанию берем резервную цитату
  let finalQuote = FALLBACK_QUOTES[Math.floor(Math.random() * FALLBACK_QUOTES.length)];

  try {
    // Делаем запрос к Forismatic API (lang=ru)
    const response = await fetch('https://api.forismatic.com/api/1.0/?method=getQuote&format=json&lang=ru', {
      cache: 'no-store'
    });
    
    if (response.ok) {
      const textData = await response.text();
      // Forismatic иногда отдает JSON с неэкранированными кавычками. 
      const cleanJson = textData.replace(/\\'/g, "'").replace(/\n/g, " "); 
      const data = JSON.parse(cleanJson);
      
      if (data.quoteText) {
        finalQuote = {
          text: data.quoteText.trim(),
          author: data.quoteAuthor ? data.quoteAuthor.trim() : "Неизвестный автор"
        };
      }
    }
  } catch (err) {
    console.warn('[NovaTab] API цитат недоступно (оффлайн режим).', err.message);
  }

  // Применяем текст в DOM
  quoteText.textContent = `«${finalQuote.text}»`;
  quoteAuthor.textContent = finalQuote.author;
  
  // Плавно проявляем виджет
  widget.style.opacity = '0.85';
}

// ── Init ──
async function init() {
  await loadState();
  await loadFaviconCache();
  renderAll();
  loadSavedWallpaper();
  updateFocusStats();
  applySidebarMode();
  applyDescriptionsMode();
  checkOnboarding();
  startClock();
  window.addEventListener('resize', updateNavLayout);
  syncNavSearchCard();
  syncClockCard();
  renderWeatherWidget();
  syncWeatherCard();
  if (S.weather?.enabled) fetchWeatherData();

  document.getElementById('weatherWidget')?.addEventListener('click', showWeatherPopup);

  renderRandomQuote();
  document.getElementById('quoteWidget')?.addEventListener('click', renderRandomQuote);
}
init();

// ── Onboarding Tour ──
const TOUR_STEPS = [
  {
    target: null,
    title: T('tour.createBoardTitle'),
    desc: T('tour.createBoardDesc'),
    pos: 'center', revealCreate: true
  },
  {
    // Есть доска → подсветим «+» на ней; нет доски → элемент не найдётся и шаг
    // автоматически станет центрированной подсказкой.
    target: '[data-tour="add-link"]',
    title: T('tour.addTitle'),
    desc: T('tour.addDesc'),
    pos: 'bottom', shape: 'circle'
  },
  {
    target: '[data-tour="add-page"]',
    title: T('tour.pagesTitle'),
    desc: T('tour.pagesDesc'),
    pos: 'bottom', shape: 'circle'
  },
  {
    target: '#menuSideBtn',
    title: T('tour.menuTitle'),
    desc: T('tour.menuDesc'),
    pos: 'left', shape: 'circle'
  },
  {
    target: '#settingsSideBtn',
    title: T('tour.settingsTitle'),
    desc: T('tour.settingsDesc'),
    pos: 'left', shape: 'circle'
  },
  {
    target: null,
    title: T('tour.saveTitle'),
    desc: T('tour.saveDesc'),
    pos: 'center'
  },
  {
    target: null,
    title: T('tour.bringTitle'),
    desc: T('tour.bringDesc'),
    pos: 'center', cta: 'import'
  },
  {
    target: null,
    title: T('tour.dragTitle'),
    desc: T('tour.dragDesc'),
    pos: 'center', demo: 'drag'
  },
  {
    target: null,
    title: T('tour.doneTitle'),
    desc: T('tour.doneDesc'),
    pos: 'center'
  }
];

let _tourStep = 0;
let _tourHighlighted = null;
// Active steps for the current run — may exclude the import/drag steps when
// the user has no Chrome bookmarks to import.
let _tourSteps = TOUR_STEPS;

function hasChromeBookmarks() {
  return new Promise(resolve => {
    if (!chrome?.bookmarks?.getTree) { resolve(false); return; }
    chrome.bookmarks.getTree(tree => {
      let found = false;
      (function walk(nodes) {
        for (const n of nodes || []) {
          if (found) return;
          if (n.url) { found = true; return; }
          if (n.children) walk(n.children);
        }
      })(tree?.[0]?.children || []);
      resolve(found);
    });
  });
}

function startTour() {
  if (localStorage.getItem('mz-tour-done')) return;
  _tourStep = 0;
  // No bookmarks to import → drop only the import step, but keep the drag demo.
  hasChromeBookmarks().then(has => {
    _tourSteps = has ? TOUR_STEPS : TOUR_STEPS.filter(s => !s.cta);
    document.getElementById('tourOverlay').style.display = '';
    document.getElementById('tourTooltip').style.display = '';
    showTourStep(0);
  });
}

function _clearTourRects() {
  document.querySelectorAll('.tour-overlay-rect').forEach(d => d.remove());
}

function setTourOverlayMask(el, shape, customBounds) {
  const overlay = document.getElementById('tourOverlay');
  _clearTourRects();
  overlay.style.background = '';
  overlay.style.mask = overlay.style.webkitMask = '';

  if (!el) return;

  const r = customBounds || el.getBoundingClientRect();
  const W = window.innerWidth, H = window.innerHeight;

  if (shape === 'circle') {
    const cx = r.left + r.width / 2;
    const cy = r.top + r.height / 2;
    const radius = Math.max(r.width, r.height) / 2 + 18;
    overlay.style.background = `radial-gradient(circle ${radius}px at ${cx}px ${cy}px, transparent ${radius - 1}px, rgba(0,0,0,0.55) ${radius}px)`;
  } else {
    const pad = shape === 'large' ? 24 : 14;
    const x1 = Math.max(0, r.left - pad),   y1 = Math.max(0, r.top - pad);
    const x2 = Math.min(W, r.right + pad),  y2 = Math.min(H, r.bottom + pad);
    const dark = 'rgba(0,0,0,0.55)';
    [
      { t: 0,  l: 0,  w: W,      h: y1        },
      { t: y2, l: 0,  w: W,      h: H - y2    },
      { t: y1, l: 0,  w: x1,     h: y2 - y1   },
      { t: y1, l: x2, w: W - x2, h: y2 - y1   },
    ].forEach(({ t, l, w, h }) => {
      if (w <= 0 || h <= 0) return;
      const d = document.createElement('div');
      d.className = 'tour-overlay-rect';
      d.style.cssText = `position:fixed;z-index:800;pointer-events:none;background:${dark};top:${t}px;left:${l}px;width:${w}px;height:${h}px;`;
      document.body.appendChild(d);
    });
  }
}

function showTourStep(idx) {
  const step = _tourSteps[idx];
  const el = step.target ? document.querySelector(step.target) : null;

  // Первый шаг: подсветить все способы создать доску (FAB + «+»-слоты).
  document.body.classList.toggle('tour-show-create', !!step.revealCreate);

  if (_tourHighlighted) {
    _tourHighlighted.classList.remove('tour-spotlight');
    _tourHighlighted.closest?.('.board')?.classList.remove('tour-board-reveal');
  }
  if (el) {
    el.classList.add('tour-spotlight');
    el.closest?.('.board')?.classList.add('tour-board-reveal');
    _tourHighlighted = el;
  } else { _tourHighlighted = null; }
  setTourOverlayMask(step.noSpotlight ? null : el, step.shape, step.getBounds?.());
  // Шаг с подсветкой слотов — без затемнения экрана (слоты видно на самих обоях).
  if (step.revealCreate) document.getElementById('tourOverlay').style.background = 'transparent';

  document.getElementById('tourStepLabel').textContent = T('tour.counter', { i: idx + 1, total: _tourSteps.length });
  document.getElementById('tourTitle').textContent = step.title;
  document.getElementById('tourDesc').textContent = step.desc;
  const nextBtn = document.getElementById('tourNextBtn');
  nextBtn.textContent = step.cta === 'import' ? T('tour.importBookmarks')
    : (idx === _tourSteps.length - 1 ? T('tour.done') : T('tour.next'));
  document.getElementById('tourLaterBtn').style.display = step.cta === 'import' ? '' : 'none';
  document.getElementById('tourBackBtn').style.display = (idx > 0 && step.cta !== 'import') ? '' : 'none';
  document.querySelector('.tour-footer').classList.toggle('cta', step.cta === 'import');

  // Self-contained drag illustration lives inside the tooltip (no dependency
  // on real board layout/scroll position).
  document.getElementById('tourDemo').style.display = step.demo === 'drag' ? '' : 'none';

  positionTourTooltip(el, step.pos);
}

function positionTourTooltip(el, pos) {
  const tooltip = document.getElementById('tourTooltip');
  if (!el || pos === 'center') {
    tooltip.style.top = '50%';
    tooltip.style.left = '50%';
    tooltip.style.transform = 'translate(-50%, -50%)';
    return;
  }
  const r = el.getBoundingClientRect();
  tooltip.style.transform = '';
  if (pos === 'right') {
    tooltip.style.top = Math.max(16, r.top + r.height / 2 - 80) + 'px';
    tooltip.style.left = (r.right + 16) + 'px';
  } else if (pos === 'left') {
    const rawTop = r.top + r.height / 2 - 80;
    tooltip.style.top = Math.min(Math.max(16, rawTop), window.innerHeight - 220) + 'px';
    tooltip.style.left = Math.max(16, r.left - 296) + 'px';
  } else if (pos === 'bottom') {
    tooltip.style.top = (r.bottom + 12) + 'px';
    tooltip.style.left = Math.min(r.left, window.innerWidth - 300) + 'px';
  } else if (pos === 'top') {
    // Над элементом (для нижних кнопок вроде «+ New board» в левом нижнем углу).
    const h = tooltip.offsetHeight || 180;
    tooltip.style.top = Math.max(16, r.top - 12 - h) + 'px';
    tooltip.style.left = Math.min(Math.max(16, r.left), window.innerWidth - 300) + 'px';
  }
}

function endTour(reason = 'skipped') {
  localStorage.setItem('mz-tour-done', '1');
  document.body.classList.remove('tour-show-create');
  document.getElementById('tourOverlay').style.display = 'none';
  document.getElementById('tourTooltip').style.display = 'none';
  document.getElementById('tourDemo').style.display = 'none';
  if (_tourHighlighted) {
    _tourHighlighted.classList.remove('tour-spotlight');
    _tourHighlighted.closest?.('.board')?.classList.remove('tour-board-reveal');
    _tourHighlighted = null;
  }
  setTourOverlayMask(null);
  _clearTourRects();
}

// Opens the import modal mid-tour, hiding the tour chrome until the modal closes.
function _tourOpenImport() {
  _tourPausedForImport = true;
  _tourDidImport = false;
  document.getElementById('tourOverlay').style.display = 'none';
  document.getElementById('tourTooltip').style.display = 'none';
  _clearTourRects();
  if (_tourHighlighted) {
    _tourHighlighted.classList.remove('tour-spotlight');
    _tourHighlighted.closest?.('.board')?.classList.remove('tour-board-reveal');
    _tourHighlighted = null;
  }
  openImportModal();
}

// Called from closeImportModal when the modal was opened by the tour.
function _resumeTourAfterImport() {
  _tourPausedForImport = false;
  document.getElementById('tourOverlay').style.display = '';
  document.getElementById('tourTooltip').style.display = '';
  if (_tourDidImport) {
    _tourDidImport = false;
    _tourStep++; // advance from the import step to the drag demo
  }
  showTourStep(_tourStep);
}

document.getElementById('tourNextBtn').addEventListener('click', () => {
  const step = _tourSteps[_tourStep];
  if (step?.cta === 'import') { _tourOpenImport(); return; }
  if (_tourStep < _tourSteps.length - 1) { _tourStep++; showTourStep(_tourStep); }
  else endTour('completed');
});
document.getElementById('tourLaterBtn').addEventListener('click', () => {
  // Declined import → skip only the import, still show the drag demo next.
  if (_tourStep < _tourSteps.length - 1) { _tourStep++; showTourStep(_tourStep); }
  else endTour('completed');
});
document.getElementById('tourBackBtn').addEventListener('click', () => {
  if (_tourStep > 0) { _tourStep--; showTourStep(_tourStep); }
});
document.getElementById('tourSkipBtn').addEventListener('click', () => endTour('skipped'));
