# Base Playwright image with pre-installed browsers and system dependencies
FROM mcr.microsoft.com/playwright/python:v1.40.0-jammy

# Set environment variables
ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    DEBIAN_FRONTEND=noninteractive \
    DATABASE_PATH=/app/data/sniper.db \
    PYTHONPATH=/app

# Set working directory
WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y tzdata && rm -rf /var/lib/apt/lists/*

# Install Python dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir --upgrade pip && \
    pip install --no-cache-dir -r requirements.txt

# Ensure Chromium browser is ready
RUN playwright install chromium

# Create data directory for SQLite database storage
RUN mkdir -p /app/data

# Copy backend codebase
COPY . .

# Create symlink so 'import backend.xxx' resolves when /app is working directory
RUN ln -s /app /app/backend || true

# Expose default application port
EXPOSE 8000

# Default entrypoint for the service
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
