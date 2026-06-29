// User-facing behavior settings live in browser.storage.local (a tiny config object), so both
// the side panel and the content script read them without going through the background/IndexedDB.
// All new in-page behaviors are toggleable here, keeping them reversible.

const STORAGE_KEY = 'wm:settings';

export interface WmSettings {
  /** Master switch: when false, the in-page UI is fully off everywhere. */
  enabled: boolean;
  /** Hosts where the in-page UI is suppressed (e.g. messengers that the toolbar would overlap). */
  disabledDomains: string[];
  /** Close the on-page note popup when clicking outside it (in addition to the Close button). */
  closeNoteOnOutsideClick: boolean;
}

/** Defaults: enabled everywhere except messenger web apps whose UI our toolbar would cover. */
export const DEFAULT_SETTINGS: WmSettings = {
  enabled: true,
  disabledDomains: [
    'web.whatsapp.com',
    'web.telegram.org',
    'webk.telegram.org',
    'webz.telegram.org',
  ],
  closeNoteOnOutsideClick: true,
};

/** Coerce a stored value into a clean string[] of domains, falling back to defaults. */
function toDomainList(v: unknown): string[] {
  return Array.isArray(v)
    ? v.filter((x): x is string => typeof x === 'string')
    : DEFAULT_SETTINGS.disabledDomains;
}

/** Read settings, merging into DEFAULT_SETTINGS so missing keys (older versions) get defaults. */
export async function loadSettings(): Promise<WmSettings> {
  const stored = (await browser.storage.local.get(STORAGE_KEY))[STORAGE_KEY] as
    | Partial<WmSettings>
    | undefined;
  if (!stored) return { ...DEFAULT_SETTINGS };
  return {
    ...DEFAULT_SETTINGS,
    ...stored,
    disabledDomains: toDomainList(stored.disabledDomains),
  };
}

export async function saveSettings(settings: WmSettings): Promise<void> {
  await browser.storage.local.set({ [STORAGE_KEY]: settings });
}

/** Subscribe to settings changes from any context; returns an unsubscribe fn. */
export function subscribeSettings(onChange: (settings: WmSettings) => void): () => void {
  const listener = (changes: Record<string, { newValue?: unknown }>, area: string) => {
    if (area === 'local' && changes[STORAGE_KEY]) {
      const next = changes[STORAGE_KEY].newValue as Partial<WmSettings> | undefined;
      onChange({
        ...DEFAULT_SETTINGS,
        ...next,
        disabledDomains: toDomainList(next?.disabledDomains),
      });
    }
  };
  browser.storage.onChanged.addListener(listener);
  return () => browser.storage.onChanged.removeListener(listener);
}

/** Whether the in-page UI should be suppressed on `host` (master off, or host in the blacklist).
 *  Matches a domain and its subdomains (suffix match), so `web.telegram.org` covers any subdomain. */
export function isDisabledOnHost(host: string, s: WmSettings): boolean {
  if (!s.enabled) return true;
  const h = host.toLowerCase();
  return s.disabledDomains.some((d) => h === d || h.endsWith('.' + d));
}

/** Clean a user-typed domain: drop scheme/path/leading 'www.', lower-case. Returns '' if empty. */
export function normalizeDomain(input: string): string {
  let v = input.trim().toLowerCase();
  if (!v) return '';
  // Strip scheme and anything from the first slash/colon (path, port) onward.
  v = v.replace(/^[a-z]+:\/\//, '');
  v = v.split('/')[0].split('?')[0].split('#')[0];
  v = v.split(':')[0]; // drop any :port so it matches location.hostname
  v = v.replace(/^www\./, '');
  return v;
}
