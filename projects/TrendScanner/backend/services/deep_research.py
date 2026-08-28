"""Backward-compatible facade for DeepResearchService."""

from app.services.deep_research import (
    COMPETITOR_ANALYSIS_SYSTEM_PROMPT,
    DeepResearchService,
    deep_research_service,
    extract_wikilinks,
    run_deep_research,
    sanitize_vault_filename,
)

__all__ = [
    "COMPETITOR_ANALYSIS_SYSTEM_PROMPT",
    "DeepResearchService",
    "deep_research_service",
    "extract_wikilinks",
    "run_deep_research",
    "sanitize_vault_filename",
]
