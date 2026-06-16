"""humanity_lab — ECHO HUMANITY V3 quality lab.

Not "is the role distinct" (that was personality_lab) but "does this feel like a person?".
For every generated message it records four axes:

  Human Score      — could a real person have written this to an acquaintance?  (10 = best)
  Bot Score        — how much it smells like a bot/coach/tracker/assistant      (10 = worst)
  Repetition Score — how close it is to the rest of the batch (deterministic)    (10 = worst)
  Annoyance Score  — how annoying it would be to receive every day              (10 = worst)

The pass/fail gate per message is one question: *would the user want to receive this every
day?* A "no" is a failure regardless of role. The headline metric is the average Human Score;
the target is > 8.

Usage:
  python -m eval.humanity_lab
  python -m eval.humanity_lab --count 25 --concurrency 4
"""
from __future__ import annotations

import argparse
import asyncio
import json
import os
import re
from dataclasses import dataclass
from datetime import datetime, timezone

from app import llm
from app.config import Settings, load_settings
from app.generator import generate_message
from app.quality import jaccard, normalize_words
from app.roles import ROLES, WINDOWS

# Same six roles, but Presence first — it is the primary register in V3.
ROLE_ORDER: tuple[str, ...] = (
    "presence", "friend", "mentor", "challenger", "coach", "philosopher",
)

DEFAULT_COUNT = 20
DEFAULT_CONCURRENCY = 4
DEFAULT_TEMPERATURE = 0.9
HUMAN_TARGET = 8.0


# --- humanity judge ---------------------------------------------------------

_JUDGE_SYSTEM = (
    "Ты строго оцениваешь ОДНО сообщение, которое бот Echo сам, без повода, прислал "
    "человеку в личку Telegram. Echo должен звучать как живой знакомый человек, который "
    "просто рядом, а НЕ как мотивация, коучинг, самопомощь, философский паблик, Jira-бот, "
    "фитнес-трекер, HR- или AI-ассистент.\n"
    "Верни СТРОГО JSON без пояснений:\n"
    "{\"human\": int 0-10, \"bot\": int 0-10, \"annoying\": int 0-10, \"daily\": true/false, "
    "\"why\": \"кратко\"}\n"
    "human — реально ли так написал бы живой человек знакомому (10 — как настоящее личное "
    "сообщение; 0 — очевидный бот);\n"
    "bot — насколько похоже на бота/коуча/трекер/ассистента (больше — хуже);\n"
    "annoying — насколько раздражает получать такое (больше — хуже);\n"
    "daily — захотел бы человек получать такое сообщение КАЖДЫЙ ДЕНЬ (true — да, false — нет)."
)


@dataclass(frozen=True)
class HumanVerdict:
    human: int
    bot: int
    annoying: int
    daily: bool
    why: str = ""


def _clamp(v: object) -> int:
    try:
        return max(0, min(10, int(v)))
    except (TypeError, ValueError):
        return 0


def _as_bool(v: object) -> bool:
    if isinstance(v, bool):
        return v
    return str(v).strip().lower() in ("true", "1", "yes", "да", "y")


def _parse(raw: str) -> HumanVerdict | None:
    match = re.search(r"\{.*\}", raw, re.DOTALL)
    if not match:
        return None
    try:
        data = json.loads(match.group(0))
    except json.JSONDecodeError:
        return None
    return HumanVerdict(
        human=_clamp(data.get("human")),
        bot=_clamp(data.get("bot")),
        annoying=_clamp(data.get("annoying")),
        daily=_as_bool(data.get("daily")),
        why=str(data.get("why", ""))[:200],
    )


def _judge_providers(settings: Settings) -> list[tuple[str, str, str]]:
    chain: list[tuple[str, str, str]] = []
    if settings.groq_api_key:
        chain.append((settings.groq_base_url, settings.groq_api_key, settings.groq_model))
    if settings.openrouter_api_key:
        chain.append((settings.openrouter_base_url, settings.openrouter_api_key, settings.openrouter_model))
    return chain


async def judge_message(settings: Settings, text: str) -> HumanVerdict | None:
    """Score one standalone message (no role label — humanity must hold on its own)."""
    user = f"Сообщение Echo: «{text}»\nОцени его."
    for base_url, api_key, model in _judge_providers(settings):
        raw = await llm.chat(
            base_url=base_url, api_key=api_key, model=model,
            system=_JUDGE_SYSTEM, user=user, temperature=0.0, title="Echo humanity judge",
        )
        if raw and (parsed := _parse(raw)) is not None:
            return parsed
    return None


# --- scoring ----------------------------------------------------------------

@dataclass(frozen=True)
class Scored:
    role: str
    text: str
    source: str
    repetition: int                 # 0-10, deterministic (higher = more repetitive)
    verdict: HumanVerdict | None     # None when no judge was available


def repetition_scores(messages: list[str]) -> list[int]:
    """For each message, its closest Jaccard neighbour in the batch, scaled to 0-10."""
    words = [normalize_words(m) for m in messages]
    out: list[int] = []
    for i, wi in enumerate(words):
        nearest = max((jaccard(wi, wj) for j, wj in enumerate(words) if j != i), default=0.0)
        out.append(round(10 * nearest))
    return out


@dataclass(frozen=True)
class RoleSummary:
    role: str
    n: int
    judged: int
    human: float
    bot: float
    annoying: float
    repetition: float
    fails: int          # messages the user would not want daily

    @property
    def fail_pct(self) -> float:
        return round(100.0 * self.fails / self.judged, 1) if self.judged else 0.0


def _avg(values: list[float]) -> float:
    return round(sum(values) / len(values), 2) if values else 0.0


def weighted_human(by_role: dict[str, list[Scored]]) -> float:
    """Human Score weighted by how often each role actually fires (role default weight).

    This is the score the *user* experiences: Presence/Friend dominate the feed and the rare
    registers (Philosopher, Coach) barely show up, so a flat per-role mean understates it.
    """
    num = den = 0.0
    for role_key in ROLE_ORDER:
        judged = [s for s in by_role.get(role_key, []) if s.verdict is not None]
        if not judged:
            continue
        weight = ROLES[role_key].default_weight
        num += weight * (sum(s.verdict.human for s in judged) / len(judged))
        den += weight
    return round(num / den, 2) if den else 0.0


def summarize(scored: list[Scored]) -> RoleSummary:
    judged = [s for s in scored if s.verdict is not None]
    role = scored[0].role if scored else "?"
    return RoleSummary(
        role=role,
        n=len(scored),
        judged=len(judged),
        human=_avg([s.verdict.human for s in judged]),
        bot=_avg([s.verdict.bot for s in judged]),
        annoying=_avg([s.verdict.annoying for s in judged]),
        repetition=_avg([float(s.repetition) for s in scored]),
        fails=sum(1 for s in judged if not s.verdict.daily),
    )


# --- generation -------------------------------------------------------------

async def generate_role(
    settings: Settings, role_key: str, count: int, *, concurrency: int, temperature: float,
) -> list[tuple[str, str]]:
    sem = asyncio.Semaphore(concurrency)

    async def one(i: int) -> tuple[str, str]:
        window = WINDOWS[i % len(WINDOWS)]
        async with sem:
            return await generate_message(settings, role_key, window, [], temperature=temperature)

    return list(await asyncio.gather(*(one(i) for i in range(count))))


async def judge_role(settings: Settings, texts: list[str], concurrency: int) -> list[HumanVerdict | None]:
    sem = asyncio.Semaphore(concurrency)

    async def one(text: str) -> HumanVerdict | None:
        async with sem:
            return await judge_message(settings, text)

    return list(await asyncio.gather(*(one(t) for t in texts)))


async def run_role(
    settings: Settings, role_key: str, count: int, *, concurrency: int, temperature: float, judge_on: bool,
) -> list[Scored]:
    produced = await generate_role(settings, role_key, count, concurrency=concurrency, temperature=temperature)
    texts = [t for t, _ in produced]
    reps = repetition_scores(texts)
    verdicts = await judge_role(settings, texts, concurrency) if judge_on else [None] * len(texts)
    return [
        Scored(role=role_key, text=t, source=src, repetition=rep, verdict=v)
        for (t, src), rep, v in zip(produced, reps, verdicts)
    ]


# --- report -----------------------------------------------------------------

def _md_table(rows: list[tuple[str, ...]], headers: tuple[str, ...]) -> str:
    out = ["| " + " | ".join(headers) + " |", "| " + " | ".join("---" for _ in headers) + " |"]
    out += ["| " + " | ".join(str(x) for x in r) + " |" for r in rows]
    return "\n".join(out)


def build_markdown(by_role: dict[str, list[Scored]]) -> str:
    summaries = [summarize(by_role[r]) for r in ROLE_ORDER if by_role.get(r)]
    judged_all = [s for r in ROLE_ORDER for s in by_role.get(r, []) if s.verdict is not None]
    overall_human = _avg([s.verdict.human for s in judged_all])
    weighted = weighted_human(by_role)
    total_fails = sum(1 for s in judged_all if not s.verdict.daily)
    fail_pct = round(100.0 * total_fails / len(judged_all), 1) if judged_all else 0.0
    verdict = "✅ ДА" if weighted > HUMAN_TARGET else "❌ НЕТ"

    lines = [
        "# Echo Humanity Lab",
        "",
        f"- **Взвешенный Human Score (как у пользователя): {weighted}** / 10 "
        f"(цель > {HUMAN_TARGET:.0f}) — {verdict}",
        f"- Плоский средний Human Score: {overall_human} / 10 (все роли поровну)",
        f"- Оценено сообщений: {len(judged_all)}",
        f"- Провалов («не хотел бы получать каждый день»): {total_fails} ({fail_pct}%)",
        "",
        "## По ролям",
        "",
        _md_table(
            [
                (s.role.capitalize(), s.n, s.judged, f"{s.human:.2f}", f"{s.bot:.2f}",
                 f"{s.annoying:.2f}", f"{s.repetition:.2f}", f"{s.fails} ({s.fail_pct}%)")
                for s in summaries
            ],
            ("роль", "n", "judged", "Human↑", "Bot↓", "Annoy↓", "Repeat↓", "провалы"),
        ),
        "",
    ]

    if judged_all:
        best = sorted(judged_all, key=lambda s: s.verdict.human, reverse=True)[:8]
        worst = sorted(judged_all, key=lambda s: (s.verdict.daily, s.verdict.human))[:8]
        lines += ["## Лучшие (самые живые)", ""]
        lines += [f"- **{s.verdict.human}** [{s.role}] «{s.text}»" for s in best]
        lines += ["", "## Худшие (провальные / ботоподобные)", ""]
        for s in worst:
            daily = "каждый день: нет" if not s.verdict.daily else f"human {s.verdict.human}"
            lines.append(f"- [{s.role}] «{s.text}» · {daily} · {s.verdict.why}")
        lines.append("")

    return "\n".join(lines)


def _parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Echo humanity lab — Human/Bot/Repetition/Annoyance")
    p.add_argument("--count", type=int, default=DEFAULT_COUNT, help="messages per role")
    p.add_argument("--concurrency", type=int, default=DEFAULT_CONCURRENCY, help="parallel calls")
    p.add_argument("--temperature", type=float, default=DEFAULT_TEMPERATURE, help="generation temperature")
    p.add_argument("--no-judge", action="store_true", help="skip the LLM judge (repetition only)")
    p.add_argument("--out", default=os.path.join("eval", "reports"), help="output directory")
    return p.parse_args()


async def _main() -> None:
    args = _parse_args()
    settings = load_settings()
    has_keys = bool(settings.groq_api_key or settings.openrouter_api_key)
    judge_on = not args.no_judge and has_keys

    by_role: dict[str, list[Scored]] = {}
    for role_key in ROLE_ORDER:
        scored = await run_role(
            settings, role_key, args.count,
            concurrency=args.concurrency, temperature=args.temperature, judge_on=judge_on,
        )
        by_role[role_key] = scored
        s = summarize(scored)
        print(f"{role_key.capitalize():12} Human {s.human:.2f}  Bot {s.bot:.2f}  "
              f"Annoy {s.annoying:.2f}  Repeat {s.repetition:.2f}  провалы {s.fails}/{s.judged}")

    md = build_markdown(by_role)
    os.makedirs(args.out, exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    path = os.path.join(args.out, f"humanity-lab-{stamp}.md")
    with open(path, "w", encoding="utf-8") as f:
        f.write(md)

    judged_all = [s for r in ROLE_ORDER for s in by_role.get(r, []) if s.verdict is not None]
    overall = _avg([s.verdict.human for s in judged_all])
    weighted = weighted_human(by_role)
    print(f"\nФайл: {path}")
    if not judge_on:
        print("Судья выключен — только Repetition Score.")
    else:
        verdict = "ДА — Echo ощущается человеком" if weighted > HUMAN_TARGET else "НЕТ — ещё не дотягивает"
        print(f"Взвешенный Human Score: {weighted} / 10 (плоский {overall}) "
              f"(цель > {HUMAN_TARGET:.0f}) → {verdict}")


def main() -> None:
    asyncio.run(_main())


if __name__ == "__main__":
    main()
