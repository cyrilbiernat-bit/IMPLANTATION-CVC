/** Dimensionnement aéraulique / hydraulique simplifié. */

export function areaCircular(diameterMm) {
  const d = diameterMm / 1000;
  return Math.PI * (d / 2) ** 2;
}

export function areaRectangular(widthMm, heightMm) {
  return (widthMm / 1000) * (heightMm / 1000);
}

/** Vitesse (m/s) pour un débit Q en m³/h et une section A en m². */
export function velocityMs(Q_m3h, area_m2) {
  if (!Q_m3h || !area_m2) return null;
  return Q_m3h / 3600 / area_m2;
}

export function velocityStatus(v, { softMin = 2, softMax = 6, hardMin = 1, hardMax = 8 } = {}) {
  if (v == null || Number.isNaN(v)) return 'ok';
  if (v < hardMin || v > hardMax) return 'bad';
  if (v < softMin || v > softMax) return 'warn';
  return 'ok';
}

/**
 * ΔP linéaire simplifiée (Pa) : R·L·(Q/1000)^1.8 + accessoires.
 * R défaut ~1 Pa/m à 1000 m³/h (ordre de grandeur CVC).
 */
export function pressureDropPa({ lengthM, Q_m3h, accessories = 0, R = 1 }) {
  if (!lengthM || !Q_m3h) return 0;
  const linear = R * lengthM * Math.pow(Math.max(Q_m3h, 1) / 1000, 1.8);
  const acc = accessories * 5; // ~5 Pa / accessoire
  return linear + acc;
}

export function parseCircDiameter(dim) {
  if (!dim) return null;
  const m = String(dim).match(/Ø?\s*(\d+)/i);
  return m ? parseFloat(m[1]) : null;
}

export function parseRectDims(dim) {
  if (!dim) return null;
  const m = String(dim).match(/(\d+)\s*[x×]\s*(\d+)/i);
  return m ? { w: parseFloat(m[1]), h: parseFloat(m[2]) } : null;
}
