"""Smart Deduplication Engine (Level 2) for TrendScanner.

Detects duplicate trends across diverse sources using:
1. Normalized canonical URL matching (excluding generic root/feed URLs).
2. Content hash matching.
3. Fuzzy similarity matching on titles and text snippets (difflib SequenceMatcher >= 0.85).
4. Multi-source context merging and mention tracking.
"""

import difflib
import re
from typing import Any, Dict, List, Optional
from urllib.parse import parse_qsl, urlencode, urlparse, urlunparse
from pydantic import BaseModel, Field


class DeduplicationResult(BaseModel):
    """Result of deduplication evaluation against existing trend records."""

    is_duplicate: bool = Field(..., description="Whether a duplicate trend was found")
    matched_trend_id: Optional[int] = Field(
        default=None, description="ID of the matching trend in DB if duplicate"
    )
    match_type: Optional[str] = Field(
        default=None,
        description="Match category: 'url_match' | 'fuzzy_match' | 'exact_hash'",
    )
    similarity_score: float = Field(
        default=0.0, description="Similarity ratio between 0.0 and 1.0"
    )


class DeduplicationEngine:
    """Level 2 Smart Deduplication Engine with URL canonicalization and fuzzy matching."""

    STRIP_PARAM_PREFIXES = ("utm_",)
    STRIP_PARAM_NAMES = {
        "ref",
        "ref_src",
        "referrer",
        "source",
        "fbclid",
        "gclid",
        "twclid",
        "igshid",
        "mc_cid",
        "mc_eid",
        "_hsenc",
        "_hsmi",
    }

    GENERIC_FEED_PATTERNS = (
        r"^https?://(?:www\.)?reddit\.com/?$",
        r"^https?://(?:www\.)?reddit\.com/r/[^/]+(?:/(?:hot|new|top|rising))?(?:\.json)?/?$",
        r"^https?://(?:www\.)?news\.ycombinator\.com(?:/rss|/newest|/news)?/?$",
        r"^https?://(?:www\.)?producthunt\.com/?$",
        r"^https?://(?:www\.)?indiehackers\.com(?:/products)?/?$",
        r"^https?://(?:www\.)?techcrunch\.com(?:/category/[^/]+/feed)?/?$",
    )

    def __init__(self) -> None:
        self._compiled_generic_patterns = [
            re.compile(pattern, re.IGNORECASE) for pattern in self.GENERIC_FEED_PATTERNS
        ]

    def normalize_url(self, url: str) -> str:
        """Normalize URL for consistent deduplication comparison.

        - Strips whitespace.
        - Lowercases netloc and path.
        - Strips standard 'www.' prefix for domain equivalence.
        - Removes tracking query parameters (utm_*, ref, etc.).
        - Strips trailing slashes.
        """
        if not url or not isinstance(url, str):
            return ""

        url_str = url.strip()
        if not url_str:
            return ""

        # Prepend scheme if missing
        if not re.match(r"^[a-zA-Z][a-zA-Z0-9+.-]*://", url_str):
            url_str = "https://" + url_str

        try:
            parsed = urlparse(url_str)
        except Exception:
            return url.strip().lower().rstrip("/")

        scheme = parsed.scheme.lower() if parsed.scheme else "https"
        netloc = parsed.netloc.lower()

        # Remove www. prefix for consistent comparison
        if netloc.startswith("www."):
            netloc = netloc[4:]

        # Remove standard default ports
        if netloc.endswith(":80"):
            netloc = netloc[:-3]
        elif netloc.endswith(":443"):
            netloc = netloc[:-4]

        # Lowercase and clean path
        path = parsed.path.lower()
        path = re.sub(r"/+", "/", path)
        path = path.rstrip("/")

        # Filter query parameters
        filtered_params: List[tuple[str, str]] = []
        if parsed.query:
            try:
                for k, v in parse_qsl(parsed.query, keep_blank_values=False):
                    k_lower = k.lower()
                    if any(
                        k_lower.startswith(prefix) for prefix in self.STRIP_PARAM_PREFIXES
                    ):
                        continue
                    if k_lower in self.STRIP_PARAM_NAMES:
                        continue
                    filtered_params.append((k_lower, v))
            except Exception:
                pass

        filtered_params.sort(key=lambda item: (item[0], item[1]))
        clean_query = urlencode(filtered_params)

        # Reconstruct normalized URL (ignore fragment)
        clean_url = urlunparse((scheme, netloc, path, "", clean_query, ""))
        return clean_url.rstrip("/")

    def is_generic_or_root_url(self, url: str) -> bool:
        """Check if URL points to a root homepage or generic feed listing rather than a specific post/product."""
        if not url:
            return True

        norm_url = self.normalize_url(url)
        if not norm_url:
            return True

        parsed = urlparse(norm_url)
        if parsed.path in ("", "/"):
            return True

        for pattern in self._compiled_generic_patterns:
            if pattern.search(norm_url) or pattern.search(url):
                return True

        if parsed.path.endswith((".rss", ".xml", ".atom", "/feed")):
            return True

        return False

    def calculate_similarity(self, text1: str, text2: str) -> float:
        """Calculate SequenceMatcher similarity ratio between two normalized strings."""
        if not text1 or not text2:
            return 0.0

        s1 = " ".join(str(text1).strip().lower().split())
        s2 = " ".join(str(text2).strip().lower().split())

        if not s1 or not s2:
            return 0.0

        if s1 == s2:
            return 1.0

        ratio = difflib.SequenceMatcher(None, s1, s2).ratio()
        return round(ratio, 4)

    def find_duplicate(
        self,
        title: str,
        text: str,
        url: str,
        candidates: List[Dict[str, Any]],
        threshold: float = 0.85,
    ) -> DeduplicationResult:
        """Identify if an item is a duplicate against a list of candidate trend dictionaries.

        Matching logic:
        1. Exact URL match (if non-empty, non-generic root).
        2. Exact text hash / identical content match.
        3. Fuzzy match: Title similarity >= threshold OR Text snippet similarity >= threshold.

        Returns DeduplicationResult.
        """
        if not candidates:
            return DeduplicationResult(
                is_duplicate=False,
                matched_trend_id=None,
                match_type=None,
                similarity_score=0.0,
            )

        norm_url = self.normalize_url(url)
        is_url_valid_for_match = bool(norm_url) and not self.is_generic_or_root_url(
            norm_url
        )

        # 1. Check direct external URL matches
        if is_url_valid_for_match:
            for candidate in candidates:
                cand_url = candidate.get("source_url") or candidate.get("url") or ""
                cand_norm_url = self.normalize_url(cand_url)
                if cand_norm_url and not self.is_generic_or_root_url(cand_norm_url):
                    if norm_url == cand_norm_url:
                        cand_id = candidate.get("id")
                        return DeduplicationResult(
                            is_duplicate=True,
                            matched_trend_id=cand_id,
                            match_type="url_match",
                            similarity_score=1.0,
                        )

        # 2. Check exact content equality
        norm_text = " ".join(text.strip().lower().split()) if text else ""
        if norm_text:
            for candidate in candidates:
                cand_text = (
                    candidate.get("original_text")
                    or candidate.get("text")
                    or candidate.get("ai_summary")
                    or ""
                )
                cand_norm_text = (
                    " ".join(str(cand_text).strip().lower().split()) if cand_text else ""
                )
                if cand_norm_text and norm_text == cand_norm_text:
                    cand_id = candidate.get("id")
                    return DeduplicationResult(
                        is_duplicate=True,
                        matched_trend_id=cand_id,
                        match_type="exact_hash",
                        similarity_score=1.0,
                    )

        # 3. Check Title similarity >= threshold OR Text snippet similarity >= threshold
        best_candidate_id: Optional[int] = None
        best_score: float = 0.0

        for candidate in candidates:
            cand_id = candidate.get("id")
            cand_title = candidate.get("trend_name") or candidate.get("title") or ""
            cand_text = (
                candidate.get("original_text")
                or candidate.get("text")
                or candidate.get("ai_summary")
                or ""
            )

            title_sim = (
                self.calculate_similarity(title, cand_title)
                if title and cand_title
                else 0.0
            )

            snippet1 = text[:500] if text else ""
            snippet2 = str(cand_text)[:500] if cand_text else ""
            text_sim = (
                self.calculate_similarity(snippet1, snippet2)
                if snippet1 and snippet2
                else 0.0
            )

            cand_best_sim = max(title_sim, text_sim)
            if cand_best_sim >= threshold and cand_best_sim > best_score:
                best_score = cand_best_sim
                best_candidate_id = cand_id

        if best_candidate_id is not None and best_score >= threshold:
            match_type = "exact_hash" if best_score >= 0.9999 else "fuzzy_match"
            return DeduplicationResult(
                is_duplicate=True,
                matched_trend_id=best_candidate_id,
                match_type=match_type,
                similarity_score=best_score,
            )

        return DeduplicationResult(
            is_duplicate=False,
            matched_trend_id=None,
            match_type=None,
            similarity_score=0.0,
        )

    def format_merged_text(
        self, existing_text: str, new_text: str, new_source_name: str
    ) -> str:
        """Append new source context cleanly into existing trend text."""
        if not existing_text:
            return f"[Дополнительное упоминание ({new_source_name})]:\n{new_text}"
        if not new_text:
            return existing_text
        return f"{existing_text}\n\n[Дополнительное упоминание ({new_source_name})]:\n{new_text}"


deduplicator = DeduplicationEngine()
