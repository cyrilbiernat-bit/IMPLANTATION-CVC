from __future__ import annotations

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from . import models
from .api import ai, calculations, equipment, fluids, projects, reports, rooms, settings
from .database import Base, engine
from .seed import seed_defaults

app = FastAPI(
    title="CVC EN 378-1 — Calcul de concentration de fluide frigorigène",
    description=(
        "API de calcul de concentration de fluide frigorigène conforme à la "
        "méthodologie NF EN 378-1, prédimensionnement CVC et gestion de "
        "bibliothèques fluides/équipements."
    ),
    version="1.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.on_event("startup")
def on_startup() -> None:
    Base.metadata.create_all(bind=engine)
    seed_defaults()


app.include_router(projects.router)
app.include_router(rooms.router)
app.include_router(fluids.router)
app.include_router(equipment.router)
app.include_router(calculations.router)
app.include_router(reports.router)
app.include_router(settings.router)
app.include_router(ai.router)


@app.get("/api/health")
def health():
    return {"status": "ok"}
