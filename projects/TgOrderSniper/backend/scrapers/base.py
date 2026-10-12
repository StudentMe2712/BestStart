"""Base interfaces and data structures for OmniLead web scrapers."""

from abc import ABC, abstractmethod

from pydantic import BaseModel, Field


class RawTask(BaseModel):
    """Normalized representation of a task or order scraped from a web platform."""

    source: str = Field(description="Scraper source identifier, e.g. 'web:habr_freelance'")
    external_id: str = Field(description="Unique ID of task on the source platform")
    title: str = Field(description="Task headline or title")
    text: str = Field(description="Full or preview description text of the task")
    url: str = Field(description="Direct URL to view the task")
    budget: str | None = Field(default=None, description="Extracted price/budget if available")
    author: str | None = Field(default=None, description="Author or client name/username")


class BaseScraper(ABC):
    """Abstract base class for web scrapers."""

    @abstractmethod
    async def fetch_new_tasks(self) -> list[RawTask]:
        """Fetches newly published tasks from the target platform.

        Returns:
            list[RawTask]: List of extracted tasks.
        """
        ...
