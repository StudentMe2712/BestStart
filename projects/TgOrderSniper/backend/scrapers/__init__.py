"""Web scrapers package for OmniLeadSniper.

Provides scrapers for freelance platforms (Habr Freelance, Kwork) and
a scheduler for periodic background execution.
"""

from backend.scrapers.base import BaseScraper, RawTask
from backend.scrapers.habr_freelance import HabrFreelanceScraper
from backend.scrapers.kwork_playwright import KworkPlaywrightScraper
from backend.scrapers.scheduler import WebScraperScheduler

__all__ = [
    "BaseScraper",
    "HabrFreelanceScraper",
    "KworkPlaywrightScraper",
    "RawTask",
    "WebScraperScheduler",
]
