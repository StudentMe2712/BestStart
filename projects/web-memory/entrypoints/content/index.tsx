import './style.css';
import { createRoot, type Root } from 'react-dom/client';
import { ContentApp } from './ContentApp';
import { emit } from './bus';
import { UI_ATTR } from '@/lib/anchor';
import { applyHighlight, removeHighlight, scrollToElement, scrollToText } from '@/lib/highlight';
import { sendBg, type TabMessage } from '@/lib/messages';
import { normalizeUrl } from '@/lib/url';

export default defineContentScript({
  matches: ['<all_urls>'],
  cssInjectionMode: 'ui',
  runAt: 'document_idle',

  async main(ctx) {
    const pageUrl = normalizeUrl(location.href);

    async function reapply() {
      const memories = await sendBg({ type: 'GET_PAGE_MEMORIES', url: pageUrl });
      for (const mem of memories) {
        if (mem.anchor.kind === 'text') applyHighlight(mem);
      }
      emit('memories', memories);
      return memories;
    }

    const handleScroll = async (id: string) => {
      const memories = await sendBg({ type: 'GET_PAGE_MEMORIES', url: pageUrl });
      const mem = memories.find((m) => m.id === id);
      if (!mem) return;
      if (mem.anchor.kind === 'text') scrollToText(mem);
      else scrollToElement(mem);
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
          void handleScroll(message.id);
          break;
        case 'REMOVE_MEMORY':
          removeHighlight(message.id);
          emit('memory-removed', message.id);
          break;
        case 'REAPPLY':
          void reapply();
          break;
      }
    });

    // Initial apply + a couple of retries for content that loads late (SPAs, lazy text).
    await reapply();
    setTimeout(() => void reapply(), 1500);
    setTimeout(() => void reapply(), 4000);
  },
});
