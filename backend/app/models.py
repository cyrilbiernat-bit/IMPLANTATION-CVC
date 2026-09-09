"""SQLAlchemy ORM models for the NF EN 378-1 refrigerant concentration
calculation platform.
"""
from __future__ import annotations

import datetime as dt
import enum

from sqlalchemy import (
    Boolean,
    DateTime,
    Enum,
    Float,
    ForeignKey,
    Integer,
    String,
    Text,
)
from sqlalchemy.orm import Mapped, mapped_column, relationship

from .database import Base


class BuildingType(str, enum.Enum):
    TERTIAIRE = "tertiaire"
    RESIDENTIEL = "residentiel"
    INDUSTRIEL = "industriel"
    COMMERCIAL = "commercial"
    ERP = "erp"
    HOTEL = "hotel"
    SANTE = "sante"
    LOGISTIQUE = "logistique"


class SystemType(str, enum.Enum):
    DRV = "DRV"
    MULTISPLIT = "Multi-split"
    SPLIT = "Split"
    GROUPE_EAU_GLACEE = "Groupe eau glacee"
    PAC = "PAC"
    CENTRALE_FRIGORIFIQUE = "Centrale frigorifique"
    MEUBLE_FRIGORIFIQUE = "Meuble frigorifique"


class AccessCategory(str, enum.Enum):
    """Catégorie d'accès au local — NF EN 378-1, Tableau 4."""

    GENERAL = "Accès général (a)"
    SURVEILLE = "Accès surveillé (b)"
    RESERVE = "Accès réservé (c)"


class MountingType(str, enum.Enum):
    """Emplacement d'installation de l'appareil — NF EN 378-1, C.2.1 (h0)."""

    FLOOR = "floor"
    WALL = "wall"
    WINDOW = "window"
    CEILING = "ceiling"


class Conformity(str, enum.Enum):
    CONFORME = "Conforme"
    CONFORME_SOUS_CONDITIONS = "Conforme sous conditions"
    NON_CONFORME = "Non conforme"


class Project(Base):
    __tablename__ = "projects"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    name: Mapped[str] = mapped_column(String(255))
    client: Mapped[str | None] = mapped_column(String(255), nullable=True)
    address: Mapped[str | None] = mapped_column(String(255), nullable=True)
    building_type: Mapped[str | None] = mapped_column(String(64), nullable=True)
    author: Mapped[str | None] = mapped_column(String(255), nullable=True)
    notes: Mapped[str | None] = mapped_column(Text, nullable=True)
    created_at: Mapped[dt.datetime] = mapped_column(DateTime, default=dt.datetime.utcnow)
    updated_at: Mapped[dt.datetime] = mapped_column(
        DateTime, default=dt.datetime.utcnow, onupdate=dt.datetime.utcnow
    )

    rooms: Mapped[list["Room"]] = relationship(
        back_populates="project", cascade="all, delete-orphan"
    )
    calculations: Mapped[list["CalculationResult"]] = relationship(
        back_populates="project", cascade="all, delete-orphan"
    )


class Room(Base):
    """Local pris en compte dans l'analyse multilocaux."""

    __tablename__ = "rooms"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    project_id: Mapped[int] = mapped_column(ForeignKey("projects.id"))
    name: Mapped[str] = mapped_column(String(255))
    room_type: Mapped[str] = mapped_column(String(64))  # bureau, chambre, ERP, local technique...
    surface_m2: Mapped[float] = mapped_column(Float)
    height_m: Mapped[float] = mapped_column(Float, default=2.5)
    access_category: Mapped[str] = mapped_column(
        String(64), default=AccessCategory.GENERAL.value
    )
    ventilation_ach: Mapped[float | None] = mapped_column(Float, nullable=True)
    system_type: Mapped[str | None] = mapped_column(String(64), nullable=True)
    fluid_code: Mapped[str | None] = mapped_column(String(32), nullable=True)
    charge_kg: Mapped[float | None] = mapped_column(Float, nullable=True)
    cooling_power_kw: Mapped[float | None] = mapped_column(Float, nullable=True)
    heating_power_kw: Mapped[float | None] = mapped_column(Float, nullable=True)
    indoor_units: Mapped[int | None] = mapped_column(Integer, nullable=True)
    mounting_type: Mapped[str | None] = mapped_column(String(16), nullable=True)
    is_lowest_basement_level: Mapped[bool] = mapped_column(Boolean, default=False)

    project: Mapped[Project] = relationship(back_populates="rooms")


class Fluid(Base):
    """Base de données des fluides frigorigènes (fiche EN 378-1 / ISO 817)."""

    __tablename__ = "fluids"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    code: Mapped[str] = mapped_column(String(32), unique=True, index=True)
    name: Mapped[str] = mapped_column(String(128))
    safety_group: Mapped[str] = mapped_column(String(8))  # A1, A2L, A2, A3, B1, B2L, B2
    lfl_kg_m3: Mapped[float | None] = mapped_column(Float, nullable=True)
    practical_limit_kg_m3: Mapped[float | None] = mapped_column(Float, nullable=True)
    atel_odl_kg_m3: Mapped[float | None] = mapped_column(Float, nullable=True)
    rcl_kg_m3: Mapped[float | None] = mapped_column(Float, nullable=True)
    qlmv_kg_m3: Mapped[float | None] = mapped_column(Float, nullable=True)
    qlav_kg_m3: Mapped[float | None] = mapped_column(Float, nullable=True)
    gwp: Mapped[float | None] = mapped_column(Float, nullable=True)
    molar_mass_g_mol: Mapped[float | None] = mapped_column(Float, nullable=True)
    source: Mapped[str | None] = mapped_column(String(255), nullable=True)
    editable: Mapped[bool] = mapped_column(Boolean, default=True)


class Manufacturer(Base):
    __tablename__ = "manufacturers"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    name: Mapped[str] = mapped_column(String(128), unique=True)

    equipments: Mapped[list["Equipment"]] = relationship(back_populates="manufacturer")


class Equipment(Base):
    """Fiche équipement de la bibliothèque constructeur."""

    __tablename__ = "equipments"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    manufacturer_id: Mapped[int] = mapped_column(ForeignKey("manufacturers.id"))
    reference: Mapped[str] = mapped_column(String(128))
    system_type: Mapped[str] = mapped_column(String(64))
    fluid_code: Mapped[str] = mapped_column(String(32))
    cooling_power_kw: Mapped[float] = mapped_column(Float)
    heating_power_kw: Mapped[float | None] = mapped_column(Float, nullable=True)
    factory_charge_kg: Mapped[float] = mapped_column(Float)
    additional_charge_kg_per_m: Mapped[float] = mapped_column(Float, default=0.0)
    max_pipe_length_m: Mapped[float | None] = mapped_column(Float, nullable=True)
    max_equivalent_length_m: Mapped[float | None] = mapped_column(Float, nullable=True)
    max_indoor_units: Mapped[int | None] = mapped_column(Integer, nullable=True)
    notes: Mapped[str | None] = mapped_column(String(255), nullable=True)

    manufacturer: Mapped[Manufacturer] = relationship(back_populates="equipments")


class Circuit(Base):
    """Circuit frigorifique pour le mode Expert (calcul détaillé)."""

    __tablename__ = "circuits"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    project_id: Mapped[int] = mapped_column(ForeignKey("projects.id"))
    room_id: Mapped[int | None] = mapped_column(ForeignKey("rooms.id"), nullable=True)
    name: Mapped[str] = mapped_column(String(255))
    outdoor_unit_reference: Mapped[str | None] = mapped_column(String(128), nullable=True)
    indoor_unit_references: Mapped[str | None] = mapped_column(Text, nullable=True)  # JSON list
    fluid_code: Mapped[str] = mapped_column(String(32))
    factory_charge_kg: Mapped[float] = mapped_column(Float)
    pipe_length_m: Mapped[float] = mapped_column(Float, default=0.0)
    pipe_diameter_mm: Mapped[float | None] = mapped_column(Float, nullable=True)
    additional_charge_kg_per_m: Mapped[float] = mapped_column(Float, default=0.0)
    altimetry_m: Mapped[float] = mapped_column(Float, default=0.0)
    equivalent_length_m: Mapped[float | None] = mapped_column(Float, nullable=True)
    zone_count: Mapped[int] = mapped_column(Integer, default=1)
    indoor_unit_count: Mapped[int] = mapped_column(Integer, default=1)

    total_charge_kg: Mapped[float | None] = mapped_column(Float, nullable=True)


class CalculationResult(Base):
    """Résultat de calcul NF EN 378-1 stocké pour historique / rapport."""

    __tablename__ = "calculation_results"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    project_id: Mapped[int] = mapped_column(ForeignKey("projects.id"))
    room_id: Mapped[int | None] = mapped_column(ForeignKey("rooms.id"), nullable=True)
    mode: Mapped[str] = mapped_column(String(16))  # "rapide" | "expert"
    fluid_code: Mapped[str] = mapped_column(String(32))
    charge_kg: Mapped[float] = mapped_column(Float)
    volume_m3: Mapped[float] = mapped_column(Float)
    concentration_kg_m3: Mapped[float] = mapped_column(Float)
    limit_used_kg_m3: Mapped[float] = mapped_column(Float)
    limit_type: Mapped[str] = mapped_column(String(16))  # RCL, LFL, ATEL, ODL
    conformity: Mapped[str] = mapped_column(String(32))
    min_volume_required_m3: Mapped[float | None] = mapped_column(Float, nullable=True)
    min_surface_required_m2: Mapped[float | None] = mapped_column(Float, nullable=True)
    recommendations: Mapped[str | None] = mapped_column(Text, nullable=True)  # JSON list
    created_at: Mapped[dt.datetime] = mapped_column(DateTime, default=dt.datetime.utcnow)

    project: Mapped[Project] = relationship(back_populates="calculations")


class CompanySettings(Base):
    """Paramètres société pour l'en-tête des rapports (logo, coordonnées)."""

    __tablename__ = "company_settings"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    company_name: Mapped[str | None] = mapped_column(String(255), nullable=True)
    address: Mapped[str | None] = mapped_column(String(255), nullable=True)
    logo_path: Mapped[str | None] = mapped_column(String(255), nullable=True)
    contact_email: Mapped[str | None] = mapped_column(String(255), nullable=True)
    contact_phone: Mapped[str | None] = mapped_column(String(64), nullable=True)
