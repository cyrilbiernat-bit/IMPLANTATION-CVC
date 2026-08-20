/** Export DXF minimal (LWPOLYLINE, TEXT, LAYER). */

export function dxfEscape(s) {
  return String(s ?? '').replace(/\r?\n/g, ' ');
}

export function buildDxf({ layers, polylines, texts, blocks = [] }) {
  const push = (arr, code, val) => {
    arr.push(String(code), String(val));
  };
  const out = [];
  push(out, 0, 'SECTION');
  push(out, 2, 'HEADER');
  push(out, 9, '$INSUNITS');
  push(out, 70, '6'); // meters
  push(out, 0, 'ENDSEC');

  push(out, 0, 'SECTION');
  push(out, 2, 'TABLES');
  push(out, 0, 'TABLE');
  push(out, 2, 'LAYER');
  push(out, 70, String(layers.length));
  for (const L of layers) {
    push(out, 0, 'LAYER');
    push(out, 2, L);
    push(out, 70, '0');
    push(out, 62, '7');
    push(out, 6, 'CONTINUOUS');
  }
  push(out, 0, 'ENDTAB');
  push(out, 0, 'ENDSEC');

  push(out, 0, 'SECTION');
  push(out, 2, 'ENTITIES');

  for (const pl of polylines) {
    push(out, 0, 'LWPOLYLINE');
    push(out, 8, pl.layer || '0');
    push(out, 90, String(pl.points.length));
    push(out, 70, pl.closed ? '1' : '0');
    for (const p of pl.points) {
      push(out, 10, p.x.toFixed(4));
      push(out, 20, p.y.toFixed(4));
    }
  }

  for (const tx of texts) {
    push(out, 0, 'TEXT');
    push(out, 8, tx.layer || 'ANNOT');
    push(out, 10, tx.x.toFixed(4));
    push(out, 20, tx.y.toFixed(4));
    push(out, 40, (tx.height || 0.25).toFixed(3));
    push(out, 1, dxfEscape(tx.text));
  }

  for (const b of blocks) {
    // Insert as a point marker + optional text (simplified "block")
    push(out, 0, 'POINT');
    push(out, 8, b.layer || 'EQUIP');
    push(out, 10, b.x.toFixed(4));
    push(out, 20, b.y.toFixed(4));
    if (b.label) {
      push(out, 0, 'TEXT');
      push(out, 8, b.layer || 'EQUIP');
      push(out, 10, (b.x + 0.1).toFixed(4));
      push(out, 20, (b.y + 0.1).toFixed(4));
      push(out, 40, '0.2');
      push(out, 1, dxfEscape(b.label));
    }
  }

  push(out, 0, 'ENDSEC');
  push(out, 0, 'EOF');
  return out.join('\n');
}

export const DEFAULT_DXF_LAYERS = [
  'RESEAU_AER_RECT',
  'RESEAU_AER_CIRC',
  'RESEAU_HYD_ECS',
  'RESEAU_HYD_ECF',
  'RESEAU_HYD_CH',
  'RESEAU_HYD_EVAC',
  'EQUIP_CVC',
  'EQUIP_PLB',
  'EQUIP_RESERVATION',
  'VERTICALITE',
  'ANNOT',
  'WALL',
];
