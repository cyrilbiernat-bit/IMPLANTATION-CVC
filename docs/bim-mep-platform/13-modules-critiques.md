# 17–21. Modules critiques — code source d'exemple

Ce document indexe les exemples de code C# livrés sous `src/BimMepPlatform/`. Ce sont des **exemples de
référence illustrant les choix d'architecture** des §5 (schéma BIM), §15 (spécifications techniques) — pas
l'implémentation de production complète (pas de tests exhaustifs, pas de bindings OpenCascade réels, géométrie
simplifiée). Objectif : donner à l'équipe de développement un point de départ concret et cohérent avec le
schéma de classes UML (§2).

> **Compilé et testé.** Un SDK .NET 9 n'étant pas disponible dans l'environnement de rédaction (dépôts Ubuntu
> limités à .NET 8/10), le code a été validé via une copie temporaire retargetée en `net9.0`→`net8.0` (jamais
> les fichiers du repo, qui restent en `net9.0`) : `dotnet build` (0 erreur/0 warning) et `dotnet test`
> (**59/59 tests unitaires** + **6/6 tests d'intégration**) ainsi que l'exécution du programme `Samples`
> bout-en-bout. Le fichier `.ifc` produit par `Core.Ifc` a été **validé avec IfcOpenShell** (parsing IFC4 réel
> + géométrie triangulée via son moteur OCCT, §4) et la persistance `Services.ProjectManagement` a été
> **validée contre un vrai PostgreSQL 16 + PostGIS 3.4** (installés dans cette session, migrations générées et
> appliquées, round-trip complet, §5). Les deux validations externes ont chacune révélé un bug réel — voir §4
> et §5. A refaire avec le SDK net9.0 réel avant mise en production.

## Organisation

| Dossier | Contenu | Correspond à |
|---|---|---|
| `Core.Geometry/` | Primitives géométriques minimales (`Point3D`, `Vector3D`, `Transform3D`, `AxisAlignedBox`) tenant lieu de façade simplifiée au wrapper OpenCascade réel (§15.1) | Livrable #18 (exemples de classes C#) |
| `Core.Bim/` | Modèle BIM : paramètres typés, `BimElement` abstrait (porte desormais une reference `Level?`, cf. docs §4.2), `Family`/`FamilyType`, `Project`/`Level`/`Room`, et surtout `RecomputeScheduler` — le **moteur de recalcul paramétrique en cascade** (§5.4) | Livrable #19 (exemple du moteur BIM) |
| `Core.Mep/` | Entités métier MEP : `MepConnector`, `MepNetwork`, `MepDuct` (avec `Recompute()` démontrant la propagation 800x400→1000x500 du §5.4), `MepPipe`, `CableTray`, `MepEquipment` | Livrable #18/#19 |
| `Core.Calculations/` | Calculs physiques purs (docs F-CALC-01/02/03) : `AerauliqueCalculator` (vitesse, pertes de charge lin./sing.), `HydrauliqueCalculator` (Reynolds, Swamee-Jain), `ThermalCalculator` (déperditions simplifiées NF EN 12831) — sans dépendance à Core.Bim/Core.Mep, consommé par `MepNetwork.ComputeLosses` et `RoutingService.OptimizeForWeight` | Extension post-livraison initiale (§16 P2) |
| `Core.Routing/` | Graphe 3D, `AStarPathFinder` et `DijkstraPathFinder`, `RoutingService` (routage + optimisation poids/pertes de charge, délègue le calcul physique à `Core.Calculations`) | Livrable #20 (exemple du moteur de routage) |
| `Core.ClashDetection/` | BVH (arbre de volumes englobants avec élagage réel), `ClashDetector` (détection dure + dégagement), `ClashResolver` (proposition de décalage automatique) | Livrable #21 (exemple du module Clash Detection) |
| `Core.Ifc/` | `IfcStepWriter` (primitives STEP/EXPRESS bas niveau) + `IfcProjectExporter` (hiérarchie spatiale Project→Site→Building→Storey/Space, export IfcDuctSegment/IfcPipeSegment/IfcCableCarrierSegment/IfcUnitaryEquipment avec géométrie extrudée réelle et Psets, docs §6.2) | Extension post-livraison initiale (§16 P0 — "export IFC basique") |
| `Core.Takeoff/` | `TakeoffService` (docs F-TAKEOFF-01/02) : nomenclature agrégée par catégorie/dimension/système (poids de gaine, surface de calorifuge, longueurs), export CSV | Extension post-livraison initiale (§16 P3 — "quick win" identifié en priorisation) |
| `Services.ProjectManagement/` | `BimMepDbContext` (EF Core + Npgsql + NetTopologySuite/PostGIS) mappant le schéma documenté (§4.2), `ProjectMapper` (domaine ↔ entités, JSONB pour les paramètres spécifiques par catégorie), `ProjectRepository` (sauvegarde/rechargement de l'agrégat Project), migrations générées (`Migrations/`) | Extension post-livraison initiale (§16 P1 — "backend collaboratif") |
| `Tests/` | Suite xUnit (59 tests) : cascade de recalcul + détection de cycle (Core.Bim), redimensionnement/avertissement de discontinuité (Core.Mep), valeurs de référence aéraulique/hydraulique/thermique (Core.Calculations), A* avec/sans obstacle (Core.Routing), BVH + clash + résolution (Core.ClashDetection), structure STEP + non-régression du bug de sérialisation (Core.Ifc), regroupement et calculs de métrés (Core.Takeoff) | Vérification (non demandée dans la liste initiale des 23 livrables, ajoutée pour fiabiliser le reste) |
| `Tests.Integration/` | 6 tests xUnit contre un **vrai PostgreSQL** (round-trip gaine/tuyauterie/pièce, connecteurs mutuellement liés, contrainte d'unicité IfcGuid) — nécessite une base disponible (§5), séparé de `Tests/` pour ne pas imposer cette dépendance aux tests unitaires rapides | Vérification |

## Choix de simplification assumés (à lever en production)

1. `Core.Geometry` remplace ici OpenCascade par des primitives analytiques (boîtes/segments) suffisantes pour
   illustrer les algorithmes de routage/clash — l'intégration réelle appelle `libbimgeo` (§15.2) pour toute
   géométrie BRep.
2. `Services.ProjectManagement` ne round-trippe pas Family/FamilyType (`FamilyTypeId` reste `null` a la
   sauvegarde) ni les Psets/manufacturers/element_revisions (tables absentes de cette premiere passe,
   docs §5 ci-dessous) ; chaque categorie MEP porte ses parametres specifiques dans une colonne JSONB
   plutot que des colonnes dediees par sous-type, ce qui suffit a reconstruire un objet de domaine
   fonctionnellement equivalent mais s'ecarte d'un mapping ORM classe-par-classe.
3. Le `RecomputeScheduler` illustre l'algorithme (tri topologique + détection de cycle) sans l'intégration au
   bus d'événements complet du moteur de production.
4. `Core.Calculations` retient des formules reconnues (Darcy-Weisbach, Swamee-Jain, NF EN 12831 simplifié) mais
   des coefficients indicatifs (frottement gaine 0.02, rugosité acier 0.15 mm) : les tests vérifient la
   cohérence interne des calculs (valeurs de référence calculées à la main, plages plausibles) mais pas leur
   conformité littérale aux abaques normalisés — à confronter aux abaques EN 12237/ASHRAE avant usage réel.
5. `Core.Ifc` place chaque élément en coordonnées absolues (`IfcLocalPlacement.PlacementRelTo = $` partout)
   plutôt qu'en hiérarchie de placements relatifs à la structure spatiale ; la géométrie des troncons MEP
   suit la même limitation que `Core.Geometry` (direction horizontale uniquement — pas d'inclinaison) ; un
   seul Pset propriétaire minimal par élément (pas de Pset standard buildingSmart) ; `MepEquipment` reçoit une
   géométrie de substitution (cube 0.6 m) faute de famille BIM fabricant réelle.
6. `Core.Takeoff` ne chiffre un poids que pour les gaines (tôle) : tuyauteries et chemins de câbles n'ont pas
   de formule de poids linéique fiable sans catalogue fabricant (matériau/DN variables) dans ce dossier
   d'exemples — leur ligne ne porte que longueur et nombre. L'export CSV utilise virgule + point décimal
   (culture invariante) ; un import Excel localisé en français demande souvent un délimiteur `;`, à adapter
   à l'intégration.

## 4. Validation externe du fichier IFC (IfcOpenShell)

Le programme `Samples` exporte le mini-modèle de démonstration en `.ifc` (scénario [4]). Ce fichier a été
**réellement rouvert et interprété par IfcOpenShell** (bibliothèque Python de référence pour IFC, docs §15.7) :

- `ifcopenshell.open(...)` : parsing IFC4 réussi, hiérarchie spatiale et éléments MEP retrouvés
  (`IfcProject`/`IfcSite`/`IfcBuilding`/`IfcBuildingStorey`, 3× `IfcDuctSegment`, Psets, containment).
- `ifcopenshell.geom.create_shape(...)` : génération effective de la géométrie triangulée (via le moteur
  OCCT sous-jacent) pour chaque `IfcDuctSegment`, avec vérification des bounding box obtenues contre les
  dimensions/placements attendus (longueur, section, décalage appliqué par le clash resolver).

**Bug réel trouvé et corrigé grâce à cette validation** : `IfcStepWriter.Write(string, params object?[])`
s'appuyait sur le passage direct d'un tableau `object?[]` en C# pour les listes imbriquées (ex.
`IfcCartesianPoint.Coordinates`, attribut unique de type LIST). Or, quand un tel tableau est le **seul**
argument variadique d'un appel, C# le transmet tel quel comme tableau params plutôt que de l'encapsuler comme
un élément — produisant `IFCCARTESIANPOINT(0.,0.,0.)` (trois attributs plats, invalide) au lieu de
`IFCCARTESIANPOINT((0.,0.,0.))` (un attribut liste, correct). Une vérification purement syntaxique/maison
n'aurait pas détecté cette erreur (le fichier "ressemblait" à du STEP valide) ; seule l'ouverture par un vrai
moteur IFC l'a révélée (`RuntimeError: Unexpected topology` puis `Failed to process shape`). Corrigé en
utilisant `List<object?>` (type non éligible au passage direct) aux points d'appel concernés — voir
`IfcProjectExporter.CreateCartesianPoint`/`CreateDirection` et `CreateUnitAssignment`, et la suite de
non-régression `Tests/IfcExportTests.cs`.

Un second correctif a porté sur le formatage des réels en notation scientifique (`1E-05` invalide en
grammaire EXPRESS faute de point décimal dans la mantisse ; corrigé en `1.e-05`).

Cet exercice illustre concrètement pourquoi docs §15.7 recommande IfcOpenShell comme référence de validation
plutôt que de faire confiance à un exporteur natif non testé contre un vrai parseur.

## 5. Validation externe de la persistance (PostgreSQL + PostGIS réels)

PostgreSQL 16 et l'extension PostGIS 3.4 ont été installés dans cette session (`apt install postgresql
postgresql-16-postgis-3`) pour valider `Services.ProjectManagement` contre une vraie base, pas seulement des
migrations générées sans jamais être appliquées :

- `dotnet ef migrations add`/`database update` ont produit et appliqué un schéma réel ; `\d bim_elements` en
  psql confirme des colonnes `jsonb`, `geometry(PointZ,0)`/`geometry(PolygonZ,0)` et des contraintes FK/unique
  identiques au schéma documenté (§4.2), une fois `EFCore.NamingConventions` ajouté pour obtenir des noms de
  colonnes en snake_case (EF Core produit du PascalCase par défaut, ce qui aurait divergé du SQL de référence).
- Le programme `Samples` (scénario [6]) sauvegarde le mini-modèle de démonstration puis le recharge en
  entier ; `Tests.Integration/` (6 tests) vérifie plus précisément : dimensions/matériau/placement d'une
  gaine, diamètre d'une gaine circulaire, système/pente d'une tuyauterie, limite polygonale d'une pièce
  (PostGIS), et la contrainte unique sur `ifc_guid`.

**Bug réel trouvé et corrigé grâce à cette validation** : `MepConnector.ConnectTo` est bidirectionnel
(`Core.Mep`, §5.6) — deux connecteurs nouvellement créés peuvent donc se référencer mutuellement via
`connected_to_id` avant même d'exister en base. Un premier essai de sauvegarde en un seul `SaveChangesAsync`
a levé `InvalidOperationException: circular dependency detected`, PostgreSQL (comme tout SGBD relationnel)
ne pouvant pas insérer deux lignes qui se référencent l'une l'autre en une seule opération. Corrigé par une
sauvegarde en deux passes dans `ProjectRepository.SaveNewProjectAsync` : insertion de tous les connecteurs
avec `ConnectedToId = null`, puis une seconde passe qui renseigne les références croisées une fois toutes les
lignes présentes — voir `ProjectMapper.CollectConnectorLinks` et le test
`SaveNewProject_MutuallyConnectedConnectors_ResolveCrossReferencesInSecondPass`.

Comme pour l'export IFC (§4), ce bug n'aurait pas été détecté par une relecture de code ou des tests unitaires
sur des objets en mémoire : il ne se manifeste qu'au moment d'un véritable `INSERT` contraint par des clés
étrangères, d'où l'intérêt de tester contre un SGBD réel plutôt qu'un double en mémoire.
