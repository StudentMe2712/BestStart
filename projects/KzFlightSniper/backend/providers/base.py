"""Base flight provider interface for KzFlightSniper adapter pattern."""

from abc import ABC, abstractmethod
from typing import List, Optional
from backend.core.models import FlightOffer


class BaseFlightProvider(ABC):
    """Abstract base class for all flight aggregator and airline provider adapters."""

    @property
    @abstractmethod
    def provider_name(self) -> str:
        """Return the unique provider name identifier (e.g. 'aviata', 'kaspi')."""
        pass

    @abstractmethod
    async def search_flights(
        self,
        origin: str,
        destination: str,
        date: str,
        max_transfers: int = 0,
    ) -> List[FlightOffer]:
        """Search flights for route and date, returning standardized flight offers.

        Args:
            origin: 3-letter IATA code of origin airport (e.g. 'ALA').
            destination: 3-letter IATA code of destination airport (e.g. 'NQZ').
            date: Departure date in YYYY-MM-DD format.
            max_transfers: Maximum number of transfers allowed (0 for direct only).

        Returns:
            List of standardized FlightOffer instances.
        """
        pass
