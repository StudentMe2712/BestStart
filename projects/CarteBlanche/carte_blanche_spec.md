# ТЕХНИЧЕСКОЕ ЗАДАНИЕ (SPEC.MD)

## Проект: Carte Blanche — Offline Luxe Menu & Tasting Guide (React Native + Expo)

---

## 1. Концепция и цель рефакторинга
* **Продукт:** Carte Blanche — автономное интерактивное меню и дегустационный гид премиум-класса высокой кухни (Haute Cuisine).
* **Целевая платформа:** iOS (через Expo Go / Native Build) + Live Web Preview для VS Code и браузеров.
* **Парадигма:** 100% Offline-First Native & Web приложение (функционирует полностью автономно без внешних серверов).
* **Стек:** React Native (0.86+), Expo SDK (57+), React (19.2+), TypeScript (6.0+), Expo Router (57+), Lucide Icons (`lucide-react-native`), React Native SVG (`react-native-svg`).
* **Ключевое требование к разработке (Vibe-Coding Loop):**
  * Приложение запускается командой `npx expo start --web` (или `npm run web`).
  * Во время работы агента Antigravity разработчик держит открытым окно предпросмотра (встроенный Simple Browser в VS Code или окно браузера рядом с редактором), либо держит в руках реальный iPhone с запущенным Expo Go.
  * Любые правки кода мгновенно отображаются на экране (Fast Refresh < 100 мс).
* **Стиль:** Dark Luxe Editorial (минимализм, глубокий графитовый/обсидиановый фон `#0E1013`, подложки темного стекла `#16191F`, акценты теплого шампанского золота `#E5A962`, сомелье-бордо `#8C2D38`, шалфейный `#5E8C6A`, шрифты с засечками в духе гида Мишлен и моноширинные цифры цен).

---

## 2. Архитектура проекта

```
projects/CarteBlanche/
├── app/                              # Файловый роутинг (Expo Router)
│   ├── _layout.tsx                   # Корневой лэйаут, TastingProvider, стили стека
│   ├── index.tsx                     # Главный экран Меню (поиск, фильтры, карточки, floating bar)
│   ├── dish/
│   │   └── [id].tsx                  # Модальный экран детализации блюда, терруар и сомелье
│   └── planner.tsx                   # Планировщик сета подач, заметки, рейтинг и сплит счета
├── src/
│   ├── components/
│   │   ├── common/
│   │   │   ├── GlassCard.tsx         # Карточка матового стекла с обводкой rgba(255,255,255,0.08)
│   │   │   ├── DietaryBadge.tsx      # Чипы диет и аллергенов (Gluten-Free, Vegan, Halal и др.)
│   │   │   └── FlavorRadarChart.tsx  # Чистый SVG радар-чарт 5 осей вкуса с RU/EN локализацией
│   │   ├── menu/
│   │   │   ├── CategoryPills.tsx     # Горизонтальный скролл курсов с золотым индикатором
│   │   │   └── DishCard.tsx          # Журнальная карточка блюда с быстрым добавлением в сет (+)
│   │   └── planner/
│   │       ├── CourseSection.tsx     # Группировка выбранных блюд по курсам, рейтинг 1-5 звезд, заметки
│   │       └── BillCalculator.tsx    # Интерактивный калькулятор счета, чаевых (0-20%) и сплита на гостей
│   ├── constants/
│   │   └── theme.ts                  # Цветовая палитра, типографика, отступы, радиусы, тени
│   ├── context/
│   │   └── TastingContext.tsx        # Глобальное оффлайн-состояние сета, отзывов и фильтров
│   ├── data/
│   │   ├── seedMenu.json             # 12 авторских блюд с полными дегустационными профилями
│   │   └── seedMenu.ts               # Типизированный экспорт датасета
│   └── types/
│       └── menu.ts                   # Интерфейсы MenuItem, CourseCategory, FlavorProfile, Pairing, etc.
├── swift_legacy/                     # Архив исходных компонентов SwiftUI
├── package.json                      # Зависимости Expo, скрипты запуска
├── tsconfig.json                     # Конфигурация TypeScript со строгим режимом
├── app.json                          # Настройки Expo и single SPA Web output
├── carte_blanche_spec.md             # Источник истины (Спецификация и ADR)
└── sync.sh                           # Автоматический скрипт синхронизации Git
```

---

## 3. Дизайн-система (Dark Luxe Editorial)

### Цветовая палитра
* **Obsidian Canvas (Фон экрана):** `#0E1013` (глубокий угольно-черный)
* **Card Surface (Поверхность карточек):** `#16191F` с тонкой каймой `rgba(255, 255, 255, 0.08)` (эффект матового темного стекла)
* **Champagne Accent (Основной акцент):** `#E5A962` (приглушенное теплое золото для цен, иконок и ключевых кнопок)
* **Burgundy Sub-Accent (Винный акцент):** `#8C2D38` (для сомелье-подборок и пейрингов)
* **Sage Green (Диетические метки):** `#5E8C6A` (веганское, вегетарианское, органика)
* **Slate Blue:** `#4A6B82` (безлактозное, орехи)
* **Ocean Teal:** `#2C7A7B` (морепродукты, пескетарианство)
* **Text Primary:** `#F5F6F8` (мягкий белый, 95% opacity)
* **Text Secondary:** `#8E95A5` (приглушенный сланцевый)

### Типографика
* **Заголовки и названия блюд:** Michelin-grade Serif (`Georgia`, `New York`, `serif`) для ощущения кулинарного артбука.
* **Основной текст и описание:** System Sans-Serif (`SF Pro`, `Inter`, `sans-serif`) с аккуратным межстрочным интервалом.
* **Цены и расчеты:** Monospaced (`SF Pro Rounded`, `Courier`, `monospace`) для строгого выравнивания чисел.

---

## 4. Схема данных (Domain Models)

```typescript
export type CourseCategory =
  | 'Prelude'
  | 'Main Courses'
  | 'Garden'
  | 'Desserts & Fromage'
  | 'Cellar';

export type DietaryTag =
  | 'Gluten-Free'
  | 'Vegan'
  | 'Vegetarian'
  | 'Halal'
  | 'Nut-Free'
  | 'Dairy-Free'
  | 'Pescatarian';

export interface FlavorProfile {
  umami: number;      // 0.0 - 1.0
  acidity: number;    // 0.0 - 1.0
  sweetness: number;  // 0.0 - 1.0
  spiciness: number;  // 0.0 - 1.0
  texture: number;    // 0.0 - 1.0
}

export interface Pairing {
  id: string;
  name: string;
  type: string;
  notes: string;
  temperature: string;
  producer: string;
  vintage: string;
}

export interface MenuItem {
  id: string;
  name: string;
  course: CourseCategory;
  price: number;
  description: string;
  culinaryStory: string;
  dietaryTags: DietaryTag[];
  flavorProfile: FlavorProfile;
  pairing: Pairing;
  heroImageSymbol: string;
  rating?: number;
  isChefSelection: boolean;
  origin?: string;
}

export interface DishFeedback {
  rating: number;     // 1 - 5
  notes: string;
  updatedAt: string;
}
```

---

## 5. Пошаговые майлстоуны реализации для Antigravity CLI

- [x] **ЭТАП 1: Инициализация Expo-окружения и Модели**
  - [x] Перемещение устаревших файлов Swift в изолированный каталог `swift_legacy/`.
  - [x] Развертывание Expo-проекта с поддержкой TypeScript, Web (`react-native-svg`, `lucide-react-native`).
  - [x] Настройка `package.json`, `app.json` (`web.output: "single"`), `tsconfig.json`, `babel.config.js`.
  - [x] Определение строгих интерфейсов в `src/types/menu.ts`.
  - [x] Создание полного датасета из 12 авторских блюд по 5 курсам в `src/data/seedMenu.json` и `src/data/seedMenu.ts`.

- [x] **ЭТАП 2: Дизайн-система и Атомарные компоненты**
  - [x] Конфигурация дизайн-токенов в `src/constants/theme.ts` (Obsidian, Champagne, Burgundy, Sage).
  - [x] Реализация `src/components/common/GlassCard.tsx` с темной матовой подложкой, бликом и обводкой `rgba(255,255,255,0.08)`.
  - [x] Реализация `src/components/common/DietaryBadge.tsx` с векторными иконками и поддержкой интерактивной фильтрации.
  - [x] Реализация `src/components/common/FlavorRadarChart.tsx` на чистом `react-native-svg` (5 концентрических пятиугольников, оси, полупрозрачный градиент заливки, золотые маркеры и двуязычные подписи осей RU/EN).

- [x] **ЭТАП 3: Каталог Меню и Интерактивная фильтрация**
  - [x] Реализация `src/context/TastingContext.tsx` с оффлайн-синхронизацией (`localStorage` / in-memory).
  - [x] Реализация `src/components/menu/CategoryPills.tsx` с горизонтальным скроллом и золотой подсветкой активного курса.
  - [x] Реализация `src/components/menu/DishCard.tsx` с составом, происхождением, моноширинной ценой и кнопкой быстрого добавления в дегустационный сет (+).
  - [x] Мгновенный полнотекстовый поиск по названию, описанию и терруару без задержек.

- [x] **ЭТАП 4: Модальное окно блюда и карточка Сомелье**
  - [x] Экран `app/dish/[id].tsx` с историей происхождения продукта и технологии приготовления ("Culinary Story & Terroir").
  - [x] Интеграция 5-осевого `FlavorRadarChart` с переключателем языка (`RU` / `EN` / `Dual`).
  - [x] Карточка сомелье в винных тонах Burgundy (`#8C2D38`) с винтажем, производителем, температурой подачи и вкусовой гармонией.
  - [x] Фиксированная нижняя кнопка добавления/удаления из дегустационного сета с актуальным состоянием.

- [x] **ЭТАП 5: Планировщик сета подач и оффлайн-сплит счета**
  - [x] Реализация `src/components/planner/CourseSection.tsx` с группировкой блюд по курсам (Prelude -> Main -> Garden -> Dessert -> Cellar).
  - [x] Интерактивное выставление оценки (1–5 звезд) и оффлайн-сохранение дегустационных заметок гостя.
  - [x] Реализация `src/components/planner/BillCalculator.tsx`: выбор чаевых (0%, 10%, 15%, 20%), сплит счета на 1–8 гостей и золотой блок суммы на персону.
  - [x] Экран `app/planner.tsx` с презентационным модальным окном итогового дегустационного фолио.

- [x] **ЭТАП 6: Синхронизация и запуск Live Preview**
  - [x] Проверка сборки Web-бандла (`npx expo export --platform web` выполнен успешно с кодом 0).
  - [x] Проверка компиляции TypeScript (`npx tsc --noEmit` — 0 ошибок).
  - [x] Актуализация спецификации `carte_blanche_spec.md` и ADR.
  - [x] Выполнение скрипта синхронизации `./sync.sh`.

---

## 6. Architectural Decision Records (ADR)

### ADR-007: Strategic Pivot to React Native + Expo for Vibe-Coding Loop
* **Контекст:** В первоначальной реализации на SwiftUI интерактивный предпросмотр требовал запуска тяжелого Xcode Canvas на macOS, что исключало работу на других рабочих станциях и не давало возможности держать живой Web Preview в соседней панели VS Code.
* **Решение:** Выполнен стратегический пивот на React Native + Expo SDK с Expo Router и TypeScript.
* **Последствия:** Мгновенный Live Preview через `npm run web` (Fast Refresh < 100 мс) как во встроенном браузере редактора, так и на физическом iPhone через QR-код Expo Go. Исходный код SwiftUI сохранен в архиве `swift_legacy/`.

### ADR-008: File-Based Routing with Expo Router
* **Контекст:** Премиальный пользовательский опыт требует плавной модальной презентации карточек блюд и быстрого перехода к дегустационному плану без перегрузки логики навигации.
* **Решение:** Использование `expo-router` с файловой структурой: `app/index.tsx` (главная витрина), `app/dish/[id].tsx` (модальное представление блюда и сомелье), `app/planner.tsx` (дегустационный фолио-планировщик).
* **Последствия:** Декларативная навигация, глубокие ссылки, типобезопасные маршруты (`experiments.typedRoutes: true`).

### ADR-009: Design System "Dark Luxe Editorial" on Pure React Native
* **Контекст:** Гастрономический гид уровня Michelin требует строгого вечернего визуального языка (Kinfolk / Vogue Gastronomie) с контрастными токенами и глассморфизмом без зависимости от тяжелых UI-фреймворков.
* **Решение:** Создание модуля `src/constants/theme.ts` и компонентов `GlassCard`, `DietaryBadge`:
  * Фон: Obsidian Canvas (`#0E1013`).
  * Поверхности: Card Surface (`#16191F`) с каймой `rgba(255, 255, 255, 0.08)`.
  * Акценты: Champagne Gold (`#E5A962`), Burgundy (`#8C2D38`), Sage Green (`#5E8C6A`).
* **Последствия:** Единый стиль на iOS, Android и Web без внешних CSS-библиотек.

### ADR-010: Pure Vector SVG 5-Axis Sensory Flavor Radar Chart
* **Контекст:** Визуализация вкусового аккорда (умами, кислотность, сладость, пряность, текстура) должна рендериться мгновенно на мобильных экранах и в браузере.
* **Решение:** Разработан компонент `FlavorRadarChart.tsx` на базе `react-native-svg`:
  * 5 концентрических пятиугольных уровней (20% – 100%).
  * Радиальный золотой градиент `champagneRadarGradient` с полупрозрачной заливкой.
  * Точки-вершины на графике и переключатель языка осей (`RU` / `EN` / `Dual`).
* **Последствия:** Векторная четкость при любом разрешении (Retina / 4K), нулевой runtime overhead.

### ADR-011: Offline-First State Persistence with TastingContext
* **Контекст:** Гостю требуется сохранять персональный дегустационный сет, оценки (1–5 звезд) и органолептические заметки без авторизации и облачных баз данных.
* **Решение:** Глобальный React Context `TastingContext.tsx` с автоматической синхронизацией в `localStorage` (на Web) и безопасным in-memory fallback.
* **Последствия:** Мгновенный отклик интерфейса, нулевая задержка, гарантированное сохранение заметок гостя между обновлениями страницы.

### ADR-012: Client-Side Single-Page Architecture (`output: "single"`)
* **Контекст:** При использовании React 19 режим `output: "static"` в Expo Router вызывал ошибку `resetServerContext is not a function`, поскольку серверный контекст устарел.
* **Решение:** Настройка `app.json`: `"web": { "bundler": "metro", "output": "single" }`.
* **Последствия:** Быстрая клиентская сборка Single Page App, полная совместимость с React 19, мгновенный экспорт в `dist/` без SSR-коллизий.

### ADR-013: Embedded iPhone 15 Pro Studio Canvas for Web Live Preview
* **Контекст:** При запуске веб-версии (`Platform.OS === 'web'`) для вайб-кодинга на широких десктопных мониторах или во встроенном браузере VS Code мобильный интерфейс без фиксированного вьюпорта растягивался на весь экран, искажая восприятие нативного мобильного UX.
* **Решение:** Реализован компонент `WebPhoneFrame.tsx`, автоматически оборачивающий приложение в элегантный корпус iPhone 15 Pro при десктопном разрешении (`width >= 450px`):
  * Точные размеры экрана: `393 × 852 px`.
  * Радиус скругления: `borderRadius: 48` с титановым безелем 4px (`#242831`).
  * Глубокая объемная тень: `boxShadow: 0 30px 90px rgba(0,0,0,0.85), 0 0 50px rgba(229,169,98,0.08)`.
  * Аутентичная имитация Dynamic Island (35px) и Home Indicator (4px).
  * Премиальный студийный фон `#07080A` с золотистым радиальным градиентом шампанского и редакционным лейблом.
  * Бесшовный проброс без оверхеда на нативных iOS/Android и мобильных экранах (`width < 450px`).
* **Последствия:** Идеальное визуальное погружение при вайб-кодинге в VS Code Simple Browser или Chrome без необходимости устанавливать дополнительные расширения или симуляторы.

### ADR-014: Full Haute Cuisine Russian Localization
* **Контекст:** Премиальный дегустационный гид Carte Blanche ориентирован на русскоязычных гостей ресторанов высокой кухни, требуя аутентичной гастрономической терминологии Мишлен-класса без англоязычных артефактов в интерфейсе.
* **Решение:** Полная локализация интерфейса и структурированного датасета:
  * В `src/types/menu.ts` добавлены константы и маппинги курсов («Прелюдия • Закуски», «Основные подачи», «Ботаника и сад», «Десерты и сыры», «Винный погреб и бар»), диетических тегов («Без глютена», «Веган», «Халяль» и др.) и зон залов («Главный зал», «Бар шефа», «Панорамная веранда»).
  * В `src/data/seedMenu.json` все 12 авторских блюд переведены на русский язык с сохранением высокой эстетики (описания, истории шефа, терруар происхождения и винные пары сомелье).
  * Полный перевод всех экранов, калькулятора счета, модального окна сомелье и радар-чарта вкусов (с сохранением переключателя языков RU / EN).
* **Последствия:** Безупречный эстетичный опыт взаимодействия для русскоязычной аудитории при сохранении обратной совместимости моделей.

### ADR-015: Infrastructure Upgrade to Expo SDK 57.0.0 (React 19 & React Native 0.86)
* **Контекст:** Переход на актуальный стек разработки требует поддержки новейших возможностей Expo SDK 57, React 19.2.3, React Native 0.86.3 и обновленного Metro Bundler для обеспечения максимальной скорости Live Web Preview и долгосрочной стабильности нативного рантайма.
* **Решение:** Выполнено комплексное обновление проекта:
  * `expo`: `~57.0.0` (с автоматическим выравниванием через `npx expo install --fix`).
  * `expo-router`: `~57.0.22`, `@expo/metro-runtime`: `~57.0.16`.
  * `react` / `react-dom`: `19.2.3`, `react-native`: `0.86.3`.
  * `typescript`: `~6.0.3` с обновленной структурой маппинга путей в `tsconfig.json`.
* **Последствия:** Улучшенная производительность компиляции и сборки бандла (2371 модуль без единого предупреждения), полная совместимость с экосистемой Expo SDK 57 и готовность к запуску в современных версиях Expo Go.