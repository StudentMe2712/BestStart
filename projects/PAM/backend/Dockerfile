FROM python:3.11-slim

WORKDIR /app

# System deps
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential libpq-dev curl \
    && rm -rf /var/lib/apt/lists/*

# Python deps — install from pyproject.toml so the full dependency set
# (httpx, beautifulsoup4, pypdf, youtube-transcript-api, pgvector, …) stays
# in sync with the code. Hardcoding a subset here drifted and broke imports.
COPY . .
RUN pip install --no-cache-dir --upgrade pip && \
    pip install --no-cache-dir -e ".[dev]"

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000", "--reload"]
