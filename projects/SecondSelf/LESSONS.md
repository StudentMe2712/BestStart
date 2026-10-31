# Lessons Learned — SecondSelf

## Log

### 2026-10-31 — SQLite Online Backup and WAL Mode in WinUI 3
- **Context:** Implementing backup and WAL PRAGMA settings for `Data/secondself.db`.
- **Finding:** In SQLite WAL mode, copying the `.db` file directly while writers are active can lead to dirty reads or missing uncheckpointed WAL frames (`-wal` file). Using `SqliteConnection.BackupDatabase(destination)` performs an atomic, consistent online backup directly through the SQLite engine without blocking readers.
- **Rule:** Always use the native SQLite Online Backup API (`source.BackupDatabase(dest)`) rather than file-system copy for active SQLite databases in WAL mode.

### 2026-10-31 — ISO-8601 Thursday Rule for Monthly Progress Partitioning
- **Context:** Binding calendar weeks to months for progress calculations ($0.30 \times D + 0.70 \times W$).
- **Finding:** Days of a week can cross month boundaries (e.g., Monday in October, Friday in November). Adhering strictly to the ISO-8601 Thursday rule (a week belongs to the month of its Thursday) guarantees that every week uniquely belongs to exactly one month, resulting in strictly 4 or 5 weeks per month with zero overlap or orphan days.
- **Rule:** In progress engines combining daily and weekly cadence, partition weeks strictly by Thursday date.
