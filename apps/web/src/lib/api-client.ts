export const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";

export interface PointDto {
  x: number;
  y: number;
}

export interface CalibrationDto {
  pageNumber: number;
  pointA: PointDto;
  pointB: PointDto;
  realDistanceMeters: number;
  metersPerPixel: number;
}

export type PlanFormat = "Pdf" | "Dxf" | "Dwg";

export interface ProjectDto {
  id: string;
  name: string;
  createdAt: string;
}

export interface AccessoryCountDto {
  type: CvcObjectType;
  count: number;
}

export interface ProjectMetresDto {
  projectId: string;
  drawingCount: number;
  totalDuctLengthMeters: number;
  totalInsulationAreaM2: number;
  totalWeightKg: number;
  accessoryCounts: AccessoryCountDto[];
}

export interface DrawingDto {
  id: string;
  projectId: string;
  fileName: string;
  format: PlanFormat;
  blobUrl: string;
  nbPages: number;
  uploadedAt: string;
  calibration: CalibrationDto | null;
}

export type PlanEntityDto =
  | { type: "line"; points: [PointDto, PointDto] }
  | { type: "polyline"; points: PointDto[]; closed: boolean }
  | { type: "circle"; center: PointDto; radius: number }
  | { type: "arc"; center: PointDto; radius: number; startAngle: number; endAngle: number }
  | { type: "text"; position: PointDto; text: string; height: number };

export interface LayerDto {
  id: string;
  drawingId: string;
  name: string;
  color: string;
  visible: boolean;
  locked: boolean;
  order: number;
}

export type CvcObjectType =
  | "GaineRectangulaire"
  | "GaineCirculaire"
  | "Coude"
  | "Te"
  | "Reduction"
  | "Bouche"
  | "Diffuseur"
  | "Extracteur"
  | "Cta";

export interface CvcObjectDto {
  id: string;
  drawingId: string;
  layerId: string;
  type: CvcObjectType;
  start: PointDto | null;
  end: PointDto | null;
  widthMm: number | null;
  heightMm: number | null;
  diameterMm: number | null;
  lengthMeters: number | null;
  weightKg: number | null;
  insulationAreaM2: number | null;
  debitM3h: number | null;
  vitesseMs: number | null;
  pressionPa: number | null;
  position: PointDto | null;
  rotationRad: number;
  connectedObjectIds: string[];
}

export interface CreateDuctInput {
  type: "GaineRectangulaire" | "GaineCirculaire";
  layerId: string;
  start: PointDto;
  end: PointDto;
  widthMm?: number;
  heightMm?: number;
  diameterMm?: number;
  debitM3h?: number;
  vitesseMs?: number;
  pressionPa?: number;
}

export interface CreatePointObjectInput {
  type: Exclude<CvcObjectType, "GaineRectangulaire" | "GaineCirculaire">;
  layerId: string;
  position: PointDto;
  rotationRad?: number;
  debitM3h?: number;
  vitesseMs?: number;
  pressionPa?: number;
}

async function parseErrorMessage(res: Response, fallback: string): Promise<string> {
  try {
    const body = await res.json();
    if (typeof body?.message === "string") return body.message;
  } catch {
    // corps non-JSON : on garde le message par défaut
  }
  return fallback;
}

export async function fetchProjects(): Promise<ProjectDto[]> {
  const res = await fetch(`${API_BASE_URL}/api/v1/projects`);

  if (!res.ok) {
    throw new Error(await parseErrorMessage(res, `Échec de la lecture des projets (${res.status})`));
  }

  return res.json();
}

export async function fetchProject(projectId: string): Promise<ProjectDto> {
  const res = await fetch(`${API_BASE_URL}/api/v1/projects/${projectId}`);

  if (!res.ok) {
    throw new Error(await parseErrorMessage(res, `Échec de la lecture du projet (${res.status})`));
  }

  return res.json();
}

export async function createProject(name: string): Promise<ProjectDto> {
  const res = await fetch(`${API_BASE_URL}/api/v1/projects`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ name }),
  });

  if (!res.ok) {
    throw new Error(await parseErrorMessage(res, `Échec de la création du projet (${res.status})`));
  }

  return res.json();
}

export async function fetchProjectDrawings(projectId: string): Promise<DrawingDto[]> {
  const res = await fetch(`${API_BASE_URL}/api/v1/projects/${projectId}/drawings`);

  if (!res.ok) {
    throw new Error(await parseErrorMessage(res, `Échec de la lecture des plans (${res.status})`));
  }

  return res.json();
}

export async function fetchProjectMetres(projectId: string): Promise<ProjectMetresDto> {
  const res = await fetch(`${API_BASE_URL}/api/v1/projects/${projectId}/metres`);

  if (!res.ok) {
    throw new Error(await parseErrorMessage(res, `Échec de la lecture des métrés (${res.status})`));
  }

  return res.json();
}

export async function uploadDrawing(projectId: string, file: File): Promise<DrawingDto> {
  const form = new FormData();
  form.append("file", file);

  const res = await fetch(`${API_BASE_URL}/api/v1/projects/${projectId}/drawings`, {
    method: "POST",
    body: form,
  });

  if (!res.ok) {
    throw new Error(await parseErrorMessage(res, `Échec de l'enregistrement du plan (${res.status})`));
  }

  return res.json();
}

export async function fetchDrawingEntities(drawingId: string): Promise<PlanEntityDto[]> {
  const res = await fetch(`${API_BASE_URL}/api/v1/drawings/${drawingId}/entities`);

  if (!res.ok) {
    throw new Error(await parseErrorMessage(res, `Échec de la lecture du plan (${res.status})`));
  }

  return res.json();
}

export async function fetchCvcObjects(drawingId: string): Promise<CvcObjectDto[]> {
  const res = await fetch(`${API_BASE_URL}/api/v1/drawings/${drawingId}/objects`);

  if (!res.ok) {
    throw new Error(await parseErrorMessage(res, `Échec de la lecture des objets (${res.status})`));
  }

  return res.json();
}

export async function createCvcObject(
  drawingId: string,
  input: CreateDuctInput | CreatePointObjectInput,
): Promise<CvcObjectDto> {
  const res = await fetch(`${API_BASE_URL}/api/v1/drawings/${drawingId}/objects`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(input),
  });

  if (!res.ok) {
    throw new Error(await parseErrorMessage(res, `Échec de la création de l'objet (${res.status})`));
  }

  return res.json();
}

export async function deleteCvcObject(drawingId: string, objectId: string): Promise<void> {
  const res = await fetch(`${API_BASE_URL}/api/v1/drawings/${drawingId}/objects/${objectId}`, {
    method: "DELETE",
  });

  if (!res.ok) {
    throw new Error(await parseErrorMessage(res, `Échec de la suppression (${res.status})`));
  }
}

export async function fetchLayers(drawingId: string): Promise<LayerDto[]> {
  const res = await fetch(`${API_BASE_URL}/api/v1/drawings/${drawingId}/layers`);

  if (!res.ok) {
    throw new Error(await parseErrorMessage(res, `Échec de la lecture des calques (${res.status})`));
  }

  return res.json();
}

export async function createLayer(drawingId: string, name?: string): Promise<LayerDto> {
  const res = await fetch(`${API_BASE_URL}/api/v1/drawings/${drawingId}/layers`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ name: name ?? null }),
  });

  if (!res.ok) {
    throw new Error(await parseErrorMessage(res, `Échec de la création du calque (${res.status})`));
  }

  return res.json();
}

export async function updateLayer(
  drawingId: string,
  layerId: string,
  input: { name?: string; color?: string; visible?: boolean; locked?: boolean },
): Promise<LayerDto> {
  const res = await fetch(`${API_BASE_URL}/api/v1/drawings/${drawingId}/layers/${layerId}`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      name: input.name ?? null,
      color: input.color ?? null,
      visible: input.visible ?? null,
      locked: input.locked ?? null,
    }),
  });

  if (!res.ok) {
    throw new Error(await parseErrorMessage(res, `Échec de la mise à jour du calque (${res.status})`));
  }

  return res.json();
}

export async function deleteLayer(drawingId: string, layerId: string): Promise<void> {
  const res = await fetch(`${API_BASE_URL}/api/v1/drawings/${drawingId}/layers/${layerId}`, {
    method: "DELETE",
  });

  if (!res.ok) {
    throw new Error(await parseErrorMessage(res, `Échec de la suppression du calque (${res.status})`));
  }
}

export async function calibrateDrawing(
  drawingId: string,
  input: { pageNumber: number; pointA: PointDto; pointB: PointDto; realDistanceMeters: number },
): Promise<DrawingDto> {
  const res = await fetch(`${API_BASE_URL}/api/v1/drawings/${drawingId}/calibration`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(input),
  });

  if (!res.ok) {
    throw new Error(await parseErrorMessage(res, `Échec de la calibration (${res.status})`));
  }

  return res.json();
}
