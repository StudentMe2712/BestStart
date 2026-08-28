---
name: groq-resilient-parser
description: Fault-tolerant integration patterns for Groq Cloud API, handling 429 rate limits and extracting JSON from noisy LLM outputs.
---

# Groq Resilient Parser & API Gateway

This skill details patterns for communicating with Groq Cloud LLM endpoints with high availability and deterministic parsing.

## 1. Rate Limit & Server Error Handling (HTTP 429 / 5xx)

- **Exponential Backoff:** When receiving HTTP 429 or 503, inspect the `Retry-After` header. If absent, apply exponential backoff: `delay = initial_delay * (2 ** attempt)`.
- **Inter-Request Jitter:** Introduce small sleep intervals (e.g. 0.3s) between batch requests to stay comfortably within Groq RPM/TPM ceilings.
- **Fail-Safe Fallback:** If max retries are exceeded or no API key is provided, never crash the pipeline. Return a fallback unclassified record marked for review.

## 2. Robust JSON Extraction Logic

LLMs can return JSON wrapped in Markdown fences, preceded by greetings, or containing trailing comments.
The extraction pipeline must:
1. Attempt direct `json.loads(text)`.
2. Search for markdown code blocks: `r"```(?:json)?\s*([\s\S]*?)\s*```"`.
3. Locate outermost balanced braces `{...}` or brackets `[...]`.
4. Apply heuristic repairs (e.g. unescaped quotes).

## 3. "Ruthless Analyst" System Prompt

The system prompt enforces strict business classification:
- **`is_trend: true`** only for verifiable business opportunities, software tools, micro-SaaS, or clear monetization mechanics.
- **`ai_score` (1-10)** and **`scam_probability` (0-100)** must be strictly validated and clamped.
- Output MUST be structured JSON only.
