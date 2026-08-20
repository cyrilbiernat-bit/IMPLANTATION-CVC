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
