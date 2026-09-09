from __future__ import annotations

from fastapi import APIRouter

from .. import schemas
from ..core import cctp_analysis

router = APIRouter(prefix="/api/ai", tags=["ai"])


@router.post("/analyze-cctp")
def analyze_cctp(payload: schemas.CctpAnalysisRequest):
    return cctp_analysis.analyze_cctp(payload.text)


@router.post("/draft-notice")
def draft_notice(project_name: str, results: list[dict]):
    return {"notice": cctp_analysis.draft_en378_notice(project_name, results)}
