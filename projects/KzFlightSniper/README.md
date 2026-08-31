# 🦅 KzFlightSniper

[![Python](https://img.shields.io/badge/Python-3.10+-3776AB.svg?style=flat&logo=python&logoColor=white)](https://www.python.org/)
[![FastAPI](https://img.shields.io/badge/FastAPI-0.110+-009688.svg?style=flat&logo=fastapi&logoColor=white)](https://fastapi.tiangolo.com/)
[![aiogram](https://img.shields.io/badge/aiogram-3.4+-2CA5E0.svg?style=flat&logo=telegram&logoColor=white)](https://docs.aiogram.dev/)
[![Playwright](https://img.shields.io/badge/Playwright-1.42+-2EAD33.svg?style=flat&logo=playwright&logoColor=white)](https://playwright.dev/python/)
[![SQLite](https://img.shields.io/badge/SQLite-aiosqlite-003B57.svg?style=flat&logo=sqlite&logoColor=white)](https://aiosqlite.omnilib.dev/)
[![Groq](https://img.shields.io/badge/LLM-Groq%20Llama%203.1-F55036.svg?style=flat)](https://groq.com/)
[![Tests](https://img.shields.io/badge/Tests-51%2F51%20passing-brightgreen.svg?style=flat)](file:///C:/Users/Mila/Desktop/BestStart/projects/KzFlightSniper/backend/tests/)

**KzFlightSniper** is an asynchronous flight price tracking and automated alerting engine built specifically for the **Kazakhstan aviation market** (covering Air Astana, FlyArystan, SCAT Airlines, and Qazaq Air across domestic routes like `ALA` ⇄ `NQZ`, `CIT`, `SCO`, `GUW`, `UKK`, and international connections like `BKK`, `DXB`, `IST`, `HKT`, `TAS`, `FRU`, `TBS`, `AYT`).

It runs continuous background checks using headless browser automation and stealth network interception, immediately dispatching rich Telegram notifications with direct booking links when flight prices drop below user-configured target thresholds.

---

## 📑 Table of Contents

- [Key Features](#-key-features)
- [NLP Natural Language Flight Creation](#-nlp-natural-language-flight-creation)
- [System Architecture](#-system-architecture)
- [Quickstart Guide (Docker Compose)](#-quickstart-guide-docker-compose)
- [Local Development Setup](#-local-development-setup-without-docker)
- [Telegram Bot Command Guide](#-telegram-bot-command-guide)
- [Kazakhstan & International Airport Codes](#-kazakhstan--international-airport-codes)
- [REST API Endpoints](#-rest-api-endpoints)
- [Configuration Reference](#-configuration-reference-env)
- [Testing & Manual Simulation](#-testing--manual-simulation)
- [License & Contributing](#-license--contributing)

---

## ⚡ Key Features

- 🧠 **Natural Language Intent Parsing (NLP)**: Create monitoring tasks by simply typing requests in Russian or English. Powered by **Groq Llama 3.1** with a resilient zero-dependency local heuristic fallback parser for 100% offline reliability.
- 💱 **Multi-Currency Auto-Conversion**: Automatically converts foreign currency budgets (USD, EUR, RUB) into Kazakhstani Tenge (KZT).
- ⏱️ **Custom Monitoring Intervals**: Configure independent checking intervals per flight task (e.g. every 5 minutes, 10 minutes, 30 minutes, 1 hour) with automated SQLite schema migrations.
- 🎯 **Target-Based Price Sniping**: Set maximum budget thresholds in Kazakhstani Tenge (₸) for any domestic or international route.
- ✈️ **Flight-Specific Filtering**: Monitor either the cheapest available flight on a date or track a specific flight number (e.g. `KC-871`, `IQ-401`, `DV-713`).
- 🥷 **Stealth Network Interception**: Employs Playwright with `playwright-stealth` and JSON response interception (`page.on("response", ...)`) to reliably extract live ticket data without fragile DOM scraping.
- 📬 **Instant Telegram Push Alerts**: Receive immediate HTML alerts with route details, savings calculations, and direct Aviata booking deep links.
- 🛡️ **Intelligent Deduplication**: Suppresses repetitive alert spam for unchanged prices within a configurable time window (default: 60 minutes), while instantly alerting on further price drops.
- 📊 **REST & Health API**: Integrated FastAPI server with `/health`, `/api/tasks`, and `/api/check-now` endpoints for manual inspections and container health checks.

---

## 💬 NLP Natural Language Flight Creation

Users can create flight monitoring tasks using freeform text without memorizing strict command syntax:

```text
User: "Рейс Алматы - Бангкок, 15 октября, прямой, KC-871, ниже 300$. Проверять каждые 5 минут"

Bot: 🔍 Распознаны параметры снайпера:
     ✈️ Маршрут: ALA ➡️ BKK
     📅 Дата: 2026-10-15
     💰 Целевая цена: ≤ 150 000 ₸ (300 USD ≈ 150 000 ₸)
     🔢 Рейс: KC-871
     🔀 Тип: Прямой рейс ⚡
     ⏱ Интервал проверки: Каждые 5 мин

     [✅ Подтвердить] [❌ Отмена]
```

Other supported phrases:
- *«Астана - Шымкент на 1 ноября до 20000 тг»*
- *«Хочу улететь из Актау в Дубай 25 декабря не дороже 80000 тенге, проверка раз в 10 минут»*
- *«Билет в Стамбул из Алматы завтра до 250 евро»*

---

## 🏛 System Architecture

```mermaid
graph TD
    User([Telegram User]) <-->|Natural Text & Commands| Bot[aiogram 3.x Telegram Bot]
    Bot -->|NLP Request| Parser[NLP Parser (Groq / Heuristic)]
    Parser -->|ParsedFlightIntent| Bot
    Bot <-->|Task CRUD & Alerts| DB[(aiosqlite SQLite Database)]
    
    Scheduler[APScheduler Engine (60s Tick)] -->|Periodic Due Check| Worker[Sniper Worker Engine]
    Worker -->|Fetch Due Tasks| DB
    Worker -->|Execute Route Search| Provider[Aviata Provider Adapter]
    
    Provider -->|Stealth Automation & Interception| AviataAPI[Aviata.kz Search API]
    AviataAPI -->|Raw JSON Flight Payloads| Provider
    Provider -->|Normalized FlightOffers| Worker
    
    Worker -->|Price <= Target & Deduplication OK| Dispatcher[Alert Dispatcher]
    Dispatcher -->|Dispatch HTML Notification| Bot
    Dispatcher -->|Record Sent Alert Log| DB
    
    FastAPI[FastAPI Web Server] -->|Healthcheck & Manual Trigger| Worker
```

---

## 🚀 Quickstart Guide (Docker Compose)

### 1. Clone & Navigate
```bash
cd projects/KzFlightSniper
```

### 2. Configure Environment Variables
Create `./backend/.env` from the example template:
```bash
cp backend/.env.example backend/.env
```
Edit `backend/.env` and insert your Telegram Bot token and optional Groq API key:
```env
BOT_TOKEN=123456789:ABCdefGhIJKlmNoPQRsTUVwxyZ
GROQ_API_KEY=gsk_your_groq_key_here
APP_PORT=8000
CHECK_INTERVAL_SECONDS=60
DATABASE_PATH=/app/data/sniper.db
LOG_LEVEL=INFO
```

### 3. Launch with Docker Compose
```bash
docker compose up -d --build
```

### 4. Verify Service Health
```bash
curl http://localhost:8000/health
```

---

## 💻 Local Development Setup (Without Docker)

### Prerequisites
- Python 3.10 or higher
- Chromium browser dependencies (via Playwright)

### Step 1: Create Virtual Environment
```bash
python -m venv .venv
# On Linux/macOS:
source .venv/bin/activate
# On Windows (PowerShell):
.venv\Scripts\Activate.ps1
```

### Step 2: Install Dependencies & Playwright Browsers
```bash
pip install -r backend/requirements.txt
playwright install chromium
```

### Step 3: Configure `.env`
```bash
cp backend/.env.example backend/.env
```

### Step 4: Run the Application Server
```bash
python -m uvicorn backend.main:app --host 0.0.0.0 --port 8000 --reload
```

---

## 🤖 Telegram Bot Command Guide

| Command | Syntax | Description | Example |
| :--- | :--- | :--- | :--- |
| Natural Language | *Any natural text* | AI/Rule extraction with inline confirmation | *«Алматы в Бангкок 15 октября до 300$»* |
| `/start` | `/start` | Welcome message and quick start guide | `/start` |
| `/help` | `/help` | Full command reference & airport codes | `/help` |
| `/snipe` | `/snipe <ORIGIN> <DEST> <YYYY-MM-DD> <TARGET_PRICE>` | Track route under target price | `/snipe ALA NQZ 2026-10-15 25000` |
| `/snipe` | `/snipe <ORIGIN> <DEST> <YYYY-MM-DD> <FLIGHT_NO> <TARGET_PRICE>` | Track specific flight number | `/snipe ALA CIT 2026-10-20 KC-871 18000` |
| `/list` | `/list` | View your active flight tracking tasks | `/list` |
| `/delete` | `/delete <TASK_ID>` | Cancel and remove a monitoring task | `/delete 1` |
| `/cancel` | `/cancel <TASK_ID>` | Alias for `/delete` | `/cancel 1` |

---

## 🇰🇿 Kazakhstan & International Airport Codes

| IATA Code | City | Airport Name |
| :--- | :--- | :--- |
| `ALA` | **Almaty** | Almaty International Airport |
| `NQZ` | **Astana** | Nursultan Nazarbayev International Airport |
| `CIT` | **Shymkent** | Shymkent International Airport |
| `SCO` | **Aktau** | Aktau International Airport |
| `GUW` | **Atyrau** | Atyrau International Airport |
| `UKK` | **Oskemen** | Oskemen (Ust-Kamenogorsk) Airport |
| `AKX` | **Aktobe** | Aktobe International Airport |
| `KSG` | **Kostanay** | Kostanay International Airport |
| `PWQ` | **Pavlodar** | Pavlodar Airport |
| `PLX` | **Semey** | Semey Airport |
| `URA` | **Uralsk** | Oral Ak Zhol Airport |
| `KGF` | **Karaganda** | Sary-Arka Airport |
| `KZO` | **Kyzylorda** | Korkyt Ata Airport |
| `BKK` | **Bangkok** | Suvarnabhumi Airport |
| `DXB` | **Dubai** | Dubai International Airport |
| `IST` | **Istanbul** | Istanbul Airport |
| `HKT` | **Phuket** | Phuket International Airport |
| `TAS` | **Tashkent** | Islam Karimov Tashkent Airport |

---

## 🌐 REST API Endpoints

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/` | Service info and operational status |
| `GET` | `/health` | Healthcheck and active task counting |
| `GET` | `/api/tasks` | List all currently active monitoring tasks |
| `POST` | `/api/check-now` | Manually trigger an immediate sniper check cycle |

---

## ⚙️ Configuration Reference (`.env`)

| Variable | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `BOT_TOKEN` | `str` | `placeholder_token` | Telegram Bot API token from [@BotFather](https://t.me/BotFather) |
| `GROQ_API_KEY` | `Optional[str]` | `None` | Optional Groq API Key for LLM-powered flight parsing |
| `GROQ_MODEL` | `str` | `llama-3.1-70b-versatile` | Groq LLM model identifier |
| `APP_PORT` | `int` | `8000` | HTTP port for FastAPI REST API |
| `DATABASE_PATH` | `str` | `data/sniper.db` | Path to SQLite database file |
| `CHECK_INTERVAL_SECONDS` | `int` | `60` | Scheduler tick interval in seconds |
| `HEADLESS` | `bool` | `true` | Run Playwright Chromium in headless mode |
| `ENVIRONMENT` | `str` | `production` | Environment profile (`development`, `testing`, `production`) |
| `LOG_LEVEL` | `str` | `INFO` | Logging level (`DEBUG`, `INFO`, `WARNING`, `ERROR`) |

---

## 🧪 Testing & Manual Simulation

### 1. Run Complete Pytest Suite
```bash
pytest backend/tests/ -v
```
All 51 tests covering database migrations, NLP parsers, currency conversions, custom intervals, bot handlers, and deduplication will run.

### 2. Run Interactive Manual Test Simulator
```bash
python backend/tests/manual_test_simulator.py
```
This standalone simulator runs an end-to-end simulation of natural language input parsing, custom interval calculation, time advancement simulation, mock flight queries, alert dispatching, and deduplication verification.

---

## 📜 License & Contributing

Licensed under the [MIT License](../../LICENSE). Contributions, bug reports, and route provider extensions are welcome!
