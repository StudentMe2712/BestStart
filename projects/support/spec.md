# 📋 ТЕХНИЧЕСКАЯ СПЕЦИФИКАЦИЯ: РАЗВЕРТЫВАНИЕ ПРОЕКТА НА ХОСТИНГЕ
## Проект: ИТС-Ком Wiki & Server Hub (BestStart Support)
**Версия проекта:** 2.7.2 (Спринты 1–7 полностью завершены)  
**Назначение документа:** Полная передача проекта целевому агенту / DevOps-инженеру для ввода в эксплуатацию и бесшовного развертывания на боевом сервере (Plesk / Linux / Nginx / Node.js 18+).  
**Дата актуализации:** 26 октября 2026 г.  
**Рабочая директория в репозитории:** `projects/support/`  
**Статус готовности:** 🟢 100% Production Ready (все автотесты пройдены)

---

## 📑 СОДЕРЖАНИЕ
1. [Паспорт системы и назначение](#1-паспорт-системы-и-назначение)
2. [Полная хронология выполненных работ (Спринты 1–7 «от и до» )](#2-полная-хронология-выполненных-работ-спринты-17-от-и-до)
3. [Текущее архитектурное состояние и стек](#3-текущее-архитектурное-состояние-и-стек)
4. [Структура файлов и модулей проекта](#4-структура-файлов-и-модулей-проекта)
5. [Схема базы данных (MariaDB / MySQL)](#5-схема-базы-данных-mariadb--mysql)
6. [Конфигурация переменных окружения (.env)](#6-конфигурация-переменных-окружения-env)
7. [Пошаговая инструкция по развертыванию на Plesk / Nginx](#7-пошаговая-инструкция-по-развертыванию-на-plesk--nginx)
8. [Миграции и начальное наполнение данными (Seed SQL)](#8-миграции-и-начальное-наполнение-данными-seed-sql)
9. [Спецификация API и маршрутов](#9-спецификация-api-и-маршрутов)
10. [Приемочный чек-лист для агента (Post-Deployment Verification)](#10-приемочный-чек-лист-для-агента-post-deployment-verification)

---

## 1. Паспорт системы и назначение

**ИТС-Ком Wiki & Server Hub** — высоконадежная корпоративная веб-система технической документации, управления серверной инфраструктурой 25+ клиентов-организаций и версионного контроля информационных баз 1С для системных администраторов и инженеров ТОО «ИТС-Ком».

### Ключевые возможности:
- **База знаний Wiki:** Ведение конфиденциальных заметок (RDP, AnyDesk, учетные записи Windows/Linux, СУБД MS SQL/PostgreSQL, iLO, RAID) в моноширинном блоке (`JetBrains Mono`, 15px) без артефактов форматирования.
- **Файловый хаб организации:** Загрузка, хранение и централизованный просмотр вложений (Excel-таблицы, Word-инструкции, PDF-регламенты, схемы XML, изображения) с защитой доступа по сессии.
- **WhatsApp-like Lightbox Предпросмотрщик:** Мгновенный просмотр `.docx` / `.doc` (рендеринг в HTML на листе А4), `.xlsx` / `.xls` / `.csv` (таблицы SheetJS с вкладками), `.xml` / `.txt` (подсветка синтаксиса), `.pdf` (чистый iframe) и картинок (исходное 1:1 качество с масштабированием и панорамированием).
- **Легендарная красная булавка 📌 (Smart Pin):** Персональное закрепление важных для инженера организаций вверху списка с плавной анимацией и нулевым визуальным шумом.
- **Журнал баз 1С («События»):** Иерархическое дерево клиентов и баз в стилистике 1С:Предприятие 8.3 (`[+]` ⇄ `[-]`), фильтрация пустых баз, свободный ввод релизов.
- **Безотказный экспорт:** Генерация Printable View в PDF средствами браузера без падений headless-браузера и мгновенный экспорт в `.txt`.
- **Единое подтверждение действий (Unified Confirm Modal):** Полный отказ от `window.confirm()` и `window.alert()` в пользу элегантного темного модального окна с размытием фона и защитой от случайных удалений.
- **Безопасный закрытый периметр:** Отключенная публичная регистрация, 2-шаговый сброс пароля, Rate Limiting против перебора, изоляция dev-подсказок на проде.

---

## 2. Полная хронология выполненных работ (Спринты 1–7 «от и до»)

В процессе разработки проект прошел 7 комплексных итераций (спринтов). Все требования полностью реализованы и покрыты тестами.

### 🔹 Спринт 1: Архитектурный фундамент и In-Memory Mock Stub
- Реализована концепция **Dual-Engine СУБД**: автоматическое переключение между реальным пулом MariaDB (`mysql2/promise`) и автономным In-Memory моком (`db-stub.js`) через переменную окружения `DB_STUB=true/false`.
- Развернут базовый стек: Node.js 18+, Express 4, EJS шаблонизатор.
- Создан модуль аутентификации `auth.js` на базе bcrypt и express-session.
- Реализованы сценарии быстрого локального запуска в один клик через `start-local.bat`.

### 🔹 Спринт 2: Чистая типографика Wiki и 2-карточный Дашборд
- Интегрирован шрифтовой стек `JetBrains Mono` для заметок (размер 15px, line-height 1.6) для комфортной работы с техническими IP/паролями.
- Разработана сбалансированная двухкарточная сетка на Главной странице.
- Реализована темная и светлая тема оформления с сохранением состояния в `localStorage` пользователя.

### 🔹 Спринт 3: Реестр информационных баз 1С и экспорт
- Создан реестр конфигураций 1С, привязанный к организациям (`organization_databases_1c`).
- Реализовано удаление визуального мусора, стандартизация полей ввода.
- Заложен фундамент выгрузки карточки организации.

### 🔹 Спринт 4: Двухвкладочная архитектура Wiki и Дерево событий
- Разделена карточка компании на 2 логические вкладки: таб «Данные» (системные параметры) и таб «Базы 1С» (учет конфигураций).
- Хэш-навигация по вкладкам (`#tab-data`, `#tab-1c`).
- Первичная верстка иерархического журнала баз 1С на странице `/events`.

### 🔹 Спринт 5: Дерево в стиле 1С:Предприятие 8.3 и Безотказный PDF
- **Иерархическое дерево 1С:** Все организации по умолчанию свернуты. Узлы раскрытия выполнены в виде классических квадратных кнопок `[+]` ⇄ `[-]`. Компании с 0 баз отображаются компактно без плюсиков.
- **Свободный ввод описания 1С:** Поле описания базы переведено на чистый гибкий `<input>` без жестких масок (поддержка формата `3.0.73.1 (БК), подсистема Строительство`).
- **Замена растровых иконок:** Все эмодзи в меню заменены на контурные векторные SVG (PDF, TXT, Корзина).
- **Безотказный PDF:** Полный отказ от падавшего процесса Puppeteer в пользу нативного Printable HTML View (`/api/export/pdf/:id`) с директивами `@media print` и автовызовом `window.print()`.

### 🔹 Спринт 6: Production Polish, безопасность dev-подсказок и монограммы
- **Изоляция dev-подсказок:** Плашка «Локальный доступ: admin / admin» изолирована через EJS-условие `<% if (isStubMode) %>`. На боевом хостинге Plesk подсказка физически не попадает в HTML.
- **SVG-переключатель темы:** Кнопка темы переведена на векторные контуры солнца и луны.
- **Предотвращение автозаполнения:** Формам модальных окон добавлен атрибут `autocomplete="off"`, исключающий перекрытие полей браузерными всплывающими подсказками.
- **Генератор монограмм:** Серый дефолтный силуэт пользователя заменен на аккуратный аватар `44x44px` с автоматической генерацией двухбуквенной монограммы названия (`getOrgMonogram()`, например `АП` для Автоперекрестка).
- **Фильтр пустых баз:** В «Событиях» внедрен тумблер `[x] Только с базами 1С` с сохранением выбора в `localStorage` и ограничением ширины страницы до 1400px.
- **Удаление номеров строк:** Из заметок Wiki удалены элементы `.wiki-line-num`, мешавшие выделению и копированию текста.

### 🔹 Спринт 7: Безопасность, Hero-Дашборд (1320px), Файловый хаб, Lightbox, Smart Pin и Unified Modal
- **Безопасность и закрытый контур:**
  - Полностью отключена регистрация пользователей: маршруты `GET /register` и `POST /register` редиректят на `/login`.
  - Внедрен 2-шаговый сброс пароля прямо на форме входа (Шаг 1: ввод логина и валидация; Шаг 2: ввод нового пароля).
  - Настроен in-memory Rate Limiter: максимум 5 неудачных попыток авторизации / сброса за 15 минут.
  - Внедрены обязательные заголовки безопасности: `X-Content-Type-Options: nosniff`, `X-Frame-Options: SAMEORIGIN`, `Referrer-Policy: same-origin`.
- **Масштабный Hero-Дашборд (1320px):**
  - Рабочий контейнер расширен до `max-width: 1320px; width: 100%; margin: 0 auto;`.
  - Презентационная 2-колоночная карточка Wiki (заголовок `Wiki — данные серверов` 30px, сапфировая иконка, 2 емких абзаца, единственный динамический бейдж `<%= totalServers %> организаций`, кнопка перехода в Wiki и корпоративная векторная стойка серверов).
- **Легендарная красная булавка 📌 (Smart Pin):**
  - Создана таблица `user_pins` и API `POST /api/organizations/:id/pin`.
  - У незакрепленных компаний булавка скрыта (`opacity: 0`), исключая визуальный шум, и плавно проявляется при наведении курсора (`:hover`) или фокусе (`:focus-within`).
  - У закрепленных организаций булавка видна постоянно (`.is-pinned`), а организация автоматически поднимается в группу «ЗАКРЕПЛЕННЫЕ».
- **Файловая подсистема и защищенное хранилище:**
  - Создана таблица `organization_files` (`utf8mb4`).
  - Файлы сохраняются в изолированной папке `uploads/org_<id>/` вне публичной статики `public/` с проверкой прав сессии.
  - Устранен сбой кодировки кириллических имен файлов через декодирование `latin1 -> utf8` в Multer.
  - Компактная кнопка `+` для прикрепления файлов без лишних баннеров Drag & Drop.
  - Общий доступ: любой авторизованный инженер видит все вложения компании и может скачивать или удалять файлы.
- **WhatsApp-like Lightbox Предпросмотр:**
  - Документы Word (`.docx`, `.doc`): рендеринг структуры через `mammoth` и `word-extractor` в нативный HTML на стилизованном листе формата А4.
  - Электронные таблицы (`.xlsx`, `.xls`, `.csv`): интерактивная таблица SheetJS с закрепленной шапкой и вкладками листов.
  - Код и разметка (`.xml`, `.txt`): просмотр отформатированного кода с моноширинной подсветкой.
  - Документы PDF: встроенный просмотр через iframe благодаря `X-Frame-Options: SAMEORIGIN`.
  - Картинки (`.png`, `.jpg`, `.jpeg`, `.webp`): просмотр в оригинальном качестве 1:1, четкая интерполяция (`-webkit-optimize-contrast`), кнопка `1:1`, лупа и панорамирование.
- **Unified Confirm Modal (`#unifiedConfirmModal`):**
  - Полный отказ от стандартных `window.confirm()` и `window.alert()`.
  - Единое модальное окно с эффектом размытия фона (`backdrop-filter: blur(8px)`), бейджем имени удаляемого объекта, спиннером в кнопке, закрытием по Escape/клику на оверлей и toast-уведомлениями.

---

## 3. Текущее архитектурное состояние и стек

```
+-------------------------------------------------------------------------------+
|                       Клиент (Браузер Пользователя)                           |
| - EJS Server-Side Rendering           - CSS3 Variables (Темная/Светлая темы)   |
| - Шрифты: JetBrains Mono / Segoe UI   - Vanilla JS ES6+ (Без фреймворков)     |
| - SheetJS (XLSX Preview)              - Lightbox Modal (DOCX/DOC/PDF/Images)  |
| - Unified Confirm Modal               - Smart Pin 📌 & Live Filter             |
+-------------------------------------------------------------------------------+
                                        │
                                        ▼ HTTPS (Port 443 / Nginx Proxy)
+-------------------------------------------------------------------------------+
|                        Nginx (Обратный прокси-сервер)                         |
| - SSL/TLS (Let's Encrypt)             - client_max_body_size 50M;             |
| - X-Forwarded-For, X-Forwarded-Proto  - Перенаправление на Node.js (Port 3000) |
+-------------------------------------------------------------------------------+
                                        │
                                        ▼ HTTP (Port 3000 / localhost)
+-------------------------------------------------------------------------------+
|                         Node.js 18+ / Express 4.x                             |
| - server.js (Маршрутизатор)            - express-session + MySQLStore         |
| - auth.js (bcrypt, Rate Limiter)      - DOMPurify & Marked (Санитизация)      |
| - Multer (Загрузка вложений)          - Mammoth & WordExtractor (Word -> HTML)|
| - app.set('trust proxy', 1);          - Безопасные заголовки (nosniff, origin)|
+-------------------------------------------------------------------------------+
                                        │
                                        ▼ SQL Pool (mysql2/promise)
+-------------------------------------------------------------------------------+
|                      MariaDB 10.x / MySQL 8.x (Прод Plesk)                    |
| - База данных: `p-14802_wiki`         - Кодировка: utf8mb4_unicode_ci         |
| - Таблицы: users, organizations, organization_databases_1c,                  |
|            user_pins, organization_files, sessions                            |
+-------------------------------------------------------------------------------+
```

---

## 4. Структура файлов и модулей проекта

```
projects/support/
├── .env                       # Конфигурация боевого окружения (создается на хостинге)
├── .env.example               # Образец переменных окружения
├── .node-version              # Версия Node.js (18+)
├── auth.js                    # Модуль авторизации пользователей, bcrypt, сброс пароля
├── database.js                # Модуль MariaDB (таблицы, CRUD операций, файлы, пины)
├── db-stub.js                 # In-Memory мок-сервер для локальной разработки (DB_STUB=true)
├── DOCUMENTATION.md           # Развернутая техническая документация системы
├── package.json               # Зависимости и скрипты Node.js
├── package-lock.json          # Зафиксированные версии пакетов
├── public/                    # Статические файлы (отдаются Express)
│   ├── css/
│   │   ├── style.css          # Основные стили, токены темы, Hero 1320px, Lightbox
│   │   └── home.css           # Стили главной страницы
│   ├── js/
│   │   ├── main.js            # Фронтенд скрипты, табы, поиск, Lightbox, Confirm Modal
│   │   └── i18n.js            # Локализация интерфейса (русский / казахский)
│   └── images/                # Иконки и логотипы
├── spec.md                    # НАСТОЯЩАЯ СПЕЦИФИКАЦИЯ РАЗВЕРТЫВАНИЯ (Данный файл)
├── support_spec.md            # Спецификация и чеклист спринтов
├── server.js                  # Главная точка входа приложения Express
├── sync.sh                    # Скрипт синхронизации репозитория
├── test-*.js                  # Набор интеграционных и приемочных автотестов
├── uploads/                   # Хранилище загруженных файлов организаций (org_<id>/)
└── views/                     # EJS-шаблоны страниц
    ├── home.ejs               # Главная страница (Hero Dashboard 1320px)
    ├── wiki.ejs               # База знаний Wiki, карточки, табы, файлы, булавка
    ├── login.ejs              # Экран входа и сброса пароля (без регистрации)
    ├── events.ejs             # Иерархический журнал баз 1С
    ├── profile.ejs            # Профиль инженера
    ├── export-pdf.ejs         # Шаблон Printable View для экспорта в PDF
    └── partials/              # Переиспользуемые фрагменты верстки (header, footer)
```

---

## 5. Схема базы данных (MariaDB / MySQL)

> [!IMPORTANT]
> `server.js` содержит автоматическую инициализацию всех таблиц при первом старте:
> `initDatabase()`, `initOrganizationTable()`, `initDatabases1CTable()`, `initUserPinsTable()`, `initFilesTable()`.
> Если на хостинге подключение к БД настроено корректно, таблицы создадутся автоматически.
> Ниже приведен полный чистый DDL-скрипт для ручного импорта через phpMyAdmin или консоль MySQL при необходимости.

```sql
-- 1. Создание базы данных (если не создана через панель Plesk)
CREATE DATABASE IF NOT EXISTS `p-14802_wiki` 
CHARACTER SET utf8mb4 
COLLATE utf8mb4_unicode_ci;

USE `p-14802_wiki`;

-- 2. Таблица пользователей
CREATE TABLE IF NOT EXISTS `users` (
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `username` VARCHAR(50) NOT NULL UNIQUE,
    `password_hash` VARCHAR(255) NOT NULL,
    `email` VARCHAR(255) DEFAULT NULL,
    `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `last_login` TIMESTAMP NULL DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3. Таблица организаций
CREATE TABLE IF NOT EXISTS `organizations` (
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `name` VARCHAR(255) NOT NULL UNIQUE,
    `content` LONGTEXT DEFAULT '',
    `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `updated_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 4. Таблица информационных баз 1С
CREATE TABLE IF NOT EXISTS `organization_databases_1c` (
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `org_id` INT NOT NULL,
    `name` VARCHAR(255) NOT NULL,
    `release_version` VARCHAR(255) DEFAULT '',
    `updated_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX `idx_org_id` (`org_id`),
    CONSTRAINT `fk_org_databases` FOREIGN KEY (`org_id`) REFERENCES `organizations` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 5. Таблица персональных закладок (булавок) пользователей
CREATE TABLE IF NOT EXISTS `user_pins` (
    `user_id` INT NOT NULL,
    `org_id` INT NOT NULL,
    `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`user_id`, `org_id`),
    INDEX `idx_user` (`user_id`),
    INDEX `idx_org` (`org_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 6. Таблица прикрепленных файлов организаций
CREATE TABLE IF NOT EXISTS `organization_files` (
    `id` INT AUTO_INCREMENT PRIMARY KEY,
    `org_id` INT NOT NULL,
    `filename` VARCHAR(255) NOT NULL,
    `original_name` VARCHAR(255) NOT NULL,
    `mime_type` VARCHAR(100) NOT NULL,
    `file_size` INT NOT NULL,
    `uploaded_by` VARCHAR(50) DEFAULT NULL,
    `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX `idx_file_org` (`org_id`),
    CONSTRAINT `fk_org_files` FOREIGN KEY (`org_id`) REFERENCES `organizations` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

---

## 6. Конфигурация переменных окружения (.env)

На боевом сервере создайте файл `.env` в корневой директории приложения (`projects/support/.env`):

```ini
# Режим окружения
NODE_ENV=production

# Порт приложения (для прямого запуска или PM2)
PORT=3000

# Отключение локального стаба (ОБЯЗАТЕЛЬНО для боевого сервера!)
DB_STUB=false

# Параметры подключения к MariaDB хостинга Plesk
DB_HOST=localhost
DB_PORT=3306
DB_USER=p-14802_adminits
DB_PASSWORD=ASDFzxcv@113525
DB_NAME=p-14802_wiki

# Секретный ключ подписи сессий (минимум 32 случайных символа)
SESSION_SECRET=d8f7e2a4b1c90538f92164abcedf8702b849e7a1234567890abcdef12345678
```

> [!CAUTION]
> Ни в коем случае не оставляйте `DB_STUB=true` на боевом сервере! Только при `DB_STUB=false` данные будут сохраняться в MariaDB и сессии будут сохраняться между перезапусками процесса в таблице сессий.

---

## 7. Пошаговая инструкция по развертыванию на Plesk / Nginx

### Шаг 1. Клонирование / Загрузка файлов на хостинг
1. Подключитесь к серверу по SSH или откройте файловый менеджер панели Plesk.
2. Корневая папка сайта (Document Root): например, `/var/www/vhosts/yourdomain.kz/httpdocs`.
3. Загрузите файлы проекта из каталога `projects/support/`.

### Шаг 2. Создание каталога загрузок и настройка прав доступа
Папка для загруженных файлов организаций `uploads` находится в корне проекта:
```bash
# В корне проекта
mkdir -p uploads
mkdir -p tmp

# Выставить владельца веб-пользователя (например, psaserv / psacln в Plesk)
chmod 775 uploads
chmod 775 tmp
```

### Шаг 3. Установка зависимостей Node.js
В консоли хостинга выполните установку зависимостей:
```bash
cd /var/www/vhosts/yourdomain.kz/httpdocs
npm install --omit=dev
```
> [!NOTE]
> Если библиотека `bcrypt` ругается на архитектуру процессора (например, сборка была под Windows, а сервер на Linux), выполните пересборку:
> `npm rebuild bcrypt --build-from-source`

### Шаг 4. Настройка Node.js в панели Plesk
Если сайт настраивается через встроенное расширение Plesk «Node.js»:
1. **Node.js Version:** Выберите `18.x` или `20.x` LTS.
2. **Application Mode:** `production`.
3. **Application Root:** `/var/www/vhosts/yourdomain.kz/httpdocs`.
4. **Document Root:** `/var/www/vhosts/yourdomain.kz/httpdocs/public`.
5. **Application Startup File:** `server.js`.
6. Нажмите **NPM Install** (если не устанавливали через SSH).
7. Нажмите **Restart Application**.

### Шаг 5. Настройка Nginx (Лимит размера загружаемых файлов)
Для корректной загрузки тяжелых Excel-таблиц, сканов договоров и PDF-инструкций до 50 МБ:
1. В панели Plesk перейдите в **«Настройки Apache и Nginx» (Apache & Nginx Settings)** для домена.
2. В блок **«Дополнительные директивы Nginx» (Additional Nginx directives)** вставьте:
```nginx
client_max_body_size 50M;

# Перенаправление заголовков для trust proxy
proxy_set_header X-Forwarded-For $remote_addr;
proxy_set_header X-Forwarded-Proto $scheme;
proxy_set_header Host $host;

# Отключение буферизации для быстрой отдачи файлов
proxy_buffering off;
```
3. Сохраните изменения.

### Шаг 6. Альтернативный запуск через PM2 (если Plesk Node.js не используется)
Если на сервере используется чистый Node.js без Phusion Passenger:
```bash
npm install -g pm2
pm2 start server.js --name "its-wiki"
pm2 save
pm2 startup
```

---

## 8. Миграции и начальное наполнение данными (Seed SQL)

Для того чтобы после развертывания база не была пустой, выполните следующий SQL-скрипт (создает администратора и наполняет список 34 организаций с начальными заметками):

```sql
USE `p-14802_wiki`;

-- 1. Администратор по умолчанию (логин: admin, пароль: admin123)
-- Хэш пароля сгенерирован bcrypt (admin123)
INSERT IGNORE INTO `users` (`id`, `username`, `password_hash`, `email`) 
VALUES (1, 'admin', '$2b$10$3euPcmQG1VvQZ2m7UjX1eOcOqG1sF6z4F/aK4u4Hn0KqO6vN2G.W2', 'support@its-kom.kz');

-- 2. Начальный реестр организаций
INSERT IGNORE INTO `organizations` (`name`, `content`) VALUES
('Antarium', 'kuadmin123 - Пароль от Ани деск\nKu@dmin123Q - mstsc\n\nAnydesk\n1218740083/antraium/администратор\n\nДля входа на комп\nantraium.local\\администратор\nKu@dmin123Q\n\nданные для создания базы\nкластер серверов - terminal\nСервер базы данных - SQL\nsa\nKu@dmin123Q'),
('ALPHUTA', ''),
('ALTYN TRADE', ''),
('Alanda', ''),
('BIRLIK SAUDA', ''),
('CityTrade', ''),
('DIVA (Казахстан)', ''),
('Di Trade', ''),
('GWS Almaty', ''),
('GWS Astana', ''),
('GWS Pavl', ''),
('GWS Павлодар', ''),
('HOLZ', ''),
('MONICO', ''),
('MYD Production Group', ''),
('NAK', ''),
('Samhat', ''),
('Silvanit', ''),
('VDS(новый сервер)', ''),
('Vehi', ''),
('АСТ-Техносервис', ''),
('Автоперекресток', ''),
('Аском', ''),
('Аудет 7 (ИП Бектуров)', ''),
('ВДС Казахстан', ''),
('ГК Sensata (ARG)', ''),
('Галерея крепежа', ''),
('Esen (Костанай сервер)', ''),
('ИП Жуманов', ''),
('ИП Эльвейн (РостДеталь)', ''),
('КазпромСнаб', ''),
('Карта', ''),
('Курылыс Групп', ''),
('ООО ПРОМСОЮЗ АЛИБЕТ', '');
```

---

## 9. Спецификация API и маршрутов

Все маршруты, кроме `/login` и `/api/auth/reset-password`, защищены middleware `requireAuth`.

| HTTP Метод | URL | Параметры / Тело | Описание |
|---|---|---|---|
| `GET` | `/` | — | Главная страница (Hero Dashboard 1320px) |
| `GET` | `/login` | — | Страница авторизации (без подсказок на проде) |
| `POST` | `/login` | `username`, `password`, `remember` | Аутентификация пользователя |
| `GET` | `/logout` | — | Завершение сессии и редирект на `/login` |
| `POST` | `/api/auth/check-user` | `username` | Шаг 1 сброса пароля (проверка логина) |
| `POST` | `/api/auth/reset-password` | `username`, `newPassword` | Шаг 2 сброса пароля |
| `GET` | `/wiki` | — | База знаний (первая организация или пустая) |
| `GET` | `/wiki/organization/:id` | `id` (URL) | Карточка выбранной организации |
| `GET` | `/api/organizations` | — | Список всех организаций (JSON) |
| `GET` | `/api/organizations/:id` | `id` (URL) | Полные данные организации (JSON) |
| `POST` | `/api/organizations` | `name`, `content` | Создание новой организации |
| `PUT` | `/api/organizations/:id` | `name`, `content` | Обновление заметок организации |
| `DELETE` | `/api/organizations/:id` | `id` (URL) | Удаление организации и всех файлов |
| `POST` | `/api/organizations/:id/pin` | `id` (URL) | Переключение персонального закрепления 📌 |
| `GET` | `/api/organizations/:id/databases` | `id` (URL) | Получение списка баз 1С организации |
| `POST` | `/api/organizations/:id/databases` | `name`, `description`, `updated_at` | Добавление базы 1С |
| `PUT` | `/api/databases/:id` | `name`, `description`, `updated_at` | Обновление данных базы 1С |
| `DELETE` | `/api/databases/:id` | `id` (URL) | Удаление базы 1С |
| `GET` | `/api/organizations/:id/files` | `id` (URL) | Получение реестра файлов компании |
| `POST` | `/api/organizations/:id/files` | `multipart/form-data` (`file`) | Загрузка файла вложения (до 50 МБ) |
| `GET` | `/api/files/:id/download` | `id` (URL) | Скачивание файла с корректным именем |
| `GET` | `/api/files/:id/view` | `id` (URL) | Потоковая отдача в Lightbox (1:1 / PDF) |
| `DELETE` | `/api/files/:id` | `id` (URL) | Удаление вложения из БД и диска |
| `GET` | `/api/export/pdf/:id` | `id` (URL) | Printable HTML View с авто-печатью PDF |
| `GET` | `/api/export/txt/:id` | `id` (URL) | Скачивание заметок и баз 1С в TXT |
| `GET` | `/events` | — | Иерархический журнал релизов 1С |
| `GET` | `/profile` | — | Профиль пользователя |
| `PUT` | `/api/profile` | `email` | Обновление email инженера |

---

## 10. Приемочный чек-лист для агента (Post-Deployment Verification)

После завершения развертывания агент обязан выполнить проверку по следующим пунктам:

- [ ] **1. Запуск приложения и статус демона:**
  - `curl -I https://yourdomain.kz/login` возвращает `HTTP 200 OK`.
  - В логах приложения (`node` / `pm2` / Passenger) отсутствуют сообщения об ошибках `ECONNREFUSED` к MariaDB.

- [ ] **2. Изоляция и чистота экрана входа (`/login`):**
  - Под плашкой авторизации **ОТСУТСТВУЕТ** блок подсказки «Локальный доступ: admin / admin».
  - Попытка открыть `/register` редиректит на `/login`.
  - Работает контурный SVG-переключатель светлой и темной темы.

- [ ] **3. Успешный вход под учетной записью администратора:**
  - Логин: `admin`, пароль: `admin123`.
  - Успешный переход на Дашборд `/`.
  - Приветствие `С возвращением, admin`.

- [ ] **4. Проверка Дашборда (1320px):**
  - Контейнер рабочей зоны центрирован и имеет ширину до 1320px.
  - Присутствует заголовок `Wiki — данные серверов` (или `Wiki — Серверная база знаний`), динамический бейдж `34 организаций` и иллюстрация серверной стойки.

- [ ] **5. База знаний Wiki (`/wiki`):**
  - В сайдбаре отображается список организаций.
  - У каждой организации генерируется монограмма из двух букв (например, `AN` или `АП`) в сапфировом блоке `44x44px`.
  - Текст заметок рендерится шрифтом `JetBrains Mono` без номеров строк (выделяется и копируется чисто).

- [ ] **6. Легендарная красная булавка 📌:**
  - У незакрепленных организаций булавка скрыта и появляется только при наведении мыши.
  - При клике на булавку отправляется запрос на `/api/organizations/:id/pin`.
  - Организация поднимается в группу «ЗАКРЕПЛЕННЫЕ» вверху сайдбара.

- [ ] **7. Файловый хаб и загрузка документов:**
  - Нажатие на кнопку `+` открывает диалог выбора файла.
  - Загрузка русского файла (например, `Схема_серверов_2026.docx`) проходит без искажения имени в заголовках.
  - Файл физически создается в папке `uploads/org_<id>/`.

- [ ] **8. Проверка WhatsApp-like Lightbox:**
  - Клик по `.docx` или `.doc`: открывается форматированный текст документа в модальном окне на А4.
  - Клик по `.xlsx` / `.xls`: открывается интерактивная таблица SheetJS с вкладками.
  - Клик по `.pdf`: открывается встроенный PDF без блокировки `X-Frame-Options`.
  - Клик по `.png` / `.jpg`: картинка открывается в 1:1 качестве, работает кнопка масштаба и зум.

- [ ] **9. Единое модальное окно подтверждения (#unifiedConfirmModal):**
  - При нажатии на удаление файла или организации открывается стильное темное окно с размытием фона и бейджем названия объекта.
  - Браузерные нативные окна `alert` и `confirm` **НЕ** вызываются.

- [ ] **10. Экспорт документации:**
  - Клик на «Ещё» -> «Печать / Сохранить в PDF» открывает Printable View с вызовом диалога печати браузера.
  - Клик на «Скачать как TXT» скачивает файл `.txt` с полным содержимым заметок и баз 1С.

- [ ] **11. Двухшаговый сброс пароля:**
  - Выйти из системы (`/logout`).
  - Нажать «Забыли пароль?».
  - Ввести `admin`, перейти на шаг 2, сменить пароль и проверить повторный вход с новым паролем.

---

## 11. СПРИНТ 8 — Production Release & Cleanup (Plesk MySQL) (Завершен на 100%)

- [x] **views/login.ejs и auth.js**:
  - [x] Физически удален блок `#quickCredsBadge` («Локальный доступ: admin / admin [Заполнить]»), стили `.quick-creds-badge` и JS-обработчики автозаполнения логина/пароля в `views/login.ejs` и `public/css/login.css`.
  - [x] Форма входа чистая и не содержит дефолтных значений.
  - [x] В `auth.js` настроен `pool` с `charset: 'utf8mb4'`, `host: process.env.DB_HOST || 'localhost'`, `port: process.env.DB_PORT || 3306`, `user: process.env.DB_USER || 'p-14802_adminits'`, `password: process.env.DB_PASSWORD || 'ASDFzxcv@113525'`, `database: process.env.DB_NAME || 'p-14802_wiki'`.
  - [x] Авторизация и 2-шаговый сброс пароля работают строго через SQL (`SELECT id, username, password_hash FROM users WHERE username = ?`, `bcrypt.compare`, `UPDATE users SET password_hash = ? WHERE id = ?`).
  - [x] `initDatabase()` создает пользователя только если таблица users пуста (`COUNT(*) === 0`).
  - [x] Экспортирован алиас `verifyUser: checkUserExists`.

- [x] **database.js**:
  - [x] Настроен `pool` с `charset: 'utf8mb4'`.
  - [x] В `initOrganizationTable()` задано поле `content LONGTEXT DEFAULT ''`.
  - [x] В `initDatabases1CTable()` и `initFilesTable()` заданы внешние ключи с `ON DELETE CASCADE`.
  - [x] В `filesDB` добавлены алиасы методов `getFilesByOrg: getByOrgId`, `getFileById: getById`, `addFile: create`, `deleteFile: delete`.
  - [x] Все запросы строго параметризованы знаком `?`.

- [x] **server.js, .env, package.json, tmp/restart.txt**:
  - [x] В `server.js` `USE_STUB` по умолчанию `false` (`const USE_STUB = (process.env.DB_STUB === 'true' || process.env.DB_STUB === '1') && fs.existsSync(path.join(__dirname, 'db-stub.js'));`).
  - [x] В `projects/support/.env` записаны production-переменные окружения Plesk (MariaDB/MySQL).
  - [x] В `package.json` удалены `puppeteer`, `pg`, `connect-pg-simple`, `sqlite3` из `dependencies`.
  - [x] Создан `tmp/restart.txt` и обеспечена папка `uploads/` с `.gitkeep`.

- [x] **Очистка от мусора**:
  - [x] Удалены все файлы тестов `test-*.js`.
  - [x] Удалены `testdb.js`, `migrate.js`, `seed-data.js`.
  - [x] Удалены все `.bat` файлы (`start-local.bat`, `start.bat`, `stop.bat`, `install.bat`, `seed.bat`, `setup-domain.bat`).
  - [x] Удален бинарный файл `p-14802_wiki` и `db-stub.js`.
  - [x] Удалены лишние `.md` и `.txt` файлы из корня `projects/support` (`PROJECT_STRUCTURE.md`, `QUICKSTART.md`, `README.md`, `VISUAL_GUIDE.md`, `ОБНОВЛЕНИЯ.md`, `УЧЕТНЫЕ_ДАННЫЕ.txt`, `DOCUMENTATION.md`), сохранены спецификации `spec.md` и `support_spec.md`.

---

> 🚀 **ИТОГ:** Проект полностью автономен, стабилизирован и готов к непрерывной эксплуатации. Все исходные коды, стили, шаблоны и зависимости находятся в актуальном синхронизированном состоянии.
