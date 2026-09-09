"""Pydantic schemas (request/response models)."""
from __future__ import annotations

import datetime as dt

from pydantic import BaseModel, ConfigDict

DEFAULT_ACCESS_CATEGORY = "Accès général (a)"


class ProjectBase(BaseModel):
    name: str
    client: str | None = None
    address: str | None = None
    building_type: str | None = None
    author: str | None = None
    notes: str | None = None


class ProjectCreate(ProjectBase):
    pass


class ProjectOut(ProjectBase):
    model_config = ConfigDict(from_attributes=True)
    id: int
    created_at: dt.datetime
    updated_at: dt.datetime


class RoomBase(BaseModel):
    name: str
    room_type: str
    surface_m2: float
    height_m: float = 2.5
    access_category: str = DEFAULT_ACCESS_CATEGORY
    ventilation_ach: float | None = None
    system_type: str | None = None
    fluid_code: str | None = None
    charge_kg: float | None = None
    cooling_power_kw: float | None = None
    heating_power_kw: float | None = None
    indoor_units: int | None = None
    mounting_type: str | None = None
    is_lowest_basement_level: bool = False


class RoomCreate(RoomBase):
    project_id: int


class RoomOut(RoomBase):
    model_config = ConfigDict(from_attributes=True)
    id: int
    project_id: int


class FluidBase(BaseModel):
    code: str
    name: str
    safety_group: str
    lfl_kg_m3: float | None = None
    practical_limit_kg_m3: float | None = None
    atel_odl_kg_m3: float | None = None
    rcl_kg_m3: float | None = None
    qlmv_kg_m3: float | None = None
    qlav_kg_m3: float | None = None
    gwp: float | None = None
    molar_mass_g_mol: float | None = None
    source: str | None = None


class FluidCreate(FluidBase):
    pass


class FluidOut(FluidBase):
    model_config = ConfigDict(from_attributes=True)
    id: int
    editable: bool


class EquipmentBase(BaseModel):
    reference: str
    system_type: str
    fluid_code: str
    cooling_power_kw: float
    heating_power_kw: float | None = None
    factory_charge_kg: float
    additional_charge_kg_per_m: float = 0.0
    max_pipe_length_m: float | None = None
    max_equivalent_length_m: float | None = None
    max_indoor_units: int | None = None
    notes: str | None = None


class EquipmentCreate(EquipmentBase):
    manufacturer_name: str


class EquipmentOut(EquipmentBase):
    model_config = ConfigDict(from_attributes=True)
    id: int
    manufacturer_id: int


class ManufacturerOut(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: int
    name: str
    equipments: list[EquipmentOut] = []


class QuickCalcRequest(BaseModel):
    project_id: int | None = None
    project_name: str | None = None
    building_type: str
    room_name: str
    room_type: str
    surface_m2: float
    height_m: float
    system_type: str | None = None
    fluid_code: str
    cooling_power_kw: float | None = None
    heating_power_kw: float | None = None
    indoor_units: int = 1
    access_category: str = DEFAULT_ACCESS_CATEGORY
    climate_zone: str = "H2"
    mounting_type: str | None = None
    is_lowest_basement_level: bool = False


class ExpertCalcCircuit(BaseModel):
    name: str
    fluid_code: str
    factory_charge_kg: float
    pipe_length_m: float = 0.0
    additional_charge_kg_per_m: float = 0.0
    altimetry_m: float = 0.0
    equivalent_length_m: float | None = None
    zone_count: int = 1
    indoor_unit_count: int = 1


class ExpertCalcRequest(BaseModel):
    project_id: int | None = None
    room_name: str
    room_type: str
    surface_m2: float
    height_m: float
    access_category: str = DEFAULT_ACCESS_CATEGORY
    system_type: str | None = None
    mounting_type: str | None = None
    is_lowest_basement_level: bool = False
    circuits: list[ExpertCalcCircuit]


class MultiRoomRoomInput(BaseModel):
    room_name: str
    room_type: str
    surface_m2: float
    height_m: float
    fluid_code: str
    charge_kg: float
    access_category: str = DEFAULT_ACCESS_CATEGORY
    system_type: str | None = None
    mounting_type: str | None = None
    is_lowest_basement_level: bool = False


class MultiRoomRequest(BaseModel):
    project_id: int | None = None
    rooms: list[MultiRoomRoomInput]


class InverseCalcRequest(BaseModel):
    fluid_code: str
    charge_kg: float
    height_m: float = 2.5
    access_category: str = DEFAULT_ACCESS_CATEGORY
    is_lowest_basement_level: bool = False


class CalculationResultOut(BaseModel):
    model_config = ConfigDict(from_attributes=True)
    id: int
    project_id: int
    room_id: int | None
    mode: str
    fluid_code: str
    charge_kg: float
    volume_m3: float
    concentration_kg_m3: float
    limit_used_kg_m3: float
    limit_type: str
    conformity: str
    min_volume_required_m3: float | None
    min_surface_required_m2: float | None
    recommendations: str | None
    created_at: dt.datetime


class CompanySettingsIn(BaseModel):
    company_name: str | None = None
    address: str | None = None
    contact_email: str | None = None
    contact_phone: str | None = None


class CompanySettingsOut(CompanySettingsIn):
    model_config = ConfigDict(from_attributes=True)
    id: int
    logo_path: str | None = None


class CctpAnalysisRequest(BaseModel):
    text: str


class CctpRoomExtracted(BaseModel):
    room_name: str
    room_type: str
    surface_m2: float | None = None
    suggested_system_type: str | None = None


class ImportedRoomRow(BaseModel):
    room_name: str
    room_type: str = "bureau"
    surface_m2: float | None = None
    height_m: float | None = None
    fluid_code: str | None = None
    charge_kg: float | None = None


class RoomImportResponse(BaseModel):
    source: str
    rooms: list[ImportedRoomRow]
    warnings: list[str] = []
