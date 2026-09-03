# Architecture modulaire (P2)

L’application reste utilisable via `implantation_cvc_plb.html` (~280 Ko).
Les bibliothèques (THREE, PDF.js, SheetJS, jsPDF) sont chargées en CDN.

## Découpage cible

| Module | Rôle |
|--------|------|
| `js/core/` | Logique pure : calibration, diamètres, bilan, DXF |
| `js/io/` | Import/export (prévu : JSON projet, IndexedDB plans) |
| `js/catalog/` | Chargement catalogue + packs fabricants |
| `render2d/` | Rendu canvas 2D (à extraire progressivement) |
| `render3d/` | Scène THREE.js (à extraire progressivement) |
| `catalog/symbols.json` | Symboles CVC/PLB externalisés |
| `catalog/packs/` | Packs fabricants (JSON) |

## Tests

```bash
node --test tests/unit.test.js
```

Couverture actuelle : calibration, diamètres/vitesses/ΔP, bilan + graphe débit, export DXF.

## Trajectoire plateforme BIM MEP professionnelle

Ce prototype web reste le banc d'essai UX/calculs. Le dossier d'architecture pour la plateforme BIM MEP 3D
cible (moteur BIM propriétaire, routage IA, clash detection, calculs normatifs, IFC natif — visant la parité
avec Stabicad/Revit MEP/MagiCAD sur les projets de conception-réalisation) est dans
[`docs/bim-mep-platform/`](docs/bim-mep-platform/README.md). Des exemples de code C# des modules critiques
(moteur BIM paramétrique, routage A*/Dijkstra, clash detection) sont dans
[`src/BimMepPlatform/`](src/BimMepPlatform/README.md).
