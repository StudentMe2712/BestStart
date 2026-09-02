import asyncio
import json
import os
import sys

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../.."))
if PROJECT_ROOT not in sys.path:
    sys.path.insert(0, PROJECT_ROOT)

from groq import AsyncGroq
from backend.core.config import get_settings
from backend.bot.nlp_parser import parse_search_query, parse_interval_nlp, parse_flight_request

async def test_candidates():
    settings = get_settings()
    client = AsyncGroq(api_key=settings.GROQ_API_KEY)
    
    candidates = ['openai/gpt-oss-20b', 'groq/compound-mini', 'groq/compound', 'qwen/qwen3.8-27b', 'allam-2-7b']
    
    for model in candidates:
        print(f"\n==========================================")
        print(f"EVALUATING MODEL: {model}")
        print(f"==========================================")
        
        # Test 1: Search query
        try:
            res1 = await parse_search_query("Алматы - Чэнду 21 ноября", model=model)
            print(f"1. Search query: {res1.origin} -> {res1.destination} on {res1.date}")
        except Exception as e:
            print(f"1. Search query FAILED: {e}")
            
        # Test 2: Interval
        try:
            res2 = await parse_interval_nlp("проверять каждые 15 минут", model=model)
            print(f"2. Interval: {res2} min")
        except Exception as e:
            print(f"2. Interval FAILED: {e}")
            
        # Test 3: Flight request
        try:
            res3 = await parse_flight_request("Рейс Алматы - Бангкок, 15 октября 2026, прямой, KC-871, ниже 300$. Проверять каждые 5 минут", model=model)
            print(f"3. Flight request: {res3.origin} -> {res3.destination} on {res3.date}, price={res3.target_price}, flight={res3.flight_number}, interval={res3.interval_minutes}")
        except Exception as e:
            print(f"3. Flight request FAILED: {e}")

if __name__ == "__main__":
    asyncio.run(test_candidates())
