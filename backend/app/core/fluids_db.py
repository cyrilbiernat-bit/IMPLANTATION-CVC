"""Base de données des fluides frigorigènes — valeurs issues de la norme
NF EN 378-1+A1 (octobre 2020), Annexe C (Tableau C.3) et Annexe E (normative).

Sources et méthode d'extraction
--------------------------------
- ``safety_group``, ``lfl_kg_m3`` (limite inférieure d'inflammabilité),
  ``practical_limit_kg_m3`` (« limite pratique ») et ``atel_odl_kg_m3``
  (colonne combinée ATEL/ODL), ``molar_mass_g_mol`` et ``gwp`` (PRG à 100 ans,
  base AR4/ITH utilisée par la norme) sont relevés directement dans
  l'Annexe E (normative), Tableaux E.1/E.2/E.3 de la NF EN 378-1+A1:2020.
- ``rcl_kg_m3``, ``qlmv_kg_m3`` et ``qlav_kg_m3`` : pour les 8 fluides listés
  au Tableau C.3 (normatif), les valeurs sont recopiées telles quelles
  (R-22, R-134a, R-407C, R-410A, R-744, R-32, R-1234yf, R-1234ze).
  Pour les autres fluides, ces trois valeurs sont ABSENTES de la norme et
  doivent être calculées par l'application (voir ``en378.py``,
  fonctions ``derive_rcl`` / ``derive_qlav`` / ``interpolate_qlmv`` selon
  C.3.2.1, C.3.2 et le Tableau C.4) — elles ne sont donc pas pré-remplies
  ici pour ces fluides afin de ne pas laisser croire à une valeur normative
  figée.

AVERTISSEMENT : bien que ces valeurs proviennent directement du texte de la
norme telle que fournie, une vérification par un professionnel qualifié par
rapport à l'exemplaire officiel en vigueur (et à ses éventuels errata/
amendements ultérieurs) reste requise avant usage réglementaire contractuel.
"""
from __future__ import annotations

DEFAULT_FLUIDS: list[dict] = [
    {
        "code": "R32",
        "name": "R32 (Difluorométhane)",
        "safety_group": "A2L",
        "lfl_kg_m3": 0.307,
        "practical_limit_kg_m3": 0.061,
        "atel_odl_kg_m3": 0.30,
        "molar_mass_g_mol": 52.0,
        "gwp": 675,
        "rcl_kg_m3": 0.061,
        "qlmv_kg_m3": 0.063,
        "qlav_kg_m3": 0.15,
        "source": "NF EN 378-1+A1:2020, Annexe E (Tableau E.1) et Tableau C.3",
    },
    {
        "code": "R410A",
        "name": "R410A (R32/R125, 50/50)",
        "safety_group": "A1",
        "lfl_kg_m3": None,
        "practical_limit_kg_m3": 0.44,
        "atel_odl_kg_m3": 0.42,
        "molar_mass_g_mol": 72.6,
        "gwp": 2088,
        "rcl_kg_m3": 0.39,
        "qlmv_kg_m3": 0.42,
        "qlav_kg_m3": 0.42,
        "source": "NF EN 378-1+A1:2020, Annexe E (Tableau E.2) et Tableau C.3",
    },
    {
        "code": "R454B",
        "name": "R454B (R32/R1234yf, 68.9/31.1)",
        "safety_group": "A2L",
        "lfl_kg_m3": 0.297,
        "practical_limit_kg_m3": 0.059,
        "atel_odl_kg_m3": 0.358,
        "molar_mass_g_mol": 62.6,
        "gwp": 466,
        "rcl_kg_m3": None,
        "qlmv_kg_m3": None,
        "qlav_kg_m3": None,
        "source": "NF EN 378-1+A1:2020, Annexe E (Tableau E.2, réf. 454B) — "
        "RCL/QLMV/QLAV non tabulées en Annexe C (Tableau C.3), calculées "
        "par l'application selon C.3.2.1 et le Tableau C.4",
    },
    {
        "code": "R290",
        "name": "R290 (Propane)",
        "safety_group": "A3",
        "lfl_kg_m3": 0.038,
        "practical_limit_kg_m3": 0.008,
        "atel_odl_kg_m3": 0.09,
        "molar_mass_g_mol": 44.1,
        "gwp": 3,
        "rcl_kg_m3": None,
        "qlmv_kg_m3": None,
        "qlav_kg_m3": None,
        "source": "NF EN 378-1+A1:2020, Annexe E (Tableau E.1) — RCL/QLMV/QLAV "
        "non tabulées en Annexe C (Tableau C.3), calculées par l'application. "
        "Pour les systèmes de conditionnement d'air/PAC de confort, la "
        "méthode C.2 (Formules C.1/C.2) s'applique directement (voir Annexe H).",
    },
    {
        "code": "R744",
        "name": "R744 (CO2)",
        "safety_group": "A1",
        "lfl_kg_m3": None,
        "practical_limit_kg_m3": 0.1,
        "atel_odl_kg_m3": 0.072,
        "molar_mass_g_mol": 44.0,
        "gwp": 1,
        "rcl_kg_m3": 0.072,
        "qlmv_kg_m3": 0.074,
        "qlav_kg_m3": 0.18,
        "source": "NF EN 378-1+A1:2020, Annexe E (Tableau E.1) et Tableau C.3 "
        "(QLAV basée sur une fraction volumique de 10 % — effet anesthésiant aigu)",
    },
    {
        "code": "R1234ze",
        "name": "R1234ze(E)",
        "safety_group": "A2L",
        "lfl_kg_m3": 0.303,
        "practical_limit_kg_m3": 0.061,
        "atel_odl_kg_m3": 0.28,
        "molar_mass_g_mol": 114.0,
        "gwp": 1,
        "rcl_kg_m3": 0.061,
        "qlmv_kg_m3": 0.063,
        "qlav_kg_m3": 0.15,
        "source": "NF EN 378-1+A1:2020, Annexe E (Tableau E.1) et Tableau C.3",
    },
    {
        "code": "R1234yf",
        "name": "R1234yf",
        "safety_group": "A2L",
        "lfl_kg_m3": 0.289,
        "practical_limit_kg_m3": 0.058,
        "atel_odl_kg_m3": 0.28,
        "molar_mass_g_mol": 114.0,
        "gwp": 4,
        "rcl_kg_m3": 0.058,
        "qlmv_kg_m3": 0.060,
        "qlav_kg_m3": 0.14,
        "source": "NF EN 378-1+A1:2020, Annexe E (Tableau E.1) et Tableau C.3",
    },
    {
        "code": "R134a",
        "name": "R134a",
        "safety_group": "A1",
        "lfl_kg_m3": None,
        "practical_limit_kg_m3": 0.25,
        "atel_odl_kg_m3": 0.21,
        "molar_mass_g_mol": 102.0,
        "gwp": 1430,
        "rcl_kg_m3": 0.21,
        "qlmv_kg_m3": 0.28,
        "qlav_kg_m3": 0.58,
        "source": "NF EN 378-1+A1:2020, Annexe E (Tableau E.1) et Tableau C.3 "
        "(exemple normatif Annexe H.3)",
    },
    {
        "code": "R404A",
        "name": "R404A (R125/143a/134a, 44/52/4)",
        "safety_group": "A1",
        "lfl_kg_m3": None,
        "practical_limit_kg_m3": 0.52,
        "atel_odl_kg_m3": 0.52,
        "molar_mass_g_mol": 97.6,
        "gwp": 3922,
        "rcl_kg_m3": None,
        "qlmv_kg_m3": None,
        "qlav_kg_m3": None,
        "source": "NF EN 378-1+A1:2020, Annexe E (Tableau E.2) — RCL/QLMV/QLAV "
        "non tabulées en Annexe C (Tableau C.3), calculées par l'application",
    },
    {
        "code": "R507A",
        "name": "R507A (R125/143a, 50/50)",
        "safety_group": "A1",
        "lfl_kg_m3": None,
        "practical_limit_kg_m3": 0.53,
        "atel_odl_kg_m3": 0.53,
        "molar_mass_g_mol": 98.9,
        "gwp": 3985,
        "rcl_kg_m3": None,
        "qlmv_kg_m3": None,
        "qlav_kg_m3": None,
        "source": "NF EN 378-1+A1:2020, Annexe E (Tableau E.3) — RCL/QLMV/QLAV "
        "non tabulées en Annexe C (Tableau C.3), calculées par l'application",
    },
]
