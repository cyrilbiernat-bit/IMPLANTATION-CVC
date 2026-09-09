from app.core import en378


def make_fluid_r32():
    return en378.FluidData(
        code="R32",
        name="R32",
        safety_group="A2L",
        lfl_kg_m3=0.307,
        rcl_kg_m3=0.061,
        atel_kg_m3=0.061,
        odl_kg_m3=0.44,
        gwp=675,
    )


def make_fluid_r410a():
    return en378.FluidData(
        code="R410A",
        name="R410A",
        safety_group="A1",
        lfl_kg_m3=None,
        rcl_kg_m3=0.44,
        atel_kg_m3=0.44,
        odl_kg_m3=0.44,
        gwp=2088,
    )


def test_compute_volume():
    assert en378.compute_volume(20, 2.5) == 50


def test_compute_volume_invalid():
    try:
        en378.compute_volume(0, 2.5)
        assert False, "should raise"
    except ValueError:
        pass


def test_conforme_case():
    fluid = make_fluid_r32()
    result = en378.compute_concentration(fluid, charge_kg=0.5, surface_m2=20, height_m=2.5)
    assert result.volume_m3 == 50
    assert result.conformity == "Conforme"
    assert result.concentration_kg_m3 == round(0.5 / 50, 5)


def test_non_conforme_case():
    fluid = make_fluid_r32()
    # Small room, large charge -> should breach limit heavily.
    result = en378.compute_concentration(fluid, charge_kg=5.0, surface_m2=5, height_m=2.5)
    assert result.conformity == "Non conforme"


def test_conforme_sous_conditions():
    fluid = make_fluid_r32()
    limit, _ = en378.applicable_limit(fluid, "Accès général (public)")
    volume = 10.0
    # Choose a charge that lands between limit and limit*1.5.
    charge = limit * volume * 1.2
    result = en378.compute_concentration(fluid, charge_kg=charge, surface_m2=4, height_m=2.5)
    assert result.conformity == "Conforme sous conditions"


def test_access_category_relaxes_limit():
    fluid = make_fluid_r32()
    general, _ = en378.applicable_limit(fluid, "Accès général (public)")
    restricted, _ = en378.applicable_limit(fluid, "Accès autorisé uniquement (personnel qualifié)")
    assert restricted > general


def test_non_flammable_uses_atel_odl():
    fluid = make_fluid_r410a()
    limit, limit_type = en378.applicable_limit(fluid, "Accès général (public)")
    assert limit_type in {"ATEL", "ODL", "RCL"}
    assert limit == 0.44


def test_inverse_min_volume():
    fluid = make_fluid_r32()
    res = en378.inverse_min_volume(fluid, charge_kg=1.0)
    assert res["min_volume_m3"] > 0


def test_multi_room_analysis_worst_case():
    fluid = make_fluid_r32()
    ok = en378.compute_concentration(fluid, charge_kg=0.2, surface_m2=30, height_m=2.5)
    bad = en378.compute_concentration(fluid, charge_kg=5.0, surface_m2=5, height_m=2.5)
    analyses = [
        en378.RoomAnalysis(room_name="Bureau A", result=ok),
        en378.RoomAnalysis(room_name="Local technique", result=bad),
    ]
    summary = en378.multi_room_analysis(analyses)
    assert summary["worst_room"] == "Local technique"
    assert summary["global_conformity"] == "Non conforme"
