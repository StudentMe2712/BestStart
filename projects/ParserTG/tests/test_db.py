"""Persistence tests — run against a real throwaway SQLite file (no network)."""
from parsertg import db


def _fresh(tmp_path) -> str:
    path = str(tmp_path / "t.db")
    db.init_db(path)
    return path


def test_save_messages_and_stats(tmp_path):
    path = _fresh(tmp_path)
    db.sync_channels(path, ["@a", "@b"])
    channel_id = db.get_channels(path)[0]["id"]

    saved, max_id = db.save_messages(
        path,
        channel_id,
        [
            (10, "2024-01-01T10:00:00+00:00", "hello", "https://t.me/a/10"),
            (11, "2024-01-01T11:00:00+00:00", "world", "https://t.me/a/11"),
        ],
    )
    assert saved == 2
    assert max_id == 11

    stats = db.get_stats(path)
    assert stats.channel_count == 2
    assert stats.message_count == 2
    assert stats.last["message_id"] == 11


def test_save_messages_is_idempotent(tmp_path):
    path = _fresh(tmp_path)
    db.sync_channels(path, ["@a"])
    channel_id = db.get_channels(path)[0]["id"]

    db.save_messages(path, channel_id, [(10, "d", "first", "l")])
    saved, _ = db.save_messages(path, channel_id, [(10, "d", "first-again", "l")])
    assert saved == 0
    assert db.get_stats(path).message_count == 1


def test_recent_messages_newest_first(tmp_path):
    path = _fresh(tmp_path)
    db.sync_channels(path, ["@a"])
    channel_id = db.get_channels(path)[0]["id"]
    db.save_messages(path, channel_id, [(1, "d1", "first", "l1"), (2, "d2", "second", "l2")])

    rows = db.get_recent_messages(path, 10)
    assert rows[0]["message_id"] == 2  # most recently saved comes first


def test_sync_channels_keeps_cursor(tmp_path):
    path = _fresh(tmp_path)
    db.sync_channels(path, ["@a"])
    db.update_channel(path, db.get_channels(path)[0]["id"], last_message_id=50)

    db.sync_channels(path, ["@a", "@b"])  # re-sync must not reset existing cursors
    channels = {c["username"]: c for c in db.get_channels(path)}
    assert channels["@a"]["last_message_id"] == 50
    assert channels["@b"]["last_message_id"] == 0
    assert len(channels) == 2


def test_stats_empty(tmp_path):
    path = _fresh(tmp_path)
    stats = db.get_stats(path)
    assert stats.channel_count == 0
    assert stats.message_count == 0
    assert stats.last is None
