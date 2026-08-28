"""Base extractor interfaces and data models for TrendScanner (Stage 2)."""

import logging
from abc import ABC, abstractmethod
from datetime import datetime
from typing import Any, Dict, List, Optional
from pydantic import BaseModel, ConfigDict, Field

logger = logging.getLogger(__name__)


class ExtractedItem(BaseModel):
    """Normalized data model representing a raw content item extracted from a source."""
    model_config = ConfigDict(arbitrary_types_allowed=True)

    title: str = Field(..., description="Title or headline of the extracted item")
    text: str = Field(..., description="Extracted body text or full content of the item")
    url: str = Field(..., description="Direct URL or permalink to the original source item")
    published_at: Optional[datetime] = Field(default=None, description="Publication timestamp in UTC")
    author: Optional[str] = Field(default=None, description="Author or username of the creator/poster")
    source_type: str = Field(default="", description="Identifier of the source parser (e.g. 'rss', 'reddit')")
    extra: Dict[str, Any] = Field(default_factory=dict, description="Additional source-specific metadata")


class BaseExtractor(ABC):
    """Abstract Base Class for all data extractors."""

    DEFAULT_USER_AGENT: str = "TrendScanner/1.0 (+https://github.com/BestStart/TrendScanner)"
    DEFAULT_TIMEOUT: float = 15.0

    def __init__(
        self,
        timeout: float = DEFAULT_TIMEOUT,
        user_agent: Optional[str] = None,
    ) -> None:
        """Initialize extractor with timeout and user-agent settings.

        Args:
            timeout: Network request timeout in seconds.
            user_agent: Custom HTTP User-Agent header value.
        """
        self.timeout = timeout
        self.user_agent = user_agent or self.DEFAULT_USER_AGENT
        self.logger = logging.getLogger(self.__class__.__name__)

    @abstractmethod
    async def extract(self, url: str) -> List[ExtractedItem]:
        """Extract items from the given source URL.

        Args:
            url: Target URL, feed URL, or API endpoint to extract from.

        Returns:
            List[ExtractedItem]: Extracted items. Returns empty list on network,
                                 parsing, or validation errors without raising exceptions.
        """
        pass
