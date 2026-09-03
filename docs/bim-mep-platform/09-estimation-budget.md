# 11/12. Estimation charge (homme/mois) et budget

> Estimations d'ordre de grandeur pour un cadrage budgétaire (± 30 %), à affiner en phase de cadrage détaillé.
> Base : coût chargé moyen France, ingénieur logiciel senior ≈ 8 à 10 k€/mois chargé, expert métier CVC
> ≈ 9 k€/mois chargé, data scientist ≈ 9 k€/mois chargé.

## 12.1 Charge par phase (homme-mois)

| Phase | Kernel/Moteur BIM (C++/C#) | Pipeline IA (Python) | MEP/Métier + Calculs normatifs | UX/UI + Rendu 3D | Backend/Cloud/DevOps | QA | Total h.mois |
|---|---|---|---|---|---|---|---|
| **MVP** (9 mois) | 24 | 10 | 12 | 10 | 8 | 6 | **70** |
| **V1** (11 mois) | 30 | 22 | 20 | 16 | 22 | 14 | **124** |
| **V2** (16 mois) | 26 | 24 | 18 | 24 | 30 | 18 | **140** |
| **Total (36 mois)** | 80 | 56 | 50 | 50 | 60 | 38 | **334** |

Effectif équivalent temps plein moyen sur la durée : ~9-10 personnes en MVP, montant à ~14-15 en V2 (pic).

## 12.2 Répartition indicative des rôles (équipe cible en régime V1/V2)

| Rôle | Effectif |
|---|---|
| Architecte logiciel CAO/BIM senior | 1 |
| Développeurs C++/C# (kernel géométrique, moteur BIM, routage, clash) | 3-4 |
| Data scientists / ingénieurs Computer Vision (import IA, copilote) | 2-3 |
| Ingénieurs métier CVC/plomberie/électricité (specs, validation calculs normatifs) | 2 |
| Développeurs rendu 3D (Vulkan/WebGPU) | 1-2 |
| Développeurs backend/cloud (API, collaboration, infra) | 2-3 |
| UX/UI designer | 1 |
| QA / test automation | 1-2 |
| Product owner / chef de projet | 1 |

## 12.3 Budget (ordre de grandeur, coûts chargés)

| Poste | MVP (9 mois) | V1 (11 mois) | V2 (16 mois) | Total (36 mois) |
|---|---|---|---|---|
| Masse salariale chargée (70/124/140 h.mois × ~9 k€) | 630 k€ | 1 116 k€ | 1 260 k€ | **3 006 k€** |
| Infrastructure cloud (dev+staging, GPU entraînement IA) | 15 k€ | 40 k€ | 90 k€ | 145 k€ |
| Licences/outils (OpenCascade support, CI, monitoring, design) | 10 k€ | 15 k€ | 20 k€ | 45 k€ |
| Données/annotation (corpus plans pour entraînement IA) | 20 k€ | 40 k€ | 30 k€ | 90 k€ |
| Certification IFC / audits sécurité | – | 15 k€ | 35 k€ | 50 k€ |
| Marketing/commercial (hors dev, indicatif) | – | 50 k€ | 150 k€ | 200 k€ |
| **Contingence (15 %)** | 100 k€ | 190 k€ | 240 k€ | 530 k€ |
| **Total** | **~775 k€** | **~1 466 k€** | **~1 825 k€** | **~4 066 k€** |

## 12.4 Facteurs de risque budgétaire (à surveiller)

1. **Qualité des données d'entraînement IA** (plans architecte réels, diversité des chartes graphiques) —
   sous-estimer l'effort d'annotation est le risque n°1 sur le respect du planning import IA.
2. **Robustesse de l'interop OpenCascade/IFC** face aux fichiers réels du marché (IFC "sales" produits par
   certains éditeurs) — prévoir une marge sur `Core.Ifc`/`ai/import-pipeline`.
3. **Recrutement de profils rares** (C++/OpenCascade + BIM métier simultanément) — le planning MVP suppose un
   noyau senior disponible dès le mois 1 ; un retard de recrutement de 2-3 mois sur ce poste décale tout le reste.
4. **Certification buildingSmart** (si visée en V2) : délais externes non maîtrisés, à sécuriser tôt.
