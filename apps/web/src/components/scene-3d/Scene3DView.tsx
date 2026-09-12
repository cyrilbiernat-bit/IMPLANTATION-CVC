"use client";

import { useEffect, useRef, useState } from "react";
import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import {
  fetchCvcObjects,
  fetchProjectDrawings,
  type CvcObjectDto,
  type CvcObjectType,
  type DrawingDto,
} from "@/lib/api-client";

/**
 * Hauteur d'installation par défaut (faux plafond) appliquée à tous les
 * objets — le modèle de données ne mémorise pas encore de hauteur réelle
 * par objet (hors périmètre du MVP, comme le calcul aéraulique).
 */
const INSTALL_HEIGHT_M = 2.5;
const MIN_CROSS_SECTION_MM = 100;

const TYPE_COLORS: Record<CvcObjectType, number> = {
  GaineRectangulaire: 0x0d9488,
  GaineCirculaire: 0x14b8a6,
  Coude: 0xf59e0b,
  Te: 0xf59e0b,
  Reduction: 0xf59e0b,
  Bouche: 0x38bdf8,
  Diffuseur: 0x38bdf8,
  Extracteur: 0xef4444,
  Cta: 0x8b5cf6,
};

const TYPE_LABELS: Record<CvcObjectType, string> = {
  GaineRectangulaire: "Gaine rectangulaire",
  GaineCirculaire: "Gaine circulaire",
  Coude: "Coude",
  Te: "Té",
  Reduction: "Réduction",
  Bouche: "Bouche",
  Diffuseur: "Diffuseur",
  Extracteur: "Extracteur",
  Cta: "CTA",
};

export function Scene3DView({ projectId }: { projectId: string }) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [drawings, setDrawings] = useState<DrawingDto[] | null>(null);
  const [selectedDrawingId, setSelectedDrawingId] = useState<string | null>(null);
  const [objects, setObjects] = useState<CvcObjectDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      setError(null);
      try {
        const data = await fetchProjectDrawings(projectId);
        if (cancelled) return;
        setDrawings(data);
        setSelectedDrawingId((prev) => prev ?? data.at(-1)?.id ?? null);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : "Échec de la lecture des plans.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [projectId]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      if (!selectedDrawingId) {
        if (!cancelled) setObjects(null);
        return;
      }
      try {
        const data = await fetchCvcObjects(selectedDrawingId);
        if (!cancelled) setObjects(data);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : "Échec de la lecture des objets.");
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [selectedDrawingId]);

  const selectedDrawing = drawings?.find((d) => d.id === selectedDrawingId) ?? null;
  const calibration = selectedDrawing?.calibration ?? null;

  useEffect(() => {
    const container = containerRef.current;
    if (!container || !calibration || !objects) return;
    const mpp = calibration.metersPerPixel;
    const toWorld = (x: number, y: number): [number, number] => [x * mpp, -y * mpp];

    const bounds = { minX: Infinity, maxX: -Infinity, minZ: Infinity, maxZ: -Infinity };
    const consider = (x: number, z: number) => {
      bounds.minX = Math.min(bounds.minX, x);
      bounds.maxX = Math.max(bounds.maxX, x);
      bounds.minZ = Math.min(bounds.minZ, z);
      bounds.maxZ = Math.max(bounds.maxZ, z);
    };
    for (const obj of objects) {
      if (obj.start) consider(...toWorld(obj.start.x, obj.start.y));
      if (obj.end) consider(...toWorld(obj.end.x, obj.end.y));
      if (obj.position) consider(...toWorld(obj.position.x, obj.position.y));
    }
    const hasBounds = Number.isFinite(bounds.minX);
    const centerX = hasBounds ? (bounds.minX + bounds.maxX) / 2 : 0;
    const centerZ = hasBounds ? (bounds.minZ + bounds.maxZ) / 2 : 0;
    const extent = hasBounds ? Math.max(bounds.maxX - bounds.minX, bounds.maxZ - bounds.minZ, 5) : 10;

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x0f172a);

    const camera = new THREE.PerspectiveCamera(50, container.clientWidth / container.clientHeight, 0.05, 2000);
    camera.position.set(centerX + extent * 0.7, extent * 0.65 + INSTALL_HEIGHT_M, centerZ + extent * 0.7);

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setSize(container.clientWidth, container.clientHeight);
    container.appendChild(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.target.set(centerX, INSTALL_HEIGHT_M, centerZ);
    controls.enableDamping = true;
    controls.update();

    scene.add(new THREE.AmbientLight(0xffffff, 0.7));
    const dirLight = new THREE.DirectionalLight(0xffffff, 0.9);
    dirLight.position.set(centerX + extent, extent * 1.5, centerZ + extent);
    scene.add(dirLight);

    const grid = new THREE.GridHelper(Math.max(extent * 1.6, 10), 24, 0x334155, 0x1e293b);
    grid.position.set(centerX, 0, centerZ);
    scene.add(grid);

    const disposables: { geometry: THREE.BufferGeometry; material: THREE.Material }[] = [];

    for (const obj of objects) {
      const color = TYPE_COLORS[obj.type];

      if (obj.start && obj.end) {
        const [sx, sz] = toWorld(obj.start.x, obj.start.y);
        const [ex, ez] = toWorld(obj.end.x, obj.end.y);
        const length = Math.hypot(ex - sx, ez - sz);
        if (length < 1e-3) continue;

        const isCircular = obj.type === "GaineCirculaire";
        const material = new THREE.MeshStandardMaterial({ color, metalness: 0.25, roughness: 0.55 });
        let geometry: THREE.BufferGeometry;
        let mesh: THREE.Mesh;

        if (isCircular) {
          const radius = Math.max(obj.diameterMm ?? MIN_CROSS_SECTION_MM, MIN_CROSS_SECTION_MM) / 2000;
          geometry = new THREE.CylinderGeometry(radius, radius, length, 20);
          mesh = new THREE.Mesh(geometry, material);
          const angle = Math.atan2(ez - sz, ex - sx);
          const alignToX = new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 0, 1), Math.PI / 2);
          const pointAlongDirection = new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), -angle);
          mesh.quaternion.copy(pointAlongDirection.multiply(alignToX));
        } else {
          const heightM = Math.max(obj.heightMm ?? MIN_CROSS_SECTION_MM, MIN_CROSS_SECTION_MM) / 1000;
          const widthM = Math.max(obj.widthMm ?? MIN_CROSS_SECTION_MM, MIN_CROSS_SECTION_MM) / 1000;
          geometry = new THREE.BoxGeometry(length, heightM, widthM);
          mesh = new THREE.Mesh(geometry, material);
          const angle = Math.atan2(ez - sz, ex - sx);
          mesh.rotation.y = -angle;
        }

        mesh.position.set((sx + ex) / 2, INSTALL_HEIGHT_M, (sz + ez) / 2);
        scene.add(mesh);
        disposables.push({ geometry, material });
      } else if (obj.position) {
        const [px, pz] = toWorld(obj.position.x, obj.position.y);
        const size = obj.type === "Cta" ? 0.6 : 0.3;
        const geometry = new THREE.BoxGeometry(size, size, size);
        const material = new THREE.MeshStandardMaterial({ color, metalness: 0.15, roughness: 0.7 });
        const mesh = new THREE.Mesh(geometry, material);
        mesh.position.set(px, INSTALL_HEIGHT_M, pz);
        mesh.rotation.y = -obj.rotationRad;
        scene.add(mesh);
        disposables.push({ geometry, material });
      }
    }

    let frameId = 0;
    const animate = () => {
      frameId = requestAnimationFrame(animate);
      controls.update();
      renderer.render(scene, camera);
    };
    animate();

    const onResize = () => {
      if (!container) return;
      camera.aspect = container.clientWidth / container.clientHeight;
      camera.updateProjectionMatrix();
      renderer.setSize(container.clientWidth, container.clientHeight);
    };
    window.addEventListener("resize", onResize);

    return () => {
      cancelAnimationFrame(frameId);
      window.removeEventListener("resize", onResize);
      controls.dispose();
      renderer.dispose();
      container.removeChild(renderer.domElement);
      for (const { geometry, material } of disposables) {
        geometry.dispose();
        material.dispose();
      }
    };
  }, [calibration, objects]);

  const usedTypes = objects ? [...new Set(objects.map((o) => o.type))] : [];

  return (
    <div className="flex flex-1 flex-col gap-3">
      <div className="flex flex-wrap items-center gap-3">
        {drawings && drawings.length > 0 && (
          <select
            value={selectedDrawingId ?? ""}
            onChange={(e) => setSelectedDrawingId(e.target.value)}
            className="rounded border border-slate-700 bg-slate-900 px-2 py-1.5 text-sm text-slate-200"
          >
            {drawings.map((d) => (
              <option key={d.id} value={d.id}>
                {d.fileName}
              </option>
            ))}
          </select>
        )}
        {usedTypes.length > 0 && (
          <div className="flex flex-wrap items-center gap-3 text-xs text-slate-400">
            {usedTypes.map((t) => (
              <span key={t} className="flex items-center gap-1.5">
                <span
                  className="h-2.5 w-2.5 rounded-sm"
                  style={{ backgroundColor: `#${TYPE_COLORS[t].toString(16).padStart(6, "0")}` }}
                />
                {TYPE_LABELS[t]}
              </span>
            ))}
          </div>
        )}
        <span className="ml-auto text-xs text-slate-500">
          Hauteur d&apos;installation par défaut&nbsp;: {INSTALL_HEIGHT_M} m
        </span>
      </div>

      <div className="relative min-h-0 flex-1 overflow-hidden rounded-lg border border-slate-700 bg-slate-950">
        {loading && (
          <div className="absolute inset-0 flex items-center justify-center text-slate-400">Chargement…</div>
        )}
        {!loading && error && (
          <div className="absolute inset-0 flex items-center justify-center px-6 text-center text-rose-400">
            {error}
          </div>
        )}
        {!loading && !error && drawings?.length === 0 && (
          <div className="absolute inset-0 flex items-center justify-center px-6 text-center text-slate-500">
            Importez d&apos;abord un plan dans l&apos;onglet Plan.
          </div>
        )}
        {!loading && !error && selectedDrawing && !calibration && (
          <div className="absolute inset-0 flex items-center justify-center px-6 text-center text-slate-500">
            Calibrez ce plan (onglet Plan) avant d&apos;afficher la vue 3D — elle a besoin de mesures réelles.
          </div>
        )}
        {!loading && !error && calibration && objects?.length === 0 && (
          <div className="absolute inset-0 flex items-center justify-center px-6 text-center text-slate-500">
            Aucun objet CVC dessiné sur ce plan pour l&apos;instant.
          </div>
        )}
        <div ref={containerRef} className="h-full w-full" data-testid="scene-3d-canvas" />
      </div>
    </div>
  );
}
