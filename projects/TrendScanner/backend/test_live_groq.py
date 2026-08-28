"""Live test script for Groq API Key validation and trend classification."""

import asyncio
import os
from pathlib import Path
from dotenv import load_dotenv

# Load from projects/TrendScanner/.env or local .env
root_env = Path(__file__).resolve().parent.parent / ".env"
if root_env.exists():
    load_dotenv(dotenv_path=root_env, override=True)
load_dotenv(override=True)

import pytest
from app.core.settings import settings
from app.services.groq_client import GroqClient

@pytest.mark.asyncio
async def test_live_groq():
    api_key = os.getenv("GROQ_API_KEY") or settings.GROQ_API_KEY
    print(f"Testing Groq API with model: {settings.GROQ_MODEL}")
    if not api_key or api_key == "your_groq_api_key_here":
        print("[ERROR] GROQ_API_KEY is not set or still has default placeholder.")
        return False
    
    masked_key = api_key[:7] + "..." + api_key[-4:] if len(api_key) > 12 else "***"
    print(f"API Key detected: {masked_key}")

    client = GroqClient(api_key=api_key, model=settings.GROQ_MODEL)
    test_text = (
        "We built a lightweight automated invoice chasing tool for freelancers that connects with "
        "Stripe and sends gentle WhatsApp/Telegram reminders. It hit $4,200 MRR in 3 months with zero churn."
    )
    print(f"\nSending sample trend text to Groq API:\n\"{test_text}\"\n")
    
    try:
        result = await client.classify_text(test_text)
        if result:
            print("[SUCCESS] Groq API Response Received and Parsed:")
            print(f" - is_trend: {result.is_trend}")
            print(f" - trend_name: {result.trend_name}")
            print(f" - ai_score: {result.ai_score}/10")
            print(f" - scam_probability: {result.scam_probability}%")
            print(f" - ai_summary: {result.ai_summary}")
            return True
        else:
            print("[ERROR] Groq API returned None or failed parsing.")
            return False
    except Exception as exc:
        print(f"[EXCEPTION] Failed during Groq API call: {exc}")
        return False

if __name__ == "__main__":
    success = asyncio.run(test_live_groq())
    exit(0 if success else 1)
