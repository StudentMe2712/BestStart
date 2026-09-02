import asyncio
import json
import os
import sys

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../.."))
if PROJECT_ROOT not in sys.path:
    sys.path.insert(0, PROJECT_ROOT)

from groq import AsyncGroq
from backend.core.config import get_settings

async def debug_prompts():
    settings = get_settings()
    client = AsyncGroq(api_key=settings.GROQ_API_KEY)
    
    candidates = ['openai/gpt-oss-20b', 'groq/compound-mini', 'groq/compound', 'qwen/qwen3.8-27b', 'allam-2-7b']
    
    system_prompt = """You are an expert flight assistant. Output strict JSON only.
Schema:
{
  "origin": "3-letter IATA",
  "destination": "3-letter IATA",
  "date": "YYYY-MM-DD",
  "flight_number": null,
  "direct_only": true,
  "target_price": null,
  "currency_detected": null,
  "original_price": null,
  "interval_minutes": 5,
  "confidence": 0.9,
  "raw_explanation": "Summary"
}
"""

    for model in candidates:
        print(f"\n--- Testing Model: {model} with max_tokens=1000 ---")
        try:
            resp = await client.chat.completions.create(
                model=model,
                messages=[
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": "Алматы - Чэнду 21 ноября"}
                ],
                response_format={"type": "json_object"},
                temperature=0.0,
                max_tokens=1000
            )
            content = resp.choices[0].message.content
            print(f"SUCCESS with {model}:\n{content}")
        except Exception as e:
            print(f"FAILED with {model}: {e}")

if __name__ == "__main__":
    asyncio.run(debug_prompts())
