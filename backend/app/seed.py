"""Peuple la base avec les données par défaut (fluides + équipements) si vides."""
from __future__ import annotations

from . import models
from .core.equipment_db import DEFAULT_EQUIPMENT, MANUFACTURERS
from .core.fluids_db import DEFAULT_FLUIDS
from .database import SessionLocal


def seed_defaults() -> None:
    db = SessionLocal()
    try:
        if db.query(models.Fluid).count() == 0:
            for f in DEFAULT_FLUIDS:
                db.add(models.Fluid(**f, editable=True))
            db.commit()

        if db.query(models.Manufacturer).count() == 0:
            manufacturer_map: dict[str, models.Manufacturer] = {}
            for name in MANUFACTURERS:
                m = models.Manufacturer(name=name)
                db.add(m)
                manufacturer_map[name] = m
            db.flush()

            for manufacturer_name, items in DEFAULT_EQUIPMENT.items():
                manufacturer = manufacturer_map[manufacturer_name]
                for item in items:
                    db.add(models.Equipment(manufacturer_id=manufacturer.id, **item))
            db.commit()

        if db.query(models.CompanySettings).count() == 0:
            db.add(models.CompanySettings())
            db.commit()
    finally:
        db.close()


if __name__ == "__main__":
    from .database import Base, engine

    Base.metadata.create_all(bind=engine)
    seed_defaults()
    print("Base de données initialisée et peuplée avec les données par défaut.")
