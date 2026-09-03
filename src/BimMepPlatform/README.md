# BimMepPlatform — exemples de code des modules critiques

Voir `docs/bim-mep-platform/13-modules-critiques.md` à la racine du repository pour le contexte complet
(quel livrable chaque dossier illustre, choix de simplification assumés, détail des validations IfcOpenShell
et PostgreSQL).

Modules : `Core.Geometry`, `Core.Bim`, `Core.Mep`, `Core.Calculations`, `Core.Routing`, `Core.ClashDetection`,
`Core.Ifc` (export IFC4/STEP), `Core.Takeoff` (métrés/nomenclatures), `Services.ProjectManagement`
(persistance EF Core/PostgreSQL/PostGIS), `Samples` (programme console démontrant six scénarios du cahier des
charges : redimensionnement paramétrique, routage A*, résolution de conflit, export IFC4, nomenclature +
CSV, sauvegarde/rechargement PostgreSQL), `Tests` (suite xUnit unitaire, 59 tests), `Tests.Integration`
(6 tests contre une vraie base PostgreSQL).

Cible `net9.0`. Vérifié dans cette session via un SDK .NET 8 (le dépôt Ubuntu ne propose pas net9.0) sur une
copie temporaire retargetée — jamais ces fichiers, qui restent en net9.0. Build et tests propres :
`dotnet build BimMepPlatform.sln` (0 erreur/0 warning), `dotnet test Tests/Tests.csproj` (59/59).

Deux validations sont allées au-delà de la simple compilation :
- Le fichier `.ifc` généré par `Samples` a été rouvert et parsé par **IfcOpenShell** (Python), avec
  génération effective de la géométrie triangulée de chaque élément linéaire — une validation par un vrai
  moteur IFC.
- La persistance a été testée contre un **PostgreSQL 16 + PostGIS 3.4 réel** (installé dans cette session) :
  migrations générées et appliquées, schéma vérifié via `psql`, et `Tests.Integration/Tests.Integration.csproj`
  (6/6) exécuté avec succès contre cette base — nécessite `BIMMEP_CONNECTION_STRING` (ou l'instance locale par
  défaut `Host=localhost;Database=bimmep_dev;Username=bimmep;Password=bimmep_dev`) et les migrations
  appliquées (`dotnet ef database update --project Services.ProjectManagement`).

Chacune de ces deux validations a révélé un bug réel qu'une relecture de code seule n'aurait pas trouvé
(voir docs §4 et §5) — corrigés, avec suite de non-régression. Avec un SDK net9.0 réel, refaire ces
commandes avant toute intégration en production.
