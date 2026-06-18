/** Normalize a URL for grouping memories by page: drop the hash, keep path + query. */
export function normalizeUrl(href: string): string {
  try {
    const u = new URL(href);
    u.hash = '';
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
