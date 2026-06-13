"""Unit tests — Fact Review Queue gate (which statuses reach chat memory)."""
from app.models import (
    ACCEPTED_FACT_STATUSES,
    FACT_APPROVED,
    FACT_EDITED,
    FACT_PENDING,
    FACT_REJECTED,
    is_fact_accepted,
)


def test_accepted_statuses_reach_memory():
    assert is_fact_accepted(FACT_APPROVED) is True
    assert is_fact_accepted(FACT_EDITED) is True


def test_pending_and_rejected_are_gated_out():
    assert is_fact_accepted(FACT_PENDING) is False
    assert is_fact_accepted(FACT_REJECTED) is False


def test_unknown_status_is_not_accepted():
    assert is_fact_accepted("") is False
    assert is_fact_accepted("garbage") is False


def test_accepted_set_is_exactly_approved_and_edited():
    assert set(ACCEPTED_FACT_STATUSES) == {FACT_APPROVED, FACT_EDITED}
