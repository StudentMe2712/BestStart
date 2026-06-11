# Web UI setup (Next.js 16, Tailwind v4)

> Уже заскаффолжено (2026-06-02). Эта инструкция — для воспроизведения с нуля.
> `create-next-app@latest` сейчас ставит **Next.js 16 + React 19 + Tailwind v4**
> (без `tailwind.config.js` — конфиг и плагины живут в CSS через `@import`/`@plugin`).

## One-time setup

Скаффолдер не запускается в непустой папке. Поэтому: убери наши исходники
(`app/`, `lib/`, этот файл) в сторону, заскаффолди, верни обратно.

```bash
# из Desktop/Pam/ (web как путь — не нужно cd)
npx create-next-app@latest web \
  --typescript --tailwind --app \
  --no-src-dir --no-import-alias --no-eslint --yes
```

Затем верни наши `app/layout.tsx`, `app/page.tsx`, `app/c/[id]/page.tsx`,
`lib/api.ts` поверх сгенерированных (наши — источник правды).
Удали мусор, который насыпает скаффолдер: `web/CLAUDE.md`, `web/AGENTS.md`.

Зависимости (markdown + плагин typography для классов `prose`):

```bash
npm install react-markdown remark-gfm
npm install -D @tailwindcss/typography
```

В `app/globals.css` оставь только подключение Tailwind и плагина
(тему задаёт `layout.tsx`):

```css
@import "tailwindcss";
@plugin "@tailwindcss/typography";
```

> ⚠️ `app/c/[id]/page.tsx` использует async-`params` (`use(params)` +
> `params: Promise<...>`) — требует **Next.js 15+**.
> `sourceClass()` в `page.tsx` типизирована как `ReactElement` (в React 19
> глобального namespace `JSX` больше нет).

## Run

```bash
npm run dev
```

Open http://localhost:3000

## Files in this folder

- `app/layout.tsx` — root layout
- `app/page.tsx` — list & search
- `app/c/[id]/page.tsx` — conversation detail
- `lib/api.ts` — backend client

## What it does

- Lists conversations grouped by source
- Search bar (full-text, server-side)
- Click conversation → see all messages

## Note

Frontend talks to `http://localhost:8000`. If you change the backend port,
update `lib/api.ts`.
