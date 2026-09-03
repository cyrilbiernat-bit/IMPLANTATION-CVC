# BimMepPlatform — exemples de code des modules critiques

Voir `docs/bim-mep-platform/13-modules-critiques.md` à la racine du repository pour le contexte complet
(quel livrable chaque dossier illustre, choix de simplification assumés).

Modules : `Core.Geometry`, `Core.Bim`, `Core.Mep`, `Core.Routing`, `Core.ClashDetection`, `Samples`
(programme console démontrant les trois scénarios du cahier des charges : redimensionnement paramétrique
800×400→1000×500, routage A* avec comparaison de variantes, résolution automatique d'un conflit
gaine/poutre).

Aucun SDK .NET n'était disponible dans l'environnement de rédaction : ce code n'a pas été compilé.
Avant intégration : `dotnet build BimMepPlatform.sln` puis exécuter `dotnet run --project Samples`.
