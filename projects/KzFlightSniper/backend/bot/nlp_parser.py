"""Natural Language Processing (NLP) Parser for KzFlightSniper.

Combines Groq LLM-powered extraction with a resilient, zero-dependency
rule-based heuristic fallback engine for offline reliability.
"""

from datetime import date, datetime, timedelta, timezone
import json
import logging
import re
from typing import Any, Dict, List, Optional, Tuple

from backend.core.config import get_settings
from backend.core.models import ParsedFlightIntent

logger = logging.getLogger("kzflight_sniper.bot.nlp_parser")

# Currency Conversion Rates to KZT
RATES_TO_KZT: Dict[str, float] = {
    "KZT": 1.0,
    "USD": 500.0,
    "EUR": 540.0,
    "RUB": 5.5,
}

# Known Airports & City Name Mappings (Including Russian/Kazakh case declensions)
CITY_TO_IATA: Dict[str, str] = {
    # Almaty
    "алматы": "ALA", "алмата": "ALA", "алмату": "ALA", "алмате": "ALA", "almaty": "ALA", "ala": "ALA",
    # Astana
    "астана": "NQZ", "астану": "NQZ", "астане": "NQZ", "астаны": "NQZ", "astana": "NQZ", "nqz": "NQZ",
    "нур-султан": "NQZ", "нур-султана": "NQZ", "нур-султану": "NQZ", "нур-султане": "NQZ",
    "нурсултан": "NQZ", "нурсултана": "NQZ", "нурсултану": "NQZ", "нурсултане": "NQZ",
    # Shymkent
    "шымкент": "CIT", "шымкента": "CIT", "шымкенту": "CIT", "шымкенте": "CIT", "shymkent": "CIT", "cit": "CIT",
    # Aktau
    "актау": "SCO", "aktau": "SCO", "sco": "SCO",
    # Atyrau
    "атырау": "GUW", "atyrau": "GUW", "guw": "GUW",
    # Aktobe
    "актобе": "AKX", "aktobe": "AKX", "akx": "AKX",
    # Oskemen
    "усть-каменогорск": "UKK", "усть-каменогорска": "UKK", "усть-каменогорске": "UKK",
    "устькаменогорск": "UKK", "оскемен": "UKK", "оскемена": "UKK", "оскемене": "UKK", "oskemen": "UKK", "ukk": "UKK",
    # Kostanay
    "костанай": "KSG", "костаная": "KSG", "костанае": "KSG", "kostanay": "KSG", "ksg": "KSG",
    # Pavlodar
    "павлодар": "PWQ", "павлодара": "PWQ", "павлодаре": "PWQ", "pavlodar": "PWQ", "pwq": "PWQ",
    # Semey
    "семей": "PLX", "семея": "PLX", "семее": "PLX", "semey": "PLX", "plx": "PLX",
    # Taraz
    "тараз": "DMB", "тараза": "DMB", "таразе": "DMB", "taraz": "DMB", "dmb": "DMB",
    # Kokshetau
    "кокшетау": "KOV", "kokshetau": "KOV", "kov": "KOV",
    # Balkhash
    "балхаш": "BXH", "балхаша": "BXH", "балхаше": "BXH", "balkhash": "BXH", "bxh": "BXH",
    # Uralsk
    "уральск": "URA", "уральска": "URA", "уральске": "URA", "орал": "URA", "орала": "URA", "орале": "URA", "uralsk": "URA", "ura": "URA",
    # Karaganda
    "караганда": "KGF", "караганду": "KGF", "караганде": "KGF", "караганды": "KGF", "karaganda": "KGF", "kgf": "KGF",
    # Petropavlovsk
    "петропавловск": "PPK", "петропавловска": "PPK", "петропавловске": "PPK", "petropavlovsk": "PPK", "ppk": "PPK",
    # Kyzylorda
    "кызылорда": "KZO", "кызылорду": "KZO", "кызылорде": "KZO", "кызылорды": "KZO", "kyzylorda": "KZO", "kzo": "KZO",
    # Bangkok
    "бангкок": "BKK", "бангкока": "BKK", "бангкоку": "BKK", "бангкоке": "BKK", "bangkok": "BKK", "bkk": "BKK",
    # Dubai
    "дубай": "DXB", "дубая": "DXB", "дубаю": "DXB", "дубае": "DXB", "dubai": "DXB", "dxb": "DXB",
    # Istanbul
    "стамбул": "IST", "стамбула": "IST", "стамбулу": "IST", "стамбуле": "IST", "istanbul": "IST", "ist": "IST",
    # Phuket
    "пхукет": "HKT", "пхукета": "HKT", "пхукету": "HKT", "пхукете": "HKT", "phuket": "HKT", "hkt": "HKT",
    # Chengdu
    "чэнду": "CTU", "ченду": "CTU", "chengdu": "CTU", "ctu": "CTU",
    # Beijing
    "пекин": "PEK", "пекина": "PEK", "пекину": "PEK", "пекине": "PEK", "beijing": "PEK", "pek": "PEK",
    # Seoul
    "сеул": "ICN", "сеула": "ICN", "сеулу": "ICN", "сеуле": "ICN", "seoul": "ICN", "icn": "ICN",
    # Guangzhou
    "гуанчжоу": "CAN", "гуаньчжоу": "CAN", "guangzhou": "CAN", "can": "CAN",
    # Shanghai
    "шанхай": "PVG", "шанхая": "PVG", "шанхаю": "PVG", "шанхае": "PVG", "shanghai": "PVG", "pvg": "PVG",
    # Tashkent
    "ташкент": "TAS", "ташкента": "TAS", "ташкенту": "TAS", "ташкенте": "TAS", "tashkent": "TAS", "tas": "TAS",
    # Bishkek
    "бишкек": "FRU", "бишкека": "FRU", "бишкеку": "FRU", "бишкеке": "FRU", "bishkek": "FRU", "fru": "FRU",
    # Tbilisi
    "тбилиси": "TBS", "tbilisi": "TBS", "tbs": "TBS",
    # Antalya
    "анталья": "AYT", "анталью": "AYT", "анталье": "AYT", "анталия": "AYT", "анталию": "AYT", "анталии": "AYT", "antalya": "AYT", "ayt": "AYT",
    # Doha
    "доха": "DOH", "доху": "DOH", "дохе": "DOH", "дохи": "DOH", "doha": "DOH", "doh": "DOH",
    # Abu Dhabi
    "абу-даби": "AUH", "абудаби": "AUH", "abu dhabi": "AUH", "auh": "AUH",
    # Moscow
    "москва": "MOW", "москву": "MOW", "москве": "MOW", "москвы": "MOW", "moscow": "MOW", "mow": "MOW", "svo": "SVO", "vko": "VKO", "dme": "DME",
    # London
    "лондон": "LON", "лондона": "LON", "лондону": "LON", "лондоне": "LON", "london": "LON", "lon": "LON", "lhr": "LHR",
    # Tokyo
    "токио": "TYO", "tokyo": "TYO", "tyo": "TYO", "nrt": "NRT", "hnd": "HND",
    # Delhi
    "дели": "DEL", "delhi": "DEL", "del": "DEL",
    # Paris
    "париж": "CDG", "парижа": "CDG", "парижу": "CDG", "париже": "CDG", "paris": "CDG", "cdg": "CDG",
    # Milan
    "милан": "MXP", "милана": "MXP", "милану": "MXP", "милане": "MXP", "milan": "MXP", "mxp": "MXP",
    # Frankfurt
    "франкфурт": "FRA", "франкфурта": "FRA", "франкфурту": "FRA", "франкфурте": "FRA", "frankfurt": "FRA", "fra": "FRA",
    # Male
    "мале": "MLE", "мальдивы": "MLE", "мальдив": "MLE", "мальдивах": "MLE", "male": "MLE", "mle": "MLE",
    # Colombo
    "коломбо": "CMB", "colombo": "CMB", "cmb": "CMB",
    # Sanya
    "санья": "SYX", "санью": "SYX", "санье": "SYX", "sanya": "SYX", "syx": "SYX",
}

# Russian Month names to Month integer
MONTHS_RU = {
    "января": 1, "январь": 1, "янв": 1, "jan": 1, "january": 1,
    "февраля": 2, "февраль": 2, "фев": 2, "feb": 2, "february": 2,
    "марта": 3, "март": 3, "мар": 3, "mar": 3, "march": 3,
    "апреля": 4, "апрель": 4, "апр": 4, "apr": 4, "april": 4,
    "мая": 5, "май": 5, "may": 5,
    "июня": 6, "июнь": 6, "июн": 6, "jun": 6, "june": 6,
    "июля": 7, "июль": 7, "июл": 7, "jul": 7, "july": 7,
    "августа": 8, "август": 8, "авг": 8, "aug": 8, "august": 8,
    "сентября": 9, "сентябрь": 9, "сен": 9, "sep": 9, "september": 9,
    "октября": 10, "октябрь": 10, "окт": 10, "oct": 10, "october": 10,
    "ноября": 11, "ноябрь": 11, "ноя": 11, "nov": 11, "november": 11,
    "декабря": 12, "декабрь": 12, "дек": 12, "dec": 12, "december": 12,
}


def _lookup_city_iata(word: str) -> Optional[str]:
    """Look up IATA code for a word with exact match or stem fallback."""
    clean = word.strip().lower()
    if clean in CITY_TO_IATA:
        return CITY_TO_IATA[clean]

    # Try common declension endings in Russian
    for suffix in ["у", "е", "а", "ы", "и", "я", "ом", "ем"]:
        if clean.endswith(suffix):
            stem = clean[:-len(suffix)]
            for name, iata in CITY_TO_IATA.items():
                if name == stem or name.startswith(stem) and len(stem) >= 3:
                    return iata
    return None


def _extract_cities(text: str) -> Tuple[Optional[str], Optional[str]]:
    """Extract origin and destination IATA codes using heuristic keywords and patterns."""
    text_lower = text.lower()

    # Direct pattern: "из <City1> в <City2>" or "от <City1> до <City2>"
    from_to_match = re.search(
        r"(?:из|от|c|с)\s+([а-яa-z\-]+)\s+(?:в|до|на|к)\s+([а-яa-z\-]+)",
        text_lower,
    )
    if from_to_match:
        c1, c2 = from_to_match.group(1).strip(), from_to_match.group(2).strip()
        iata1 = _lookup_city_iata(c1)
        iata2 = _lookup_city_iata(c2)
        if iata1 and iata2 and iata1 != iata2:
            return iata1, iata2

    # Reverse pattern: "в <City2> из <City1>"
    to_from_match = re.search(
        r"(?:в|до|к)\s+([а-яa-z\-]+)\s+(?:из|от|с)\s+([а-яa-z\-]+)",
        text_lower,
    )
    if to_from_match:
        c2, c1 = to_from_match.group(1).strip(), to_from_match.group(2).strip()
        iata1 = _lookup_city_iata(c1)
        iata2 = _lookup_city_iata(c2)
        if iata1 and iata2 and iata1 != iata2:
            return iata1, iata2

    # Dash / Arrow pattern: "<City1> - <City2>" or "<City1> -> <City2>"
    dash_match = re.search(
        r"([а-яa-z\-]+)\s*(?:-|—|–|->|→)\s*([а-яa-z\-]+)",
        text_lower,
    )
    if dash_match:
        c1, c2 = dash_match.group(1).strip(), dash_match.group(2).strip()
        iata1 = _lookup_city_iata(c1)
        iata2 = _lookup_city_iata(c2)
        if iata1 and iata2 and iata1 != iata2:
            return iata1, iata2

    # Direct 3-letter IATA uppercase token sequence (e.g. "ALA NQZ")
    words = re.findall(r"\b[A-Za-z]{3}\b", text)
    iatas = [w.upper() for w in words if _lookup_city_iata(w)]
    if len(iatas) >= 2 and iatas[0] != iatas[1]:
        return iatas[0], iatas[1]

    # Heuristic: search all known city names in order of appearance
    found_cities: List[Tuple[int, str]] = []
    # Sort keys by length descending to match multi-word or longer names first
    sorted_city_names = sorted(CITY_TO_IATA.keys(), key=len, reverse=True)
    for name in sorted_city_names:
        idx = text_lower.find(name)
        if idx != -1:
            iata = CITY_TO_IATA[name]
            if not any(fc[1] == iata for fc in found_cities):
                found_cities.append((idx, iata))

    found_cities.sort(key=lambda x: x[0])
    if len(found_cities) >= 2 and found_cities[0][1] != found_cities[1][1]:
        return found_cities[0][1], found_cities[1][1]

    return None, None


def _extract_date(text: str, base_date: Optional[date] = None) -> Optional[str]:
    """Extract flight date in YYYY-MM-DD format using absolute and relative date patterns."""
    today = base_date or datetime.now(timezone.utc).date()
    text_lower = text.lower()

    # 1. ISO format: YYYY-MM-DD
    iso_match = re.search(r"\b(202\d)-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])\b", text)
    if iso_match:
        return iso_match.group(0)

    # 2. DD.MM.YYYY format
    dot_full_match = re.search(r"\b(0[1-9]|[12]\d|3[01])\.(0[1-9]|1[0-2])\.(202\d)\b", text)
    if dot_full_match:
        day, month, year = int(dot_full_match.group(1)), int(dot_full_match.group(2)), int(dot_full_match.group(3))
        try:
            return date(year, month, day).isoformat()
        except ValueError:
            pass

    # 3. Relative terms: сегодня, завтра, послезавтра, через неделю, через N дней
    if "послезавтра" in text_lower:
        return (today + timedelta(days=2)).isoformat()
    if "завтра" in text_lower:
        return (today + timedelta(days=1)).isoformat()
    if "сегодня" in text_lower:
        return today.isoformat()
    if "через неделю" in text_lower:
        return (today + timedelta(days=7)).isoformat()

    rel_days_match = re.search(r"через\s+(\d+)\s+(?:дней|дня|день)", text_lower)
    if rel_days_match:
        days = int(rel_days_match.group(1))
        return (today + timedelta(days=days)).isoformat()

    # 4. Textual month: "15 октября", "25 декабря 2026", "5 мая"
    for month_str, month_num in MONTHS_RU.items():
        pattern = rf"\b(0?[1-9]|[12]\d|3[01])\s+{month_str}(?:\s+(202\d))?\b"
        match = re.search(pattern, text_lower)
        if match:
            day = int(match.group(1))
            year = int(match.group(2)) if match.group(2) else today.year
            try:
                target = date(year, month_num, day)
                if target < today and not match.group(2):
                    target = date(year + 1, month_num, day)
                return target.isoformat()
            except ValueError:
                pass

    # 5. DD.MM without year (assumes current or next year)
    dot_short_match = re.search(r"\b(0?[1-9]|[12]\d|3[01])\.(0?[1-9]|1[0-2])\b", text)
    if dot_short_match:
        day, month = int(dot_short_match.group(1)), int(dot_short_match.group(2))
        try:
            target = date(today.year, month, day)
            if target < today:
                target = date(today.year + 1, month, day)
            return target.isoformat()
        except ValueError:
            pass

    return None


def _extract_price_and_currency(text: str) -> Tuple[Optional[float], str, Optional[float]]:
    """Extract target price, detected currency, and converted price in KZT.

    Returns:
        Tuple of (price_in_kzt, currency_detected, original_price)
    """
    text_clean = text.replace("\xa0", " ")
    num_pat = r"(\d{1,3}(?:[\s,]\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?)"

    # 1. USD Patterns: "$300", "300$", "300 USD", "300 долларов", "300 баксов"
    usd_patterns = [
        rf"\$\s*{num_pat}",
        rf"{num_pat}\s*(?:\$|usd|доллар(?:ов|а)?|баксов|бакса|бакс)",
    ]
    for pat in usd_patterns:
        m = re.search(pat, text_clean, re.IGNORECASE)
        if m:
            raw_val = float(m.group(1).replace(" ", "").replace(",", ""))
            if raw_val > 0:
                return raw_val * RATES_TO_KZT["USD"], "USD", raw_val

    # 2. EUR Patterns: "€300", "300€", "300 EUR", "300 евро"
    eur_patterns = [
        rf"€\s*{num_pat}",
        rf"{num_pat}\s*(?:€|eur|евро)",
    ]
    for pat in eur_patterns:
        m = re.search(pat, text_clean, re.IGNORECASE)
        if m:
            raw_val = float(m.group(1).replace(" ", "").replace(",", ""))
            if raw_val > 0:
                return raw_val * RATES_TO_KZT["EUR"], "EUR", raw_val

    # 3. RUB Patterns: "3000 руб", "3000 ₽", "3000 RUB", "3000 рублей"
    rub_patterns = [
        rf"₽\s*{num_pat}",
        rf"{num_pat}\s*(?:₽|rub|руб(?:лей|ля)?|руб)",
    ]
    for pat in rub_patterns:
        m = re.search(pat, text_clean, re.IGNORECASE)
        if m:
            raw_val = float(m.group(1).replace(" ", "").replace(",", ""))
            if raw_val > 0:
                return raw_val * RATES_TO_KZT["RUB"], "RUB", raw_val

    # 4. KZT Patterns: "25000 ₸", "25 000 тг", "25000 тенге", "25000 kzt"
    kzt_patterns = [
        rf"{num_pat}\s*(?:₸|тг|тенге|kzt)",
        rf"(?:₸|kzt)\s*{num_pat}",
    ]
    for pat in kzt_patterns:
        m = re.search(pat, text_clean, re.IGNORECASE)
        if m:
            raw_val = float(m.group(1).replace(" ", "").replace(",", ""))
            if raw_val > 0:
                return raw_val, "KZT", raw_val

    # 5. Generic Price with prefixes: "до 25000", "ниже 25000", "< 25000", "дешевле 25000", "бюджет 25000"
    prefix_patterns = [
        rf"(?:до|ниже|дешевле|бюджет|<|<=|не дороже|за)\s*{num_pat}",
    ]
    for pat in prefix_patterns:
        m = re.search(pat, text_clean, re.IGNORECASE)
        if m:
            raw_val = float(m.group(1).replace(" ", "").replace(",", ""))
            if raw_val > 0:
                return raw_val, "KZT", raw_val

    # 6. Fallback: Standalone large number (> 1000), excluding date formats and 4-digit years
    # Strip ISO dates, dotted dates, and years 2024-2039 from fallback search
    text_no_dates = re.sub(r"\b\d{4}-\d{2}-\d{2}\b", " ", text_clean)
    text_no_dates = re.sub(r"\b\d{1,2}\.\d{1,2}(?:\.\d{2,4})?\b", " ", text_no_dates)
    text_no_dates = re.sub(r"\b(202\d|203\d)\b", " ", text_no_dates)

    numbers = re.findall(r"\b\d{4,7}\b", text_no_dates)
    if numbers:
        raw_val = float(numbers[-1])
        if raw_val >= 1000:
            return raw_val, "KZT", raw_val

    return None, "KZT", None


def _extract_flight_number(text: str) -> Optional[str]:
    """Extract airline flight number code (e.g. KC-871, DV-713, IQ-401, Z9-2101)."""
    known_airlines = {
        "KC", "DV", "IQ", "Z9", "FZ", "TK", "QR", "HY", "PC", "EK", "SU", "S7",
        "W6", "CZ", "LH", "BA", "AF", "KL", "EY", "QR", "J2", "SZ", "T9", "K9",
    }
    stop_codes = {"DO", "NA", "OT", "PO", "ZA", "IZ", "TO", "IN", "ON", "AT", "KZT", "RUB", "USD", "EUR"}
    matches = re.finditer(r"\b([A-Za-z]{2}|[A-Za-z]\d|\d[A-Za-z])[-\s]?(\d{2,4})\b", text)
    for match in matches:
        code, num = match.group(1).upper(), match.group(2)
        if code in stop_codes:
            continue
        if code in known_airlines or (re.match(r"^[A-Z]{2}$", code) and code not in stop_codes):
            return f"{code}-{num}"
    return None


def _extract_interval(text: str) -> int:
    """Extract custom check interval in minutes (default 5)."""
    text_lower = text.lower()

    if "каждый час" in text_lower or "раз в час" in text_lower:
        return 60
    if "каждые полчаса" in text_lower or "раз в полчаса" in text_lower:
        return 30
    if "каждый день" in text_lower or "раз в сутки" in text_lower:
        return 1440

    m_min = re.search(r"(?:кажды[еяй]|раз в)\s+(\d+)\s*(?:мин|минут|minute|m\b)", text_lower)
    if m_min:
        return max(1, int(m_min.group(1)))

    m_hour = re.search(r"(?:кажды[еяй]|раз в)\s+(\d+)\s*(?:час|часа|часов|hour|h\b)", text_lower)
    if m_hour:
        return max(1, int(m_hour.group(1)) * 60)

    return 5


def _extract_direct_only(text: str) -> bool:
    """Determine whether direct-only flight is requested."""
    text_lower = text.lower()
    if "с пересадк" in text_lower or "можно с пересад" in text_lower or "любой" in text_lower:
        if "без пересадок" not in text_lower and "не пересад" not in text_lower:
            return False
    return True


def rule_based_flight_parser(text: str, base_date: Optional[date] = None) -> Optional[ParsedFlightIntent]:
    """Resilient regex and heuristic flight intent parser without external dependencies.

    Args:
        text: Natural language user query.
        base_date: Optional reference date for relative date resolution.

    Returns:
        ParsedFlightIntent if origin, destination, and date are found, else None.
    """
    if not text or not text.strip():
        return None

    origin, dest = _extract_cities(text)
    flight_date = _extract_date(text, base_date=base_date)
    price_kzt, currency, orig_price = _extract_price_and_currency(text)
    flight_num = _extract_flight_number(text)
    interval = _extract_interval(text)
    direct_only = _extract_direct_only(text)

    if not (origin and dest and flight_date):
        logger.debug(
            "Rule-based parser incomplete: origin=%s, dest=%s, date=%s",
            origin, dest, flight_date,
        )
        return None

    return ParsedFlightIntent(
        origin=origin,
        destination=dest,
        date=flight_date,
        flight_number=flight_num,
        direct_only=direct_only,
        target_price=price_kzt,
        currency_detected=currency if price_kzt else None,
        original_price=orig_price if price_kzt else None,
        interval_minutes=interval,
        confidence=0.9,
        raw_explanation="Rule-based heuristic parsing successfully matched route and date.",
    )


async def parse_flight_request(
    text: str,
    api_key: Optional[str] = None,
    model: Optional[str] = None,
    base_date: Optional[date] = None,
) -> Optional[ParsedFlightIntent]:
    """Parse natural language flight search intent using Groq LLM with heuristic fallback.

    Args:
        text: Natural language user request text.
        api_key: Optional Groq API Key override.
        model: Optional Groq Model identifier override.
        base_date: Optional reference date for relative date resolution.

    Returns:
        ParsedFlightIntent if successfully extracted, None otherwise.
    """
    settings = get_settings()
    groq_key = api_key or settings.GROQ_API_KEY
    groq_model = model or settings.GROQ_MODEL
    ref_date = base_date or datetime.now(timezone.utc).date()

    if not text or not text.strip():
        return None

    # Try Groq LLM if API Key is available
    if groq_key and groq_key != "placeholder_token" and not groq_key.startswith("your_"):
        try:
            from groq import AsyncGroq

            client = AsyncGroq(api_key=groq_key)
            system_prompt = f"""You are an expert Flight Intent Extraction Assistant for KzFlightSniper (Kazakhstan Aviation).
Current Reference Date: {ref_date.isoformat()} (Year: {ref_date.year}).

Extract flight monitoring parameters from the user's message into strict JSON with the following schema:
{{
  "origin": "3-letter IATA code (e.g. ALA, NQZ, CIT, SCO, GUW, UKK, AKX, KSG, PWQ, PLX, DMB, KOV, BXH, URA, KGF, PPK, KZO, CTU, PEK, ICN, HKT, CAN, PVG, BKK, DXB, IST, TAS, FRU, TBS, AYT, DOH, AUH, MOW, LON, TYO, DEL, CDG, MXP, FRA, MLE, CMB, SYX)",
  "destination": "3-letter IATA code",
  "date": "YYYY-MM-DD (resolve relative terms like 'завтра', 'послезавтра', '15 октября' using reference date {ref_date.isoformat()})",
  "flight_number": "Optional flight code (e.g. 'KC-871', 'DV-713') or null",
  "direct_only": boolean (true for direct flights, false if transfers allowed),
  "target_price": number or null (converted to KZT in Tenge: USD*500, EUR*540, RUB*5.5, KZT*1; if user did not specify target price, set null),
  "currency_detected": "USD" | "EUR" | "RUB" | "KZT" | null,
  "original_price": number or null (original price before conversion),
  "interval_minutes": integer (check frequency in minutes, e.g. 5, 10, 30, 60, default 5),
  "confidence": float (between 0.0 and 1.0),
  "raw_explanation": "Brief Russian or English summary"
}}

If the user query does not contain flight intent or lacks critical route/date info, return:
{{"error": "insufficient_info", "confidence": 0.0}}
"""
            response = await client.chat.completions.create(
                model=groq_model,
                messages=[
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": text},
                ],
                response_format={"type": "json_object"},
                temperature=0.1,
                max_tokens=400,
            )

            content = response.choices[0].message.content
            if content:
                data = json.loads(content)
                if "origin" in data and "destination" in data and "date" in data and data.get("origin") and data.get("destination") and data.get("date"):
                    intent = ParsedFlightIntent(**data)
                    logger.info("Groq LLM parsed flight intent successfully: %s -> %s on %s", intent.origin, intent.destination, intent.date)
                    return intent
        except Exception as e:
            logger.warning("Groq LLM parsing failed or timed out (%s). Falling back to rule-based parser.", e)

    # Fallback to Rule-Based Heuristic Parser
    return rule_based_flight_parser(text, base_date=ref_date)
