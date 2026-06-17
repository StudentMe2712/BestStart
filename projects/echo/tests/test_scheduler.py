"""Scheduler logic tests: quiet-hour windows and role selection (spec §10)."""
from datetime import datetime, timezone

from app import db
from app.roles import window_for_hour
from app.scheduler import (
    CAP_LOW_FRAC,
    GAP_HIGH_FRAC,
    choose_role,
    effective_daily_cap,
    effective_min_gap,
    within_quiet,
)

_NOW = datetime.now(timezone.utc)


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
    picks = {choose_role("day", 15, None, _NOW) for _ in range(80)}
    assert picks <= {"friend", "presence"}
    assert picks  # at least one role chosen


def test_choose_role_returns_none_when_all_disabled(fresh_db):
    db.set_role_enabled("friend", False)
    db.set_role_enabled("presence", False)
    assert choose_role("day", 15, None, _NOW) is None


def test_coach_only_eligible_in_work_hours(fresh_db):
    db.set_role_enabled("friend", False)
    db.set_role_enabled("presence", False)
    db.set_role_enabled("coach", True)
    # Inside 10–17 Coach is the only enabled role -> always chosen.
    assert choose_role("lunch", 12, None, _NOW) == "coach"
    # Outside its window nothing is eligible.
    assert choose_role("evening", 20, None, _NOW) is None
    # A manual /now (respect_constraints=False) ignores the work-hour gate.
    assert choose_role("evening", 20, None, _NOW, respect_constraints=False) == "coach"


def test_coach_respects_per_role_cooldown(fresh_db):
    db.set_role_enabled("friend", False)
    db.set_role_enabled("presence", False)
    db.set_role_enabled("coach", True)
    # Just sent a Coach message -> within the 75-min cooldown -> unavailable...
    db.log_message(_NOW.isoformat(), "coach", "template", "Пора пройтись.", None)
    assert choose_role("lunch", 12, None, _NOW) is None
    # ...but a manual override still works.
    assert choose_role("lunch", 12, None, _NOW, respect_constraints=False) == "coach"


def test_effective_daily_cap_stays_in_range():
    week = [f"2026-06-{d:02d}" for d in range(1, 29)]
    low = round(35 * CAP_LOW_FRAC)
    for date in week:
        assert low <= effective_daily_cap(date, 35) <= 35


def test_effective_min_gap_stays_in_range():
    week = [f"2026-06-{d:02d}" for d in range(1, 29)]
    high = round(24 * GAP_HIGH_FRAC)
    for date in week:
        assert 24 <= effective_min_gap(date, 24) <= high


def test_effective_values_stable_within_a_day():
    # Pure function of the date → every tick of the same day (and after a restart) agrees.
    assert effective_daily_cap("2026-06-16", 35) == effective_daily_cap("2026-06-16", 35)
    assert effective_min_gap("2026-06-16", 24) == effective_min_gap("2026-06-16", 24)


def test_effective_values_vary_across_days():
    caps = {effective_daily_cap(f"2026-06-{d:02d}", 35) for d in range(1, 29)}
    gaps = {effective_min_gap(f"2026-06-{d:02d}", 24) for d in range(1, 29)}
    assert len(caps) > 1  # not a constant masquerading as a range
    assert len(gaps) > 1


def test_effective_cap_handles_degenerate_anchor():
    # max_per_day == 1 must not crash randint (low would round to 1).
    assert effective_daily_cap("2026-06-16", 1) == 1
