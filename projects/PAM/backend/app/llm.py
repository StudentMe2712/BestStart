"""Phase 4 — chat LLM provider abstraction with token streaming.

Provider-agnostic: `stream_chat(messages)` yields text tokens from either
Groq (cloud, free tier, OpenAI-compatible API) or a local Ollama chat model,
selected by `settings.LLM_PROVIDER`. Embeddings stay local (see indexing.py);
this is only for the chat/extraction LLM.
"""
from __future__ import annotations

import json
import logging
import re
from collections.abc import AsyncIterator

import httpx

from .config import settings

log = logging.getLogger(__name__)

GROQ_URL = "https://api.groq.com/openai/v1/chat/completions"
OPENROUTER_URL = "https://openrouter.ai/api/v1/chat/completions"
# OpenRouter использует эти заголовки для рейтинга приложений (необязательны).
OPENROUTER_HEADERS = {"HTTP-Referer": "http://localhost:3000", "X-Title": "PAM"}

# Гибрид: «тяжёлые» запросы → мощная (но медленная) модель OpenRouter,
# остальное → быстрый Groq. Триггеры сложности (код/разбор/длина).
_HARD_RE = re.compile(
    r"(```|код|скрипт|функци|класс|алгоритм|debug|деб[аа]г|ошибк|error|traceback|"
    r"exception|стек|оптимиз|рефактор|регуляр|sql|запрос|конфиг|настрой|пошагов|"
    r"подробно|разбер|сравни|почему|как работает|архитектур|спроект|реализу|"
    r"напиши|сгенерир|посчитай|докаж|выведи формул|объясни подроб)",
    re.IGNORECASE,
)


def route_provider(text: str) -> str:
    """Гибрид-маршрутизация по тексту запроса: 'groq' (быстро) или 'openrouter' (мощно)."""
    if not settings.OPENROUTER_API_KEY:
        return "groq"
    if len(text) > 280 or "```" in text or _HARD_RE.search(text):
        return "openrouter"
    return "groq"


def model_for(provider: str) -> str:
    """Имя модели для отображения/логов по имени провайдера."""
    p = provider.lower()
    if p == "openrouter":
        return settings.OPENROUTER_MODEL
    if p == "ollama":
        return settings.OLLAMA_CHAT_MODEL
    return settings.GROQ_MODEL


async def stream_chat(
    messages: list[dict], provider: str | None = None
) -> AsyncIterator[str]:
    """Stream response tokens. `provider` overrides settings (used by the hybrid router)."""
    p = (provider or settings.LLM_PROVIDER).lower()
    if p == "hybrid":  # на всякий случай: hybrid разрешается выше по стеку
        p = "groq"
    if p == "ollama":
        async for tok in _stream_ollama(messages):
            yield tok
    elif p == "openrouter":
        async for tok in _stream_openai_compatible(
            messages, OPENROUTER_URL, settings.OPENROUTER_API_KEY,
            settings.OPENROUTER_MODEL, "OPENROUTER_API_KEY", OPENROUTER_HEADERS,
        ):
            yield tok
    else:
        async for tok in _stream_groq(messages):
            yield tok


# --- OpenAI-compatible providers (Groq, OpenRouter) share one implementation;
#     reasoning-models keep chain-of-thought in delta.reasoning, so reading
#     delta.content keeps the streamed answer clean. ---
async def _stream_openai_compatible(
    messages: list[dict],
    url: str,
    key: str,
    model: str,
    key_name: str,
    extra_headers: dict | None = None,
) -> AsyncIterator[str]:
    if not key:
        raise RuntimeError(
            f"{key_name} не задан. Добавь ключ в backend/.env или переключи LLM_PROVIDER."
        )
    payload = {"model": model, "messages": messages, "stream": True}
    headers = {"Authorization": f"Bearer {key}", **(extra_headers or {})}
    async with httpx.AsyncClient(timeout=120.0) as client:
        async with client.stream("POST", url, headers=headers, json=payload) as resp:
            resp.raise_for_status()
            async for line in resp.aiter_lines():
                if not line or not line.startswith("data: "):
                    continue
                data = line[6:]
                if data == "[DONE]":
                    break
                try:
                    delta = json.loads(data)["choices"][0]["delta"].get("content")
                except (KeyError, IndexError, json.JSONDecodeError):
                    continue
                if delta:
                    yield delta


async def _complete_openai_compatible(
    messages: list[dict],
    url: str,
    key: str,
    model: str,
    key_name: str,
    json_mode: bool,
    extra_headers: dict | None = None,
) -> str:
    if not key:
        raise RuntimeError(f"{key_name} не задан (или переключи LLM_PROVIDER).")
    payload: dict = {"model": model, "messages": messages, "stream": False}
    if json_mode:
        payload["response_format"] = {"type": "json_object"}
    headers = {"Authorization": f"Bearer {key}", **(extra_headers or {})}
    async with httpx.AsyncClient(timeout=120.0) as client:
        r = await client.post(url, headers=headers, json=payload)
        r.raise_for_status()
        return r.json()["choices"][0]["message"]["content"]


async def _stream_groq(messages: list[dict]) -> AsyncIterator[str]:
    async for tok in _stream_openai_compatible(
        messages, GROQ_URL, settings.GROQ_API_KEY, settings.GROQ_MODEL, "GROQ_API_KEY"
    ):
        yield tok


async def complete(messages: list[dict], json_mode: bool = False) -> str:
    """Non-streaming completion (used for fact extraction / courses). Returns full text."""
    provider = settings.LLM_PROVIDER.lower()
    if provider == "ollama":
        return await _complete_ollama(messages, json_mode)
    if provider == "openrouter":
        return await _complete_openai_compatible(
            messages, OPENROUTER_URL, settings.OPENROUTER_API_KEY,
            settings.OPENROUTER_MODEL, "OPENROUTER_API_KEY", json_mode, OPENROUTER_HEADERS,
        )
    return await _complete_openai_compatible(
        messages, GROQ_URL, settings.GROQ_API_KEY, settings.GROQ_MODEL,
        "GROQ_API_KEY", json_mode,
    )


async def _complete_ollama(messages: list[dict], json_mode: bool) -> str:
    payload: dict = {"model": settings.OLLAMA_CHAT_MODEL, "messages": messages, "stream": False}
    if json_mode:
        payload["format"] = "json"
    async with httpx.AsyncClient(timeout=300.0) as client:
        r = await client.post(f"{settings.OLLAMA_URL}/api/chat", json=payload)
        r.raise_for_status()
        return r.json()["message"]["content"]


async def _stream_ollama(messages: list[dict]) -> AsyncIterator[str]:
    payload = {"model": settings.OLLAMA_CHAT_MODEL, "messages": messages, "stream": True}
    async with httpx.AsyncClient(timeout=300.0) as client:
        async with client.stream(
            "POST", f"{settings.OLLAMA_URL}/api/chat", json=payload
        ) as resp:
            resp.raise_for_status()
            async for line in resp.aiter_lines():
                if not line:
                    continue
                try:
                    obj = json.loads(line)
                except json.JSONDecodeError:
                    continue
                tok = (obj.get("message") or {}).get("content")
                if tok:
                    yield tok
                if obj.get("done"):
                    break
