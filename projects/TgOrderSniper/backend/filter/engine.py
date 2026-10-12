"""Rule-based Filter Engine for Telegram Orders.

Implements a multi-stage pipeline:
1. Quick length cutoff (< 15 characters)
2. Strict Negative Gatekeeper (drops resumes, spam, crypto, scam)
3. Positive Commercial Demand Matcher (requires at least one commercial demand trigger)
4. Metadata, category, and budget extractor
5. Text normalization and FilteredOrder generation
"""

import logging
import re
from typing import Any, Union

from pydantic import BaseModel, Field

logger = logging.getLogger(__name__)


class FilteredOrder(BaseModel):
    """Normalized order entity passing all negative and positive filters."""

    chat_id: Union[int, str] = Field(description="Telegram chat ID or username")
    chat_title: str = Field(description="Title or display name of the chat")
    message_id: int = Field(description="Message ID inside the chat")
    raw_text: str = Field(description="Original raw message text")
    cleaned_text: str = Field(description="Sanitized and formatted message text")
    author_id: int | None = Field(default=None, description="Telegram user ID of the sender")
    author_username: str | None = Field(
        default=None, description="Telegram username of the sender (without @)"
    )
    author_name: str | None = Field(default=None, description="Full name or title of the sender")
    message_url: str = Field(
        default="", description="Clickable direct deep-link to the message"
    )
    matched_triggers: list[str] = Field(
        default_factory=list, description="List of positive commercial triggers matched"
    )
    detected_budget: str | None = Field(
        default=None, description="Extracted budget string with currency"
    )
    category_tags: list[str] = Field(
        default_factory=list, description="Categorized technology and domain tags"
    )
    ai_analysis: Any | None = Field(
        default=None, description="Optional AI scoring and pitch result"
    )



class FilterEngine:
    """High-performance filter engine for Telegram messages."""

    # Default Negative Gatekeeper: Resumes / Job seekers
    DEFAULT_RESUME_STOP_WORDS: list[str] = [
        "ищу работу",
        "обо мне",
        "навыки",
        "портфолио",
        "опыт работы:",
        "опыт работы",
        "ищу проект",
        "готов работать",
        "junior",
        "моё резюме",
        "мое резюме",
        "ищу заказы",
        "готов к сотрудничеству",
        "ищу стажировку",
        "fullstack разработчик ищет",
        "разработчик ищет",
        "ищу вакансию",
        "в поисках работы",
        "в поисках проекта",
        "рассмотрю предложения",
        "ищу удаленную работу",
        "мои кейсы",
        "о себе:",
        "обо мне:",
        "готов взять проект",
        "ищу подработку",
        "возьму проект",
        "возьму заказ",
        "мой стек",
        "мои навыки",
    ]

    # Default Negative Gatekeeper: Spam, Scam, Info-gypsy, Gambling
    DEFAULT_SPAM_STOP_WORDS: list[str] = [
        "крипта",
        "казино",
        "раскрутка",
        "сигналы",
        "пассивный доход",
        "заработок от",
        "вход в канал",
        "подпишись",
        "бесплатный вебинар",
        "дарю гайд",
        "слив курсов",
        "ставка",
        "ставки",
        "1win",
        "арбитраж",
        "заработок в интернете",
        "схема заработка",
        "лёгкие деньги",
        "легкие деньги",
        "быстрый заработок",
        "переходи по ссылке",
        "vip канал",
        "вип канал",
        "подписывайся",
    ]

    # Default Positive Triggers: Commercial demand from clients
    DEFAULT_POSITIVE_TRIGGERS: list[str] = [
        "срочно",
        "заказ",
        "требуется",
        "нужен",
        "нужна",
        "нужно",
        "нужны",
        "ищем",
        "ищу разработчика",
        "ищу программиста",
        "ищу исполнителя",
        "ищу специалиста",
        "исправить",
        "доработать",
        "доработка",
        "написать",
        "разработать",
        "сделать",
        "под ключ",
        "тз",
        "техзадание",
        "техническое задание",
        "оплата",
        "бюджет",
        "поправить",
        "задача",
        "проект",
        "требуются",
    ]

    # Tech stack & domain categories
    DEFAULT_CATEGORY_PATTERNS: dict[str, str] = {
        "телеграм бот": r"\b(?:бот(?:[а-я]{1,3})?|телеграм[- ]?бот(?:[а-я]{1,3})?|telegram[- ]?bot|aiogram|telethon|pyrogram)\b",
        "парсер": r"\b(?:парсер(?:[а-я]{1,3})?|парсинг(?:[а-я]{1,3})?)\b",
        "скрипт": r"\bскрипт(?:[а-я]{1,3})?\b",
        "1с": r"\b(?:1с|1c)\b",
        "python": r"\b(?:python|питон(?:[а-я]{1,3})?)\b",
        "api": r"\bapi\b",
        "интеграция": r"\b(?:интеграци[а-я]+|выгрузк[а-я]+)\b",
        "база данных": r"\b(?:баз[а-я]+\s+данных|бд|postgresql|postgres|mysql)\b",
        "сайт": r"\b(?:сайт(?:[а-я]{1,3})?|лендинг(?:[а-я]{1,3})?|веб|web)\b",
        "django": r"\bdjango\b",
        "fastapi": r"\bfastapi\b",
        "aiogram": r"\baiogram\b",
    }

    # Regex patterns for budget detection
    BUDGET_REGEXES: list[re.Pattern] = [
        # 1. Range with currency (от 10 000 до 20 000 руб / 10-20к / $500-$1000)
        re.compile(
            r"(?:от\s*)?(\d+[\s\d\.,]*)\s*(?:до|-|—|–)\s*(\d+[\s\d\.,]*)\s*(?:руб(?:лей|ля)?|р\b|₽|usd|\$|eur|€|тг|kzt|к\b|k\b)",
            re.IGNORECASE,
        ),
        # 2. Currency symbol before number: $500, €300, ₽10 000
        re.compile(r"([\$€₽])\s*(\d+[\s\d\.,]*)", re.IGNORECASE),
        # 3. Number + currency: 15 000 руб / 500$ / 10к / 50 000 ₽ / 100 usd / 50000 kzt
        re.compile(
            r"(\d+[\s\d\.,]*)\s*(?:руб(?:лей|ля)?|р\b|₽|usd|\$|eur|€|тг|kzt|к\b|k\b)",
            re.IGNORECASE,
        ),
        # 4. Explicit label: Бюджет: 30000 / Оплата: 50к / Стоимость 50 000
        re.compile(
            r"(?:бюджет|оплата|цена|стоимость)\s*[:—–\-]?\s*(\d+[\s\d\.,]*(?:\s*(?:руб(?:лей|ля)?|р\b|₽|\$|€|k|к|usd|eur|тг|kzt))?)",
            re.IGNORECASE,
        ),
    ]

    def __init__(
        self,
        resume_stop_words: list[str] | None = None,
        spam_stop_words: list[str] | None = None,
        positive_triggers: list[str] | None = None,
        category_patterns: dict[str, str] | None = None,
        min_length: int = 15,
    ) -> None:
        """Initializes the filter engine with configurable rules."""
        self.min_length = min_length
        self.resume_stop_words = (
            resume_stop_words
            if resume_stop_words is not None
            else list(self.DEFAULT_RESUME_STOP_WORDS)
        )
        self.spam_stop_words = (
            spam_stop_words
            if spam_stop_words is not None
            else list(self.DEFAULT_SPAM_STOP_WORDS)
        )
        self.positive_triggers = (
            positive_triggers
            if positive_triggers is not None
            else list(self.DEFAULT_POSITIVE_TRIGGERS)
        )
        self.category_patterns = (
            category_patterns
            if category_patterns is not None
            else dict(self.DEFAULT_CATEGORY_PATTERNS)
        )

        # Precompile regexes for fast performance
        self._compiled_negative: list[tuple[str, re.Pattern]] = []
        for word in self.resume_stop_words + self.spam_stop_words:
            clean_word = word.strip().lower()
            if not clean_word:
                continue
            # Build word boundary pattern
            if clean_word.endswith(":"):
                base = clean_word[:-1].strip()
                pat = re.compile(rf"\b{re.escape(base)}\s*:", re.IGNORECASE)
            elif " " in clean_word:
                # Multi-word phrase: match words with flexible whitespace
                tokens = clean_word.split()
                escaped_tokens = [re.escape(t) for t in tokens]
                whitespace_sep = r"\s+"
                pat_str = rf"\b{whitespace_sep.join(escaped_tokens)}\b"
                pat = re.compile(pat_str, re.IGNORECASE)
            else:
                pat = re.compile(rf"\b{re.escape(clean_word)}\b", re.IGNORECASE)
            self._compiled_negative.append((clean_word, pat))

        # Precompile positive triggers
        self._compiled_positive: list[tuple[str, re.Pattern]] = []
        for trig in self.positive_triggers:
            clean_trig = trig.strip().lower()
            if not clean_trig:
                continue
            # Support Russian inflections for commercial triggers
            pattern_str = self._build_inflected_trigger_pattern(clean_trig)
            self._compiled_positive.append((clean_trig, re.compile(pattern_str, re.IGNORECASE)))

        # Precompile category patterns
        self._compiled_categories: list[tuple[str, re.Pattern]] = [
            (name, re.compile(pat, re.IGNORECASE))
            for name, pat in self.category_patterns.items()
        ]

    @staticmethod
    def _build_inflected_trigger_pattern(trigger: str) -> str:
        """Builds regex pattern supporting common Russian inflection suffixes."""
        if " " in trigger:
            tokens = trigger.split()
            escaped_tokens = [re.escape(t) for t in tokens]
            whitespace_sep = r"\s+"
            return rf"\b{whitespace_sep.join(escaped_tokens)}\b"

        # Specific inflected patterns
        inflections = {
            "заказ": r"\bзаказ(?:[а-я]{1,3})?\b",
            "требуется": r"\bтребу(?:ется|ются|ем)?\b",
            "требуются": r"\bтребу(?:ется|ются|ем)?\b",
            "нужен": r"\bнуж(?:ен|на|но|ны)\b",
            "нужна": r"\bнуж(?:ен|на|но|ны)\b",
            "нужно": r"\bнуж(?:ен|на|но|ны)\b",
            "нужны": r"\bнуж(?:ен|на|но|ны)\b",
            "ищем": r"\bищ(?:ем|у)\b",
            "исправить": r"\bисправ(?:ить|ление|ления|ь)?\b",
            "доработать": r"\bдоработ(?:ать|ка|ки|ку|аем)?\b",
            "доработка": r"\bдоработ(?:ать|ка|ки|ку|аем)?\b",
            "написать": r"\bнапис(?:ать|ано)?\b",
            "разработать": r"\bразработ(?:ать|ка|ку|ки|ан)?\b",
            "сделать": r"\bсдел(?:ать|аем|ано)?\b",
            "техзадание": r"\b(?:техзадани[а-я]+|техническ[а-я]+\s+задани[а-я]+)\b",
            "оплата": r"\bоплат(?:[а-я]{1,3})?\b",
            "бюджет": r"\bбюджет(?:[а-я]{1,3})?\b",
            "поправить": r"\bпоправ(?:ить|ка|ки)?\b",
            "задача": r"\bзадач(?:[а-я]{1,3})?\b",
            "проект": r"\bпроект(?:[а-я]{1,3})?\b",
        }
        return inflections.get(trigger, rf"\b{re.escape(trigger)}\b")

    def check_negative_gatekeeper(self, text: str) -> str | None:
        """Checks if text contains any stop-words from resume or spam lists.

        Returns:
            The matched stop-word if found, otherwise None.
        """
        for word, pat in self._compiled_negative:
            if pat.search(text):
                return word
        return None

    def match_positive_triggers(self, text: str) -> list[str]:
        """Finds all commercial demand triggers present in the text.

        Returns:
            List of matched positive triggers.
        """
        matched = []
        for trig, pat in self._compiled_positive:
            if pat.search(text):
                matched.append(trig)
        return matched

    def match_categories(self, text: str) -> list[str]:
        """Detects category and tech stack tags in the text.

        Returns:
            List of detected categories.
        """
        matched = []
        for name, pat in self._compiled_categories:
            if pat.search(text):
                matched.append(name)
        return matched

    def extract_budget(self, text: str) -> str | None:
        """Extracts and normalizes budget value with currency from text.

        Args:
            text: Message text.

        Returns:
            Clean budget string (e.g. '15 000 руб', '$500', '10к') or None.
        """
        for pattern in self.BUDGET_REGEXES:
            match = pattern.search(text)
            if match:
                raw_match = match.group(0).strip()
                # Remove prefixes like 'бюджет:', 'оплата:'
                cleaned = re.sub(
                    r"^(?:бюджет|оплата|цена|стоимость)\s*[:—–\-]?\s*",
                    "",
                    raw_match,
                    flags=re.IGNORECASE,
                ).strip()
                if cleaned:
                    cleaned = cleaned.rstrip(".,;:! ")
                    return cleaned
        return None

    @staticmethod
    def clean_text(text: str) -> str:
        """Sanitizes text by removing redundant blank lines and trailing spaces."""
        lines = [line.strip() for line in text.strip().splitlines()]
        cleaned_lines = []
        empty_line_count = 0

        for line in lines:
            if not line:
                empty_line_count += 1
                if empty_line_count == 1:
                    cleaned_lines.append("")
            else:
                empty_line_count = 0
                cleaned_lines.append(line)

        return "\n".join(cleaned_lines).strip()

    def evaluate(
        self,
        text: str,
        chat_title: str,
        chat_id: Union[int, str],
        message_id: int,
        sender: Any = None,
        message_url: str = "",
    ) -> FilteredOrder | None:
        """Evaluates a message against the filter rules.

        Step 1: Length cutoff (< 15 characters) -> return None
        Step 2: Strict Negative Gatekeeper (stop-words) -> return None
        Step 3: Positive Triggers (at least one) -> return None if none found
        Step 4: Extract budget
        Step 5: Determine categories
        Step 6: Clean text and build FilteredOrder

        Args:
            text: Message body.
            chat_title: Name of the chat/channel.
            chat_id: ID or username of the chat.
            message_id: Telegram message ID.
            sender: Optional sender entity or dict with author info.
            message_url: Deep-link URL to the message.

        Returns:
            FilteredOrder if message meets all criteria, otherwise None.
        """
        # 1. Quick length cutoff
        if not text or len(text.strip()) < self.min_length:
            logger.debug(
                f"[FilterEngine] Dropped message {message_id} in {chat_title}: text length < {self.min_length}"
            )
            return None

        # 2. Strict Negative Gatekeeper
        matched_stop_word = self.check_negative_gatekeeper(text)
        if matched_stop_word:
            logger.debug(
                f"[FilterEngine] Dropped message {message_id} in {chat_title}: matched stop-word '{matched_stop_word}'"
            )
            return None

        # 3. Positive Trigger Matching
        matched_triggers = self.match_positive_triggers(text)
        if not matched_triggers:
            logger.debug(
                f"[FilterEngine] Dropped message {message_id} in {chat_title}: no commercial triggers matched"
            )
            return None

        # 4. Extract Budget
        detected_budget = self.extract_budget(text)

        # 5. Extract Category Tags
        category_tags = self.match_categories(text)

        # 6. Extract Sender Info
        author_id = None
        author_username = None
        author_name = None

        if sender is not None:
            if isinstance(sender, dict):
                author_id = sender.get("id") or sender.get("author_id")
                author_username = sender.get("username") or sender.get("author_username")
                author_name = sender.get("name") or sender.get("author_name")
            else:
                author_id = getattr(sender, "id", None)
                author_username = getattr(sender, "username", None)
                first_name = getattr(sender, "first_name", "") or ""
                last_name = getattr(sender, "last_name", "") or ""
                full_name = f"{first_name} {last_name}".strip()
                if not full_name:
                    full_name = (
                        getattr(sender, "title", None)
                        or author_username
                        or (str(author_id) if author_id else None)
                    )
                author_name = full_name

        cleaned_text = self.clean_text(text)

        order = FilteredOrder(
            chat_id=chat_id,
            chat_title=chat_title,
            message_id=message_id,
            raw_text=text,
            cleaned_text=cleaned_text,
            author_id=author_id,
            author_username=author_username,
            author_name=author_name,
            message_url=message_url,
            matched_triggers=matched_triggers,
            detected_budget=detected_budget,
            category_tags=category_tags,
        )

        logger.info(
            f"[FilterEngine] MATCHED ORDER {message_id} in '{chat_title}' | "
            f"Triggers: {matched_triggers} | Budget: {detected_budget} | Categories: {category_tags}"
        )
        return order
