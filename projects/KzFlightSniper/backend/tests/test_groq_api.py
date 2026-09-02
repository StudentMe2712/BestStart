"""Test and diagnostic script for Groq API integration and NLP parsing in KzFlightSniper.

Validates:
1. Groq API Key authentication and connection.
2. Available model listing via client.models.list().
3. JSON object response format functionality with explicit JSON keyword in system prompts.
4. Parsing functions: parse_search_query, parse_interval_nlp, parse_flight_request.
5. Verifies valid JSON responses and error-free API operation (HTTP 200).

Can be run directly:
    python backend/tests/test_groq_api.py
or with pytest:
    pytest backend/tests/test_groq_api.py
"""

import asyncio
from datetime import date
import json
import os
import sys
from typing import Any, Dict, List, Optional
import pytest

# Configure UTF-8 stdout encoding for Windows console compatibility
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

# Add project root to sys.path
PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../.."))
if PROJECT_ROOT not in sys.path:
    sys.path.insert(0, PROJECT_ROOT)

from backend.bot.nlp_parser import (
    parse_flight_request,
    parse_interval_nlp,
    parse_search_query,
)
from backend.core.config import get_settings
from backend.core.models import ParsedFlightIntent


async def inspect_and_test_groq(
    api_key: Optional[str] = None,
    model_override: Optional[str] = None,
) -> Dict[str, Any]:
    """Inspect Groq API models and verify NLP parsing with live LLM."""
    settings = get_settings()
    key = api_key or settings.GROQ_API_KEY
    if not key or key == "placeholder_token" or key.startswith("your_"):
        raise ValueError("GROQ_API_KEY is not configured or is a placeholder.")

    print("=" * 80)
    print("⚡ GROQ API DIAGNOSTIC & TESTING SUITE")
    print("=" * 80)
    print(f"🔑 API Key: {key[:8]}...{key[-4:]} (Length: {len(key)})")

    from groq import AsyncGroq

    client = AsyncGroq(api_key=key)

    # 1. List all available models
    print("\n📋 STEP 1: Querying available Groq models (client.models.list())...")
    model_list_resp = await client.models.list()
    available_model_ids = [m.id for m in model_list_resp.data]
    print(f"  Found {len(available_model_ids)} available models:")
    selected_model = model_override or settings.GROQ_MODEL
    for mid in sorted(available_model_ids):
        active_mark = " 🎯 [SELECTED IN CONFIG]" if mid == selected_model else ""
        print(f"   • {mid}{active_mark}")

    # Determine model to use
    if selected_model not in available_model_ids:
        print(f"\n⚠️  Configured model '{selected_model}' NOT found in active model list!")
        # Find best fallback from available models
        preferred_candidates = [
            "openai/gpt-oss-20b",
            "groq/compound-mini",
            "groq/compound",
            "qwen/qwen3.8-27b",
            "openai/gpt-oss-safeguard-20b",
            "allam-2-7b",
        ]
        for candidate in preferred_candidates:
            if candidate in available_model_ids:
                selected_model = candidate
                print(f"  👉 Auto-selected recommended model: '{selected_model}'")
                break
        else:
            selected_model = available_model_ids[0]
            print(f"  👉 Falling back to first available model: '{selected_model}'")
    else:
        print(f"\n✅ Configured model '{selected_model}' is verified and available in Groq model catalog.")

    # 2. Test flight intent parsing query with response_format={"type": "json_object"}
    # System prompt explicitly contains the word "JSON"
    print(f"\n🧪 STEP 2: Testing flight intent JSON completion with '{selected_model}'...")
    system_prompt_test = """You are an expert flight extraction assistant for Kazakhstan and international routes.
You MUST output a strict valid JSON object only according to the following schema:
{
  "origin": "3-letter IATA code (e.g. ALA for Almaty, NQZ for Astana)",
  "destination": "3-letter IATA code (e.g. CTU for Chengdu, BKK for Bangkok)",
  "date": "YYYY-MM-DD"
}
"""
    raw_response = await client.chat.completions.create(
        model=selected_model,
        messages=[
            {"role": "system", "content": system_prompt_test},
            {"role": "user", "content": "Рейс Алматы - Чэнду 21 ноября"},
        ],
        response_format={"type": "json_object"},
        temperature=0.0,
        max_tokens=200,
    )
    raw_content = raw_response.choices[0].message.content
    print(f"  Raw JSON Output:\n  {raw_content}")
    assert raw_content is not None, "Groq returned empty response"
    parsed_raw = json.loads(raw_content)
    assert parsed_raw.get("origin") == "ALA", f"Expected origin 'ALA', got {parsed_raw.get('origin')}"
    assert parsed_raw.get("destination") == "CTU", f"Expected destination 'CTU', got {parsed_raw.get('destination')}"
    print("  ✅ Step 2 direct JSON object completion verified successfully (HTTP 200 OK)!")

    # 3. Test prompt type 1: parse_search_query
    print("\n🔍 STEP 3: Testing Prompt Type 1 (parse_search_query) with live Groq LLM...")
    search_query = "Алматы - Чэнду 21 ноября"
    ref_date = date(2026, 9, 1)
    search_intent = await parse_search_query(
        search_query,
        api_key=key,
        model=selected_model,
        base_date=ref_date,
    )
    print(f"  Query: '{search_query}'")
    print(f"  Parsed Intent: {search_intent}")
    assert search_intent is not None, "parse_search_query returned None"
    assert search_intent.origin == "ALA", f"Expected ALA, got {search_intent.origin}"
    assert search_intent.destination == "CTU", f"Expected CTU, got {search_intent.destination}"
    assert search_intent.date == "2026-11-21", f"Expected 2026-11-21, got {search_intent.date}"
    assert search_intent.direct_only is True
    print("  ✅ Prompt Type 1 (parse_search_query) passed successfully!")

    # 4. Test prompt type 2: parse_interval_nlp
    print("\n⏱️ STEP 4: Testing Prompt Type 2 (parse_interval_nlp) with live Groq LLM...")
    interval_query = "проверять каждые 15 минут"
    interval_result = await parse_interval_nlp(
        interval_query,
        api_key=key,
        model=selected_model,
    )
    print(f"  Query: '{interval_query}'")
    print(f"  Parsed Interval: {interval_result} minutes")
    assert interval_result == 15, f"Expected 15, got {interval_result}"
    print("  ✅ Prompt Type 2 (parse_interval_nlp) passed successfully!")

    # 5. Test prompt type 3: parse_flight_request
    print("\n✈️ STEP 5: Testing Prompt Type 3 (parse_flight_request) with live Groq LLM...")
    full_query = "Рейс Алматы - Бангкок, 15 октября 2026, прямой, KC-871, ниже 300$. Проверять каждые 5 минут"
    flight_intent = await parse_flight_request(
        full_query,
        api_key=key,
        model=selected_model,
        base_date=ref_date,
    )
    print(f"  Query: '{full_query}'")
    print(f"  Parsed Flight Intent: {flight_intent}")
    assert flight_intent is not None, "parse_flight_request returned None"
    assert flight_intent.origin == "ALA"
    assert flight_intent.destination == "BKK"
    assert flight_intent.date == "2026-10-15"
    assert flight_intent.flight_number == "KC-871"
    assert flight_intent.direct_only is True
    assert flight_intent.currency_detected == "USD"
    assert flight_intent.original_price == 300.0
    assert flight_intent.target_price == 150000.0  # 300 * 500
    assert flight_intent.interval_minutes == 5
    print("  ✅ Prompt Type 3 (parse_flight_request) passed successfully!")

    print("\n" + "=" * 80)
    print("🎉 ALL GROQ API TESTS COMPLETED SUCCESSFULLY!")
    print(f"🎯 Verified Working Model: {selected_model}")
    print("=" * 80)

    return {
        "status": "success",
        "models": available_model_ids,
        "selected_model": selected_model,
        "search_intent": search_intent,
        "interval": interval_result,
        "flight_intent": flight_intent,
    }


@pytest.mark.asyncio
async def test_groq_api_live() -> None:
    """Pytest test case for live Groq API testing."""
    settings = get_settings()
    if not settings.is_groq_configured:
        pytest.skip("GROQ_API_KEY not configured, skipping live Groq API test.")
    result = await inspect_and_test_groq()
    assert result["status"] == "success"
    assert result["selected_model"] == settings.GROQ_MODEL
    assert result["search_intent"].origin == "ALA"
    assert result["search_intent"].destination == "CTU"
    assert result["interval"] == 15
    assert result["flight_intent"].origin == "ALA"
    assert result["flight_intent"].destination == "BKK"


if __name__ == "__main__":
    try:
        asyncio.run(inspect_and_test_groq())
        sys.exit(0)
    except Exception as e:
        print(f"\n❌ ERROR during Groq API test execution: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
