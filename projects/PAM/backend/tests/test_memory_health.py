"""Unit tests — Memory Health derivation (компоненты качества памяти). Без БД."""
from app.routes.stats import (
    MEMORY_HEALTH_WEIGHTS,
    WEAK_SPOT_THRESHOLD,
    compute_memory_health,
    memory_health_label,
    memory_health_weak_spots,
)


def _all(v: int) -> dict:
    return {k: v for k in MEMORY_HEALTH_WEIGHTS}


def test_all_full_is_100():
    assert compute_memory_health(_all(100)) == 100


def test_all_zero_is_0():
    assert compute_memory_health(_all(0)) == 0


def test_weighted_single_component():
    comps = _all(0)
    comps["linked"] = 100
    assert compute_memory_health(comps) == round(
        100 * MEMORY_HEALTH_WEIGHTS["linked"]
    )


def test_weights_sum_to_one():
    assert round(sum(MEMORY_HEALTH_WEIGHTS.values()), 6) == 1.0


def test_label_thresholds():
    assert memory_health_label(85, 10) == "good"
    assert memory_health_label(60, 10) == "ok"
    assert memory_health_label(30, 10) == "poor"


def test_empty_memory_label_regardless_of_score():
    assert memory_health_label(0, 0) == "empty"
    assert memory_health_label(100, 0) == "empty"


def test_weak_spots_lists_low_components():
    comps = _all(100)
    comps["linked"] = WEAK_SPOT_THRESHOLD - 1
    comps["tags"] = 0
    spots = memory_health_weak_spots(comps)
    assert len(spots) == 2
    assert any("связ" in s for s in spots)
    assert any("тег" in s for s in spots)


def test_no_weak_spots_when_all_healthy():
    assert memory_health_weak_spots(_all(100)) == []
