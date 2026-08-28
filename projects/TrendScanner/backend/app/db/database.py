"""SQLite database initialization, migration, and connection management."""

import sqlite3
from contextlib import contextmanager
from pathlib import Path
from typing import Generator, Optional
from app.core.settings import settings

DDL_BASE_TABLES = """
PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;

CREATE TABLE IF NOT EXISTS sources (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    url TEXT NOT NULL,
    source_type TEXT NOT NULL,
    is_active INTEGER NOT NULL DEFAULT 1,
    last_scanned TIMESTAMP NULL
);

CREATE TABLE IF NOT EXISTS trends (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    source_id INTEGER NOT NULL,
    original_text TEXT NOT NULL,
    content_hash TEXT UNIQUE,
    parsed_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    is_trend INTEGER NOT NULL DEFAULT 0,
    trend_name TEXT NULL,
    ai_score INTEGER NULL,
    scam_probability INTEGER NULL,
    ai_summary TEXT NULL,
    source_url TEXT NULL,
    is_reviewed INTEGER NOT NULL DEFAULT 0,
    ai_status TEXT NOT NULL DEFAULT 'pending',
    mention_count INTEGER NOT NULL DEFAULT 1,
    detailed_report TEXT NULL,
    is_liked INTEGER NOT NULL DEFAULT 0,
    user_feedback INTEGER NOT NULL DEFAULT 0,
    is_new INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY (source_id) REFERENCES sources(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_trends_source_id ON trends(source_id);
CREATE INDEX IF NOT EXISTS idx_trends_content_hash ON trends(content_hash);
CREATE INDEX IF NOT EXISTS idx_trends_is_reviewed ON trends(is_reviewed);
CREATE INDEX IF NOT EXISTS idx_trends_ai_score ON trends(ai_score);
CREATE INDEX IF NOT EXISTS idx_trends_parsed_date ON trends(parsed_date);
CREATE INDEX IF NOT EXISTS idx_trends_mention_count ON trends(mention_count);
CREATE INDEX IF NOT EXISTS idx_trends_is_liked ON trends(is_liked);
CREATE INDEX IF NOT EXISTS idx_trends_user_feedback ON trends(user_feedback);
CREATE INDEX IF NOT EXISTS idx_trends_is_new ON trends(is_new);
"""

DEFAULT_SOURCES = [
    # SPA & Playwright
    (
        "Product Hunt Trending (SPA)",
        "https://www.producthunt.com/",
        "playwright_spa",
        1,
    ),
    (
        "Indie Hackers Products (SPA)",
        "https://www.indiehackers.com/products",
        "playwright_spa",
        1,
    ),
    # RSS / Atom Feeds
    (
        "Hacker News Best",
        "https://news.ycombinator.com/rss",
        "rss",
        1,
    ),
    (
        "Hacker News Show HN",
        "https://hnrss.org/show",
        "rss",
        1,
    ),
    (
        "TechCrunch Startups",
        "https://techcrunch.com/category/startups/feed/",
        "rss",
        1,
    ),
    (
        "Medium: SaaS",
        "https://medium.com/feed/tag/saas",
        "rss",
        1,
    ),
    (
        "Medium: Startups",
        "https://medium.com/feed/tag/startup",
        "rss",
        1,
    ),
    (
        "Medium: AI",
        "https://medium.com/feed/tag/artificial-intelligence",
        "rss",
        1,
    ),
    # Reddit Channels
    (
        "Reddit /r/SaaS",
        "https://www.reddit.com/r/SaaS/hot.json?limit=25",
        "reddit",
        1,
    ),
    (
        "Reddit /r/Entrepreneur",
        "https://www.reddit.com/r/Entrepreneur/hot.json?limit=25",
        "reddit",
        1,
    ),
    (
        "Reddit /r/startups",
        "https://www.reddit.com/r/startups/hot.json?limit=25",
        "reddit",
        1,
    ),
    (
        "Reddit /r/SideProject",
        "https://www.reddit.com/r/SideProject/hot.json?limit=25",
        "reddit",
        1,
    ),
    (
        "Reddit /r/GrowthHacking",
        "https://www.reddit.com/r/GrowthHacking/hot.json?limit=25",
        "reddit",
        1,
    ),
    (
        "Reddit /r/Flipping",
        "https://www.reddit.com/r/Flipping/hot.json?limit=25",
        "reddit",
        1,
    ),
    (
        "Reddit /r/technology",
        "https://www.reddit.com/r/technology/hot.json?limit=25",
        "reddit",
        1,
    ),
    # Telegram Channels
    (
        "Telegram: Tech Trends",
        "https://t.me/tech_trends",
        "telegram",
        1,
    ),
    (
        "Telegram: AI & SaaS Radar",
        "https://t.me/ai_startups_radar",
        "telegram",
        1,
    ),
    (
        "Telegram: @startupoftheday",
        "https://t.me/startupoftheday",
        "telegram",
        1,
    ),
    (
        "Telegram: @the_hustle_ru",
        "https://t.me/the_hustle_ru",
        "telegram",
        1,
    ),
    (
        "Telegram: @ycombinator_ru",
        "https://t.me/ycombinator_ru",
        "telegram",
        1,
    ),
]


def get_db_path() -> str:
    """Return database file path, ensuring parent directory exists."""
    path = Path(settings.DATABASE_PATH)
    path.parent.mkdir(parents=True, exist_ok=True)
    return str(path)


@contextmanager
def get_db_connection(db_path: Optional[str] = None) -> Generator[sqlite3.Connection, None, None]:
    """Context manager for SQLite connections with Row factory and PRAGMA configurations."""
    target_path = db_path if db_path is not None else get_db_path()
    Path(target_path).parent.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(
        target_path,
        timeout=10.0,
        detect_types=sqlite3.PARSE_DECLTYPES | sqlite3.PARSE_COLNAMES,
    )
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA foreign_keys = ON;")
    conn.execute("PRAGMA busy_timeout = 5000;")
    conn.execute("PRAGMA synchronous = NORMAL;")
    try:
        yield conn
        conn.commit()
    except Exception:
        conn.rollback()
        raise
    finally:
        conn.close()


def init_db(seed_default_sources: bool = True) -> None:
    """Initialize database tables, apply schema updates, and seed default sources."""
    with get_db_connection() as conn:
        # 1. Check if trends table already exists
        cursor = conn.execute("SELECT name FROM sqlite_master WHERE type='table' AND name='trends'")
        trends_exists = cursor.fetchone() is not None

        if not trends_exists:
            # Create fresh tables with all columns and indexes
            conn.executescript(DDL_BASE_TABLES)
            conn.execute("CREATE INDEX IF NOT EXISTS idx_trends_ai_status ON trends(ai_status);")
            conn.execute("CREATE INDEX IF NOT EXISTS idx_trends_is_new ON trends(is_new);")
            conn.execute("CREATE INDEX IF NOT EXISTS idx_trends_user_feedback ON trends(user_feedback);")
        else:
            # Ensure sources table exists
            conn.execute("""
                CREATE TABLE IF NOT EXISTS sources (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    url TEXT NOT NULL,
                    source_type TEXT NOT NULL,
                    is_active INTEGER NOT NULL DEFAULT 1,
                    last_scanned TIMESTAMP NULL
                );
            """)

            # Ensure ai_status, mention_count, detailed_report, is_liked, user_feedback, and is_new columns exist in trends table
            cols = [row["name"] for row in conn.execute("PRAGMA table_info(trends)").fetchall()]
            if "ai_status" not in cols:
                conn.execute("ALTER TABLE trends ADD COLUMN ai_status TEXT NOT NULL DEFAULT 'pending';")
            if "mention_count" not in cols:
                conn.execute("ALTER TABLE trends ADD COLUMN mention_count INTEGER NOT NULL DEFAULT 1;")
            if "detailed_report" not in cols:
                conn.execute("ALTER TABLE trends ADD COLUMN detailed_report TEXT NULL;")
            if "is_liked" not in cols:
                conn.execute("ALTER TABLE trends ADD COLUMN is_liked INTEGER NOT NULL DEFAULT 0;")
            if "user_feedback" not in cols:
                conn.execute("ALTER TABLE trends ADD COLUMN user_feedback INTEGER NOT NULL DEFAULT 0;")
            if "is_new" not in cols:
                conn.execute("ALTER TABLE trends ADD COLUMN is_new INTEGER NOT NULL DEFAULT 1;")

            # Migration: Convert existing is_liked = 1 rows to user_feedback = 1
            conn.execute("UPDATE trends SET user_feedback = 1 WHERE is_liked = 1 AND user_feedback = 0;")

            # Safe index creation
            conn.execute("CREATE INDEX IF NOT EXISTS idx_trends_source_id ON trends(source_id);")
            conn.execute("CREATE INDEX IF NOT EXISTS idx_trends_content_hash ON trends(content_hash);")
            conn.execute("CREATE INDEX IF NOT EXISTS idx_trends_is_reviewed ON trends(is_reviewed);")
            conn.execute("CREATE INDEX IF NOT EXISTS idx_trends_ai_score ON trends(ai_score);")
            conn.execute("CREATE INDEX IF NOT EXISTS idx_trends_parsed_date ON trends(parsed_date);")
            conn.execute("CREATE INDEX IF NOT EXISTS idx_trends_ai_status ON trends(ai_status);")
            conn.execute("CREATE INDEX IF NOT EXISTS idx_trends_mention_count ON trends(mention_count);")
            conn.execute("CREATE INDEX IF NOT EXISTS idx_trends_is_liked ON trends(is_liked);")
            conn.execute("CREATE INDEX IF NOT EXISTS idx_trends_user_feedback ON trends(user_feedback);")
            conn.execute("CREATE INDEX IF NOT EXISTS idx_trends_is_new ON trends(is_new);")

        # 2. Seed sources
        if seed_default_sources:
            cursor = conn.execute("SELECT COUNT(*) as cnt FROM sources")
            count = cursor.fetchone()["cnt"]
            if count == 0:
                conn.executemany(
                    """
                    INSERT INTO sources (name, url, source_type, is_active)
                    VALUES (?, ?, ?, ?)
                    """,
                    DEFAULT_SOURCES,
                )
            else:
                # Add any missing default sources by URL
                existing_urls = {
                    r["url"] for r in conn.execute("SELECT url FROM sources").fetchall()
                }
                for src_name, src_url, src_type, is_act in DEFAULT_SOURCES:
                    if src_url not in existing_urls:
                        conn.execute(
                            """
                            INSERT INTO sources (name, url, source_type, is_active)
                            VALUES (?, ?, ?, ?)
                            """,
                            (src_name, src_url, src_type, is_act),
                        )
