-- ============================================================================
-- Migration 001: Initial Schema and Triggers for Second Self
-- ============================================================================

-- 1. ActiveTasks (Today's Focus)
CREATE TABLE IF NOT EXISTS ActiveTasks (
    Id TEXT PRIMARY KEY,
    Title TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    OrderIndex INTEGER NOT NULL DEFAULT 0,
    PostponedDays INTEGER NOT NULL DEFAULT 0
);

-- 2. ArchivedTasks
CREATE TABLE IF NOT EXISTS ArchivedTasks (
    Id TEXT PRIMARY KEY,
    OriginalTaskId TEXT,
    Title TEXT NOT NULL,
    TaskSource TEXT NOT NULL DEFAULT 'Today',
    CreatedAt TEXT NOT NULL,
    CompletedAt TEXT NOT NULL,
    LogicalDate TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_ArchivedTasks_LogicalDate ON ArchivedTasks (LogicalDate);
CREATE INDEX IF NOT EXISTS IX_ArchivedTasks_CompletedAt ON ArchivedTasks (CompletedAt DESC);

-- 3. BigGoals
CREATE TABLE IF NOT EXISTS BigGoals (
    Id TEXT PRIMARY KEY,
    Title TEXT NOT NULL,
    Motivation TEXT,
    TargetResult TEXT,
    TargetPeriod TEXT,
    IconKey TEXT NOT NULL DEFAULT 'Compass',
    Status TEXT NOT NULL DEFAULT 'Active',
    CreatedAt TEXT NOT NULL
);

-- 4. Milestones
CREATE TABLE IF NOT EXISTS Milestones (
    Id TEXT PRIMARY KEY,
    GoalId TEXT NOT NULL,
    OrderIndex INTEGER NOT NULL,
    Title TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Upcoming',
    CompletedAt TEXT,
    FOREIGN KEY (GoalId) REFERENCES BigGoals(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_Milestones_GoalId ON Milestones (GoalId, OrderIndex ASC);

-- 5. WeeklyGoals
CREATE TABLE IF NOT EXISTS WeeklyGoals (
    Id TEXT PRIMARY KEY,
    IsoYear INTEGER NOT NULL,
    IsoWeek INTEGER NOT NULL,
    BelongsToMonth TEXT NOT NULL,
    MilestoneId TEXT,
    Title TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Active',
    LockedAt TEXT,
    CompletedAt TEXT,
    CancellationReason TEXT,
    FOREIGN KEY (MilestoneId) REFERENCES Milestones(Id) ON DELETE SET NULL,
    CONSTRAINT UQ_WeeklyGoal_Year_Week UNIQUE (IsoYear, IsoWeek)
);

CREATE INDEX IF NOT EXISTS IX_WeeklyGoals_Month ON WeeklyGoals (BelongsToMonth);

-- 6. VideoLibrary
CREATE TABLE IF NOT EXISTS VideoLibrary (
    Id TEXT PRIMARY KEY,
    FilePath TEXT NOT NULL UNIQUE,
    FileName TEXT NOT NULL,
    DurationSeconds REAL NOT NULL DEFAULT 0,
    ThumbnailCachePath TEXT,
    PlaybackCount INTEGER NOT NULL DEFAULT 0,
    IsMissing INTEGER NOT NULL DEFAULT 0,
    LastVerifiedAt TEXT NOT NULL
);

-- 7. DailyRitualLogs
CREATE TABLE IF NOT EXISTS DailyRitualLogs (
    LogicalDate TEXT PRIMARY KEY,
    VideoId TEXT NOT NULL,
    LastPositionMs INTEGER NOT NULL DEFAULT 0,
    IsCompleted INTEGER NOT NULL DEFAULT 0,
    CompletedAt TEXT,
    FOREIGN KEY (VideoId) REFERENCES VideoLibrary(Id)
);

CREATE INDEX IF NOT EXISTS IX_DailyRitualLogs_VideoId ON DailyRitualLogs (VideoId);

-- 8. MonthlyRecaps
CREATE TABLE IF NOT EXISTS MonthlyRecaps (
    YearMonth TEXT PRIMARY KEY,
    FinalScore REAL NOT NULL,
    DailyRatio TEXT NOT NULL,
    WeeklyRatio TEXT NOT NULL,
    DailyWeight REAL NOT NULL DEFAULT 0.30,
    WeeklyWeight REAL NOT NULL DEFAULT 0.70,
    ReflectionText TEXT,
    IsFinalized INTEGER NOT NULL DEFAULT 0,
    CompletedAt TEXT NOT NULL
);

-- 9. Triggers
CREATE TRIGGER IF NOT EXISTS TR_BigGoals_MaxActiveCheck
BEFORE INSERT ON BigGoals
WHEN NEW.Status = 'Active' AND (SELECT COUNT(*) FROM BigGoals WHERE Status = 'Active') >= 4
BEGIN
    SELECT RAISE(FAIL, 'Превышен лимит активных целей: одновременно можно вести не более 4 Больших целей.');
END;

CREATE TRIGGER IF NOT EXISTS TR_DailyRitualLogs_IncrementPlayback
AFTER UPDATE OF IsCompleted ON DailyRitualLogs
WHEN NEW.IsCompleted = 1 AND OLD.IsCompleted = 0
BEGIN
    UPDATE VideoLibrary
    SET PlaybackCount = PlaybackCount + 1
    WHERE Id = NEW.VideoId;
END;
