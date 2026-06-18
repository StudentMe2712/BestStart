import { defineConfig } from 'wxt';

// WXT config. The React module wires up the Vite React plugin + JSX.
// WXT auto-adds the `sidePanel` permission + `side_panel` manifest entry because an
// `entrypoints/sidepanel/` exists, and auto-adds `tabs`/`scripting` in dev for HMR.
export default defineConfig({
  modules: ['@wxt-dev/module-react'],
  manifest: {
    name: 'Web Memory',
    description:
      'Сохраняйте важные фрагменты страниц, а не страницы целиком. Подсветка, заметки, поиск. Локально, без облака.',
    permissions: ['storage', 'contextMenus', 'sidePanel', 'tabs'],
    host_permissions: ['<all_urls>'],
    action: {
      default_title: 'Web Memory — открыть боковую панель',
    },
  },
});
