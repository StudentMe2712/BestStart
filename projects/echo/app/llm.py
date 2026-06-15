"""LLM access over the OpenAI-compatible chat-completions API.

Both Groq and OpenRouter speak this format, so one small client serves both. The
generator calls these providers in order and falls back to templates when both fail.
"""
from __future__ import annotations

import logging

import httpx

logger = logging.getLogger(__name__)

REQUEST_TIMEOUT = 20.0
MAX_TOKENS = 200


async def chat(
    *,
    base_url: str,
    api_key: str,
    model: str,
    system: str,
    user: str,
    temperature: float = 0.9,
    title: str | None = None,
) -> str | None:
    """Return the model's reply text, or None on any failure (so the caller can fall back)."""
    if not api_key:
        return None

    url = f"{base_url.rstrip('/')}/chat/completions"
    headers = {
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json",
    }
    if title:  # OpenRouter uses this for attribution; harmless elsewhere.
        headers["X-Title"] = title

    payload = {
        "model": model,
        "messages": [
            {"role": "system", "content": system},
            {"role": "user", "content": user},
        ],
        "temperature": temperature,
        "max_tokens": MAX_TOKENS,
    }

    try:
        async with httpx.AsyncClient(timeout=REQUEST_TIMEOUT) as client:
            resp = await client.post(url, headers=headers, json=payload)
        if resp.status_code != 200:
            logger.warning("LLM %s returned %s: %s", base_url, resp.status_code, resp.text[:200])
            return None
        data = resp.json()
        content = data["choices"][0]["message"]["content"]
        text = (content or "").strip()
        return text or None
    except (httpx.HTTPError, KeyError, IndexError, ValueError) as exc:
        logger.warning("LLM %s call failed: %s", base_url, exc)
        return None
