"""Natural Language Processing (NLP) Parser for KzFlightSniper.

Combines Groq LLM-powered extraction with a resilient, zero-dependency
rule-based heuristic fallback engine for offline reliability.
"""

from datetime import date, datetime, timedelta, timezone
import json
import logging
import re
from typing import Any, Dict, List, Optional, Set, Tuple

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
    # --- Kazakhstan Hubs ---
    # Almaty
    "алматы": "ALA", "алмата": "ALA", "алмату": "ALA", "алмате": "ALA", "алматыда": "ALA",
    "almaty": "ALA", "ala": "ALA",
    # Astana / Nur-Sultan
    "астана": "NQZ", "астану": "NQZ", "астане": "NQZ", "астаны": "NQZ", "астаной": "NQZ",
    "astana": "NQZ", "nqz": "NQZ",
    "нур-султан": "NQZ", "нур-султана": "NQZ", "нур-султану": "NQZ", "нур-султане": "NQZ", "нур-султаном": "NQZ",
    "нурсултан": "NQZ", "нурсултана": "NQZ", "нурсултану": "NQZ", "нурсултане": "NQZ", "нурсултаном": "NQZ",
    "nur-sultan": "NQZ", "nursultan": "NQZ",
    # Shymkent
    "шымкент": "CIT", "шымкента": "CIT", "шымкенту": "CIT", "шымкенте": "CIT", "шымкентом": "CIT",
    "shymkent": "CIT", "cit": "CIT",
    "чимкент": "CIT", "чимкента": "CIT", "чимкенту": "CIT", "чимкенте": "CIT",
    # Aktau
    "актау": "SCO", "актауа": "SCO", "актауе": "SCO", "актауда": "SCO", "aktau": "SCO", "sco": "SCO",
    # Atyrau
    "атырау": "GUW", "атырауа": "GUW", "атырауе": "GUW", "атырауда": "GUW", "atyrau": "GUW", "guw": "GUW",
    # Aktobe
    "актобе": "AKX", "актюбинск": "AKX", "актюбинска": "AKX", "актюбинску": "AKX", "актюбинске": "AKX",
    "aktobe": "AKX", "akx": "AKX",
    # Oskemen / Ust-Kamenogorsk
    "усть-каменогорск": "UKK", "усть-каменогорска": "UKK", "усть-каменогорску": "UKK", "усть-каменогорске": "UKK",
    "устькаменогорск": "UKK", "устькаменогорска": "UKK", "устькаменогорску": "UKK", "устькаменогорске": "UKK",
    "оскемен": "UKK", "оскемена": "UKK", "оскемену": "UKK", "оскемене": "UKK", "oskemen": "UKK", "ukk": "UKK",
    # Kostanay
    "костанай": "KSG", "костаная": "KSG", "костанаю": "KSG", "костанае": "KSG", "костанаем": "KSG",
    "kostanay": "KSG", "ksg": "KSG", "кустанай": "KSG", "кустаная": "KSG", "кустанае": "KSG",
    # Pavlodar
    "павлодар": "PWQ", "павлодара": "PWQ", "павлодару": "PWQ", "павлодаре": "PWQ", "павлодаром": "PWQ",
    "pavlodar": "PWQ", "pwq": "PWQ",
    # Semey
    "семей": "PLX", "семея": "PLX", "семею": "PLX", "семее": "PLX", "семеем": "PLX",
    "semey": "PLX", "plx": "PLX", "семипалатинск": "PLX", "семипалатинска": "PLX", "семипалатинске": "PLX",
    # Taraz
    "тараз": "DMB", "тараза": "DMB", "таразу": "DMB", "таразе": "DMB", "таразом": "DMB",
    "taraz": "DMB", "dmb": "DMB", "джамбул": "DMB", "джамбула": "DMB", "джамбуле": "DMB",
    # Kokshetau
    "кокшетау": "KOV", "кокшетауа": "KOV", "кокшетауе": "KOV", "кокшетауда": "KOV",
    "kokshetau": "KOV", "kov": "KOV", "кокчетав": "KOV", "кокчетава": "KOV", "кокчетаве": "KOV",
    # Balkhash
    "балхаш": "BXH", "балхаша": "BXH", "балхашу": "BXH", "балхаше": "BXH", "балхашом": "BXH",
    "balkhash": "BXH", "bxh": "BXH",
    # Uralsk / Oral
    "уральск": "URA", "уральска": "URA", "уральску": "URA", "уральске": "URA", "уральском": "URA",
    "орал": "URA", "орала": "URA", "оралу": "URA", "орале": "URA", "оралом": "URA", "uralsk": "URA", "ura": "URA",
    # Karaganda
    "караганда": "KGF", "караганду": "KGF", "караганде": "KGF", "караганды": "KGF", "карагандой": "KGF",
    "karaganda": "KGF", "kgf": "KGF",
    # Petropavlovsk
    "петропавловск": "PPK", "петропавловска": "PPK", "петропавловску": "PPK", "петропавловске": "PPK",
    "petropavlovsk": "PPK", "ppk": "PPK",
    # Kyzylorda
    "кызылорда": "KZO", "кызылорду": "KZO", "кызылорде": "KZO", "кызылорды": "KZO", "кызылордой": "KZO",
    "kyzylorda": "KZO", "kzo": "KZO", "кзыл-орда": "KZO", "кзылорда": "KZO",
    # Turkestan
    "туркестан": "HSA", "туркестана": "HSA", "туркестану": "HSA", "туркестане": "HSA", "туркестаном": "HSA",
    "turkestan": "HSA", "hsa": "HSA",
    # Taldykorgan
    "талдыкорган": "TDK", "талдыкоргана": "TDK", "талдыкоргану": "TDK", "талдыкоргане": "TDK",
    "taldykorgan": "TDK", "tdk": "TDK",
    # Zhezkazgan
    "жезказган": "DZN", "жезказгана": "DZN", "жезказгану": "DZN", "жезказгане": "DZN",
    "zhezkazgan": "DZN", "dzn": "DZN",
 
    # --- Asian & Middle Eastern Hubs ---
    # Chengdu
    "чэнду": "CTU", "чэндо": "CTU", "ченду": "CTU", "chengdu": "CTU", "ctu": "CTU",
    "тяньфу": "TFU", "tianfu": "TFU", "tfu": "TFU", "шуанлю": "CTU", "shuangliu": "CTU",
    # Beijing
    "пекин": "PEK", "пекина": "PEK", "пекину": "PEK", "пекине": "PEK", "пекином": "PEK",
    "beijing": "PEK", "pek": "PEK", "дасин": "PKX", "daxing": "PKX", "pkx": "PKX",
    # Seoul
    "сеул": "ICN", "сеула": "ICN", "сеулу": "ICN", "сеуле": "ICN", "сеулом": "ICN",
    "seoul": "ICN", "icn": "ICN", "инчхон": "ICN", "инчхона": "ICN", "инчхоне": "ICN", "incheon": "ICN",
    # Phuket
    "пхукет": "HKT", "пхукета": "HKT", "пхукету": "HKT", "пхукете": "HKT", "пхукетом": "HKT",
    "phuket": "HKT", "hkt": "HKT",
    # Guangzhou
    "гуанчжоу": "CAN", "гуаньчжоу": "CAN", "guangzhou": "CAN", "can": "CAN",
    # Shanghai
    "шанхай": "PVG", "шанхая": "PVG", "шанхаю": "PVG", "шанхае": "PVG", "шанхаем": "PVG",
    "shanghai": "PVG", "pvg": "PVG", "пудун": "PVG", "pudong": "PVG",
    # Bangkok
    "бангкок": "BKK", "бангкока": "BKK", "бангкоку": "BKK", "бангкоке": "BKK", "бангкоком": "BKK",
    "bangkok": "BKK", "bkk": "BKK", "суварнабхуми": "BKK", "suvarnabhumi": "BKK",
    "донмыанг": "DMK", "don mueang": "DMK", "dmk": "DMK",
    # Dubai
    "дубай": "DXB", "дубая": "DXB", "дубаю": "DXB", "дубае": "DXB", "дубаем": "DXB",
    "дубаи": "DXB", "дубаях": "DXB", "dubai": "DXB", "dxb": "DXB",
    "аль-мактум": "DWC", "аль мактум": "DWC", "al maktoum": "DWC", "dwc": "DWC",
    # Istanbul
    "стамбул": "IST", "стамбула": "IST", "стамбулу": "IST", "стамбуле": "IST", "стамбулом": "IST",
    "istanbul": "IST", "ist": "IST",
    "сабиха": "SAW", "сабиху": "SAW", "сабихе": "SAW", "сабихи": "SAW", "сабихой": "SAW", "saw": "SAW", "sabiha": "SAW",
    # Tashkent
    "ташкент": "TAS", "ташкента": "TAS", "ташкенту": "TAS", "ташкенте": "TAS", "ташкентом": "TAS",
    "tashkent": "TAS", "tas": "TAS",
    # Bishkek
    "бишкек": "FRU", "бишкека": "FRU", "бишкеку": "FRU", "бишкеке": "FRU", "бишкеком": "FRU",
    "bishkek": "FRU", "fru": "FRU", "манас": "FRU",
    # Tbilisi
    "тбилиси": "TBS", "тбилисиа": "TBS", "тбилисие": "TBS", "tbilisi": "TBS", "tbs": "TBS",
    # Antalya
    "анталья": "AYT", "анталью": "AYT", "анталье": "AYT", "анталия": "AYT", "анталию": "AYT",
    "анталии": "AYT", "анталией": "AYT", "antalya": "AYT", "ayt": "AYT",
    # Doha
    "доха": "DOH", "доху": "DOH", "дохе": "DOH", "дохи": "DOH", "дохой": "DOH",
    "doha": "DOH", "doh": "DOH", "хамад": "DOH", "hamad": "DOH",
    # Abu Dhabi
    "абу-даби": "AUH", "абудаби": "AUH", "абу даби": "AUH", "абу-дабиа": "AUH", "абу-дабие": "AUH",
    "abu dhabi": "AUH", "abu-dhabi": "AUH", "auh": "AUH",
    # Sanya
    "санья": "SYX", "санью": "SYX", "санье": "SYX", "саньи": "SYX", "саньей": "SYX",
    "sanya": "SYX", "syx": "SYX",
 
    # --- Other International Hubs ---
    # Moscow
    "москва": "MOW", "москву": "MOW", "москве": "MOW", "москвы": "MOW", "москвой": "MOW",
    "moscow": "MOW", "mow": "MOW", "svo": "SVO", "vko": "VKO", "dme": "DME", "zia": "ZIA",
    "шереметьево": "SVO", "шереметьева": "SVO", "шереметьеву": "SVO", "шереметьеве": "SVO", "шереметьевом": "SVO",
    "внуково": "VKO", "внукова": "VKO", "внукову": "VKO", "внукове": "VKO", "внуковом": "VKO",
    "домодедово": "DME", "домодедова": "DME", "домодедову": "DME", "домодедове": "DME", "домодедовом": "DME",
    "жуковский": "ZIA",
    # London
    "лондон": "LON", "лондона": "LON", "лондону": "LON", "лондоне": "LON", "лондоном": "LON",
    "london": "LON", "lon": "LON", "lhr": "LHR", "хитроу": "LHR",
    "lgw": "LGW", "гатвик": "LGW", "гатвика": "LGW", "гатвику": "LGW", "гатвике": "LGW",
    "stn": "STN", "станстед": "STN", "станстеда": "STN", "станстеду": "STN", "станстеде": "STN",
    # Tokyo
    "токио": "TYO", "tokyo": "TYO", "tyo": "TYO", "nrt": "NRT", "hnd": "HND",
    "ханеда": "HND", "ханеду": "HND", "ханеде": "HND", "ханеды": "HND",
    "нарита": "NRT", "нариту": "NRT", "нарите": "NRT", "нариты": "NRT",
    # Delhi
    "дели": "DEL", "delhi": "DEL", "del": "DEL", "нью-дели": "DEL", "new delhi": "DEL",
    # Paris
    "париж": "CDG", "парижа": "CDG", "парижу": "CDG", "париже": "CDG", "парижем": "CDG",
    "paris": "CDG", "cdg": "CDG",
    # Milan
    "милан": "MXP", "милана": "MXP", "милану": "MXP", "милане": "MXP", "миланом": "MXP",
    "milan": "MXP", "mxp": "MXP",
    # Frankfurt
    "франкфурт": "FRA", "франкфурта": "FRA", "франкфурту": "FRA", "франкфурте": "FRA", "франкфуртом": "FRA",
    "frankfurt": "FRA", "fra": "FRA",
    # Male / Maldives
    "мале": "MLE", "мальдивы": "MLE", "мальдив": "MLE", "мальдивах": "MLE", "мальдивам": "MLE",
    "male": "MLE", "mle": "MLE",
    # Colombo
    "коломбо": "CMB", "colombo": "CMB", "cmb": "CMB",
    # Baku
    "баку": "GYD", "baku": "GYD", "gyd": "GYD",
    # Yerevan
    "ереван": "EVN", "еревана": "EVN", "yerevan": "EVN", "evn": "EVN",
    # Kuala Lumpur
    "куала-лумпур": "KUL", "куала лумпур": "KUL", "kuala lumpur": "KUL", "kul": "KUL",
    # Singapore
    "сингапур": "SIN", "сингапура": "SIN", "singapore": "SIN", "sin": "SIN",
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

# Multi-Airport Metropolitan Cities & Airport Disambiguation Catalog
CITY_AMBIGUOUS_AIRPORTS: Dict[str, Dict[str, Any]] = {
    "чэнду": {
        "city_name": "Чэнду",
        "airports": [
            {"iata": "TFU", "name": "Тяньфу (TFU) — Основной/Лоукостеры"},
            {"iata": "CTU", "name": "Шуанлю (CTU) — Старый терминал"},
        ],
    },
    "москва": {
        "city_name": "Москва",
        "airports": [
            {"iata": "SVO", "name": "Шереметьево (SVO)"},
            {"iata": "DME", "name": "Домодедово (DME)"},
            {"iata": "VKO", "name": "Внуково (VKO)"},
        ],
    },
    "стамбул": {
        "city_name": "Стамбул",
        "airports": [
            {"iata": "IST", "name": "Новый Аэропорт Стамбул (IST)"},
            {"iata": "SAW", "name": "Сабиха Гёкчен (SAW)"},
        ],
    },
    "дубай": {
        "city_name": "Дубай",
        "airports": [
            {"iata": "DXB", "name": "Дубай International (DXB)"},
            {"iata": "DWC", "name": "Аль-Мактум (DWC)"},
        ],
    },
    "бангкок": {
        "city_name": "Бангкок",
        "airports": [
            {"iata": "BKK", "name": "Суварнабхуми (BKK)"},
            {"iata": "DMK", "name": "Донмыанг (DMK)"},
        ],
    },
    "пекин": {
        "city_name": "Пекин",
        "airports": [
            {"iata": "PEK", "name": "Столичный / Capital (PEK)"},
            {"iata": "PKX", "name": "Дасин (PKX)"},
        ],
    },
    "токио": {
        "city_name": "Токио",
        "airports": [
            {"iata": "HND", "name": "Ханеда (HND)"},
            {"iata": "NRT", "name": "Нарита (NRT)"},
        ],
    },
    "лондон": {
        "city_name": "Лондон",
        "airports": [
            {"iata": "LHR", "name": "Хитроу (LHR)"},
            {"iata": "LGW", "name": "Гатвик (LGW)"},
            {"iata": "STN", "name": "Станстед (STN)"},
        ],
    },
}

# Aliases and declensions for CITY_AMBIGUOUS_AIRPORTS keys
for _alias in ["chengdu", "chengdou", "ченду", "чэндо", "чэнду"]:
    CITY_AMBIGUOUS_AIRPORTS[_alias] = CITY_AMBIGUOUS_AIRPORTS["чэнду"]
for _alias in ["moscow", "москва", "москву", "москве", "москвы", "москвой"]:
    CITY_AMBIGUOUS_AIRPORTS[_alias] = CITY_AMBIGUOUS_AIRPORTS["москва"]
for _alias in ["istanbul", "стамбул", "стамбула", "стамбулу", "стамбуле", "стамбулом"]:
    CITY_AMBIGUOUS_AIRPORTS[_alias] = CITY_AMBIGUOUS_AIRPORTS["стамбул"]
for _alias in ["dubai", "дубай", "дубая", "дубаю", "дубае", "дубаем", "дубаи", "дубаях"]:
    CITY_AMBIGUOUS_AIRPORTS[_alias] = CITY_AMBIGUOUS_AIRPORTS["дубай"]
for _alias in ["bangkok", "бангкок", "бангкока", "бангкоку", "бангкоке", "бангкоком"]:
    CITY_AMBIGUOUS_AIRPORTS[_alias] = CITY_AMBIGUOUS_AIRPORTS["бангкок"]
for _alias in ["beijing", "пекин", "пекина", "пекину", "пекине", "пекином"]:
    CITY_AMBIGUOUS_AIRPORTS[_alias] = CITY_AMBIGUOUS_AIRPORTS["пекин"]
for _alias in ["tokyo", "токио"]:
    CITY_AMBIGUOUS_AIRPORTS[_alias] = CITY_AMBIGUOUS_AIRPORTS["токио"]
for _alias in ["london", "лондон", "лондона", "лондону", "лондоне", "лондоном"]:
    CITY_AMBIGUOUS_AIRPORTS[_alias] = CITY_AMBIGUOUS_AIRPORTS["лондон"]

# Specific airport keywords: if present in text, query points to an exact airport and is not ambiguous
SPECIFIC_AIRPORT_KEYWORDS: Dict[str, Set[str]] = {
    "TFU": {"тяньфу", "tianfu", "tfu"},
    "CTU": {"шуанлю", "shuangliu"},
    "SVO": {"шереметьево", "sheremetyevo", "svo"},
    "DME": {"домодедово", "domodedovo", "dme"},
    "VKO": {"внуково", "vnukovo", "vko"},
    "ZIA": {"жуковский", "zhukovsky", "zia"},
    "IST": {"новый аэропорт стамбул", "новый аэропорт"},
    "SAW": {"сабиха", "сабихи", "сабихе", "сабиху", "сабихой", "sabiha", "gokcen", "gökçen", "saw"},
    "DXB": {"дубай international", "дубай интернешнл"},
    "DWC": {"аль-мактум", "аль мактум", "al maktoum", "dwc"},
    "BKK": {"суварнабхуми", "suvarnabhumi"},
    "DMK": {"донмыанг", "don mueang", "dmk"},
    "PEK": {"столичный", "capital"},
    "PKX": {"дасин", "daxing", "pkx"},
    "HND": {"ханеда", "haneda", "hnd"},
    "NRT": {"нарита", "narita", "nrt"},
    "LHR": {"хитроу", "heathrow", "lhr"},
    "LGW": {"гатвик", "gatwick", "lgw"},
    "STN": {"станстед", "stansted", "stn"},
}

# Mapping of all IATAs belonging to an ambiguous metropolitan area -> key in CITY_AMBIGUOUS_AIRPORTS
AMBIGUOUS_IATA_TO_CITY_KEY: Dict[str, str] = {
    "CTU": "чэнду", "TFU": "чэнду",
    "MOW": "москва", "SVO": "москва", "DME": "москва", "VKO": "москва", "ZIA": "москва",
    "IST": "стамбул", "SAW": "стамбул",
    "DXB": "дубай", "DWC": "дубай",
    "BKK": "бангкок", "DMK": "бангкок",
    "PEK": "пекин", "PKX": "пекин",
    "TYO": "токио", "HND": "токио", "NRT": "токио",
    "LON": "лондон", "LHR": "лондон", "LGW": "лондон", "STN": "лондон",
}


def _check_disambiguation(
    text: str, origin: str, dest: str
) -> Tuple[bool, List[Dict[str, str]], Optional[str], Optional[str]]:
    """Check whether origin or destination refers to an ambiguous multi-airport city without specifying an airport.

    Returns:
        Tuple of (is_ambiguous, ambiguous_options, ambiguous_target, ambiguous_city_name)
    """
    text_lower = text.lower()

    # Check destination first
    if dest in AMBIGUOUS_IATA_TO_CITY_KEY:
        city_key = AMBIGUOUS_IATA_TO_CITY_KEY[dest]
        info = CITY_AMBIGUOUS_AIRPORTS[city_key]
        has_specific = False
        for airport in info["airports"]:
            iata = airport["iata"]
            kws = SPECIFIC_AIRPORT_KEYWORDS.get(iata, set())
            for kw in kws:
                pattern = rf"(?:\b|^){re.escape(kw)}(?:\b|$)"
                if re.search(pattern, text_lower):
                    has_specific = True
                    break
            if has_specific:
                break
        if not has_specific:
            return True, info["airports"], "destination", info["city_name"]

    # Check origin second
    if origin in AMBIGUOUS_IATA_TO_CITY_KEY:
        city_key = AMBIGUOUS_IATA_TO_CITY_KEY[origin]
        info = CITY_AMBIGUOUS_AIRPORTS[city_key]
        has_specific = False
        for airport in info["airports"]:
            iata = airport["iata"]
            kws = SPECIFIC_AIRPORT_KEYWORDS.get(iata, set())
            for kw in kws:
                pattern = rf"(?:\b|^){re.escape(kw)}(?:\b|$)"
                if re.search(pattern, text_lower):
                    has_specific = True
                    break
            if has_specific:
                break
        if not has_specific:
            return True, info["airports"], "origin", info["city_name"]

    return False, [], None, None


def _lookup_city_iata(word: str) -> Optional[str]:
    """Look up IATA code for a word or city name with exact match or stem fallback."""
    clean = word.strip().lower().strip(".,!?:;\"'()[]{}«»")
    if clean in CITY_TO_IATA:
        return CITY_TO_IATA[clean]

    # Try common declension endings in Russian
    for suffix in ["у", "е", "а", "ы", "и", "я", "ом", "ем", "ой", "ей", "ях", "ах", "да", "де", "та", "те"]:
        if clean.endswith(suffix) and len(clean) > len(suffix) + 2:
            stem = clean[:-len(suffix)]
            if stem in CITY_TO_IATA:
                return CITY_TO_IATA[stem]
            for name, iata in CITY_TO_IATA.items():
                if name == stem or (name.startswith(stem) and len(stem) >= 3):
                    return iata
    return None


def _extract_cities(text: str) -> Tuple[Optional[str], Optional[str]]:
    """Extract origin and destination IATA codes using heuristic keywords and patterns."""
    text_lower = text.lower()

    # 1. Look for explicit prepositions:
    # Origin prepositions: "из <city>", "от <city>", "с <city>", "c <city>"
    # Destination prepositions: "в <city>", "во <city>", "до <city>", "на <city>", "к <city>"
    origin_iata: Optional[str] = None
    dest_iata: Optional[str] = None

    # Search for origin: matches 1-2 words following origin preposition
    origin_matches = re.finditer(r"(?:из|от|c|с)\s+([а-яa-z\-]+(?:\s+[а-яa-z\-]+)?)", text_lower)
    for m in origin_matches:
        candidate = m.group(1).strip()
        iata = _lookup_city_iata(candidate)
        if not iata and " " in candidate:
            iata = _lookup_city_iata(candidate.split()[0])
        if iata:
            origin_iata = iata
            break

    # Search for destination: matches 1-2 words following destination preposition
    dest_matches = re.finditer(r"(?:в|во|до|на|к)\s+([а-яa-z\-]+(?:\s+[а-яa-z\-]+)?)", text_lower)
    for m in dest_matches:
        candidate = m.group(1).strip()
        iata = _lookup_city_iata(candidate)
        if not iata and " " in candidate:
            iata = _lookup_city_iata(candidate.split()[0])
        if iata:
            dest_iata = iata
            break

    if origin_iata and dest_iata and origin_iata != dest_iata:
        return origin_iata, dest_iata

    # 2. Dash / Arrow pattern: "<City1> - <City2>" or "<City1> -> <City2>"
    dash_match = re.search(
        r"([а-яa-z\-]+(?:\s+[а-яa-z\-]+)?)\s*(?:-|—|–|->|→)\s*([а-яa-z\-]+(?:\s+[а-яa-z\-]+)?)",
        text_lower,
    )
    if dash_match:
        c1, c2 = dash_match.group(1).strip(), dash_match.group(2).strip()
        iata1 = _lookup_city_iata(c1) or (_lookup_city_iata(c1.split()[-1]) if " " in c1 else None)
        iata2 = _lookup_city_iata(c2) or (_lookup_city_iata(c2.split()[0]) if " " in c2 else None)
        if iata1 and iata2 and iata1 != iata2:
            return iata1, iata2

    # 3. Direct 3-letter IATA uppercase token sequence (e.g. "ALA NQZ", "ALA CTU")
    words = re.findall(r"\b[A-Za-z]{3}\b", text)
    iatas = [w.upper() for w in words if _lookup_city_iata(w)]
    if len(iatas) >= 2 and iatas[0] != iatas[1]:
        return iatas[0], iatas[1]

    # 4. Heuristic: search all known city names in order of appearance
    found_cities: List[Tuple[int, str]] = []
    sorted_city_names = sorted(CITY_TO_IATA.keys(), key=len, reverse=True)
    for name in sorted_city_names:
        pattern = rf"(?:\b|^){re.escape(name)}"
        m = re.search(pattern, text_lower)
        if m:
            idx = m.start()
            iata = CITY_TO_IATA[name]
            if not any(fc[1] == iata for fc in found_cities):
                found_cities.append((idx, iata))

    found_cities.sort(key=lambda x: x[0])
    if len(found_cities) >= 2 and found_cities[0][1] != found_cities[1][1]:
        return found_cities[0][1], found_cities[1][1]

    return origin_iata, dest_iata


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

    # 4. Textual month: "15 октября", "25 декабря 2026", "5 мая", "21 ноября"
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
    """Extract airline flight number code (e.g. KC-871, DV-713, IQ-401, Z9-2101, CA-484, CZ-6012)."""
    known_airlines = {
        "KC", "DV", "IQ", "Z9", "FZ", "TK", "QR", "HY", "PC", "EK", "SU", "S7",
        "W6", "CZ", "LH", "BA", "AF", "KL", "EY", "J2", "SZ", "T9", "K9", "CA", "MU",
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


def rule_based_interval_parser(text: str) -> int:
    """Extract check interval in minutes from text using heuristics and regex.

    Examples:
        - "каждые 10 минут" -> 10
        - "раз в час" / "каждый час" -> 60
        - "раз в полчаса" / "полчаса" -> 30
        - "каждые 2 часа" -> 120
        - "раз в сутки" / "каждый день" -> 1440
        - "15 минут" -> 15
        - Default: 5
    """
    if not text or not text.strip():
        return 5

    text_lower = text.strip().lower()

    # 1. Day / Daily / Сутки patterns -> 1440
    if any(k in text_lower for k in [
        "сутки", "суток", "суткам", "сутках",
        "каждый день", "раз в день", "в день", "ежедневно",
        "раз в сутки", "каждые сутки", "в сутки", "1 день", "один день",
    ]):
        return 1440

    # 2. Half-hour / Полчаса patterns -> 30
    if any(k in text_lower for k in [
        "полчаса", "пол-часа", "пол часа", "раз в полчаса", "каждые полчаса",
        "каждые пол часа", "раз в пол часа", "30 мин", "30мин", "30 минут", "30m", "30 min",
    ]):
        return 30

    # 3. Hours patterns: "каждые 2 часа", "2 часа", "каждые 3 часа", "раз в 4 часа", "2h", "2 hours"
    m_hours = re.search(
        r"(?:кажды[еяй]|раз в|каждых)?\s*(\d+)\s*(?:час(?:а|ов)?|hour|hours|h|ч)\b",
        text_lower,
    )
    if m_hours:
        hours = int(m_hours.group(1))
        return max(1, hours * 60)

    # Hourly single patterns: "каждый час", "раз в час", "в час", "ежечасно", "1 час", "час"
    if any(k in text_lower for k in [
        "каждый час", "раз в час", "в час", "ежечасно", "каждые час", "один час", "1 час",
    ]) or text_lower == "час":
        return 60

    # 4. Minutes patterns: "каждые 10 минут", "раз в 15 минут", "15 минут", "10 мин", "10мин", "10m", "10 min"
    m_min = re.search(
        r"(?:кажды[еяй]|раз в|каждых)?\s*(\d+)\s*(?:мин(?:ут(?:ы|у|а)?)?|minute|minutes|min|m)\b",
        text_lower,
    )
    if m_min:
        mins = int(m_min.group(1))
        return max(1, mins)

    # 5. Standalone integer, e.g. "10", "15", "60", "120"
    m_num = re.search(r"^\s*(\d+)\s*$", text_lower)
    if m_num:
        val = int(m_num.group(1))
        return max(1, val)

    return 5


def _extract_interval(text: str) -> int:
    """Internal helper to extract custom check interval in minutes (default 5)."""
    return rule_based_interval_parser(text)


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

    is_ambiguous, amb_opts, amb_tgt, amb_city = _check_disambiguation(text, origin, dest)

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
        is_ambiguous=is_ambiguous,
        ambiguous_options=amb_opts,
        ambiguous_target=amb_tgt,
        ambiguous_city_name=amb_city,
    )


async def parse_search_query(
    text: str,
    api_key: Optional[str] = None,
    model: Optional[str] = None,
    base_date: Optional[date] = None,
) -> Optional[ParsedFlightIntent]:
    """Dedicated function for Step 1 of FSM flow to parse origin, destination, date, and direct_only.

    Uses Groq LLM with a specialized prompt emphasizing Asian and Kazakhstan city IATAs,
    or falls back to the rule-based parser.

    Args:
        text: Natural language search query (e.g. "Алматы - Чэнду 21 ноября", "Астана в Сеул завтра").
        api_key: Optional Groq API key override.
        model: Optional Groq Model identifier override.
        base_date: Optional reference date for relative date resolution.

    Returns:
        ParsedFlightIntent if required route and date are resolved, else None.
    """
    settings = get_settings()
    groq_key = api_key if api_key is not None else settings.GROQ_API_KEY
    groq_model = model if model is not None else settings.GROQ_MODEL
    ref_date = base_date or datetime.now(timezone.utc).date()

    if not text or not text.strip():
        return None

    # Try Groq LLM if API Key is available
    if groq_key and groq_key != "placeholder_token" and not groq_key.startswith("your_"):
        try:
            from groq import AsyncGroq

            client = AsyncGroq(api_key=groq_key)
            system_prompt = f"""You are an expert Flight Route & Date Extraction Assistant for KzFlightSniper (Kazakhstan, Asian and International Aviation).
Current Reference Date: {ref_date.isoformat()} (Year: {ref_date.year}).

Extract flight search query parameters from the user's message into strict valid JSON object with the following schema:
{{
  "origin": "3-letter IATA code (e.g. ALA, NQZ, CIT, SCO, GUW, UKK, AKX, KSG, PWQ, PLX, DMB, KOV, BXH, URA, KGF, PPK, KZO, HSA, TDK, DZN, CTU, TFU, PEK, PKX, ICN, HKT, CAN, PVG, BKK, DMK, DXB, DWC, IST, SAW, TAS, FRU, TBS, AYT, DOH, AUH, SYX, MOW, SVO, DME, VKO, LON, LHR, LGW, STN, TYO, HND, NRT, DEL, CDG, MXP, FRA, MLE, CMB, GYD, EVN, KUL, SIN)",
  "destination": "3-letter IATA code",
  "date": "YYYY-MM-DD (resolve relative terms like 'завтра', 'послезавтра', '15 октября', 'через неделю' using reference date {ref_date.isoformat()})",
  "flight_number": "Optional flight code (e.g. 'KC-871', 'CA-484', 'DV-713') or null",
  "direct_only": boolean (true for direct flights, false if transfers allowed/requested),
  "target_price": number or null (converted to KZT: USD*500, EUR*540, RUB*5.5, KZT*1; null if not specified),
  "currency_detected": "USD" | "EUR" | "RUB" | "KZT" | null,
  "original_price": number or null,
  "interval_minutes": integer (check frequency in minutes, default 5),
  "confidence": float (between 0.0 and 1.0),
  "raw_explanation": "Brief Russian or English summary",
  "is_ambiguous": boolean,
  "ambiguous_target": "destination" | "origin" | null,
  "ambiguous_city_name": string or null,
  "ambiguous_options": [{{"iata": "...", "name": "..."}}]
}}

CRITICAL DISAMBIGUATION RULES:
If the user query mentions a city with multiple major commercial airports (such as Chengdu, Moscow, Istanbul, Dubai, Bangkok, Beijing, Tokyo, London) and does NOT specify an exact single airport (like SVO or TFU), set 'is_ambiguous': true, 'ambiguous_target': 'destination' or 'origin', 'ambiguous_city_name': '<City>', and 'ambiguous_options': [{{"iata": "...", "name": "..."}}]. Otherwise, set 'is_ambiguous': false and 'ambiguous_options': [].

If the user query does not contain flight intent or lacks critical origin, destination, or date info, return JSON:
{{"error": "insufficient_info", "confidence": 0.0}}
"""
            response = await client.chat.completions.create(
                model=groq_model,
                messages=[
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": text},
                ],
                response_format={"type": "json_object"},
                temperature=0.0,
                max_tokens=600,
            )

            content = response.choices[0].message.content
            if content:
                data = json.loads(content)
                if (
                    "origin" in data
                    and "destination" in data
                    and "date" in data
                    and data.get("origin")
                    and data.get("destination")
                    and data.get("date")
                    and len(str(data["origin"]).strip()) == 3
                    and len(str(data["destination"]).strip()) == 3
                ):
                    intent = ParsedFlightIntent(**data)
                    is_amb, amb_opts, amb_tgt, amb_city = _check_disambiguation(text, intent.origin, intent.destination)
                    if is_amb:
                        intent.is_ambiguous = True
                        intent.ambiguous_options = amb_opts
                        intent.ambiguous_target = amb_tgt
                        intent.ambiguous_city_name = amb_city
                    logger.info(
                        "Groq LLM parsed search query successfully: %s -> %s on %s (ambiguous=%s)",
                        intent.origin,
                        intent.destination,
                        intent.date,
                        intent.is_ambiguous,
                    )
                    return intent
        except Exception as e:
            logger.warning(
                "Groq LLM search query parsing failed (%s). Falling back to rule-based parser.",
                e,
            )

    # Fallback to Rule-Based Heuristic Parser
    return rule_based_flight_parser(text, base_date=ref_date)


async def parse_interval_nlp(
    text: str,
    api_key: Optional[str] = None,
    model: Optional[str] = None,
) -> int:
    """Extract custom monitoring check interval in minutes from user text.

    Uses light Groq LLM prompt when configured, with a resilient rule-based fallback.

    Args:
        text: User input text (e.g. "каждые 10 минут", "раз в час", "раз в полчаса",
              "каждые 2 часа", "раз в сутки", "каждый день", "15 минут", "10").
        api_key: Optional Groq API key override.
        model: Optional Groq model override.

    Returns:
        Integer interval in minutes (>= 1, default 5).
    """
    if not text or not text.strip():
        return 5

    settings = get_settings()
    groq_key = api_key if api_key is not None else settings.GROQ_API_KEY
    groq_model = model if model is not None else settings.GROQ_MODEL

    # Try Groq LLM if API Key is available
    if groq_key and groq_key != "placeholder_token" and not groq_key.startswith("your_"):
        try:
            from groq import AsyncGroq

            client = AsyncGroq(api_key=groq_key)
            system_prompt = """You are an expert interval extraction assistant for flight monitoring bot.
Extract the periodic check interval in integer minutes from the user text:
- "каждые 10 минут" / "10 минут" / "10m" -> 10
- "раз в полчаса" / "полчаса" / "30 минут" / "30m" -> 30
- "раз в час" / "каждый час" / "1 час" / "час" / "1h" -> 60
- "каждые 2 часа" / "2 часа" / "2h" -> 120
- "раз в сутки" / "каждый день" / "сутки" -> 1440
- "15 минут" / "15 мин" / "15m" / "15 min" -> 15
- "5 минут" / default -> 5

Respond ONLY with a strict valid JSON object:
{
  "interval_minutes": 5
}
"""
            response = await client.chat.completions.create(
                model=groq_model,
                messages=[
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": text},
                ],
                response_format={"type": "json_object"},
                temperature=0.0,
                max_tokens=150,
            )

            content = response.choices[0].message.content
            if content:
                data = json.loads(content)
                if "interval_minutes" in data and isinstance(data["interval_minutes"], (int, float)):
                    val = int(data["interval_minutes"])
                    if val >= 1:
                        return val
        except Exception as e:
            logger.warning(
                "Groq LLM interval parsing failed (%s). Falling back to rule-based parser.",
                e,
            )

    return rule_based_interval_parser(text)


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
    groq_key = api_key if api_key is not None else settings.GROQ_API_KEY
    groq_model = model if model is not None else settings.GROQ_MODEL
    ref_date = base_date or datetime.now(timezone.utc).date()

    if not text or not text.strip():
        return None

    # Try Groq LLM if API Key is available
    if groq_key and groq_key != "placeholder_token" and not groq_key.startswith("your_"):
        try:
            from groq import AsyncGroq

            client = AsyncGroq(api_key=groq_key)
            system_prompt = f"""You are an expert Flight Route & Date Extraction Assistant for KzFlightSniper (Kazakhstan, Asian and International Aviation).
Current Reference Date: {ref_date.isoformat()} (Year: {ref_date.year}).

Extract flight search query parameters from the user's message into strict valid JSON object with the following schema:
{{
    "origin": "string (3-letter IATA code, e.g. ALA, NQZ)",
    "destination": "string (3-letter IATA code, e.g. CTU, BKK)",
    "date": "string (YYYY-MM-DD format)",
    "flight_number": "string or null (e.g., 'KC-871')",
    "direct_only": "boolean (true if user explicitly asked for direct flight, else false)",
    "target_price": "float or null (if user did not specify the price, set to null)",
    "interval_minutes": "integer or null (e.g., 'каждые 10 минут' -> 10, 'раз в час' -> 60)",
    "is_ambiguous": "boolean",
    "ambiguous_target": "'destination' | 'origin' | null",
    "ambiguous_city_name": "string or null",
    "ambiguous_options": [{{"iata": "...", "name": "..."}}]
}}

CRITICAL RULES:
1. Верни ответ СТРОГО В ФОРМАТЕ JSON. Никакого текста, пояснений или markdown-разметки вокруг JSON.
2. Convert city names to standard IATA codes:
   - Kazakhstan: Алматы -> ALA, Астана -> NQZ, Атырау -> GUW, Актау -> SCO, Шымкент -> CIT
   - Asia/Intl: Ченду -> CTU, Пекин -> PEK, Сеул -> ICN, Бангкок -> BKK, Пхукет -> HKT, Дубай -> DXB, Стамбул -> IST.
3. If the user mentions a relative date ("завтра", "через неделю", "21 ноября"), calculate the exact YYYY-MM-DD based on the Current Reference Date.
4. If NO price is mentioned in the text, you MUST return "target_price": null.
5. If the user query mentions a city with multiple major commercial airports (such as Chengdu, Moscow, Istanbul, Dubai, Bangkok, Beijing, Tokyo, London) and does NOT specify an exact single airport (like SVO or TFU), set 'is_ambiguous': true, 'ambiguous_target': 'destination' or 'origin', 'ambiguous_city_name': '<City>', and 'ambiguous_options': [{{"iata": "...", "name": "..."}}]. Otherwise, set 'is_ambiguous': false and 'ambiguous_options': [].
"""
            response = await client.chat.completions.create(
                model=groq_model,
                messages=[
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": text},
                ],
                response_format={"type": "json_object"},
                temperature=0.0,
                max_tokens=600,
            )

            content = response.choices[0].message.content
            if content:
                data = json.loads(content)
                
                # Normalize legacy keys if present
                if "origin_iata" in data and "origin" not in data:
                    data["origin"] = data["origin_iata"]
                if "destination_iata" in data and "destination" not in data:
                    data["destination"] = data["destination_iata"]

                if data.get("origin") and data.get("destination") and data.get("date"):
                    try:
                        intent = ParsedFlightIntent(**data)
                        if not intent.is_ambiguous:
                            is_amb, amb_opts, amb_tgt, amb_city = _check_disambiguation(text, intent.origin, intent.destination)
                            if is_amb:
                                intent.is_ambiguous = True
                                intent.ambiguous_options = amb_opts
                                intent.ambiguous_target = amb_tgt
                                intent.ambiguous_city_name = amb_city
                        logger.info(
                            "Groq LLM parsed flight intent successfully: %s -> %s on %s (ambiguous=%s)", 
                            intent.origin, intent.destination, intent.date, intent.is_ambiguous,
                        )
                        return intent
                    except Exception as validation_error:
                        logger.error("Pydantic validation error: %s", validation_error)
                        
        except Exception as e:
            logger.warning("Groq LLM parsing failed or timed out (%s). Falling back to rule-based parser.", e)

    # Fallback to Rule-Based Heuristic Parser
    return rule_based_flight_parser(text, base_date=ref_date)

