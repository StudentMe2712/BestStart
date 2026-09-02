import asyncio
import json
import os
import sys

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "../.."))
if PROJECT_ROOT not in sys.path:
    sys.path.insert(0, PROJECT_ROOT)

from groq import AsyncGroq
from backend.core.config import get_settings

async def probe():
    settings = get_settings()
    client = AsyncGroq(api_key=settings.GROQ_API_KEY)
    model_list = await client.models.list()
    ids = [m.id for m in model_list.data]
    print(f"Total models returned by list: {len(ids)}")
    print(ids)

    # Let's test list models + standard candidates
    test_models = ids + [
        "llama-3.3-70b-versatile",
        "llama-3.1-8b-instant",
        "llama-3.1-70b-versatile",
        "llama3-70b-8192",
        "llama3-8b-8192",
        "mixtral-8x7b-32768",
        "gemma2-9b-it"
    ]

    working_models = []

    for model in test_models:
        print(f"\n--- Testing model: {model} ---")
        try:
            resp = await client.chat.completions.create(
                model=model,
                messages=[
                    {"role": "system", "content": "You are a JSON assistant. Return valid JSON only."},
                    {"role": "user", "content": "Hello! Output JSON with key status: ok"}
                ],
                response_format={"type": "json_object"},
                temperature=0.1,
                max_tokens=50
            )
            print(f"SUCCESS with {model}: {resp.choices[0].message.content}")
            working_models.append(model)
        except Exception as e:
            print(f"FAILED with {model}: {e}")

    print("\n===============================")
    print(f"WORKING MODELS: {working_models}")
    print("===============================")

if __name__ == "__main__":
    asyncio.run(probe())
