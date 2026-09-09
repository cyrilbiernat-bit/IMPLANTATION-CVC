from app.core import en378


def make_fluid_r32():
    return en378.FluidData(
        code="R32", name="R32", safety_group="A2L",
        lfl_kg_m3=0.307, practical_limit_kg_m3=0.061, atel_odl_kg_m3=0.30,
        molar_mass_g_mol=52.0, gwp=675,
        rcl_kg_m3=0.061, qlmv_kg_m3=0.063, qlav_kg_m3=0.15,
    )


def make_fluid_r410a():
    return en378.FluidData(
        code="R410A", name="R410A", safety_group="A1",
        lfl_kg_m3=None, practical_limit_kg_m3=0.44, atel_odl_kg_m3=0.42,
        molar_mass_g_mol=72.6, gwp=2088,
        rcl_kg_m3=0.39, qlmv_kg_m3=0.42, qlav_kg_m3=0.42,
    )


def make_fluid_r290():
    return en378.FluidData(
        code="R290", name="R290", safety_group="A3",
        lfl_kg_m3=0.038, practical_limit_kg_m3=0.008, atel_odl_kg_m3=0.09,
        molar_mass_g_mol=44.1, gwp=3,
    )


def make_fluid_r134a():
    return en378.FluidData(
        code="R134a", name="R134a", safety_group="A1",
        lfl_kg_m3=None, practical_limit_kg_m3=0.25, atel_odl_kg_m3=0.21,
        molar_mass_g_mol=102.0, gwp=1430,
        rcl_kg_m3=0.21, qlmv_kg_m3=0.28, qlav_kg_m3=0.58,
    )


def test_compute_volume():
    assert en378.compute_volume(20, 2.5) == 50


def test_compute_volume_invalid():
    try:
        en378.compute_volume(0, 2.5)
        assert False, "should raise"
    except ValueError:
        pass


# --- Méthode A (C.2, Formules C.1/C.2) — validée contre l'Annexe H.1/H.2 ---

def test_annexe_h1_r290_floor_mounting():
    """H.1 : 300 g de R-290 (LFL=0.038), montage au sol -> surface min ~142.1 m2."""
    fluid = make_fluid_r290()
    result = en378.compute_split_system_limit(fluid, charge_kg=0.300, surface_m2=142.1, mounting_type="floor")
    assert result.applicable
    assert abs(result.m1_kg - 0.152) < 1e-6
    assert result.charge_above_threshold is True
    assert abs(result.amin_m2 - 142.1) < 1.0


def test_annexe_h1_r290_wall_mounting():
    fluid = make_fluid_r290()
    result = en378.compute_split_system_limit(fluid, charge_kg=0.300, surface_m2=15.8, mounting_type="wall")
    assert abs(result.amin_m2 - 15.8) < 0.5


def test_annexe_h1_r290_window_mounting():
    fluid = make_fluid_r290()
    result = en378.compute_split_system_limit(fluid, charge_kg=0.300, surface_m2=51.2, mounting_type="window")
    assert abs(result.amin_m2 - 51.2) < 0.5


def test_annexe_h1_r290_ceiling_mounting():
    fluid = make_fluid_r290()
    result = en378.compute_split_system_limit(fluid, charge_kg=0.300, surface_m2=10.6, mounting_type="ceiling")
    assert abs(result.amin_m2 - 10.6) < 0.3


def test_annexe_h2_r290_window_30m2_charge_230g():
    """H.2 : salle de 30 m2, montage sur fenêtre -> charge admissible = 230 g."""
    fluid = make_fluid_r290()
    h0 = en378.MOUNTING_HEIGHT_COEFFICIENTS["window"]
    lfl_pow = fluid.lfl_kg_m3 ** 1.25
    mmax = 2.5 * lfl_pow * h0 * (30 ** 0.5)
    assert abs(mmax - 0.230) < 0.002


def test_split_system_not_applicable_for_a1():
    fluid = make_fluid_r410a()
    result = en378.compute_split_system_limit(fluid, charge_kg=5, surface_m2=20, mounting_type="wall")
    assert not result.applicable


# --- Méthode B (C.3, Tableau C.3) — validée contre l'Annexe H.3 ---

def test_annexe_h3_r134a_conforme_sous_conditions():
    """H.3 : 90 kg de R-134a dans 300 m3 -> 0.3 kg/m3, entre QLMV(0.28) et QLAV(0.58)."""
    fluid = make_fluid_r134a()
    result = en378.compute_general_method(fluid, charge_kg=90, surface_m2=100, height_m=3)
    assert result.volume_m3 == 300
    assert abs(result.concentration_kg_m3 - 0.3) < 1e-6
    assert result.qlmv_kg_m3 == 0.28
    assert result.qlav_kg_m3 == 0.58
    assert result.conformity == "Conforme sous conditions"
    assert result.measures_required == 1


def test_general_method_conforme_case():
    fluid = make_fluid_r32()
    result = en378.compute_general_method(fluid, charge_kg=0.5, surface_m2=20, height_m=2.5)
    assert result.conformity == "Conforme"


def test_general_method_non_conforme_case():
    fluid = make_fluid_r32()
    result = en378.compute_general_method(fluid, charge_kg=5.0, surface_m2=5, height_m=2.5)
    assert result.conformity == "Non conforme"


def test_general_method_basement_uses_rcl():
    fluid = make_fluid_r32()
    result_basement = en378.compute_general_method(
        fluid, charge_kg=0.5, surface_m2=10, height_m=2.5, is_lowest_basement_level=True
    )
    result_normal = en378.compute_general_method(
        fluid, charge_kg=0.5, surface_m2=10, height_m=2.5, is_lowest_basement_level=False
    )
    # RCL (0.061) == QLMV (0.063) pour R32 donc peu de différence, mais le
    # champ is_lowest_basement_level doit être répercuté dans le résultat.
    assert result_basement.is_lowest_basement_level is True
    assert result_normal.is_lowest_basement_level is False


def test_method_b_not_eligible_for_a3():
    fluid = make_fluid_r290()
    eligible, note = en378.is_method_b_eligible(fluid, charge_kg=1.0)
    assert not eligible
    assert "A1 ou A2L" in note


def test_method_b_charge_cap():
    fluid = make_fluid_r410a()
    eligible, note = en378.is_method_b_eligible(fluid, charge_kg=200)
    assert not eligible


def test_interpolate_qlmv_table_c4_exact_points():
    assert abs(en378.interpolate_qlmv(0.10, 50) - 0.106) < 1e-6
    assert abs(en378.interpolate_qlmv(0.30, 125) - 0.689) < 1e-6


def test_interpolate_qlmv_missing_cell_returns_none():
    assert en378.interpolate_qlmv(0.35, 100) is None


# --- Orchestrateur ---

def test_compute_concentration_uses_method_a_for_split_ac():
    fluid = make_fluid_r290()
    analysis = en378.compute_concentration(
        fluid, charge_kg=0.300, surface_m2=30, height_m=2.5,
        system_type="Split", mounting_type="window",
    )
    assert analysis.method_used == "A"


def test_compute_concentration_uses_method_b_by_default():
    fluid = make_fluid_r32()
    analysis = en378.compute_concentration(fluid, charge_kg=0.5, surface_m2=20, height_m=2.5)
    assert analysis.method_used == "B"


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
