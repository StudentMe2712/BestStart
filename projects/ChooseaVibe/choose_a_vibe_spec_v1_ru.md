# Choose a Vibe (Выбери Вайб)

**Продуктовая и техническая спецификация · Версия 1.2 (Upstash Redis Cloud Persistence, 24-Hour Master Password & Vercel Serverless Architecture) · Спринт 11**

**Слоган:** Превратите обычный вечер в маленькое приключение.  
**Категория:** Lifestyle / Игры для пар / Развлечения  
**Платформа:** iOS Standalone PWA (Vercel Serverless + Upstash Redis) & SwiftUI / SwiftData  
**Язык интерфейса и контента:** Русский (100% локализация UI, названий глав, уровней, достижений и карточек)  
**Валюта по умолчанию:** Казахстанский тенге (₸)  
**Корень воркспейса:** `C:\Users\Mila\Desktop\BestStart`  
**Директория проекта:** `projects/ChooseaVibe`  

---

## 1. Концепция продукта и философия «Calm Luxury» (Версия 1.2 · Спринт 11)

**Главная идея:** Choose a Vibe — это элегантный, ненавязчивый спутник совместного вечера (*Calm Lifestyle Companion*) для одной пары. Приложение функционирует как полноценное установленное iOS-приложение (Standalone PWA с экрана «Домой» без интерфейса Safari), защищено сквозным мастер-паролем с сессией ровно на 24 часа (86 400 000 мс) и синхронизирует весь прогресс пары через сверхбыструю облачную базу Upstash Redis REST API.

### Ключевые архитектурные столпы:

1. **Разгрузка Главного экрана (Zen Luxury & Progressive Disclosure):**
   - Главный экран «Сегодня» очищен от визуального шума и давления: на нём отображается тёплая закатная атмосфера, приветствие пары и одна центральная интерактивная карточка «Колода приключений» с кнопкой `✦ Подобрать вечер ›`.
   - Тонкий фильтр настроения, времени и бюджета вынесен в плавную модальную шторку (`VibePickerModalSheet`), в которой также доступна кнопка мгновенного старта `🎲 Сюрприз на двоих`.
   - Шкала опыта (XP), кубок и уровни вынесены во вкладку «Коллекция» и не отвлекают от атмосферы вечера.

2. **Автономный iOS Standalone PWA:**
   - Приложение устанавливается в один шаг через Safari («Поделиться → На экран «Домой»») и открывается в полноэкранном системном окне iOS без адресной строки браузера и без 7-дневных ограничений бесплатных сертификатов.
   - `sw.js` (Service Worker) реализует стратегию кэширования статики для мгновенного холодного запуска за ~50 мс.
   - Запросы к `/api/*` гарантированно минуют Service Worker и никогда не кэшируются оффлайн-кэшем.

3. **Вечное облачное хранение и умная синхронизация (Upstash Redis Cloud Persistence & Smart Merge):**
   - Данные сохраняются локально в `localStorage` (мгновенный отклик 0 мс без ожидания сети) и асинхронно синхронизируются с облачной базой Upstash Redis по ключу `choose_a_vibe:couple_state:v1`.
   - **Умное слияние (Smart Merge):**
     * `totalXP = Math.max(local.totalXP, cloud.totalXP)`
     * Объединение массивов пройденных квестов (`finishedSessions`), достижений (`unlockedAchievementIds`) и глав (`unlockedChapters`) через объединение множеств (Set union).
     * Если в облаке активная сессия свежее локальной (по временной метке `updatedAt`), она аккуратно подтягивается без потери прогресса.
   - При удалении PWA, очистке кэша Safari или покупке нового iPhone — после ввода пароля пары состояние на 100% восстанавливается из облака.
   - Оба партнёра на разных смартфонах всегда видят единый актуальный прогресс.

4. **Безопасность: Закрытый доступ по Мастер-паролю с TTL сессии 24 часа (`AuthLockOverlay`):**
   - Приложение предназначено исключительно для двух людей.
   - При первом открытии или истечении 24 часов (86 400 000 мс) активируется экран блокировки `AuthLockOverlay`.
   - Пароль проверяется сервером Vercel (`POST /api/auth`) против серверной переменной окружения `APP_MASTER_PASSWORD` с использованием `crypto.timingSafeEqual`.
   - При успешной аутентификации выдаётся подписанный токен HMAC-SHA256 (`payload = { exp, scope: "couple_access" }`).
   - Клиент сохраняет токен и автоматически валидирует его каждые 30 секунд. По истечении 24 часов экран блокировки возвращается автоматически.
   - **Локальный фолбэк для `file://` и локальной разработки:** при отсутствии сети или запуске из локальной файловой системы поддерживается валидация с локальной 24-часовой сессией.

---

## 2. Архитектура безопасности и облачной БД (Vercel Serverless + Upstash Redis)

### 2.1. Схема данных пары в Upstash Redis (`choose_a_vibe:couple_state:v1`)

```json
{
  "version": 1,
  "updatedAt": 1761648000000,
  "profile": {
    "partnerOneName": "Алишер",
    "partnerTwoName": "Дана",
    "totalXP": 240,
    "unlockedAchievementIds": ["first_step", "cozy_night", "secret_chef"],
    "selectedScratchThemeId": "classic_silver",
    "excludedBoundaries": [],
    "hasCompletedOnboarding": true,
    "hasSeenWalkthrough": true,
    "soundEnabled": true
  },
  "activeSession": {
    "adventureId": "adv_03",
    "currentStepIndex": 2,
    "isCurrentCardScratched": true,
    "isUsingFallback": false,
    "stepStatuses": ["completed", "pending", "pending", "pending"],
    "startedAt": 1761647000000,
    "updatedAt": 1761648000000
  },
  "finishedSessions": [
    {
      "adventureId": "adv_01",
      "completedAt": "2026-10-27T18:30:00.000Z",
      "earnedXP": 65
    }
  ],
  "unlockedChapters": ["ch_01", "ch_02"]
}
```

### 2.2. Серверные API-эндпоинты (Vercel Serverless Functions)

#### `POST /api/auth`
- **Тело запроса:** `{ "password": "..." }`
- **Логика:** Сравнивает пароль с `process.env.APP_MASTER_PASSWORD` (по умолчанию `Vibe2026!`) через постоянное по времени криптографическое сравнение SHA-256 хэшей (`crypto.timingSafeEqual`).
- **Ответ 200:**
  ```json
  {
    "ok": true,
    "token": "eyJleHAiOjE3NjE3MzQ0MDAwMDAsInNjb3BlIjoiY291cGxlX2FjY2VzcyJ9.3b8f...",
    "expiresAt": 1761734400000
  }
  ```
- **Ответ 401:** `{ "ok": false, "error": "Неверный пароль доступа" }`

#### `GET /api/auth`
- **Заголовок:** `Authorization: Bearer <token>`
- **Логика:** Проверяет валидность HMAC-SHA256 подписи и срок жизни токена (`Date.now() < payload.exp`).
- **Ответ 200:** `{ "ok": true, "valid": true, "expiresAt": 1761734400000, "remainingMs": 86395000 }`
- **Ответ 401:** `{ "ok": false, "error": "Сессия истекла или недействительна" }`

#### `GET /api/sync`
- **Заголовок:** `Authorization: Bearer <token>`
- **Логика:** Запрашивает `choose_a_vibe:couple_state:v1` из Upstash Redis по REST API (`${redisUrl}/get/...`). Если база ещё не настроена в окружении, возвращает мягкое предупреждение без падения приложения.
- **Ответ 200:** `{ "ok": true, "state": { ... }, "serverTime": 1761648000000 }`
- **Ответ 401:** `{ "ok": false, "error": "Требуется авторизация" }`

#### `POST /api/sync`
- **Заголовок:** `Authorization: Bearer <token>`
- **Тело запроса:** `{ "state": { ... } }`
- **Логика:** Валидирует токен, проставляет `state.updatedAt = Date.now()`, сохраняет состояние в Upstash Redis через REST API (`POST ${redisUrl}/set/...`).
- **Ответ 200:** `{ "ok": true, "updatedAt": 1761648000000 }`
- **Ответ 401:** `{ "ok": false, "error": "Требуется авторизация" }`

---

## 3. Структура директорий проекта

```
projects/ChooseaVibe/
├── choose_a_vibe_spec_v1_ru.md       // Настоящая спецификация (v1.2 · Спринт 11)
├── sync.sh                           // Git sync скрипт
├── ChooseAVibe/                      // SwiftUI / SwiftData нативный проект
└── preview/                          // Директория для деплоя на Vercel (Root Directory)
    ├── vercel.json                   // Конфигурация Vercel (no-cache sw.js, no-store /api/*)
    ├── .env.example                  // Шаблон переменных окружения
    ├── DEPLOY_GUIDE_RU.md            // Русскоязычное руководство по деплою за 5 шагов
    ├── api/                          // Serverless Functions (Node.js 18+/20+)
    │   ├── auth.js                   // POST /api/auth, GET /api/auth (24h HMAC токен)
    │   └── sync.js                   // GET /api/sync, POST /api/sync (Upstash Redis REST)
    ├── icons/                        // Apple Touch Icons (180x180, 192x192, 512x512)
    ├── index.html                    // Разметка PWA + оверлей блокировки #authLockScreen
    ├── styles.css                    // Стили Calm Luxury + AuthLockOverlay
    ├── app.js                        // Движок квестов + CloudSyncManager + AuthGuard
    ├── manifest.json                 // PWA Standalone манифест
    └── sw.js                         // Offline Service Worker (пропускает /api/*)
```

---

## 4. Чек-лист готовности Спринта 11

- [x] **Облачная персистентность Upstash Redis:** прогресс пары надёжно сохраняется в Redis через REST API и восстанавливается на любом новом устройстве.
- [x] **Защита мастер-паролем на 24 часа:** доступ закрыт экраном `#authLockScreen`. Сессия подписывается HMAC-SHA256 токеном ровно на 86 400 000 мс.
- [x] **Изоляция API от Service Worker:** запросы к `/api/auth` и `/api/sync` никогда не кэшируются Service Worker'ом.
- [x] **Оффлайн/локальный фолбэк:** при запуске через `file://` приложение остаётся полностью работоспособным с локальной сессией.
- [x] **Интерфейс управления синхронизацией:** во вкладке «Пара» отображается статус облака, оставшееся время сессии, кнопки ручной синхронизации и мгновенной блокировки.
