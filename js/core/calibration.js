/** Calibration & coordonnées plan ↔ monde (mètres). */
export function metersPerPixelFromSegment(pxLength, realMeters) {
  if (!pxLength || !realMeters) return null;
  return realMeters / pxLength;
}

export function m2px(calibration, meters) {
  return calibration ? meters / calibration.metersPerPixel : meters * 50;
}

export function px2m(calibration, px) {
  return calibration ? px * calibration.metersPerPixel : px / 50;
}

export function planToWorld(lvl, px, py) {
  const mpp = lvl.calibration ? lvl.calibration.metersPerPixel : 1 / 50;
  const ip = lvl.insertionPoint || { x: 0, y: 0 };
  return { x: (px - ip.x) * mpp, z: (py - ip.y) * mpp };
}

export function worldToPlanPx(lvl, wx, wz) {
  const mpp = lvl.calibration ? lvl.calibration.metersPerPixel : 1 / 50;
  const ip = lvl.insertionPoint || { x: 0, y: 0 };
  return { x: wx / mpp + ip.x, y: wz / mpp + ip.y };
}

/** Homothétie entre 2 paires de points d'insertion (niveaux A/B). */
export function alignScaleFromTwoPoints(refA, refB, curA, curB) {
  const dRef = Math.hypot(refB.x - refA.x, refB.y - refA.y);
  const dCur = Math.hypot(curB.x - curA.x, curB.y - curA.y);
  if (dRef < 1e-9 || dCur < 1e-9) return null;
  return dRef / dCur;
}
