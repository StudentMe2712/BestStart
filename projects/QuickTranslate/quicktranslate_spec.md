# QuickTranslate — Спецификация и Roadmap

Легковесное расширение для браузера Google Chrome (Manifest V3), позволяющее мгновенно переводить выделенный текст прямо на веб-страницах через плавающую кнопку и компактный всплывающий поп-ап.

## Задачи проекта

- [x] 1.1. Создать `manifest.json` (Manifest V3) с правами на `activeTab`, `storage`, `background` service worker и `host_permissions`.
- [x] 1.2. Написать `content.js` и `content.css`, которые слушают событие `mouseup`, ловят выделенный текст (`window.getSelection()`), вычисляют координаты и отрисовывают стильную плавающую кнопку «Перевести».
- [x] 1.3. Написать логику запроса к публичному API перевода через `background.js` (проксирование запросов, обход CORS, multi-tier fallback: Google gtx, Chrome dict API, MyMemory).
- [x] 1.4. Сверстать компактный и адаптивный мини-поп-ап с результатом перевода прямо на странице, индикатором языковой пары (`EN → RU`), кнопками копирования и закрытия (Esc / клик вне поп-апа).

---

## Архитектура и стек
- **Платформа:** Chrome Extensions Manifest V3
- **Архитектура связи:**
  - `content.js` ── `chrome.runtime.sendMessage` ──► `background.js` (Service Worker) ── `fetch` ──► `Translation API`
  - Ответ возвращается обратно через асинхронный `sendResponse`.
- **Изоляция стилей:** Префиксные классы `.qt-*` и CSS reset (`all: initial`) внутри контейнера `#quicktranslate-root` для полной защиты от стилей хост-страницы.
- **Иконки:** Векторная отрисовка в PNG 16x16, 48x48, 128x128 в папке `icons/`.
