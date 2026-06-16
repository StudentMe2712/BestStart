"""Presence register tests (Bible V2): a statement-only role, never a question."""
from app.generator import _user_prompt
from app.quality import check
from app.roles import ROLES, WINDOWS
from app.templates import templates_for


def test_presence_role_exists_and_is_a_statement():
    assert "presence" in ROLES
    assert ROLES["presence"].asks_question is False
    # Every other register still asks / questions as before.
    assert all(spec.asks_question for key, spec in ROLES.items() if key != "presence")


def test_presence_templates_pass_the_quality_gate():
    bank = templates_for("presence")
    assert bank, "presence must have a fallback bank"
    for text in bank:
        ok, reason = check(text, "presence", [])
        assert ok, f"{text!r} rejected as {reason}"


def test_user_prompt_drops_question_push_for_presence():
    window = WINDOWS[0]
    friend_prompt = _user_prompt(ROLES["friend"], window, [])
    presence_prompt = _user_prompt(ROLES["presence"], window, [])
    assert "лучше вопрос" in friend_prompt
    assert "без вопроса" in presence_prompt
    assert "лучше вопрос" not in presence_prompt
