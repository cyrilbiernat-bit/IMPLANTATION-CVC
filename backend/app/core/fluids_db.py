"""Base de données indicative des fluides frigorigènes.

IMPORTANT — AVERTISSEMENT RÉGLEMENTAIRE
----------------------------------------
Les valeurs de groupe de sécurité proviennent de la classification ASHRAE 34 /
ISO 817 (largement reprise par la NF EN 378-1). Les valeurs de LFL, RCL, ATEL
et ODL indiquées ici sont des valeurs indicatives couramment publiées dans la
littérature technique (fiches fabricants, guides d'application EN 378 /
ISO 5149). Elles DOIVENT être vérifiées par un professionnel qualifié par
rapport à l'édition en vigueur de la norme NF EN 378-1 et aux fiches de
données de sécurité (FDS) du fabricant avant toute utilisation à des fins de
conformité réglementaire. Cette base est entièrement éditable (bibliothèque
fluides) afin de permettre la mise à jour par le bureau d'études.

Unités : LFL / RCL / ATEL / ODL en kg/m3, GWP en kgCO2eq/kg (PRG, base AR5/AR6
selon la source), masse molaire en g/mol.
"""
from __future__ import annotations

DEFAULT_FLUIDS: list[dict] = [
    {
        "code": "R32",
        "name": "R32 (Difluorométhane)",
        "safety_group": "A2L",
        "lfl_kg_m3": 0.307,
        "rcl_kg_m3": 0.061,
        "atel_kg_m3": 0.061,
        "odl_kg_m3": 0.44,
        "gwp": 675,
        "molar_mass_g_mol": 52.0,
        "source": "ASHRAE 34 / ISO 817 — valeurs indicatives à vérifier",
    },
    {
        "code": "R410A",
        "name": "R410A (R32/R125)",
        "safety_group": "A1",
        "lfl_kg_m3": None,
        "rcl_kg_m3": 0.44,
        "atel_kg_m3": 0.44,
        "odl_kg_m3": 0.44,
        "gwp": 2088,
        "molar_mass_g_mol": 72.6,
        "source": "ASHRAE 34 / ISO 817 — valeurs indicatives à vérifier",
    },
    {
        "code": "R454B",
        "name": "R454B (R32/R1234yf)",
        "safety_group": "A2L",
        "lfl_kg_m3": 0.301,
        "rcl_kg_m3": 0.061,
        "atel_kg_m3": 0.061,
        "odl_kg_m3": 0.42,
        "gwp": 466,
        "molar_mass_g_mol": 63.2,
        "source": "ASHRAE 34 / ISO 817 — valeurs indicatives à vérifier",
    },
    {
        "code": "R290",
        "name": "R290 (Propane)",
        "safety_group": "A3",
        "lfl_kg_m3": 0.038,
        "rcl_kg_m3": 0.008,
        "atel_kg_m3": 0.008,
        "odl_kg_m3": 0.42,
        "gwp": 3,
        "molar_mass_g_mol": 44.1,
        "source": "ASHRAE 34 / ISO 817 — valeurs indicatives à vérifier",
    },
    {
        "code": "R744",
        "name": "R744 (CO2)",
        "safety_group": "A1",
        "lfl_kg_m3": None,
        "rcl_kg_m3": 0.10,
        "atel_kg_m3": 0.072,
        "odl_kg_m3": 0.10,
        "gwp": 1,
        "molar_mass_g_mol": 44.0,
        "source": "ASHRAE 34 / ISO 817 — valeurs indicatives à vérifier (toxicité CO2 à seuil bas)",
    },
    {
        "code": "R1234ze",
        "name": "R1234ze(E)",
        "safety_group": "A2L",
        "lfl_kg_m3": 0.303,
        "rcl_kg_m3": 0.061,
        "atel_kg_m3": 0.061,
        "odl_kg_m3": 0.60,
        "gwp": 1,
        "molar_mass_g_mol": 114.0,
        "source": "ASHRAE 34 / ISO 817 — valeurs indicatives à vérifier",
    },
    {
        "code": "R1234yf",
        "name": "R1234yf",
        "safety_group": "A2L",
        "lfl_kg_m3": 0.289,
        "rcl_kg_m3": 0.058,
        "atel_kg_m3": 0.058,
        "odl_kg_m3": 0.47,
        "gwp": 1,
        "molar_mass_g_mol": 114.0,
        "source": "ASHRAE 34 / ISO 817 — valeurs indicatives à vérifier",
    },
    {
        "code": "R134a",
        "name": "R134a",
        "safety_group": "A1",
        "lfl_kg_m3": None,
        "rcl_kg_m3": 0.25,
        "atel_kg_m3": 0.25,
        "odl_kg_m3": 0.42,
        "gwp": 1430,
        "molar_mass_g_mol": 102.0,
        "source": "ASHRAE 34 / ISO 817 — valeurs indicatives à vérifier",
    },
    {
        "code": "R404A",
        "name": "R404A",
        "safety_group": "A1",
        "lfl_kg_m3": None,
        "rcl_kg_m3": 0.52,
        "atel_kg_m3": 0.52,
        "odl_kg_m3": 0.52,
        "gwp": 3922,
        "molar_mass_g_mol": 97.6,
        "source": "ASHRAE 34 / ISO 817 — valeurs indicatives à vérifier",
    },
    {
        "code": "R507A",
        "name": "R507A",
        "safety_group": "A1",
        "lfl_kg_m3": None,
        "rcl_kg_m3": 0.53,
        "atel_kg_m3": 0.53,
        "odl_kg_m3": 0.53,
        "gwp": 3985,
        "molar_mass_g_mol": 98.9,
        "source": "ASHRAE 34 / ISO 817 — valeurs indicatives à vérifier",
    },
]
