import './style.css';
import { createRoot, type Root } from 'react-dom/client';
import { ContentApp } from './ContentApp';
import { emit } from './bus';
import { UI_ATTR } from '@/lib/anchor';
import {
  applyHighlight,
  isApplied,
  removeHighlight,
  restyleHighlight,
  scrollToElement,
  scrollToText,
} from '@/lib/highlight';
import { sendBg, type AnchorStatusBroadcast, type TabMessage } from '@/lib/messages';
import { normalizeUrl } from '@/lib/url';

/** Read the page's title + a meta description, locally (used by "Save Page"). */
function readPageMeta(): { title: string; description: string } {
  const meta = (sel: string) =>
    document.querySelector<HTMLMetaElement>(sel)?.content?.trim() ?? '';
  const description =
    meta('meta[name="description"]') ||
    meta('meta[property="og:description"]') ||
    meta('meta[name="twitter:description"]');
  return { title: document.title, description };
}

export default defineContentScript({
  matches: ['<all_urls>'],
  cssInjectionMode: 'ui',
  runAt: 'document_idle',

  async main(ctx) {
    const pageUrl = normalizeUrl(location.href);

    // Ids of text memories that exist for this page but couldn't be re-located in the DOM.
    let unlocated: string[] = [];

    async function reapply() {
      const memories = await sendBg({ type: 'GET_PAGE_MEMORIES', url: pageUrl });
      const failed: string[] = [];
      for (const mem of memories) {
        if (mem.anchor.kind !== 'text') continue;
        // Already on the page → refresh its style (e.g. after a category recolour);
        // otherwise (re)locate and wrap it, tracking ones we couldn't find.
        if (isApplied(mem.id)) restyleHighlight(mem);
        else if (!applyHighlight(mem)) failed.push(mem.id);
      }
      unlocated = failed;
      emit('memories', memories);
      // Let an open side panel surface the failures (no receiver → ignored).
      const status: AnchorStatusBroadcast = { type: 'ANCHOR_STATUS', url: pageUrl, unlocated };
      browser.runtime.sendMessage(status).catch(() => {});
      return memories;
    }

    const handleScroll = async (id: string): Promise<{ ok: boolean }> => {
      const memories = await sendBg({ type: 'GET_PAGE_MEMORIES', url: pageUrl });
      const mem = memories.find((m) => m.id === id);
      if (!mem) return { ok: false };
      const found = mem.anchor.kind === 'text' ? scrollToText(mem) : scrollToElement(mem);
      return { ok: found };
    };

    const ui = await createShadowRootUi<{ root: Root; wrapper: HTMLElement }>(ctx, {
      name: 'web-memory-ui',
      position: 'inline',
      anchor: 'body',
      onMount(container) {
        const shadowRoot = container.getRootNode();
        if (shadowRoot instanceof ShadowRoot) {
          (shadowRoot.host as HTMLElement).setAttribute(UI_ATTR, '');
        }
        const wrapper = document.createElement('div');
        container.append(wrapper);
        const root = createRoot(wrapper);
        root.render(<ContentApp pageUrl={pageUrl} />);
        return { root, wrapper };
      },
      onRemove(mounted) {
        mounted?.root.unmount();
        mounted?.wrapper.remove();
      },
    });
    ui.mount();

    browser.runtime.onMessage.addListener((raw: unknown) => {
      const message = raw as TabMessage;
      switch (message.type) {
        case 'CAPTURE_SELECTION':
          emit('capture', message.mode);
          break;
        case 'START_ELEMENT_NOTE':
          emit('element-note', undefined);
          break;
        case 'SCROLL_TO_MEMORY':
          return handleScroll(message.id);
        case 'REMOVE_MEMORY':
          removeHighlight(message.id);
          emit('memory-removed', message.id);
          break;
        case 'REAPPLY':
          void reapply();
          break;
        case 'GET_ANCHOR_STATUS':
          return Promise.resolve({ unlocated });
        case 'GET_PAGE_META':
          return Promise.resolve(readPageMeta());
      }
      return undefined;
    });

    // Initial apply + a couple of retries for content that loads late (SPAs, lazy text).
    await reapply();
    setTimeout(() => void reapply(), 1500);
    setTimeout(() => void reapply(), 4000);
  },
});
