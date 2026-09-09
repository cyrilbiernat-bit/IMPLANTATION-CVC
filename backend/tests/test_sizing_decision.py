from app.core import decision_engine, sizing


def test_estimate_loads_positive():
    loads = sizing.estimate_loads(100, "tertiaire", "H2")
    assert loads["cooling_power_kw"] > 0
    assert loads["heating_power_kw"] > 0


def test_suggest_system_type_small_split():
    assert sizing.suggest_system_type(4) == "Split"


def test_suggest_system_type_drv_many_units():
    assert sizing.suggest_system_type(50, indoor_units_hint=10) == "DRV"


def test_estimate_probable_charge():
    charge = sizing.estimate_probable_charge("DRV", 20, "R32", indoor_units=5)
    assert charge["total_charge_kg"] > charge["factory_charge_kg"]
    assert charge["charge_per_indoor_unit_kg"] > 0


def test_decision_engine_conforme_no_measures():
    recs = decision_engine.recommend("A2L", "Conforme", 0.5)
    assert len(recs) == 1
    assert "Aucune mesure" in recs[0]["measure"]


def test_decision_engine_non_conforme_flammable():
    recs = decision_engine.recommend("A2L", "Non conforme", 2.0, room_type="erp")
    measures = [r["measure"] for r in recs]
    assert any("Électrovanne" in m for m in measures)
    assert any("Détecteur" in m for m in measures)
    assert any("Réduction" in m for m in measures)
