"""Unit tests — learning progress derivation (percent + study status)."""
from app.models import (
    COURSE_COMPLETED,
    COURSE_IN_PROGRESS,
    COURSE_NOT_STARTED,
    course_percent,
    course_study_status,
)


def test_percent_basic():
    assert course_percent(0, 0) == 0
    assert course_percent(0, 10) == 0
    assert course_percent(5, 10) == 50
    assert course_percent(10, 10) == 100


def test_percent_never_exceeds_100():
    # completed может превысить total, если курс ужали при пересоздании
    assert course_percent(12, 10) == 100


def test_percent_no_total_is_zero():
    assert course_percent(3, 0) == 0


def test_status_not_started():
    assert course_study_status(0, 10) == COURSE_NOT_STARTED
    assert course_study_status(0, 0) == COURSE_NOT_STARTED


def test_status_in_progress_by_lessons():
    assert course_study_status(1, 10) == COURSE_IN_PROGRESS


def test_status_in_progress_by_quiz_only():
    assert course_study_status(0, 10, quiz_taken=True) == COURSE_IN_PROGRESS


def test_status_completed_when_all_lessons_done():
    assert course_study_status(10, 10) == COURSE_COMPLETED
    assert course_study_status(11, 10) == COURSE_COMPLETED


def test_status_zero_total_not_completed_even_if_quiz():
    # без уроков курс не «completed»; квиз делает его in_progress
    assert course_study_status(0, 0, quiz_taken=True) == COURSE_IN_PROGRESS
