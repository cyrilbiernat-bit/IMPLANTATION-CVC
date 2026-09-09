# CVC EN 378-1 — Calcul de concentration de fluide frigorigène

Application web professionnelle destinée aux bureaux d'études CVC, frigoristes
et installateurs pour réaliser les calculs de concentration de fluides
frigorigènes selon la méthodologie de la norme **NF EN 378-1**, avec deux
modes de travail :

- **Mode Rapide** — prédimensionnement / étude de faisabilité.
- **Mode Expert** — calcul détaillé par circuit, multi-locaux.

> Le dépôt contient également `implantation_cvc_plb.html`, un outil existant
> d'implantation de plans CVC (2D/3D, calibration, export DXF) indépendant de
> cette application. Voir `ARCHITECTURE.md` pour ce module historique.

## Version autonome au format HTML

**`en378_calculateur.html`** : version 100% autonome de cette application
(un seul fichier, sans backend ni installation) — mêmes fonctionnalités que
la version FastAPI/React ci-dessous : moteur de calcul NF EN 378-1 (Méthodes
C.2 et C.3), bibliothèques fluides/équipements éditables, mode Rapide, mode
Expert (circuits + analyse multilocaux), import Excel, détection de locaux
depuis un plan, Assistant IA (CCTP, moteur heuristique), rapports PDF/XLSX.

À utiliser telle quelle : double-cliquer sur le fichier pour l'ouvrir dans un
navigateur (Chrome, Edge, Firefox), ou le déposer sur un intranet/partage
réseau. Les données du projet sont stockées localement dans le navigateur
(`localStorage`) — aucun envoi à un serveur. Une connexion Internet est
nécessaire uniquement pour charger les bibliothèques de génération de
rapport/import (jsPDF, SheetJS, pdf.js, Tesseract.js), servies depuis
`cdn.jsdelivr.net` au premier usage de ces fonctions ; le reste de l'outil
(calcul, bibliothèques, assistant CCTP) fonctionne hors ligne. Voir le pied
de page de l'outil pour l'avertissement réglementaire complet.

## ⚠️ Avertissement réglementaire

Le moteur de calcul (`backend/app/core/en378.py`) implémente les formules et
tableaux normatifs de l'**Annexe C (normative)** de la **NF EN 378-1+A1
(octobre 2020)** — Formules (C.1)/(C.2) pour la climatisation/PAC de confort
et Tableau C.3 (RCL/QLMV/QLAV) pour la méthode alternative générale — extraits
directement du texte de la norme et validés numériquement par recalcul des
exemples chiffrés de son Annexe H (voir `backend/tests/test_en378.py`). Les
valeurs de fluides non tabulées au Tableau C.3 sont calculées par
l'application à partir de l'Annexe E selon la méthode décrite en C.3.2.1 (une
approximation, documentée comme telle dans la bibliothèque fluides). Le
catalogue équipements constructeurs reste un jeu de données d'exemple. Une
vérification par un professionnel qualifié par rapport à l'édition en vigueur
de la norme et aux fiches de données de sécurité (FDS) des fabricants reste
requise avant toute utilisation à des fins de conformité réglementaire
contractuelle. Les bibliothèques fluides et équipements sont entièrement
éditables dans l'application. Le moteur ne couvre pas la méthode générale des
Tableaux C.1/C.2 (classes d'emplacement I à IV) ni les patinoires (Annexe F).

## Architecture

```
backend/    FastAPI + SQLAlchemy (PostgreSQL en production, SQLite en dev)
  app/core/       Moteur de calcul NF EN 378-1, prédimensionnement CVC,
                   arbre de décision, bases fluides/équipements, analyse CCTP
  app/api/        Routeurs REST (projects, rooms, fluids, equipment,
                   calculations, reports, settings, ai)
  app/reports/    Génération PDF (reportlab) / DOCX (python-docx) / XLSX (openpyxl)
  tests/          pytest (moteur de calcul + API)

frontend/   React + TypeScript + Material UI (Vite)
  src/pages/      Nouveau projet, Calcul rapide, Calcul expert (+ analyse
                   multilocaux), Bibliothèque fluides, Bibliothèque
                   équipements, Assistant IA (CCTP), Rapports, Paramètres
  src/components/ Jauge de conformité, indicateur de risque, heatmap des
                   locaux, diagramme de concentration
```

## Démarrage rapide (développement local)

### Backend

```bash
cd backend
python3 -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
uvicorn app.main:app --reload --port 8000
```

La détection de locaux depuis un plan (voir plus bas) nécessite les outils
système `poppler-utils` (pdftotext/pdftoppm) et `tesseract-ocr` (+ paquet de
langue `tesseract-ocr-fra`) — déjà installés dans l'image Docker fournie ;
en environnement local sans Docker : `apt install poppler-utils tesseract-ocr
tesseract-ocr-fra` (Debian/Ubuntu). Sans ces outils, le reste de l'application
fonctionne normalement (seule cette fonctionnalité est indisponible).

Au démarrage, la base (SQLite par défaut : `backend/cvc_en378.db`) est créée
et peuplée automatiquement avec les fluides et équipements par défaut.
Pour utiliser PostgreSQL, définir `DATABASE_URL` (ex. via `docker-compose`).

Tests :

```bash
cd backend && source .venv/bin/activate && pytest
```

### Frontend

```bash
cd frontend
npm install
npm run dev
```

L'application est servie sur `http://localhost:5173` et proxifie les appels
`/api/*` vers `http://localhost:8000` (backend FastAPI).

### Avec Docker Compose (PostgreSQL + backend + frontend)

```bash
docker compose up --build
```

## Assistant IA (analyse de CCTP) et détection depuis un plan

La page « Assistant IA » extrait automatiquement les locaux mentionnés dans
un CCTP (nom, type, surface) et propose un type de système CVC. Sans clé API
configurée, un moteur heuristique local (mots-clés + expressions régulières)
est utilisé. En définissant la variable d'environnement `ANTHROPIC_API_KEY`
côté backend, l'extraction et la proposition sont réalisées par un modèle
Claude pour une analyse plus fine du texte.

Le mode Expert (onglet « Analyse multilocaux ») permet en plus :

- **l'import d'une liste de locaux depuis un fichier Excel** (colonnes Nom /
  Type / Surface / Hauteur / Fluide / Charge, en-têtes libres) ;
- **la détection automatique de locaux depuis un plan** (PDF ou image) :
  lecture des étiquettes de surface annotées sur le plan (ex. « Bureau 1 —
  18 m² »), via extraction du texte vectoriel pour les PDF issus de CAO/BIM,
  ou par OCR (Tesseract, français) pour les plans scannés/images. Cette
  fonctionnalité repose sur la lecture des annotations textuelles du plan et
  ne réalise pas de reconnaissance géométrique des murs/polygones : les
  résultats doivent être vérifiés avant utilisation (voir
  `backend/app/core/plan_detection.py`).

## Méthodologie de calcul NF EN 378-1 (résumé)

1. **Volume du local** : `V = Surface × Hauteur` (surface plafonnée à 250 m²
   pour la vérification, conformément à C.3.2.1).
2. **Deux méthodes normatives implémentées** (Annexe C) :
   - **Méthode A — C.2** (climatisation / PAC de confort, fluide inflammable
     2L/2/3) : `mmax = 2,5 × LFL^(5/4) × h0 × √A` (Formule C.1) et
     `Amin = m² / (2,5 × LFL^(5/4) × h0)²` (Formule C.2), où `h0` dépend de
     l'emplacement d'installation (plancher/mur/fenêtre/plafond).
   - **Méthode B — C.3** (autre solution, fluides A1/A2L, ≤ 150 kg) :
     comparaison de la concentration à la RCL, la QLMV et la QLAV (Tableau
     C.3 pour R-22/R-134a/R-407C/R-410A/R-744/R-32/R-1234yf/R-1234ze, sinon
     calculées selon C.3.2.1 et le Tableau C.4). Le local situé à l'étage le
     plus bas en sous-sol utilise la RCL comme seuil (C.3.2.3).
3. **Verdict** : Conforme / Conforme sous conditions (1 ou 2 mesures
   compensatoires) / Non conforme, avec calcul inverse du volume et de la
   surface minimale requis.
4. **Analyse multilocaux** : détermination du local le plus pénalisant et de
   la conformité globale du projet.
5. **Arbre de décision** : recommandation de mesures compensatoires
   (détection de fuite, électrovanne de sécurité, ventilation mécanique,
   cloisonnement, réduction de charge) selon le résultat.

Voir `backend/app/core/en378.py` pour l'implémentation complète (commentée
avec les références d'articles de la norme) et `backend/tests/test_en378.py`
pour la validation numérique contre les exemples chiffrés de l'Annexe H.
