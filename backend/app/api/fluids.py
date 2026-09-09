from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from .. import models, schemas
from ..database import get_db

router = APIRouter(prefix="/api/fluids", tags=["fluids"])


@router.get("", response_model=list[schemas.FluidOut])
def list_fluids(db: Session = Depends(get_db)):
    return db.query(models.Fluid).order_by(models.Fluid.code).all()


@router.post("", response_model=schemas.FluidOut)
def create_fluid(payload: schemas.FluidCreate, db: Session = Depends(get_db)):
    if db.query(models.Fluid).filter(models.Fluid.code == payload.code).first():
        raise HTTPException(400, "Ce code fluide existe déjà")
    fluid = models.Fluid(**payload.model_dump(), editable=True)
    db.add(fluid)
    db.commit()
    db.refresh(fluid)
    return fluid


@router.put("/{fluid_id}", response_model=schemas.FluidOut)
def update_fluid(fluid_id: int, payload: schemas.FluidCreate, db: Session = Depends(get_db)):
    fluid = db.get(models.Fluid, fluid_id)
    if not fluid:
        raise HTTPException(404, "Fluide introuvable")
    for key, value in payload.model_dump().items():
        setattr(fluid, key, value)
    db.commit()
    db.refresh(fluid)
    return fluid


@router.delete("/{fluid_id}")
def delete_fluid(fluid_id: int, db: Session = Depends(get_db)):
    fluid = db.get(models.Fluid, fluid_id)
    if not fluid:
        raise HTTPException(404, "Fluide introuvable")
    db.delete(fluid)
    db.commit()
    return {"ok": True}
