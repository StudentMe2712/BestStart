-- ============================================================================
-- Migration 002: VideoPlaybackQueue Table and Indexes for Second Self
-- ============================================================================

CREATE TABLE IF NOT EXISTS VideoPlaybackQueue (
    QueueOrder INTEGER PRIMARY KEY AUTOINCREMENT,
    VideoId TEXT NOT NULL,
    IsPlayed INTEGER NOT NULL DEFAULT 0,
    PlayedAt TEXT,
    FOREIGN KEY (VideoId) REFERENCES VideoLibrary(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_VideoPlaybackQueue_Played ON VideoPlaybackQueue (IsPlayed, QueueOrder ASC);
