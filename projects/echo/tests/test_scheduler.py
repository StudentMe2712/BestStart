"""Scheduler logic tests: quiet-hour windows and role selection (spec §10)."""
from app.roles import window_for_hour
from app.scheduler import choose_role, within_quiet


def test_within_quiet_wraps_past_midnight():
    assert within_quiet(2, 23, 8) is True
    assert within_quiet(23, 23, 8) is True
    assert within_quiet(8, 23, 8) is False
    assert within_quiet(12, 23, 8) is False


def test_within_quiet_same_day_range():
    assert within_quiet(13, 12, 14) is True
    assert within_quiet(14, 12, 14) is False
    assert within_quiet(11, 12, 14) is False


def test_window_boundaries():
    assert window_for_hour(7).key == "morning"
    assert window_for_hour(12).key == "lunch"
    assert window_for_hour(15).key == "day"
    assert window_for_hour(20).key == "evening"


def test_window_is_none_at_night():
    assert window_for_hour(3) is None
    assert window_for_hour(23) is None


def test_choose_role_only_returns_enabled_roles(fresh_db):
    picks = {choose_role("day", None) for _ in range(80)}
    assert picks <= {"mentor", "friend"}
    assert picks  # at least one role chosen


def test_choose_role_returns_none_when_all_disabled(fresh_db):
    from app import db
    db.set_role_enabled("mentor", False)
    db.set_role_enabled("friend", False)
    assert choose_role("day", None) is None
