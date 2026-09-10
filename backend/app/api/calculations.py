from __future__ import annotations

import dataclasses
import json

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from .. import models, schemas
from ..core import decision_engine, en378, sizing
from ..database import get_db

router = APIRouter(prefix="/api/calculations", tags=["calculations"])


def _fluid_to_dataclass(fluid: models.Fluid) -> en378.FluidData:
    return en378.FluidData(
        code=fluid.code,
        name=fluid.name,
        safety_group=fluid.safety_group,
        lfl_kg_m3=fluid.lfl_kg_m3,
        practical_limit_kg_m3=fluid.practical_limit_kg_m3,
        atel_odl_kg_m3=fluid.atel_odl_kg_m3,
        molar_mass_g_mol=fluid.molar_mass_g_mol,
        gwp=fluid.gwp,
        rcl_kg_m3=fluid.rcl_kg_m3,
        qlmv_kg_m3=fluid.qlmv_kg_m3,
        qlav_kg_m3=fluid.qlav_kg_m3,
    )


def _get_fluid_or_404(db: Session, code: str) -> models.Fluid:
    fluid = db.query(models.Fluid).filter(models.Fluid.code == code).first()
    if not fluid:
        raise HTTPException(404, f"Fluide {code} introuvable dans la bibliothèque")
    return fluid


def _serialize_analysis(result: en378.ConcentrationAnalysis) -> dict:
    return dataclasses.asdict(result)


def _persist_result(
    db: Session,
    project_id: int,
    room_id: int | None,
    mode: str,
    fluid_code: str,
    charge_kg: float,
    result: en378.ConcentrationAnalysis,
    recommendations: list[dict],
) -> models.CalculationResult:
    row = models.CalculationResult(
        project_id=project_id,
        room_id=room_id,
        mode=mode,
        fluid_code=fluid_code,
        charge_kg=charge_kg,
        volume_m3=result.volume_m3,
        concentration_kg_m3=result.concentration_kg_m3,
        limit_used_kg_m3=result.limit_used_kg_m3 or 0.0,
        limit_type=result.limit_type,
        conformity=result.conformity,
        min_volume_required_m3=result.min_volume_required_m3,
        min_surface_required_m2=result.min_surface_required_m2,
        recommendations=json.dumps(recommendations, ensure_ascii=False),
    )
    db.add(row)
    db.commit()
    db.refresh(row)
    return row


@router.post("/quick")
def quick_calc(payload: schemas.QuickCalcRequest, db: Session = Depends(get_db)):
    loads = sizing.estimate_loads(payload.surface_m2, payload.building_type, payload.climate_zone)
    cooling_power = payload.cooling_power_kw or loads["cooling_power_kw"]
    heating_power = payload.heating_power_kw or loads["heating_power_kw"]

    selected_equipment = (
        db.get(models.Equipment, payload.equipment_id) if payload.equipment_id else None
    )

    fluid_code = selected_equipment.fluid_code if selected_equipment else payload.fluid_code
    fluid = _get_fluid_or_404(db, fluid_code)
    fluid_data = _fluid_to_dataclass(fluid)

    suggested_system = (
        selected_equipment.system_type if selected_equipment
        else payload.system_type or sizing.suggest_system_type(cooling_power, payload.indoor_units)
    )

    if selected_equipment:
        pipe_length = max(payload.indoor_units, 1) * 5
        additional_charge = round(selected_equipment.additional_charge_kg_per_m * pipe_length, 2)
        total_charge = round(selected_equipment.factory_charge_kg + additional_charge, 2)
        charge_estimate = {
            "factory_charge_kg": selected_equipment.factory_charge_kg,
            "additional_charge_kg": additional_charge,
            "total_charge_kg": total_charge,
            "charge_per_indoor_unit_kg": round(total_charge / max(payload.indoor_units, 1), 3),
            "estimated_pipe_length_m": pipe_length,
            "fluid_code": fluid_code,
            "equipment_reference": selected_equipment.reference,
        }
        cooling_power = selected_equipment.cooling_power_kw
    else:
        charge_estimate = sizing.estimate_probable_charge(
            suggested_system, cooling_power, fluid_code, payload.indoor_units
        )

    result = en378.compute_concentration(
        fluid_data,
        charge_estimate["total_charge_kg"],
        payload.surface_m2,
        payload.height_m,
        system_type=suggested_system,
        mounting_type=payload.mounting_type,
        is_lowest_basement_level=payload.is_lowest_basement_level,
    )
    recommendations = decision_engine.recommend(
        fluid.safety_group, result.conformity, result.margin_ratio, payload.room_type
    )

    equipment_matches = (
        db.query(models.Equipment)
        .filter(
            models.Equipment.system_type == suggested_system,
            models.Equipment.fluid_code == fluid_code,
            models.Equipment.cooling_power_kw >= cooling_power,
        )
        .order_by(models.Equipment.cooling_power_kw)
        .limit(5)
        .all()
    )

    project_id = payload.project_id
    if not project_id and payload.project_name:
        project = models.Project(name=payload.project_name, building_type=payload.building_type)
        db.add(project)
        db.commit()
        db.refresh(project)
        project_id = project.id

    saved = None
    if project_id:
        saved = _persist_result(
            db,
            project_id,
            None,
            "rapide",
            fluid_code,
            charge_estimate["total_charge_kg"],
            result,
            recommendations,
        )

    return {
        "loads": loads,
        "suggested_system_type": suggested_system,
        "charge_estimate": charge_estimate,
        "concentration": _serialize_analysis(result),
        "recommendations": recommendations,
        "equipment_suggestions": [
            schemas.EquipmentOut.model_validate(e).model_dump() for e in equipment_matches
        ],
        "calculation_id": saved.id if saved else None,
        "project_id": project_id,
    }


@router.post("/expert")
def expert_calc(payload: schemas.ExpertCalcRequest, db: Session = Depends(get_db)):
    if not payload.circuits:
        raise HTTPException(400, "Au moins un circuit est requis")

    circuit_totals = []
    total_charge = 0.0
    dominant_fluid_code = payload.circuits[0].fluid_code

    for c in payload.circuits:
        additional = c.additional_charge_kg_per_m * (c.equivalent_length_m or c.pipe_length_m)
        circuit_total = round(c.factory_charge_kg + additional, 3)
        total_charge += circuit_total
        circuit_totals.append(
            {
                "name": c.name,
                "factory_charge_kg": c.factory_charge_kg,
                "additional_charge_kg": round(additional, 3),
                "total_charge_kg": circuit_total,
                "charge_per_zone_kg": round(circuit_total / max(c.zone_count, 1), 3),
                "charge_per_indoor_unit_kg": round(circuit_total / max(c.indoor_unit_count, 1), 3),
            }
        )
        if circuit_total == max(x["total_charge_kg"] for x in circuit_totals):
            dominant_fluid_code = c.fluid_code

    fluid = _get_fluid_or_404(db, dominant_fluid_code)
    fluid_data = _fluid_to_dataclass(fluid)

    result = en378.compute_concentration(
        fluid_data,
        round(total_charge, 3),
        payload.surface_m2,
        payload.height_m,
        system_type=payload.system_type,
        mounting_type=payload.mounting_type,
        is_lowest_basement_level=payload.is_lowest_basement_level,
    )
    recommendations = decision_engine.recommend(
        fluid.safety_group, result.conformity, result.margin_ratio, payload.room_type
    )

    saved = None
    if payload.project_id:
        saved = _persist_result(
            db,
            payload.project_id,
            None,
            "expert",
            dominant_fluid_code,
            round(total_charge, 3),
            result,
            recommendations,
        )

    return {
        "circuits": circuit_totals,
        "total_charge_kg": round(total_charge, 3),
        "dominant_fluid_code": dominant_fluid_code,
        "concentration": _serialize_analysis(result),
        "recommendations": recommendations,
        "calculation_id": saved.id if saved else None,
    }


@router.post("/multi-room")
def multi_room(payload: schemas.MultiRoomRequest, db: Session = Depends(get_db)):
    if not payload.rooms:
        raise HTTPException(400, "Au moins un local est requis")

    analyses = []
    room_details = []
    for r in payload.rooms:
        fluid = _get_fluid_or_404(db, r.fluid_code)
        fluid_data = _fluid_to_dataclass(fluid)
        result = en378.compute_concentration(
            fluid_data,
            r.charge_kg,
            r.surface_m2,
            r.height_m,
            system_type=r.system_type,
            mounting_type=r.mounting_type,
            is_lowest_basement_level=r.is_lowest_basement_level,
        )
        recommendations = decision_engine.recommend(
            fluid.safety_group, result.conformity, result.margin_ratio, r.room_type
        )
        analyses.append(en378.RoomAnalysis(room_name=r.room_name, result=result))
        room_details.append(
            {
                "room_name": r.room_name,
                "room_type": r.room_type,
                "result": _serialize_analysis(result),
                "recommendations": recommendations,
            }
        )

    summary = en378.multi_room_analysis(analyses)
    return {"summary": summary, "rooms": room_details}


@router.post("/inverse")
def inverse_calc(payload: schemas.InverseCalcRequest, db: Session = Depends(get_db)):
    fluid = _get_fluid_or_404(db, payload.fluid_code)
    fluid_data = _fluid_to_dataclass(fluid)
    result = en378.compute_general_method(
        fluid_data,
        payload.charge_kg,
        surface_m2=1.0,
        height_m=payload.height_m,
        is_lowest_basement_level=payload.is_lowest_basement_level,
    )
    return {
        "min_volume_m3": result.min_volume_required_m3,
        "min_surface_required_m2": result.min_surface_required_m2,
        "rcl_kg_m3": result.rcl_kg_m3,
        "qlmv_kg_m3": result.qlmv_kg_m3,
        "qlav_kg_m3": result.qlav_kg_m3,
    }


@router.get("/history", response_model=list[schemas.CalculationResultOut])
def calculation_history(project_id: int | None = None, db: Session = Depends(get_db)):
    q = db.query(models.CalculationResult).order_by(models.CalculationResult.created_at.desc())
    if project_id:
        q = q.filter(models.CalculationResult.project_id == project_id)
    return q.limit(100).all()
