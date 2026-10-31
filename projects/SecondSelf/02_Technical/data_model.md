# Data Model — Модель данных, схема SQLite и миграции Second Self

**Документ:** `02_Technical/Data_Model.md`
**Этап:** 3. Технический анализ
**Статус:** Версия 1.0 (На согласовании)

## 1. Концептуальная архитектура хранилища (Local-First Database)

База данных **Second Self** размещается в локальном файле `Data/secondself.db` и обслуживается движком SQLite 3 через библиотеку `Microsoft.Data.Sqlite`.

### Ключевые параметры подключения (PRAGMA Settings)

При каждом открытии соединения приложение выполняет инициализационную настройку:

```
PRAGMA journal_mode = WAL;          -- Параллельное чтение без блокировки потоков записи
PRAGMA synchronous = NORMAL;        -- Высокая скорость I/O при сохранении целостности транзакций
PRAGMA foreign_keys = ON;           -- Строгий контроль целостности внешних ключей
PRAGMA busy_timeout = 5000;         -- 5 секунд ожидания при конкурентных операциях
PRAGMA cache_size = -8000;          -- 8 МБ кэша в оперативной памяти
PRAGMA encoding = 'UTF-8';

```

## 2. ER-диаграмма сущностей (Entity-Relationship)

```
┌───────────────────────┐             ┌─────────────────────────┐
│       BigGoals        │1           N│        Milestones       │
├───────────────────────┼─────────────┼─────────────────────────┤
│ PK  Id (UUID)         │             │ PK  Id (UUID)           │
│     Title             │             │ FK  GoalId (UUID)       │
│     Motivation        │             │     OrderIndex (INT)    │
│     TargetResult      │             │     Title               │
│     TargetPeriod      │             │     Status              │
│     Status            │             │     CompletedAt         │
└───────────────────────┘             └────────────┬────────────┘
                                                   │1
                                                   │
                                                   │N
┌───────────────────────┐             ┌────────────┴────────────┐
│      WeeklyGoals      │             │       ActiveTasks       │
├───────────────────────┤             ├─────────────────────────┤
│ PK  Id (UUID)         │             │ PK  Id (UUID)           │
│     IsoYear (INT)     │             │     Title               │
│     IsoWeek (INT)     │             │     CreatedAt           │
│     BelongsToMonth    │             │     OrderIndex (INT)    │
│ FK  MilestoneId (UUID)│             │     PostponedDays (INT) │
│     Title             │             └─────────────────────────┘
│     Status            │
│     LockedAt          │             ┌─────────────────────────┐
│     CompletedAt       │             │      ArchivedTasks      │
└───────────────────────┘             ├─────────────────────────┤
                                      │ PK  Id (UUID)           │
┌───────────────────────┐             │     OriginalTaskId      │
│      VideoLibrary     │1           N│     Title               │
├───────────────────────┼─────────────┤     TaskSource          │
│ PK  Id (UUID)         │             │     CreatedAt           │
│     FilePath          │             │     CompletedAt         │
│     FileName          │             │     LogicalDate (TEXT)  │
│     DurationSeconds   │             └─────────────────────────┘
│     PlaybackCount     │
│     IsMissing (BOOL)  │             ┌─────────────────────────┐
└───────────┬───────────┘             │     MonthlyRecaps       │
            │1                        ├─────────────────────────┤
            │                         │ PK  YearMonth (TEXT)    │
            │N                        │     FinalScore (REAL)   │
┌───────────┴───────────┐             │     DailyRatio (TEXT)   │
│    DailyRitualLogs    │             │     WeeklyRatio (TEXT)  │
├───────────────────────┤             │     DailyWeight (REAL)  │
│ PK  LogicalDate (TEXT)│             │     WeeklyWeight (REAL) │
│ FK  VideoId (UUID)    │             │     ReflectionText      │
│     LastPositionMs    │             │     IsFinalized (BOOL)  │
│     IsCompleted (BOOL)│             │     CompletedAt         │
│     CompletedAt       │             └─────────────────────────┘
└───────────────────────┘

```

## 3. Полная DDL-схема таблиц SQLite (Data Definition Language)

### 3.1. Задачи дня: ActiveTasks и ArchivedTasks

```
-- Активные задачи в списке Today's Focus
CREATE TABLE IF NOT EXISTS ActiveTasks (
    Id TEXT PRIMARY KEY,                       -- UUID v4
    Title TEXT NOT NULL,                       -- Текст задачи (2..120 символов)
    CreatedAt TEXT NOT NULL,                   -- ISO-8601 UTC (например, '2026-10-31T08:00:00Z')
    OrderIndex INTEGER NOT NULL DEFAULT 0,     -- Порядок сортировки в UI
    PostponedDays INTEGER NOT NULL DEFAULT 0   -- Сколько дней задача переносится без движения
);

-- Архив выполненных задач (хроника побед)
CREATE TABLE IF NOT EXISTS ArchivedTasks (
    Id TEXT PRIMARY KEY,                       -- UUID v4
    OriginalTaskId TEXT,                       -- Исходный Id задачи
    Title TEXT NOT NULL,                       -- Название выполненной задачи
    TaskSource TEXT NOT NULL DEFAULT 'Today',  -- Источник: 'Today' или 'Weekly'
    CreatedAt TEXT NOT NULL,                   -- Дата создания задачи
    CompletedAt TEXT NOT NULL,                 -- Точный штамп времени выполнения
    LogicalDate TEXT NOT NULL                  -- Логическая дата дня выполнения 'YYYY-MM-DD'
);

CREATE INDEX IF NOT EXISTS IX_ArchivedTasks_LogicalDate ON ArchivedTasks (LogicalDate);
CREATE INDEX IF NOT EXISTS IX_ArchivedTasks_CompletedAt ON ArchivedTasks (CompletedAt DESC);

```

### 3.2. Большие цели и Этапы: BigGoals и Milestones

```
-- Большие цели горизонта 1–3 года (максимум 4 активных)
CREATE TABLE IF NOT EXISTS BigGoals (
    Id TEXT PRIMARY KEY,                       -- UUID v4
    Title TEXT NOT NULL,                       -- Название (например, 'Financial freedom')
    Motivation TEXT,                           -- 'Почему это важно для меня'
    TargetResult TEXT,                         -- 'Какой образ результата должен быть достигнут'
    TargetPeriod TEXT,                         -- Целевой период (например, '2026—2028')
    IconKey TEXT NOT NULL DEFAULT 'Compass',   -- Ключ SVG-иконки: 'Wallet', 'Briefcase', 'Compass', 'Brain'
    Status TEXT NOT NULL DEFAULT 'Active',     -- 'Active', 'Archived', 'Completed'
    CreatedAt TEXT NOT NULL
);

-- Этапы достижения Большой цели (Milestones)
CREATE TABLE IF NOT EXISTS Milestones (
    Id TEXT PRIMARY KEY,                       -- UUID v4
    GoalId TEXT NOT NULL,                      -- Связь с BigGoals
    OrderIndex INTEGER NOT NULL,               -- Порядковый номер этапа (1, 2, 3...)
    Title TEXT NOT NULL,                       -- Название этапа ('Establish a stronger foundation')
    Status TEXT NOT NULL DEFAULT 'Upcoming',   -- 'Completed', 'InProgress', 'Upcoming', 'Future'
    CompletedAt TEXT,                          -- Заполняется при завершении этапа
    FOREIGN KEY (GoalId) REFERENCES BigGoals(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_Milestones_GoalId ON Milestones (GoalId, OrderIndex ASC);

```

### 3.3. Недельные цели: WeeklyGoals

```
-- Главное обязательство календарной недели (Weekly Goal)
CREATE TABLE IF NOT EXISTS WeeklyGoals (
    Id TEXT PRIMARY KEY,                       -- UUID v4
    IsoYear INTEGER NOT NULL,                  -- Календарный год по ISO-8601
    IsoWeek INTEGER NOT NULL,                  -- Номер недели (1..53)
    BelongsToMonth TEXT NOT NULL,              -- Месяц, к которому привязана неделя по четвергу: 'YYYY-MM'
    MilestoneId TEXT,                          -- Привязка к этапу Большой цели (NULL если цель самостоятельная)
    Title TEXT NOT NULL,                       -- Формулировка цели недели
    Status TEXT NOT NULL DEFAULT 'Active',     -- 'Active', 'Locked', 'Completed', 'Postponed', 'Cancelled'
    LockedAt TEXT,                             -- Момент блокировки от редактирования (Commitment Lock)
    CompletedAt TEXT,                          -- Точный штамп выполнения
    CancellationReason TEXT,                   -- Причина отмены/переноса (если статус Postponed/Cancelled)
    FOREIGN KEY (MilestoneId) REFERENCES Milestones(Id) ON DELETE SET NULL,
    CONSTRAINT UQ_WeeklyGoal_Year_Week UNIQUE (IsoYear, IsoWeek)
);

CREATE INDEX IF NOT EXISTS IX_WeeklyGoals_Month ON WeeklyGoals (BelongsToMonth);

```

### 3.4. Видеотека и ежедневные ритуалы: VideoLibrary и DailyRitualLogs

```
-- Локальная библиотека видеофайлов для Daily Reset
CREATE TABLE IF NOT EXISTS VideoLibrary (
    Id TEXT PRIMARY KEY,                       -- UUID v4
    FilePath TEXT NOT NULL UNIQUE,             -- Абсолютный путь к файлу на диске
    FileName TEXT NOT NULL,                    -- Имя файла ('sunrise_clouds.mp4')
    DurationSeconds REAL NOT NULL DEFAULT 0,   -- Длительность ролика
    ThumbnailCachePath TEXT,                   -- Путь к кэшированному превью в Thumbnails/
    PlaybackCount INTEGER NOT NULL DEFAULT 0,  -- Число успешных просмотров
    IsMissing INTEGER NOT NULL DEFAULT 0,      -- 1, если файл не найден на диске
    LastVerifiedAt TEXT NOT NULL               -- Дата последней проверки доступности
);

-- Журнал ежедневных ритуалов
CREATE TABLE IF NOT EXISTS DailyRitualLogs (
    LogicalDate TEXT PRIMARY KEY,              -- Логическая дата 'YYYY-MM-DD' (смена в 04:00)
    VideoId TEXT NOT NULL,                     -- Назначенное на день видео
    LastPositionMs INTEGER NOT NULL DEFAULT 0, -- Сохраненный таймкод для возобновления
    IsCompleted INTEGER NOT NULL DEFAULT 0,    -- 1, если просмотр подтвержден пользователем
    CompletedAt TEXT,                          -- Точный штамп нажатия кнопки "Засчитать выполнение"
    FOREIGN KEY (VideoId) REFERENCES VideoLibrary(Id)
);

CREATE INDEX IF NOT EXISTS IX_DailyRitualLogs_VideoId ON DailyRitualLogs (VideoId);

```

### 3.5. Итоги и рефлексии месяцев: MonthlyRecaps

```
-- Архивные ежемесячные отчеты (Monthly Recap)
CREATE TABLE IF NOT EXISTS MonthlyRecaps (
    YearMonth TEXT PRIMARY KEY,                -- Формат 'YYYY-MM' (например, '2026-10')
    FinalScore REAL NOT NULL,                  -- Совокупный процент (например, 77.66)
    DailyRatio TEXT NOT NULL,                  -- Текстовая дробь '26/31'
    WeeklyRatio TEXT NOT NULL,                 -- Текстовая дробь '3/4'
    DailyWeight REAL NOT NULL DEFAULT 0.30,    -- Зафиксированный вес ритуалов в этом месяце
    WeeklyWeight REAL NOT NULL DEFAULT 0.70,   -- Зафиксированный вес недельных целей
    ReflectionText TEXT,                       -- Текст осознанной рефлексии пользователя
    IsFinalized INTEGER NOT NULL DEFAULT 0,    -- 1, если месяц закрыт и иммутабелен
    CompletedAt TEXT NOT NULL                  -- Дата финализации отчета
);

```

## 4. Контроль целостности и триггеры базы данных

Для обеспечения правил проекта на уровне БД применяются триггеры SQLite:

```
-- 1. Защита от создания более 4 активных Больших целей
CREATE TRIGGER IF NOT EXISTS TR_BigGoals_MaxActiveCheck
BEFORE INSERT ON BigGoals
WHEN NEW.Status = 'Active' AND (SELECT COUNT(*) FROM BigGoals WHERE Status = 'Active') >= 4
BEGIN
    SELECT RAISE(FAIL, 'Превышен лимит активных целей: одновременно можно вести не более 4 Больших целей.');
END;

-- 2. Автоматический инкремент счетчика просмотров в VideoLibrary при подтверждении ритуала
CREATE TRIGGER IF NOT EXISTS TR_DailyRitualLogs_IncrementPlayback
AFTER UPDATE OF IsCompleted ON DailyRitualLogs
WHEN NEW.IsCompleted = 1 AND OLD.IsCompleted = 0
BEGIN
    UPDATE VideoLibrary
    SET PlaybackCount = PlaybackCount + 1
    WHERE Id = NEW.VideoId;
END;

```

## 5. Система миграций и версионирования схемы (Migration Engine)

* В базе создаётся системная сервисная таблица `__SchemaMigrations (Version INT PRIMARY KEY, AppliedAt TEXT, ScriptName TEXT)`.

* При старте приложения класс `DatabaseMigrationService` сканирует папку внедрённых SQL-ресурсов сборки `SecondSelf.Data.Migrations`:

  * `001_Initial_Schema.sql` — базовые таблицы и индексы версии 1.0.

  * `002_Triggers_And_Constraints.sql` — правила целостности.

* Каждая миграция выполняется в рамках единой транзакции `BEGIN TRANSACTION ... COMMIT;`.

* При возникновении ошибки транзакция откатывается (`ROLLBACK`), приложение записывает стек-трейс в `migrations.log` и безопасно останавливается с уведомлением пользователя, не повреждая данные.