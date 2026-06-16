"""CLI entrypoint for the Echo Character Benchmark.

Examples:
  python -m eval.run_eval --no-llm                 # zero-cost dry run (default if no keys)
  python -m eval.run_eval --samples 5 --temps 0.7,0.9
  python -m eval.run_eval --limit 6                # quick smoke over the first 6 scenarios
"""
from __future__ import annotations

import argparse
import asyncio
import json
import os
from dataclasses import asdict
from datetime import datetime, timezone

from app.config import load_settings

from . import report
from .runner import run


def _parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Echo character benchmark")
    p.add_argument("--samples", type=int, default=5, help="replies per scenario per temperature")
    p.add_argument("--temps", default="0.7,0.9", help="comma-separated temperatures (live runs)")
    p.add_argument("--no-llm", action="store_true", help="dry run: no generation/judging API calls")
    p.add_argument("--limit", type=int, default=None, help="only the first N scenarios")
    p.add_argument("--out", default=os.path.join("eval", "reports"), help="output directory")
    return p.parse_args()


def _candidate_dict(c) -> dict:
    d = asdict(c)
    d["penalty"] = asdict(c.penalty)
    d["judge"] = asdict(c.judge) if c.judge is not None else None
    return d


async def _main() -> None:
    args = _parse_args()
    temps = tuple(float(t) for t in args.temps.split(",") if t.strip())
    settings = load_settings()

    result = await run(
        settings, samples=args.samples, temps=temps,
        use_llm=not args.no_llm, limit=args.limit,
    )
    md = report.build(result, samples=args.samples, temps=temps)

    os.makedirs(args.out, exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    md_path = os.path.join(args.out, f"report-{stamp}.md")
    json_path = os.path.join(args.out, f"report-{stamp}.json")
    with open(md_path, "w", encoding="utf-8") as f:
        f.write(md)
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(
            {"mode": result.mode, "candidates": [_candidate_dict(c) for c in result.candidates]},
            f, ensure_ascii=False, indent=2,
        )

    print(md)
    print(f"\nОтчёт: {md_path}\nСырые данные: {json_path}")


def main() -> None:
    asyncio.run(_main())


if __name__ == "__main__":
    main()
