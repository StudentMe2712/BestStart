"""Web scrapers package for OmniLeadSniper.

Provides scrapers for freelance platforms (Freelancehunt, Upwork, Weblancer, FL.ru) and
a scheduler for periodic background execution.
"""

from backend.scrapers.base import BaseScraper, RawTask
from backend.scrapers.fl_ru import FlRuScraper
from backend.scrapers.freelancehunt import FreelancehuntScraper
from backend.scrapers.habr_freelance import HabrFreelanceScraper
from backend.scrapers.scheduler import WebScraperScheduler
from backend.scrapers.upwork_rss import UpworkRssScraper
from backend.scrapers.weblancer import WeblancerScraper

__all__ = [
    "BaseScraper",
    "FlRuScraper",
    "FreelancehuntScraper",
    "HabrFreelanceScraper",
    "RawTask",
    "UpworkRssScraper",
    "WebScraperScheduler",
    "WeblancerScraper",
]
