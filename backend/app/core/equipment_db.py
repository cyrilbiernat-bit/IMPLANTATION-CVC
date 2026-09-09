"""Bibliothèque équipements — catalogue constructeurs (données d'exemple).

AVERTISSEMENT : ce catalogue fournit un jeu de données de démonstration
(quelques références par gamme/constructeur) afin d'illustrer et de rendre
opérationnel le moteur de proposition automatique. Les puissances, charges
usine et longueurs de tuyauterie DOIVENT être vérifiées dans la documentation
technique officielle du fabricant avant tout dimensionnement contractuel.
La bibliothèque est éditable et destinée à être complétée par le bureau
d'études (import catalogue constructeur).
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

# Chaque entrée : reference, system_type, fluid_code, cooling_power_kw,
# heating_power_kw, factory_charge_kg, additional_charge_kg_per_m,
# max_pipe_length_m, max_equivalent_length_m, max_indoor_units
DEFAULT_EQUIPMENT: dict[str, list[dict]] = {
    "Daikin": [
        {
            "reference": "VRV 5 RXYQ8T",
            "system_type": "DRV",
            "fluid_code": "R32",
            "cooling_power_kw": 22.4,
            "heating_power_kw": 25.0,
            "factory_charge_kg": 8.0,
            "additional_charge_kg_per_m": 0.075,
            "max_pipe_length_m": 165,
            "max_equivalent_length_m": 190,
            "max_indoor_units": 17,
        },
        {
            "reference": "Perfera FTXM35",
            "system_type": "Split",
            "fluid_code": "R32",
            "cooling_power_kw": 3.5,
            "heating_power_kw": 4.0,
            "factory_charge_kg": 1.0,
            "additional_charge_kg_per_m": 0.02,
            "max_pipe_length_m": 20,
            "max_equivalent_length_m": 25,
            "max_indoor_units": 1,
        },
    ],
    "Mitsubishi Electric": [
        {
            "reference": "City Multi PURY-P200",
            "system_type": "DRV",
            "fluid_code": "R32",
            "cooling_power_kw": 22.4,
            "heating_power_kw": 25.0,
            "factory_charge_kg": 7.5,
            "additional_charge_kg_per_m": 0.08,
            "max_pipe_length_m": 165,
            "max_equivalent_length_m": 190,
            "max_indoor_units": 16,
        },
        {
            "reference": "MSZ-AP35VG",
            "system_type": "Split",
            "fluid_code": "R32",
            "cooling_power_kw": 3.5,
            "heating_power_kw": 4.2,
            "factory_charge_kg": 0.9,
            "additional_charge_kg_per_m": 0.02,
            "max_pipe_length_m": 20,
            "max_equivalent_length_m": 25,
            "max_indoor_units": 1,
        },
    ],
    "Mitsubishi Heavy": [
        {
            "reference": "FDC-VNP DRV",
            "system_type": "DRV",
            "fluid_code": "R410A",
            "cooling_power_kw": 22.4,
            "heating_power_kw": 25.0,
            "factory_charge_kg": 9.0,
            "additional_charge_kg_per_m": 0.09,
            "max_pipe_length_m": 150,
            "max_equivalent_length_m": 175,
            "max_indoor_units": 16,
        },
    ],
    "Toshiba": [
        {
            "reference": "SHRM-e DRV",
            "system_type": "DRV",
            "fluid_code": "R32",
            "cooling_power_kw": 22.4,
            "heating_power_kw": 25.0,
            "factory_charge_kg": 8.2,
            "additional_charge_kg_per_m": 0.075,
            "max_pipe_length_m": 165,
            "max_equivalent_length_m": 190,
            "max_indoor_units": 16,
        },
    ],
    "Hitachi": [
        {
            "reference": "Set Free VRF FSXN",
            "system_type": "DRV",
            "fluid_code": "R32",
            "cooling_power_kw": 22.4,
            "heating_power_kw": 25.0,
            "factory_charge_kg": 8.0,
            "additional_charge_kg_per_m": 0.075,
            "max_pipe_length_m": 165,
            "max_equivalent_length_m": 190,
            "max_indoor_units": 16,
        },
    ],
    "Samsung": [
        {
            "reference": "DVM S2 AM220",
            "system_type": "DRV",
            "fluid_code": "R410A",
            "cooling_power_kw": 22.4,
            "heating_power_kw": 25.0,
            "factory_charge_kg": 9.5,
            "additional_charge_kg_per_m": 0.09,
            "max_pipe_length_m": 150,
            "max_equivalent_length_m": 175,
            "max_indoor_units": 16,
        },
    ],
    "LG": [
        {
            "reference": "Multi V 5 ARUN220",
            "system_type": "DRV",
            "fluid_code": "R410A",
            "cooling_power_kw": 22.4,
            "heating_power_kw": 25.0,
            "factory_charge_kg": 9.2,
            "additional_charge_kg_per_m": 0.09,
            "max_pipe_length_m": 150,
            "max_equivalent_length_m": 175,
            "max_indoor_units": 16,
        },
    ],
    "Panasonic": [
        {
            "reference": "PACi Elite / ECOi",
            "system_type": "DRV",
            "fluid_code": "R32",
            "cooling_power_kw": 22.4,
            "heating_power_kw": 25.0,
            "factory_charge_kg": 8.1,
            "additional_charge_kg_per_m": 0.075,
            "max_pipe_length_m": 165,
            "max_equivalent_length_m": 190,
            "max_indoor_units": 16,
        },
    ],
}
