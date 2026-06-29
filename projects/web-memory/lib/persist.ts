// Ask the browser to keep our IndexedDB from being evicted under storage pressure.
// Requested at most once: if already persisted, or if we've asked before, we don't ask again.

const REQUESTED_KEY = 'wm:persistRequested';

export async function ensurePersistentStorage(): Promise<void> {
  if (!navigator.storage?.persist) return;
  try {
    if (await navigator.storage.persisted()) {
      console.info('[web-memory] persistent storage: already granted');
      return;
    }
    const { [REQUESTED_KEY]: requested } = await browser.storage.local.get(REQUESTED_KEY);
    if (requested) return; // asked once already — don't spam
    const granted = await navigator.storage.persist();
    await browser.storage.local.set({ [REQUESTED_KEY]: true });
    console.info(`[web-memory] persistent storage: ${granted ? 'granted' : 'not granted'}`);
  } catch (err) {
    console.warn('[web-memory] persistent storage request failed', err);
  }
}
