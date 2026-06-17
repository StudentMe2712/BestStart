"""Tests for the pure formatting / argument-parsing helpers (no network, no Telegram)."""
from parsertg import bot, parser
from parsertg.db import Stats
from parsertg.parser import ChannelResult


def test_message_link_public():
    assert parser.message_link("@durov", 123, 7) == "https://t.me/durov/7"
    assert parser.message_link("durov", 123, 7) == "https://t.me/durov/7"


def test_message_link_private_and_missing():
    assert parser.message_link(None, 555, 7) == "https://t.me/c/555/7"
    assert parser.message_link(None, None, 7) == ""


def test_parse_n():
    assert bot._parse_n([], 20) == 20
    assert bot._parse_n(["5"], 20) == 5
    assert bot._parse_n(["0"], 20) is None
    assert bot._parse_n(["-3"], 20) is None
    assert bot._parse_n(["abc"], 20) is None


def test_format_status_with_and_without_last():
    assert "Последнее: —" in bot.format_status(Stats(3, 0, None))
    populated = bot.format_status(
        Stats(2, 5, {"channel": "Chan", "message_id": 9, "date": "2024-01-01T10:00:00+00:00"})
    )
    assert "Каналов: 2" in populated
    assert "#9" in populated


def test_format_feed_empty_and_rows():
    assert "Пока нет" in bot.format_feed([])
    rows = [{"channel": "Chan", "date": "2024-01-01T10:00:00+00:00", "text": "hi", "link": "https://t.me/a/1"}]
    out = bot.format_feed(rows)
    assert "Chan" in out
    assert "t.me/a/1" in out


def test_format_feed_escapes_html():
    rows = [{"channel": "C", "date": "bad-date", "text": "<b>x</b> & y", "link": ""}]
    out = bot.format_feed(rows)
    assert "<b>x</b>" not in out  # the message body must be escaped
    assert "&amp; y" in out


def test_parse_summary():
    out = bot.format_parse_summary([ChannelResult("A", 3), ChannelResult("B", 0, "не найден")])
    assert "Новых сообщений: 3" in out
    assert "не найден" in out
    assert bot.format_parse_summary([]).startswith("Нет настроенных каналов")
