"""Unit tests for Antigravity Context Tracker hook script."""

from io import StringIO
from pathlib import Path
from unittest.mock import patch
import json

from scripts.context_tracker import (
    WARNING_MESSAGE,
    calculate_percentage,
    calculate_tokens,
    extract_latest_tokens,
    find_transcript_path,
    format_tracker_output,
    main,
    render_progress_bar,
)


def test_calculate_tokens() -> None:
    """Verifies token summation logic with and without prompt caching."""
    # Standard prompt + completion
    assert calculate_tokens(40_000, 5_200) == 45_200

    # With prompt cache read tokens
    assert calculate_tokens(2_000, 300, cache_read_tokens=88_000) == 90_300

    # Zeroes and negative safeguards
    assert calculate_tokens(0, 0, 0) == 0
    assert calculate_tokens(-10, 50) == 50


def test_calculate_percentage() -> None:
    """Verifies percentage calculation against context limits."""
    # 45,200 out of 1,000,000 = 4.52%
    pct = calculate_percentage(45_200, 1_000_000)
    assert round(pct, 2) == 4.52

    # Exactly 70% threshold
    assert calculate_percentage(700_000, 1_000_000) == 70.0

    # 100% capacity
    assert calculate_percentage(1_000_000, 1_000_000) == 100.0

    # Division by zero safeguard
    assert calculate_percentage(500, 0) == 0.0


def test_render_progress_bar_format() -> None:
    """Verifies terminal progress bar formatting and character structure."""
    bar = render_progress_bar(45_200, 1_000_000, bar_width=20)

    assert "[Context Tracker]" in bar
    assert "🧠 Tokens: 45,200 / 1,000,000 (4.5%)" in bar
    assert " | [" in bar
    assert bar.endswith("]")

    # Extract inner bar blocks between [ and ]
    inner_bar = bar.split("| [")[-1].rstrip("]")
    assert len(inner_bar) == 20
    assert "█" in inner_bar
    assert "░" in inner_bar


def test_render_progress_bar_extremes() -> None:
    """Verifies progress bar display at 0% and 100%."""
    zero_bar = render_progress_bar(0, 1_000_000, bar_width=20)
    inner_zero = zero_bar.split("| [")[-1].rstrip("]")
    assert inner_zero == "░" * 20

    full_bar = render_progress_bar(1_000_000, 1_000_000, bar_width=20)
    inner_full = full_bar.split("| [")[-1].rstrip("]")
    assert inner_full == "█" * 20


def test_format_tracker_output_warning_threshold() -> None:
    """Verifies warning triggers strictly when context usage exceeds 70%."""
    # Under 70% (e.g. 50%) -> No warning
    out_under = format_tracker_output(500_000, 1_000_000)
    assert WARNING_MESSAGE not in out_under
    assert "(50.0%)" in out_under

    # Exactly 70% -> No warning (strictly > 70%)
    out_70 = format_tracker_output(700_000, 1_000_000)
    assert WARNING_MESSAGE not in out_70

    # 71% -> Warning included
    out_over = format_tracker_output(710_000, 1_000_000)
    assert WARNING_MESSAGE in out_over
    assert "(71.0%)" in out_over
    assert out_over.endswith(WARNING_MESSAGE)


def test_extract_latest_tokens_from_jsonl(tmp_path: Path) -> None:
    """Verifies extraction of latest tokens from JSONL transcript."""
    transcript_file = tmp_path / "transcript.jsonl"

    lines = [
        json.dumps({"step_index": 1, "source": "USER", "content": "Hello"}),
        json.dumps({
            "step_index": 2,
            "source": "MODEL",
            "input_tokens": 12_000,
            "output_tokens": 400,
            "cache_read_tokens": 0,
        }),
        json.dumps({"step_index": 3, "source": "USER", "content": "Next step"}),
        json.dumps({
            "step_index": 4,
            "source": "MODEL",
            "input_tokens": 15_500,
            "output_tokens": 850,
            "cache_read_tokens": 5_000,
        }),
    ]
    transcript_file.write_text("\n".join(lines), encoding="utf-8")

    in_tok, out_tok, cache_tok = extract_latest_tokens(transcript_file)
    assert in_tok == 15_500
    assert out_tok == 850
    assert cache_tok == 5_000


def test_extract_latest_tokens_nonexistent_or_empty(tmp_path: Path) -> None:
    """Verifies graceful handling of missing or corrupted files."""
    assert extract_latest_tokens(tmp_path / "nonexistent.jsonl") == (0, 0, 0)

    empty_file = tmp_path / "empty.jsonl"
    empty_file.write_text("", encoding="utf-8")
    assert extract_latest_tokens(empty_file) == (0, 0, 0)


def test_find_transcript_path() -> None:
    """Verifies transcript path resolution from various payload structures."""
    # CamelCase
    assert find_transcript_path({"transcriptPath": "C:/logs/t.jsonl"}) == "C:/logs/t.jsonl"

    # snake_case
    assert find_transcript_path({"transcript_path": "C:/logs/t.jsonl"}) == "C:/logs/t.jsonl"

    # Nested
    nested = {"data": {"session": {"transcriptPath": "C:/nested/t.jsonl"}}}
    assert find_transcript_path(nested) == "C:/nested/t.jsonl"


def test_context_tracker_cli_main_success(tmp_path: Path) -> None:
    """Verifies end-to-end execution of main() CLI hook."""
    transcript_file = tmp_path / "transcript.jsonl"
    transcript_file.write_text(
        json.dumps({"input_tokens": 40_000, "output_tokens": 5_200}) + "\n",
        encoding="utf-8",
    )

    hook_payload = json.dumps({"transcriptPath": str(transcript_file)})

    mock_stdin = StringIO(hook_payload)
    mock_stdout = StringIO()
    mock_stderr = StringIO()

    with patch("sys.stdin", mock_stdin), \
         patch("sys.stdout", mock_stdout), \
         patch("sys.stderr", mock_stderr):
        main()

    # stdout must output valid JSON {}
    stdout_val = mock_stdout.getvalue().strip()
    assert json.loads(stdout_val) == {}

    # stderr must contain progress bar
    stderr_val = mock_stderr.getvalue()
    assert "[Context Tracker]" in stderr_val
    assert "45,200 / 1,000,000" in stderr_val


def test_context_tracker_cli_main_warning(tmp_path: Path) -> None:
    """Verifies warning output when context usage exceeds 70% in CLI main()."""
    transcript_file = tmp_path / "transcript.jsonl"
    transcript_file.write_text(
        json.dumps({"input_tokens": 750_000, "output_tokens": 20_000}) + "\n",
        encoding="utf-8",
    )

    hook_payload = json.dumps({"transcriptPath": str(transcript_file)})

    mock_stdin = StringIO(hook_payload)
    mock_stdout = StringIO()
    mock_stderr = StringIO()

    with patch("sys.stdin", mock_stdin), \
         patch("sys.stdout", mock_stdout), \
         patch("sys.stderr", mock_stderr):
        main()

    assert json.loads(mock_stdout.getvalue().strip()) == {}
    stderr_val = mock_stderr.getvalue()
    assert "770,000 / 1,000,000 (77.0%)" in stderr_val
    assert WARNING_MESSAGE in stderr_val


def test_context_tracker_cli_main_empty_input() -> None:
    """Verifies CLI main resilience on empty input."""
    mock_stdin = StringIO("")
    mock_stdout = StringIO()
    mock_stderr = StringIO()

    with patch("sys.stdin", mock_stdin), \
         patch("sys.stdout", mock_stdout), \
         patch("sys.stderr", mock_stderr):
        main()

    assert json.loads(mock_stdout.getvalue().strip()) == {}
