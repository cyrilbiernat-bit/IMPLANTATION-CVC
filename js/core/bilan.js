/** Bilan aéraulique et continuité de débit sur graphe. */

export function equipmentDebit(obj) {
  if (!obj) return 0;
  const d = parseFloat(obj.debit);
  return Number.isFinite(d) ? d : 0;
}

export function isSoufflage(obj) {
  const t = (obj && obj.type) || '';
  return /diffusion|soufflage|cta|vmc/i.test(t) && !/reprise/i.test(t);
}

export function isReprise(obj) {
  const t = (obj && obj.type) || '';
  return /reprise|extraction/i.test(t);
}

/** Parcourt le graphe réseaux depuis un équipement (CTA / bouche). */
export function collectDebitGraphFrom(objId, { objects, networks }) {
  const visitedNet = new Set();
  const visitedObj = new Set([objId]);
  const queue = [objId];
  while (queue.length) {
    const id = queue.shift();
    for (const n of networks) {
      const touches =
        n.startConn === id ||
        n.endConn === id ||
        (n.servedEquipIds || []).includes(id);
      if (!touches || visitedNet.has(n.id)) continue;
      visitedNet.add(n.id);
      for (const oid of [n.startConn, n.endConn, ...(n.servedEquipIds || [])]) {
        if (!oid || String(oid).startsWith('V:') || visitedObj.has(oid)) continue;
        visitedObj.add(oid);
        queue.push(oid);
      }
    }
  }
  const linked = [...visitedObj]
    .map((id) => objects.find((o) => o.id === id))
    .filter(Boolean);
  const nets = networks.filter((n) => visitedNet.has(n.id));
  return { linked, nets, visitedObj, visitedNet };
}

export function bilanForCentral(central, { objects, networks }) {
  const { linked } = collectDebitGraphFrom(central.id, { objects, networks });
  let souff = 0;
  let rep = 0;
  for (const o of linked) {
    const q = equipmentDebit(o);
    if (o.id === central.id) continue;
    if (isReprise(o)) rep += q;
    else if (isSoufflage(o) || q) souff += q;
  }
  // Si la CTA porte un débit nominal, l'utiliser comme référence soufflage
  const ctaQ = equipmentDebit(central);
  if (ctaQ && !souff) souff = ctaQ;
  return { souff, rep, delta: souff - rep, linkedCount: linked.length };
}

export function bilanNiveau(objects) {
  let souff = 0;
  let rep = 0;
  for (const o of objects) {
    const q = equipmentDebit(o);
    if (isReprise(o)) rep += q;
    else if (isSoufflage(o)) souff += q;
  }
  return { souff, rep, delta: souff - rep };
}
