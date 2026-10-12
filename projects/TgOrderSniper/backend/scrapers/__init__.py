"""Web scrapers package for OmniLeadSniper.

Provides scrapers for freelance platforms (Kwork, Freelancehunt, FL.ru) and
a scheduler for periodic background execution.
"""

from backend.scrapers.base import BaseScraper, RawTask
from backend.scrapers.fl_ru import FlRuScraper
from backend.scrapers.freelancehunt import FreelancehuntScraper
from backend.scrapers.habr_freelance import HabrFreelanceScraper
from backend.scrapers.kwork_playwright import KworkPlaywrightScraper
from backend.scrapers.scheduler import WebScraperScheduler

__all__ = [
    "BaseScraper",
    "FlRuScraper",
    "FreelancehuntScraper",
    "HabrFreelanceScraper",
    "KworkPlaywrightScraper",
    "RawTask",
    "WebScraperScheduler",
]
