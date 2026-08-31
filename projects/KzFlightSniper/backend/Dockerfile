# Base Playwright image with pre-installed browsers and dependencies
FROM mcr.microsoft.com/playwright/python:v1.40.0-jammy

# Set environment variables
ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    DEBIAN_FRONTEND=noninteractive \
    DATABASE_PATH=/app/data/sniper.db

# Set working directory
WORKDIR /app

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

# Expose default application port
EXPOSE 8000

# Default entrypoint for the service
CMD ["python", "-m", "uvicorn", "backend.main:app", "--host", "0.0.0.0", "--port", "8000"]
