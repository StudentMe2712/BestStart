---
name: scraper-anti-block
description: Reliable web scraping, RSS parsing, and Reddit API extraction strategies with anti-blocking safeguards.
---

# Scraper Anti-Block & Ingestion Guidelines

This skill defines rules for scraping RSS feeds, Reddit JSON APIs, and web portals while avoiding rate limits and IP bans.

## Rules & Best Practices

1. **Compliant & Realistic User-Agents:**
   - Always supply a descriptive User-Agent.
   - For Reddit: `desktop:TrendScanner:v1.0.0 (by /u/trendscanner_bot)`
   - For RSS: `TrendScanner-RSS/1.0 (+https://github.com/BestStart/TrendScanner)`

2. **Error Isolation:**
   - Every extractor must catch network errors (`httpx.ConnectError`, `httpx.TimeoutException`, `httpx.HTTPStatusError`) and return an empty list `[]` with warning logs, never letting an individual feed crash the whole scheduler.

3. **Reddit API Specifics:**
   - Convert standard URLs (`r/Entrepreneur`, `https://reddit.com/r/...`) into `.json?limit=25` endpoints.
   - Respect HTTP 429 and filter out moderator stickied threads unless explicitly requested.

4. **Multi-layer Text Sanitization:**
   - Strip all `<script>`, `<style>`, and raw HTML tags.
   - Convert Markdown links to readable text.
   - Normalize Unicode to NFKC and remove zero-width and control characters.
   - Reject short texts (< 100 characters) and spam patterns using precompiled regex filters before hitting LLMs.
