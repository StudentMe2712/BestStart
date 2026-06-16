"""Quality gate tests (spec §16.2)."""
from app.quality import check, jaccard, normalize_words


def test_accepts_a_good_message():
    ok, reason = check("Как вообще день?", "friend", [])
    assert ok and reason == "ok"


def test_rejects_aphorism_opener():
    ok, reason = check("Иногда такие события приводят к чему-то новому.", "friend", [])
    assert not ok and reason == "banal"


def test_rejects_banal_cliche():
    ok, reason = check("Верь в себя, и всё получится!", "friend", [])
    assert not ok and reason == "banal"


def test_rejects_bible_forbidden_phrases():
    # Forbidden "ЗАПРЕЩЕНО" tells from the Personality Bible V2 that the gate must catch.
    forbidden = [
        "Что если посмотреть под другим углом на эту задачу.",
        "Время не уходит, оно остаётся в нас.",
        "Сегодня есть шанс увидеть мир иначе.",
        "Настоящая ясность приходит в тишине.",
        "Сегодня может стать днём открытий.",
        "Мир откроется с другой стороны, если позволишь.",
    ]
    for text in forbidden:
        ok, reason = check(text, "philosopher", [])
        assert not ok and reason == "banal", text


def test_rejects_too_long():
    ok, reason = check("а" * 400, "coach", [])
    assert not ok and reason == "too_long"


def test_rejects_too_short():
    ok, reason = check(" ", "friend", [])
    assert not ok and reason == "too_short"


def test_rejects_toxic():
    ok, reason = check("ты тупой и ничтожество", "friend", [])
    assert not ok and reason == "toxic"


def test_rejects_message_too_similar_to_recent():
    recent = ["Что ты сейчас делаешь только по привычке сегодня вечером"]
    ok, reason = check("Что ты сейчас делаешь только по привычке сегодня вечером", "challenger", recent)
    assert not ok and reason == "repetitive"


def test_jaccard_identical_is_one():
    words = normalize_words("одна и та же мысль")
    assert jaccard(words, words) == 1.0


def test_jaccard_disjoint_is_zero():
    assert jaccard(normalize_words("утро день"), normalize_words("вечер ночь")) == 0.0


# --- ECHO HUMANITY V3 ---

def test_rejects_tired_friend_opener():
    ok, reason = check("Как дела?", "friend", [])
    assert not ok and reason == "tired"


def test_tired_opener_block_is_friend_only():
    # Other registers may still phrase things this way.
    ok, _ = check("Как там с тем, что застряло?", "mentor", [])
    assert ok


def test_philosopher_may_open_with_inogda():
    ok, reason = check("Иногда ожидание тяжелее самого события.", "philosopher", [])
    assert ok and reason == "ok"


def test_friend_still_blocked_on_aphorism_opener():
    ok, reason = check("Иногда всё меняется само собой.", "friend", [])
    assert not ok and reason == "banal"


def test_philosopher_fabricated_quote_with_guillemets_rejected():
    ok, reason = check("«Время — деньги.» — Бенджамин Франклин", "philosopher", [])
    assert not ok and reason == "fabricated_quote"


def test_philosopher_fabricated_quote_with_attribution_rejected():
    ok, reason = check("Жизнь коротка. — Сенека", "philosopher", [])
    assert not ok and reason == "fabricated_quote"


def test_coach_fitness_tracker_phrase_banned():
    ok, reason = check("Выпей воды и разомнись.", "coach", [])
    assert not ok and reason == "banal"


def test_challenger_do_nothing_loop_banned():
    ok, reason = check("А если ничего не менять?", "challenger", [])
    assert not ok and reason == "banal"
