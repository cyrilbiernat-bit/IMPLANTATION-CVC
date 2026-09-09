"""Moteur de calcul de concentration de fluide frigorigène — NF EN 378-1.

Méthodologie implémentée (approche pratique couramment utilisée par les
bureaux d'études et outils constructeurs pour l'analyse du risque de
concentration, à l'échelle du principe de la norme NF EN 378-1 /
ISO 5149-1) :

1. Volume du local :          V = Surface x Hauteur
2. Concentration théorique :  Cm = Masse relâchée / V
3. Limite applicable :
   - Fluides inflammables (A2L, A2, A3) : limite pratique = 0.25 x LFL
     dans les locaux à occupation générale (marge de sécurité usuelle),
     ajustée par la catégorie d'accès. Le RCL est vérifié en complément
     lorsqu'il est renseigné.
   - Fluides non inflammables mais toxiques/asphyxiants (A1, B1) : limite
     = min(ATEL, ODL) lorsque disponibles, sinon RCL.
4. Verdict de conformité :
   - Cm <= limite retenue                              -> Conforme
   - limite < Cm <= limite x facteur de tolérance (1.5) -> Conforme sous
     conditions (mesures compensatoires : ventilation, détection, etc.)
   - Cm > limite x 1.5                                  -> Non conforme
5. Calcul inverse : à partir d'une charge donnée, détermine le volume
   minimal, puis la surface minimale pour une hauteur donnée.

AVERTISSEMENT : cette implémentation est un outil d'aide à la
pré-vérification. Les valeurs de seuils (LFL/RCL/ATEL/ODL) doivent être
vérifiées par un professionnel qualifié conformément à l'édition en
vigueur de la norme NF EN 378-1. Voir fluids_db.py.
"""
from __future__ import annotations

from dataclasses import dataclass, field

# Facteur de limite pratique appliqué à la LFL pour les fluides inflammables,
# usuellement retenu pour les locaux à occupation générale.
PRACTICAL_LFL_FACTOR = 0.25

# Marge de tolérance au-delà de la limite avant de considérer l'installation
# comme définitivement non conforme (zone "conforme sous conditions").
TOLERANCE_FACTOR = 1.5

# Coefficients multiplicateurs de la limite selon la catégorie d'accès du
# local (un accès restreint à du personnel qualifié autorise une marge
# différente ; simplification pédagogique du principe de la norme).
ACCESS_CATEGORY_FACTORS = {
    "Accès général (public)": 1.0,
    "Accès supervisé": 1.25,
    "Accès autorisé uniquement (personnel qualifié)": 1.5,
}


@dataclass
class FluidData:
    code: str
    name: str
    safety_group: str
    lfl_kg_m3: float | None
    rcl_kg_m3: float | None
    atel_kg_m3: float | None
    odl_kg_m3: float | None
    gwp: float | None = None


@dataclass
class ConcentrationResult:
    volume_m3: float
    concentration_kg_m3: float
    limit_used_kg_m3: float
    limit_type: str
    conformity: str
    margin_ratio: float  # concentration / limite (1.0 = à la limite)
    min_volume_required_m3: float
    min_surface_required_m2: float | None
    details: dict = field(default_factory=dict)


def compute_volume(surface_m2: float, height_m: float) -> float:
    if surface_m2 <= 0 or height_m <= 0:
        raise ValueError("La surface et la hauteur doivent être positives.")
    return surface_m2 * height_m


def is_flammable(safety_group: str) -> bool:
    return safety_group.upper() in {"A2L", "A2", "A3", "B2L", "B2"}


def applicable_limit(fluid: FluidData, access_category: str) -> tuple[float, str]:
    """Détermine la limite réglementaire applicable et son type (RCL/LFL/ATEL/ODL)."""
    factor = ACCESS_CATEGORY_FACTORS.get(access_category, 1.0)

    if is_flammable(fluid.safety_group):
        candidates = []
        if fluid.lfl_kg_m3:
            candidates.append((fluid.lfl_kg_m3 * PRACTICAL_LFL_FACTOR, "LFL (limite pratique 25%)"))
        if fluid.rcl_kg_m3:
            candidates.append((fluid.rcl_kg_m3, "RCL"))
        if not candidates:
            raise ValueError(f"Aucune limite LFL/RCL disponible pour {fluid.code}.")
        limit, limit_type = min(candidates, key=lambda c: c[0])
        return limit * factor, limit_type

    # Fluides non inflammables : toxicité / asphyxie
    candidates = []
    if fluid.atel_kg_m3:
        candidates.append((fluid.atel_kg_m3, "ATEL"))
    if fluid.odl_kg_m3:
        candidates.append((fluid.odl_kg_m3, "ODL"))
    if fluid.rcl_kg_m3:
        candidates.append((fluid.rcl_kg_m3, "RCL"))
    if not candidates:
        raise ValueError(f"Aucune limite ATEL/ODL/RCL disponible pour {fluid.code}.")
    limit, limit_type = min(candidates, key=lambda c: c[0])
    return limit * factor, limit_type


def evaluate_conformity(concentration: float, limit: float) -> tuple[str, float]:
    ratio = concentration / limit if limit else float("inf")
    if ratio <= 1.0:
        return "Conforme", ratio
    if ratio <= TOLERANCE_FACTOR:
        return "Conforme sous conditions", ratio
    return "Non conforme", ratio


def compute_concentration(
    fluid: FluidData,
    charge_kg: float,
    surface_m2: float,
    height_m: float,
    access_category: str = "Accès général (public)",
) -> ConcentrationResult:
    if charge_kg < 0:
        raise ValueError("La charge de fluide doit être positive.")

    volume = compute_volume(surface_m2, height_m)
    concentration = charge_kg / volume
    limit, limit_type = applicable_limit(fluid, access_category)
    conformity, ratio = evaluate_conformity(concentration, limit)

    min_volume = charge_kg / limit if limit else 0.0
    min_surface = min_volume / height_m if height_m else None

    return ConcentrationResult(
        volume_m3=round(volume, 3),
        concentration_kg_m3=round(concentration, 5),
        limit_used_kg_m3=round(limit, 5),
        limit_type=limit_type,
        conformity=conformity,
        margin_ratio=round(ratio, 3),
        min_volume_required_m3=round(min_volume, 3),
        min_surface_required_m2=round(min_surface, 3) if min_surface else None,
        details={"safety_group": fluid.safety_group, "access_category": access_category},
    )


def inverse_min_volume(fluid: FluidData, charge_kg: float, access_category: str = "Accès général (public)") -> dict:
    """Calcul inverse : à partir de la charge, détermine le volume minimal du local."""
    limit, limit_type = applicable_limit(fluid, access_category)
    min_volume = charge_kg / limit if limit else 0.0
    return {
        "min_volume_m3": round(min_volume, 3),
        "limit_used_kg_m3": round(limit, 5),
        "limit_type": limit_type,
    }


@dataclass
class RoomAnalysis:
    room_name: str
    result: ConcentrationResult


def multi_room_analysis(analyses: list[RoomAnalysis]) -> dict:
    """Détermine le local le plus pénalisant et la conformité globale."""
    if not analyses:
        raise ValueError("Aucun local à analyser.")

    order = {"Non conforme": 2, "Conforme sous conditions": 1, "Conforme": 0}
    worst = max(analyses, key=lambda a: (order[a.result.conformity], a.result.margin_ratio))

    global_conformity = "Conforme"
    if any(a.result.conformity == "Non conforme" for a in analyses):
        global_conformity = "Non conforme"
    elif any(a.result.conformity == "Conforme sous conditions" for a in analyses):
        global_conformity = "Conforme sous conditions"

    return {
        "worst_room": worst.room_name,
        "worst_margin_ratio": worst.result.margin_ratio,
        "global_conformity": global_conformity,
        "rooms": [
            {
                "room_name": a.room_name,
                "conformity": a.result.conformity,
                "concentration_kg_m3": a.result.concentration_kg_m3,
                "limit_used_kg_m3": a.result.limit_used_kg_m3,
                "margin_ratio": a.result.margin_ratio,
            }
            for a in analyses
        ],
    }
