from __future__ import annotations

import json

from fastapi import APIRouter, Depends, HTTPException, Response
from sqlalchemy.orm import Session

from .. import models
from ..database import get_db
from ..reports.docx_report import build_report_docx
from ..reports.pdf_report import build_report_pdf
from ..reports.xlsx_report import build_report_xlsx

router = APIRouter(prefix="/api/reports", tags=["reports"])


def _gather_project_results(db: Session, project_id: int) -> tuple[dict, list[dict]]:
    project = db.get(models.Project, project_id)
    if not project:
        raise HTTPException(404, "Projet introuvable")

    calc_rows = (
        db.query(models.CalculationResult)
        .filter(models.CalculationResult.project_id == project_id)
        .order_by(models.CalculationResult.created_at.desc())
        .all()
    )
    results = []
    for row in calc_rows:
        room_name = "Local"
        if row.room_id:
            room = db.get(models.Room, row.room_id)
            if room:
                room_name = room.name
        results.append(
            {
                "room_name": room_name,
                "room_type": "-",
                "fluid_code": row.fluid_code,
                "charge_kg": row.charge_kg,
                "volume_m3": row.volume_m3,
                "concentration_kg_m3": row.concentration_kg_m3,
                "limit_used_kg_m3": row.limit_used_kg_m3,
                "limit_type": row.limit_type,
                "conformity": row.conformity,
                "recommendations": json.loads(row.recommendations) if row.recommendations else [],
            }
        )

    project_dict = {
        "name": project.name,
        "client": project.client,
        "address": project.address,
        "building_type": project.building_type,
        "author": project.author,
        "notes": project.notes,
    }
    return project_dict, results


@router.get("/{project_id}/pdf")
def report_pdf(project_id: int, db: Session = Depends(get_db)):
    project_dict, results = _gather_project_results(db, project_id)
    if not results:
        raise HTTPException(400, "Aucun résultat de calcul enregistré pour ce projet")

    company_row = db.query(models.CompanySettings).first()
    company = None
    logo_path = None
    if company_row:
        company = {
            "company_name": company_row.company_name,
            "address": company_row.address,
            "contact_email": company_row.contact_email,
        }
        logo_path = company_row.logo_path

    pdf_bytes = build_report_pdf(project_dict, results, company, logo_path)
    return Response(
        content=pdf_bytes,
        media_type="application/pdf",
        headers={"Content-Disposition": f'attachment; filename="rapport_{project_id}.pdf"'},
    )


@router.get("/{project_id}/docx")
def report_docx(project_id: int, db: Session = Depends(get_db)):
    project_dict, results = _gather_project_results(db, project_id)
    if not results:
        raise HTTPException(400, "Aucun résultat de calcul enregistré pour ce projet")

    company_row = db.query(models.CompanySettings).first()
    company = {"company_name": company_row.company_name} if company_row else None

    docx_bytes = build_report_docx(project_dict, results, company)
    return Response(
        content=docx_bytes,
        media_type="application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        headers={"Content-Disposition": f'attachment; filename="rapport_{project_id}.docx"'},
    )


@router.get("/{project_id}/xlsx")
def report_xlsx(project_id: int, db: Session = Depends(get_db)):
    project_dict, results = _gather_project_results(db, project_id)
    if not results:
        raise HTTPException(400, "Aucun résultat de calcul enregistré pour ce projet")

    xlsx_bytes = build_report_xlsx(project_dict, results)
    return Response(
        content=xlsx_bytes,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={"Content-Disposition": f'attachment; filename="rapport_{project_id}.xlsx"'},
    )
