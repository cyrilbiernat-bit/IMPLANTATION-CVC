export interface Project {
  id: number;
  name: string;
  client?: string | null;
  address?: string | null;
  building_type?: string | null;
  author?: string | null;
  notes?: string | null;
  created_at: string;
  updated_at: string;
}

export interface Fluid {
  id: number;
  code: string;
  name: string;
  safety_group: string;
  lfl_kg_m3?: number | null;
  rcl_kg_m3?: number | null;
  atel_kg_m3?: number | null;
  odl_kg_m3?: number | null;
  gwp?: number | null;
  molar_mass_g_mol?: number | null;
  source?: string | null;
  editable: boolean;
}

export interface Equipment {
  id: number;
  manufacturer_id: number;
  reference: string;
  system_type: string;
  fluid_code: string;
  cooling_power_kw: number;
  heating_power_kw?: number | null;
  factory_charge_kg: number;
  additional_charge_kg_per_m: number;
  max_pipe_length_m?: number | null;
  max_equivalent_length_m?: number | null;
  max_indoor_units?: number | null;
  notes?: string | null;
}

export interface Manufacturer {
  id: number;
  name: string;
  equipments: Equipment[];
}

export interface ConcentrationResult {
  volume_m3: number;
  concentration_kg_m3: number;
  limit_used_kg_m3: number;
  limit_type: string;
  conformity: "Conforme" | "Conforme sous conditions" | "Non conforme";
  margin_ratio: number;
  min_volume_required_m3: number;
  min_surface_required_m2?: number | null;
  details: Record<string, unknown>;
}

export interface Recommendation {
  measure: string;
  reason: string;
  priority: string;
}

export interface QuickCalcResponse {
  loads: { cooling_power_kw: number; heating_power_kw: number };
  suggested_system_type: string;
  charge_estimate: {
    factory_charge_kg: number;
    additional_charge_kg: number;
    total_charge_kg: number;
    charge_per_indoor_unit_kg: number;
    estimated_pipe_length_m: number;
    fluid_code: string;
  };
  concentration: ConcentrationResult;
  recommendations: Recommendation[];
  equipment_suggestions: Equipment[];
  calculation_id: number | null;
  project_id: number | null;
}

export interface ExpertCircuitInput {
  name: string;
  fluid_code: string;
  factory_charge_kg: number;
  pipe_length_m: number;
  additional_charge_kg_per_m: number;
  altimetry_m: number;
  equivalent_length_m?: number | null;
  zone_count: number;
  indoor_unit_count: number;
}

export interface ExpertCalcResponse {
  circuits: Array<{
    name: string;
    factory_charge_kg: number;
    additional_charge_kg: number;
    total_charge_kg: number;
    charge_per_zone_kg: number;
    charge_per_indoor_unit_kg: number;
  }>;
  total_charge_kg: number;
  dominant_fluid_code: string;
  concentration: ConcentrationResult;
  recommendations: Recommendation[];
  calculation_id: number | null;
}

export interface MultiRoomRoomInput {
  room_name: string;
  room_type: string;
  surface_m2: number;
  height_m: number;
  fluid_code: string;
  charge_kg: number;
  access_category: string;
}

export interface MultiRoomResponse {
  summary: {
    worst_room: string;
    worst_margin_ratio: number;
    global_conformity: string;
    rooms: Array<{
      room_name: string;
      conformity: string;
      concentration_kg_m3: number;
      limit_used_kg_m3: number;
      margin_ratio: number;
    }>;
  };
  rooms: Array<{
    room_name: string;
    room_type: string;
    result: ConcentrationResult;
    recommendations: Recommendation[];
  }>;
}

export interface CompanySettings {
  id: number;
  company_name?: string | null;
  address?: string | null;
  contact_email?: string | null;
  contact_phone?: string | null;
  logo_path?: string | null;
}

export interface CctpRoomExtracted {
  room_name: string;
  room_type: string;
  surface_m2?: number | null;
  suggested_system_type?: string | null;
}
