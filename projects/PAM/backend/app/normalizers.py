"""Normalizers — convert raw JSON from each AI service into IncomingConversation.

This is a stub layer. In Phase 1 the extension sends already-normalized data, so
this module mostly validates and re-shapes. As you tune extension perchatchikov,
you can either:
  (a) normalize on the extension side (TypeScript) and send unified format, or
  (b) send raw responses and normalize here.

The current design assumes the extension does primary normalization. This module
provides helpers to verify / re-normalize if needed.
"""
from __future__ import annotations

from datetime import datetime
from typing import Any

from .schemas import IncomingConversation, IncomingMessage, Source


def normalize_chatgpt(raw: dict[str, Any]) -> IncomingConversation:
    """Normalize a raw ChatGPT conversation JSON.

    Expected shape (simplified) from /backend-api/conversation/{id}:
      {
        "title": "...",
        "create_time": 1234567890,
        "conversation_id": "...",
        "mapping": {"node_id": {"message": {...}, "parent": "...", "children": [...]}}
      }
    """
    conv_id = raw.get("conversation_id") or raw.get("id") or ""
    title = raw.get("title")
    started = _epoch_to_dt(raw.get("create_time"))

    # Walk the message tree linearly via children
    messages: list[IncomingMessage] = []
    mapping = raw.get("mapping", {}) or {}
    # find the root, then BFS via children
    root_id = next(
        (nid for nid, node in mapping.items() if node.get("parent") is None),
        None,
    )
    order = 0
    if root_id:
        stack = [root_id]
        visited: set[str] = set()
        while stack:
            nid = stack.pop(0)
            if nid in visited:
                continue
            visited.add(nid)
            node = mapping.get(nid) or {}
            msg = node.get("message")
            if msg:
                parts = msg.get("content", {}).get("parts") or []
                content = "\n".join(str(p) for p in parts if p).strip()
                if content:
                    role = msg.get("author", {}).get("role", "user")
                    messages.append(
                        IncomingMessage(
                            role=role if role in ("user", "assistant", "system", "tool") else "user",
                            content=content,
                            position=order,
                            sent_at=_epoch_to_dt(msg.get("create_time")),
                        )
                    )
                    order += 1
            for cid in node.get("children", []) or []:
                stack.append(cid)

    return IncomingConversation(
        source="chatgpt",
        external_id=str(conv_id),
        title=title,
        started_at=started,
        messages=messages,
        raw=raw,
    )


def normalize_claude(raw: dict[str, Any]) -> IncomingConversation:
    """Normalize a raw Claude.ai conversation JSON.

    Expected shape (simplified) from /api/organizations/{org}/chat_conversations/{id}:
      {
        "uuid": "...",
        "name": "...",
        "created_at": "ISO8601",
        "chat_messages": [
          {"sender": "human|assistant", "text": "...", "created_at": "ISO8601", ...}
        ]
      }
    """
    conv_id = raw.get("uuid") or raw.get("id") or ""
    title = raw.get("name") or raw.get("title")
    started = _iso_to_dt(raw.get("created_at"))

    messages: list[IncomingMessage] = []
    for pos, m in enumerate(raw.get("chat_messages") or []):
        sender = (m.get("sender") or "").lower()
        role = {"human": "user", "assistant": "assistant"}.get(sender, "user")
        text = m.get("text") or _extract_content_blocks(m.get("content"))
        if not text:
            continue
        messages.append(
            IncomingMessage(
                role=role,
                content=text.strip(),
                position=pos,
                sent_at=_iso_to_dt(m.get("created_at")),
            )
        )

    return IncomingConversation(
        source="claude",
        external_id=str(conv_id),
        title=title,
        started_at=started,
        messages=messages,
        raw=raw,
    )


def normalize_gemini(raw: dict[str, Any]) -> IncomingConversation:
    """Normalize a raw Gemini conversation JSON.

    Gemini's API is the trickiest — it streams chunks. The extension is expected
    to assemble these into a coherent message list before sending.

    Expected shape (extension-assembled):
      {
        "conversation_id": "...",
        "title": "...",
        "messages": [{"role": "user|assistant", "content": "...", "ts": "ISO8601"}]
      }
    """
    conv_id = raw.get("conversation_id") or raw.get("id") or ""
    title = raw.get("title")

    messages: list[IncomingMessage] = []
    for pos, m in enumerate(raw.get("messages") or []):
        role = m.get("role", "user")
        content = (m.get("content") or "").strip()
        if not content:
            continue
        messages.append(
            IncomingMessage(
                role=role if role in ("user", "assistant") else "user",
                content=content,
                position=pos,
                sent_at=_iso_to_dt(m.get("ts")),
            )
        )

    return IncomingConversation(
        source="gemini",
        external_id=str(conv_id),
        title=title,
        started_at=None,
        messages=messages,
        raw=raw,
    )


NORMALIZERS = {
    "chatgpt": normalize_chatgpt,
    "claude": normalize_claude,
    "gemini": normalize_gemini,
}


def normalize(source: Source, raw: dict[str, Any]) -> IncomingConversation:
    return NORMALIZERS[source](raw)


# ---- helpers ----

def _epoch_to_dt(v: Any) -> datetime | None:
    if v is None:
        return None
    try:
        return datetime.utcfromtimestamp(float(v))
    except (TypeError, ValueError):
        return None


def _iso_to_dt(v: Any) -> datetime | None:
    if not v or not isinstance(v, str):
        return None
    try:
        # tolerate trailing Z
        return datetime.fromisoformat(v.replace("Z", "+00:00"))
    except ValueError:
        return None


def _extract_content_blocks(blocks: Any) -> str:
    """Claude content can come as list of blocks ({type:'text', text:'...'})."""
    if not blocks or not isinstance(blocks, list):
        return ""
    parts: list[str] = []
    for b in blocks:
        if isinstance(b, dict) and b.get("type") == "text":
            parts.append(b.get("text") or "")
    return "\n".join(p for p in parts if p)
