"""Domain models and schemas for KzFlightSniper."""

from datetime import datetime
from typing import Any, Dict, List, Optional
from pydantic import BaseModel, Field, field_validator


class FlightOffer(BaseModel):
    """Represents a standardized flight offer extracted from any provider."""

    provider: str = Field(default="aviasales", description="Provider identifier (e.g. aviasales, aviata, kaspi)")
    airline: str = Field(..., description="Operating airline name (e.g. Air Astana, FlyArystan, SCAT)")
    flight_number: str = Field(..., description="Flight code (e.g. KC-853, DV-713, IQ-401)")
    origin: str = Field(..., description="3-letter IATA origin airport code")
    destination: str = Field(..., description="3-letter IATA destination airport code")
    departure_time: str = Field(..., description="ISO formatted or human-readable departure time")
    arrival_time: str = Field(..., description="ISO formatted or human-readable arrival time")
    price_kzt: float = Field(..., description="Total ticket price in Kazakhstani Tenge (KZT)")
    transfers_count: int = Field(default=0, description="Number of transfers (0 = direct flight)")
    duration_minutes: Optional[int] = Field(default=None, description="Total journey duration in minutes")
    deep_link: Optional[str] = Field(default=None, description="Direct booking or search URL")

    @property
    def is_direct(self) -> bool:
        """Return True if flight is non-stop direct flight."""
        return self.transfers_count == 0

    @property
    def route(self) -> str:
        """Return route string representation e.g. ALA -> NQZ."""
        return f"{self.origin} -> {self.destination}"

    @property
    def formatted_price(self) -> str:
        """Return formatted price string in KZT."""
        return f"{self.price_kzt:,.0f} ₸".replace(",", " ")

    @field_validator("origin", "destination", mode="before")
    @classmethod
    def normalize_iata(cls, value: str) -> str:
        """Ensure IATA codes are stored uppercase and trimmed."""
        return value.strip().upper() if isinstance(value, str) else value

    @field_validator("flight_number", mode="before")
    @classmethod
    def normalize_flight_number(cls, value: str) -> str:
        """Ensure flight numbers are uppercase."""
        return value.strip().upper() if isinstance(value, str) else value


class ParsedFlightIntent(BaseModel):
    """Structured intent representation extracted by NLP parser from natural language input."""

    origin: str = Field(..., min_length=3, max_length=3, description="3-letter IATA origin code (e.g. ALA)")
    destination: str = Field(..., min_length=3, max_length=3, description="3-letter IATA destination code (e.g. BKK, NQZ)")
    date: str = Field(..., description="Flight date in YYYY-MM-DD format")
    flight_number: Optional[str] = Field(default=None, description="Optional flight number filter (e.g. KC-871)")
    direct_only: bool = Field(default=True, description="Whether only direct flights are targeted")
    target_price: Optional[float] = Field(default=None, description="Optional target maximum price in KZT")
    currency_detected: Optional[str] = Field(default=None, description="Original currency symbol or code detected")
    original_price: Optional[float] = Field(default=None, description="Original numeric price before conversion")
    interval_minutes: int = Field(default=5, ge=1, description="Periodic check frequency in minutes")
    confidence: float = Field(default=1.0, ge=0.0, le=1.0, description="Extraction confidence score")
    raw_explanation: Optional[str] = Field(default=None, description="Human readable explanation or summary")
    is_ambiguous: Optional[bool] = Field(default=False, description="Whether origin or destination matches multiple major airport codes")
    ambiguous_options: Optional[List[Dict[str, str]]] = Field(default_factory=list, description="List of airport options e.g. [{'iata': 'TFU', 'name': 'Чэнду (Тяньфу)'}]")
    ambiguous_target: Optional[str] = Field(default=None, description="'origin' or 'destination'")
    ambiguous_city_name: Optional[str] = Field(default=None, description="Name of the ambiguous city (e.g. 'Чэнду')")

    @property
    def route(self) -> str:
        """Return route string representation e.g. ALA -> NQZ."""
        return f"{self.origin} -> {self.destination}"

    @property
    def formatted_target_price(self) -> str:
        """Return formatted target price string in KZT."""
        if self.target_price is not None:
            return f"{self.target_price:,.0f} ₸".replace(",", " ")
        return "Автоматически"

    @field_validator("origin", "destination", mode="before")
    @classmethod
    def normalize_iata(cls, value: Any) -> str:
        """Normalize 3-letter IATA code to uppercase."""
        if not value:
            return ""
        val_str = str(value).strip().upper()
        cleaned = "".join(c for c in val_str if c.isalpha())
        return cleaned[:3] if len(cleaned) >= 3 else val_str

    @field_validator("flight_number", mode="before")
    @classmethod
    def normalize_flight_number(cls, value: Any) -> Optional[str]:
        """Normalize optional flight number to uppercase."""
        if value is None or str(value).strip().lower() in ("none", "null", ""):
            return None
        return str(value).strip().upper()

    @field_validator("is_ambiguous", mode="before")
    @classmethod
    def normalize_is_ambiguous(cls, value: Any) -> bool:
        """Ensure is_ambiguous defaults to False if None or parses truthy/falsy values."""
        if value is None:
            return False
        if isinstance(value, str):
            return value.strip().lower() in ("true", "1", "yes")
        return bool(value)

    @field_validator("ambiguous_options", mode="before")
    @classmethod
    def normalize_ambiguous_options(cls, value: Optional[List[Dict[str, str]]]) -> List[Dict[str, str]]:
        """Ensure ambiguous options defaults to empty list if None."""
        if value is None:
            return []
        if isinstance(value, list):
            clean_list = []
            for item in value:
                if isinstance(item, dict):
                    clean_list.append({str(k): str(v) for k, v in item.items()})
            return clean_list
        return []

    @field_validator("ambiguous_target", mode="before")
    @classmethod
    def normalize_ambiguous_target(cls, value: Any) -> Optional[str]:
        """Ensure ambiguous_target is valid or None."""
        if value is None or str(value).strip().lower() in ("none", "null", ""):
            return None
        val_str = str(value).strip().lower()
        if "dest" in val_str:
            return "destination"
        if "orig" in val_str:
            return "origin"
        return None

    @field_validator("ambiguous_city_name", mode="before")
    @classmethod
    def normalize_ambiguous_city_name(cls, value: Any) -> Optional[str]:
        """Ensure ambiguous_city_name is string or None."""
        if value is None or str(value).strip().lower() in ("none", "null", ""):
            return None
        return str(value).strip()

    @field_validator("target_price", "original_price", mode="before")
    @classmethod
    def normalize_prices(cls, value: Any) -> Optional[float]:
        """Convert string prices or return float/None."""
        if value is None or value == "" or str(value).strip().lower() in ("null", "none"):
            return None
        try:
            val = float(str(value).replace(" ", "").replace(",", ""))
            return val if val > 0 else None
        except (ValueError, TypeError):
            return None

    @field_validator("direct_only", mode="before")
    @classmethod
    def normalize_direct_only(cls, value: Any) -> bool:
        """Ensure direct_only defaults to True if None."""
        if value is None:
            return True
        if isinstance(value, str):
            return value.strip().lower() in ("true", "1", "yes")
        return bool(value)

    @field_validator("interval_minutes", mode="before")
    @classmethod
    def normalize_interval_minutes(cls, value: Any) -> int:
        """Ensure interval_minutes is integer >= 1."""
        if value is None:
            return 5
        try:
            val = int(value)
            return max(1, val)
        except (ValueError, TypeError):
            return 5


class TaskCreate(BaseModel):
    """Schema for creating a new flight snipe monitoring task."""

    chat_id: int = Field(..., description="Telegram chat / user ID")
    origin: str = Field(..., min_length=3, max_length=3, description="3-letter IATA origin code")
    destination: str = Field(..., min_length=3, max_length=3, description="3-letter IATA destination code")
    date: str = Field(..., description="Flight date in YYYY-MM-DD format")
    target_price: float = Field(..., gt=0, description="Target maximum acceptable price in KZT")
    flight_number: Optional[str] = Field(default=None, description="Optional flight number filter")
    interval_minutes: int = Field(default=5, ge=1, description="Check interval in minutes")
    max_transfers: int = Field(default=0, ge=0, description="Maximum transfers (0 = direct only)")

    @field_validator("origin", "destination", mode="before")
    @classmethod
    def normalize_iata(cls, value: str) -> str:
        """Normalize 3-letter IATA code to uppercase."""
        return value.strip().upper() if isinstance(value, str) else value

    @field_validator("flight_number", mode="before")
    @classmethod
    def normalize_flight_number(cls, value: Optional[str]) -> Optional[str]:
        """Normalize optional flight number to uppercase."""
        return value.strip().upper() if isinstance(value, str) and value.strip() else None


class TaskRead(BaseModel):
    """Schema representing an existing flight snipe monitoring task."""

    id: int = Field(..., description="Unique task ID")
    chat_id: int = Field(..., description="Telegram chat / user ID")
    origin: str = Field(..., description="3-letter IATA origin code")
    destination: str = Field(..., description="3-letter IATA destination code")
    date: str = Field(..., description="Flight date in YYYY-MM-DD format")
    flight_number: Optional[str] = Field(default=None, description="Optional flight number filter")
    target_price: float = Field(..., description="Target maximum acceptable price in KZT")
    is_active: int = Field(default=1, description="Active status flag (1 = active, 0 = inactive)")
    created_at: Optional[str] = Field(default=None, description="Task creation timestamp")
    last_checked_at: Optional[str] = Field(default=None, description="Timestamp of last check")
    last_price: Optional[float] = Field(default=None, description="Lowest price observed on last check")
    interval_minutes: int = Field(default=5, description="Check interval in minutes")
    max_transfers: int = Field(default=0, description="Maximum transfers allowed")


class AlertRead(BaseModel):
    """Schema representing a logged price alert history entry."""

    id: int = Field(..., description="Unique alert ID")
    task_id: int = Field(..., description="Associated task ID")
    flight_number: str = Field(..., description="Flight number triggered")
    price: float = Field(..., description="Alerted ticket price in KZT")
    alert_time: Optional[str] = Field(default=None, description="Alert dispatch timestamp")


class HealthResponse(BaseModel):
    """Health check response schema."""

    status: str = Field(default="ok", description="Service health status")
    database: str = Field(default="connected", description="Database connection status")
    active_tasks: int = Field(..., description="Total count of active monitoring tasks")
    version: str = Field(default="1.0.0", description="API version")
