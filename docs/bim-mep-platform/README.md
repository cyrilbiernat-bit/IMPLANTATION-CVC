# Plateforme BIM MEP Professionnelle — Dossier d'architecture

> Équipe projet (rôles simulés pour ce dossier) : Architecte logiciel CAO/BIM senior · Dév. C++/C# OpenCascade/Forge/IFC ·
> Expert MEP (CVC, plomberie, électricité) · Expert IA/Computer Vision · Expert UX/UI · Expert BIM LOD 100→500.

## Objectif

Faire évoluer `implantation-cvc-plb` (prototype web léger : calibration de plans, calepinage 2D/3D, bilans,
export DXF — voir `../../ARCHITECTURE.md`) vers une **plateforme BIM MEP 3D professionnelle** capable de concurrencer
Stabicad / Revit MEP / MagiCAD / DDS-CAD sur les projets de conception-réalisation, avec génération automatique de
maquette à partir de PDF vectoriels, PDF scannés, DWG, IFC et plans d'architecte.

Le prototype web actuel reste la **maquette de validation fonctionnelle** (UX, calculs, calepinage) ; ce dossier
définit la **plateforme cible** (moteur BIM propriétaire, routage IA, clash detection, calculs normatifs, rendu 3D
temps réel, IFC natif) et la trajectoire pour y arriver sans jeter l'existant.

## Sommaire des livrables

| # | Livrable | Document |
|---|----------|----------|
| 1 | Architecture globale détaillée | [01-architecture-globale.md](01-architecture-globale.md) |
| 2 | Diagrammes UML | [02-uml-diagrammes.md](02-uml-diagrammes.md) |
| 3, 13 | Structure du projet / du repository | [03-structure-repository.md](03-structure-repository.md) |
| 4 | Base de données | [04-base-de-donnees.md](04-base-de-donnees.md) |
| 5 | Schéma BIM | [05-schema-bim.md](05-schema-bim.md) |
| 6 | Schéma IFC | [06-schema-ifc.md](06-schema-ifc.md) |
| 7 | Architecture IA | [07-architecture-ia.md](07-architecture-ia.md) |
| 8, 9, 10 | Roadmap MVP / V1 / V2 | [08-roadmap.md](08-roadmap.md) |
| 11, 12 | Estimation charge homme/mois et budget | [09-estimation-budget.md](09-estimation-budget.md) |
| 14 | Spécifications fonctionnelles détaillées | [10-specifications-fonctionnelles.md](10-specifications-fonctionnelles.md) |
| 15 | Spécifications techniques détaillées | [11-specifications-techniques.md](11-specifications-techniques.md) |
| 16 | Priorisation des développements | [12-priorisation.md](12-priorisation.md) |
| 17–21 | Modules critiques (code) : moteur BIM, routage, clash detection | [13-modules-critiques.md](13-modules-critiques.md) + `../../src/BimMepPlatform/` |
| 22 | Architecture SaaS et Desktop | [14-architecture-saas-desktop.md](14-architecture-saas-desktop.md) |
| 23 | Plan de migration cloud collaboratif (type ACC) | [15-plan-migration-cloud.md](15-plan-migration-cloud.md) |

## Positionnement produit

| | Stabicad / MagiCAD | Revit MEP | DDS-CAD | **Notre cible** |
|---|---|---|---|---|
| Hôte | AutoCAD/Revit (plug-in) | Natif Autodesk | Natif | **Natif, indépendant** |
| Import PDF architecte → maquette | Manuel | Manuel (calque lié) | Manuel | **Semi-automatique IA (vision + OCR)** |
| Routage auto gaines/tuyauteries/CdC | Assisté | Assisté (Fabrication) | Assisté | **IA (A*/Dijkstra sur graphe 3D) + règles métier** |
| Calculs normatifs intégrés | Oui (add-on) | Limité (add-ins tiers) | Oui | **Natif (EN 16798, NF EN 12831, RE2020)** |
| Cycle conception-réalisation | Lent (dépendance host) | Lent | Moyen | **Rapide : moteur propriétaire + copilote IA** |
| Licence | Perpétuelle + host | Abonnement + host | Perpétuelle | **SaaS + Desktop offline** |

Le différenciateur produit n'est pas "faire pareil que Revit MEP", c'est **compresser le temps APS→EXE** grâce à
l'automatisation (import IA, routage IA, clash auto-résolu, métrés auto) sur des projets où le planning
conception-réalisation est la contrainte dominante.
