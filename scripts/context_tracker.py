#!/usr/bin/env python3
"""Context Tracker hook script for Antigravity.

Tracks token usage and context window consumption across conversation steps.
Receives hook payload on stdin, reads latest tokens from transcript.jsonl,
renders a terminal progress bar to stderr, and writes valid JSON {} to stdout.
"""

from pathlib import Path
from typing import Any
import glob
import json
import logging
import os
import sys

logger = logging.getLogger(__name__)

DEFAULT_CONTEXT_WINDOW: int = 1_000_000
DEFAULT_BAR_WIDTH: int = 20
WARNING_THRESHOLD_PERCENT: float = 70.0
WARNING_MESSAGE: str = "⚠️ Внимание: Контекст заполнен на 70%+, рекомендуется начать новый сеанс!"


def calculate_tokens(
    input_tokens: int,
    output_tokens: int,
    cache_read_tokens: int = 0,
) -> int:
    """Calculates total token count from token metrics.

    Args:
        input_tokens: Number of prompt/input tokens.
        output_tokens: Number of completion/output tokens.
        cache_read_tokens: Number of cache-read prompt tokens (if applicable).

    Returns:
        int: Total token consumption.
    """
    return max(0, input_tokens) + max(0, output_tokens) + max(0, cache_read_tokens)


def calculate_percentage(
    total_tokens: int,
    max_tokens: int = DEFAULT_CONTEXT_WINDOW,
) -> float:
    """Calculates context window consumption percentage.

    Args:
        total_tokens: Total token count used.
        max_tokens: Maximum context window size.

    Returns:
        float: Percentage of context window used (0.0 to 100.0+).
    """
    if max_tokens <= 0:
        return 0.0
    return (total_tokens / max_tokens) * 100.0


def render_progress_bar(
    total_tokens: int,
    max_tokens: int = DEFAULT_CONTEXT_WINDOW,
    bar_width: int = DEFAULT_BAR_WIDTH,
) -> str:
    """Renders terminal progress bar string for context usage.

    Args:
        total_tokens: Total tokens used in context.
        max_tokens: Total context limit (e.g. 1,000,000).
        bar_width: Width of the progress bar in blocks.

    Returns:
        str: Formatted progress bar string.
    """
    pct = calculate_percentage(total_tokens, max_tokens)
    if max_tokens > 0:
        filled = min(bar_width, max(0, round((total_tokens / max_tokens) * bar_width)))
    else:
        filled = 0
    empty = bar_width - filled
    bar = "█" * filled + "░" * empty

    return f"[Context Tracker] 🧠 Tokens: {total_tokens:,} / {max_tokens:,} ({pct:.1f}%) | [{bar}]"


def format_tracker_output(
    total_tokens: int,
    max_tokens: int = DEFAULT_CONTEXT_WINDOW,
    bar_width: int = DEFAULT_BAR_WIDTH,
) -> str:
    """Generates complete terminal output including optional >70% warning.

    Args:
        total_tokens: Total tokens used.
        max_tokens: Context window limit.
        bar_width: Width of progress bar.

    Returns:
        str: Formatted multi-line or single-line report string.
    """
    bar_str = render_progress_bar(total_tokens, max_tokens, bar_width)
    pct = calculate_percentage(total_tokens, max_tokens)
    if pct > WARNING_THRESHOLD_PERCENT:
        return f"{bar_str}\n{WARNING_MESSAGE}"
    return bar_str


def extract_latest_tokens(transcript_path: str | Path) -> tuple[int, int, int]:
    """Reads latest input, output, and cache token metrics from transcript JSONL.

    Args:
        transcript_path: Path to transcript.jsonl file.

    Returns:
        tuple[int, int, int]: (input_tokens, output_tokens, cache_read_tokens)
    """
    path = Path(transcript_path)
    if not path.is_file():
        return 0, 0, 0

    latest_entry: dict[str, Any] | None = None
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            for line in f:
                line_str = line.strip()
                if not line_str:
                    continue
                try:
                    entry = json.loads(line_str)
                    if isinstance(entry, dict) and ("input_tokens" in entry or "output_tokens" in entry):
                        latest_entry = entry
                except json.JSONDecodeError:
                    continue
    except OSError as exc:
        logger.debug("[ContextTracker] Could not read transcript file: %s", exc)
        return 0, 0, 0

    if not latest_entry:
        return 0, 0, 0

    input_tokens = int(latest_entry.get("input_tokens", 0) or 0)
    output_tokens = int(latest_entry.get("output_tokens", 0) or 0)
    cache_tokens = int(latest_entry.get("cache_read_tokens", 0) or 0)

    return input_tokens, output_tokens, cache_tokens


def find_transcript_path(payload: dict[str, Any]) -> str | None:
    """Extracts transcriptPath from hook payload or discovers recent transcript.

    Args:
        payload: Hook JSON dictionary from stdin.

    Returns:
        str | None: Resolved path to transcript.jsonl, or None if not found.
    """
    # 1. Direct key lookups
    for key in ("transcriptPath", "transcript_path", "transcript"):
        val = payload.get(key)
        if isinstance(val, str) and val.strip():
            return val.strip()

    # 2. Nested dictionary lookup
    def _search_dict(d: dict[str, Any]) -> str | None:
        for k, v in d.items():
            if k.lower() in ("transcriptpath", "transcript_path") and isinstance(v, str):
                return v
            if isinstance(v, dict):
                res = _search_dict(v)
                if res:
                    return res
        return None

    nested_path = _search_dict(payload)
    if nested_path:
        return nested_path

    # 3. Fallback: discover most recent transcript in local AppData
    appdata_patterns = [
        os.path.expanduser("~/.gemini/antigravity-cli/brain/*/.system_generated/logs/transcript*.jsonl"),
        os.path.expanduser("~/.gemini/antigravity-cli/brain/*/.system_generated/transcript*.jsonl"),
    ]
    all_transcripts: list[str] = []
    for pattern in appdata_patterns:
        all_transcripts.extend(glob.glob(pattern))

    if all_transcripts:
        # Sort by mtime descending
        all_transcripts.sort(key=lambda p: os.path.getmtime(p), reverse=True)
        return all_transcripts[0]

    return None


def main() -> None:
    """Main CLI entrypoint for Antigravity context tracker hook."""
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            try:
                stream.reconfigure(encoding="utf-8")
            except Exception:
                pass

    try:
        raw_input = sys.stdin.read().strip()
        payload: dict[str, Any] = json.loads(raw_input) if raw_input else {}
    except Exception:
        payload = {}

    try:
        transcript_path = find_transcript_path(payload)
        if transcript_path and os.path.exists(transcript_path):
            in_tok, out_tok, cache_tok = extract_latest_tokens(transcript_path)
            total = calculate_tokens(in_tok, out_tok, cache_tok)
            if total > 0:
                report = format_tracker_output(total)
                try:
                    sys.stderr.write(report + "\n")
                    sys.stderr.flush()
                except UnicodeEncodeError:
                    sys.stderr.buffer.write((report + "\n").encode("utf-8"))
                    sys.stderr.buffer.flush()
    except Exception as exc:
        logger.debug("[ContextTracker] Unhandled error during context tracking: %s", exc)

    # Always return valid JSON on stdout
    sys.stdout.write("{}\n")
    sys.stdout.flush()


if __name__ == "__main__":
    main()
