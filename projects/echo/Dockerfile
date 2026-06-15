FROM python:3.12-slim

WORKDIR /app

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1

COPY requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt

COPY app ./app

# SQLite lives here; mounted as a volume so it survives container restarts.
RUN mkdir -p /app/data

CMD ["python", "-m", "app.main"]
