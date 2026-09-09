from __future__ import annotations

from fastapi import APIRouter, HTTPException, UploadFile

from .. import schemas
from ..core import cctp_analysis, plan_detection

router = APIRouter(prefix="/api/ai", tags=["ai"])


@router.post("/analyze-cctp")
def analyze_cctp(payload: schemas.CctpAnalysisRequest):
    return cctp_analysis.analyze_cctp(payload.text)


@router.post("/analyze-plan")
async def analyze_plan(file: UploadFile):
    content = await file.read()
    try:
        return plan_detection.analyze_plan(content, file.filename or "plan")
    except ValueError as exc:
        raise HTTPException(400, str(exc))


@router.post("/draft-notice")
def draft_notice(project_name: str, results: list[dict]):
    return {"notice": cctp_analysis.draft_en378_notice(project_name, results)}
