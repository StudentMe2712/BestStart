"""Live end-to-end verification of FastAPI endpoints with the real database file."""

import json
from starlette.testclient import TestClient
from main import app
from app.db.database import init_db

def test_endpoints():
    print("Testing live FastAPI endpoints with real data...")
    init_db()
    client = TestClient(app)

    # 1. Root
    r_root = client.get("/")
    print(f"GET / -> Status {r_root.status_code}: {r_root.json()}")
    assert r_root.status_code == 200

    # 2. System Status
    r_status = client.get("/api/system/status")
    print(f"GET /api/system/status -> Status {r_status.status_code}:")
    print(json.dumps(r_status.json(), indent=2, ensure_ascii=False))
    assert r_status.status_code == 200

    # 3. Trends List
    r_trends = client.get("/api/trends")
    print(f"GET /api/trends -> Status {r_trends.status_code} (Items: {len(r_trends.json())}):")
    if r_trends.json():
        print(json.dumps(r_trends.json()[0], indent=2, ensure_ascii=False))
    assert r_trends.status_code == 200

    # 4. Filtered trends
    r_filtered = client.get("/api/trends?status=new&min_score=5")
    print(f"GET /api/trends?status=new&min_score=5 -> Status {r_filtered.status_code} (Items: {len(r_filtered.json())})")
    assert r_filtered.status_code == 200

    # 5. Sources list
    r_sources = client.get("/api/sources")
    print(f"GET /api/sources -> Status {r_sources.status_code} (Sources: {len(r_sources.json())})")
    assert r_sources.status_code == 200

    print("\nALL RUNTIME ENDPOINTS RETURNED 200 OK WITH VALID JSON!")

if __name__ == "__main__":
    test_endpoints()
