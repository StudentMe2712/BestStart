"""Database module for OmniLeadSniper.

Exports Database client and ProcessedLead ORM model.
"""

from backend.db.database import Database
from backend.db.models import ProcessedLead

__all__ = ["Database", "ProcessedLead"]
