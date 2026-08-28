"""Global Radar Pipeline: Ingestion Queue & Throttled Groq AI Worker."""

import asyncio
import logging
from datetime import datetime, timezone
from typing import Any, Dict, List, Optional

import httpx

from app.core.settings import settings
from app.db.dao import SourcesDAO, TrendsDAO, calculate_text_hash
from app.services.extractors import get_extractor
from app.services.groq_client import groq_client
from app.services.sanitizer import (
    extract_candidate_sources,
    is_predominantly_cyrillic,
    sanitize_and_translate_content,
)
from app.services.deduplicator import deduplicator
from app.services.notifier import notifier

logger = logging.getLogger(__name__)


class PipelineManager:
    """Orchestrator managing asynchronous ingestion queue and throttled Groq AI classification."""

    def __init__(self) -> None:
        self._ingest_lock = asyncio.Lock()
        self._groq_lock = asyncio.Lock()
        self.is_ingesting: bool = False
        self.is_classifying: bool = False
        self.last_ingest_time: Optional[datetime] = None
        self.last_run_summary: Dict[str, Any] = {}
        self.rate_limited_until: Optional[float] = None

    async def ingest_source(self, source: Dict[str, Any]) -> Dict[str, Any]:
        """Scrape raw items from a source, deduplicate, sanitize, discover candidate sources, and insert into pending queue."""
        source_id = source["id"]
        source_name = source["name"]
        source_url = source["url"]
        source_type = source["source_type"]

        logger.info("Radar scanning source #%d '%s' (%s)...", source_id, source_name, source_type)
        report: Dict[str, Any] = {
            "source_id": source_id,
            "source_name": source_name,
            "extracted_count": 0,
            "skipped_duplicates": 0,
            "rejected_sanitizer": 0,
            "queued_pending": 0,
            "errors": [],
        }

        # 1. Extractor
        extractor = get_extractor(source_type)
        if not extractor:
            err_msg = f"Unsupported extractor type '{source_type}' for source #{source_id}"
            logger.warning(err_msg)
            report["errors"].append(err_msg)
            return report

        # 2. Extract
        try:
            items = await extractor.extract(source_url)
            report["extracted_count"] = len(items)
        except Exception as exc:
            err_msg = f"Extraction failed for source #{source_id}: {exc}"
            logger.error(err_msg)
            report["errors"].append(err_msg)
            return report

        if not items:
            SourcesDAO.update_last_scanned(source_id)
            return report

        # 3. Deduplicate (Level 2 Smart Deduplication Engine)
        candidate_items = []
        recent_candidates = TrendsDAO.get_recent_candidates(limit=500)

        for item in items:
            h = calculate_text_hash(item.text)
            dedup_res = deduplicator.find_duplicate(
                title=item.title,
                text=item.text,
                url=item.url,
                candidates=recent_candidates,
            )
            if dedup_res.is_duplicate:
                report["skipped_duplicates"] += 1
                if dedup_res.matched_trend_id:
                    matched_trend = next(
                        (c for c in recent_candidates if c.get("id") == dedup_res.matched_trend_id),
                        None,
                    )
                    existing_text = (
                        matched_trend.get("original_text", "") if matched_trend else ""
                    )
                    merged_text = deduplicator.format_merged_text(
                        existing_text=existing_text,
                        new_text=item.text,
                        new_source_name=source_name,
                    )
                    TrendsDAO.increment_mention_count(
                        dedup_res.matched_trend_id, merged_text=merged_text
                    )
            else:
                candidate_items.append((h, item))
                # Register in local batch candidate list to avoid intra-batch duplicates
                recent_candidates.append(
                    {
                        "id": None,
                        "trend_name": item.title,
                        "original_text": item.text,
                        "source_url": item.url,
                        "content_hash": h,
                    }
                )

        if not candidate_items:
            SourcesDAO.update_last_scanned(source_id)
            return report

        # 4. Sanitize and discover candidate sources
        to_queue = []
        for h, item in candidate_items:
            # Extract candidate external sources for auto-discovery
            try:
                discovered_candidates = extract_candidate_sources(item.text)
                for candidate in discovered_candidates:
                    cand_url = candidate.get("url")
                    cand_name = candidate.get("name") or cand_url
                    if cand_url and not SourcesDAO.exists_by_url(cand_url):
                        SourcesDAO.create(
                            name=cand_name,
                            url=cand_url,
                            source_type="auto_discovered",
                            is_active=True,
                        )
                        logger.info("Auto-discovered new source: %s (%s)", cand_name, cand_url)
            except Exception as disc_err:
                logger.warning("Error auto-discovering candidate sources: %s", disc_err)

            # Mandatory Level 11 Language Checkpoint & Translation ("No English" Rule)
            res = await sanitize_and_translate_content(item.text, min_length=settings.MIN_TEXT_LENGTH)
            if not res.is_valid:
                report["rejected_sanitizer"] += 1
                if res.reject_reason == "untranslated_english":
                    logger.info("Source #%d: Dropped item due to untranslated English: '%s...'", source_id, item.text[:60])
            else:
                # Ensure title is in Russian
                trend_title = item.title[:120] if item.title else "Сигнал Радара"
                if not is_predominantly_cyrillic(trend_title):
                    try:
                        translated_title = await groq_client.translate_to_russian(trend_title)
                        if translated_title and is_predominantly_cyrillic(translated_title):
                            trend_title = translated_title[:120]
                    except Exception:
                        pass

                to_queue.append(
                    {
                        "source_id": source_id,
                        "original_text": res.cleaned_text,
                        "content_hash": h,
                        "is_trend": False,
                        "trend_name": trend_title,
                        "ai_score": None,
                        "scam_probability": None,
                        "ai_summary": None,
                        "source_url": item.url,
                        "is_reviewed": False,
                        "ai_status": "pending",
                        "is_new": True,
                    }
                )

        # 5. Insert into Queue (ai_status='pending')
        if to_queue:
            saved_count = TrendsDAO.create_batch(to_queue)
            report["queued_pending"] = saved_count
            logger.info("Source #%d: queued %d items for AI processing.", source_id, saved_count)

        SourcesDAO.update_last_scanned(source_id)
        return report

    async def ingest_all_sources(self) -> Dict[str, Any]:
        """Run radar ingestion cycle across all active sources."""
        if self._ingest_lock.locked():
            return {
                "status": "already_running",
                "message": "Radar ingestion is already active.",
                "scanned_sources": 0,
                "queued_items": 0,
                "reports": [],
            }

        async with self._ingest_lock:
            self.is_ingesting = True
            try:
                archived_count = TrendsDAO.archive_previous_inbox()
                logger.info("Archived %d previous inbox trends to Trend Database.", archived_count)

                start_time = datetime.now(timezone.utc)
                sources = SourcesDAO.get_all(active_only=True)

                logger.info("Starting Radar ingestion cycle across %d active sources...", len(sources))
                total_queued = 0
                reports = []

                for src in sources:
                    try:
                        src_report = await self.ingest_source(src)
                        total_queued += src_report.get("queued_pending", 0)
                        reports.append(src_report)
                    except Exception as src_err:
                        logger.error("Error in ingestion for source #%s: %s", src.get("id"), src_err)
                        reports.append({"source_id": src.get("id"), "error": str(src_err)})

                self.last_ingest_time = start_time
                self.last_run_summary = {
                    "status": "completed",
                    "scanned_sources": len(sources),
                    "queued_items": total_queued,
                    "pending_queue_size": TrendsDAO.count_pending(),
                    "duration_seconds": (datetime.now(timezone.utc) - start_time).total_seconds(),
                    "reports": reports,
                }
                return self.last_run_summary
            finally:
                self.is_ingesting = False

    async def process_groq_queue(self, batch_size: int = 3) -> Dict[str, Any]:
        """Throttling worker: picks a small batch of pending items and evaluates them via Groq."""
        if self._groq_lock.locked():
            return {"status": "busy", "processed": 0}

        async with self._groq_lock:
            self.is_classifying = True
            try:
                pending_items = TrendsDAO.get_pending_trends(limit=batch_size)
                if not pending_items:
                    return {"status": "idle", "processed": 0}

                logger.info("Groq worker processing %d pending items...", len(pending_items))
                processed_count = 0

                for item in pending_items:
                    item_id = item["id"]
                    cleaned_text = item["original_text"]

                    try:
                        ai_res = await groq_client.classify_text(cleaned_text)
                        if ai_res:
                            TrendsDAO.update_ai_classification(
                                trend_id=item_id,
                                is_trend=ai_res.is_trend,
                                trend_name=ai_res.trend_name or item.get("trend_name"),
                                ai_score=ai_res.ai_score,
                                scam_probability=ai_res.scam_probability,
                                ai_summary=ai_res.ai_summary,
                                ai_status="processed",
                            )
                            processed_count += 1
                            logger.info("Classified trend #%d: score=%s scam=%s", item_id, ai_res.ai_score, ai_res.scam_probability)

                            # Trigger Telegram Push Alert for top-tier trends (ai_score >= 9 and scam < 15)
                            if ai_res.ai_score >= 9 and ai_res.scam_probability < 15:
                                logger.info("High-value trend #%d detected! Dispatching Telegram push alert...", item_id)
                                try:
                                    await notifier.send_trend_alert(
                                        trend_name=ai_res.trend_name or item.get("trend_name") or "Перспективный тренд",
                                        ai_score=ai_res.ai_score,
                                        scam_probability=ai_res.scam_probability,
                                        ai_summary=ai_res.ai_summary,
                                        source_url=item.get("source_url"),
                                        mention_count=item.get("mention_count", 1),
                                        is_liked=item.get("is_liked", False),
                                    )
                                except Exception as alert_err:
                                    logger.error("Failed to send Telegram push alert: %s", alert_err)
                        else:
                            # Parsing error or unclassified
                            TrendsDAO.update_ai_classification(
                                trend_id=item_id,
                                is_trend=False,
                                trend_name=item.get("trend_name"),
                                ai_score=1,
                                scam_probability=0,
                                ai_summary="Не удалось классифицировать ответ ИИ.",
                                ai_status="failed",
                            )

                    except httpx.HTTPStatusError as http_err:
                        if http_err.response.status_code == 429:
                            logger.warning("Groq HTTP 429 Rate Limit encountered! Sleeping worker for 60s...")
                            # Put worker to sleep for 60 seconds to protect rate limits
                            await asyncio.sleep(60)
                            break
                        else:
                            logger.error("Groq HTTP error on item #%d: %s", item_id, http_err)
                            TrendsDAO.update_ai_classification(
                                trend_id=item_id,
                                is_trend=False,
                                trend_name=item.get("trend_name"),
                                ai_score=1,
                                scam_probability=0,
                                ai_summary=f"Ошибка API: {http_err}",
                                ai_status="failed",
                            )
                    except Exception as exc:
                        logger.error("Unexpected failure evaluating trend #%d: %s", item_id, exc)
                        TrendsDAO.update_ai_classification(
                            trend_id=item_id,
                            is_trend=False,
                            trend_name=item.get("trend_name"),
                            ai_score=1,
                            scam_probability=0,
                            ai_summary="Сбой воркера классификации.",
                            ai_status="failed",
                        )

                return {
                    "status": "completed",
                    "processed": processed_count,
                    "remaining_pending": TrendsDAO.count_pending(),
                }
            finally:
                self.is_classifying = False

    async def run_crawler_cycle(self, queries: Optional[List[str]] = None) -> Dict[str, Any]:
        """Trigger global deep web search crawler cycle."""
        crawler_source = None
        sources = SourcesDAO.get_all()
        for s in sources:
            if s.get("source_type") == "deep_crawler":
                crawler_source = s
                break

        if not crawler_source:
            source_id = SourcesDAO.create(
                name="Глобальный ИИ-Поисковый Краулер (Deep Web)",
                url="https://duckduckgo.com/?q=new+ai+saas+2026",
                source_type="deep_crawler",
                is_active=True,
            )
            crawler_source = SourcesDAO.get_by_id(source_id)

        if crawler_source:
            return await self.ingest_source(crawler_source)
        return {"status": "error", "message": "Failed to resolve deep crawler source"}

    async def run_all(self) -> Dict[str, Any]:
        """Trigger immediate radar ingestion and process first batch through Groq."""
        archived_count = TrendsDAO.archive_previous_inbox()
        logger.info("Archived %d previous inbox trends to Trend Database.", archived_count)
        ingest_summary = await self.ingest_all_sources()
        groq_summary = await self.process_groq_queue(batch_size=3)
        return {
            "status": "completed",
            "scanned_sources": ingest_summary.get("scanned_sources", 0),
            "new_trends_found": ingest_summary.get("queued_items", 0),
            "processed_ai": groq_summary.get("processed", 0),
            "pending_ai_count": TrendsDAO.count_pending(),
            "reports": ingest_summary.get("reports", []),
        }


pipeline_manager = PipelineManager()

