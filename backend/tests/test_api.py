import os

os.environ["DATABASE_URL"] = "sqlite:///./test_api.db"

import pytest
from fastapi.testclient import TestClient

from app.main import app

_test_client_cm = TestClient(app)
client = _test_client_cm.__enter__()


@pytest.fixture(autouse=True, scope="module")
def cleanup():
    yield
    _test_client_cm.__exit__(None, None, None)
    if os.path.exists("./test_api.db"):
        os.remove("./test_api.db")


def test_health():
    r = client.get("/api/health")
    assert r.status_code == 200


def test_fluids_seeded():
    r = client.get("/api/fluids")
    assert r.status_code == 200
    codes = [f["code"] for f in r.json()]
    assert "R32" in codes
    assert "R410A" in codes


def test_manufacturers_seeded():
    r = client.get("/api/equipment/manufacturers")
    assert r.status_code == 200
    names = [m["name"] for m in r.json()]
    assert "Daikin" in names


def test_quick_calc_flow():
    payload = {
        "project_name": "Test projet",
        "building_type": "tertiaire",
        "room_name": "Bureau 1",
        "room_type": "bureau",
        "surface_m2": 30,
        "height_m": 2.7,
        "system_type": "DRV",
        "fluid_code": "R32",
        "indoor_units": 4,
    }
    r = client.post("/api/calculations/quick", json=payload)
    assert r.status_code == 200
    data = r.json()
    assert "concentration" in data
    assert data["concentration"]["conformity"] in {
        "Conforme",
        "Conforme sous conditions",
        "Non conforme",
    }


def test_expert_calc_flow():
    payload = {
        "room_name": "Local technique",
        "room_type": "local_technique",
        "surface_m2": 10,
        "height_m": 2.5,
        "circuits": [
            {
                "name": "Circuit 1",
                "fluid_code": "R32",
                "factory_charge_kg": 3.0,
                "pipe_length_m": 20,
                "additional_charge_kg_per_m": 0.02,
            }
        ],
    }
    r = client.post("/api/calculations/expert", json=payload)
    assert r.status_code == 200
    data = r.json()
    assert data["total_charge_kg"] > 3.0


def test_multi_room_flow():
    payload = {
        "rooms": [
            {
                "room_name": "Bureau A",
                "room_type": "bureau",
                "surface_m2": 40,
                "height_m": 2.5,
                "fluid_code": "R32",
                "charge_kg": 0.3,
            },
            {
                "room_name": "Local technique",
                "room_type": "local_technique",
                "surface_m2": 4,
                "height_m": 2.3,
                "fluid_code": "R32",
                "charge_kg": 3.0,
            },
        ]
    }
    r = client.post("/api/calculations/multi-room", json=payload)
    assert r.status_code == 200
    data = r.json()
    assert data["summary"]["worst_room"] == "Local technique"


def test_inverse_calc():
    r = client.post(
        "/api/calculations/inverse",
        json={"fluid_code": "R32", "charge_kg": 2.0, "height_m": 2.5},
    )
    assert r.status_code == 200
    assert r.json()["min_volume_m3"] > 0


def test_cctp_analysis_heuristic():
    text = "Bureau direction: 25 m2\nSalle de réunion: 18 m2\nLocal technique: 6 m2"
    r = client.post("/api/ai/analyze-cctp", json={"text": text})
    assert r.status_code == 200
    rooms = r.json()["rooms"]
    assert len(rooms) >= 2
