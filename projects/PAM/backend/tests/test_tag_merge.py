"""Unit tests — AI-теггер: слияние тегов (_merge_ai_tags). Чистая функция, без БД.

Ключевой кейс V1.2: решение, сохранённое с одним только st:<status>, всё равно
получает навигационные теги и категорию от теггера (служебные cat:/st: не считаем
за «пользовательские свободные теги»).
"""
from app.tagging import _merge_ai_tags


def test_fills_free_tags_when_only_status_present():
    out = _merge_ai_tags(["st:resolved"], ["docker", "nginx"], "docker", is_solution=True)
    assert "docker" in out and "nginx" in out  # навигационные теги добавлены
    assert "st:resolved" in out                # статус сохранён
    assert "cat:docker" in out                 # категория добавлена


def test_does_not_overwrite_user_free_tags():
    out = _merge_ai_tags(
        ["st:resolved", "mytag"], ["ai1", "ai2"], "docker", is_solution=True
    )
    assert "ai1" not in out and "ai2" not in out  # свои свободные теги не трогаем
    assert "mytag" in out and "st:resolved" in out
    assert "cat:docker" in out                     # cat всё равно дозаполняем


def test_empty_existing_takes_ai_tags():
    assert _merge_ai_tags([], ["a", "b"], None, is_solution=False) == ["a", "b"]


def test_category_not_added_for_non_solution():
    out = _merge_ai_tags(["st:resolved"], ["x"], "docker", is_solution=False)
    assert not any(t.startswith("cat:") for t in out)


def test_category_not_duplicated():
    out = _merge_ai_tags(
        ["st:resolved", "cat:python"], ["x"], "docker", is_solution=True
    )
    assert "cat:python" in out and "cat:docker" not in out
    assert out.count("cat:python") == 1


def test_dedup_and_cap_8():
    out = _merge_ai_tags(["st:resolved"], [f"t{i}" for i in range(12)], "docker", is_solution=True)
    assert len(out) <= 8 and len(out) == len(set(out))
