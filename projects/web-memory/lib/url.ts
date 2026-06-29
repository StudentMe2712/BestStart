// Tracking parameters that don't identify the page itself, so the same article shows up
// under several URLs. Stripped (along with any `utm_*`) only for matching — never from `href`.
const TRACKING_PARAMS = new Set(['fbclid', 'gclid', 'ref']);

/** Normalize a URL for grouping memories by page. Drops the hash, lowercases the host,
 *  strips tracking params (utm_*, fbclid, gclid, ref) and a trailing slash. The original
 *  `href` is kept separately for navigation; this value is used only for matching. */
export function normalizeUrl(href: string): string {
  try {
    const u = new URL(href);
    u.hash = '';
    u.hostname = u.hostname.toLowerCase();
    for (const key of [...u.searchParams.keys()]) {
      const k = key.toLowerCase();
      if (k.startsWith('utm_') || TRACKING_PARAMS.has(k)) u.searchParams.delete(key);
    }
    // Drop a trailing slash from the path, but keep the root "/".
    if (u.pathname.length > 1 && u.pathname.endsWith('/')) {
      u.pathname = u.pathname.replace(/\/+$/, '');
    }
    return u.toString();
  } catch {
    return href;
  }
}

/** A compact, human-friendly label for a URL (host + trimmed path). */
export function prettyUrl(href: string): string {
  try {
    const u = new URL(href);
    const path = u.pathname === '/' ? '' : u.pathname;
    return (u.host + path).replace(/\/$/, '');
  } catch {
    return href;
  }
}
