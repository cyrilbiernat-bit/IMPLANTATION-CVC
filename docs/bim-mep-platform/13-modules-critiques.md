# 17–21. Modules critiques — code source d'exemple

Ce document indexe les exemples de code C# livrés sous `src/BimMepPlatform/`. Ce sont des **exemples de
référence illustrant les choix d'architecture** des §5 (schéma BIM), §15 (spécifications techniques) — pas
l'implémentation de production complète (pas de tests exhaustifs, pas de bindings OpenCascade réels, géométrie
simplifiée). Objectif : donner à l'équipe de développement un point de départ concret et cohérent avec le
schéma de classes UML (§2).

> **Compilé et testé.** Un SDK .NET 9 n'étant pas disponible dans l'environnement de rédaction (dépôts Ubuntu
> limités à .NET 8/10), le code a été validé via une copie temporaire retargetée en `net9.0`→`net8.0` (jamais
> les fichiers du repo, qui restent en `net9.0`) : `dotnet build` (0 erreur/0 warning) et `dotnet test`
> (**31/31 tests passent**, projet `Tests/`) ainsi que l'exécution du programme `Samples` bout-en-bout. A
> refaire avec le SDK net9.0 réel avant mise en production.

## Organisation

| Dossier | Contenu | Correspond à |
|---|---|---|
| `Core.Geometry/` | Primitives géométriques minimales (`Point3D`, `Vector3D`, `Transform3D`, `AxisAlignedBox`) tenant lieu de façade simplifiée au wrapper OpenCascade réel (§15.1) | Livrable #18 (exemples de classes C#) |
| `Core.Bim/` | Modèle BIM : paramètres typés, `BimElement` abstrait, `Family`/`FamilyType`, `Project`/`Level`/`Room`, et surtout `RecomputeScheduler` — le **moteur de recalcul paramétrique en cascade** (§5.4) | Livrable #19 (exemple du moteur BIM) |
| `Core.Mep/` | Entités métier MEP : `MepConnector`, `MepNetwork`, `MepDuct` (avec `Recompute()` démontrant la propagation 800x400→1000x500 du §5.4), `MepPipe`, `CableTray`, `MepEquipment` | Livrable #18/#19 |
| `Core.Calculations/` | Calculs physiques purs (docs F-CALC-01/02/03) : `AerauliqueCalculator` (vitesse, pertes de charge lin./sing.), `HydrauliqueCalculator` (Reynolds, Swamee-Jain), `ThermalCalculator` (déperditions simplifiées NF EN 12831) — sans dépendance à Core.Bim/Core.Mep, consommé par `MepNetwork.ComputeLosses` et `RoutingService.OptimizeForWeight` | Extension post-livraison initiale (§16 P2) |
| `Core.Routing/` | Graphe 3D, `AStarPathFinder` et `DijkstraPathFinder`, `RoutingService` (routage + optimisation poids/pertes de charge, délègue le calcul physique à `Core.Calculations`) | Livrable #20 (exemple du moteur de routage) |
| `Core.ClashDetection/` | BVH (arbre de volumes englobants avec élagage réel), `ClashDetector` (détection dure + dégagement), `ClashResolver` (proposition de décalage automatique) | Livrable #21 (exemple du module Clash Detection) |
| `Tests/` | Suite xUnit (31 tests) : cascade de recalcul + détection de cycle (Core.Bim), redimensionnement/avertissement de discontinuité (Core.Mep), valeurs de référence aéraulique/hydraulique/thermique (Core.Calculations), A* avec/sans obstacle (Core.Routing), BVH + clash + résolution (Core.ClashDetection) | Vérification (non demandée dans la liste initiale des 23 livrables, ajoutée pour fiabiliser le reste) |

## Choix de simplification assumés (à lever en production)

1. `Core.Geometry` remplace ici OpenCascade par des primitives analytiques (boîtes/segments) suffisantes pour
   illustrer les algorithmes de routage/clash — l'intégration réelle appelle `libbimgeo` (§15.2) pour toute
   géométrie BRep.
2. La persistance (PostgreSQL, §4) n'est pas câblée : les classes exposées sont des objets en mémoire,
   représentatifs du modèle de domaine à persister.
3. Le `RecomputeScheduler` illustre l'algorithme (tri topologique + détection de cycle) sans l'intégration au
   bus d'événements complet du moteur de production.
4. `Core.Calculations` retient des formules reconnues (Darcy-Weisbach, Swamee-Jain, NF EN 12831 simplifié) mais
   des coefficients indicatifs (frottement gaine 0.02, rugosité acier 0.15 mm) : les tests vérifient la
   cohérence interne des calculs (valeurs de référence calculées à la main, plages plausibles) mais pas leur
   conformité littérale aux abaques normalisés — à confronter aux abaques EN 12237/ASHRAE avant usage réel.
