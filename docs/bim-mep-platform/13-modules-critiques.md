# 17–21. Modules critiques — code source d'exemple

Ce document indexe les exemples de code C# livrés sous `src/BimMepPlatform/`. Ce sont des **exemples de
référence illustrant les choix d'architecture** des §5 (schéma BIM), §15 (spécifications techniques) — pas
l'implémentation de production complète (pas de tests exhaustifs, pas de bindings OpenCascade réels, géométrie
simplifiée). Objectif : donner à l'équipe de développement un point de départ concret et cohérent avec le
schéma de classes UML (§2).

> Environnement de rédaction sans SDK .NET installé : ce code n'a pas été compilé dans cette session. Il est
> stylistiquement homogène et cohérent inter-fichiers (mêmes signatures, mêmes namespaces) mais doit être
> compilé et testé (`dotnet build`, `dotnet test`) avant intégration.

## Organisation

| Dossier | Contenu | Correspond à |
|---|---|---|
| `Core.Geometry/` | Primitives géométriques minimales (`Point3D`, `Vector3D`, `Transform3D`, `AxisAlignedBox`) tenant lieu de façade simplifiée au wrapper OpenCascade réel (§15.1) | Livrable #18 (exemples de classes C#) |
| `Core.Bim/` | Modèle BIM : paramètres typés, `BimElement` abstrait, `Family`/`FamilyType`, `Project`/`Level`/`Room`, et surtout `RecomputeScheduler` — le **moteur de recalcul paramétrique en cascade** (§5.4) | Livrable #19 (exemple du moteur BIM) |
| `Core.Mep/` | Entités métier MEP : `MepConnector`, `MepNetwork`, `MepDuct` (avec `Recompute()` démontrant la propagation 800x400→1000x500 du §5.4), `MepPipe`, `CableTray`, `MepEquipment` | Livrable #18/#19 |
| `Core.Routing/` | Graphe 3D, `AStarPathFinder` et `DijkstraPathFinder`, `RoutingService` (routage + optimisation poids/pertes de charge) | Livrable #20 (exemple du moteur de routage) |
| `Core.ClashDetection/` | BVH (arbre de volumes englobants), `ClashDetector` (détection dure + dégagement), `ClashResolver` (proposition de décalage automatique) | Livrable #21 (exemple du module Clash Detection) |

## Choix de simplification assumés (à lever en production)

1. `Core.Geometry` remplace ici OpenCascade par des primitives analytiques (boîtes/segments) suffisantes pour
   illustrer les algorithmes de routage/clash — l'intégration réelle appelle `libbimgeo` (§15.2) pour toute
   géométrie BRep.
2. La persistance (PostgreSQL, §4) n'est pas câblée : les classes exposées sont des objets en mémoire,
   représentatifs du modèle de domaine à persister.
3. Le `RecomputeScheduler` illustre l'algorithme (tri topologique + détection de cycle) sans l'intégration au
   bus d'événements complet du moteur de production.
