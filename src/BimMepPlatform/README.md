# BimMepPlatform — exemples de code des modules critiques

Voir `docs/bim-mep-platform/13-modules-critiques.md` à la racine du repository pour le contexte complet
(quel livrable chaque dossier illustre, choix de simplification assumés, détail de la validation IfcOpenShell).

Modules : `Core.Geometry`, `Core.Bim`, `Core.Mep`, `Core.Calculations`, `Core.Routing`, `Core.ClashDetection`,
`Core.Ifc` (export IFC4/STEP), `Core.Takeoff` (métrés/nomenclatures), `Samples` (programme console démontrant
cinq scénarios du cahier des charges : redimensionnement paramétrique 800×400→1000×500, routage A* avec
comparaison de variantes, résolution automatique d'un conflit gaine/poutre, export IFC4 du modèle,
nomenclature + export CSV), `Tests` (suite xUnit, 59 tests).

Cible `net9.0`. Vérifié dans cette session via un SDK .NET 8 (le dépôt Ubuntu ne propose pas net9.0) sur une
copie temporaire retargetée — jamais ces fichiers, qui restent en net9.0. Build et tests propres :
`dotnet build BimMepPlatform.sln` (0 erreur/0 warning), `dotnet test` (59/59), `dotnet run --project Samples`.
Le fichier `.ifc` généré par `Samples` a en outre été rouvert et parsé par **IfcOpenShell** (Python), avec
génération effective de la géométrie triangulée de chaque `IfcDuctSegment`/`IfcPipeSegment`/
`IfcCableCarrierSegment` — une validation par un vrai moteur IFC, pas seulement une vérification syntaxique.
Avec un SDK net9.0 réel, refaire ces commandes avant toute intégration en production.
