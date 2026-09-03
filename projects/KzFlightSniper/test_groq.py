import os
import json
import asyncio
import httpx
from dotenv import load_dotenv

load_dotenv()
GROQ_API_KEY = os.getenv("GROQ_API_KEY")

# Обновленный список актуальных моделей
MODELS_TO_TEST = [
    "qwen/qwen3.8-27b",
    "openai/gpt-oss-120b",
    "groq/compound"
]

async def check_model(model_name: str):
    url = "https://api.groq.com/openai/v1/chat/completions"
    headers = {
        "Authorization": f"Bearer {GROQ_API_KEY}",
        "Content-Type": "application/json"
    }

    # ВАЖНО: В system prompt жестко прописано слово "JSON"
    payload = {
        "model": model_name,
        "response_format": {"type": "json_object"},
        "messages": [
            {
                "role": "system",
                "content": (
                    "Ты AI-ассистент парсера авиабилетов. "
                    "Извлеки данные из текста и верни ответ СТРОГО В ФОРМАТЕ JSON. "
                    "Схема: {\"origin_iata\": \"STR\", \"destination_iata\": \"STR\", \"date\": \"YYYY-MM-DD\", \"target_price\": float | null}"
                )
            },
            {
                "role": "user",
                "content": "Алмата Ченду 21 ноября прямой рейс"
            }
        ],
        "temperature": 0.1 
    }

    async with httpx.AsyncClient() as client:
        print(f"\n🔄 Проверяем модель: {model_name}...")
        try:
            response = await client.post(url, headers=headers, json=payload, timeout=15.0)

            if response.status_code == 200:
                data = response.json()
                content = data['choices'][0]['message']['content']
                parsed_json = json.loads(content)
                print(f"✅ УСПЕХ! HTTP 200")
                print(f"📦 Парсинг JSON:\n{json.dumps(parsed_json, ensure_ascii=False, indent=2)}")
            else:
                print(f"❌ ОШИБКА: HTTP {response.status_code}")
                print(f"📝 Текст ответа: {response.text}")
                
        except Exception as e:
            print(f"⚠️ Исключение при подключении: {e}")

async def main():
    if not GROQ_API_KEY:
        print("❌ ОШИБКА: GROQ_API_KEY не найден.")
        return

    print("🚀 Запуск тестирования актуальных моделей Groq...")
    for model in MODELS_TO_TEST:
        await check_model(model)

if __name__ == "__main__":
    asyncio.run(main())