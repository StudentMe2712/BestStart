"""Unit tests for TgOrderSniper single-file launcher (run.py)."""

from pathlib import Path
from unittest.mock import MagicMock, patch

import run
from run import (
    check_preflight,
    detect_session_status,
    format_preflight_banner,
    mask_credential,
)


def test_mask_credential() -> None:
    """Tests credential masking for various input lengths and empty values."""
    assert mask_credential(None) == "NOT CONFIGURED"
    assert mask_credential("") == "NOT CONFIGURED"
    assert mask_credential(0) == "NOT CONFIGURED"
    assert mask_credential("short") == "Configured (***)"
    assert mask_credential("1234567") == "Configured (***)"
    assert mask_credential("12345678") == "1234...678"
    assert mask_credential("gsk_super_secret_groq_api_key") == "gsk_...key"


def test_detect_session_configured_exists(tmp_path: Path) -> None:
    """Tests session detection when the configured session file already exists."""
    configured_file = tmp_path / "my_session.session"
    configured_file.write_text("dummy", encoding="utf-8")

    effective_name, msg, fell_back = detect_session_status(tmp_path, "my_session")
    assert effective_name == "my_session"
    assert not fell_back
    assert "Using configured session 'my_session.session'" in msg


def test_detect_session_auto_fallback(tmp_path: Path) -> None:
    """Tests auto-fallback to custom_session.session when configured session is missing."""
    custom_file = tmp_path / "custom_session.session"
    custom_file.write_text("dummy", encoding="utf-8")

    effective_name, msg, fell_back = detect_session_status(tmp_path, "missing_session")
    assert effective_name == "custom_session"
    assert fell_back
    assert "Auto-fallback to 'custom_session.session'" in msg


def test_detect_session_other_available(tmp_path: Path) -> None:
    """Tests session detection when other .session files exist but custom_session does not."""
    other_file = tmp_path / "alternate.session"
    other_file.write_text("dummy", encoding="utf-8")

    effective_name, msg, fell_back = detect_session_status(tmp_path, "target_session")
    assert effective_name == "target_session"
    assert not fell_back
    assert "Available session file(s): alternate.session" in msg


def test_detect_session_none_exist(tmp_path: Path) -> None:
    """Tests session detection when no .session files exist at all."""
    effective_name, msg, fell_back = detect_session_status(tmp_path, "new_session")
    assert effective_name == "new_session"
    assert not fell_back
    assert "will be created upon first MTProto login" in msg


def test_check_preflight_missing_env(tmp_path: Path) -> None:
    """Tests pre-flight checks when .env file is missing."""
    settings, summary = check_preflight(tmp_path)
    assert not summary["env_exists"]
    assert str(tmp_path / ".env") in summary["env_path"]
    assert settings is not None


def test_check_preflight_with_env(tmp_path: Path) -> None:
    """Tests pre-flight checks when .env file is present and populated."""
    env_content = (
        "TG_API_ID=998877\n"
        "TG_API_HASH=abcdef0123456789abcdef0123456789\n"
        "TG_SESSION_NAME=test_env_session\n"
        "TG_BOT_TOKEN=123456:ABC-DEF1234ghIkl-zyx567\n"
        "TARGET_CHAT_ID=-100999888777\n"
        "GROQ_API_KEY=gsk_test_api_key_12345\n"
    )
    (tmp_path / ".env").write_text(env_content, encoding="utf-8")

    settings, summary = check_preflight(tmp_path)
    assert summary["env_exists"]
    assert summary["tg_api_id"] == 998877
    assert summary["tg_api_id_configured"]
    assert summary["tg_api_hash_configured"]
    assert summary["bot_token_configured"]
    assert summary["target_chat_id"] == -100999888777
    assert summary["groq_configured"]
    assert settings.TG_API_ID == 998877


def test_check_preflight_auto_fallback_reloads_settings(tmp_path: Path) -> None:
    """Tests that auto-fallback updates settings and environment variable."""
    env_content = "TG_SESSION_NAME=OrderParser\n"
    (tmp_path / ".env").write_text(env_content, encoding="utf-8")
    (tmp_path / "custom_session.session").write_text("session_data", encoding="utf-8")

    settings, summary = check_preflight(tmp_path)
    assert summary["session_fell_back"]
    assert summary["effective_session"] == "custom_session"
    assert settings.TG_SESSION_NAME == "custom_session"


def test_format_preflight_banner(tmp_path: Path) -> None:
    """Tests pre-flight visual banner formatting output."""
    _, summary = check_preflight(tmp_path)
    banner = format_preflight_banner(summary)

    assert "TgOrderSniper Service Pre-Flight Check" in banner
    assert "Environment File:" in banner
    assert "Telegram MTProto:" in banner
    assert "Active Session:" in banner
    assert "Telegram Bot API:" in banner
    assert "Groq AI Classifier:" in banner


def test_run_success_invokes_main(tmp_path: Path) -> None:
    """Tests that run() successfully performs preflight and executes backend main."""
    mock_main = MagicMock()
    with patch("run.backend_main", mock_main):
        exit_code = run.run(project_dir=tmp_path, dry_run=False)

    assert exit_code == 0
    mock_main.assert_called_once()
    assert "settings" in mock_main.call_args.kwargs


def test_run_dry_run_skips_main(tmp_path: Path) -> None:
    """Tests that run(dry_run=True) performs checks without executing backend main."""
    mock_main = MagicMock()
    with patch("run.backend_main", mock_main):
        exit_code = run.run(project_dir=tmp_path, dry_run=True)

    assert exit_code == 0
    mock_main.assert_not_called()


def test_run_keyboard_interrupt_handled(tmp_path: Path) -> None:
    """Tests graceful exit on KeyboardInterrupt during startup."""
    mock_main = MagicMock(side_effect=KeyboardInterrupt)
    with patch("run.backend_main", mock_main):
        exit_code = run.run(project_dir=tmp_path, dry_run=False)

    assert exit_code == 0


def test_run_system_exit_handled(tmp_path: Path) -> None:
    """Tests clean exit code preservation when SystemExit is raised."""
    mock_main = MagicMock(side_effect=SystemExit(42))
    with patch("run.backend_main", mock_main):
        exit_code = run.run(project_dir=tmp_path, dry_run=False)

    assert exit_code == 42


def test_run_unhandled_exception_returns_code_1(tmp_path: Path) -> None:
    """Tests fatal error handling and exit code 1 on unhandled exception."""
    mock_main = MagicMock(side_effect=RuntimeError("Fatal startup failure"))
    with patch("run.backend_main", mock_main):
        exit_code = run.run(project_dir=tmp_path, dry_run=False)

    assert exit_code == 1
