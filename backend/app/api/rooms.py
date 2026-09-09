from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from .. import models, schemas
from ..database import get_db

router = APIRouter(prefix="/api/rooms", tags=["rooms"])


@router.get("", response_model=list[schemas.RoomOut])
def list_rooms(project_id: int | None = None, db: Session = Depends(get_db)):
    q = db.query(models.Room)
    if project_id:
        q = q.filter(models.Room.project_id == project_id)
    return q.all()


@router.post("", response_model=schemas.RoomOut)
def create_room(payload: schemas.RoomCreate, db: Session = Depends(get_db)):
    if not db.get(models.Project, payload.project_id):
        raise HTTPException(404, "Projet introuvable")
    room = models.Room(**payload.model_dump())
    db.add(room)
    db.commit()
    db.refresh(room)
    return room


@router.put("/{room_id}", response_model=schemas.RoomOut)
def update_room(room_id: int, payload: schemas.RoomBase, db: Session = Depends(get_db)):
    room = db.get(models.Room, room_id)
    if not room:
        raise HTTPException(404, "Local introuvable")
    for key, value in payload.model_dump().items():
        setattr(room, key, value)
    db.commit()
    db.refresh(room)
    return room


@router.delete("/{room_id}")
def delete_room(room_id: int, db: Session = Depends(get_db)):
    room = db.get(models.Room, room_id)
    if not room:
        raise HTTPException(404, "Local introuvable")
    db.delete(room)
    db.commit()
    return {"ok": True}
