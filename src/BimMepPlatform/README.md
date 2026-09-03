# BimMepPlatform — exemples de code des modules critiques

Voir `docs/bim-mep-platform/13-modules-critiques.md` à la racine du repository pour le contexte complet
(quel livrable chaque dossier illustre, choix de simplification assumés).

Modules : `Core.Geometry`, `Core.Bim`, `Core.Mep`, `Core.Calculations`, `Core.Routing`, `Core.ClashDetection`,
`Samples` (programme console démontrant les trois scénarios du cahier des charges : redimensionnement
paramétrique 800×400→1000×500, routage A* avec comparaison de variantes, résolution automatique d'un conflit
gaine/poutre), `Tests` (suite xUnit, 31 tests).

Cible `net9.0`. Vérifié dans cette session via un SDK .NET 8 (le dépôt Ubuntu ne propose pas net9.0) sur une
copie temporaire retargetée — jamais ces fichiers, qui restent en net9.0. Build et tests propres :
`dotnet build BimMepPlatform.sln` (0 erreur/0 warning), `dotnet test` (31/31), `dotnet run --project Samples`.
Avec un SDK net9.0 réel, refaire ces trois commandes avant toute intégration en production.
