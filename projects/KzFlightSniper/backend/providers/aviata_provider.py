"""Aviata.kz / Freedom Travel flight provider using Playwright UI automation.

Navigates to the Aviata homepage, fills the search form (origin, destination,
date) via UI interaction, intercepts internal JSON API responses containing
flight data, and parses standardized FlightOffer instances.
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

    # ------------------------------------------------------------------ #
    #  IATA city name mapping for form autocomplete selection              #
    # ------------------------------------------------------------------ #
    _IATA_CITY_NAMES: Dict[str, str] = {
        "ALA": "Алматы",
        "NQZ": "Астана",
        "TSE": "Астана",
        "CIT": "Шымкент",
        "AKX": "Актобе",
        "GUW": "Атырау",
        "KZO": "Кызылорда",
        "URA": "Уральск",
        "SCO": "Актау",
        "KGF": "Караганда",
        "PWQ": "Павлодар",
        "PLX": "Семей",
        "DMB": "Тараз",
        "DZN": "Жезказган",
        "KOV": "Кокшетау",
        "PPK": "Петропавловск",
        "TDK": "Талдыкорган",
        "USJ": "Усть-Каменогорск",
        "HSA": "Туркестан",
        # International popular destinations
        "IST": "Стамбул",
        "AYT": "Анталья",
        "DXB": "Дубай",
        "SHJ": "Шарджа",
        "BKK": "Бангкок",
        "HKT": "Пхукет",
        "ICN": "Сеул",
        "CJU": "Чеджу",
        "PEK": "Пекин",
        "CAN": "Гуанчжоу",
        "URC": "Урумчи",
        "DEL": "Дели",
        "GYD": "Баку",
        "TBS": "Тбилиси",
        "LED": "Санкт-Петербург",
        "SVO": "Москва",
        "DME": "Москва",
        "LHR": "Лондон",
        "FRA": "Франкфурт",
        "KUL": "Куала-Лумпур",
    }

    async def _take_debug_screenshot(self, page: Any, filename: str, context_msg: str = "") -> None:
        """Save a debug screenshot and log page state.

        Args:
            page: Playwright page instance.
            filename: Screenshot filename (saved under /app/data/).
            context_msg: Additional context for the log message.
        """
        screenshot_path = f"/app/data/{filename}"
        try:
            await page.screenshot(path=screenshot_path, full_page=True)
            logger.info("[DEBUG-SCREENSHOT] Saved: %s %s", screenshot_path, context_msg)
        except Exception as ss_err:
            logger.error("[DEBUG-SCREENSHOT] Failed to save %s: %s", screenshot_path, ss_err)
        try:
            page_title = await page.title()
            logger.warning(
                "[DEBUG-SCREENSHOT] Page title: '%s' | URL: %s %s",
                page_title, page.url, context_msg,
            )
        except Exception:
            pass

    async def _fill_city_input(self, page: Any, input_elem: Any, iata_code: str) -> bool:
        """Fill a city input field and select the matching autocomplete suggestion.

        Tries multiple strategies: type IATA code, type city name in Russian,
        and select the first matching dropdown item.

        Args:
            page: Playwright page instance.
            input_elem: The located input element handle.
            iata_code: 3-letter IATA code (e.g. 'ALA').

        Returns:
            True if a suggestion was clicked, False otherwise.
        """
        city_name = self._IATA_CITY_NAMES.get(iata_code, iata_code)

        # Clear existing content and type city name
        await input_elem.click()
        await page.wait_for_timeout(300)
        await input_elem.fill("")
        await page.wait_for_timeout(200)
        await input_elem.type(city_name, delay=80)
        await page.wait_for_timeout(1000)

        # Try to find and click a dropdown suggestion
        dropdown_selectors = [
            # Generic autocomplete / dropdown patterns
            "[class*='dropdown'] [class*='item']",
            "[class*='dropdown'] li",
            "[class*='suggest'] [class*='item']",
            "[class*='suggest'] li",
            "[class*='autocomplete'] [class*='item']",
            "[class*='autocomplete'] li",
            "[class*='option']",
            "[role='option']",
            "[role='listbox'] [role='option']",
            "[class*='list'] [class*='item']",
            "ul[class*='dropdown'] li",
            "div[class*='popup'] div[class*='item']",
            "[class*='menu'] [class*='item']",
        ]

        for sel in dropdown_selectors:
            try:
                items = await page.query_selector_all(sel)
                if items and len(items) > 0:
                    # Prefer item whose text contains the IATA code or city name
                    for item in items:
                        text = (await item.inner_text() or "").strip()
                        if iata_code in text.upper() or city_name.lower() in text.lower():
                            await item.click()
                            logger.info("[FORM] Selected suggestion: '%s' for %s (%s)", text[:60], iata_code, sel)
                            await page.wait_for_timeout(500)
                            return True
                    # Fallback: click the first visible item
                    first_text = (await items[0].inner_text() or "").strip()
                    await items[0].click()
                    logger.info("[FORM] Selected first suggestion: '%s' for %s (%s)", first_text[:60], iata_code, sel)
                    await page.wait_for_timeout(500)
                    return True
            except Exception:
                continue

        # Final fallback: try pressing Enter to confirm typed text
        logger.warning("[FORM] No dropdown found for %s (%s). Pressing Enter as fallback.", iata_code, city_name)
        await input_elem.press("Enter")
        await page.wait_for_timeout(500)
        return False

    async def _select_date_in_calendar(self, page: Any, target_date: str) -> bool:
        """Select a date in the calendar/date-picker widget.

        Tries to find the date trigger, open the calendar, and click the target date.

        Args:
            page: Playwright page instance.
            target_date: Date string in YYYY-MM-DD format.

        Returns:
            True if date was selected, False otherwise.
        """
        dt = datetime.strptime(target_date, "%Y-%m-%d")
        day = dt.day
        # Formatted date strings for matching
        day_str = str(day)
        iso_date = dt.strftime("%Y-%m-%d")

        # Step 1: Click on the date input/trigger to open the calendar
        date_trigger_selectors = [
            "[class*='date'] input",
            "[class*='date'][class*='picker']",
            "[class*='calendar'] input",
            "[data-qa*='date']",
            "[placeholder*='Когда']",
            "[placeholder*='когда']",
            "[placeholder*='Дата']",
            "[placeholder*='дата']",
            "[class*='departure'] [class*='date']",
            "input[type='date']",
            "[class*='date-input']",
            "[class*='datepicker']",
            "button[class*='date']",
            "[class*='field'][class*='date']",
        ]

        date_trigger = None
        for sel in date_trigger_selectors:
            try:
                elem = await page.wait_for_selector(sel, timeout=2000)
                if elem:
                    date_trigger = elem
                    logger.info("[FORM] Found date trigger: %s", sel)
                    break
            except Exception:
                continue

        if date_trigger:
            await date_trigger.click()
            await page.wait_for_timeout(800)

        # Step 2: Navigate calendar to the correct month if needed
        # Try to find month/year display and navigate forward if needed
        target_month_year = dt.strftime("%Y-%m")
        for _ in range(12):  # max 12 months forward
            try:
                # Check if current calendar shows the right month
                cal_text = await page.inner_text("[class*='calendar']") or ""
                # Simple heuristic: check if the month name is visible
                month_names_ru = [
                    "", "январ", "феврал", "март", "апрел", "ма", "июн",
                    "июл", "август", "сентябр", "октябр", "ноябр", "декабр",
                ]
                target_month_fragment = month_names_ru[dt.month]
                if target_month_fragment.lower() in cal_text.lower() and str(dt.year) in cal_text:
                    logger.info("[FORM] Calendar shows correct month.")
                    break
                # Click next month button
                next_btn_sels = [
                    "[class*='next']", "[class*='forward']", "[aria-label*='next']",
                    "[aria-label*='Next']", "button[class*='arrow-right']",
                    "[class*='calendar'] [class*='right']",
                ]
                clicked = False
                for nsel in next_btn_sels:
                    try:
                        nbtn = await page.query_selector(nsel)
                        if nbtn:
                            await nbtn.click()
                            await page.wait_for_timeout(400)
                            clicked = True
                            break
                    except Exception:
                        continue
                if not clicked:
                    break
            except Exception:
                break

        # Step 3: Click the target day cell
        day_selectors = [
            f"[data-date='{iso_date}']",
            f"[data-day='{day}']",
            f"td[data-date='{iso_date}']",
            f"[aria-label*='{day}']",
            f"[class*='calendar'] [class*='day']:has-text('{day_str}')",
            f"[class*='calendar'] td:has-text('{day_str}')",
            f"[class*='calendar'] button:has-text('{day_str}')",
            f"[class*='calendar'] div[class*='cell']:has-text('{day_str}')",
        ]

        for sel in day_selectors:
            try:
                day_elem = await page.query_selector(sel)
                if day_elem:
                    await day_elem.click()
                    logger.info("[FORM] Selected date %s via selector: %s", iso_date, sel)
                    await page.wait_for_timeout(500)
                    return True
            except Exception:
                continue

        # Fallback: try clicking any element with the exact day number text in the calendar area
        try:
            cal_area = await page.query_selector("[class*='calendar']")
            if cal_area:
                all_cells = await cal_area.query_selector_all("td, div[class*='day'], button, span")
                for cell in all_cells:
                    text = (await cell.inner_text() or "").strip()
                    if text == day_str:
                        await cell.click()
                        logger.info("[FORM] Selected date %s via cell text match.", iso_date)
                        await page.wait_for_timeout(500)
                        return True
        except Exception:
            pass

        logger.warning("[FORM] Could not select date %s in calendar.", iso_date)
        return False

    async def search_flights(
        self,
        origin: str,
        destination: str,
        date: str,
        max_transfers: int = 0,
    ) -> List[FlightOffer]:
        """Search Aviata/Freedom Travel for flights via UI form automation.

        Navigates to the homepage, fills the search form (origin, destination,
        date), submits it, and intercepts the backend JSON API response.

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
        home_url = "https://aviata.kz/"

        logger.info("Executing Aviata flight search: %s -> %s on %s", clean_origin, clean_dest, date)
        logger.info("[NAV] Strategy: UI form automation from homepage %s", home_url)

        intercepted_payloads: List[Dict[str, Any]] = []
        data_received_event = asyncio.Event()

        async def _handle_response(response: Any) -> None:
            """Intercept network responses and capture flight-related JSON payloads."""
            url = response.url
            url_lower = url.lower()

            # --- Network Debugging: log any request hitting /api/ or /search/ ---
            if "/api/" in url_lower or "/search/" in url_lower:
                logger.info(
                    "[NET-DEBUG] Response %s %s (content-type: %s)",
                    response.status,
                    url[:150],
                    response.headers.get("content-type", "n/a"),
                )

            # ============================================================
            # BINGO DETECTOR: api.freedom-travel.kz flight search endpoint
            # ============================================================
            is_freedom_api = "api.freedom-travel.kz" in url_lower or "freedom-travel" in url_lower
            freedom_api_keywords = ["search", "flight", "result", "direction", "offer", "ticket", "fare"]

            if is_freedom_api and any(kw in url_lower for kw in freedom_api_keywords):
                content_type = response.headers.get("content-type", "")
                if "json" in content_type or "json" in url_lower:
                    try:
                        payload = await response.json()
                        if isinstance(payload, (dict, list)):
                            payload_info = (
                                list(payload.keys())[:12] if isinstance(payload, dict) else f"list[{len(payload)}]"
                            )
                            logger.warning("=" * 80)
                            logger.warning("[BINGO! FOUND FLIGHT API URL]: %s", url)
                            logger.warning("[BINGO! RESPONSE STATUS]: %s", response.status)
                            logger.warning("[BINGO! PAYLOAD TYPE]: %s | KEYS/LEN: %s", type(payload).__name__, payload_info)
                            logger.warning("=" * 80)
                            intercepted_payloads.append({"url": url, "data": payload, "bingo": True})
                            data_received_event.set()
                            return  # Already captured, skip generic handler below
                    except Exception as e:
                        logger.debug("Could not parse Freedom API JSON from %s: %s", url[:100], e)

            # Broad keyword match for any flight data endpoints
            flight_keywords = [
                "search", "flight", "offer", "variant", "v2/avia", "avia/",
                "result", "ticket", "price", "fare", "direction",
            ]
            if any(kw in url_lower for kw in flight_keywords):
                content_type = response.headers.get("content-type", "")
                if "application/json" in content_type or "json" in url_lower:
                    try:
                        payload = await response.json()
                        if isinstance(payload, (dict, list)):
                            logger.info(
                                "[NET-DEBUG] Captured JSON from %s (type=%s, keys/len=%s)",
                                url[:150],
                                type(payload).__name__,
                                list(payload.keys())[:10] if isinstance(payload, dict) else len(payload),
                            )
                            intercepted_payloads.append({"url": url, "data": payload})
                            data_received_event.set()
                    except Exception as e:
                        logger.debug("Could not parse JSON from %s: %s", url[:100], e)

        parsed_offers: List[FlightOffer] = []
        final_url = home_url  # track actual URL for deep links

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
                    await _apply_stealth(page)

                    # ============================================================
                    # PERFORMANCE: Block analytics/tracking to avoid networkidle stalls
                    # ============================================================
                    _BLOCKED_DOMAINS = [
                        "tiktok.com", "analytics.tiktok.com",
                        "sentry.io", "sentry-cdn.com",
                        "google-analytics.com", "googletagmanager.com",
                        "mc.yandex.ru", "yandex.ru/metrika", "metrika.yandex",
                        "hotjar.com", "hotjar.io",
                        "facebook.net", "facebook.com", "fbcdn.net",
                        "doubleclick.net", "googlesyndication.com",
                        "adservice.google", "googleads",
                        "amplitude.com", "mixpanel.com", "segment.io", "segment.com",
                        "appsflyer.com", "adjust.com",
                        "clarity.ms",
                    ]

                    async def _block_trackers(route: Any) -> None:
                        url = route.request.url.lower()
                        if any(domain in url for domain in _BLOCKED_DOMAINS):
                            logger.debug("[PERF] Blocked tracker: %s", url[:80])
                            await route.abort()
                        else:
                            await route.continue_()

                    await page.route("**/*", _block_trackers)
                    logger.info("[PERF] Tracker blocking active for %d domains.", len(_BLOCKED_DOMAINS))

                    # Attach network interceptor BEFORE any navigation
                    page.on("response", _handle_response)

                    try:
                        # ============================================================
                        # STEP 1: Navigate to the homepage
                        # ============================================================
                        logger.info("[NAV] Step 1: Navigating to homepage...")
                        await page.goto(home_url, wait_until="domcontentloaded", timeout=self.timeout_ms)

                        final_url = page.url
                        logger.info("[NAV] Landed on: %s", final_url)

                        # ============================================================
                        # STEP 1.5: Nuclear modal/popup/banner dismissal
                        # ============================================================
                        logger.info("[MODAL] Waiting for popups to render...")
                        await page.wait_for_timeout(3500)

                        logger.info("[MODAL] Executing nuclear modal removal via z-index purge...")
                        await page.evaluate('''
                            document.querySelectorAll('*').forEach(el => {
                                const zIndex = window.getComputedStyle(el).zIndex;
                                if (zIndex !== 'auto' && parseInt(zIndex) > 50) {
                                    el.style.display = 'none';
                                }
                            });
                        ''')

                        # Fallback background click
                        try:
                            await page.mouse.click(10, 10)
                        except Exception:
                            pass

                        # Lightweight backup Escape and close button handling
                        try:
                            await page.keyboard.press("Escape")
                        except Exception:
                            pass

                        close_button_selectors = [
                            "button[aria-label='close']",
                            "button[aria-label='Close']",
                            "button[aria-label='Закрыть']",
                            "button[aria-label='закрыть']",
                            "[class*='modal'] button[class*='close']",
                            "[class*='modal'] [class*='close']",
                            "[class*='Modal'] [class*='Close']",
                            "[role='dialog'] button[aria-label='Close']",
                            "[role='dialog'] button[aria-label='close']",
                            "[class*='superapp'] button",
                            "button[class*='btn-close']",
                            "button[class*='close-btn']",
                            ".close",
                        ]

                        for sel in close_button_selectors:
                            try:
                                btn = await page.query_selector(sel)
                                if btn and await btn.is_visible():
                                    await btn.click()
                                    logger.info("[MODAL] Dismissed via: %s", sel)
                                    break
                            except Exception:
                                pass

                        logger.info("[MODAL] Modal removal sequence complete.")

                        # ============================================================
                        # STEP 2: Fill "Origin" (Откуда) field
                        # ============================================================
                        logger.info("[FORM] Step 2: Filling origin = %s", clean_origin)
                        origin_selectors = [
                            "[data-qa*='origin'] input",
                            "[data-qa*='from'] input",
                            "input[placeholder*='Откуда']",
                            "input[placeholder*='откуда']",
                            "input[placeholder*='From']",
                            "input[placeholder*='Город вылета']",
                            "input[name*='origin']",
                            "input[name*='from']",
                            "[class*='origin'] input",
                            "[class*='from'] input",
                            "[class*='departure'] input",
                            # Broad fallbacks: first and second input in the search form
                            "form input:first-of-type",
                            "[class*='search'] input:first-of-type",
                        ]

                        origin_input = None
                        for sel in origin_selectors:
                            try:
                                elem = await page.wait_for_selector(sel, state="visible", timeout=5000)
                                if elem:
                                    origin_input = elem
                                    logger.info("[FORM] Found origin input (visible): %s", sel)
                                    break
                            except Exception:
                                continue

                        if not origin_input:
                            # Ultra-fallback: grab all visible inputs and use the first one
                            all_inputs = await page.query_selector_all("input[type='text'], input:not([type])")
                            for inp in all_inputs:
                                if await inp.is_visible():
                                    origin_input = inp
                                    logger.warning("[FORM] Using fallback first visible input for origin.")
                                    break

                        if origin_input:
                            await self._fill_city_input(page, origin_input, clean_origin)
                        else:
                            logger.error("[FORM] Could not find origin input field!")
                            await self._take_debug_screenshot(page, "aviata_debug_no_origin_input.png")

                        await page.wait_for_timeout(800)

                        # ============================================================
                        # STEP 3: Fill "Destination" (Куда) field
                        # ============================================================
                        logger.info("[FORM] Step 3: Filling destination = %s", clean_dest)
                        dest_selectors = [
                            "[data-qa*='destination'] input",
                            "[data-qa*='to'] input",
                            "input[placeholder*='Куда']",
                            "input[placeholder*='куда']",
                            "input[placeholder*='To']",
                            "input[placeholder*='Город прибытия']",
                            "input[name*='destination']",
                            "input[name*='to']",
                            "[class*='destination'] input",
                            "[class*='to'] input",
                            "[class*='arrival'] input",
                        ]

                        dest_input = None
                        for sel in dest_selectors:
                            try:
                                elem = await page.wait_for_selector(sel, state="visible", timeout=5000)
                                if elem:
                                    dest_input = elem
                                    logger.info("[FORM] Found destination input (visible): %s", sel)
                                    break
                            except Exception:
                                continue

                        if not dest_input:
                            # Fallback: second visible text input
                            all_inputs = await page.query_selector_all("input[type='text'], input:not([type])")
                            visible_inputs = []
                            for inp in all_inputs:
                                if await inp.is_visible():
                                    visible_inputs.append(inp)
                            if len(visible_inputs) >= 2:
                                dest_input = visible_inputs[1]
                                logger.warning("[FORM] Using fallback second visible input for destination.")

                        if dest_input:
                            await self._fill_city_input(page, dest_input, clean_dest)
                        else:
                            logger.error("[FORM] Could not find destination input field!")
                            await self._take_debug_screenshot(page, "aviata_debug_no_dest_input.png")

                        await page.wait_for_timeout(800)

                        # ============================================================
                        # STEP 4: Select departure date
                        # ============================================================
                        logger.info("[FORM] Step 4: Selecting date = %s", date)
                        await self._select_date_in_calendar(page, date)
                        await page.wait_for_timeout(800)

                        # ============================================================
                        # STEP 5: Click "Search" / "Найти" button
                        # ============================================================
                        logger.info("[FORM] Step 5: Clicking search button...")
                        search_btn_selectors = [
                            "button:has-text('Найти')",
                            "button:has-text('найти')",
                            "button:has-text('Поиск')",
                            "button:has-text('Search')",
                            "button:has-text('Найти билеты')",
                            "[data-qa*='search'] button",
                            "[class*='search'] button[type='submit']",
                            "form button[type='submit']",
                            "button[class*='search']",
                            "[class*='submit'] button",
                            "a:has-text('Найти')",
                        ]

                        search_clicked = False
                        for sel in search_btn_selectors:
                            try:
                                btn = await page.wait_for_selector(sel, timeout=3000)
                                if btn and await btn.is_visible():
                                    await btn.click()
                                    logger.info("[FORM] Clicked search button: %s", sel)
                                    search_clicked = True
                                    break
                            except Exception:
                                continue

                        if not search_clicked:
                            logger.warning("[FORM] Could not find search button. Trying Enter key...")
                            await page.keyboard.press("Enter")

                        # ============================================================
                        # STEP 6: Wait for search results (API interception)
                        # ============================================================
                        logger.info("[NAV] Step 6: Waiting for search results...")

                        # Wait ONLY for the intercepted JSON data event — no networkidle!
                        try:
                            await asyncio.wait_for(data_received_event.wait(), timeout=25.0)
                            logger.info(
                                "[NAV] data_received_event fired! Payloads captured: %d",
                                len(intercepted_payloads),
                            )
                        except asyncio.TimeoutError:
                            logger.warning("[NAV] API data event timed out (25s). No JSON payloads intercepted.")

                        # Short grace period for any trailing result chunks
                        await page.wait_for_timeout(3000)

                        logger.info(
                            "[NAV] Total intercepted payloads after settling: %d",
                            len(intercepted_payloads),
                        )

                        final_url = page.url

                        # ============================================================
                        # STEP 7: Parse all intercepted JSON payloads
                        # ============================================================
                        for payload_entry in intercepted_payloads:
                            offers = self.parse_aviata_json(
                                payload_entry["data"],
                                clean_origin,
                                clean_dest,
                                final_url,
                            )
                            for off in offers:
                                if not any(
                                    o.flight_number == off.flight_number and o.price_kzt == off.price_kzt
                                    for o in parsed_offers
                                ):
                                    parsed_offers.append(off)

                        # ============================================================
                        # STEP 8: Debug screenshot if 0 offers found
                        # ============================================================
                        if len(parsed_offers) == 0:
                            logger.warning("[RESULT] 0 offers parsed from %d payloads!", len(intercepted_payloads))
                            await self._take_debug_screenshot(page, "aviata_debug_0_offers.png", "(0 offers)")
                        else:
                            logger.info("[RESULT] Successfully parsed %d offers.", len(parsed_offers))

                    except Exception as page_err:
                        logger.warning("Error during Aviata page interaction: %s", page_err)
                        try:
                            await self._take_debug_screenshot(page, "aviata_debug_error.png", f"(exception: {page_err})")
                        except Exception:
                            pass
                finally:
                    if context is not None:
                        try:
                            await context.close()
                        except Exception:
                            pass
                    try:
                        await browser.close()
                    except Exception:
                        pass

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
