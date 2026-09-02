"""Flight provider adapters package."""

from backend.providers.base import BaseFlightProvider
from backend.providers.aviasales_provider import AviasalesProvider

__all__ = ["BaseFlightProvider", "AviasalesProvider"]
