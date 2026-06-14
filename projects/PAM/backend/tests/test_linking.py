"""Unit tests — auto-link scoring (score_candidate). Чистая функция, без БД."""
from types import SimpleNamespace

from app.linking import MIN_CONFIDENCE, score_candidate


def _item(item_type, tags=None, project_id=None):
    return SimpleNamespace(
        item_type=item_type, tags=tags or [], project_id=project_id, id=None
    )


def test_type_pattern_idea_decision_becomes():
    rel, conf = score_candidate(_item("idea"), _item("decision"))
    assert rel == "became"
    assert conf >= MIN_CONFIDENCE


def test_shared_tags_yield_related_to():
    rel, conf = score_candidate(
        _item("note", ["mikrotik", "сеть", "nat"]), _item("note", ["mikrotik", "сеть"])
    )
    assert rel == "related_to"
    assert conf >= MIN_CONFIDENCE


def test_one_shared_tag_no_pattern_is_none():
    rel, conf = score_candidate(_item("note", ["x"]), _item("note", ["x"]))
    assert rel is None and conf == 0.0


def test_no_pattern_no_overlap_is_none():
    rel, conf = score_candidate(_item("tool"), _item("article"))
    assert rel is None and conf == 0.0


def test_same_project_boosts_confidence():
    p = "11111111-1111-1111-1111-111111111111"
    _, conf_same = score_candidate(
        _item("idea", project_id=p), _item("decision", project_id=p)
    )
    _, conf_diff = score_candidate(_item("idea"), _item("decision"))
    assert conf_same > conf_diff


def test_confidence_capped_at_one():
    p = "22222222-2222-2222-2222-222222222222"
    _, conf = score_candidate(
        _item("idea", ["a", "b", "c", "d"], project_id=p),
        _item("decision", ["a", "b", "c", "d"], project_id=p),
    )
    assert conf <= 1.0
