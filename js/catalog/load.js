/** Chargeurs catalogue (ES modules). */
export async function loadSymbolsCatalog(url = '../catalog/symbols.json') {
  const res = await fetch(url);
  if (!res.ok) throw new Error('Catalogue symboles introuvable: ' + url);
  return res.json();
}

export async function loadManufacturerPack(url) {
  const res = await fetch(url);
  if (!res.ok) throw new Error('Pack fabricant introuvable: ' + url);
  return res.json();
}

/** Fusionne un pack fabricant sur un catalogue de base (par id). */
export function mergePack(catalog, pack) {
  const out = structuredClone(catalog);
  const all = [];
  for (const fam of Object.values(out.families || {})) {
    all.push(...(fam.items || []));
  }
  const byId = Object.fromEntries(all.map((s) => [s.id, s]));
  for (const s of pack.symbols || []) {
    const base = byId[s.base] || {};
    const merged = { ...base, ...s, manufacturer: pack.id };
    // Attache dans la même famille que le symbole de base si possible
    let placed = false;
    for (const fam of Object.values(out.families || {})) {
      const idx = (fam.items || []).findIndex((it) => it.id === s.base);
      if (idx >= 0) {
        fam.items.push(merged);
        placed = true;
        break;
      }
    }
    if (!placed) {
      out.families = out.families || {};
      out.families.custom = out.families.custom || { label: 'Fabricants', items: [] };
      out.families.custom.items.push(merged);
    }
  }
  return out;
}
