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

export interface DrawingDto {
  id: string;
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

async function parseErrorMessage(res: Response, fallback: string): Promise<string> {
  try {
    const body = await res.json();
    if (typeof body?.message === "string") return body.message;
  } catch {
    // corps non-JSON : on garde le message par défaut
  }
  return fallback;
}

export async function uploadDrawing(file: File): Promise<DrawingDto> {
  const form = new FormData();
  form.append("file", file);

  const res = await fetch(`${API_BASE_URL}/api/v1/drawings`, {
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
