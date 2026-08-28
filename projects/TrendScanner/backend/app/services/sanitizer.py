"""Text sanitization and pre-filtering logic for TrendScanner (Stage 2)."""

import html
import re
import unicodedata
import urllib.parse
from typing import Any, Dict, List, Optional, Pattern, Set
from pydantic import BaseModel, Field
from bs4 import BeautifulSoup


class SanitizedResult(BaseModel):
    """Result of text sanitization and validation pipeline."""
    is_valid: bool = Field(..., description="Whether the text passed all sanitization and filtering checks")
    cleaned_text: str = Field(..., description="Sanitized and normalized text content")
    reject_reason: Optional[str] = Field(default=None, description="Reason for rejection if is_valid is False")


class TextSanitizer:
    """Sanitizer and heuristic spam detector for raw ingested content."""

    DEFAULT_SPAM_PATTERNS: List[str] = [
        # Crypto pump and dump
        r"\bcrypto\s*pump\b",
        r"\bpump\s*(?:and|&)\s*dump\b",
        r"\bpump\s*(?:channel|group|signal[s]?)\b",
        # Airdrops & giveaways
        r"\bfree\s*airdrop[s]?\b",
        r"\bairdrop\s*claim\b",
        r"\bclaim\s*(?:free\s*)?(?:tokens?|airdrop|crypto)\b",
        r"\bfree\s*(?:tokens?|crypto|usdt|sol|btc|eth)\b",
        r"\bdouble\s*your\s*(?:btc|eth|crypto|money|investment)\b",
        # Guaranteed returns & scams
        r"\bguaranteed\s*profit[s]?\b",
        r"\bguaranteed\s*returns?\b",
        r"\bguaranteed\s*income\b",
        r"\brisk[-\s]*free\s*(?:profit|returns?|investment)\b",
        r"\b100%\s*(?:profit|guarantee|risk[-\s]*free)\b",
        # 100x / 1000x hype
        r"\b100x\s*(?:gem|potential|gains?|easy|crypto)?\b",
        r"\b1000x\s*(?:gem|potential|gains?|easy|crypto)?\b",
        r"\bnext\s*(?:100x|1000x)\b",
        # Outreach & funnel redirection
        r"\bjoin\s*(?:our\s*)?(?:the\s*)?telegram\b",
        r"\bjoin\s*(?:my|our)\s*channel\b",
        r"\btelegram\s*group\b",
        r"\bvip\s*signals?\b",
        r"\bt\.me\/[a-zA-Z0-9_+]+",
        r"\bdm\s*(?:me\s*)?for\s*(?:info|details|signals?|access)\b",
        r"\binbox\s*(?:me\s*)?for\s*(?:info|details)\b",
        r"\bpm\s*(?:me\s*)?for\s*(?:info|details)\b",
        # Presale & mint spam
        r"\bpresale\s*is\s*live\b",
        r"\bmint\s*is\s*live\b",
        r"\bwhitelist\s*spot[s]?\b",
        # Multilingual / Russian heuristics
        r"крипто\s*памп",
        r"памп\s*(?:и|&)\s*дамп",
        r"бесплатн(?:ый|ые)\s*аирдроп",
        r"гарантированн(?:ая|ый|ое)\s*(?:прибыль|доход|заработок)",
        r"(?:вступай|переходи|подписывайся)\s*(?:в|на)\s*(?:наш\s*)?(?:телеграм|тг|канал)",
        r"пиши(?:те)?\s*в\s*(?:лс|директ|личку)",
        r"сигналы\s*для\s*(?:крипты|трейдинга)",
        r"100х\s*гем",
    ]

    def __init__(self, custom_spam_patterns: Optional[List[str]] = None) -> None:
        """Initialize TextSanitizer with default or custom regex patterns."""
        patterns_to_compile = list(self.DEFAULT_SPAM_PATTERNS)
        if custom_spam_patterns:
            patterns_to_compile.extend(custom_spam_patterns)

        self._compiled_spam_patterns: List[Pattern[str]] = [
            re.compile(pattern, re.IGNORECASE | re.UNICODE)
            for pattern in patterns_to_compile
        ]

    def clean_text(self, raw_text: str) -> str:
        """Clean and normalize raw text.
        
        Performs:
        1. HTML unescape
        2. HTML tags removal (stripping tags, scripts, styles)
        3. Markdown links and images extraction/trimming
        4. Unicode normalization (NFKC) and control characters stripping
        5. Excessive whitespace and newline normalization
        """
        if not raw_text or not isinstance(raw_text, str):
            return ""

        text = raw_text

        # 1. Unescape HTML entities (e.g. &amp;, &lt;, &gt;, &quot;)
        text = html.unescape(text)

        # 2. Strip HTML tags (using BeautifulSoup if tags present)
        if "<" in text and ">" in text:
            try:
                soup = BeautifulSoup(text, "html.parser")
                # Remove non-content tags
                for tag in soup(["script", "style", "head", "title", "meta", "[document]"]):
                    tag.decompose()
                text = soup.get_text(separator=" ")
            except Exception:
                # Fallback to regex if bs4 parsing fails
                text = re.sub(r"<[^>]+>", " ", text)

        # 3. Trim markdown links & images: ![alt](url) -> alt, [text](url) -> text
        text = re.sub(r"!\[([^\]]*)\]\([^)]+\)", r"\1", text)
        text = re.sub(r"\[([^\]]+)\]\([^)]+\)", r"\1", text)

        # 4. Normalize unicode characters (NFKC)
        text = unicodedata.normalize("NFKC", text)

        # 5. Remove non-printable control characters, zero-width spaces, BOM
        # Keep standard whitespace like \n, \t, \r
        text = re.sub(r"[\u200B-\u200D\uFEFF\u200E\u200F\u00A0]", " ", text)
        cleaned_chars = [
            ch for ch in text
            if ch.isprintable() or ch in ("\n", "\t", "\r")
        ]
        text = "".join(cleaned_chars)

        # 6. Normalize whitespace:
        # Collapse multiple horizontal spaces/tabs to a single space
        text = re.sub(r"[^\S\r\n]+", " ", text)
        # Collapse 3 or more consecutive newlines into 2 (preserving paragraph structure)
        text = re.sub(r"\n{3,}", "\n\n", text)
        # Normalize lines with only whitespace
        text = re.sub(r"^[ \t]+|[ \t]+$", "", text, flags=re.MULTILINE)

        return text.strip()

    def is_spam(self, text: str) -> bool:
        """Check whether text matches spam or crypto-pump heuristics."""
        if not text:
            return False

        for pattern in self._compiled_spam_patterns:
            if pattern.search(text):
                return True
        return False

    def sanitize(self, text: str, min_length: int = 100) -> SanitizedResult:
        """Sanitize raw text and evaluate validity according to length and spam criteria.
        
        Args:
            text: Raw input text.
            min_length: Minimum character length required for valid content (default 100).
            
        Returns:
            SanitizedResult: Validation status, cleaned text, and reject reason if invalid.
        """
        cleaned_text = self.clean_text(text)

        # Length check
        if len(cleaned_text) < min_length:
            return SanitizedResult(
                is_valid=False,
                cleaned_text=cleaned_text,
                reject_reason="too_short",
            )

        # Spam / pump heuristics check
        if self.is_spam(cleaned_text):
            return SanitizedResult(
                is_valid=False,
                cleaned_text=cleaned_text,
                reject_reason="spam_detected",
            )

        return SanitizedResult(
            is_valid=True,
            cleaned_text=cleaned_text,
            reject_reason=None,
        )


def extract_candidate_sources(raw_content: str) -> List[Dict[str, str]]:
    """
    Extract and auto-discover potential new radar sources (Telegram, Substack, Medium, Hacker News, Reddit, web)
    from raw ingested content.

    Parses:
    - HTML links (<a href="...">)
    - Markdown links ([text](url))
    - Plain text URLs (t.me/..., medium.com/..., substack.com, news.ycombinator.com/item?id=...)
    - Telegram @mentions and Reddit r/mentions

    Returns a deduplicated list of candidate source dictionaries:
    [{"url": base_url, "name": name, "source_type": "auto_discovered"}]
    """
    if not raw_content or not isinstance(raw_content, str):
        return []

    # 1. Collect all raw URL candidates
    raw_urls: List[str] = []

    # HTML href links
    for match in re.finditer(r'<a\s+(?:[^>]*?\s+)?href=["\']([^"\']+)["\']', raw_content, re.IGNORECASE):
        raw_urls.append(match.group(1).strip())

    # Markdown links: [title](url)
    for match in re.finditer(r'\[(?:[^\]]*)\]\((https?://[^\s\)]+)\)', raw_content):
        raw_urls.append(match.group(1).strip())

    # Plain text URLs and target domain paths
    plain_url_pattern = re.compile(
        r'(?:https?://|www\.)[^\s<>"\'\)\]\}]+|'
        r'\b(?:t\.me|telegram\.me|news\.ycombinator\.com|[a-zA-Z0-9_-]+\.substack\.com|(?:[a-zA-Z0-9_-]+\.)?medium\.com|reddit\.com/r/[a-zA-Z0-9_]+)/[^\s<>"\'\)\]\}]*',
        re.IGNORECASE,
    )
    for match in plain_url_pattern.finditer(raw_content):
        raw_urls.append(match.group(0).strip())

    # Direct @username mentions (Telegram)
    for match in re.finditer(r'(?<![\w@])@([a-zA-Z0-9_]{4,32})\b', raw_content):
        raw_urls.append(f"https://t.me/{match.group(1)}")

    # Direct r/subreddit mentions (Reddit)
    for match in re.finditer(r'(?<!\w)r/([a-zA-Z0-9_]{3,32})\b', raw_content, re.IGNORECASE):
        raw_urls.append(f"https://www.reddit.com/r/{match.group(1)}")

    candidates: List[Dict[str, str]] = []
    seen_urls: Set[str] = set()

    for url_str in raw_urls:
        if not url_str:
            continue

        # Strip trailing punctuation often caught in plain text
        cleaned_url = re.sub(r'[\.,;:!\?\'"\)\]\}]+$', '', url_str).strip()
        if not cleaned_url:
            continue

        # Skip media / binary files
        if re.search(r"\.(png|jpg|jpeg|gif|svg|webp|ico|mp4|mp3|css|js|woff|woff2|ttf|pdf)$", cleaned_url, re.IGNORECASE):
            continue

        if not cleaned_url.startswith("http://") and not cleaned_url.startswith("https://"):
            cleaned_url = "https://" + cleaned_url

        try:
            parsed = urllib.parse.urlparse(cleaned_url)
        except Exception:
            continue

        netloc = (parsed.netloc or "").strip()
        netloc_lower = netloc.lower()
        if netloc_lower.startswith("www."):
            netloc_check = netloc_lower[4:]
        else:
            netloc_check = netloc_lower

        path = (parsed.path or "").strip("/")
        path_segments = [seg for seg in path.split("/") if seg]

        candidate_url: Optional[str] = None
        candidate_name: Optional[str] = None

        # 1. Telegram: t.me/<username>
        if netloc_check in ("t.me", "telegram.me"):
            if path_segments:
                seg0 = path_segments[0]
                username = path_segments[1] if (seg0.lower() == "s" and len(path_segments) > 1) else seg0
                username = username.lstrip("@").strip()
                reserved = {
                    "joinchat", "share", "addstickers", "addtheme",
                    "setlanguage", "iv", "login", "socks", "proxy", "c", "bot"
                }
                if username and username.lower() not in reserved and not username.startswith("+") and len(username) >= 3:
                    candidate_url = f"https://t.me/{username}"
                    candidate_name = f"Telegram: @{username} (Найдено ИИ)"

        # 2. Substack: https://<subdomain>.substack.com
        elif netloc_check.endswith(".substack.com"):
            subdomain = netloc_check[:-len(".substack.com")].strip(".")
            if subdomain and subdomain.lower() not in ("www", "api", "cdn", "support", "status", "about"):
                candidate_url = f"https://{subdomain}.substack.com/feed"
                candidate_name = f"Substack: {subdomain} (Найдено ИИ)"

        # 3. Hacker News: news.ycombinator.com
        elif netloc_check == "news.ycombinator.com":
            candidate_url = "https://news.ycombinator.com/rss"
            candidate_name = "Hacker News (Найдено ИИ)"

        # 4. Medium: medium.com/@author, <publication>.medium.com, medium.com/tag/<topic>
        elif netloc_check == "medium.com" or netloc_check.endswith(".medium.com"):
            if netloc_check.endswith(".medium.com") and netloc_check != "medium.com":
                pub = netloc_check[:-len(".medium.com")].strip(".")
                if pub and pub.lower() not in ("www", "api", "cdn", "support", "status"):
                    candidate_url = f"https://{pub}.medium.com/feed"
                    candidate_name = f"Medium: {pub} (Найдено ИИ)"
            elif path_segments:
                seg0 = path_segments[0]
                if seg0.startswith("@"):
                    author = seg0.lstrip("@")
                    candidate_url = f"https://medium.com/feed/@{author}"
                    candidate_name = f"Medium: @{author} (Найдено ИИ)"
                elif seg0.lower() == "tag" and len(path_segments) > 1:
                    topic = path_segments[1]
                    candidate_url = f"https://medium.com/feed/tag/{topic}"
                    candidate_name = f"Medium: tag/{topic} (Найдено ИИ)"
                elif seg0.lower() == "feed" and len(path_segments) > 1:
                    item = path_segments[1]
                    candidate_url = f"https://medium.com/feed/{item}"
                    candidate_name = f"Medium: {item} (Найдено ИИ)"
                elif seg0.lower() not in ("m", "p", "search", "me", "plans", "membership", "about", "creators", "tag", "feed"):
                    candidate_url = f"https://medium.com/feed/{seg0}"
                    candidate_name = f"Medium: {seg0} (Найдено ИИ)"

        # 5. Reddit: reddit.com/r/<sub_name>
        elif netloc_check == "reddit.com":
            if len(path_segments) >= 2 and path_segments[0].lower() == "r":
                subreddit = path_segments[1]
                candidate_url = f"https://www.reddit.com/r/{subreddit}"
                candidate_name = f"Reddit: r/{subreddit} (Найдено ИИ)"

        # 6. Generic URLs (e.g. https://trendscanner.io)
        else:
            if netloc:
                candidate_url = f"{parsed.scheme or 'https'}://{netloc}"
                if path:
                    candidate_url += f"/{path}"
                candidate_name = f"{netloc} (Найдено ИИ)"

        if candidate_url and candidate_name:
            norm_key = candidate_url.rstrip("/").lower()
            if norm_key not in seen_urls:
                seen_urls.add(norm_key)
                candidates.append({
                    "url": candidate_url.rstrip("/"),
                    "name": candidate_name,
                    "source_type": "auto_discovered",
                })

    return candidates


# Global default sanitizer instance for direct usage
sanitizer = TextSanitizer()

ENGLISH_STOPWORDS_REGEX = re.compile(
    r"\b(the|and|is|are|was|were|this|that|with|for|from|have|has|had|will|would|can|could|should|been|about|which|their|there|they|what|when|where|who|why|how)\b",
    re.IGNORECASE,
)

ENGLISH_STARTER_REGEX = re.compile(
    r"(?:^|[\.\?!]\s+)(The|This|These|Those|An|A|In|On|It|We|They|There|Here|When|Why|How|You|Our|Their|If|As|With|For|By)\s+",
    re.IGNORECASE,
)


def is_predominantly_cyrillic(text: str) -> bool:
    """
    Check whether text contains predominantly Cyrillic characters or is written in Russian.
    Allows standard IT abbreviations and terms (SaaS, ARR, MRR, API, Stripe, React, AI).
    """
    if not text or not text.strip():
        return False

    clean = text.strip()
    cyrillic_chars = len(re.findall(r"[а-яА-ЯёЁ]", clean))
    latin_chars = len(re.findall(r"[a-zA-Z]", clean))
    total_letters = cyrillic_chars + latin_chars

    if total_letters == 0:
        return False

    # Pure Russian / High Cyrillic
    if cyrillic_chars > 15 and cyrillic_chars >= latin_chars:
        return True

    # Check ratio: if latin characters exceed 40% and cyrillic count is low (< 20)
    latin_ratio = latin_chars / total_letters
    if latin_ratio > 0.40 and cyrillic_chars < 20:
        return False

    # If cyrillic dominates
    if cyrillic_chars > latin_chars:
        return True

    return cyrillic_chars >= latin_chars


def has_untranslated_english_markers(text: str) -> bool:
    """
    Check if text contains obvious markers of untranslated English sentences.
    Detects:
    1. Complete absence of Cyrillic letters in non-empty text.
    2. English sentence starters when latin >= cyrillic.
    3. Prominent English grammatical stopwords (the, and, is, for, with...).
    """
    if not text or not text.strip():
        return False

    clean = text.strip()
    cyrillic_count = len(re.findall(r"[а-яА-ЯёЁ]", clean))
    latin_count = len(re.findall(r"[a-zA-Z]", clean))

    # If there are no Cyrillic characters at all and has latin words
    if cyrillic_count == 0 and latin_count >= 10:
        return True

    # Check English stop words frequency
    stopwords_matches = ENGLISH_STOPWORDS_REGEX.findall(clean)
    if len(stopwords_matches) >= 3 and cyrillic_count < latin_count:
        return True

    if len(stopwords_matches) >= 2 and cyrillic_count == 0:
        return True

    # Sentence starters with high Latin ratio
    if ENGLISH_STARTER_REGEX.search(clean) and latin_count >= cyrillic_count:
        return True

    return False


async def translate_to_russian(text: str) -> str:
    """Helper delegating to groq_client.translate_to_russian."""
    from app.services.groq_client import translate_to_russian as _trans
    return await _trans(text)


async def sanitize_and_translate_content(
    text: str,
    min_length: int = 100,
    sanitizer_instance: Optional[TextSanitizer] = None,
    translate_func: Optional[Any] = None,
) -> SanitizedResult:
    """
    Full Ingestion Language Checkpoint (Level 11 'No English' Rule):
    1. Sanitizes and strips HTML/spam.
    2. Checks if content is predominantly Russian.
    3. If not Russian (English / Latin > 40%), executes deep-translator GoogleTranslator.
    4. Validates translated text: if still containing English markers, DROPS the item (reject_reason='untranslated_english').
    """
    san = sanitizer_instance or sanitizer
    base_res = san.sanitize(text, min_length=min_length)
    if not base_res.is_valid:
        return base_res

    cleaned_text = base_res.cleaned_text

    # Check if already clean Russian
    if is_predominantly_cyrillic(cleaned_text) and not has_untranslated_english_markers(cleaned_text):
        return SanitizedResult(
            is_valid=True,
            cleaned_text=cleaned_text,
            reject_reason=None,
        )

    # Enforce translation to Russian
    try:
        if translate_func is not None:
            translated = await translate_func(cleaned_text)
        elif hasattr(san, "translate_to_russian") and callable(san.translate_to_russian):
            translated = await san.translate_to_russian(cleaned_text)
        else:
            translated = await translate_to_russian(cleaned_text)

        translated_clean = san.clean_text(translated)

        # Verification checkpoint: Must be predominantly Cyrillic and free of English markers
        if not is_predominantly_cyrillic(translated_clean) or has_untranslated_english_markers(translated_clean):
            return SanitizedResult(
                is_valid=False,
                cleaned_text=translated_clean,
                reject_reason="untranslated_english",
            )

        if len(translated_clean) < min_length:
            return SanitizedResult(
                is_valid=False,
                cleaned_text=translated_clean,
                reject_reason="too_short",
            )

        return SanitizedResult(
            is_valid=True,
            cleaned_text=translated_clean,
            reject_reason=None,
        )
    except Exception as exc:
        return SanitizedResult(
            is_valid=False,
            cleaned_text=cleaned_text,
            reject_reason="untranslated_english",
        )


TextSanitizer.translate_to_russian = staticmethod(translate_to_russian)
TextSanitizer.sanitize_and_translate_content = staticmethod(sanitize_and_translate_content)



