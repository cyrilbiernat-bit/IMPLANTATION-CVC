"""Moteur de calcul de concentration de fluide frigorigène — NF EN 378-1+A1
(octobre 2020), Annexe C (normative) « Exigences relatives aux limites de
charge de fluide frigorigène ».

Ce module implémente deux méthodes normatives distinctes, toutes deux
vérifiées par recalcul des exemples chiffrés de l'Annexe H (informative) de
la norme :

**Méthode A — C.2** (« Limites de charge dues à l'inflammabilité pour les
systèmes de conditionnement d'air ou les pompes à chaleur pour le confort
des personnes ») : formules (C.1) et (C.2), applicable aux fluides
inflammables (classes 2L, 2, 3) dès que la charge dépasse le seuil m1
(x 1,5 pour la classe 2L). Validée numériquement contre l'exemple H.1/H.2
(R-290, résultats identiques au kg/m2 près).

    m_max = 2,5 x LFL^(5/4) x h0 x sqrt(A)                          (C.1)
    A_min = m^2 / (2,5 x LFL^(5/4) x h0)^2                          (C.2)

**Méthode B — C.3** (« Autre solution pour la gestion des risques associés
aux systèmes frigorifiques dans des espaces occupés »), réservée aux fluides
de groupe A1 ou A2L (C.3.1) : comparaison de la concentration (charge totale
/ volume du local, plafonné à 250 m² de surface par C.3.2.1) aux valeurs
RCL, QLMV et QLAV (Tableau C.3, ou calculées selon C.3.2.1 et le Tableau C.4
pour les fluides non tabulés). Validée contre l'exemple H.3 (R-134a).

AVERTISSEMENT : cet outil est une aide à la pré-vérification. Les deux
méthodes ci-dessus ne couvrent pas l'intégralité des cas de la norme (la
méthode générale des Tableaux C.1/C.2 fondée sur les classes d'emplacement
I à IV et les catégories d'accès a/b/c n'est pas implémentée). Toute étude
doit être validée par un professionnel qualifié au regard du texte intégral
de la norme en vigueur.
"""
from __future__ import annotations

import math
from dataclasses import dataclass, field

# ---------------------------------------------------------------------------
# Constantes normatives (Annexe C)
# ---------------------------------------------------------------------------

# Coefficients de hauteur h0 selon l'emplacement d'installation de l'appareil
# (Formules C.1/C.2, C.2.1).
MOUNTING_HEIGHT_COEFFICIENTS = {
    "floor": 0.6,       # emplacement au sol
    "wall": 1.8,        # montage au mur
    "window": 1.0,      # montage sur fenêtre
    "ceiling": 2.2,      # montage au plafond
}

MOUNTING_TYPE_LABELS = {
    "floor": "Plancher",
    "wall": "Montage au mur",
    "window": "Montage sur fenêtre",
    "ceiling": "Montage au plafond",
}

# Types de systèmes considérés comme « conditionnement d'air ou pompe à
# chaleur pour le confort des personnes » au sens de C.2.
COMFORT_AC_SYSTEM_TYPES = {"DRV", "Multi-split", "Split", "PAC"}

# Table C.4 — interpolation de la QLMV (kg/m3) en fonction de la RCL (kg/m3,
# lignes) et de la masse moléculaire (g/mol, colonnes 50/75/100/125).
# Une cellule à None indique une valeur non fournie par la norme (« - »).
TABLE_C4_MOLAR_MASSES = (50, 75, 100, 125)
TABLE_C4 = {
    0.05: (0.051, 0.051, 0.051, 0.051),
    0.10: (0.106, 0.108, 0.108, 0.109),
    0.15: (0.168, 0.173, 0.175, 0.176),
    0.20: (0.242, 0.254, 0.260, 0.264),
    0.25: (0.336, 0.367, 0.383, 0.394),
    0.30: (0.470, 0.564, 0.633, 0.689),
    0.35: (0.724, None, None, None),
}

# Surface de plancher plafonnée pour le calcul du volume de vérification
# (C.3.2.1) lorsque l'espace occupé dépasse cette valeur.
MAX_SURFACE_FOR_VOLUME_CHECK_M2 = 250.0

# Charge admissible plafond pour l'éligibilité à la méthode C.3 (C.3.1).
METHOD_B_MAX_CHARGE_KG = 150.0


@dataclass
class FluidData:
    code: str
    name: str
    safety_group: str
    lfl_kg_m3: float | None
    practical_limit_kg_m3: float | None
    atel_odl_kg_m3: float | None
    molar_mass_g_mol: float | None = None
    gwp: float | None = None
    rcl_kg_m3: float | None = None
    qlmv_kg_m3: float | None = None
    qlav_kg_m3: float | None = None


def is_flammable(safety_group: str) -> bool:
    return safety_group.upper() in {"A2L", "A2", "A3", "B2L", "B2"}


def compute_volume(surface_m2: float, height_m: float) -> float:
    if surface_m2 <= 0 or height_m <= 0:
        raise ValueError("La surface et la hauteur doivent être positives.")
    return surface_m2 * height_m


# ---------------------------------------------------------------------------
# Méthode A — C.2 : systèmes de conditionnement d'air / PAC de confort
# ---------------------------------------------------------------------------

@dataclass
class SplitSystemResult:
    applicable: bool
    reason: str | None
    m1_kg: float | None = None
    threshold_kg: float | None = None
    charge_above_threshold: bool | None = None
    mmax_kg: float | None = None
    amin_m2: float | None = None
    conformity: str | None = None
    mounting_type: str | None = None
    h0: float | None = None


def compute_split_system_limit(
    fluid: FluidData,
    charge_kg: float,
    surface_m2: float,
    mounting_type: str,
) -> SplitSystemResult:
    """Applique les Formules (C.1) et (C.2) de la NF EN 378-1 (C.2.1)."""
    if not is_flammable(fluid.safety_group):
        return SplitSystemResult(
            applicable=False,
            reason=f"La méthode C.2 ne s'applique qu'aux fluides inflammables "
            f"(classes 2L/2/3) ; {fluid.code} est de classe {fluid.safety_group}.",
        )
    if fluid.lfl_kg_m3 is None:
        return SplitSystemResult(
            applicable=False, reason=f"LFL non définie pour {fluid.code}."
        )
    if mounting_type not in MOUNTING_HEIGHT_COEFFICIENTS:
        return SplitSystemResult(
            applicable=False,
            reason=f"Type de montage inconnu : {mounting_type}.",
        )

    h0 = MOUNTING_HEIGHT_COEFFICIENTS[mounting_type]
    m1 = 4.0 * fluid.lfl_kg_m3

    flammability_class = fluid.safety_group.upper().replace("A", "").replace("B", "")
    threshold = m1 * 1.5 if flammability_class == "2L" else m1

    above_threshold = charge_kg > threshold

    lfl_pow = fluid.lfl_kg_m3 ** 1.25
    denom = 2.5 * lfl_pow * h0

    mmax = denom * math.sqrt(surface_m2) if surface_m2 > 0 else 0.0
    amin = (charge_kg**2) / (denom**2) if denom > 0 else None

    conformity = "Conforme" if not above_threshold or charge_kg <= mmax else "Non conforme"

    return SplitSystemResult(
        applicable=True,
        reason=None,
        m1_kg=round(m1, 4),
        threshold_kg=round(threshold, 4),
        charge_above_threshold=above_threshold,
        mmax_kg=round(mmax, 4),
        amin_m2=round(amin, 3) if amin is not None else None,
        conformity=conformity,
        mounting_type=mounting_type,
        h0=h0,
    )


# ---------------------------------------------------------------------------
# Méthode B — C.3 : autre solution pour la gestion des risques (RCL/QLMV/QLAV)
# ---------------------------------------------------------------------------

def _bilinear_interp(x: float, x0: float, x1: float, y0: float, y1: float) -> float:
    if x1 == x0:
        return y0
    t = (x - x0) / (x1 - x0)
    return y0 + t * (y1 - y0)


def interpolate_qlmv(rcl: float, molar_mass: float) -> float | None:
    """Interpolation linéaire (Tableau C.4) de la QLMV à partir de la RCL et
    de la masse moléculaire, pour une masse moléculaire comprise entre 50 et
    125 g/mol (C.3.2.1)."""
    rcl_keys = sorted(TABLE_C4.keys())
    if rcl <= rcl_keys[0]:
        row_low = row_high = rcl_keys[0]
    elif rcl >= rcl_keys[-1]:
        row_low = row_high = rcl_keys[-1]
    else:
        row_low = max(k for k in rcl_keys if k <= rcl)
        row_high = min(k for k in rcl_keys if k >= rcl)

    mm = max(min(molar_mass, TABLE_C4_MOLAR_MASSES[-1]), TABLE_C4_MOLAR_MASSES[0])
    col_low_idx = max(i for i, m in enumerate(TABLE_C4_MOLAR_MASSES) if m <= mm)
    col_high_idx = min(i for i, m in enumerate(TABLE_C4_MOLAR_MASSES) if m >= mm)

    def value_at(row: float) -> float | None:
        vals = TABLE_C4[row]
        v_low, v_high = vals[col_low_idx], vals[col_high_idx]
        if v_low is None or v_high is None:
            return None
        return _bilinear_interp(
            mm, TABLE_C4_MOLAR_MASSES[col_low_idx], TABLE_C4_MOLAR_MASSES[col_high_idx], v_low, v_high
        )

    v_row_low = value_at(row_low)
    v_row_high = value_at(row_high)
    if v_row_low is None or v_row_high is None:
        return None
    return _bilinear_interp(rcl, row_low, row_high, v_row_low, v_row_high)


def derive_rcl(fluid: FluidData) -> tuple[float | None, str]:
    """RCL : valeur tabulée (Tableau C.3) si disponible, sinon dérivée comme
    min(limite pratique, ATEL/ODL) conformément à l'usage de l'Annexe E
    (approximation lorsque non tabulée — voir avertissement du module)."""
    if fluid.rcl_kg_m3 is not None:
        return fluid.rcl_kg_m3, "Tableau C.3 (valeur normative)"
    candidates = [v for v in (fluid.practical_limit_kg_m3, fluid.atel_odl_kg_m3) if v]
    if not candidates:
        return None, "indéterminée"
    return min(candidates), "dérivée de l'Annexe E (min. limite pratique / ATEL-ODL)"


def derive_qlav(fluid: FluidData) -> tuple[float | None, str]:
    """QLAV : valeur tabulée si disponible, sinon min(ODL, 50% LFL pour les
    fluides 2L) conformément au texte suivant le Tableau C.3."""
    if fluid.qlav_kg_m3 is not None:
        return fluid.qlav_kg_m3, "Tableau C.3 (valeur normative)"
    candidates: list[float] = []
    if fluid.atel_odl_kg_m3:
        candidates.append(fluid.atel_odl_kg_m3)
    if fluid.lfl_kg_m3 and is_flammable(fluid.safety_group):
        candidates.append(0.5 * fluid.lfl_kg_m3)
    if not candidates:
        return None, "indéterminée"
    return min(candidates), "dérivée (min. ODL / 50% LFL, règle suivant le Tableau C.3)"


def derive_qlmv(fluid: FluidData, rcl: float | None, qlav: float | None) -> tuple[float | None, str]:
    if fluid.qlmv_kg_m3 is not None:
        return fluid.qlmv_kg_m3, "Tableau C.3 (valeur normative)"
    if rcl is None or fluid.molar_mass_g_mol is None:
        return qlav, "indéterminée — QLAV utilisée par défaut (Formule C.6 non calculable)"
    qlmv = interpolate_qlmv(rcl, fluid.molar_mass_g_mol)
    if qlmv is None or (qlav is not None and qlmv > qlav):
        return qlav, "Tableau C.4 (interpolation) plafonnée à la QLAV"
    return qlmv, "Tableau C.4 (interpolation linéaire RCL / masse moléculaire)"


def is_method_b_eligible(fluid: FluidData, charge_kg: float) -> tuple[bool, str | None]:
    if fluid.safety_group.upper() not in {"A1", "A2L"}:
        return False, (
            f"La méthode alternative C.3 ne s'applique qu'aux fluides de groupe "
            f"A1 ou A2L (C.3.1) ; {fluid.code} est de groupe {fluid.safety_group}."
        )
    if charge_kg > METHOD_B_MAX_CHARGE_KG:
        return False, f"Charge ({charge_kg} kg) supérieure au plafond de 150 kg fixé par C.3.1."
    return True, None


@dataclass
class GeneralMethodResult:
    volume_m3: float
    volume_used_for_check_m3: float
    concentration_kg_m3: float
    rcl_kg_m3: float | None
    rcl_source: str
    qlmv_kg_m3: float | None
    qlmv_source: str
    qlav_kg_m3: float | None
    qlav_source: str
    conformity: str
    measures_required: int
    is_lowest_basement_level: bool
    min_volume_required_m3: float | None
    min_surface_required_m2: float | None
    eligible: bool
    eligibility_note: str | None


def compute_general_method(
    fluid: FluidData,
    charge_kg: float,
    surface_m2: float,
    height_m: float,
    is_lowest_basement_level: bool = False,
) -> GeneralMethodResult:
    """Méthode C.3 (Autre solution) : RCL / QLMV / QLAV (C.3.2)."""
    if charge_kg < 0:
        raise ValueError("La charge de fluide doit être positive.")

    volume = compute_volume(surface_m2, height_m)
    capped_surface = min(surface_m2, MAX_SURFACE_FOR_VOLUME_CHECK_M2)
    volume_for_check = compute_volume(capped_surface, height_m)
    concentration = charge_kg / volume_for_check

    rcl, rcl_source = derive_rcl(fluid)
    qlav, qlav_source = derive_qlav(fluid)
    qlmv, qlmv_source = derive_qlmv(fluid, rcl, qlav)

    eligible, eligibility_note = is_method_b_eligible(fluid, charge_kg)

    lower_bound = rcl if is_lowest_basement_level else qlmv

    if qlav is not None and concentration > qlav:
        conformity = "Non conforme"
        measures = 2
    elif lower_bound is not None and concentration <= lower_bound:
        conformity = "Conforme"
        measures = 0
    elif is_lowest_basement_level and qlmv is not None and concentration > qlmv:
        conformity = "Conforme sous conditions"
        measures = 2
    elif lower_bound is not None:
        conformity = "Conforme sous conditions"
        measures = 1
    else:
        conformity = "Non conforme"
        measures = 0

    min_volume = charge_kg / lower_bound if lower_bound else None
    min_surface = min_volume / height_m if (min_volume and height_m) else None

    return GeneralMethodResult(
        volume_m3=round(volume, 3),
        volume_used_for_check_m3=round(volume_for_check, 3),
        concentration_kg_m3=round(concentration, 5),
        rcl_kg_m3=round(rcl, 5) if rcl is not None else None,
        rcl_source=rcl_source,
        qlmv_kg_m3=round(qlmv, 5) if qlmv is not None else None,
        qlmv_source=qlmv_source,
        qlav_kg_m3=round(qlav, 5) if qlav is not None else None,
        qlav_source=qlav_source,
        conformity=conformity,
        measures_required=measures,
        is_lowest_basement_level=is_lowest_basement_level,
        min_volume_required_m3=round(min_volume, 3) if min_volume is not None else None,
        min_surface_required_m2=round(min_surface, 3) if min_surface is not None else None,
        eligible=eligible,
        eligibility_note=eligibility_note,
    )


# ---------------------------------------------------------------------------
# Orchestration : sélection de méthode + analyse multilocaux
# ---------------------------------------------------------------------------

@dataclass
class ConcentrationAnalysis:
    method_used: str  # "A" (C.2) ou "B" (C.3)
    general: GeneralMethodResult
    split_system: SplitSystemResult | None
    conformity: str
    margin_ratio: float
    volume_m3: float
    concentration_kg_m3: float
    limit_used_kg_m3: float | None
    limit_type: str
    min_volume_required_m3: float | None
    min_surface_required_m2: float | None
    notes: list[str] = field(default_factory=list)


def compute_concentration(
    fluid: FluidData,
    charge_kg: float,
    surface_m2: float,
    height_m: float,
    system_type: str | None = None,
    mounting_type: str | None = None,
    is_lowest_basement_level: bool = False,
) -> ConcentrationAnalysis:
    """Point d'entrée principal : calcule le résultat NF EN 378-1 en
    choisissant la méthode applicable (C.2 pour la climatisation/PAC de
    confort avec fluide inflammable et montage renseigné, sinon C.3)."""
    general = compute_general_method(fluid, charge_kg, surface_m2, height_m, is_lowest_basement_level)

    notes: list[str] = []
    if not general.eligible and general.eligibility_note:
        notes.append(general.eligibility_note)

    split_result: SplitSystemResult | None = None
    use_method_a = (
        system_type in COMFORT_AC_SYSTEM_TYPES
        and mounting_type is not None
        and is_flammable(fluid.safety_group)
    )
    if use_method_a:
        split_result = compute_split_system_limit(fluid, charge_kg, surface_m2, mounting_type)

    if split_result and split_result.applicable:
        method_used = "A"
        conformity = split_result.conformity or "Conforme"
        limit_used = split_result.mmax_kg
        limit_type = "mmax (Formule C.1)"
        volume_m3 = general.volume_m3
        concentration = charge_kg / volume_m3 if volume_m3 else 0.0
        margin_ratio = (charge_kg / limit_used) if limit_used else 0.0
        min_volume = None
        min_surface = split_result.amin_m2
        notes.append(
            "Méthode C.2 appliquée (conditionnement d'air / PAC de confort, fluide "
            f"inflammable {fluid.safety_group}) : charge comparée à la charge maximale "
            "admissible mmax (Formule C.1) pour la surface réelle du local."
        )
        if not split_result.charge_above_threshold:
            notes.append(
                f"Charge ({charge_kg} kg) inférieure au seuil réglementaire "
                f"({split_result.threshold_kg} kg) : aucune restriction de surface "
                "n'est requise par la Formule (C.1)."
            )
    else:
        method_used = "B"
        conformity = general.conformity
        limit_used = general.qlmv_kg_m3 if not is_lowest_basement_level else general.rcl_kg_m3
        limit_type = "RCL" if is_lowest_basement_level else "QLMV"
        volume_m3 = general.volume_m3
        concentration = general.concentration_kg_m3
        margin_ratio = (concentration / limit_used) if limit_used else 0.0
        min_volume = general.min_volume_required_m3
        min_surface = general.min_surface_required_m2
        if split_result and not split_result.applicable and split_result.reason:
            notes.append(split_result.reason)
        notes.append(
            "Méthode C.3 appliquée (autre solution pour la gestion des risques) : "
            f"comparaison de la concentration à la {limit_type} et à la QLAV."
        )

    return ConcentrationAnalysis(
        method_used=method_used,
        general=general,
        split_system=split_result,
        conformity=conformity,
        margin_ratio=round(margin_ratio, 3),
        volume_m3=volume_m3,
        concentration_kg_m3=round(concentration, 5),
        limit_used_kg_m3=round(limit_used, 5) if limit_used is not None else None,
        limit_type=limit_type,
        min_volume_required_m3=min_volume,
        min_surface_required_m2=min_surface,
        notes=notes,
    )


@dataclass
class RoomAnalysis:
    room_name: str
    result: ConcentrationAnalysis


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
