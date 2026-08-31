"""Domain models and schemas for KzFlightSniper."""

from datetime import datetime
from typing import Optional
from pydantic import BaseModel, Field, field_validator


class FlightOffer(BaseModel):
    """Represents a standardized flight offer extracted from any provider."""

    provider: str = Field(default="aviata", description="Provider identifier (e.g. aviata, kaspi)")
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
