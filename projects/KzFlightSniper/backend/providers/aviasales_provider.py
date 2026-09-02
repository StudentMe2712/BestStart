"""Aviasales / Travelpayouts flight provider using async HTTP client.

Interacts with the Travelpayouts Aviasales v3 API (`/aviasales/v3/prices_for_dates`),
parses response payloads into standardized FlightOffer instances, maps airline
IATA codes, calculates departure/arrival times, and formats deep booking links.
"""

from datetime import datetime, timedelta
import logging
import os
import re
from typing import Any, Dict, List, Optional, Tuple

import httpx

from backend.core.models import FlightOffer
from backend.providers.base import BaseFlightProvider

logger = logging.getLogger("kzflight_sniper.providers.aviasales")

# Map of common airline IATA 2-letter codes to their human-readable airline names
AIRLINE_IATA_MAP: Dict[str, str] = {
    "KC": "Air Astana",
    "IQ": "Qazaq Air",
    "FS": "FlyArystan",
    "DV": "SCAT Airlines",
    "FZ": "Flydubai",
    "EK": "Emirates",
    "TK": "Turkish Airlines",
    "PC": "Pegasus Airlines",
    "HY": "Uzbekistan Airways",
    "QR": "Qatar Airways",
    "CZ": "China Southern Airlines",
    "CA": "Air China",
    "W6": "Wizz Air",
    "G9": "Air Arabia",
    "J2": "Azerbaijan Airlines",
    "XJ": "AirAsia X",
    "SU": "Aeroflot",
    "S7": "S7 Airlines",
    "LH": "Lufthansa",
    "EY": "Etihad Airways",
    "LO": "LOT Polish Airlines",
    "MS": "EgyptAir",
    "SV": "Saudia",
    "RJ": "Royal Jordanian",
    "TG": "Thai Airways",
    "SQ": "Singapore Airlines",
    "MH": "Malaysia Airlines",
    "VN": "Vietnam Airlines",
    "SZ": "Somon Air",
    "7J": "Tajik Air",
    "B2": "Belavia",
    "VF": "AJet",
    "XC": "Corendon Airlines",
    "PS": "Ukraine International Airlines",
    "DP": "Pobeda",
    "UT": "Utair",
    "WZ": "Red Wings",
    "A4": "Azimuth",
}

# Map of metropolitan airport alternatives for multi-airport cities
METRO_AIRPORT_ALTERNATIVES: Dict[str, List[str]] = {
    "CTU": ["TFU"],
    "TFU": ["CTU"],
    "IST": ["SAW"],
    "SAW": ["IST"],
    "DXB": ["DWC"],
    "DWC": ["DXB"],
    "BKK": ["DMK"],
    "DMK": ["BKK"],
    "PEK": ["PKX"],
    "PKX": ["PEK"],
    "MOW": ["SVO", "DME", "VKO"],
    "TYO": ["HND", "NRT"],
    "LON": ["LHR", "LGW", "STN"],
}



def _normalize_flight_number(airline_code: str, flight_number: Any) -> str:
    """Normalize flight number into standardized format (e.g. 'FS-7051').

    Args:
        airline_code: 2-letter IATA airline code (e.g. 'FS', 'KC').
        flight_number: Raw flight number string or integer (e.g. '7051', 'FS7051', 'FS-7051').

    Returns:
        Standardized flight code string (e.g. 'FS-7051').
    """
    raw_num = str(flight_number if flight_number is not None else "").strip().upper()
    code = str(airline_code or "").strip().upper()

    if not raw_num and not code:
        return "Aviasales Flight"
    if not raw_num:
        return code

    # If already formatted with hyphen e.g. "FS-7051", return as-is
    if "-" in raw_num:
        return raw_num

    # If raw_num starts with airline code (e.g. "FS7051" with code "FS")
    if code and raw_num.startswith(code):
        suffix = raw_num[len(code):].lstrip("- ")
        if suffix:
            return f"{code}-{suffix}"
        return code

    # If raw_num is numeric (e.g. "7051" or 7051) and code is available
    if raw_num.isdigit():
        if code and len(code) <= 3:
            return f"{code}-{raw_num}"
        return raw_num

    # If raw_num matches 2 letters + digits without hyphen (e.g. "KC853")
    match = re.match(r"^([A-Z]{2})(\d+)$", raw_num)
    if match:
        return f"{match.group(1)}-{match.group(2)}"

    return raw_num


def _parse_time_and_arrival(
    departure_raw: Any,
    arrival_raw: Any,
    duration_minutes: Optional[int],
) -> Tuple[str, str]:
    """Parse departure time and compute or parse arrival time.

    Args:
        departure_raw: Raw ISO timestamp or time string for departure.
        arrival_raw: Optional raw arrival timestamp or time string.
        duration_minutes: Flight duration in minutes if available.

    Returns:
        Tuple of (departure_time_str, arrival_time_str) in HH:MM format when available.
    """
    dep_time_str = "Scheduled"
    arr_time_str = "Scheduled"
    dep_dt: Optional[datetime] = None

    if departure_raw:
        if isinstance(departure_raw, dict):
            departure_raw = departure_raw.get("time", departure_raw.get("value", str(departure_raw)))
        dep_str = str(departure_raw).strip()
        # Handle ISO timestamp (e.g. 2026-10-15T06:45:00+06:00 or 2026-10-15T06:45:00Z)
        try:
            dep_dt = datetime.fromisoformat(dep_str.replace("Z", "+00:00"))
            dep_time_str = dep_dt.strftime("%H:%M")
        except Exception:
            for fmt in ("%Y-%m-%d %H:%M:%S", "%Y-%m-%d %H:%M", "%H:%M"):
                try:
                    dep_dt = datetime.strptime(dep_str, fmt)
                    dep_time_str = dep_dt.strftime("%H:%M")
                    break
                except Exception:
                    pass
            if dep_time_str == "Scheduled":
                dep_time_str = dep_str

    if arrival_raw:
        if isinstance(arrival_raw, dict):
            arrival_raw = arrival_raw.get("time", arrival_raw.get("value", str(arrival_raw)))
        arr_str = str(arrival_raw).strip()
        try:
            arr_dt = datetime.fromisoformat(arr_str.replace("Z", "+00:00"))
            arr_time_str = arr_dt.strftime("%H:%M")
        except Exception:
            for fmt in ("%Y-%m-%d %H:%M:%S", "%Y-%m-%d %H:%M", "%H:%M"):
                try:
                    arr_dt = datetime.strptime(arr_str, fmt)
                    arr_time_str = arr_dt.strftime("%H:%M")
                    break
                except Exception:
                    pass
            if arr_time_str == "Scheduled":
                arr_time_str = arr_str
    elif dep_dt is not None and duration_minutes is not None and duration_minutes > 0:
        arr_dt = dep_dt + timedelta(minutes=duration_minutes)
        arr_time_str = arr_dt.strftime("%H:%M")

    return dep_time_str, arr_time_str


def _build_deep_link(
    base_url: str,
    link_raw: Optional[str],
    origin: str,
    destination: str,
    date_str: str = "",
) -> Optional[str]:
    """Construct full Aviasales booking or search deep link.

    Args:
        base_url: Base domain URL (e.g. 'https://www.aviasales.kz').
        link_raw: Relative or absolute link provided by API payload.
        origin: 3-letter IATA origin airport.
        destination: 3-letter IATA destination airport.
        date_str: Departure date string for fallback link construction.

    Returns:
        Full booking/search URL.
    """
    clean_base = base_url.rstrip("/")
    if link_raw and str(link_raw).strip():
        link_str = str(link_raw).strip()
        if link_str.startswith("http://") or link_str.startswith("https://"):
            return link_str
        return f"{clean_base}/{link_str.lstrip('/')}"

    # Fallback search deep link
    if date_str:
        try:
            # Try to extract date component from YYYY-MM-DD or ISO timestamp
            iso_match = re.match(r"^(\d{4})-(\d{2})-(\d{2})", date_str)
            if iso_match:
                day = iso_match.group(3)
                month = iso_match.group(2)
                return f"{clean_base}/search/{origin.upper()}{day}{month}{destination.upper()}1"
        except Exception:
            pass

    return f"{clean_base}/search/{origin.upper()}{destination.upper()}1"


class AviasalesProvider(BaseFlightProvider):
    """Flight search provider adapter for Travelpayouts / Aviasales v3 API."""

    DEFAULT_API_URL: str = "https://api.travelpayouts.com/aviasales/v3/prices_for_dates"
    DEFAULT_TOKEN: str = "321d6a221f8926b5ec41ae89a3b2ae7b"
    DEFAULT_BASE_URL: str = "https://www.aviasales.kz"

    def __init__(
        self,
        token: Optional[str] = None,
        api_url: Optional[str] = None,
        base_url: Optional[str] = None,
        timeout: float = 15.0,
        **kwargs: Any,
    ) -> None:
        """Initialize Aviasales provider adapter.

        Args:
            token: Travelpayouts API token (defaults to TRAVELPAYOUTS_TOKEN/AVIASALES_TOKEN env var or default token).
            api_url: Travelpayouts v3 prices_for_dates endpoint URL.
            base_url: Aviasales portal base URL for deep link formatting.
            timeout: HTTP request timeout in seconds.
            **kwargs: Extra parameters accepted for backward compatibility.
        """
        try:
            from backend.core.config import get_settings
            settings = get_settings()
            cfg_token = getattr(settings, "TRAVELPAYOUTS_TOKEN", self.DEFAULT_TOKEN)
            cfg_api_url = getattr(settings, "AVIASALES_API_URL", self.DEFAULT_API_URL)
            cfg_base_url = getattr(settings, "AVIASALES_BASE_URL", self.DEFAULT_BASE_URL)
        except Exception:
            cfg_token = self.DEFAULT_TOKEN
            cfg_api_url = self.DEFAULT_API_URL
            cfg_base_url = self.DEFAULT_BASE_URL

        self.token = (
            token
            or os.getenv("TRAVELPAYOUTS_TOKEN")
            or os.getenv("AVIASALES_TOKEN")
            or cfg_token
            or self.DEFAULT_TOKEN
        ).strip()
        self.api_url = (api_url or os.getenv("AVIASALES_API_URL") or cfg_api_url or self.DEFAULT_API_URL).strip()
        self.base_url = (base_url or os.getenv("AVIASALES_BASE_URL") or cfg_base_url or self.DEFAULT_BASE_URL).strip()
        self.timeout = timeout

    @property
    def provider_name(self) -> str:
        """Return provider identifier name."""
        return "aviasales"

    @classmethod
    def parse_aviasales_json(
        cls,
        raw_data: Any,
        origin: str,
        destination: str,
        base_url: str = "https://www.aviasales.kz",
    ) -> List[FlightOffer]:
        """Parse raw Travelpayouts / Aviasales API JSON response into FlightOffer list.

        Args:
            raw_data: Raw JSON response object (dict or list).
            origin: 3-letter IATA origin airport code.
            destination: 3-letter IATA destination airport code.
            base_url: Base portal URL for building deep booking links.

        Returns:
            List of parsed and standardized FlightOffer instances.
        """
        offers: List[FlightOffer] = []

        if isinstance(raw_data, list):
            items = raw_data
        elif isinstance(raw_data, dict):
            if "data" in raw_data:
                data_field = raw_data["data"]
                if isinstance(data_field, list):
                    items = data_field
                elif isinstance(data_field, dict):
                    items = []
                    for subkey in ["offers", "flights", "variants", "results", "items"]:
                        if subkey in data_field and isinstance(data_field[subkey], list):
                            items = data_field[subkey]
                            break
                    if not items:
                        items = list(data_field.values())
                else:
                    items = []
            elif "results" in raw_data and isinstance(raw_data["results"], list):
                items = raw_data["results"]
            elif "offers" in raw_data and isinstance(raw_data["offers"], list):
                items = raw_data["offers"]
            elif "flights" in raw_data and isinstance(raw_data["flights"], list):
                items = raw_data["flights"]
            elif "variants" in raw_data and isinstance(raw_data["variants"], list):
                items = raw_data["variants"]
            elif "prices" in raw_data and isinstance(raw_data["prices"], list):
                items = raw_data["prices"]
            else:
                items = []
        else:
            return offers

        clean_origin = origin.strip().upper()
        clean_dest = destination.strip().upper()

        for item in items:
            if not isinstance(item, dict):
                continue

            try:
                # Extract numeric price in KZT
                raw_price = item.get("price") or item.get("value") or item.get("total_price") or item.get("amount") or 0.0
                if isinstance(raw_price, dict):
                    price = float(raw_price.get("amount", raw_price.get("value", raw_price.get("total", 0.0))))
                elif isinstance(raw_price, str):
                    cleaned = "".join(c for c in raw_price if c.isdigit() or c == '.')
                    price = float(cleaned) if cleaned else 0.0
                else:
                    try:
                        price = float(raw_price)
                    except (ValueError, TypeError):
                        price = 0.0

                if price <= 0:
                    continue

                # Extract airline and resolve full name
                raw_airline = (
                    item.get("airline")
                    or item.get("airline_name")
                    or item.get("carrier")
                    or item.get("carrier_name")
                    or item.get("company")
                    or item.get("airline_code")
                    or ""
                )
                if isinstance(raw_airline, dict):
                    raw_airline = raw_airline.get("name", raw_airline.get("title", raw_airline.get("code", "")))

                airline_str = str(raw_airline).strip()
                airline_code = airline_str.upper() if len(airline_str) <= 3 else ""

                if airline_code in AIRLINE_IATA_MAP:
                    airline_name = AIRLINE_IATA_MAP[airline_code]
                elif airline_str:
                    airline_name = airline_str
                else:
                    airline_name = "Unknown Airline"

                # Extract & normalize flight number
                flight_raw = item.get("flight_number") or item.get("flight_no") or item.get("number") or item.get("code")
                flight_number = _normalize_flight_number(airline_code, flight_raw)

                # Inspect nested segments/legs if needed
                raw_segments = item.get("segments", item.get("legs", []))
                if isinstance(raw_segments, list) and raw_segments:
                    seg_nums = []
                    for seg in raw_segments:
                        if isinstance(seg, dict):
                            num = seg.get("flight_number", seg.get("flight_no", seg.get("number", "")))
                            if num:
                                seg_nums.append(str(num).strip().upper())
                    if (not flight_raw or flight_number == "Aviasales Flight") and seg_nums:
                        flight_number = " / ".join(seg_nums)
                    if airline_name == "Unknown Airline":
                        first_seg = raw_segments[0]
                        if isinstance(first_seg, dict):
                            first_carrier = first_seg.get("airline_name") or first_seg.get("carrier_name") or first_seg.get("airline")
                            if first_carrier:
                                airline_name = str(first_carrier).strip()

                # Origin and Destination codes
                item_origin = str(item.get("origin") or item.get("origin_airport") or clean_origin).strip().upper()
                item_dest = str(item.get("destination") or item.get("destination_airport") or clean_dest).strip().upper()

                # Transfers count
                transfers_raw = item.get("transfers", item.get("transfers_count", item.get("stops", 0)))
                try:
                    transfers_count = int(transfers_raw)
                except (ValueError, TypeError):
                    transfers_count = 0

                # Flight duration in minutes
                duration_raw = item.get("duration", item.get("duration_to", item.get("duration_minutes")))
                duration_minutes: Optional[int] = None
                if isinstance(duration_raw, (int, float)) and duration_raw > 0:
                    duration_minutes = int(duration_raw)

                # Departure and Arrival times
                dep_raw = item.get("departure_at") or item.get("departure_time") or item.get("departure")
                arr_raw = item.get("arrival_at") or item.get("arrival_time") or item.get("arrival")
                dep_time, arr_time = _parse_time_and_arrival(dep_raw, arr_raw, duration_minutes)

                # Construct booking deep link
                link_field = item.get("link") or item.get("deep_link") or item.get("booking_url")
                deep_link = _build_deep_link(base_url, link_field, item_origin, item_dest, str(dep_raw or ""))

                offer = FlightOffer(
                    provider="aviasales",
                    airline=airline_name,
                    flight_number=flight_number,
                    origin=item_origin,
                    destination=item_dest,
                    departure_time=dep_time,
                    arrival_time=arr_time,
                    price_kzt=price,
                    transfers_count=transfers_count,
                    duration_minutes=duration_minutes,
                    deep_link=deep_link,
                )

                # Deduplicate offers with identical flight number, departure time, and price
                if not any(
                    o.flight_number == offer.flight_number
                    and o.departure_time == offer.departure_time
                    and o.price_kzt == offer.price_kzt
                    for o in offers
                ):
                    offers.append(offer)

            except Exception as err:
                logger.debug("Failed parsing individual Aviasales flight item: %s", err)

        return offers

    @classmethod
    def parse_aviata_json(
        cls,
        raw_data: Any,
        origin: str,
        destination: str,
        search_url: str = "https://www.aviasales.kz",
    ) -> List[FlightOffer]:
        """Backward-compatible alias for parse_aviasales_json."""
        return cls.parse_aviasales_json(raw_data, origin, destination, base_url=search_url)

    async def search_flights(
        self,
        origin: str,
        destination: str,
        date: str,
        max_transfers: int = 0,
        direct_only: bool = False,
    ) -> List[FlightOffer]:
        """Search flights via Travelpayouts / Aviasales v3 API.

        Args:
            origin: 3-letter IATA code of origin airport (e.g. 'ALA').
            destination: 3-letter IATA code of destination airport (e.g. 'NQZ').
            date: Departure date in YYYY-MM-DD or YYYY-MM format.
            max_transfers: Maximum number of transfers allowed (0 for direct only).
            direct_only: Whether to restrict search to direct flights only.

        Returns:
            List of matching FlightOffer instances sorted by price.
        """
        clean_origin = origin.strip().upper()
        clean_dest = destination.strip().upper()
        clean_date = date.strip()

        logger.info(
            "Querying Aviasales/Travelpayouts Flight Data Cache API for route %s -> %s on %s (direct_only=%s, max_transfers=%d)",
            clean_origin,
            clean_dest,
            clean_date,
            direct_only,
            max_transfers,
        )

        params: Dict[str, Any] = {
            "origin": clean_origin,
            "destination": clean_dest,
            "departure_at": clean_date,
            "currency": "kzt",
            "unique": "false",
            "sorting": "price",
            "token": self.token,
        }

        if direct_only or max_transfers == 0:
            params["direct"] = "true"
        elif not direct_only and max_transfers > 0:
            params["direct"] = "false"

        headers: Dict[str, str] = {
            "User-Agent": (
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                "(KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
            ),
            "Accept": "application/json",
            "Accept-Encoding": "gzip, deflate",
            "x-access-token": self.token,
        }

        offers: List[FlightOffer] = []
        try:
            async with httpx.AsyncClient(timeout=self.timeout, follow_redirects=True) as client:
                response = await client.get(self.api_url, params=params, headers=headers)

                if response.status_code == 200:
                    data = response.json()
                    raw_offers = self.parse_aviasales_json(
                        raw_data=data,
                        origin=clean_origin,
                        destination=clean_dest,
                        base_url=self.base_url,
                    )

                    # Filter by direct_only and max_transfers
                    for offer in raw_offers:
                        if (direct_only or max_transfers == 0) and offer.transfers_count > 0:
                            continue
                        if max_transfers > 0 and offer.transfers_count > max_transfers:
                            continue
                        offers.append(offer)
                else:
                    logger.warning(
                        "Aviasales API returned HTTP %d for %s->%s on %s: %s",
                        response.status_code,
                        clean_origin,
                        clean_dest,
                        clean_date,
                        response.text[:200],
                    )

                # Metro IATA Airport Resolution fallback if 0 offers found
                if not offers and clean_dest in METRO_AIRPORT_ALTERNATIVES:
                    for alt_code in METRO_AIRPORT_ALTERNATIVES[clean_dest]:
                        alt_params = dict(params)
                        alt_params["destination"] = alt_code
                        try:
                            alt_response = await client.get(self.api_url, params=alt_params, headers=headers)
                            if alt_response.status_code == 200:
                                alt_data = alt_response.json()
                                raw_alt_offers = self.parse_aviasales_json(
                                    raw_data=alt_data,
                                    origin=clean_origin,
                                    destination=alt_code,
                                    base_url=self.base_url,
                                )
                                alt_offers: List[FlightOffer] = []
                                for offer in raw_alt_offers:
                                    if (direct_only or max_transfers == 0) and offer.transfers_count > 0:
                                        continue
                                    if max_transfers > 0 and offer.transfers_count > max_transfers:
                                        continue
                                    alt_offers.append(offer)

                                if alt_offers:
                                    logger.info(
                                        "Found %d flight(s) via alternative metro airport %s for %s",
                                        len(alt_offers),
                                        alt_code,
                                        clean_dest,
                                    )
                                    offers.extend(alt_offers)
                        except Exception as alt_err:
                            logger.debug(
                                "Alternative metro airport query failed for %s->%s: %s",
                                clean_origin,
                                alt_code,
                                alt_err,
                            )

                # Fallback Month Cache Lookup if 0 offers found
                if not offers:
                    month_str = clean_date[:7] if len(clean_date) >= 7 else clean_date
                    fallback_params: Dict[str, Any] = {
                        "origin": clean_origin,
                        "destination": clean_dest,
                        "departure_at": month_str,
                        "currency": "kzt",
                        "unique": "false",
                        "sorting": "price",
                        "token": self.token,
                    }
                    try:
                        fallback_response = await client.get(
                            self.api_url,
                            params=fallback_params,
                            headers=headers,
                        )
                        if fallback_response.status_code == 200:
                            fallback_data = fallback_response.json()
                            fallback_offers = self.parse_aviasales_json(
                                raw_data=fallback_data,
                                origin=clean_origin,
                                destination=clean_dest,
                                base_url=self.base_url,
                            )
                            if fallback_offers:
                                min_price = min(o.price_kzt for o in fallback_offers)
                                logger.info(
                                    "ℹ️ [Cache Fallback] Route %s -> %s: Found %d cached flight offer(s) across month %s (e.g. min price %.0f KZT). Exact date %s has no matching %s offers.",
                                    clean_origin,
                                    clean_dest,
                                    len(fallback_offers),
                                    month_str,
                                    min_price,
                                    clean_date,
                                    "direct" if direct_only else "",
                                )
                            else:
                                logger.info(
                                    "ℹ️ [Cache Fallback] Route %s -> %s: Cache is completely empty for the entire month %s.",
                                    clean_origin,
                                    clean_dest,
                                    month_str,
                                )
                        else:
                            logger.info(
                                "ℹ️ [Cache Fallback] Route %s -> %s: Cache is completely empty for the entire month %s.",
                                clean_origin,
                                clean_dest,
                                month_str,
                            )
                    except Exception as fb_err:
                        logger.debug("Fallback month cache query failed for %s->%s: %s", clean_origin, clean_dest, fb_err)

                # Sort offers by price ascending
                offers.sort(key=lambda o: o.price_kzt)
                logger.info(
                    "Aviasales search found %d qualifying offer(s) for %s->%s",
                    len(offers),
                    clean_origin,
                    clean_dest,
                )
                return offers

        except httpx.TimeoutException as exc:
            logger.error(
                "Aviasales API request timed out after %.1fs for %s->%s on %s: %s",
                self.timeout,
                clean_origin,
                clean_dest,
                clean_date,
                exc,
            )
            return []
        except httpx.HTTPError as exc:
            logger.error(
                "Aviasales API HTTP error for %s->%s on %s: %s",
                clean_origin,
                clean_dest,
                clean_date,
                exc,
            )
            return []
        except Exception as exc:
            logger.exception(
                "Unexpected error during Aviasales flight search %s->%s: %s",
                clean_origin,
                clean_dest,
                exc,
            )
            return []

    async def search(
        self,
        origin: str,
        destination: str,
        date: str,
        direct_only: bool = True,
        flight_number: Optional[str] = None,
        max_transfers: Optional[int] = None,
    ) -> List[FlightOffer]:
        """Convenience search method with multi-parameter filtering for bot handlers and workers.

        Args:
            origin: 3-letter IATA code of origin airport.
            destination: 3-letter IATA code of destination airport.
            date: Departure date (YYYY-MM-DD).
            direct_only: If True, only non-stop flights are returned.
            flight_number: Optional flight number filter substring (e.g. 'KC-853', '7051').
            max_transfers: Explicit max transfers allowed (overrides direct_only if provided).

        Returns:
            Filtered list of FlightOffer instances.
        """
        effective_max_transfers = (
            max_transfers if max_transfers is not None else (0 if direct_only else 10)
        )

        offers = await self.search_flights(
            origin=origin,
            destination=destination,
            date=date,
            max_transfers=effective_max_transfers,
            direct_only=direct_only,
        )

        if direct_only:
            offers = [o for o in offers if o.transfers_count == 0]

        if flight_number and flight_number.strip():
            fn_filter = flight_number.strip().upper()
            fn_clean = fn_filter.replace("-", "").replace(" ", "")

            filtered = []
            for o in offers:
                o_fn = o.flight_number.upper()
                o_fn_clean = o_fn.replace("-", "").replace(" ", "")
                if (
                    fn_filter in o_fn
                    or o_fn in fn_filter
                    or fn_clean in o_fn_clean
                    or o_fn_clean in fn_clean
                ):
                    filtered.append(o)
            offers = filtered

        return offers
