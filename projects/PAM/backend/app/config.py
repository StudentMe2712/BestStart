from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Application settings, loaded from environment variables."""

    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    DATABASE_URL: str = "postgresql+asyncpg://pam:pam@localhost:5432/pam"
    CORS_ORIGINS: str = "chrome-extension://*,http://localhost:3000"
    LOG_LEVEL: str = "INFO"

    # Phase 2 — local embeddings via Ollama
    OLLAMA_URL: str = "http://localhost:11434"
    EMBED_MODEL: str = "nomic-embed-text"

    # Phase 4 — chat LLM (provider-agnostic).
    #   "groq" (cloud, free, fast) | "openrouter" (cloud, free/paid, много моделей) | "ollama" (local)
    LLM_PROVIDER: str = "groq"
    GROQ_API_KEY: str = ""
    # gpt-oss-120b — самая сильная open-модель на Groq (reasoning в отдельном
    # поле, в content не течёт), быстрая и многоязычная. Можно вернуть
    # llama-3.3-70b-versatile / meta-llama/llama-4-scout-17b-16e-instruct.
    GROQ_MODEL: str = "openai/gpt-oss-120b"
    # JSON-mode completions (курсы, извлечение фактов) НЕЛЬЗЯ гонять на reasoning-
    # моделях: gpt-oss держит chain-of-thought в ответе и обрезает JSON → Groq
    # отдаёт 400 json_validate_failed (плюс free-tier лимит ~8000 токенов/мин).
    # Для структурированного JSON берём обычную (не-reasoning) модель.
    GROQ_JSON_MODEL: str = "llama-3.3-70b-versatile"
    # Vision-модель для распознавания вложенных изображений в чате (multimodal,
    # OpenAI-совместимо). llama-4-scout — мультимодальная и быстрая на Groq.
    GROQ_VISION_MODEL: str = "meta-llama/llama-4-scout-17b-16e-instruct"
    # OpenRouter — один ключ на десятки моделей (OpenAI-совместимо). Ключ:
    # https://openrouter.ai/keys (бесплатно, без карты). Сильные free-модели:
    # deepseek/deepseek-r1:free, deepseek/deepseek-chat:free, qwen/qwen3-235b-a22b:free.
    OPENROUTER_API_KEY: str = ""
    # Проверено живьём: быстрый (~5с), чистый ответ, поддержка JSON-mode, 1M контекст.
    OPENROUTER_MODEL: str = "nvidia/nemotron-3-super-120b-a12b:free"
    OLLAMA_CHAT_MODEL: str = "llama3.2:3b"

    # Project Memory (P2) — Telegram capture (long polling, без webhook/домена).
    # Токен и allow-list берутся из backend/.env (gitignored). user id =
    # единственный разрешённый отправитель. BACKEND_URL — куда бот шлёт элементы.
    TELEGRAM_BOT_TOKEN: str = ""
    TELEGRAM_ALLOWED_USER_ID: int = 0  # 0 = никого не пускать (защита по умолчанию)
    BACKEND_URL: str = "http://localhost:8000"

    @property
    def cors_origins_list(self) -> list[str]:
        return [o.strip() for o in self.CORS_ORIGINS.split(",") if o.strip()]


settings = Settings()
