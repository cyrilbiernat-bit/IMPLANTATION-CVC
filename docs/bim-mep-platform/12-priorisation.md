# 16. Priorisation des développements

Méthode : score = Valeur métier (1-5) × Différenciation concurrentielle (1-5) / Effort relatif (1-5), pour
arbitrer l'ordre à l'intérieur de chaque phase de la roadmap (§8).

| Priorité | Bloc fonctionnel | Valeur métier | Différenciation | Effort | Justification |
|---|---|---|---|---|---|
| **P0** | Kernel géométrique + modèle paramétrique (`Core.Geometry`, `Core.Bim`) | 5 | 2 | 5 | Fondation bloquante : rien ne fonctionne sans ça, mais peu différenciant en soi |
| **P0** | Import PDF vectoriel → LOD 100/200 | 5 | 4 | 3 | Premier levier de gain de temps visible dès le MVP |
| **P0** | Aéraulique de base + export IFC | 5 | 2 | 3 | Preuve de bout-en-bout indispensable pour valider la chaîne |
| **P1** | Clash detection (lecture) | 4 | 3 | 2 | Forte attente métier, effort modéré une fois le kernel prêt |
| **P1** | Routage IA (A*/Dijkstra) | 4 | 5 | 4 | Différenciateur majeur vs. Revit MEP/Stabicad (assistance vs. traçage manuel) |
| **P1** | Backend collaboratif (PostgreSQL, verrous, API) | 4 | 2 | 4 | Nécessaire dès qu'on sort du mono-poste, mais peu visible côté ingénieur |
| **P2** | Import PDF scanné (OCR/CV complet) | 4 | 4 | 4 | Beaucoup de plans réels sont scannés — fort impact adoption |
| **P2** | Plomberie + chemins de câbles | 4 | 3 | 3 | Élargit le marché adressable (BE tous corps d'état) |
| **P2** | Clash resolution automatique | 4 | 5 | 4 | Différenciateur fort, mais dépend de la maturité du routage IA (P1) |
| **P2** | Calculs hydraulique/thermique normatifs | 4 | 3 | 3 | Requis pour usage réglementaire réel en bureau d'études |
| **P3** | Copilote IA (commandes simples) | 3 | 5 | 4 | Très différenciant mais non bloquant pour un usage productif de base |
| **P3** | Métrés/nomenclatures automatiques | 3 | 2 | 2 | Valeur métier réelle, effort faible une fois le modèle stable — bon "quick win" |
| **P3** | Rendu Vulkan/WebGPU | 3 | 3 | 5 | Amélioration de confort, non bloquant tant que Helix/Three.js suffit |
| **P4** | Électricité (circuits, tableaux) | 3 | 2 | 4 | Corps d'état supplémentaire, marché plus étroit à ce stade |
| **P4** | Copilote IA avancé (optimisation multi-variantes) | 3 | 5 | 5 | Très différenciant mais effort élevé, à réserver une fois le socle IA mature |
| **P4** | SaaS multi-tenant complet | 3 | 3 | 5 | Stratégique pour le passage à l'échelle commerciale, pas pour la preuve de valeur technique |
| **P5** | Interop Forge/ACC, certification IFC | 2 | 2 | 4 | Utile pour la crédibilité marché, non bloquant produit |

## Règles d'arbitrage

1. **Ne jamais avancer un bloc P(n+1) tant qu'un bloc P(n) bloquant n'est pas livré** — en particulier, le
  routage IA (P1) ne doit pas être entamé avant que le modèle paramétrique (P0) soit stable, sous peine de
  double travail (cf. risque §12.4 du budget).
2. **Les blocs à forte différenciation (routage IA, clash resolution, copilote) sont priorisés dès que leur
   pré-requis technique est prêt**, même s'ils ne sont pas les moins coûteux — c'est ce qui justifie le
   positionnement produit face à Revit MEP/Stabicad (cf. §1 README).
3. **Les "quick wins" (métrés automatiques) sont insérés dès que possible** dans les phases où l'équipe a de
   la capacité résiduelle, car ils génèrent de la valeur visible à faible risque.
