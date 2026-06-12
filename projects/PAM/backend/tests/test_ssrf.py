"""Unit tests — SSRF guard rejects non-public / non-http targets.

Uses literal IPs so getaddrinfo resolves locally (no real DNS / network).
"""
import pytest

from app.content import _assert_public_url


def test_rejects_loopback_ip():
    with pytest.raises(ValueError):
        _assert_public_url("http://127.0.0.1/")


def test_rejects_link_local_metadata():
    with pytest.raises(ValueError):
        _assert_public_url("http://169.254.169.254/latest/meta-data/")


def test_rejects_non_http_scheme():
    with pytest.raises(ValueError):
        _assert_public_url("ftp://example.com/")


def test_rejects_missing_host():
    with pytest.raises(ValueError):
        _assert_public_url("http:///nohost")
