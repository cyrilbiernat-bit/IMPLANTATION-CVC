from __future__ import annotations

import os
import uuid

from fastapi import APIRouter, Depends, HTTPException, UploadFile
from fastapi.responses import FileResponse
from sqlalchemy.orm import Session

from .. import models, schemas
from ..database import get_db

router = APIRouter(prefix="/api/settings", tags=["settings"])

MEDIA_DIR = os.path.join(os.path.dirname(os.path.dirname(__file__)), "media")
os.makedirs(MEDIA_DIR, exist_ok=True)


def _get_or_create(db: Session) -> models.CompanySettings:
    settings = db.query(models.CompanySettings).first()
    if not settings:
        settings = models.CompanySettings()
        db.add(settings)
        db.commit()
        db.refresh(settings)
    return settings


@router.get("", response_model=schemas.CompanySettingsOut)
def get_settings(db: Session = Depends(get_db)):
    return _get_or_create(db)


@router.put("", response_model=schemas.CompanySettingsOut)
def update_settings(payload: schemas.CompanySettingsIn, db: Session = Depends(get_db)):
    settings = _get_or_create(db)
    for key, value in payload.model_dump().items():
        setattr(settings, key, value)
    db.commit()
    db.refresh(settings)
    return settings


@router.post("/logo", response_model=schemas.CompanySettingsOut)
def upload_logo(file: UploadFile, db: Session = Depends(get_db)):
    settings = _get_or_create(db)
    ext = os.path.splitext(file.filename or "logo.png")[1] or ".png"
    filename = f"logo_{uuid.uuid4().hex}{ext}"
    path = os.path.join(MEDIA_DIR, filename)
    with open(path, "wb") as f:
        f.write(file.file.read())
    settings.logo_path = path
    db.commit()
    db.refresh(settings)
    return settings


@router.get("/logo-file")
def get_logo_file(db: Session = Depends(get_db)):
    settings = _get_or_create(db)
    if not settings.logo_path or not os.path.exists(settings.logo_path):
        raise HTTPException(404, "Aucun logo enregistré")
    return FileResponse(settings.logo_path)
