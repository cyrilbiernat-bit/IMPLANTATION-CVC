"""
Extrait la géométrie 3D triangulée d'un fichier IFC, en JSON, pour
affichage dans la vue 3D (Three.js) de l'application.

Usage: python3 ifc_extract.py <entree.ifc> <sortie.json>

Moteur : IfcOpenShell (open source, https://ifcopenshell.org) — le même
moteur utilisé par l'atelier BIM de FreeCAD pour importer/exporter l'IFC.
Aucune bibliothèque .NET équivalente n'existe ; ce script est invoqué en
sous-processus depuis Bim.Cvc.Infrastructure.IfcOpenShellGeometryExtractor.
"""

import json
import sys


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: ifc_extract.py <entree.ifc> <sortie.json>", file=sys.stderr)
        return 2

    input_path, output_path = sys.argv[1], sys.argv[2]

    try:
        import ifcopenshell
        import ifcopenshell.geom
        import ifcopenshell.util.unit
    except ImportError as exc:
        print(f"ifcopenshell n'est pas installé sur ce serveur : {exc}", file=sys.stderr)
        return 3

    try:
        model = ifcopenshell.open(input_path)
    except Exception as exc:
        print(f"fichier IFC invalide ou illisible : {exc}", file=sys.stderr)
        return 4

    # Un fichier IFC peut être modélisé en mètres, millimètres, pieds… On
    # normalise systématiquement en mètres (unité déjà utilisée partout
    # ailleurs dans l'application) plutôt que de supposer l'unité du fichier.
    try:
        unit_scale = ifcopenshell.util.unit.calculate_unit_scale(model)
    except Exception:
        unit_scale = 1.0

    settings = ifcopenshell.geom.settings()
    settings.set("use-world-coords", True)

    elements = []
    for product in model.by_type("IfcProduct"):
        if not (product.is_a("IfcElement") or product.is_a("IfcSpace")):
            continue
        if not getattr(product, "Representation", None):
            continue
        try:
            shape = ifcopenshell.geom.create_shape(settings, product)
        except Exception:
            # Un élément dont la géométrie ne se triangule pas (profil
            # dégénéré, représentation non supportée…) est simplement
            # ignoré plutôt que de faire échouer tout l'import.
            continue

        geometry = shape.geometry
        positions = [v * unit_scale for v in geometry.verts]
        indices = list(geometry.faces)
        if not positions or not indices:
            continue

        elements.append(
            {
                "ifcType": product.is_a(),
                "name": product.Name or "",
                "positions": positions,
                "indices": indices,
            }
        )

    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(elements, f)

    print(f"extracted {len(elements)} element(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
