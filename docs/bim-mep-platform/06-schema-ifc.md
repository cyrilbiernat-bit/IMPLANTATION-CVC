# 6. Schéma IFC

## 6.1 Portée de compatibilité

| Schéma | Usage | Statut cible |
|---|---|---|
| IFC2x3 (TC1) | Compatibilité descendante (beaucoup d'outils AEC encore en 2x3) | Import/Export complet |
| IFC4 (Add2 TC1) | Schéma pivot recommandé (MEP mieux couvert : `IfcDistributionElement` enrichi) | Import/Export complet, format natif d'échange |
| IFC4.3 | Infrastructure + alignement (utile en conception-réalisation avec VRD) | Export ciblé, import best-effort |

## 6.2 Mapping modèle interne → entités IFC

| Entité interne | Entité IFC | Notes |
|---|---|---|
| `Project` | `IfcProject` | Une seule instance racine |
| `Level` | `IfcBuildingStorey` | `Elevation` mappé directement |
| `Room` | `IfcSpace` | `Boundary` → `IfcSpace.Representation` (footprint) + `IfcRelSpaceBoundary` |
| `MepDuct` (rect.) | `IfcDuctSegment` + `IfcDuctSegmentType` | `PredefinedType = RIGIDSEGMENT` |
| `MepDuct` (circ.) | `IfcDuctSegment` | Profil `IfcCircleProfileDef` |
| Raccord de gaine | `IfcDuctFitting` | `PredefinedType` selon géométrie (BEND, TRANSITION, JUNCTION, ...) |
| `MepPipe` | `IfcPipeSegment` + `IfcPipeSegmentType` | `SystemType` → `IfcDistributionSystem.PredefinedType` |
| Raccord de tuyauterie | `IfcPipeFitting` | |
| `CableTray` | `IfcCableCarrierSegment` | |
| `Cable` | `IfcCableSegment` | |
| `MepEquipment` (CTA, pompe, chaudière...) | `IfcUnitaryEquipment` / `IfcBoiler` / `IfcPump` / `IfcChiller` / `IfcAirTerminal` | Choix selon la catégorie fonctionnelle |
| `MepConnector` | `IfcDistributionPort` | Relié par `IfcRelConnectsPorts` |
| `MepNetwork` | `IfcDistributionSystem` / `IfcDistributionCircuit` | Regroupe les éléments via `IfcRelAssignsToGroup` |
| `Family` / `FamilyType` | `IfcElementType` (sous-type propre à la catégorie) | Relié via `IfcRelDefinesByType` |
| Paramètres (JSONB) | `IfcPropertySet` (`Pset_...` standard quand applicable, sinon `Pset_BimMep_<Categorie>`) | Les Psets standards Buildingsmart sont utilisés en priorité (ex. `Pset_DuctSegmentTypeCommon`) |
| Calorifuge | `IfcMaterialLayerSet` associé (couche isolant) | Épaisseur, conductivité en propriétés |
| Classification système | `IfcDistributionSystem.PredefinedType` (`AIRCONDITIONING`, `VENTILATION`, `DOMESTICCOLDWATER`, `SEWAGE`, `ELECTRICAL`, ...) | |
| `IfcGuid` interne | `GlobalId` IFC (même valeur, généré une fois) | Garantit la stabilité d'identité à travers les allers-retours |

## 6.3 Flux d'export

```mermaid
flowchart LR
    A[Modèle BIM interne] --> B[Sélection du périmètre<br/>+ LOD cible]
    B --> C[Résolution des types<br/>IfcElementType]
    C --> D[Génération géométrie<br/>IfcShapeRepresentation<br/>Body/Axis/FootPrint selon LOD]
    D --> E[Génération topologie<br/>IfcRelConnectsPorts,<br/>IfcRelAssignsToGroup]
    E --> F[Génération Psets<br/>standard + propriétaires]
    F --> G[Sérialisation STEP<br/>(.ifc) ou ifcXML/ifcJSON]
    G --> H[Validation<br/>(ifcOpenShell + bSDD checks)]
    H --> I[Export final]
```

- **LOD → niveau de représentation géométrique IFC** : LOD 100/200 → `FootPrint`/`Axis` uniquement (pas de `Body`
  détaillé) ; LOD 300+ → `Body` (BRep ou tessellation `IfcTriangulatedFaceSet`) ; LOD 400+ → ajout des attributs
  fabricant/installation dans les Psets.
- **Validation systématique avant livraison** : contrôle STEP (conformité au schéma EXPRESS), contrôle sémantique
  (bSDD / IDS — *Information Delivery Specification* — pour vérifier la conformité aux exigences du projet).

## 6.4 Flux d'import (IFC en entrée d'un projet)

1. Parsing STEP via **IfcOpenShell** (pipeline Python, robuste aux variations d'implémentation des éditeurs tiers).
2. Résolution de la hiérarchie spatiale (`IfcProject` → `IfcSite` → `IfcBuilding` → `IfcBuildingStorey` → `IfcSpace`).
3. Mapping inverse du tableau §6.2 : chaque entité IFC MEP reconnue devient une occurrence dans le modèle interne,
   avec **conservation du GlobalId** comme `IfcGuid` (pas de régénération → permet la synchronisation bidirectionnelle
   avec la maquette architecte/structure mise à jour par des tiers).
4. Les entités IFC non reconnues (extensions propriétaires d'autres éditeurs) sont conservées en **objets opaques**
   (géométrie affichée, non paramétrique) plutôt que rejetées — évite la perte de contexte visuel.
5. Rapport d'import : éléments mappés / éléments opaques / erreurs de géométrie, présenté à l'ingénieur avant
   validation définitive (jamais d'import silencieux qui écraserait un travail en cours).

## 6.5 Synchronisation multi-lots (IFC comme bus d'échange)

Pattern **BCF (BIM Collaboration Format)** pour les échanges de clash/observations avec les autres corps d'état
(architecte, structure), en complément de l'IFC : `Core.Ifc` expose un sous-module `Bcf` (import/export BCF XML/JSON)
consommé par `Core.ClashDetection` pour publier les conflits vers les outils tiers (BIMcollab, Solibri, etc.).
