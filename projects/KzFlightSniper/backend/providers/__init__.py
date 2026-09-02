"""Flight provider adapters package."""

from backend.providers.base import BaseFlightProvider
from backend.providers.aviasales_provider import AviasalesProvider

# Backward-compatible alias for deprecated provider
AviataProvider = AviasalesProvider

__all__ = ["BaseFlightProvider", "AviasalesProvider", "AviataProvider"]
