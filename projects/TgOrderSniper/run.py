"""TgOrderSniper Single-File Application Runner.

Entry point that performs pre-flight verification, logs active configuration summary,
detects available session files with auto-fallback, and starts all components.
"""

from __future__ import annotations

import logging
import os
from pathlib import Path
import sys
from typing import Any

# Ensure project root directory is in sys.path
PROJECT_ROOT = Path(__file__).resolve().parent
if str(PROJECT_ROOT) not in sys.path:
    sys.path.insert(0, str(PROJECT_ROOT))

# Force UTF-8 stream output on Windows console if supported
if sys.platform == "win32":
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
    if hasattr(sys.stderr, "reconfigure"):
        try:
            sys.stderr.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass

from backend.core.config import Settings, get_settings  # noqa: E402
from backend.main import main as backend_main, setup_logging  # noqa: E402

logger = logging.getLogger("TgOrderSniper.Run")


def mask_credential(value: str | int | None, prefix_len: int = 4, suffix_len: int = 3) -> str:
    """Masks sensitive credentials for secure log/banner display.

    Args:
        value: Credential string, int, or None.
        prefix_len: Number of initial characters to leave visible.
        suffix_len: Number of trailing characters to leave visible.

    Returns:
        Masked string representation.
    """
    if value is None or value == "" or value == 0:
        return "NOT CONFIGURED"

    str_val = str(value).strip()
    if len(str_val) <= prefix_len + suffix_len:
        return "Configured (***)"
    return f"{str_val[:prefix_len]}...{str_val[-suffix_len:]}"


def detect_session_status(
    project_dir: Path, configured_session_name: str
) -> tuple[str, str, bool]:
    """Detects available SQLite .session files and evaluates fallback logic.

    If the configured session file does not exist, but 'custom_session.session'
    is present, auto-fallback is triggered.

    Args:
        project_dir: Directory to inspect for .session files.
        configured_session_name: Name of configured session (without extension).

    Returns:
        tuple[str, str, bool]: (effective_session_name, status_message, fell_back)
    """
    configured_file = project_dir / f"{configured_session_name}.session"
    custom_file = project_dir / "custom_session.session"

    if configured_file.is_file():
        return (
            configured_session_name,
            f"Using configured session '{configured_file.name}'",
            False,
        )

    if custom_file.is_file():
        return (
            "custom_session",
            f"Configured '{configured_file.name}' not found. "
            f"Auto-fallback to '{custom_file.name}'",
            True,
        )

    available_sessions = [f.name for f in project_dir.glob("*.session") if f.is_file()]
    if available_sessions:
        return (
            configured_session_name,
            f"Configured '{configured_file.name}' not found. "
            f"Available session file(s): {', '.join(available_sessions)}",
            False,
        )

    return (
        configured_session_name,
        f"Session '{configured_file.name}' not found. "
        "A new session will be created upon first MTProto login.",
        False,
    )


def check_preflight(project_dir: Path | None = None) -> tuple[Settings, dict[str, Any]]:
    """Performs pre-flight checks and returns active configuration summary.

    Args:
        project_dir: Optional root directory path. Defaults to PROJECT_ROOT.

    Returns:
        tuple[Settings, dict[str, Any]]: Loaded Settings and configuration summary dictionary.
    """
    base_dir = project_dir or PROJECT_ROOT
    env_file = base_dir / ".env"
    env_exists = env_file.is_file()

    # Load initial settings
    if env_exists:
        initial_settings = Settings(_env_file=str(env_file))
    else:
        initial_settings = Settings(_env_file=None)

    # Session detection & fallback
    configured_session = initial_settings.TG_SESSION_NAME
    effective_session, session_message, fell_back = detect_session_status(
        base_dir, configured_session
    )

    if fell_back:
        os.environ["TG_SESSION_NAME"] = effective_session
        get_settings.cache_clear()
        settings = Settings(_env_file=str(env_file) if env_exists else None)
    else:
        settings = initial_settings

    # Format summary metrics
    channels_count = (
        len(settings.CHANNELS_TO_WATCH)
        if isinstance(settings.CHANNELS_TO_WATCH, list)
        else (1 if settings.CHANNELS_TO_WATCH else 0)
    )
    queries_count = (
        len(settings.SEARCH_QUERIES)
        if isinstance(settings.SEARCH_QUERIES, list)
        else (1 if settings.SEARCH_QUERIES else 0)
    )

    summary: dict[str, Any] = {
        "env_path": str(env_file),
        "env_exists": env_exists,
        "tg_api_id_configured": bool(settings.TG_API_ID),
        "tg_api_id": settings.TG_API_ID,
        "tg_api_hash_configured": bool(settings.TG_API_HASH),
        "tg_api_hash_masked": mask_credential(settings.TG_API_HASH),
        "bot_token_configured": bool(settings.TG_BOT_TOKEN),
        "bot_token_masked": mask_credential(settings.TG_BOT_TOKEN),
        "target_chat_id": settings.TARGET_CHAT_ID,
        "channels_count": channels_count,
        "groq_configured": bool(settings.GROQ_API_KEY),
        "groq_model": settings.GROQ_MODEL,
        "search_interval_minutes": settings.SEARCH_INTERVAL_MINUTES,
        "search_queries_count": queries_count,
        "db_path": settings.DB_PATH,
        "web_scrapers_enabled": settings.ENABLE_WEB_SCRAPERS,
        "web_scrape_interval_minutes": settings.WEB_SCRAPE_INTERVAL_MINUTES,
        "effective_session": effective_session,
        "session_message": session_message,
        "session_fell_back": fell_back,
    }

    return settings, summary


def format_preflight_banner(summary: dict[str, Any]) -> str:
    """Renders visual pre-flight banner string from summary dictionary.

    Args:
        summary: Configuration summary dictionary from check_preflight.

    Returns:
        str: Formatted ASCII banner text.
    """
    env_status = (
        f"FOUND ({summary['env_path']})"
        if summary["env_exists"]
        else f"MISSING ({summary['env_path']}) -> Copy .env.example to .env"
    )

    api_id_status = (
        f"Configured ({summary['tg_api_id']})"
        if summary["tg_api_id_configured"]
        else "NOT CONFIGURED (Required for MTProto listener)"
    )

    bot_status = (
        f"Configured ({summary['bot_token_masked']}) -> Target: {summary['target_chat_id']}"
        if summary["bot_token_configured"]
        else "NOT CONFIGURED (Dispatcher running in dry-run mode)"
    )

    groq_status = (
        f"Enabled (Model: {summary['groq_model']})"
        if summary["groq_configured"]
        else "Disabled (No API key; rule-based heuristics only)"
    )

    search_status = (
        f"Active ({summary['search_interval_minutes']}m interval, "
        f"{summary['search_queries_count']} queries)"
    )

    channels_status = (
        f"{summary['channels_count']} channel(s) monitored"
        if summary["channels_count"] > 0
        else "All available dialogs / chats"
    )

    scrapers_status = (
        f"Enabled (Habr, Kwork every {summary.get('web_scrape_interval_minutes', 5)}m)"
        if summary.get("web_scrapers_enabled")
        else "Disabled"
    )

    db_status = f"SQLite ({summary.get('db_path', 'data/sniper.db')})"

    banner_lines = [
        "=" * 70,
        "                TgOrderSniper Service Pre-Flight Check                ",
        "=" * 70,
        "[Configuration Status]",
        f"  - Environment File:    {env_status}",
        f"  - Telegram MTProto:    {api_id_status}",
        f"  - Active Session:      {summary['session_message']}",
        f"  - Telegram Bot API:    {bot_status}",
        f"  - Groq AI Classifier:  {groq_status}",
        f"  - Channel Monitor:     {channels_status}",
        f"  - Global Search:       {search_status}",
        f"  - Persistent DB:       {db_status}",
        f"  - Web Scrapers:        {scrapers_status}",
        "=" * 70,
    ]
    return "\n".join(banner_lines)


def run(project_dir: Path | None = None, dry_run: bool = False) -> int:
    """Runs pre-flight verification and starts the TgOrderSniper service.

    Args:
        project_dir: Optional root directory path. Defaults to PROJECT_ROOT.
        dry_run: If True, performs pre-flight verification without starting the service loop.

    Returns:
        int: Exit status code (0 for success/clean shutdown, 1 for fatal error).
    """
    setup_logging()
    resolved_dir = project_dir or PROJECT_ROOT
    is_dry = dry_run or ("--check" in sys.argv) or ("--dry-run" in sys.argv)

    try:
        settings, summary = check_preflight(resolved_dir)
        banner = format_preflight_banner(summary)
        print(banner, flush=True)

        if not summary["env_exists"]:
            logger.warning(
                "[Preflight] .env file not found. Running with default / environment variables."
            )

        if summary["session_fell_back"]:
            logger.warning(f"[Preflight] Session Fallback: {summary['session_message']}")

        if is_dry:
            logger.info("[Preflight] Dry-run check completed successfully.")
            return 0

        logger.info(
            f"[Preflight] Starting TgOrderSniper engine with session '{settings.TG_SESSION_NAME}'..."
        )
        backend_main(settings=settings)
        return 0

    except KeyboardInterrupt:
        logger.info("[Launcher] Stopped by user (KeyboardInterrupt). Clean shutdown.")
        return 0
    except SystemExit as exc:
        code = exc.code if isinstance(exc.code, int) else 0
        return code
    except Exception as exc:
        logger.error(f"[Launcher] Fatal startup error: {exc}", exc_info=True)
        return 1


if __name__ == "__main__":
    sys.exit(run())
