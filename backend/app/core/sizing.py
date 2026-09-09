"""Moteur de prédimensionnement CVC (Mode Rapide).

Estime les besoins de chauffage/refroidissement à partir de ratios usuels
(W/m2) modulés par le type de bâtiment et la zone climatique, propose un
type de système et estime la charge de fluide probable et la concentration
potentielle associée.

AVERTISSEMENT : ratios de pré-dimensionnement usuels donnés à titre
indicatif pour une étude de faisabilité rapide ; un calcul de déperditions/
apports détaillé (méthode NF EN 12831 / RT / RE2020) reste nécessaire pour
le dimensionnement contractuel.
"""
from __future__ import annotations

# W/m2 de référence par type de bâtiment (froid, chaud) — ordre de grandeur.
BUILDING_RATIOS_W_M2 = {
    "tertiaire": {"cooling": 80, "heating": 60},
    "residentiel": {"cooling": 50, "heating": 70},
    "industriel": {"cooling": 60, "heating": 50},
    "commercial": {"cooling": 100, "heating": 65},
    "erp": {"cooling": 90, "heating": 70},
    "hotel": {"cooling": 70, "heating": 65},
    "sante": {"cooling": 85, "heating": 75},
    "logistique": {"cooling": 30, "heating": 25},
}

# Coefficient multiplicatif appliqué selon la zone climatique française (H1/H2/H3).
CLIMATE_ZONE_FACTORS = {
    "H1": {"cooling": 1.0, "heating": 1.15},
    "H2": {"cooling": 1.05, "heating": 1.0},
    "H3": {"cooling": 1.2, "heating": 0.85},
}

SYSTEM_SUGGESTION_RULES = [
    # (max_power_kw, occupancy_hint) -> système
    (10, "DRV"),
    (30, "Multi-split"),
    (100, "DRV"),
]


def estimate_loads(
    surface_m2: float,
    building_type: str,
    climate_zone: str = "H2",
) -> dict:
    ratios = BUILDING_RATIOS_W_M2.get(building_type, BUILDING_RATIOS_W_M2["tertiaire"])
    factors = CLIMATE_ZONE_FACTORS.get(climate_zone, CLIMATE_ZONE_FACTORS["H2"])

    cooling_kw = surface_m2 * ratios["cooling"] * factors["cooling"] / 1000
    heating_kw = surface_m2 * ratios["heating"] * factors["heating"] / 1000

    return {
        "cooling_power_kw": round(cooling_kw, 2),
        "heating_power_kw": round(heating_kw, 2),
    }


def suggest_system_type(cooling_power_kw: float, indoor_units_hint: int | None = None) -> str:
    if indoor_units_hint and indoor_units_hint > 8:
        return "DRV"
    if cooling_power_kw <= 5:
        return "Split"
    if cooling_power_kw <= 16:
        return "Multi-split"
    if cooling_power_kw <= 150:
        return "DRV"
    return "Groupe eau glacee"


def estimate_probable_charge(
    system_type: str,
    cooling_power_kw: float,
    fluid_code: str,
    indoor_units: int = 1,
    pipe_length_estimate_m: float | None = None,
) -> dict:
    """Estime la charge de fluide probable à partir de ratios kg/kW usuels
    par type de système, faute de référence constructeur précise (mode rapide).
    """
    ratio_kg_per_kw = {
        "DRV": 0.35,
        "Multi-split": 0.30,
        "Split": 0.28,
        "Groupe eau glacee": 0.20,
        "PAC": 0.30,
        "Centrale frigorifique": 0.40,
        "Meuble frigorifique": 0.15,
    }.get(system_type, 0.30)

    factory_charge = round(cooling_power_kw * ratio_kg_per_kw, 2)

    pipe_length = pipe_length_estimate_m
    if pipe_length is None:
        # Estimation grossière : 5 m de réseau par unité intérieure.
        pipe_length = max(indoor_units, 1) * 5

    additional_charge_per_m = 0.03 if system_type in {"DRV", "Multi-split"} else 0.02
    additional_charge = round(pipe_length * additional_charge_per_m, 2)

    total_charge = round(factory_charge + additional_charge, 2)
    charge_per_unit = round(total_charge / max(indoor_units, 1), 3)

    return {
        "factory_charge_kg": factory_charge,
        "additional_charge_kg": additional_charge,
        "total_charge_kg": total_charge,
        "charge_per_indoor_unit_kg": charge_per_unit,
        "estimated_pipe_length_m": pipe_length,
        "fluid_code": fluid_code,
    }
