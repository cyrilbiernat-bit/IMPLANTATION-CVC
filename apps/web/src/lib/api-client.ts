export const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";

export interface DrawingDto {
  id: string;
  fileName: string;
  blobUrl: string;
  nbPages: number;
  uploadedAt: string;
}

export async function uploadDrawing(file: File): Promise<DrawingDto> {
  const form = new FormData();
  form.append("file", file);

  const res = await fetch(`${API_BASE_URL}/api/v1/drawings`, {
    method: "POST",
    body: form,
  });

  if (!res.ok) {
    throw new Error(`Échec de l'enregistrement du plan (${res.status})`);
  }

  return res.json();
}
