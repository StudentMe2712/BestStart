"""Unit tests — Memory Health Score + capture reliability (pure helpers)."""
from app.routes.stats import (
    HEALTH_WEIGHTS,
    _pct,
    _reliability,
    compute_health,
    health_label,
)


def test_pct_empty_is_100():
    # нечего покрывать = здорово
    assert _pct(0, 0) == 100


def test_pct_basic():
    assert _pct(5, 10) == 50
    assert _pct(10, 10) == 100
    assert _pct(0, 10) == 0


def test_reliability():
    assert _reliability(0, 0) == 100  # не было захватов
    assert _reliability(9, 1) == 90
    assert _reliability(0, 5) == 0


def test_weights_sum_to_one():
    assert round(sum(HEALTH_WEIGHTS.values()), 6) == 1.0


def test_compute_health_all_perfect():
    comps = {"capture": 100, "indexing": 100, "review": 100, "stability": 100}
    assert compute_health(comps) == 100


def test_compute_health_weighted():
    # только indexing просел до 0 (нет Ollama) → теряем ровно вес indexing (25)
    comps = {"capture": 100, "indexing": 0, "review": 100, "stability": 100}
    assert compute_health(comps) == 75


def test_health_label_thresholds():
    assert health_label(100) == "good"
    assert health_label(80) == "good"
    assert health_label(79) == "ok"
    assert health_label(50) == "ok"
    assert health_label(49) == "poor"
    assert health_label(0) == "poor"
