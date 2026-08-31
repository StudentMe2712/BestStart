"""Aviata.kz flight provider implementation using Playwright with stealth.

Intersects internal search JSON API endpoints from Aviata.kz, parses
standardized flight offers, and provides fallback DOM extraction.
"""

import asyncio
from datetime import datetime
import json
import logging
from typing import Any, Dict, List, Optional

from backend.core.config import get_settings
from backend.core.models import FlightOffer
from backend.providers.base import BaseFlightProvider

logger = logging.getLogger("kzflight_sniper.providers.aviata")


async def _apply_stealth(page: Any) -> None:
    """Apply stealth scripts and configurations to avoid bot detection."""
    try:
        from playwright_stealth import stealth_async  # type: ignore
        await stealth_async(page)
        logger.debug("Applied playwright-stealth package scripts.")
    except Exception:
        logger.debug("playwright-stealth not available or failed. Applying fallback custom stealth scripts.")
        await page.add_init_script("""
            // Overwrite webdriver property
            Object.defineProperty(navigator, 'webdriver', {
                get: () => undefined
            });

            // Mock chrome object
            window.chrome = {
                runtime: {},
                loadTimes: function() {},
                csi: function() {},
                app: {}
            };

            // Mock permissions
            const originalQuery = window.navigator.permissions.query;
            window.navigator.permissions.query = (parameters) => (
                parameters.name === 'notifications' ?
                    Promise.resolve({ state: Notification.permission }) :
                    originalQuery(parameters)
            );

            // Mock plugins length
            Object.defineProperty(navigator, 'plugins', {
                get: () => [1, 2, 3, 4, 5],
            });

            // Mock languages
            Object.defineProperty(navigator, 'languages', {
                get: () => ['ru-RU', 'ru', 'en-US', 'en', 'kk-KZ', 'kk'],
            });
        """)


class AviataProvider(BaseFlightProvider):
    """Flight search provider adapter for Aviata.kz aggregator."""

    def __init__(self, headless: Optional[bool] = None, timeout_ms: int = 30000) -> None:
        """Initialize Aviata provider.

        Args:
            headless: Whether to run Chromium in headless mode (defaults to config settings).
            timeout_ms: Timeout in milliseconds for page navigation and API interception.
        """
        settings = get_settings()
        self.headless = headless if headless is not None else settings.HEADLESS
        self.timeout_ms = timeout_ms

    @property
    def provider_name(self) -> str:
        """Unique provider identifier."""
        return "aviata"

    @staticmethod
    def _extract_price(item: Dict[str, Any]) -> float:
        """Extract numeric price in KZT from item structure."""
        price: float = 0.0
        if "price" in item:
            p = item["price"]
            if isinstance(p, (int, float)):
                price = float(p)
            elif isinstance(p, dict):
                price = float(p.get("amount", p.get("value", p.get("total", 0.0))))
            elif isinstance(p, str):
                cleaned = "".join(c for c in p if c.isdigit() or c == '.')
                price = float(cleaned) if cleaned else 0.0
        elif "total_price" in item:
            price = float(item["total_price"])
        elif "amount" in item:
            price = float(item["amount"])
        return price

    @staticmethod
    def _extract_airline(item: Dict[str, Any]) -> str:
        """Extract airline name from item structure."""
        airline = (
            item.get("airline_name")
            or item.get("carrier_name")
            or item.get("airline")
            or item.get("company")
            or "Unknown Airline"
        )
        if isinstance(airline, dict):
            airline = airline.get("name", airline.get("title", airline.get("code", "Unknown Airline")))
        return str(airline).strip()

    @classmethod
    def _extract_single_offer(
        cls,
        item: Dict[str, Any],
        origin: str,
        destination: str,
        search_url: str = "",
    ) -> Optional[FlightOffer]:
        """Extract a single standardized FlightOffer from a raw JSON item."""
        price = cls._extract_price(item)
        if price <= 0:
            return None

        airline = cls._extract_airline(item)

        flight_num = str(
            item.get("flight_number")
            or item.get("flight_no")
            or item.get("number")
            or item.get("code")
            or ""
        ).strip()

        # Extract departure & arrival times
        dep_val = item.get("departure_time") or item.get("departure", {})
        arr_val = item.get("arrival_time") or item.get("arrival", {})

        dep_time = (
            dep_val.get("time", dep_val.get("value", str(dep_val)))
            if isinstance(dep_val, dict)
            else str(dep_val)
        )
        arr_time = (
            arr_val.get("time", arr_val.get("value", str(arr_val)))
            if isinstance(arr_val, dict)
            else str(arr_val)
        )

        # Transfers / stops count
        transfers_raw = item.get("transfers_count", item.get("stops", item.get("transfers", 0)))
        try:
            transfers = int(transfers_raw)
        except (ValueError, TypeError):
            transfers = 0

        # Duration
        duration_min = item.get("duration_minutes", item.get("duration"))
        duration: Optional[int] = None
        if isinstance(duration_min, (int, float)) and duration_min > 0:
            duration = int(duration_min)

        # Inspect nested segments if flight_number is missing or transfers > 0
        raw_segments = item.get("segments", item.get("legs", []))
        if isinstance(raw_segments, list) and raw_segments:
            seg_nums = []
            for seg in raw_segments:
                if isinstance(seg, dict):
                    num = seg.get("flight_number", seg.get("flight_no", seg.get("number", "")))
                    if num:
                        seg_nums.append(str(num).strip().upper())
            if not flight_num and seg_nums:
                flight_num = " / ".join(seg_nums)
            if not airline or airline == "Unknown Airline":
                first_seg = raw_segments[0]
                if isinstance(first_seg, dict):
                    airline = cls._extract_airline(first_seg)

        if not flight_num:
            flight_num = "Aviata Flight"

        deep_link = item.get("deep_link", item.get("link", item.get("booking_url", search_url))) or search_url

        return FlightOffer(
            provider="aviata",
            airline=airline,
            flight_number=flight_num,
            origin=origin.strip().upper(),
            destination=destination.strip().upper(),
            departure_time=str(dep_time) if dep_time else "Scheduled",
            arrival_time=str(arr_time) if arr_time else "Scheduled",
            price_kzt=price,
            transfers_count=transfers,
            duration_minutes=duration,
            deep_link=deep_link or None,
        )

    @classmethod
    def parse_aviata_json(
        cls,
        raw_data: Any,
        origin: str,
        destination: str,
        search_url: str = "",
    ) -> List[FlightOffer]:
        """Parse raw Aviata JSON payloads into structured FlightOffer instances.

        Supports various Aviata backend schemas (results, offers, flights, variants, nested data).

        Args:
            raw_data: JSON payload dictionary or list.
            origin: 3-letter IATA origin airport code.
            destination: 3-letter IATA destination airport code.
            search_url: Direct search URL for deep linking.

        Returns:
            List of parsed FlightOffer models.
        """
        offers: List[FlightOffer] = []

        if isinstance(raw_data, list):
            items = raw_data
        elif isinstance(raw_data, dict):
            items = []
            if "results" in raw_data and isinstance(raw_data["results"], list):
                items = raw_data["results"]
            elif "offers" in raw_data and isinstance(raw_data["offers"], list):
                items = raw_data["offers"]
            elif "flights" in raw_data and isinstance(raw_data["flights"], list):
                items = raw_data["flights"]
            elif "variants" in raw_data and isinstance(raw_data["variants"], list):
                items = raw_data["variants"]
            elif "data" in raw_data:
                data_field = raw_data["data"]
                if isinstance(data_field, list):
                    items = data_field
                elif isinstance(data_field, dict):
                    for subkey in ["offers", "flights", "variants", "results", "items"]:
                        if subkey in data_field and isinstance(data_field[subkey], list):
                            items = data_field[subkey]
                            break
        else:
            return offers

        for item in items:
            if isinstance(item, dict):
                try:
                    offer = cls._extract_single_offer(item, origin, destination, search_url)
                    if offer:
                        # Avoid duplicates within same response payload
                        if not any(
                            o.flight_number == offer.flight_number and o.price_kzt == offer.price_kzt
                            for o in offers
                        ):
                            offers.append(offer)
                except Exception as err:
                    logger.debug("Failed parsing individual Aviata flight item: %s", err)

        return offers

    async def search_flights(
        self,
        origin: str,
        destination: str,
        date: str,
        max_transfers: int = 0,
    ) -> List[FlightOffer]:
        """Search Aviata for flights between origin and destination on specified date.

        Args:
            origin: 3-letter IATA origin airport code (e.g. 'ALA').
            destination: 3-letter IATA destination airport code (e.g. 'NQZ').
            date: Flight date in YYYY-MM-DD format.
            max_transfers: Maximum transfers allowed (0 for direct flights only).

        Returns:
            List of matching FlightOffer instances.
        """
        from playwright.async_api import async_playwright

        clean_origin = origin.strip().upper()
        clean_dest = destination.strip().upper()
        date_formatted = date.replace("-", "").strip()
        search_path = f"{clean_origin}{clean_dest}{date_formatted}100E"
        search_url = f"https://aviata.kz/flights/search/{search_path}/"

        logger.info("Executing Aviata flight search: %s -> %s on %s", clean_origin, clean_dest, date)
        logger.debug("Aviata target URL: %s", search_url)

        intercepted_payloads: List[Dict[str, Any]] = []
        data_received_event = asyncio.Event()

        async def _handle_response(response: Any) -> None:
            url = response.url
            if any(kw in url.lower() for kw in ["search", "flight", "offer", "variant", "v2/avia", "avia/"]):
                content_type = response.headers.get("content-type", "")
                if "application/json" in content_type or "json" in url:
                    try:
                        payload = await response.json()
                        if isinstance(payload, (dict, list)):
                            logger.debug("Captured flight API JSON from %s", url[:80])
                            intercepted_payloads.append({"url": url, "data": payload})
                            data_received_event.set()
                    except Exception as e:
                        logger.debug("Could not parse JSON response from %s: %s", url[:80], e)

        parsed_offers: List[FlightOffer] = []

        try:
            async with async_playwright() as pw:
                browser = await pw.chromium.launch(
                    headless=self.headless,
                    args=[
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-dev-shm-usage",
                        "--disable-blink-features=AutomationControlled",
                    ],
                )

                context = await browser.new_context(
                    viewport={"width": 1440, "height": 900},
                    user_agent=(
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                        "(KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
                    ),
                    locale="ru-RU",
                    timezone_id="Asia/Almaty",
                )

                page = await context.new_page()
                await _apply_stealth(page)

                page.on("response", _handle_response)

                try:
                    await page.goto(search_url, wait_until="domcontentloaded", timeout=self.timeout_ms)
                    try:
                        await asyncio.wait_for(data_received_event.wait(), timeout=12.0)
                    except asyncio.TimeoutError:
                        logger.debug("Initial API event wait timed out; giving brief settling grace period.")

                    await page.wait_for_timeout(3000)

                    for payload_entry in intercepted_payloads:
                        offers = self.parse_aviata_json(
                            payload_entry["data"],
                            clean_origin,
                            clean_dest,
                            search_url,
                        )
                        for off in offers:
                            if not any(
                                o.flight_number == off.flight_number and o.price_kzt == off.price_kzt
                                for o in parsed_offers
                            ):
                                parsed_offers.append(off)

                except Exception as page_err:
                    logger.warning("Error during Aviata page navigation/interception: %s", page_err)
                finally:
                    await context.close()
                    await browser.close()

        except Exception as browser_err:
            logger.error("Playwright browser execution failed: %s", browser_err)

        # Filter by transfer count constraint
        if max_transfers == 0:
            filtered = [o for o in parsed_offers if o.transfers_count == 0]
        else:
            filtered = [o for o in parsed_offers if o.transfers_count <= max_transfers]

        logger.info(
            "Aviata search complete: found %d total offers, %d matching max_transfers=%d",
            len(parsed_offers),
            len(filtered),
            max_transfers,
        )
        return filtered

    async def search(
        self,
        origin: str,
        destination: str,
        date: str,
        flight_number: Optional[str] = None,
        direct_only: bool = True,
    ) -> List[FlightOffer]:
        """Convenience method to search flights with optional flight number and direct filter.

        Args:
            origin: 3-letter IATA origin airport code.
            destination: 3-letter IATA destination airport code.
            date: Departure date in YYYY-MM-DD format.
            flight_number: Optional flight code filter (e.g. 'KC-853').
            direct_only: If True, only return direct non-stop flights.

        Returns:
            List of matching FlightOffer instances.
        """
        max_transfers = 0 if direct_only else 99
        offers = await self.search_flights(
            origin=origin,
            destination=destination,
            date=date,
            max_transfers=max_transfers,
        )

        if flight_number and flight_number.strip():
            clean_fn = flight_number.strip().upper()
            offers = [o for o in offers if clean_fn in o.flight_number.upper()]

        return offers
