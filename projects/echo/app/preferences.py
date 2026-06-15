"""Preference adaptation: nudge role weights from how the user engages (spec §12).

Roles that earn replies or 👍 get more frequent; 👎 makes them rarer. Bounds live in
db.adjust_weight so weights can't run away.
"""
from __future__ import annotations

from . import db

REPLY_BOOST = 1.15
LIKE_BOOST = 1.2
DISLIKE_PENALTY = 0.7


def on_reply(role: str) -> None:
    db.adjust_weight(role, REPLY_BOOST)


def on_reaction(role: str, liked: bool) -> None:
    db.adjust_weight(role, LIKE_BOOST if liked else DISLIKE_PENALTY)
