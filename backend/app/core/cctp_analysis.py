"""Assistant IA — analyse de CCTP.

Deux modes :
  1. Si la variable d'environnement ANTHROPIC_API_KEY est définie, le texte
     du CCTP est envoyé au modèle Claude pour une extraction structurée des
     locaux et une proposition de système / rédaction de notice.
  2. Sinon, un extracteur heuristique (mots-clés + expressions régulières en
     français) réalise l'extraction de base, afin que la fonctionnalité
     reste opérationnelle hors connexion / sans clé API.
"""
from __future__ import annotations

import json
import os
import re

ROOM_TYPE_KEYWORDS = {
    "bureau": "bureau",
    "salle de réunion": "salle_reunion",
    "chambre": "chambre",
    "hôtel": "hotel",
    "hall": "erp",
    "accueil": "erp",
    "commerce": "erp",
    "boutique": "erp",
    "laboratoire": "laboratoire",
    "local technique": "local_technique",
    "chaufferie": "local_technique",
    "sous-station": "local_technique",
    "cuisine": "commercial",
    "restaurant": "erp",
    "atelier": "industriel",
    "entrepôt": "logistique",
}

SURFACE_PATTERN = re.compile(
    r"([A-ZÀ-Ü][\w' \-]{2,60}?)\s*[:\-]?\s*(\d{1,4}(?:[.,]\d+)?)\s*m2|m²", re.IGNORECASE
)
SIMPLE_SURFACE_PATTERN = re.compile(
    r"(?P<name>[A-Za-zÀ-ÿ][\w'À-ÿ \-]{2,60}?)\s*[:\-]\s*(?P<surface>\d{1,4}(?:[.,]\d+)?)\s*m(?:2|²)",
    re.IGNORECASE,
)


def heuristic_extract_rooms(text: str) -> list[dict]:
    rooms: list[dict] = []
    seen = set()

    for match in SIMPLE_SURFACE_PATTERN.finditer(text):
        name = match.group("name").strip(" .:-\n\t")
        surface_raw = match.group("surface").replace(",", ".")
        try:
            surface = float(surface_raw)
        except ValueError:
            continue
        if not name or name.lower() in seen:
            continue
        seen.add(name.lower())

        room_type = "bureau"
        for kw, rtype in ROOM_TYPE_KEYWORDS.items():
            if kw in name.lower():
                room_type = rtype
                break

        rooms.append(
            {
                "room_name": name,
                "room_type": room_type,
                "surface_m2": surface,
                "suggested_system_type": _suggest_system_for_room_type(room_type),
            }
        )

    return rooms


def _suggest_system_for_room_type(room_type: str) -> str:
    mapping = {
        "bureau": "DRV",
        "salle_reunion": "DRV",
        "chambre": "Multi-split",
        "hotel": "Multi-split",
        "erp": "DRV",
        "laboratoire": "Groupe eau glacee",
        "local_technique": "Centrale frigorifique",
        "commercial": "Meuble frigorifique",
        "industriel": "PAC",
        "logistique": "PAC",
    }
    return mapping.get(room_type, "DRV")


def analyze_cctp(text: str) -> dict:
    api_key = os.environ.get("ANTHROPIC_API_KEY")
    if api_key:
        try:
            return _analyze_with_claude(text, api_key)
        except Exception as exc:  # pragma: no cover - network dependent
            return {
                "engine": "heuristique (repli après erreur IA)",
                "error": str(exc),
                "rooms": heuristic_extract_rooms(text),
            }

    return {"engine": "heuristique", "rooms": heuristic_extract_rooms(text)}


def _analyze_with_claude(text: str, api_key: str) -> dict:  # pragma: no cover - network dependent
    import anthropic

    client = anthropic.Anthropic(api_key=api_key)
    prompt = (
        "Tu es un assistant pour bureau d'études CVC. Analyse le CCTP suivant et "
        "extrais la liste des locaux mentionnés avec leur type et leur surface en m2 "
        "si disponible, puis propose un type de système CVC adapté pour chacun "
        "(DRV, Multi-split, Split, Groupe eau glacee, PAC, Centrale frigorifique, "
        "Meuble frigorifique). Réponds uniquement en JSON avec la clé 'rooms' : liste "
        "d'objets {room_name, room_type, surface_m2, suggested_system_type}.\n\n"
        f"CCTP:\n{text[:12000]}"
    )
    message = client.messages.create(
        model="claude-sonnet-5",
        max_tokens=2000,
        messages=[{"role": "user", "content": prompt}],
    )
    content = "".join(block.text for block in message.content if hasattr(block, "text"))
    match = re.search(r"\{.*\}", content, re.DOTALL)
    data = json.loads(match.group(0)) if match else {"rooms": []}
    data["engine"] = "claude"
    return data


def draft_en378_notice(project_name: str, results: list[dict]) -> str:
    """Rédige une notice de synthèse NF EN 378 pour le DOE (texte brut)."""
    lines = [
        f"NOTICE DE VÉRIFICATION NF EN 378-1 — {project_name}",
        "",
        "Cette notice résume la vérification de la concentration de fluide "
        "frigorigène réalisée conformément à la méthodologie NF EN 378-1 pour "
        "les locaux du projet cité en objet.",
        "",
    ]
    for r in results:
        lines.append(
            f"- {r.get('room_name', 'Local')} : fluide {r.get('fluid_code')}, "
            f"charge {r.get('charge_kg')} kg, concentration "
            f"{r.get('concentration_kg_m3')} kg/m3, limite "
            f"{r.get('limit_used_kg_m3')} kg/m3 ({r.get('limit_type')}) -> "
            f"{r.get('conformity')}."
        )
    lines.append("")
    lines.append(
        "Cette notice constitue une aide à la vérification et doit être "
        "contresignée par un professionnel qualifié avant intégration au DOE."
    )
    return "\n".join(lines)
