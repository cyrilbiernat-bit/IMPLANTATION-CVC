"""Bibliothèque équipements — catalogue constructeurs (données d'exemple).

AVERTISSEMENT : ce catalogue fournit un jeu de données de démonstration
(plusieurs paliers de puissance par gamme/constructeur) afin d'illustrer et
de rendre opérationnel le moteur de proposition automatique et la présaisie
de la charge de fluide à partir d'une machine sélectionnée. Les puissances,
charges usine et longueurs de tuyauterie sont des valeurs indicatives
dérivées de ratios usuels (kg/kW) et DOIVENT être vérifiées dans la
documentation technique officielle du fabricant (fiche produit / notice
d'installation) avant tout dimensionnement contractuel. La bibliothèque est
éditable et destinée à être complétée par le bureau d'études (import
catalogue constructeur réel).
"""
from __future__ import annotations

MANUFACTURERS = [
    "Daikin",
    "Mitsubishi Electric",
    "Mitsubishi Heavy",
    "Toshiba",
    "Hitachi",
    "Samsung",
    "LG",
    "Panasonic",
]

# Fluide et longueurs par défaut selon la génération de gamme DRV de chaque
# constructeur (indicatif — certaines gammes récentes basculent vers le R32).
_DRV_PROFILE = {
    "Daikin": {"fluid": "R32", "ratio": 0.357, "series": "VRV 5"},
    "Mitsubishi Electric": {"fluid": "R32", "ratio": 0.335, "series": "City Multi"},
    "Mitsubishi Heavy": {"fluid": "R410A", "ratio": 0.402, "series": "FDC-WNP"},
    "Toshiba": {"fluid": "R32", "ratio": 0.366, "series": "SHRM-e"},
    "Hitachi": {"fluid": "R32", "ratio": 0.357, "series": "Set Free VRF"},
    "Samsung": {"fluid": "R410A", "ratio": 0.424, "series": "DVM S2"},
    "LG": {"fluid": "R410A", "ratio": 0.411, "series": "Multi V 5"},
    "Panasonic": {"fluid": "R32", "ratio": 0.362, "series": "PACi / ECOi"},
}

_SPLIT_PROFILE = {
    "Daikin": "Perfera",
    "Mitsubishi Electric": "MSZ",
    "Mitsubishi Heavy": "SRK",
    "Toshiba": "Shorai",
    "Hitachi": "Airhome",
    "Samsung": "WindFree",
    "LG": "Artcool",
    "Panasonic": "Etherea",
}

_DRV_TIERS = [8.0, 14.0, 22.4, 33.5]
_DRV_PIPE = {
    "R32": {8.0: (100, 125), 14.0: (130, 155), 22.4: (165, 190), 33.5: (190, 215)},
    "R410A": {8.0: (90, 115), 14.0: (120, 145), 22.4: (150, 175), 33.5: (175, 200)},
}
_DRV_ADDITIONAL = {8.0: 0.05, 14.0: 0.065, 22.4: 0.075, 33.5: 0.09}
_DRV_ADDITIONAL_R410A = {8.0: 0.055, 14.0: 0.07, 22.4: 0.09, 33.5: 0.10}
_DRV_UNITS = {8.0: 6, 14.0: 10, 22.4: 16, 33.5: 24}

_SPLIT_TIERS = [2.5, 3.5, 5.0]
_SPLIT_ADDITIONAL = {2.5: 0.02, 3.5: 0.02, 5.0: 0.025}
_SPLIT_PIPE = {2.5: (15, 20), 3.5: (20, 25), 5.0: (25, 30)}

_MULTISPLIT_KW = 6.0


def _drv_entry(manufacturer: str, power: float) -> dict:
    profile = _DRV_PROFILE[manufacturer]
    fluid = profile["fluid"]
    additional = (_DRV_ADDITIONAL if fluid == "R32" else _DRV_ADDITIONAL_R410A)[power]
    pipe, equiv = _DRV_PIPE[fluid][power]
    return {
        "reference": f"{profile['series']} {power:g}kW",
        "system_type": "DRV",
        "fluid_code": fluid,
        "cooling_power_kw": power,
        "heating_power_kw": round(power * 1.116, 1),
        "factory_charge_kg": round(power * profile["ratio"], 1),
        "additional_charge_kg_per_m": additional,
        "max_pipe_length_m": pipe,
        "max_equivalent_length_m": equiv,
        "max_indoor_units": _DRV_UNITS[power],
    }


def _split_entry(manufacturer: str, power: float) -> dict:
    series = _SPLIT_PROFILE[manufacturer]
    pipe, equiv = _SPLIT_PIPE[power]
    return {
        "reference": f"{series} {power:g}kW",
        "system_type": "Split",
        "fluid_code": "R32",
        "cooling_power_kw": power,
        "heating_power_kw": round(power * 1.14, 1),
        "factory_charge_kg": round(power * 0.28, 2),
        "additional_charge_kg_per_m": _SPLIT_ADDITIONAL[power],
        "max_pipe_length_m": pipe,
        "max_equivalent_length_m": equiv,
        "max_indoor_units": 1,
    }


def _multisplit_entry(manufacturer: str) -> dict:
    series = _SPLIT_PROFILE[manufacturer]
    power = _MULTISPLIT_KW
    return {
        "reference": f"{series} Multi {power:g}kW (3 zones)",
        "system_type": "Multi-split",
        "fluid_code": "R32",
        "cooling_power_kw": power,
        "heating_power_kw": round(power * 1.12, 1),
        "factory_charge_kg": round(power * 0.30, 2),
        "additional_charge_kg_per_m": 0.03,
        "max_pipe_length_m": 30,
        "max_equivalent_length_m": 35,
        "max_indoor_units": 3,
    }


def _build_default_equipment() -> dict[str, list[dict]]:
    catalog: dict[str, list[dict]] = {}
    for manufacturer in MANUFACTURERS:
        entries = [_drv_entry(manufacturer, p) for p in _DRV_TIERS]
        entries += [_split_entry(manufacturer, p) for p in _SPLIT_TIERS]
        entries.append(_multisplit_entry(manufacturer))
        catalog[manufacturer] = entries
    return catalog


# Chaque entrée : reference, system_type, fluid_code, cooling_power_kw,
# heating_power_kw, factory_charge_kg, additional_charge_kg_per_m,
# max_pipe_length_m, max_equivalent_length_m, max_indoor_units
DEFAULT_EQUIPMENT: dict[str, list[dict]] = _build_default_equipment()
