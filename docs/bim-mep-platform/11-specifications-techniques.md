# 15. Spécifications techniques détaillées

## 15.1 Kernel géométrique (`Core.Geometry`)

- **OpenCascade (OCCT) 7.8+** compilé en bibliothèque native (C++), exposé au C# via un wrapper fin
  (P/Invoke direct sur une API C stable exportée par une couche C++ intermédiaire — préférer cette approche à
  C++/CLI pour rester portable Linux/Windows, cf. besoin de services serveur Linux).
- Représentation **BRep** (Boundary Representation) pour toute géométrie devant subir des opérations booléennes
  (perçages de gaines dans une dalle, intersections pour clash) ; **tessellation** (maillage triangulé) dérivée
  à la demande pour le rendu, jamais l'inverse (le maillage n'est jamais la source de vérité géométrique).
- Primitives exposées : extrusion de profil (rect./circ.) le long d'un chemin (génération de tronçons de gaine/
  tuyauterie), congés/chanfreins (raccords), opérations booléines (union/soustraction pour perçages), calcul de
  bounding box et de volume/surface (métrés).
- Tolérance géométrique unique et centralisée (`GeometryTolerances.Default = 1e-4 m`) — toute comparaison de
  points/distances passe par cette tolérance pour éviter les incohérences de connexions MEP à cheval sur
  plusieurs échelles de projet.

## 15.2 Interop C++ ↔ .NET

```
[C# Core.Bim] --P/Invoke--> [libbimgeo.so / bimgeo.dll (C++)] --> [OpenCascade (OCCT)]
```

- La couche C++ (`libbimgeo`) expose une API C plate (structures POD, pas de STL dans la signature) pour
  stabilité ABI. Les erreurs remontent par code de retour + buffer de message, jamais par exception C++
  traversant la frontière P/Invoke.
- Marshalling des géométries volumineuses (maillages) par buffers partagés (mémoire mappée) plutôt que par
  copie répétée, pour les gros modèles.

## 15.3 Backend applicatif

| Aspect | Choix | Détail |
|---|---|---|
| Framework | ASP.NET Core (.NET 9) | API Gateway (REST pour CRUD/simple, gRPC pour flux modèle/streaming) |
| ORM | EF Core (+ Npgsql pour PostGIS) | Migrations versionnées dans `db/migrations` |
| Authentification | OIDC (Keycloak self-hosted ou Auth0 managé) | JWT courts + refresh, RBAC par projet (`project_members.role`) |
| Cache/verrous | Redis | Verrous collaboratifs (§4.5), cache de requêtes catalogue |
| File de jobs asynchrones | RabbitMQ ou NATS JetStream | Jobs d'import IA, exports lourds, recalculs de réseau différés |
| Observabilité | OpenTelemetry → Prometheus/Grafana + logs structurés (Serilog) | Traçage bout-en-bout d'un import (utile pour diagnostiquer les échecs IA) |

## 15.4 Rendu 3D

| Cible | Techno court terme | Techno cible | Notes |
|---|---|---|---|
| Desktop Windows | Helix Toolkit (DirectX 11) | Vulkan via Silk.NET (bindings .NET) | Migration progressive, interface `IRenderBackend` commune pour permuter |
| Web SaaS | Three.js (WebGL2, aligné avec le prototype existant) | WebGPU (avec repli WebGL2) | Réutilisation du savoir-faire déjà présent dans `render3d/` |
| Format d'échange rendu | glTF/.glb (dérivé du BRep, LOD de maillage adapté à la distance caméra) | idem | Un seul exporteur de maillage partagé Desktop/Web |

Techniques : instancing GPU pour les familles répétées (diffuseurs, colliers de fixation), frustum culling +
occlusion culling hiérarchique (BVH partagé avec `Core.ClashDetection`, cf. §15.6), LOD de maillage dynamique.

## 15.5 IA — infrastructure

- **Entraînement** : cluster GPU (cloud, à la demande — pas d'infrastructure dédiée permanente en MVP/V1),
  PyTorch + gestion d'expériences (MLflow) pour la traçabilité des versions de modèle.
- **Inférence** : ONNX Runtime, avec **exécution identique** Desktop (CPU/GPU local, mode offline) et serveur
  (GPU, traitement batch) — un seul artefact modèle versionné, pas de divergence d'implémentation.
- **Format d'échange pipeline** : chaque étape produit un objet intermédiaire sérialisé (Protobuf) stocké en
  object storage, permettant de rejouer une étape sans tout refaire (cf. §7.2.2).

## 15.6 Clash Detection — infrastructure

- **BVH (Bounding Volume Hierarchy)** reconstruit incrémentalement (pas de reconstruction complète à chaque
  modification — mise à jour locale du sous-arbre impacté) pour supporter le clash quasi temps réel pendant
  la modélisation interactive, et un mode batch complet pour un contrôle exhaustif avant livraison.
- Pré-filtrage par bounding box (PostGIS `&&` sur la colonne `bbox`, cf. §4.2) pour les clash inter-lots à
  grande échelle (maquette fédérée multi-BE), avant le test géométrique fin (BRep-BRep via OpenCascade).

## 15.7 IFC — infrastructure

- **IfcOpenShell** (Python) pour le parsing/écriture robuste (gère les variations d'implémentation des éditeurs
  tiers), appelé comme service dédié (`Services.Ifc` ou job asynchrone) plutôt qu'embarqué dans le moteur C#
  temps réel.
- Un mapping objet natif C# (`Core.Ifc`) pour l'export **direct depuis le moteur** quand la performance prime
  (export rapide en cours de session) ; les deux implémentations partagent la même table de mapping (§6.2),
  testée par un jeu de fichiers IFC de référence (buildingSmart sample files) en CI.

## 15.8 Sécurité

- Chiffrement au repos (object storage + PostgreSQL) et en transit (TLS partout, y compris inter-services).
- Isolation multi-tenant stricte au niveau base (schéma par tenant ou `organization_id` en RLS PostgreSQL —
  Row-Level Security activée par défaut sur toutes les tables métier).
- Journal d'audit immuable des actions sensibles (partage de projet, export, changement de permission).

## 15.9 Performance — objectifs cibles

| Scénario | Objectif |
|---|---|
| Ouverture d'un projet de 50 000 éléments MEP (Desktop) | < 8 s |
| Frame rate navigation 3D (maquette moyenne, poste milieu de gamme) | ≥ 60 fps |
| Clash detection incrémental (après une modification locale) | < 500 ms |
| Clash detection complet (batch, 50 000 éléments) | < 2 min |
| Import PDF vectoriel (plan de plateau ~2000 m²) → LOD 100 | < 3 min |
| Recalcul paramétrique en cascade (modification d'un tronçon) | < 200 ms perçu |
