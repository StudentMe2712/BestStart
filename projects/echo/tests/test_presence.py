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


def test_every_role_defines_a_form_signal():
    # The form is what makes a register identifiable from one message — none may be blank.
    for key, spec in ROLES.items():
        assert spec.form.strip(), f"{key} must define a form signal"


def test_user_prompt_carries_the_role_form():
    window = WINDOWS[0]
    for key, spec in ROLES.items():
        assert spec.form in _user_prompt(spec, window, []), key


def test_presence_prompt_forbids_questions():
    window = WINDOWS[0]
    presence_prompt = _user_prompt(ROLES["presence"], window, [])
    # Presence is statement-only; its directive must explicitly bar a question.
    assert "Никогда не вопрос" in presence_prompt
