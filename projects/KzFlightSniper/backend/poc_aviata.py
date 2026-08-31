"""Proof of Concept (PoC) Aviata.kz Flight Interceptor.

This standalone asynchronous script uses Playwright with stealth configurations
to navigate to Aviata.kz, intercept internal flight search JSON API responses,
and extract structured flight data (prices, airlines, timings, transfer counts).

Usage:
    python poc_aviata.py [--origin ALA] [--destination NQZ] [--date YYYY-MM-DD] [--headless]
"""

import argparse
import asyncio
from datetime import datetime, timedelta
import json
import logging
import sys
from typing import Any, Dict, List, Optional
from pydantic import BaseModel, Field

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    datefmt="%H:%M:%S",
)
logger = logging.getLogger("AviataPoC")


class FlightSegment(BaseModel):
    """Details for an individual flight segment/leg."""
    flight_number: str
    airline: str
    airline_code: Optional[str] = None
    origin: str
    destination: str
    departure_time: str
    arrival_time: str
    duration_minutes: Optional[int] = None


class FlightOffer(BaseModel):
    """Standardized representation of a flight search offer."""
    provider: str = "Aviata"
    flight_number: str
    airline: str
    origin: str
    destination: str
    departure_time: str
    arrival_time: str
    duration_str: str = ""
    is_direct: bool = True
    transfers_count: int = 0
    price_kzt: float
    deep_link: str = ""
    segments: List[FlightSegment] = Field(default_factory=list)


async def apply_stealth_to_page(page: Any) -> None:
    """Apply stealth scripts and configurations to avoid bot detection."""
    try:
        from playwright_stealth import stealth_async  # type: ignore
        await stealth_async(page)
        logger.debug("Applied playwright-stealth package scripts.")
    except ImportError:
        logger.debug("playwright-stealth not installed. Applying fallback custom stealth scripts.")
        # Fallback stealth initialization
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


class AviataInterceptor:
    """Handles Playwright browser lifecycle and Aviata network response interception."""

    def __init__(self, headless: bool = True, timeout_ms: int = 45000):
        self.headless = headless
        self.timeout_ms = timeout_ms
        self.intercepted_payloads: List[Dict[str, Any]] = []
        self._data_received_event = asyncio.Event()

    async def _handle_response(self, response: Any) -> None:
        """Inspect and capture relevant JSON responses from Aviata API endpoints."""
        url = response.url
        # Filter for flight search API endpoints
        if any(keyword in url.lower() for keyword in ["search", "flight", "offer", "variant", "v2/avia", "avia/"]):
            content_type = response.headers.get("content-type", "")
            if "application/json" in content_type or "json" in url:
                try:
                    payload = await response.json()
                    if isinstance(payload, (dict, list)):
                        logger.info(f"Captured flight API response from: {url[:80]}...")
                        self.intercepted_payloads.append({"url": url, "data": payload})
                        self._data_received_event.set()
                except Exception as err:
                    logger.debug(f"Could not parse JSON from {url}: {err}")

    def _parse_aviata_json(self, raw_data: Any, origin: str, destination: str, search_url: str) -> List[FlightOffer]:
        """Parse raw Aviata JSON structures into structured FlightOffer objects."""
        offers: List[FlightOffer] = []

        if isinstance(raw_data, dict):
            # Check common Aviata JSON response keys
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
                    for subkey in ["offers", "flights", "variants", "results"]:
                        if subkey in data_field and isinstance(data_field[subkey], list):
                            items = data_field[subkey]
                            break

            for item in items:
                try:
                    offer = self._extract_single_offer(item, origin, destination, search_url)
                    if offer:
                        offers.append(offer)
                except Exception as e:
                    logger.debug(f"Error parsing item: {e}")

        return offers

    def _extract_single_offer(self, item: Dict[str, Any], origin: str, destination: str, search_url: str) -> Optional[FlightOffer]:
        """Extract a single standardized FlightOffer from a raw payload item."""
        # Price extraction
        price: float = 0.0
        if "price" in item:
            p = item["price"]
            if isinstance(p, (int, float)):
                price = float(p)
            elif isinstance(p, dict):
                price = float(p.get("amount", p.get("value", p.get("total", 0.0))))
            elif isinstance(p, str):
                price = float("".join(filter(lambda c: c.isdigit() or c == '.', p)) or 0)
        elif "total_price" in item:
            price = float(item["total_price"])
        elif "amount" in item:
            price = float(item["amount"])

        if price <= 0:
            return None

        # Airline & Flight number extraction
        airline = item.get("airline_name") or item.get("carrier_name") or item.get("airline") or "Unknown Airline"
        if isinstance(airline, dict):
            airline = airline.get("name", airline.get("title", "Unknown Airline"))

        flight_num = item.get("flight_number") or item.get("flight_no") or item.get("number") or ""
        
        # Timing extraction
        dep_time = item.get("departure_time") or item.get("departure", {}).get("time", "") if isinstance(item.get("departure"), dict) else str(item.get("departure", ""))
        arr_time = item.get("arrival_time") or item.get("arrival", {}).get("time", "") if isinstance(item.get("arrival"), dict) else str(item.get("arrival", ""))
        
        # Transfers
        transfers = item.get("transfers_count", item.get("stops", item.get("transfers", 0)))
        if not isinstance(transfers, int):
            try:
                transfers = int(transfers)
            except (ValueError, TypeError):
                transfers = 0

        # Duration
        duration_min = item.get("duration_minutes", item.get("duration", 0))
        if isinstance(duration_min, (int, float)) and duration_min > 0:
            hours = int(duration_min // 60)
            mins = int(duration_min % 60)
            dur_str = f"{hours}h {mins}m"
        else:
            dur_str = str(item.get("duration_str", item.get("travel_time", "N/A")))

        # Segments
        segments_list: List[FlightSegment] = []
        raw_segments = item.get("segments", item.get("legs", []))
        if isinstance(raw_segments, list):
            for seg in raw_segments:
                if isinstance(seg, dict):
                    seg_airline = seg.get("airline_name", seg.get("carrier", airline))
                    seg_num = seg.get("flight_number", seg.get("flight_no", flight_num))
                    segments_list.append(FlightSegment(
                        flight_number=str(seg_num),
                        airline=str(seg_airline),
                        origin=str(seg.get("origin", origin)),
                        destination=str(seg.get("destination", destination)),
                        departure_time=str(seg.get("departure_time", dep_time)),
                        arrival_time=str(seg.get("arrival_time", arr_time)),
                    ))

        if not flight_num and segments_list:
            flight_num = " / ".join(s.flight_number for s in segments_list if s.flight_number)
        if not flight_num:
            flight_num = "Aviata Flight"

        return FlightOffer(
            provider="Aviata.kz",
            flight_number=str(flight_num),
            airline=str(airline),
            origin=origin.upper(),
            destination=destination.upper(),
            departure_time=str(dep_time),
            arrival_time=str(arr_time),
            duration_str=dur_str,
            is_direct=(transfers == 0),
            transfers_count=transfers,
            price_kzt=price,
            deep_link=search_url,
            segments=segments_list,
        )

    async def search_and_intercept(
        self, origin: str, destination: str, date_str: str
    ) -> List[FlightOffer]:
        """Launch browser, perform flight search navigation, and intercept responses."""
        from playwright.async_api import async_playwright

        # Format date for Aviata URL structure
        # Typical Aviata search URL: https://aviata.kz/flights/search/ALANQZYYYYMMDD100E/
        date_formatted = date_str.replace("-", "")
        search_path = f"{origin.upper()}{destination.upper()}{date_formatted}100E"
        aviata_url = f"https://aviata.kz/flights/search/{search_path}/"
        logger.info(f"Targeting Search URL: {aviata_url}")

        parsed_offers: List[FlightOffer] = []

        async with async_playwright() as pw:
            logger.info("Launching Playwright Chromium browser instance...")
            browser = await pw.chromium.launch(
                headless=self.headless,
                args=[
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-dev-shm-usage",
                    "--disable-blink-features=AutomationControlled",
                ],
            )

            context = None
            try:
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
                await apply_stealth_to_page(page)

                # Attach network response listener
                page.on("response", self._handle_response)

                try:
                    logger.info(f"Navigating to Aviata flight search page for {origin.upper()} -> {destination.upper()}...")
                    await page.goto(aviata_url, wait_until="domcontentloaded", timeout=self.timeout_ms)

                    # Wait for data or network settlement
                    logger.info("Waiting for flight search API responses to settle...")
                    try:
                        await asyncio.wait_for(self._data_received_event.wait(), timeout=15.0)
                    except asyncio.TimeoutError:
                        logger.warning("No immediate API event triggered within initial window, waiting for page network idle...")

                    # Additional grace period for secondary variants
                    await page.wait_for_timeout(5000)

                    # Parse intercepted network payloads
                    logger.info(f"Processing {len(self.intercepted_payloads)} intercepted JSON network payload(s)...")
                    for payload_entry in self.intercepted_payloads:
                        raw_data = payload_entry["data"]
                        offers = self._parse_aviata_json(raw_data, origin, destination, aviata_url)
                        for off in offers:
                            # Avoid duplicates
                            if not any(o.flight_number == off.flight_number and o.price_kzt == off.price_kzt for o in parsed_offers):
                                parsed_offers.append(off)

                    # If JSON interception yielded no offers (e.g. dynamic client hydration/SSR), fallback to DOM extraction
                    if not parsed_offers:
                        logger.info("Attempting DOM fallback extraction from rendered flight cards...")
                        dom_offers = await self._extract_from_dom(page, origin, destination, aviata_url)
                        parsed_offers.extend(dom_offers)

                except Exception as err:
                    logger.error(f"Error during search execution: {err}", exc_info=True)
            finally:
                if context is not None:
                    await context.close()
                await browser.close()

        return parsed_offers

    async def _extract_from_dom(self, page: Any, origin: str, destination: str, search_url: str) -> List[FlightOffer]:
        """Fallback DOM scraper if API response structure was masked or encoded."""
        dom_offers: List[FlightOffer] = []
        try:
            # Evaluate script in browser to extract visible flight cards
            cards_data = await page.evaluate("""() => {
                const results = [];
                // Look for common flight card elements
                const cards = document.querySelectorAll('[class*="flight-card"], [class*="OfferCard"], [class*="ticket"], [class*="SearchResult"]');
                cards.forEach(card => {
                    const text = card.innerText;
                    // Extract price regex (e.g. 15 500 ₸ or 23400 KZT)
                    const priceMatch = text.match(/(\\d[\\d\\s]{2,})\\s*(?:₸|тг|KZT|тенге)/i);
                    const price = priceMatch ? parseFloat(priceMatch[1].replace(/\\s+/g, '')) : 0;
                    
                    // Extract airline name if present
                    const airlineElem = card.querySelector('[class*="airline"], [class*="carrier"], [class*="company"]');
                    const airline = airlineElem ? airlineElem.innerText.trim() : 'Kazakhstan Carrier';

                    if (price > 0) {
                        results.push({
                            airline: airline,
                            price: price,
                            is_direct: text.toLowerCase().includes('прямой') || text.toLowerCase().includes('без пересадок'),
                            raw_text: text.slice(0, 200)
                        });
                    }
                });
                return results;
            }""")

            for idx, item in enumerate(cards_data):
                dom_offers.append(FlightOffer(
                    provider="Aviata.kz (DOM)",
                    flight_number=f"Flight-{idx+1}",
                    airline=item["airline"],
                    origin=origin.upper(),
                    destination=destination.upper(),
                    departure_time="Check Aviata",
                    arrival_time="Check Aviata",
                    duration_str="Direct" if item["is_direct"] else "1+ Stop",
                    is_direct=item["is_direct"],
                    transfers_count=0 if item["is_direct"] else 1,
                    price_kzt=item["price"],
                    deep_link=search_url,
                ))
        except Exception as e:
            logger.debug(f"DOM fallback extraction failed: {e}")

        return dom_offers


def print_flight_table(offers: List[FlightOffer], origin: str, destination: str, date_str: str) -> None:
    """Print structured, human-readable terminal table of flight offers."""
    print("\n" + "=" * 90)
    print(f"✈️  KZ FLIGHT SNIPER — AVIATA.KZ FLIGHT SEARCH RESULTS")
    print(f"📍 Route: {origin.upper()} ➡️ {destination.upper()} | 📅 Date: {date_str}")
    print("=" * 90)

    if not offers:
        print("⚠️  No flights found or search returned 0 available seats for the requested date.")
        print("=" * 90 + "\n")
        return

    # Sort offers by price ascending
    sorted_offers = sorted(offers, key=lambda x: x.price_kzt)

    header = f"{'#':<3} | {'AIRLINE':<18} | {'FLIGHT':<12} | {'TIMES':<15} | {'TYPE':<12} | {'PRICE (KZT)':<15}"
    print(header)
    print("-" * 90)

    for idx, offer in enumerate(sorted_offers, 1):
        times = f"{offer.departure_time} - {offer.arrival_time}" if offer.departure_time else "Scheduled"
        flight_type = "Direct ⚡" if offer.is_direct else f"{offer.transfers_count} transfer(s)"
        price_formatted = f"{offer.price_kzt:,.0f} ₸".replace(",", " ")
        airline_name = (offer.airline[:16] + "..") if len(offer.airline) > 18 else offer.airline
        flight_no = (offer.flight_number[:10] + "..") if len(offer.flight_number) > 12 else offer.flight_number

        print(
            f"{idx:<3} | {airline_name:<18} | {flight_no:<12} | {times:<15} | {flight_type:<12} | {price_formatted:<15}"
        )

    print("-" * 90)
    lowest = sorted_offers[0]
    highest = sorted_offers[-1]
    direct_count = sum(1 for o in sorted_offers if o.is_direct)
    
    print(f"📊 SUMMARY:")
    print(f"   • Total Offers Found: {len(sorted_offers)} ({direct_count} Direct, {len(sorted_offers)-direct_count} Transfers)")
    print(f"   • Lowest Fare:        {lowest.price_kzt:,.0f} ₸ ({lowest.airline} - {lowest.flight_number})".replace(",", " "))
    print(f"   • Highest Fare:       {highest.price_kzt:,.0f} ₸".replace(",", " "))
    print(f"   • Direct Booking URL: {lowest.deep_link}")
    print("=" * 90 + "\n")


async def main() -> None:
    """CLI entrypoint for the Aviata PoC search."""
    parser = argparse.ArgumentParser(description="Aviata.kz Flight Interceptor PoC")
    parser.add_argument("--origin", default="ALA", help="Origin IATA code (default: ALA)")
    parser.add_argument("--destination", default="NQZ", help="Destination IATA code (default: NQZ)")
    
    # Default date: 7 days from today
    default_date = (datetime.now() + timedelta(days=7)).strftime("%Y-%m-%d")
    parser.add_argument("--date", default=default_date, help=f"Departure date YYYY-MM-DD (default: {default_date})")
    parser.add_argument("--headless", action="store_true", default=True, help="Run headless browser (default: True)")
    parser.add_argument("--no-headless", action="store_false", dest="headless", help="Run with visible browser window")

    args = parser.parse_args()

    interceptor = AviataInterceptor(headless=args.headless)
    offers = await interceptor.search_and_intercept(
        origin=args.origin,
        destination=args.destination,
        date_str=args.date,
    )

    print_flight_table(offers, args.origin, args.destination, args.date)


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        logger.info("Process interrupted by user.")
        sys.exit(0)
