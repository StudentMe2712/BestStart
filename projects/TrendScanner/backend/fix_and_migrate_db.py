"""Database migration script ensuring all tables, columns, indexes, and seeds are up to date."""

import sqlite3
from app.db.database import get_db_connection, init_db, get_db_path
from app.db.dao import TrendsDAO, SourcesDAO

def migrate():
    print("Running database initialization & migration...")
    init_db(seed_default_sources=True)
    
    with get_db_connection() as conn:
        # Check and add ai_status if missing
        cols = [r["name"] for r in conn.execute("PRAGMA table_info(trends)").fetchall()]
        if "ai_status" not in cols:
            print("Adding missing column 'ai_status' to trends table...")
            conn.execute("ALTER TABLE trends ADD COLUMN ai_status TEXT NOT NULL DEFAULT 'pending';")
            conn.execute("CREATE INDEX IF NOT EXISTS idx_trends_ai_status ON trends(ai_status);")
            print("Column 'ai_status' added successfully.")
        else:
            print("Column 'ai_status' already present.")
            
        print("Updated columns in trends table:", [r["name"] for r in conn.execute("PRAGMA table_info(trends)").fetchall()])

    # Test DAO calls
    sources = SourcesDAO.get_all()
    print(f"Sources count: {len(sources)}")
    stats = TrendsDAO.get_stats()
    print(f"Stats: {stats}")
    trends = TrendsDAO.get_trends()
    print(f"Trends count: {len(trends)}")
    print("Migration and DAO validation complete!")

if __name__ == "__main__":
    migrate()
