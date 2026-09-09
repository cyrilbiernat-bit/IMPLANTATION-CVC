from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from .. import models, schemas
from ..database import get_db

router = APIRouter(prefix="/api/equipment", tags=["equipment"])


@router.get("/manufacturers", response_model=list[schemas.ManufacturerOut])
def list_manufacturers(db: Session = Depends(get_db)):
    return db.query(models.Manufacturer).order_by(models.Manufacturer.name).all()


@router.get("", response_model=list[schemas.EquipmentOut])
def list_equipment(
    manufacturer: str | None = None,
    system_type: str | None = None,
    fluid_code: str | None = None,
    min_power_kw: float | None = None,
    db: Session = Depends(get_db),
):
    q = db.query(models.Equipment)
    if manufacturer:
        q = q.join(models.Manufacturer).filter(models.Manufacturer.name == manufacturer)
    if system_type:
        q = q.filter(models.Equipment.system_type == system_type)
    if fluid_code:
        q = q.filter(models.Equipment.fluid_code == fluid_code)
    if min_power_kw:
        q = q.filter(models.Equipment.cooling_power_kw >= min_power_kw)
    return q.all()


@router.post("", response_model=schemas.EquipmentOut)
def create_equipment(payload: schemas.EquipmentCreate, db: Session = Depends(get_db)):
    manufacturer = (
        db.query(models.Manufacturer)
        .filter(models.Manufacturer.name == payload.manufacturer_name)
        .first()
    )
    if not manufacturer:
        manufacturer = models.Manufacturer(name=payload.manufacturer_name)
        db.add(manufacturer)
        db.flush()

    data = payload.model_dump(exclude={"manufacturer_name"})
    equipment = models.Equipment(manufacturer_id=manufacturer.id, **data)
    db.add(equipment)
    db.commit()
    db.refresh(equipment)
    return equipment


@router.delete("/{equipment_id}")
def delete_equipment(equipment_id: int, db: Session = Depends(get_db)):
    equipment = db.get(models.Equipment, equipment_id)
    if not equipment:
        raise HTTPException(404, "Équipement introuvable")
    db.delete(equipment)
    db.commit()
    return {"ok": True}


@router.get("/suggest", response_model=list[schemas.EquipmentOut])
def suggest_equipment(
    system_type: str,
    required_power_kw: float,
    fluid_code: str | None = None,
    indoor_units: int | None = None,
    db: Session = Depends(get_db),
):
    """Propose automatiquement les équipements compatibles avec le besoin."""
    q = db.query(models.Equipment).filter(models.Equipment.system_type == system_type)
    if fluid_code:
        q = q.filter(models.Equipment.fluid_code == fluid_code)
    candidates = [e for e in q.all() if e.cooling_power_kw >= required_power_kw]
    if indoor_units:
        candidates = [
            e for e in candidates if not e.max_indoor_units or e.max_indoor_units >= indoor_units
        ]
    candidates.sort(key=lambda e: e.cooling_power_kw)
    return candidates[:5]
