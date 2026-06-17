"""personality_lab — raw character exploration. No scoring, no judge, no statistics.

Generates a large batch of proactive messages per role through Echo's real production
path (LLM chain -> template fallback), writes them all to one markdown file grouped by
role, then clusters near-duplicate messages and lists the 20 most frequent patterns per
role. The question it answers is simply: *what does Echo actually say, and what does it
say most often?* — not how good it is.

Usage:
  python -m eval.personality_lab
  python -m eval.personality_lab --count 150 --concurrency 6 --temperature 1.0
"""
from __future__ import annotations

import argparse
import asyncio
import itertools
import os
from collections import Counter
from dataclasses import dataclass
from datetime import datetime, timezone

from app.config import Settings, load_settings
from app.generator import generate_message
from app.quality import jaccard, normalize_words
from app.roles import WINDOWS

# Roles in the order the spec lists them (English headers, matching the request).
ROLE_ORDER: tuple[str, ...] = (
    "friend", "presence", "coach", "philosopher",
)

DEFAULT_COUNT = 100
DEFAULT_CONCURRENCY = 4
DEFAULT_TEMPERATURE = 0.9
DEFAULT_TOP = 20
# Looser than the production gate (0.55) so near-duplicates merge into one pattern.
DEFAULT_SIMILARITY = 0.5
# A role pair above this share of mutually-confusable messages is "not unique".
OVERLAP_LIMIT_PCT = 30.0


@dataclass(frozen=True)
class Pattern:
    text: str       # representative line (the most frequent surface form in the cluster)
    count: int      # how many generated messages fell into this cluster
    variants: int   # how many distinct surface forms the cluster holds


def cluster_messages(messages: list[str], threshold: float) -> list[Pattern]:
    """Greedy single-pass clustering by Jaccard word overlap.

    Each cluster is anchored to its first member's word set; a later message joins the
    most similar cluster when overlap >= threshold, otherwise it seeds a new one. The
    representative is the most frequent exact text in the cluster. Returns patterns sorted
    by size (desc), ties broken alphabetically so the output is deterministic.
    """
    anchors: list[set[str]] = []
    counters: list[Counter] = []

    for msg in messages:
        words = normalize_words(msg)
        best_idx, best_sim = -1, 0.0
        for i, anchor in enumerate(anchors):
            sim = jaccard(words, anchor)
            if sim > best_sim:
                best_idx, best_sim = i, sim
        if best_idx >= 0 and best_sim >= threshold:
            counters[best_idx][msg] += 1
        else:
            anchors.append(words)
            counters.append(Counter({msg: 1}))

    patterns = [
        Pattern(text=counter.most_common(1)[0][0], count=sum(counter.values()), variants=len(counter))
        for counter in counters
    ]
    patterns.sort(key=lambda p: (-p.count, p.text))
    return patterns


@dataclass(frozen=True)
class PairOverlap:
    role_a: str
    role_b: str
    pct: float          # share of the two roles' messages that are mutually confusable
    unique: bool        # True when pct <= OVERLAP_LIMIT_PCT


def pair_overlap(msgs_a: list[str], msgs_b: list[str], threshold: float) -> float:
    """Percent of the two roles' messages that have a near-twin in the *other* role.

    A message is "confusable" with the other role if some message there shares >= threshold
    Jaccard word overlap. This is the single-message-identifiability metric: if a large share
    of two roles' lines could pass for each other, you can't tell the roles apart unlabeled.
    """
    if not msgs_a or not msgs_b:
        return 0.0
    wa = [normalize_words(m) for m in msgs_a]
    wb = [normalize_words(m) for m in msgs_b]
    conf_a = sum(1 for x in wa if any(jaccard(x, y) >= threshold for y in wb))
    conf_b = sum(1 for y in wb if any(jaccard(y, x) >= threshold for x in wa))
    return 100.0 * (conf_a + conf_b) / (len(wa) + len(wb))


def overlap_matrix(results: dict[str, list[str]], threshold: float) -> list[PairOverlap]:
    """Pairwise overlap for every role pair, worst first."""
    roles = [r for r in ROLE_ORDER if results.get(r)]
    pairs = [
        PairOverlap(a, b, round(pct, 1), pct <= OVERLAP_LIMIT_PCT)
        for a, b in itertools.combinations(roles, 2)
        if (pct := pair_overlap(results[a], results[b], threshold)) is not None
    ]
    pairs.sort(key=lambda p: p.pct, reverse=True)
    return pairs


async def generate_for_role(
    settings: Settings, role_key: str, count: int, *, concurrency: int, temperature: float,
) -> list[str]:
    """Generate `count` proactive messages for one role, rotating through the day's windows.

    Each message goes through the real generator (LLM chain then template fallback), so the
    batch reflects what Echo would genuinely send. `recent` is empty on purpose: we want the
    natural distribution, including repeats, so the frequency analysis is meaningful.
    """
    sem = asyncio.Semaphore(concurrency)

    async def one(i: int) -> str:
        window = WINDOWS[i % len(WINDOWS)]
        async with sem:
            text, _source = await generate_message(
                settings, role_key, window, [], temperature=temperature,
            )
            return text

    return list(await asyncio.gather(*(one(i) for i in range(count))))


def build_markdown(
    results: dict[str, list[str]], *, top_n: int, threshold: float,
) -> str:
    """Raw dump (one section per role) followed by the most-frequent-patterns section."""
    lines: list[str] = []
    for role_key in ROLE_ORDER:
        lines.append(f"# {role_key.capitalize()}")
        lines.append("")
        lines.extend(f"- {msg}" for msg in results.get(role_key, []))
        lines.append("")

    lines.append(f"# Паттерны — {top_n} самых частых на роль")
    lines.append("")
    for role_key in ROLE_ORDER:
        patterns = cluster_messages(results.get(role_key, []), threshold)[:top_n]
        lines.append(f"## {role_key.capitalize()}")
        lines.append("")
        for i, p in enumerate(patterns, 1):
            tail = f" (вариантов: {p.variants})" if p.variants > 1 else ""
            lines.append(f"{i}. «{p.text}» — {p.count}{tail}")
        lines.append("")

    lines.extend(_uniqueness_section(results, threshold))
    return "\n".join(lines)


def _uniqueness_section(results: dict[str, list[str]], threshold: float) -> list[str]:
    """Pairwise overlap matrix + verdict on which roles fail the <=30% bar."""
    roles = [r for r in ROLE_ORDER if results.get(r)]
    pairs = overlap_matrix(results, threshold)
    by_pair = {(p.role_a, p.role_b): p.pct for p in pairs}

    header = ["роль"] + [r.capitalize() for r in roles]
    lines = [
        "# Различимость ролей (пересечение паттернов)",
        "",
        f"Доля взаимно спутываемых сообщений (Jaccard ≥ {threshold}). "
        f"Порог уникальности: ≤ {OVERLAP_LIMIT_PCT:.0f}%.",
        "",
        "| " + " | ".join(header) + " |",
        "| " + " | ".join("---" for _ in header) + " |",
    ]
    for a in roles:
        cells = [a.capitalize()]
        for b in roles:
            if a == b:
                cells.append("—")
            else:
                key = (a, b) if (a, b) in by_pair else (b, a)
                cells.append(f"{by_pair.get(key, 0.0):.1f}%")
        lines.append("| " + " | ".join(cells) + " |")
    lines.append("")

    failed = [p for p in pairs if not p.unique]
    if failed:
        lines.append(f"**Неуникальные пары (> {OVERLAP_LIMIT_PCT:.0f}%):**")
        lines += [f"- {p.role_a.capitalize()} ↔ {p.role_b.capitalize()}: {p.pct:.1f}%" for p in failed]
    else:
        lines.append(
            f"**Все пары ≤ {OVERLAP_LIMIT_PCT:.0f}% — каждую роль можно определить "
            "по одному сообщению без подписи.**"
        )
    lines.append("")
    return lines


def _parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Echo personality lab — raw, unscored generation")
    p.add_argument("--count", type=int, default=DEFAULT_COUNT, help="messages per role (min 100)")
    p.add_argument("--concurrency", type=int, default=DEFAULT_CONCURRENCY, help="parallel generations")
    p.add_argument("--temperature", type=float, default=DEFAULT_TEMPERATURE, help="sampling temperature")
    p.add_argument("--top", type=int, default=DEFAULT_TOP, help="patterns to show per role")
    p.add_argument("--similarity", type=float, default=DEFAULT_SIMILARITY, help="clustering threshold")
    p.add_argument("--out", default=os.path.join("eval", "reports"), help="output directory")
    return p.parse_args()


async def _main() -> None:
    args = _parse_args()
    count = max(args.count, DEFAULT_COUNT)  # the spec floor is 100 per role
    settings = load_settings()

    results: dict[str, list[str]] = {}
    for role_key in ROLE_ORDER:
        msgs = await generate_for_role(
            settings, role_key, count,
            concurrency=args.concurrency, temperature=args.temperature,
        )
        results[role_key] = msgs
        print(f"{role_key.capitalize():12} → {len(msgs)} сообщений")

    md = build_markdown(results, top_n=args.top, threshold=args.similarity)

    os.makedirs(args.out, exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    path = os.path.join(args.out, f"personality-lab-{stamp}.md")
    with open(path, "w", encoding="utf-8") as f:
        f.write(md)

    print(f"\nФайл: {path}\n")
    for role_key in ROLE_ORDER:
        patterns = cluster_messages(results[role_key], args.similarity)[: args.top]
        print(f"# {role_key.capitalize()} — топ паттернов")
        for i, p in enumerate(patterns, 1):
            print(f"  {i:2}. «{p.text}» — {p.count}")
        print()

    pairs = overlap_matrix(results, args.similarity)
    print(f"# Различимость ролей (порог ≤ {OVERLAP_LIMIT_PCT:.0f}%)")
    for p in pairs:
        mark = "ok" if p.unique else "НЕ УНИКАЛЬНА"
        print(f"  {p.role_a.capitalize():12} ↔ {p.role_b.capitalize():12} {p.pct:5.1f}%  {mark}")
    failed = [p for p in pairs if not p.unique]
    print(
        "\nИТОГ: все пары различимы ≤ 30%."
        if not failed
        else f"\nИТОГ: {len(failed)} пар(ы) выше порога — роли пока неуникальны."
    )


def main() -> None:
    asyncio.run(_main())


if __name__ == "__main__":
    main()
