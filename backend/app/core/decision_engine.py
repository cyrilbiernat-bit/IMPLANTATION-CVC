"""Arbre de décision — recommandations de mesures compensatoires.

Propose, à partir du résultat NF EN 378-1 (groupe de sécurité du fluide,
conformité, ratio de marge), un ensemble de mesures parmi :
  - Détecteur de fuite
  - Électrovanne de sécurité
  - Ventilation mécanique
  - Cloisonnement / éloignement
  - Réduction de charge de fluide
"""
from __future__ import annotations

from .en378 import is_flammable


def recommend(
    safety_group: str,
    conformity: str,
    margin_ratio: float,
    room_type: str | None = None,
) -> list[dict]:
    recommendations: list[dict] = []

    if conformity == "Conforme":
        recommendations.append(
            {
                "measure": "Aucune mesure complémentaire obligatoire",
                "reason": "La concentration calculée respecte la limite applicable.",
                "priority": "info",
            }
        )
        return recommendations

    flammable = is_flammable(safety_group)

    recommendations.append(
        {
            "measure": "Détecteur de fuite de fluide frigorigène",
            "reason": "Concentration proche ou supérieure à la limite : la détection permet "
            "une alarme précoce et l'activation d'une ventilation ou d'un arrêt d'urgence.",
            "priority": "haute" if conformity == "Non conforme" else "moyenne",
        }
    )

    recommendations.append(
        {
            "measure": "Ventilation mécanique (naturelle ou forcée)",
            "reason": "Réduit la concentration résiduelle en cas de fuite et permet souvent "
            "de repasser sous la limite réglementaire sans réduire la charge.",
            "priority": "haute" if conformity == "Non conforme" else "moyenne",
        }
    )

    if flammable:
        recommendations.append(
            {
                "measure": "Électrovanne de sécurité (coupure d'alimentation fluide)",
                "reason": "Fluide inflammable (groupe A2L/A2/A3) : limite la masse pouvant "
                "être relâchée en cas de fuite détectée.",
                "priority": "haute",
            }
        )

    if margin_ratio > 1.5:
        recommendations.append(
            {
                "measure": "Réduction de la charge de fluide (fractionnement des circuits)",
                "reason": f"Le ratio concentration/limite ({margin_ratio:.2f}) dépasse largement "
                "la tolérance : envisager plusieurs circuits de charge réduite plutôt qu'un "
                "circuit unique.",
                "priority": "haute",
            }
        )

    if room_type and room_type.lower() in {"erp", "chambre", "hotel", "sante"}:
        recommendations.append(
            {
                "measure": "Cloisonnement / éloignement des locaux occupés",
                "reason": f"Type de local sensible ({room_type}) : limiter l'exposition du "
                "public en isolant la zone technique ou en augmentant la distance aux "
                "locaux occupés.",
                "priority": "moyenne",
            }
        )
    else:
        recommendations.append(
            {
                "measure": "Cloisonnement de la zone technique",
                "reason": "Réduit le volume d'occupation exposé en cas de fuite.",
                "priority": "moyenne" if conformity == "Conforme sous conditions" else "haute",
            }
        )

    return recommendations
