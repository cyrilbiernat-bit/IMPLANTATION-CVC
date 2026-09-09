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

## ⚠️ Avertissement réglementaire

Les valeurs de seuils (LFL, RCL, ATEL, ODL) et le catalogue équipements
fournis par défaut sont des **données indicatives** (ASHRAE 34 / ISO 817 pour
les fluides, jeux d'exemple pour les équipements constructeurs). Elles
doivent être vérifiées par un professionnel qualifié par rapport à l'édition
en vigueur de la norme NF EN 378-1 et aux fiches de données de sécurité (FDS)
des fabricants avant toute utilisation à des fins de conformité
réglementaire contractuelle. Les bibliothèques fluides et équipements sont
entièrement éditables dans l'application.

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

## Assistant IA (analyse de CCTP)

La page « Assistant IA » extrait automatiquement les locaux mentionnés dans
un CCTP (nom, type, surface) et propose un type de système CVC. Sans clé API
configurée, un moteur heuristique local (mots-clés + expressions régulières)
est utilisé. En définissant la variable d'environnement `ANTHROPIC_API_KEY`
côté backend, l'extraction et la proposition sont réalisées par un modèle
Claude pour une analyse plus fine du texte.

## Méthodologie de calcul NF EN 378-1 (résumé)

1. **Volume du local** : `V = Surface × Hauteur`
2. **Concentration théorique** : `Cm = Masse relâchée / V`
3. **Limite applicable** :
   - Fluides inflammables (A2L/A2/A3) : limite pratique = `0.25 × LFL`
     (locaux à occupation générale), comparée également au RCL si disponible.
   - Fluides non inflammables (A1/B1) : `min(ATEL, ODL, RCL)`.
   - La catégorie d'accès du local (général / supervisé / autorisé) module
     la limite retenue.
4. **Verdict** : Conforme / Conforme sous conditions / Non conforme, avec
   calcul inverse du volume et de la surface minimale requis.
5. **Analyse multilocaux** : détermination du local le plus pénalisant et de
   la conformité globale du projet.
6. **Arbre de décision** : recommandation de mesures compensatoires
   (détection de fuite, électrovanne de sécurité, ventilation mécanique,
   cloisonnement, réduction de charge) selon le résultat.

Voir `backend/app/core/en378.py` pour l'implémentation complète et ses
commentaires méthodologiques.
