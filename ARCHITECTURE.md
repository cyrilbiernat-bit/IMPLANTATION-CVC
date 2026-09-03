# Architecture

## Application principale : `implantation_cvc_plb.html` (mono-fichier, hors-ligne)

Depuis la version « v20 », l'application est livrée comme un **fichier HTML unique et
autonome** (~3,8 Mo) : THREE.js, PDF.js (+ son worker), SheetJS et jsPDF sont
**embarqués directement dans le fichier** plutôt que chargés depuis un CDN.

Avantage : le fichier fonctionne intégralement **sans connexion internet**, ce qui est
important en usage bureau d'études (postes sans accès CDN, chantier, présentation
client hors-ligne). Il suffit d'ouvrir `implantation_cvc_plb.html` dans un navigateur.

Contrepartie : le fichier est volumineux et toute modification des librairies tierces
nécessite de les ré-embarquer (voir « Mise à jour des librairies » ci-dessous).

### Structure interne du fichier

Le fichier contient 7 blocs `<script>` :

1. THREE.js (r128)
2. THREE.OrbitControls
3. PDF.js
4. SheetJS (xlsx.js)
5. jsPDF
6. Worker PDF.js encodé en base64 (chargé en Blob interne, pas de requête réseau)
7. **Code applicatif** (catalogue de symboles, dessin 2D/3D, calculs, exports…) — c'est
   le seul bloc à éditer pour faire évoluer l'outil.

### Domaines couverts par le catalogue

- **CVC** : centrales de traitement d'air, VMC (simple/double flux), diffusion/reprise,
  groupes froid, chaudières, ventilo-convecteurs, régulation, réseaux aérauliques et
  hydrauliques (ECS/ECF/chauffage/évacuation).
- **Plomberie** : appareils sanitaires, pompes, adoucisseurs, disconnecteurs.
- **Protection incendie** (sprinklage / RIA) : têtes sprinkler (pendant/montant/mural),
  postes RIA, vannes à alarme, poteaux incendie, colonnes/points d'attaque,
  extincteurs — avec réseaux dédiés `inc-sprink` / `inc-ria`.
- **Réservations** : passages de dalle, fourreaux, trémies.

### Catalogue fabricants

`MANUFACTURER_CATALOG` associe à chaque type d'équipement une liste de gammes
indicatives (marque + gamme, débit/puissance de repère). Sélectionner un fabricant
dans le panneau propriétés préremplit Fabricant / Référence / Débit / Puissance ; ces
données sont ensuite reprises dans les exports Excel, chiffrage et IFC (jeu de
propriétés `Pset_ManufacturerTypeInformation`). Ce sont des gammes de référence à
affiner avec le catalogue fournisseur à jour au moment du chiffrage.

### Export IFC

`exportIFC()` génère un IFC2X3 avec :
- des `IfcGloballyUniqueId` réellement compressés (algorithme buildingSMART 22
  caractères, fonction `ifcGuid()`), hiérarchie spatiale complète
  (`IfcProject → IfcSite → IfcBuilding → IfcBuildingStorey`) et une unité de longueur
  assignée au projet ;
- un type IFC + `PredefinedType` par métier (`IFC_TYPE_MAP`) plutôt qu'un
  `IfcBuildingElementProxy` générique (ex. `IfcSanitaryTerminal`, `IfcFireSuppressionTerminal`,
  `IfcChiller`, `IfcBoiler`, `IfcPump`, `IfcValve`…) ;
- l'export des réseaux (gaines/tuyauteries) en `IfcDuctSegment` / `IfcPipeSegment`, pas
  seulement des équipements ;
- un `Pset_ManufacturerTypeInformation` (fabricant / référence) et un jeu de propriétés
  `CVC_PLB_Donnees` (repère, débit, puissance, diamètre/section, calorifuge) quand
  l'information est renseignée.

Reste volontairement simplifié : géométrie de type boîte (pas de solide balayé réel
pour les tuyauteries), export limité au niveau actif (comme le DXF).

## Modules extraits (exploratoires, non branchés dans le fichier principal)

| Module | Rôle |
|--------|------|
| `js/core/` | Logique pure : calibration, diamètres, bilan, DXF |
| `js/io/` | Import/export (prévu : JSON projet, IndexedDB plans) |
| `js/catalog/` | Chargement catalogue + packs fabricants |
| `render2d/` | Rendu canvas 2D (à extraire progressivement) |
| `render3d/` | Scène THREE.js (à extraire progressivement) |
| `catalog/symbols.json` | Symboles CVC/PLB externalisés |
| `catalog/packs/` | Packs fabricants (JSON) |

Ces modules ne sont **pas** importés par `implantation_cvc_plb.html` (qui reste
mono-fichier pour rester utilisable hors-ligne sans bundler). Ils documentent une piste
de refactorisation future si le fichier devient trop difficile à maintenir en l'état.

## Tests

```bash
node --test tests/unit.test.js
```

Couverture actuelle : calibration, diamètres/vitesses/ΔP, bilan + graphe débit, export
DXF — sur les fonctions pures extraites dans `js/core/` (pas encore sur le fichier
principal, qui n'est pas un module Node).
