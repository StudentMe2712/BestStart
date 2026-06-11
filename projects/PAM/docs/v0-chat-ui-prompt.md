# Промпт для v0.dev — чат-GUI PAM

Скопируй блок ниже на https://v0.dev. Он отдаст React/Tailwind компоненты, которые
встроятся в наш Next.js (App Router). Пришли результат (код или скриншот) — интегрирую
под наш backend (стриминг чата, сайдбар, источники памяти).

---

```
Build a Next.js (App Router) + Tailwind chat UI for a local-first "Personal AI Memory" app (Russian UI). Dark, minimal, terminal-inspired aesthetic.

Theme:
- Background near-black (#0a0a0a / neutral-950), text neutral-100, accent lime (#a3e635), secondary teal (#5ee9d0).
- Monospace (ui-monospace) for labels/headings, sans-serif for body text.

Top navbar (sticky):
- Left: brand "PAM" with a small lime square dot + tiny uppercase "personal_ai_memory".
- Right: tabs "Чат" (active), "История", "Избранное", "Лектор" (Лектор is a dim "скоро" chip). Active tab = lime text + subtle bg.

Main = ChatGPT/Claude-style chat:
- Left sidebar (w-60, hidden on mobile): "+ Новый чат" button, label "чаты", scrollable list of chat titles; active item highlighted.
- Center column (max-w-3xl, centered): scrollable message list.
  - User messages: right-aligned rounded bubble (bg neutral-800).
  - Assistant messages: left-aligned with a small square lime avatar "P", markdown content, and under the answer small bordered chips labeled "память:" listing source tags.
  - "typing" indicator (three bouncing dots) while the assistant generates.
- Bottom input bar: rounded (bg neutral-900, border, lime focus ring) with a paperclip "attach" icon button on the left, an auto-growing textarea, and a circular lime send button with an up-arrow. Helper line under it: "PAM помнит твои разговоры · Enter — отправить, Shift+Enter — перенос".
- Empty state: centered lime "P" badge, title "Чат с твоей памятью", subtitle about remembering past ChatGPT/Claude/Gemini conversations.

Make it clean, responsive, accessible. Use lucide-react icons. Components only (no backend).
```

---

## После генерации
- Пришли мне **код** компонентов (или **скриншот**) — я причешу под текущую тему и подключу:
  - стриминг ответа (`POST /chat`, SSE через fetch+ReadableStream),
  - сайдбар из реальных чатов (`GET /conversations?source=pam`),
  - чипы «память:» из события `sources`,
  - кнопку attach (загрузка файлов/картинок — отдельная задача бэклога).
