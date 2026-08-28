"""Pydantic schemas for TrendScanner API requests and responses."""

from datetime import datetime
from typing import Any, Dict, List, Optional, Union
from pydantic import BaseModel, ConfigDict, Field


# --- Sources Schemas ---

class SourceBase(BaseModel):
    name: str = Field(..., description="Source display name")
    url: str = Field(..., description="URL endpoint or RSS feed URL")
    source_type: str = Field(..., description="Type of source parser: rss, reddit, telegram_html")
    is_active: bool = Field(default=True, description="Whether source is active for auto-scanning")


class SourceCreate(SourceBase):
    pass


class SourceUpdate(BaseModel):
    name: Optional[str] = None
    url: Optional[str] = None
    source_type: Optional[str] = None
    is_active: Optional[bool] = None


class SourceResponse(SourceBase):
    model_config = ConfigDict(from_attributes=True)

    id: int
    last_scanned: Optional[Union[datetime, str]] = None


# --- Trends Schemas ---

class TrendBase(BaseModel):
    source_id: int
    original_text: str
    is_trend: bool = False
    trend_name: Optional[str] = None
    ai_score: Optional[int] = Field(None, description="AI rating from 1 to 10")
    scam_probability: Optional[int] = Field(None, description="Scam probability in percent")
    ai_summary: Optional[str] = None
    source_url: Optional[str] = None
    is_reviewed: bool = False
    ai_status: Optional[str] = "pending"
    mention_count: int = 1
    detailed_report: Optional[str] = None
    is_liked: bool = False
    is_new: bool = True


class TrendCreate(TrendBase):
    content_hash: Optional[str] = None


class TrendResponse(TrendBase):
    model_config = ConfigDict(from_attributes=True)

    id: int
    content_hash: Optional[str] = None
    parsed_date: Optional[Union[datetime, str]] = None
    source_name: Optional[str] = None
    source_type: Optional[str] = None
    ai_status: Optional[str] = "pending"
    mention_count: int = 1
    detailed_report: Optional[str] = None
    is_liked: bool = False
    is_new: bool = True


class TrendReviewUpdate(BaseModel):
    is_reviewed: bool = True


class TrendLikeUpdate(BaseModel):
    is_liked: Optional[bool] = None


class TrendLikeResponse(BaseModel):
    trend_id: int
    is_liked: bool
    updated: bool


class TrendReportResponse(BaseModel):
    trend_id: int
    detailed_report: str
    trend_name: Optional[str] = None


class DeepResearchResponse(BaseModel):
    status: str = "success"
    trend_id: int
    file_name: str
    file_path: str
    detailed_report: Optional[str] = None
    message: Optional[str] = None


# --- System & Orchestration Schemas ---

class SystemStatusResponse(BaseModel):
    status: str = "operational"
    scheduler_running: bool = False
    next_run_time: Optional[str] = None
    next_scan_time: Optional[str] = None
    stats: Dict[str, Any] = Field(default_factory=dict)
    active_sources_count: int = 0
    pending_ai_count: int = 0
    groq_model: str = "openai/gpt-oss-20b"
    last_scan_time: Optional[str] = None


class ManualScanResponse(BaseModel):
    status: str
    scanned_sources: int
    new_trends_found: int
    processed_ai: Optional[int] = 0
    pending_ai_count: Optional[int] = 0
    errors: List[str] = Field(default_factory=list)
