"""FastAPI API Routers for Trends, Sources, System, and Manual Scans."""

from typing import List, Optional
from fastapi import APIRouter, HTTPException, Query, status

from app.core.settings import settings
from app.db.dao import SourcesDAO, TrendsDAO
from app.models.schemas import (
    DeepResearchResponse,
    ManualScanResponse,
    SourceCreate,
    SourceResponse,
    SourceUpdate,
    SystemPauseResponse,
    SystemStatusResponse,
    TrendFeedbackResponse,
    TrendFeedbackUpdate,
    TrendLikeResponse,
    TrendLikeUpdate,
    TrendReportResponse,
    TrendResponse,
    TrendReviewUpdate,
)
from app.services.deep_research import run_deep_research
from app.services.groq_client import groq_client
from app.services.pipeline import pipeline_manager
from app.workers.scheduler import get_scheduler_status, pause_scheduler, resume_scheduler

router = APIRouter(prefix=settings.API_V1_PREFIX)


# --- Trends Endpoints ---

@router.get(
    "/trends",
    response_model=List[TrendResponse],
    tags=["Trends"],
    summary="Get paginated and filtered trends list",
)
async def get_trends(
    skip: int = Query(0, ge=0, description="Offset for pagination"),
    limit: int = Query(50, ge=1, le=200, description="Page limit"),
    min_score: Optional[int] = Query(None, ge=1, le=10, description="Minimum AI score (1-10)"),
    max_scam: Optional[int] = Query(None, ge=0, le=100, description="Maximum scam probability % (0-100)"),
    status: Optional[str] = Query(None, pattern="^(new|reviewed)$", description="Filter by review status"),
    source_id: Optional[int] = Query(None, description="Filter by source ID"),
    only_trends: Optional[bool] = Query(None, description="Filter confirmed trends only (is_trend=1)"),
    tab: Optional[str] = Query(None, pattern="^(inbox|liked|disliked|database|history|archive|all)$", description="Filter by tab: inbox (default), liked, disliked, database, history, archive, all"),
    is_liked: Optional[bool] = Query(None, description="Filter by liked status"),
    user_feedback: Optional[int] = Query(None, ge=-1, le=1, description="Filter by user feedback score: 1 (Like), -1 (Dislike), 0 (Neutral)"),
    is_new: Optional[bool] = Query(None, description="Filter by new inbox status"),
    search: Optional[str] = Query(None, description="Search term in trend name, summary, or text"),
):
    """Retrieve trends grid with analytical filters, Inbox Zero tabs, database archive, and search."""
    trends = TrendsDAO.get_trends(
        skip=skip,
        limit=limit,
        min_score=min_score,
        max_scam=max_scam,
        status=status,
        source_id=source_id,
        only_trends=only_trends,
        tab=tab,
        is_liked=is_liked,
        user_feedback=user_feedback,
        is_new=is_new,
        search_query=search,
    )
    return trends


@router.get(
    "/trends/{trend_id}",
    response_model=TrendResponse,
    tags=["Trends"],
    summary="Get single trend by ID",
)
async def get_trend_by_id(trend_id: int):
    """Retrieve details for a specific trend item."""
    trend = TrendsDAO.get_by_id(trend_id)
    if not trend:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"Trend with ID #{trend_id} not found.",
        )
    return trend


@router.post(
    "/trends/{trend_id}/report",
    response_model=TrendReportResponse,
    tags=["Trends"],
    summary="Generate or retrieve deep analytical report for a trend",
)
async def generate_trend_report(trend_id: int):
    """Generate extensive AI deep report in Russian on demand or return cached report."""
    trend = TrendsDAO.get_by_id(trend_id)
    if not trend:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"Trend with ID #{trend_id} not found.",
        )

    # Return cached report if already generated
    if trend.get("detailed_report"):
        return TrendReportResponse(
            trend_id=trend_id,
            detailed_report=trend["detailed_report"],
            trend_name=trend.get("trend_name"),
        )

    # Generate deep report via Groq LLM
    report_text = await groq_client.generate_deep_report(
        text=trend["original_text"],
        trend_name=trend.get("trend_name") or "",
    )

    if not report_text:
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail="Failed to generate deep report via Groq AI. Check API key or try again.",
        )

    # Persist report in database
    TrendsDAO.save_detailed_report(trend_id, report_text)

    return TrendReportResponse(
        trend_id=trend_id,
        detailed_report=report_text,
        trend_name=trend.get("trend_name"),
    )


@router.post(
    "/trends/{trend_id}/deep-research",
    response_model=DeepResearchResponse,
    tags=["Trends"],
    summary="Conduct deep web search, competitor analysis, and save Obsidian Vault note",
)
async def deep_research_trend(trend_id: int):
    """Conduct deep competitor intelligence and write markdown note to Obsidian Vault."""
    trend = TrendsDAO.get_by_id(trend_id)
    if not trend:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"Trend with ID #{trend_id} not found.",
        )

    try:
        result = await run_deep_research(trend_id=trend_id)
        return DeepResearchResponse(
            status="success",
            trend_id=trend_id,
            file_name=result.get("file_name", f"Trend_{trend_id}.md"),
            file_path=result.get("file_path", ""),
            detailed_report=result.get("competitor_analysis") or trend.get("detailed_report"),
            message="Файл сохранен в Vault",
        )
    except Exception as exc:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Failed to execute deep research: {str(exc)}",
        )


@router.patch(
    "/trends/{trend_id}/feedback",
    response_model=TrendFeedbackResponse,
    tags=["Trends"],
    summary="Set user feedback score for a trend (RLHF loop)",
)
@router.put(
    "/trends/{trend_id}/feedback",
    response_model=TrendFeedbackResponse,
    tags=["Trends"],
    summary="Set user feedback score for a trend (RLHF loop)",
)
async def set_trend_feedback(
    trend_id: int,
    payload: TrendFeedbackUpdate,
):
    """Set RLHF user feedback rating: 1 (Like), -1 (Dislike), 0 (Neutral)."""
    trend = TrendsDAO.get_by_id(trend_id)
    if not trend:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"Trend with ID #{trend_id} not found.",
        )

    score = TrendsDAO.set_feedback(trend_id, payload.score)
    if score is None:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"Trend with ID #{trend_id} not found.",
        )

    return TrendFeedbackResponse(
        trend_id=trend_id,
        user_feedback=score,
        is_liked=(score == 1),
        updated=True,
    )


@router.patch(
    "/trends/{trend_id}/like",
    response_model=TrendLikeResponse,
    tags=["Trends"],
    summary="Toggle or set like/favorite status for a trend (backward compatible)",
)
@router.put(
    "/trends/{trend_id}/like",
    response_model=TrendLikeResponse,
    tags=["Trends"],
    summary="Toggle or set like/favorite status for a trend (backward compatible)",
)
async def like_trend(
    trend_id: int,
    payload: Optional[TrendLikeUpdate] = None,
):
    """Toggle like status or set explicitly (backward compatibility)."""
    trend = TrendsDAO.get_by_id(trend_id)
    if not trend:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"Trend with ID #{trend_id} not found.",
        )

    if payload is not None and payload.is_liked is not None:
        score = 1 if payload.is_liked else 0
        res = TrendsDAO.set_feedback(trend_id, score)
        if res is None:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail=f"Trend with ID #{trend_id} not found.",
            )
        new_state = (res == 1)
    else:
        new_state = TrendsDAO.toggle_like(trend_id)
        if new_state is None:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail=f"Trend with ID #{trend_id} not found.",
            )

    return TrendLikeResponse(
        trend_id=trend_id,
        is_liked=new_state,
        updated=True,
    )


@router.put(
    "/trends/{trend_id}/review",
    tags=["Trends"],
    summary="Mark trend as reviewed or unreviewed",
)
async def review_trend(
    trend_id: int,
    payload: TrendReviewUpdate = TrendReviewUpdate(is_reviewed=True),
):
    """Update trend review status."""
    trend = TrendsDAO.get_by_id(trend_id)
    if not trend:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"Trend with ID #{trend_id} not found.",
        )

    updated = TrendsDAO.mark_reviewed(trend_id, is_reviewed=payload.is_reviewed)
    return {
        "trend_id": trend_id,
        "is_reviewed": payload.is_reviewed,
        "updated": updated,
    }


@router.delete(
    "/trends/{trend_id}",
    tags=["Trends"],
    summary="Delete a trend record",
)
async def delete_trend(trend_id: int):
    """Delete a trend record from database."""
    deleted = TrendsDAO.delete(trend_id)
    if not deleted:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"Trend with ID #{trend_id} not found.",
        )
    return {"deleted": True, "trend_id": trend_id}


# --- Sources Endpoints ---

@router.get(
    "/sources",
    response_model=List[SourceResponse],
    tags=["Sources"],
    summary="List all configured sources",
)
async def get_sources(
    active_only: bool = Query(False, description="Filter active sources only")
):
    """Retrieve all ingestion sources and their scan status."""
    return SourcesDAO.get_all(active_only=active_only)


@router.post(
    "/sources",
    response_model=SourceResponse,
    status_code=status.HTTP_201_CREATED,
    tags=["Sources"],
    summary="Create a new ingestion source",
)
async def create_source(source_in: SourceCreate):
    """Register a new source for scheduled or manual scanning."""
    new_id = SourcesDAO.create(
        name=source_in.name,
        url=source_in.url,
        source_type=source_in.source_type,
        is_active=source_in.is_active,
    )
    source = SourcesDAO.get_by_id(new_id)
    return source


@router.put(
    "/sources/{source_id}",
    response_model=SourceResponse,
    tags=["Sources"],
    summary="Update an existing source",
)
async def update_source(source_id: int, source_in: SourceUpdate):
    """Modify source configuration or toggle active state."""
    existing = SourcesDAO.get_by_id(source_id)
    if not existing:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"Source with ID #{source_id} not found.",
        )

    SourcesDAO.update(
        source_id=source_id,
        name=source_in.name,
        url=source_in.url,
        source_type=source_in.source_type,
        is_active=source_in.is_active,
    )
    return SourcesDAO.get_by_id(source_id)


@router.delete(
    "/sources/{source_id}",
    tags=["Sources"],
    summary="Delete a source",
)
async def delete_source(source_id: int):
    """Remove a source and cascade delete its associated trends."""
    deleted = SourcesDAO.delete(source_id)
    if not deleted:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"Source with ID #{source_id} not found.",
        )
    return {"deleted": True, "source_id": source_id}


# --- Scanning & System Status ---

@router.post(
    "/scan/manual",
    response_model=ManualScanResponse,
    tags=["Scan"],
    summary="Trigger manual pipeline execution",
)
async def trigger_manual_scan():
    """Trigger an immediate radar ingestion and Groq queue batch execution."""
    if pipeline_manager.is_ingesting:
        return ManualScanResponse(
            status="busy",
            scanned_sources=0,
            new_trends_found=0,
            processed_ai=0,
            pending_ai_count=TrendsDAO.count_pending(),
            errors=["Radar is already executing an ingestion cycle."],
        )

    summary = await pipeline_manager.run_all()
    all_errors: List[str] = []
    for rep in summary.get("reports", []):
        all_errors.extend(rep.get("errors", []))

    return ManualScanResponse(
        status=summary.get("status", "completed"),
        scanned_sources=summary.get("scanned_sources", 0),
        new_trends_found=summary.get("new_trends_found", 0),
        processed_ai=summary.get("processed_ai", 0),
        pending_ai_count=summary.get("pending_ai_count", TrendsDAO.count_pending()),
        errors=all_errors,
    )


@router.get(
    "/system/status",
    tags=["System"],
    summary="Get complete system health, statistics, and scheduler info",
)
async def get_system_status():
    """System status and aggregate counters for dashboard topbar."""
    active_sources_cnt = SourcesDAO.count_active()
    stats = TrendsDAO.get_stats()
    sched_info = get_scheduler_status()

    # Determine last scan timestamp
    last_scan_time: Optional[str] = None
    if pipeline_manager.last_ingest_time:
        last_scan_time = pipeline_manager.last_ingest_time.isoformat()
    else:
        last_scan_time = SourcesDAO.get_last_scan_time() or TrendsDAO.get_last_scan_time()

    return {
        "status": "operational",
        "scheduler": sched_info,
        "is_paused": sched_info.get("is_paused", False),
        "active_sources_count": active_sources_cnt,
        "pending_ai_count": TrendsDAO.count_pending(),
        "stats": stats,
        "groq_model": settings.GROQ_MODEL,
        "last_scan": pipeline_manager.last_run_summary,
        "last_scan_time": last_scan_time,
        "next_scan_time": None if sched_info.get("is_paused") else sched_info.get("next_run_time"),
    }


@router.post(
    "/system/pause",
    response_model=SystemPauseResponse,
    tags=["System"],
    summary="Pause automated scanner scheduler",
)
async def pause_system_scheduler():
    """Pause automated background scanning scheduler."""
    return pause_scheduler()


@router.post(
    "/system/resume",
    response_model=SystemPauseResponse,
    tags=["System"],
    summary="Resume automated scanner scheduler",
)
async def resume_system_scheduler():
    """Resume automated background scanning scheduler."""
    return resume_scheduler()
