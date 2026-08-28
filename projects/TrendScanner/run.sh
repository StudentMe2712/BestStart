#!/usr/bin/env bash
set -e

echo "======================================================="
echo "         TrendScanner Analytical Terminal              "
echo "======================================================="
echo ""

if command -v docker >/dev/null 2>&1 && docker compose version >/dev/null 2>&1; then
    echo "[INFO] Docker detected. Launching multi-container stack..."
    docker compose up --build
else
    echo "[WARNING] Docker not available. Starting local dev servers..."
    (cd backend && python -m uvicorn main:app --host 0.0.0.0 --port 8000 --reload) &
    BACKEND_PID=$!
    (cd frontend && npm run dev) &
    FRONTEND_PID=$!

    trap "kill $BACKEND_PID $FRONTEND_PID" EXIT
    wait
fi
