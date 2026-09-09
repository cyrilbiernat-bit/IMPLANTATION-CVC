import axios from "axios";

export const api = axios.create({
  baseURL: "/api",
});

/** Extrait un message d'erreur lisible depuis une erreur axios/FastAPI.
 * FastAPI renvoie `detail` sous forme de chaîne (HTTPException) ou de
 * liste d'objets {type, loc, msg, input} (erreur de validation Pydantic) :
 * on ne rend jamais ces objets directement dans du JSX.
 */
export function extractErrorMessage(error: unknown, fallback = "Une erreur est survenue."): string {
  const detail = (error as any)?.response?.data?.detail;
  if (!detail) return fallback;
  if (typeof detail === "string") return detail;
  if (Array.isArray(detail)) {
    return detail
      .map((d) => (typeof d === "string" ? d : d?.msg ?? JSON.stringify(d)))
      .join(" — ");
  }
  return fallback;
}
