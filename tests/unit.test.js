/**
 * Tests unitaires — calibration, diamètres, bilan, export DXF.
 * Exécution : node --test tests/unit.test.js
 * (Node 18+ / 22)
 */
import { describe, it } from 'node:test';
import assert from 'node:assert/strict';
import {
  metersPerPixelFromSegment,
  m2px,
  px2m,
  planToWorld,
  worldToPlanPx,
  alignScaleFromTwoPoints,
} from '../js/core/calibration.js';
import {
  areaCircular,
  areaRectangular,
  velocityMs,
  velocityStatus,
  pressureDropPa,
  parseCircDiameter,
  parseRectDims,
} from '../js/core/diameters.js';
import {
  bilanNiveau,
  bilanForCentral,
  collectDebitGraphFrom,
  equipmentDebit,
} from '../js/core/bilan.js';
import { buildDxf, DEFAULT_DXF_LAYERS } from '../js/core/dxf.js';

describe('calibration', () => {
  it('metersPerPixelFromSegment', () => {
    assert.equal(metersPerPixelFromSegment(100, 5), 0.05);
    assert.equal(metersPerPixelFromSegment(0, 5), null);
  });
  it('m2px / px2m round-trip', () => {
    const cal = { metersPerPixel: 0.02 };
    assert.equal(m2px(cal, 1), 50);
    assert.equal(px2m(cal, 50), 1);
  });
  it('planToWorld / worldToPlanPx', () => {
    const lvl = { calibration: { metersPerPixel: 0.01 }, insertionPoint: { x: 100, y: 200 } };
    const w = planToWorld(lvl, 200, 200);
    assert.equal(w.x, 1);
    assert.equal(w.z, 0);
    const p = worldToPlanPx(lvl, 1, 0);
    assert.equal(p.x, 200);
    assert.equal(p.y, 200);
  });
  it('alignScaleFromTwoPoints', () => {
    const s = alignScaleFromTwoPoints({ x: 0, y: 0 }, { x: 10, y: 0 }, { x: 0, y: 0 }, { x: 20, y: 0 });
    assert.equal(s, 0.5);
  });
});

describe('diameters', () => {
  it('circular area & velocity', () => {
    const A = areaCircular(200); // Ø200
    assert.ok(Math.abs(A - Math.PI * 0.1 ** 2) < 1e-9);
    const v = velocityMs(360, A); // 0.1 m3/s / A
    assert.ok(v > 2 && v < 4);
    assert.equal(velocityStatus(3), 'ok');
    assert.equal(velocityStatus(9), 'bad');
  });
  it('rectangular parse & pressure drop', () => {
    assert.deepEqual(parseRectDims('400x200'), { w: 400, h: 200 });
    assert.equal(parseCircDiameter('Ø315'), 315);
    const A = areaRectangular(400, 200);
    assert.ok(Math.abs(A - 0.08) < 1e-12);
    const dp = pressureDropPa({ lengthM: 10, Q_m3h: 1000, accessories: 2 });
    assert.ok(dp > 10);
  });
});

describe('bilan', () => {
  it('bilanNiveau soufflage/reprise', () => {
    const objs = [
      { id: '1', type: 'diffusion', debit: 100 },
      { id: '2', type: 'diffusion', debit: 50 },
      { id: '3', type: 'reprise', debit: 140 },
    ];
    const b = bilanNiveau(objs);
    assert.equal(b.souff, 150);
    assert.equal(b.rep, 140);
    assert.equal(b.delta, 10);
  });
  it('continuité graphe depuis CTA', () => {
    const objects = [
      { id: 'cta1', type: 'cta', debit: 500 },
      { id: 'd1', type: 'diffusion', debit: 200 },
      { id: 'd2', type: 'diffusion', debit: 300 },
      { id: 'r1', type: 'reprise', debit: 480 },
    ];
    const networks = [
      { id: 'n1', startConn: 'cta1', endConn: 'd1', servedEquipIds: ['d2'] },
      { id: 'n2', startConn: 'cta1', endConn: 'r1', servedEquipIds: [] },
    ];
    const g = collectDebitGraphFrom('cta1', { objects, networks });
    assert.equal(g.linked.length, 4);
    assert.equal(g.nets.length, 2);
    const b = bilanForCentral(objects[0], { objects, networks });
    assert.equal(b.rep, 480);
    assert.ok(b.souff >= 200);
  });
  it('equipmentDebit', () => {
    assert.equal(equipmentDebit({ debit: '12,5' }), 12); // parseFloat s'arrête à la virgule
    assert.equal(equipmentDebit({ debit: 12.5 }), 12.5);
  });
});

describe('dxf', () => {
  it('buildDxf contains layers and LWPOLYLINE', () => {
    const dxf = buildDxf({
      layers: DEFAULT_DXF_LAYERS,
      polylines: [{ layer: 'RESEAU_AER_CIRC', points: [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 1, y: 1 }], closed: false }],
      texts: [{ layer: 'ANNOT', x: 0.5, y: 0.5, text: 'REP-01', height: 0.2 }],
      blocks: [{ layer: 'EQUIP_CVC', x: 2, y: 2, label: 'CTA' }],
    });
    assert.match(dxf, /LWPOLYLINE/);
    assert.match(dxf, /RESEAU_AER_CIRC/);
    assert.match(dxf, /REP-01/);
    assert.match(dxf, /EOF/);
  });
});
