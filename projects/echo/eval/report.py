"""Report builder — turns a RunResult into the markdown report character.md asks for.

Sections: summary, evaluator self-check, per-role scores (incl. Presence), best/worst
replies, most frequent problems (penalty hits + red flags), and a phrases-to-ban list.
"""
from __future__ import annotations

from collections import Counter

from .runner import Candidate, RunResult


def _role_scores(cands: list[Candidate]) -> dict[str, tuple[float, int]]:
    """role -> (avg composite character score, n judged)."""
    buckets: dict[str, list[float]] = {}
    for c in cands:
        if c.judge is not None:
            buckets.setdefault(c.role, []).append(c.judge.composite)
    return {r: (round(sum(v) / len(v), 2), len(v)) for r, v in buckets.items() if v}


def _frequent_problems(cands: list[Candidate]) -> Counter:
    problems: Counter = Counter()
    for c in cands:
        problems.update(c.penalty.hits)
        if c.judge is not None:
            problems.update(c.judge.red_flags)
    return problems


def _phrases_to_ban(cands: list[Candidate]) -> Counter:
    phrases: Counter = Counter()
    for c in cands:
        phrases.update(c.penalty.hits)
    return phrases


def _md_table(rows: list[tuple[str, ...]], headers: tuple[str, ...]) -> str:
    out = ["| " + " | ".join(headers) + " |", "| " + " | ".join("---" for _ in headers) + " |"]
    for r in rows:
        out.append("| " + " | ".join(str(x) for x in r) + " |")
    return "\n".join(out)


def build(result: RunResult, *, samples: int, temps: tuple[float, ...]) -> str:
    cands = result.candidates
    judged = [c for c in cands if c.judge is not None]
    ex = result.exemplars

    lines: list[str] = [
        "# Echo Character Benchmark — отчёт",
        "",
        f"- Режим: **{result.mode}**",
        f"- Сэмплов на сценарий: {samples} · температуры: {', '.join(map(str, temps))}",
        f"- Сгенерировано реплик: {len(cands)} · из них оценено судьёй: {len(judged)}",
        "",
        "## Самопроверка оценщика (детерминированный слой)",
        "Прогон фраз-эталонов из каталога через продакшн-гейт (`app.quality`) + штрафы.",
        "",
        f"- Плохих эталонов поймано: **{ex.bad_caught}/{ex.bad_total}** "
        f"({_pct(ex.bad_caught, ex.bad_total)})",
        f"- Хороших эталонов ошибочно зафлагано: **{ex.good_flagged}/{ex.good_total}** "
        f"({_pct(ex.good_flagged, ex.good_total)})",
        "",
        "> Детерминированный слой ловит только фразовые тэллы. Тонкий коучинг/обобщённость "
        "ловит LLM-судья — поэтому в dry-run часть «плохих» эталонов остаётся непойманной; "
        "это ожидаемо и показывает, где нужен судья.",
        "",
    ]

    if judged:
        role_scores = _role_scores(judged)
        lines += ["## Рейтинг ролей (composite 0–10, больше — лучше)", ""]
        rows = sorted(
            ((r, f"{score:.2f}", n) for r, (score, n) in role_scores.items()),
            key=lambda x: float(x[1]), reverse=True,
        )
        lines += [_md_table(rows, ("роль", "score", "n")), ""]

        ranked = sorted(judged, key=lambda c: c.judge.composite, reverse=True)
        lines += ["## Лучшие реплики", ""]
        for c in ranked[:5]:
            lines.append(f"- **{c.judge.composite}** [{c.role}] «{c.reply}»")
        lines += ["", "## Худшие реплики", ""]
        for c in ranked[-5:]:
            flags = (", ".join(c.judge.red_flags)) if c.judge.red_flags else "—"
            lines.append(f"- **{c.judge.composite}** [{c.role}] «{c.reply}» · флаги: {flags}")
        lines.append("")

    problems = _frequent_problems(cands)
    lines += ["## Самые частые проблемы", ""]
    if problems:
        lines += [_md_table(
            [(p, n) for p, n in problems.most_common(15)], ("проблема/тэлл", "раз")
        ), ""]
    else:
        lines += ["_Чисто: ни штрафов, ни красных флагов._", ""]

    phrases = _phrases_to_ban(cands)
    lines += ["## Фразы-кандидаты в бан (по частоте в сгенерированном)", ""]
    if phrases:
        lines += [_md_table(
            [(p, n) for p, n in phrases.most_common(15)], ("фраза", "раз")
        ), ""]
    else:
        lines += ["_Нет фразовых срабатываний в сгенерированных репликах._", ""]

    return "\n".join(lines)


def _pct(num: int, den: int) -> str:
    return f"{(100 * num / den):.0f}%" if den else "n/a"
