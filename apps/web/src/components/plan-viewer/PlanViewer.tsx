"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { PageViewport, PDFDocumentProxy, RenderTask } from "pdfjs-dist";
import {
  calibrateDrawing,
  createCvcObject,
  createLayer,
  deleteCvcObject,
  deleteLayer,
  fetchCvcObjects,
  fetchDrawingEntities,
  fetchLayers,
  updateLayer,
  uploadDrawing,
  type CalibrationDto,
  type CreateDuctInput,
  type CreatePointObjectInput,
  type CvcObjectDto,
  type CvcObjectType,
  type LayerDto,
  type PlanEntityDto,
} from "@/lib/api-client";

const ZOOM_STEP = 1.2;
const PDF_MIN_SCALE = 0.2;
const PDF_MAX_SCALE = 6;

type SaveStatus = "idle" | "uploading" | "saved" | "error";
type CalibrationMode = "idle" | "picking-a" | "picking-b" | "confirm";
type DrawingPoint = [number, number];
type CadFormat = "dxf" | "dwg";
type PlanFormatState = "pdf" | CadFormat | null;
type Project = (x: number, y: number) => DrawingPoint;

interface Bounds {
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
}

const DUCT_TYPES = new Set<CvcObjectType>(["GaineRectangulaire", "GaineCirculaire"]);

const CVC_TYPE_LABELS: Record<CvcObjectType, string> = {
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

const POINT_OBJECT_LABELS: Record<CvcObjectType, string> = {
  GaineRectangulaire: "",
  GaineCirculaire: "",
  Coude: "CO",
  Te: "TE",
  Reduction: "RD",
  Bouche: "BO",
  Diffuseur: "DI",
  Extracteur: "EX",
  Cta: "CTA",
};

const TOOLS: { type: CvcObjectType; icon: string; short: string }[] = [
  { type: "GaineRectangulaire", icon: "▭", short: "Rect." },
  { type: "GaineCirculaire", icon: "◯", short: "Circ." },
  { type: "Coude", icon: "∟", short: "Coude" },
  { type: "Te", icon: "┼", short: "Té" },
  { type: "Reduction", icon: "▷", short: "Réduc." },
  { type: "Bouche", icon: "▣", short: "Bouche" },
  { type: "Diffuseur", icon: "◈", short: "Diffus." },
  { type: "Extracteur", icon: "⊘", short: "Extract." },
  { type: "Cta", icon: "▦", short: "CTA" },
];

async function getPdfjs() {
  const pdfjsLib = await import("pdfjs-dist");
  pdfjsLib.GlobalWorkerOptions.workerSrc = new URL(
    "pdfjs-dist/build/pdf.worker.min.mjs",
    import.meta.url,
  ).toString();
  return pdfjsLib;
}

const distanceFormatter = new Intl.NumberFormat("fr-FR", {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const UNSUPPORTED_FORMAT_MESSAGES: Record<string, string> = {
  ifc: "Le format IFC sera pris en charge dans une prochaine itération. En attendant, exportez votre plan en DXF ou PDF.",
  rvt: "Le format natif Revit (.rvt) n'est pas pris en charge. Depuis Revit, exportez votre vue en DWG, DXF ou PDF (Fichier > Exporter > Formats CAO).",
};

function computeBounds(entities: PlanEntityDto[]): Bounds | null {
  let minX = Infinity;
  let minY = Infinity;
  let maxX = -Infinity;
  let maxY = -Infinity;
  const consider = (x: number, y: number) => {
    minX = Math.min(minX, x);
    minY = Math.min(minY, y);
    maxX = Math.max(maxX, x);
    maxY = Math.max(maxY, y);
  };

  for (const entity of entities) {
    switch (entity.type) {
      case "line":
      case "polyline":
        entity.points.forEach((p) => consider(p.x, p.y));
        break;
      case "circle":
      case "arc":
        consider(entity.center.x - entity.radius, entity.center.y - entity.radius);
        consider(entity.center.x + entity.radius, entity.center.y + entity.radius);
        break;
      case "text":
        consider(entity.position.x, entity.position.y);
        break;
    }
  }

  return Number.isFinite(minX) ? { minX, minY, maxX, maxY } : null;
}

function computeCadViewport(bounds: Bounds, scale: number) {
  const width = Math.max(bounds.maxX - bounds.minX, 1e-6);
  const height = Math.max(bounds.maxY - bounds.minY, 1e-6);
  const pad = Math.max(width, height) * 0.05;
  const naturalWidth = width + pad * 2;
  const naturalHeight = height + pad * 2;
  const cx = (bounds.minX + bounds.maxX) / 2;
  const cy = (bounds.minY + bounds.maxY) / 2;
  const minX = cx - naturalWidth / 2;
  const minY = -cy - naturalHeight / 2;

  return {
    renderedWidth: naturalWidth * scale,
    renderedHeight: naturalHeight * scale,
    viewBox: `${minX} ${minY} ${naturalWidth} ${naturalHeight}`,
    minX,
    minY,
    naturalWidth,
    naturalHeight,
  };
}

/** Le point de dessin existant (extrémité de gaine ou position d'accessoire) le plus proche, dans la tolérance donnée. */
function findSnapTarget(point: DrawingPoint, objects: CvcObjectDto[], toleranceUnits: number): DrawingPoint | null {
  let best: DrawingPoint | null = null;
  let bestDist = toleranceUnits;

  for (const obj of objects) {
    const candidates: DrawingPoint[] = [];
    if (obj.start) candidates.push([obj.start.x, obj.start.y]);
    if (obj.end) candidates.push([obj.end.x, obj.end.y]);
    if (obj.position) candidates.push([obj.position.x, obj.position.y]);

    for (const c of candidates) {
      const d = Math.hypot(c[0] - point[0], c[1] - point[1]);
      if (d < bestDist) {
        bestDist = d;
        best = c;
      }
    }
  }

  return best;
}

export function PlanViewer() {
  const [format, setFormat] = useState<PlanFormatState>(null);
  const [fileName, setFileName] = useState<string | null>(null);
  const [scale, setScale] = useState(1);
  const [scaleBounds, setScaleBounds] = useState({ min: PDF_MIN_SCALE, max: PDF_MAX_SCALE });
  const [rotation, setRotation] = useState(0);
  const [layerVisible, setLayerVisible] = useState(true);
  const [layerOpacity, setLayerOpacity] = useState(100);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saveStatus, setSaveStatus] = useState<SaveStatus>("idle");
  const [saveMessage, setSaveMessage] = useState<string | null>(null);
  const [isDraggingFile, setIsDraggingFile] = useState(false);
  const [drawingId, setDrawingId] = useState<string | null>(null);

  // PDF (module 1, raster via pdf.js).
  const [pdfDoc, setPdfDoc] = useState<PDFDocumentProxy | null>(null);
  const [numPages, setNumPages] = useState(0);
  const [currentPage, setCurrentPage] = useState(1);
  const [pdfViewport, setPdfViewport] = useState<PageViewport | null>(null);
  const [canvasSize, setCanvasSize] = useState({ width: 0, height: 0 });

  // DXF/DWG (vectoriel, rendu SVG).
  const [entities, setEntities] = useState<PlanEntityDto[] | null>(null);
  const bounds = useMemo(() => (entities ? computeBounds(entities) : null), [entities]);

  // Module 2 — calibration (partagée entre les deux modes de rendu).
  const [calibration, setCalibration] = useState<CalibrationDto | null>(null);
  const [calibrationMode, setCalibrationMode] = useState<CalibrationMode>("idle");
  const [pickedPoints, setPickedPoints] = useState<{ a?: DrawingPoint; b?: DrawingPoint }>({});
  const [distanceInput, setDistanceInput] = useState("");
  const [calibrationSaving, setCalibrationSaving] = useState(false);
  const [calibrationError, setCalibrationError] = useState<string | null>(null);

  // Module 3 — dessin CVC.
  const [objects, setObjects] = useState<CvcObjectDto[]>([]);
  const [activeTool, setActiveTool] = useState<CvcObjectType | null>(null);
  const [ductDraft, setDuctDraft] = useState<{ start?: DrawingPoint; end?: DrawingPoint }>({});
  const [ductWidthInput, setDuctWidthInput] = useState("400");
  const [ductHeightInput, setDuctHeightInput] = useState("250");
  const [ductDiameterInput, setDuctDiameterInput] = useState("315");
  const [ductDebitInput, setDuctDebitInput] = useState("");
  const [ductVitesseInput, setDuctVitesseInput] = useState("");
  const [ductPressionInput, setDuctPressionInput] = useState("");
  const [objectSaving, setObjectSaving] = useState(false);
  const [objectError, setObjectError] = useState<string | null>(null);
  const [selectedObjectId, setSelectedObjectId] = useState<string | null>(null);
  const [deletingObject, setDeletingObject] = useState(false);

  // Calques (verrouillage + affichage indépendants des objets CVC).
  const [cvcLayers, setCvcLayers] = useState<LayerDto[]>([]);
  const [activeLayerId, setActiveLayerId] = useState<string | null>(null);
  const [layersError, setLayersError] = useState<string | null>(null);
  const [layersBusy, setLayersBusy] = useState(false);

  const canvasRef = useRef<HTMLCanvasElement>(null);
  const svgRef = useRef<SVGSVGElement>(null);
  const scrollRef = useRef<HTMLDivElement>(null);
  const panState = useRef<{ x: number; y: number; left: number; top: number } | null>(null);
  const renderTaskRef = useRef<RenderTask | null>(null);

  const loadFile = useCallback(async (file: File) => {
    const extension = file.name.split(".").pop()?.toLowerCase() ?? "";

    setLoadError(null);
    setFileName(file.name);
    setCurrentPage(1);
    setRotation(0);
    setCalibration(null);
    setCalibrationMode("idle");
    setPickedPoints({});
    setDrawingId(null);
    setPdfDoc(null);
    setEntities(null);
    setSaveStatus("idle");
    setSaveMessage(null);
    setObjects([]);
    setActiveTool(null);
    setDuctDraft({});
    setObjectError(null);
    setSelectedObjectId(null);
    setCvcLayers([]);
    setActiveLayerId(null);
    setLayersError(null);

    if (extension in UNSUPPORTED_FORMAT_MESSAGES) {
      setFormat(null);
      setLoadError(UNSUPPORTED_FORMAT_MESSAGES[extension]);
      return;
    }
    if (extension !== "pdf" && extension !== "dxf" && extension !== "dwg") {
      setFormat(null);
      setLoadError("Formats acceptés : PDF, DXF, DWG.");
      return;
    }

    if (extension === "pdf") {
      setFormat("pdf");
      setScale(1);
      setScaleBounds({ min: PDF_MIN_SCALE, max: PDF_MAX_SCALE });
      try {
        const pdfjsLib = await getPdfjs();
        const buffer = await file.arrayBuffer();
        const doc = await pdfjsLib.getDocument({ data: buffer }).promise;
        setPdfDoc(doc);
        setNumPages(doc.numPages);
      } catch (err) {
        console.error(err);
        setLoadError("Impossible de lire ce PDF. Le fichier est-il valide ?");
        setFormat(null);
        return;
      }
    } else {
      setFormat(extension);
      setNumPages(1);
    }

    setSaveStatus("uploading");
    try {
      const drawing = await uploadDrawing(file);
      setDrawingId(drawing.id);

      if (extension === "dxf" || extension === "dwg") {
        const remoteEntities = await fetchDrawingEntities(drawing.id);
        setEntities(remoteEntities);
        setSaveStatus("saved");
        setSaveMessage(`Plan vectoriel analysé — ${remoteEntities.length} entité(s)`);
      } else {
        setSaveStatus("saved");
        setSaveMessage(`Enregistré côté serveur — ${drawing.nbPages} page(s)`);
      }

      try {
        setObjects(await fetchCvcObjects(drawing.id));
      } catch (err) {
        console.error(err);
      }

      try {
        const remoteLayers = await fetchLayers(drawing.id);
        setCvcLayers(remoteLayers);
        setActiveLayerId(remoteLayers[0]?.id ?? null);
      } catch (err) {
        console.error(err);
      }
    } catch (err) {
      console.error(err);
      setSaveStatus("error");
      if (extension === "dxf" || extension === "dwg") {
        // Pas d'aperçu possible sans passer par l'API : elle seule sait lire le DXF/DWG.
        setFormat(null);
        setLoadError(
          err instanceof Error ? err.message : "Le serveur n'a pas pu analyser ce fichier.",
        );
      } else {
        setSaveMessage("Aperçu local uniquement : l'API de persistance n'a pas répondu.");
      }
    }
  }, []);

  const onFileInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) void loadFile(file);
    e.target.value = "";
  };

  const onDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setIsDraggingFile(false);
    const file = e.dataTransfer.files?.[0];
    if (file) void loadFile(file);
  };

  // Rendu de la page PDF courante sur le canvas à chaque changement
  // d'échelle, rotation ou page.
  useEffect(() => {
    if (format !== "pdf" || !pdfDoc || !canvasRef.current) return;
    let cancelled = false;

    (async () => {
      const page = await pdfDoc.getPage(currentPage);
      if (cancelled) return;

      const viewport = page.getViewport({ scale, rotation });
      setPdfViewport(viewport);

      const canvas = canvasRef.current;
      if (!canvas) return;
      const context = canvas.getContext("2d");
      if (!context) return;

      canvas.width = viewport.width;
      canvas.height = viewport.height;
      setCanvasSize({ width: viewport.width, height: viewport.height });

      const task = page.render({ canvasContext: context, viewport });
      renderTaskRef.current = task;
      try {
        await task.promise;
      } catch (err) {
        if (!(err instanceof Error && err.name === "RenderingCancelledException")) {
          console.error(err);
        }
      }
    })();

    return () => {
      cancelled = true;
      renderTaskRef.current?.cancel();
    };
  }, [format, pdfDoc, currentPage, scale, rotation]);

  // Ajustement automatique de l'échelle à l'ouverture d'un plan vectoriel :
  // les unités de dessin DXF/DWG (mm, m, sans unité…) n'ont aucun rapport
  // fixe avec le pixel CSS, contrairement au PDF.
  useEffect(() => {
    if ((format !== "dxf" && format !== "dwg") || !bounds || !scrollRef.current) return;
    const width = bounds.maxX - bounds.minX || 1;
    const height = bounds.maxY - bounds.minY || 1;
    const availableWidth = scrollRef.current.clientWidth - 48;
    const availableHeight = scrollRef.current.clientHeight - 48;
    const fit = Math.min(availableWidth / width, availableHeight / height) * 0.9;
    const safeFit = Number.isFinite(fit) && fit > 0 ? fit : 1;
    setScale(safeFit);
    setScaleBounds({ min: safeFit / 20, max: safeFit * 40 });
  }, [format, bounds]);

  const zoomIn = () => setScale((s) => Math.min(scaleBounds.max, s * ZOOM_STEP));
  const zoomOut = () => setScale((s) => Math.max(scaleBounds.min, s / ZOOM_STEP));

  const fitToView = async () => {
    if (!scrollRef.current) return;
    if (format === "pdf" && pdfDoc) {
      const page = await pdfDoc.getPage(currentPage);
      const baseViewport = page.getViewport({ scale: 1, rotation });
      const available = scrollRef.current.clientWidth - 48;
      setScale(Math.max(scaleBounds.min, Math.min(scaleBounds.max, available / baseViewport.width)));
    } else if ((format === "dxf" || format === "dwg") && bounds) {
      const width = bounds.maxX - bounds.minX || 1;
      const height = bounds.maxY - bounds.minY || 1;
      const availableWidth = scrollRef.current.clientWidth - 48;
      const availableHeight = scrollRef.current.clientHeight - 48;
      const fit = Math.min(availableWidth / width, availableHeight / height) * 0.9;
      if (Number.isFinite(fit) && fit > 0) setScale(fit);
    }
  };

  const rotate = () => setRotation((r) => (r + 90) % 360);
  const goToPage = (n: number) => setCurrentPage(Math.min(numPages, Math.max(1, n)));

  const isInteractiveMode = calibrationMode !== "idle" || !!activeTool;

  // Pan : cliquer-glisser dans la zone de visualisation (désactivé pendant
  // la calibration et le dessin, pour ne pas confondre un déplacement avec
  // un clic de pointage).
  const onPanStart = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!scrollRef.current || isInteractiveMode) return;
    panState.current = {
      x: e.clientX,
      y: e.clientY,
      left: scrollRef.current.scrollLeft,
      top: scrollRef.current.scrollTop,
    };
  };
  const onPanMove = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!panState.current || !scrollRef.current) return;
    const dx = e.clientX - panState.current.x;
    const dy = e.clientY - panState.current.y;
    scrollRef.current.scrollLeft = panState.current.left - dx;
    scrollRef.current.scrollTop = panState.current.top - dy;
  };
  const onPanEnd = () => {
    panState.current = null;
  };

  // --- Module 2 — Calibration ---------------------------------------

  const startCalibration = () => {
    setCalibrationMode("picking-a");
    setPickedPoints({});
    setCalibrationError(null);
    setDistanceInput("");
    setActiveTool(null);
    setDuctDraft({});
  };

  const cancelCalibration = () => {
    setCalibrationMode("idle");
    setPickedPoints({});
    setCalibrationError(null);
  };

  const registerCalibrationPoint = useCallback(
    (point: DrawingPoint) => {
      if (calibrationMode === "picking-a") {
        setPickedPoints({ a: point });
        setCalibrationMode("picking-b");
      } else if (calibrationMode === "picking-b") {
        setPickedPoints((prev) => ({ ...prev, b: point }));
        setCalibrationMode("confirm");
      }
    },
    [calibrationMode],
  );

  const submitCalibration = async () => {
    if (!drawingId || !pickedPoints.a || !pickedPoints.b) return;
    const meters = Number(distanceInput.replace(",", "."));
    if (!Number.isFinite(meters) || meters <= 0) {
      setCalibrationError("Entrez une distance réelle valide, en mètres.");
      return;
    }

    setCalibrationSaving(true);
    setCalibrationError(null);
    try {
      const drawing = await calibrateDrawing(drawingId, {
        pageNumber: currentPage,
        pointA: { x: pickedPoints.a[0], y: pickedPoints.a[1] },
        pointB: { x: pickedPoints.b[0], y: pickedPoints.b[1] },
        realDistanceMeters: meters,
      });
      setCalibration(drawing.calibration);
      setCalibrationMode("idle");
      setPickedPoints({});
    } catch (err) {
      setCalibrationError(err instanceof Error ? err.message : "Échec de la calibration.");
    } finally {
      setCalibrationSaving(false);
    }
  };

  // --- Module 3 — Dessin CVC ------------------------------------------

  const startTool = (type: CvcObjectType) => {
    setActiveTool((prev) => (prev === type ? null : type));
    setDuctDraft({});
    setObjectError(null);
    setCalibrationMode("idle");
    setPickedPoints({});
    setSelectedObjectId(null);
  };

  const cancelTool = () => {
    setActiveTool(null);
    setDuctDraft({});
    setObjectError(null);
  };

  const selectObject = (id: string) => {
    if (isInteractiveMode) return;
    setSelectedObjectId(id);
  };

  /**
   * Ajoute l'objet créé et répercute côté client les connexions mutuelles
   * que le serveur vient d'établir — sans ça, un objet déjà affiché reste
   * bloqué avec sa liste de connexions d'origine tant que le plan n'est
   * pas rechargé.
   */
  const addCreatedObject = useCallback((created: CvcObjectDto) => {
    setObjects((prev) => [
      ...prev.map((o) =>
        created.connectedObjectIds.includes(o.id) && !o.connectedObjectIds.includes(created.id)
          ? { ...o, connectedObjectIds: [...o.connectedObjectIds, created.id] }
          : o,
      ),
      created,
    ]);
  }, []);

  const placePointObject = useCallback(
    async (type: CvcObjectType, point: DrawingPoint) => {
      if (!drawingId || !activeLayerId) return;
      setObjectSaving(true);
      setObjectError(null);
      try {
        const input: CreatePointObjectInput = {
          type: type as CreatePointObjectInput["type"],
          layerId: activeLayerId,
          position: { x: point[0], y: point[1] },
        };
        const created = await createCvcObject(drawingId, input);
        addCreatedObject(created);
      } catch (err) {
        setObjectError(err instanceof Error ? err.message : "Échec de la création de l'objet.");
      } finally {
        setObjectSaving(false);
      }
    },
    [drawingId, activeLayerId, addCreatedObject],
  );

  const submitDuct = async () => {
    if (!drawingId || !activeLayerId || !ductDraft.start || !ductDraft.end || !activeTool) return;
    const isRect = activeTool === "GaineRectangulaire";

    let widthMm: number | undefined;
    let heightMm: number | undefined;
    let diameterMm: number | undefined;

    if (isRect) {
      widthMm = Number(ductWidthInput.replace(",", "."));
      heightMm = Number(ductHeightInput.replace(",", "."));
      if (!Number.isFinite(widthMm) || widthMm <= 0 || !Number.isFinite(heightMm) || heightMm <= 0) {
        setObjectError("Largeur et hauteur doivent être des nombres positifs (mm).");
        return;
      }
    } else {
      diameterMm = Number(ductDiameterInput.replace(",", "."));
      if (!Number.isFinite(diameterMm) || diameterMm <= 0) {
        setObjectError("Le diamètre doit être un nombre positif (mm).");
        return;
      }
    }

    const parseOptional = (raw: string) => {
      if (!raw.trim()) return undefined;
      const n = Number(raw.replace(",", "."));
      return Number.isFinite(n) && n > 0 ? n : undefined;
    };

    setObjectSaving(true);
    setObjectError(null);
    try {
      const input: CreateDuctInput = {
        type: activeTool as CreateDuctInput["type"],
        layerId: activeLayerId,
        start: { x: ductDraft.start[0], y: ductDraft.start[1] },
        end: { x: ductDraft.end[0], y: ductDraft.end[1] },
        widthMm,
        heightMm,
        diameterMm,
        debitM3h: parseOptional(ductDebitInput),
        vitesseMs: parseOptional(ductVitesseInput),
        pressionPa: parseOptional(ductPressionInput),
      };
      const created = await createCvcObject(drawingId, input);
      addCreatedObject(created);
      setDuctDraft({});
      setDuctDebitInput("");
      setDuctVitesseInput("");
      setDuctPressionInput("");
    } catch (err) {
      setObjectError(err instanceof Error ? err.message : "Échec de la création de la gaine.");
    } finally {
      setObjectSaving(false);
    }
  };

  const deleteSelected = async () => {
    if (!drawingId || !selectedObjectId) return;
    setDeletingObject(true);
    try {
      await deleteCvcObject(drawingId, selectedObjectId);
      const removedId = selectedObjectId;
      setObjects((prev) =>
        prev
          .filter((o) => o.id !== removedId)
          .map((o) => ({ ...o, connectedObjectIds: o.connectedObjectIds.filter((id) => id !== removedId) })),
      );
      setSelectedObjectId(null);
    } catch (err) {
      setObjectError(err instanceof Error ? err.message : "Échec de la suppression.");
    } finally {
      setDeletingObject(false);
    }
  };

  // --- Calques -----------------------------------------------------------

  const addLayer = async () => {
    if (!drawingId) return;
    setLayersBusy(true);
    setLayersError(null);
    try {
      const layer = await createLayer(drawingId);
      setCvcLayers((prev) => [...prev, layer]);
      setActiveLayerId(layer.id);
    } catch (err) {
      setLayersError(err instanceof Error ? err.message : "Échec de la création du calque.");
    } finally {
      setLayersBusy(false);
    }
  };

  const patchLayer = async (layer: LayerDto, patch: { name?: string; visible?: boolean; locked?: boolean }) => {
    if (!drawingId) return;
    setLayersError(null);
    try {
      const updated = await updateLayer(drawingId, layer.id, patch);
      setCvcLayers((prev) => prev.map((l) => (l.id === updated.id ? updated : l)));
    } catch (err) {
      setLayersError(err instanceof Error ? err.message : "Échec de la mise à jour du calque.");
    }
  };

  const removeLayer = async (layer: LayerDto) => {
    if (!drawingId) return;
    setLayersBusy(true);
    setLayersError(null);
    try {
      await deleteLayer(drawingId, layer.id);
      setCvcLayers((prev) => prev.filter((l) => l.id !== layer.id));
      // Les objets du calque supprimé sont réaffectés côté serveur au premier
      // calque restant : on recharge les objets pour refléter leur nouveau LayerId.
      setObjects(await fetchCvcObjects(drawingId));
      setActiveLayerId((prev) => (prev === layer.id ? (cvcLayers.find((l) => l.id !== layer.id)?.id ?? null) : prev));
    } catch (err) {
      setLayersError(err instanceof Error ? err.message : "Échec de la suppression du calque.");
    } finally {
      setLayersBusy(false);
    }
  };

  const activeLayer = cvcLayers.find((l) => l.id === activeLayerId) ?? null;
  const layerById = useMemo(() => new Map(cvcLayers.map((l) => [l.id, l])), [cvcLayers]);
  const visibleObjects = useMemo(
    () => objects.filter((o) => layerById.get(o.layerId)?.visible !== false),
    [objects, layerById],
  );

  /** Point de fond de plan cliqué : calibration, pose d'un objet CVC (avec accrochage), ou désélection. */
  const handleBackgroundPoint = useCallback(
    (rawPoint: DrawingPoint, toleranceUnits: number) => {
      if (calibrationMode === "picking-a" || calibrationMode === "picking-b") {
        registerCalibrationPoint(rawPoint);
        return;
      }

      if (activeTool && !objectSaving) {
        const snapped = findSnapTarget(rawPoint, visibleObjects, toleranceUnits) ?? rawPoint;
        if (DUCT_TYPES.has(activeTool)) {
          setDuctDraft((prev) => (!prev.start ? { start: snapped } : { ...prev, end: snapped }));
        } else {
          void placePointObject(activeTool, snapped);
        }
        return;
      }

      setSelectedObjectId(null);
    },
    [calibrationMode, activeTool, objectSaving, visibleObjects, placePointObject, registerCalibrationPoint],
  );

  const onPdfBackgroundClick = (e: React.MouseEvent<SVGRectElement>) => {
    const viewport = pdfViewport;
    if (!viewport) return;
    const rect = e.currentTarget.getBoundingClientRect();
    const toPdf = (cx: number, cy: number) => viewport.convertToPdfPoint(cx - rect.left, cy - rect.top) as DrawingPoint;
    const p = toPdf(e.clientX, e.clientY);
    const pTol = toPdf(e.clientX + 12, e.clientY);
    handleBackgroundPoint(p, Math.hypot(pTol[0] - p[0], pTol[1] - p[1]));
  };

  // Les plans vectoriels n'ont pas besoin de conversion pdf.js : un clic se
  // convertit directement en coordonnées de dessin via la matrice courante
  // du SVG (getScreenCTM), correcte quels que soient le zoom et la rotation.
  const onCadBackgroundClick = (e: React.MouseEvent<SVGRectElement>) => {
    const ctm = svgRef.current?.getScreenCTM();
    if (!ctm) return;
    const inverse = ctm.inverse();
    const toDrawing = (cx: number, cy: number): DrawingPoint => {
      const local = new DOMPoint(cx, cy).matrixTransform(inverse);
      return [local.x, -local.y];
    };
    const p = toDrawing(e.clientX, e.clientY);
    const pTol = toDrawing(e.clientX + 12, e.clientY);
    handleBackgroundPoint(p, Math.hypot(pTol[0] - p[0], pTol[1] - p[1]));
  };

  const pdfProject: Project = useCallback(
    (x, y) => (pdfViewport ? (pdfViewport.convertToViewportPoint(x, y) as DrawingPoint) : [x, y]),
    [pdfViewport],
  );
  const cadProject: Project = useCallback((x, y) => [x, -y], []);

  // Coordonnées canvas (dépendantes du zoom/rotation courants) des points
  // en cours de pointage et de la calibration déjà enregistrée — PDF
  // uniquement, puisque la calibration d'un plan vectoriel se dessine
  // directement dans son propre repère (voir <CalibrationOverlay flipY>).
  const pdfOverlay = useMemo(() => {
    if (format !== "pdf" || !pdfViewport) return null;
    return {
      pickA: pickedPoints.a ? pdfProject(...pickedPoints.a) : null,
      pickB: pickedPoints.b ? pdfProject(...pickedPoints.b) : null,
      calibA:
        calibration && calibration.pageNumber === currentPage
          ? pdfProject(calibration.pointA.x, calibration.pointA.y)
          : null,
      calibB:
        calibration && calibration.pageNumber === currentPage
          ? pdfProject(calibration.pointB.x, calibration.pointB.y)
          : null,
      ductStart: ductDraft.start ? pdfProject(...ductDraft.start) : null,
    };
  }, [format, pdfViewport, pickedPoints, calibration, currentPage, ductDraft, pdfProject]);

  const hasContent = format === "pdf" ? !!pdfDoc : format === "dxf" || format === "dwg" ? !!entities : false;
  const isProcessingCad = (format === "dxf" || format === "dwg") && !entities && saveStatus !== "error";
  const cadViewport = format !== "pdf" && bounds ? computeCadViewport(bounds, scale) : null;
  const markerSize = format === "pdf" ? 12 : bounds ? Math.max(bounds.maxX - bounds.minX, bounds.maxY - bounds.minY) * 0.02 : 100;
  const selectedObject = objects.find((o) => o.id === selectedObjectId) ?? null;
  const selectedObjectLayer = selectedObject ? (layerById.get(selectedObject.layerId) ?? null) : null;
  const cursor = !hasContent ? "default" : isInteractiveMode ? "crosshair" : "grab";
  const drawingToolsDisabled = !calibration || objectSaving || !activeLayerId || !!activeLayer?.locked;

  return (
    <div className="flex h-full flex-col overflow-hidden rounded-lg border border-slate-700 bg-slate-900">
      <Toolbar
        hasDocument={hasContent}
        format={format}
        fileName={fileName}
        scale={scale}
        onImportClick={() => document.getElementById("plan-file-input")?.click()}
        onZoomIn={zoomIn}
        onZoomOut={zoomOut}
        onFitWidth={fitToView}
        onRotate={rotate}
        currentPage={currentPage}
        numPages={numPages}
        onPrevPage={() => goToPage(currentPage - 1)}
        onNextPage={() => goToPage(currentPage + 1)}
        onPageInput={goToPage}
        layerVisible={layerVisible}
        onToggleLayer={() => setLayerVisible((v) => !v)}
        layerOpacity={layerOpacity}
        onLayerOpacity={setLayerOpacity}
        saveStatus={saveStatus}
        saveMessage={saveMessage}
        calibration={calibration}
        calibrationMode={calibrationMode}
        canCalibrate={!!drawingId}
        onStartCalibration={startCalibration}
        onCancelCalibration={cancelCalibration}
      />

      {calibrationMode === "confirm" && (
        <CalibrationBar
          saving={calibrationSaving}
          error={calibrationError}
          distanceInput={distanceInput}
          onDistanceChange={setDistanceInput}
          onConfirm={submitCalibration}
          onCancel={cancelCalibration}
        />
      )}
      {(calibrationMode === "picking-a" || calibrationMode === "picking-b") && (
        <div className="flex items-center gap-2 border-b border-sky-800 bg-sky-950/40 px-3 py-1.5 text-xs text-sky-300">
          <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-sky-400" />
          {calibrationMode === "picking-a"
            ? "Cliquez le premier point d'une cote connue sur le plan (ex. un coin de mur)."
            : "Cliquez le second point de cette même cote."}
          <button type="button" onClick={cancelCalibration} className="ml-auto text-sky-400 underline">
            Annuler
          </button>
        </div>
      )}

      {activeTool && DUCT_TYPES.has(activeTool) && !ductDraft.end && (
        <div className="flex items-center gap-2 border-b border-teal-800 bg-teal-950/30 px-3 py-1.5 text-xs text-teal-300">
          <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-teal-400" />
          {!ductDraft.start ? "Cliquez le point de départ de la gaine." : "Cliquez le point d'arrivée."}
          <button type="button" onClick={cancelTool} className="ml-auto text-teal-400 underline">
            Annuler
          </button>
        </div>
      )}
      {activeTool && !DUCT_TYPES.has(activeTool) && (
        <div className="flex items-center gap-2 border-b border-teal-800 bg-teal-950/30 px-3 py-1.5 text-xs text-teal-300">
          <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-teal-400" />
          Cliquez sur le plan pour poser un{CVC_TYPE_LABELS[activeTool].match(/^[aeiouéè]/i) ? "" : "e"} {CVC_TYPE_LABELS[activeTool].toLowerCase()}.
          {objectSaving && <span>Enregistrement…</span>}
          <button type="button" onClick={cancelTool} className="ml-auto text-teal-400 underline">
            Terminer
          </button>
        </div>
      )}
      {ductDraft.start && ductDraft.end && activeTool && (
        <DuctDimensionsBar
          isCircular={activeTool === "GaineCirculaire"}
          widthInput={ductWidthInput}
          heightInput={ductHeightInput}
          diameterInput={ductDiameterInput}
          debitInput={ductDebitInput}
          vitesseInput={ductVitesseInput}
          pressionInput={ductPressionInput}
          onWidthChange={setDuctWidthInput}
          onHeightChange={setDuctHeightInput}
          onDiameterChange={setDuctDiameterInput}
          onDebitChange={setDuctDebitInput}
          onVitesseChange={setDuctVitesseInput}
          onPressionChange={setDuctPressionInput}
          saving={objectSaving}
          error={objectError}
          onConfirm={submitDuct}
          onCancel={() => setDuctDraft({})}
        />
      )}
      {objectError && !ductDraft.start && (
        <div className="border-b border-rose-900 bg-rose-950/30 px-3 py-1.5 text-xs text-rose-300">{objectError}</div>
      )}

      <input
        id="plan-file-input"
        type="file"
        accept=".pdf,.dxf,.dwg,.ifc,.rvt"
        className="hidden"
        onChange={onFileInputChange}
      />

      <div className="flex min-h-0 flex-1">
        <ToolDock
          activeTool={activeTool}
          onSelect={startTool}
          disabled={drawingToolsDisabled}
          disabledReason={
            !calibration
              ? "Calibrez le plan avant de dessiner (module 2)"
              : activeLayer?.locked
                ? `Le calque « ${activeLayer.name} » est verrouillé`
                : !activeLayerId
                  ? "Créez ou sélectionnez un calque"
                  : undefined
          }
        />

        <div
          ref={scrollRef}
          className="relative flex-1 overflow-auto bg-slate-950"
          onDragOver={(e) => {
            e.preventDefault();
            setIsDraggingFile(true);
          }}
          onDragLeave={() => setIsDraggingFile(false)}
          onDrop={onDrop}
          onMouseDown={onPanStart}
          onMouseMove={onPanMove}
          onMouseUp={onPanEnd}
          onMouseLeave={onPanEnd}
          style={{ cursor }}
        >
          {!hasContent && !isProcessingCad && (
            <button
              type="button"
              onClick={() => document.getElementById("plan-file-input")?.click()}
              className={`flex h-full w-full flex-col items-center justify-center gap-3 border-2 border-dashed text-slate-400 transition-colors ${
                isDraggingFile
                  ? "border-sky-400 bg-sky-950/30 text-sky-300"
                  : "border-slate-700 hover:border-slate-500 hover:text-slate-300"
              }`}
            >
              <span className="text-4xl">⇪</span>
              <span className="font-medium">
                Glissez un plan (PDF, DXF, DWG) ici, ou cliquez pour l&apos;importer
              </span>
              <span className="text-xs text-slate-500">
                IFC et Revit (.rvt)&nbsp;: exportez d&apos;abord en DXF ou PDF depuis votre logiciel.
              </span>
              {loadError && <span className="text-sm text-rose-400">{loadError}</span>}
            </button>
          )}

          {isProcessingCad && (
            <div className="flex h-full w-full flex-col items-center justify-center gap-3 text-slate-400">
              <span className="h-6 w-6 animate-spin rounded-full border-2 border-slate-600 border-t-sky-400" />
              <span>Analyse du plan {format?.toUpperCase()} en cours…</span>
            </div>
          )}

          {format === "pdf" && pdfDoc && (
            <div className="flex min-h-full min-w-full items-center justify-center p-6">
              <div className="relative" style={{ lineHeight: 0 }}>
                <canvas
                  ref={canvasRef}
                  className="bg-white shadow-xl"
                  style={{
                    opacity: layerVisible ? layerOpacity / 100 : 0,
                    transition: "opacity 120ms ease",
                  }}
                />
                {(canvasSize.width > 0 || canvasSize.height > 0) && (
                  <svg
                    className="absolute inset-0"
                    width={canvasSize.width}
                    height={canvasSize.height}
                    viewBox={`0 0 ${canvasSize.width} ${canvasSize.height}`}
                  >
                    <rect
                      x={0}
                      y={0}
                      width={canvasSize.width}
                      height={canvasSize.height}
                      fill="transparent"
                      onClick={onPdfBackgroundClick}
                    />
                    <CvcObjectsLayer
                      objects={visibleObjects}
                      selectedObjectId={selectedObjectId}
                      onSelect={selectObject}
                      project={pdfProject}
                      markerSize={markerSize}
                    />
                    {pdfOverlay?.ductStart && (
                      <circle
                        cx={pdfOverlay.ductStart[0]}
                        cy={pdfOverlay.ductStart[1]}
                        r={5}
                        fill="#0d9488"
                        stroke="white"
                        strokeWidth={1.5}
                        vectorEffect="non-scaling-stroke"
                        style={{ pointerEvents: "none" }}
                      />
                    )}
                    <g style={{ pointerEvents: "none" }}>
                      {pdfOverlay && (
                        <CalibrationOverlay
                          pickA={pdfOverlay.pickA}
                          pickB={pdfOverlay.pickB}
                          calibA={pdfOverlay.calibA}
                          calibB={pdfOverlay.calibB}
                          realDistanceMeters={calibration?.realDistanceMeters}
                        />
                      )}
                    </g>
                  </svg>
                )}
              </div>
            </div>
          )}

          {format !== "pdf" && entities && cadViewport && (
            <div className="flex min-h-full min-w-full items-center justify-center p-6">
              <div style={{ transform: rotation ? `rotate(${rotation}deg)` : undefined }}>
                <svg
                  ref={svgRef}
                  data-testid="plan-svg"
                  width={cadViewport.renderedWidth}
                  height={cadViewport.renderedHeight}
                  viewBox={cadViewport.viewBox}
                  className="shadow-xl"
                  style={{
                    background: "white",
                    opacity: layerVisible ? layerOpacity / 100 : 0,
                    transition: "opacity 120ms ease",
                  }}
                >
                  <rect
                    x={cadViewport.minX}
                    y={cadViewport.minY}
                    width={cadViewport.naturalWidth}
                    height={cadViewport.naturalHeight}
                    fill="transparent"
                    onClick={onCadBackgroundClick}
                  />
                  {entities.map((entity, i) => (
                    <PlanEntityShape key={i} entity={entity} />
                  ))}
                  <CvcObjectsLayer
                    objects={visibleObjects}
                    selectedObjectId={selectedObjectId}
                    onSelect={selectObject}
                    project={cadProject}
                    markerSize={markerSize}
                  />
                  {ductDraft.start && (
                    <circle
                      cx={ductDraft.start[0]}
                      cy={-ductDraft.start[1]}
                      r={markerSize * 0.25}
                      fill="#0d9488"
                      stroke="white"
                      strokeWidth={1.5}
                      vectorEffect="non-scaling-stroke"
                      style={{ pointerEvents: "none" }}
                    />
                  )}
                  <g style={{ pointerEvents: "none" }}>
                    <CalibrationOverlay
                      flipY
                      pickA={pickedPoints.a ?? null}
                      pickB={pickedPoints.b ?? null}
                      calibA={calibration ? [calibration.pointA.x, calibration.pointA.y] : null}
                      calibB={calibration ? [calibration.pointB.x, calibration.pointB.y] : null}
                      realDistanceMeters={calibration?.realDistanceMeters}
                    />
                  </g>
                </svg>
              </div>
            </div>
          )}
        </div>

        {hasContent && drawingId && (
          <div className="flex w-56 flex-shrink-0 flex-col divide-y divide-slate-700 overflow-y-auto border-l border-slate-700 bg-slate-800">
            <LayersPanel
              layers={cvcLayers}
              activeLayerId={activeLayerId}
              onSelectActive={setActiveLayerId}
              onAdd={addLayer}
              onToggleVisible={(l) => patchLayer(l, { visible: !l.visible })}
              onToggleLocked={(l) => patchLayer(l, { locked: !l.locked })}
              onRename={(l, name) => patchLayer(l, { name })}
              onRemove={removeLayer}
              busy={layersBusy}
              error={layersError}
            />
            {selectedObject && (
              <PropertiesPanel
                object={selectedObject}
                layer={selectedObjectLayer}
                onDelete={deleteSelected}
                deleting={deletingObject}
              />
            )}
          </div>
        )}
      </div>
    </div>
  );
}

function ToolDock({
  activeTool,
  onSelect,
  disabled,
  disabledReason,
}: {
  activeTool: CvcObjectType | null;
  onSelect: (type: CvcObjectType) => void;
  disabled: boolean;
  disabledReason?: string;
}) {
  return (
    <div className="flex w-16 flex-shrink-0 flex-col items-center gap-1.5 overflow-y-auto border-r border-slate-700 bg-slate-800 py-3">
      {TOOLS.map((tool) => (
        <button
          key={tool.type}
          type="button"
          data-testid={`tool-${tool.type}`}
          disabled={disabled}
          onClick={() => onSelect(tool.type)}
          title={disabled ? (disabledReason ?? "Indisponible") : CVC_TYPE_LABELS[tool.type]}
          className={`flex h-11 w-13 flex-col items-center justify-center rounded text-[9.5px] leading-none transition-colors disabled:cursor-not-allowed disabled:opacity-30 ${
            activeTool === tool.type
              ? "bg-teal-600 text-white"
              : "bg-slate-900 text-slate-300 hover:bg-slate-700"
          }`}
        >
          <span className="text-base">{tool.icon}</span>
          <span className="mt-0.5">{tool.short}</span>
        </button>
      ))}
    </div>
  );
}

function PropertiesPanel({
  object,
  layer,
  onDelete,
  deleting,
}: {
  object: CvcObjectDto;
  layer: LayerDto | null;
  onDelete: () => void;
  deleting: boolean;
}) {
  const isDuct = !!object.start && !!object.end;
  const locked = !!layer?.locked;

  return (
    <div className="flex flex-col gap-3 p-3 text-xs text-slate-300">
      <div className="text-[11px] font-semibold uppercase tracking-wide text-slate-400">Propriétés</div>
      <PropertyField label="Identifiant" value={object.id.slice(0, 8)} />
      <PropertyField label="Type" value={CVC_TYPE_LABELS[object.type]} />
      <PropertyField label="Calque" value={layer?.name ?? "—"} />
      {isDuct ? (
        <>
          <PropertyField
            label="Dimensions"
            value={
              object.type === "GaineRectangulaire"
                ? `${object.widthMm ?? "—"} × ${object.heightMm ?? "—"} mm`
                : `⌀ ${object.diameterMm ?? "—"} mm`
            }
          />
          <PropertyField label="Longueur" value={object.lengthMeters != null ? `${object.lengthMeters.toFixed(2)} m` : "—"} />
          <PropertyField label="Surface calorifuge" value={object.insulationAreaM2 != null ? `${object.insulationAreaM2.toFixed(2)} m²` : "—"} />
          <PropertyField label="Poids" value={object.weightKg != null ? `${object.weightKg.toFixed(1)} kg` : "—"} />
        </>
      ) : (
        <PropertyField
          label="Position"
          value={object.position ? `${object.position.x.toFixed(0)}, ${object.position.y.toFixed(0)}` : "—"}
        />
      )}
      <PropertyField label="Débit" value={object.debitM3h != null ? `${object.debitM3h} m³/h` : "—"} />
      <PropertyField label="Vitesse" value={object.vitesseMs != null ? `${object.vitesseMs} m/s` : "—"} />
      <PropertyField label="Pression" value={object.pressionPa != null ? `${object.pressionPa} Pa` : "—"} />
      <PropertyField label="Connexions" value={String(object.connectedObjectIds.length)} />
      <button
        type="button"
        onClick={onDelete}
        disabled={deleting || locked}
        title={locked ? `Le calque « ${layer?.name} » est verrouillé` : undefined}
        className="mt-2 rounded border border-rose-800 bg-rose-950/40 px-2 py-1.5 font-medium text-rose-300 hover:bg-rose-950/70 disabled:opacity-50"
      >
        {locked ? "🔒 Calque verrouillé" : deleting ? "Suppression…" : "✕ Supprimer"}
      </button>
    </div>
  );
}

function LayersPanel({
  layers,
  activeLayerId,
  onSelectActive,
  onAdd,
  onToggleVisible,
  onToggleLocked,
  onRename,
  onRemove,
  busy,
  error,
}: {
  layers: LayerDto[];
  activeLayerId: string | null;
  onSelectActive: (id: string) => void;
  onAdd: () => void;
  onToggleVisible: (layer: LayerDto) => void;
  onToggleLocked: (layer: LayerDto) => void;
  onRename: (layer: LayerDto, name: string) => void;
  onRemove: (layer: LayerDto) => void;
  busy: boolean;
  error: string | null;
}) {
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingName, setEditingName] = useState("");

  const startEditing = (layer: LayerDto) => {
    setEditingId(layer.id);
    setEditingName(layer.name);
  };

  const commitEditing = (layer: LayerDto) => {
    const trimmed = editingName.trim();
    if (trimmed && trimmed !== layer.name) onRename(layer, trimmed);
    setEditingId(null);
  };

  return (
    <div className="flex flex-col gap-2 p-3 text-xs text-slate-300">
      <div className="flex items-center justify-between">
        <span className="text-[11px] font-semibold uppercase tracking-wide text-slate-400">Calques</span>
        <button
          type="button"
          onClick={onAdd}
          disabled={busy}
          title="Ajouter un calque"
          className="rounded border border-slate-600 px-1.5 py-0.5 font-medium text-slate-300 hover:border-sky-500 hover:text-sky-300 disabled:opacity-40"
        >
          +
        </button>
      </div>
      <ul className="flex flex-col gap-1">
        {layers.map((layer) => {
          const isActive = layer.id === activeLayerId;
          return (
            <li
              key={layer.id}
              data-testid={`layer-row-${layer.name}`}
              onClick={() => onSelectActive(layer.id)}
              className={`flex items-center gap-1.5 rounded border px-1.5 py-1 ${
                isActive ? "border-teal-600 bg-teal-950/30" : "border-slate-700 hover:border-slate-600"
              }`}
            >
              <button
                type="button"
                data-testid="layer-toggle-visible"
                onClick={(e) => {
                  e.stopPropagation();
                  onToggleVisible(layer);
                }}
                title={layer.visible ? "Masquer le calque" : "Afficher le calque"}
                className="w-4 flex-shrink-0 text-slate-300 hover:text-sky-300"
              >
                {layer.visible ? "👁" : "—"}
              </button>
              <button
                type="button"
                data-testid="layer-toggle-locked"
                onClick={(e) => {
                  e.stopPropagation();
                  onToggleLocked(layer);
                }}
                title={layer.locked ? "Déverrouiller le calque" : "Verrouiller le calque"}
                className={`w-4 flex-shrink-0 ${layer.locked ? "text-amber-400" : "text-slate-500 hover:text-amber-300"}`}
              >
                {layer.locked ? "🔒" : "🔓"}
              </button>
              {editingId === layer.id ? (
                <input
                  autoFocus
                  value={editingName}
                  onChange={(e) => setEditingName(e.target.value)}
                  onBlur={() => commitEditing(layer)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") commitEditing(layer);
                    if (e.key === "Escape") setEditingId(null);
                  }}
                  onClick={(e) => e.stopPropagation()}
                  className="min-w-0 flex-1 rounded border border-slate-600 bg-slate-900 px-1 py-0.5 text-slate-100"
                />
              ) : (
                <span
                  onDoubleClick={(e) => {
                    e.stopPropagation();
                    startEditing(layer);
                  }}
                  className={`min-w-0 flex-1 truncate ${isActive ? "text-teal-200" : "text-slate-300"}`}
                  title="Double-cliquer pour renommer"
                >
                  {layer.name}
                </span>
              )}
              <button
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  onRemove(layer);
                }}
                disabled={busy || layers.length <= 1}
                title={layers.length <= 1 ? "Impossible de supprimer le dernier calque" : "Supprimer le calque"}
                className="flex-shrink-0 text-slate-500 hover:text-rose-400 disabled:cursor-not-allowed disabled:opacity-30"
              >
                ✕
              </button>
            </li>
          );
        })}
      </ul>
      {error && <span className="text-rose-400">{error}</span>}
    </div>
  );
}

function PropertyField({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between border-b border-slate-700 pb-1.5">
      <span className="text-slate-500">{label}</span>
      <span className="font-mono text-slate-200">{value}</span>
    </div>
  );
}

function CvcObjectsLayer({
  objects,
  selectedObjectId,
  onSelect,
  project,
  markerSize,
}: {
  objects: CvcObjectDto[];
  selectedObjectId: string | null;
  onSelect: (id: string) => void;
  project: Project;
  markerSize: number;
}) {
  return (
    <>
      {objects.map((obj) => {
        const selected = obj.id === selectedObjectId;
        const color = selected ? "#f59e0b" : "#0d9488";
        const onClick = (e: React.MouseEvent) => {
          e.stopPropagation();
          onSelect(obj.id);
        };

        if (obj.start && obj.end) {
          const [x1, y1] = project(obj.start.x, obj.start.y);
          const [x2, y2] = project(obj.end.x, obj.end.y);
          const isCircular = obj.type === "GaineCirculaire";
          return (
            <g key={obj.id} onClick={onClick} style={{ cursor: "pointer" }}>
              {/* zone de clic élargie, invisible : une gaine à 3px est difficile à viser précisément */}
              <line x1={x1} y1={y1} x2={x2} y2={y2} stroke="transparent" strokeWidth={16} vectorEffect="non-scaling-stroke" />
              <line x1={x1} y1={y1} x2={x2} y2={y2} stroke={color} strokeWidth={selected ? 4 : 3} vectorEffect="non-scaling-stroke" />
              {isCircular && (
                <>
                  <circle cx={x1} cy={y1} r={markerSize * 0.22} fill={color} />
                  <circle cx={x2} cy={y2} r={markerSize * 0.22} fill={color} />
                </>
              )}
            </g>
          );
        }

        if (obj.position) {
          const [x, y] = project(obj.position.x, obj.position.y);
          const size = obj.type === "Cta" ? markerSize * 1.8 : markerSize;
          const isBouche = obj.type === "Bouche";
          return (
            <g
              key={obj.id}
              onClick={onClick}
              style={{ cursor: "pointer" }}
              transform={`translate(${x} ${y}) rotate(${(obj.rotationRad * 180) / Math.PI})`}
            >
              <rect
                x={-size / 2}
                y={-size / 2}
                width={size}
                height={size}
                fill={isBouche ? "white" : color}
                fillOpacity={isBouche ? 0 : 0.85}
                stroke={color}
                strokeWidth={selected ? 2.5 : 1.5}
                vectorEffect="non-scaling-stroke"
              />
              <text
                x={0}
                y={1}
                textAnchor="middle"
                dominantBaseline="middle"
                fontSize={size * 0.4}
                fill={isBouche ? color : "white"}
                style={{ pointerEvents: "none" }}
              >
                {POINT_OBJECT_LABELS[obj.type]}
              </text>
            </g>
          );
        }

        return null;
      })}
    </>
  );
}

function DuctDimensionsBar({
  isCircular,
  widthInput,
  heightInput,
  diameterInput,
  debitInput,
  vitesseInput,
  pressionInput,
  onWidthChange,
  onHeightChange,
  onDiameterChange,
  onDebitChange,
  onVitesseChange,
  onPressionChange,
  saving,
  error,
  onConfirm,
  onCancel,
}: {
  isCircular: boolean;
  widthInput: string;
  heightInput: string;
  diameterInput: string;
  debitInput: string;
  vitesseInput: string;
  pressionInput: string;
  onWidthChange: (v: string) => void;
  onHeightChange: (v: string) => void;
  onDiameterChange: (v: string) => void;
  onDebitChange: (v: string) => void;
  onVitesseChange: (v: string) => void;
  onPressionChange: (v: string) => void;
  saving: boolean;
  error: string | null;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <div className="flex flex-wrap items-center gap-2 border-b border-teal-800 bg-teal-950/30 px-3 py-2 text-sm text-teal-200">
      <span className="text-xs">Dimensions de la gaine&nbsp;:</span>
      {isCircular ? (
        <>
          <input
            type="text"
            inputMode="decimal"
            autoFocus
            value={diameterInput}
            onChange={(e) => onDiameterChange(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && onConfirm()}
            placeholder="315"
            className="w-20 rounded border border-teal-700 bg-slate-900 px-2 py-1 text-center font-mono text-teal-100"
          />
          <span className="text-xs">mm ⌀</span>
        </>
      ) : (
        <>
          <input
            type="text"
            inputMode="decimal"
            autoFocus
            value={widthInput}
            onChange={(e) => onWidthChange(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && onConfirm()}
            placeholder="400"
            className="w-16 rounded border border-teal-700 bg-slate-900 px-2 py-1 text-center font-mono text-teal-100"
          />
          <span className="text-xs">×</span>
          <input
            type="text"
            inputMode="decimal"
            value={heightInput}
            onChange={(e) => onHeightChange(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && onConfirm()}
            placeholder="250"
            className="w-16 rounded border border-teal-700 bg-slate-900 px-2 py-1 text-center font-mono text-teal-100"
          />
          <span className="text-xs">mm</span>
        </>
      )}
      <span className="ml-2 text-xs text-teal-400/70">Débit</span>
      <input
        type="text"
        inputMode="decimal"
        value={debitInput}
        onChange={(e) => onDebitChange(e.target.value)}
        onKeyDown={(e) => e.key === "Enter" && onConfirm()}
        placeholder="facultatif"
        className="w-20 rounded border border-teal-700 bg-slate-900 px-2 py-1 text-center font-mono text-teal-100 placeholder:text-teal-100/30"
      />
      <span className="text-xs">m³/h</span>
      <span className="ml-1 text-xs text-teal-400/70">Vitesse</span>
      <input
        type="text"
        inputMode="decimal"
        value={vitesseInput}
        onChange={(e) => onVitesseChange(e.target.value)}
        onKeyDown={(e) => e.key === "Enter" && onConfirm()}
        placeholder="facultatif"
        className="w-20 rounded border border-teal-700 bg-slate-900 px-2 py-1 text-center font-mono text-teal-100 placeholder:text-teal-100/30"
      />
      <span className="text-xs">m/s</span>
      <span className="ml-1 text-xs text-teal-400/70">Pression</span>
      <input
        type="text"
        inputMode="decimal"
        value={pressionInput}
        onChange={(e) => onPressionChange(e.target.value)}
        onKeyDown={(e) => e.key === "Enter" && onConfirm()}
        placeholder="facultatif"
        className="w-20 rounded border border-teal-700 bg-slate-900 px-2 py-1 text-center font-mono text-teal-100 placeholder:text-teal-100/30"
      />
      <span className="text-xs">Pa</span>
      <button
        type="button"
        onClick={onConfirm}
        disabled={saving}
        className="rounded bg-teal-600 px-3 py-1 font-medium text-slate-950 hover:bg-teal-500 disabled:opacity-50"
      >
        {saving ? "Création…" : "Valider"}
      </button>
      <button type="button" onClick={onCancel} className="text-xs text-teal-400 underline">
        Annuler
      </button>
      {error && <span className="text-xs text-rose-400">{error}</span>}
    </div>
  );
}

function PlanEntityShape({ entity }: { entity: PlanEntityDto }) {
  const stroke = "#1f2937";
  const strokeWidth = 1;
  const vectorEffect = "non-scaling-stroke" as const;

  switch (entity.type) {
    case "line": {
      const [a, b] = entity.points;
      return <line x1={a.x} y1={-a.y} x2={b.x} y2={-b.y} stroke={stroke} strokeWidth={strokeWidth} vectorEffect={vectorEffect} />;
    }
    case "polyline": {
      const points = entity.closed ? [...entity.points, entity.points[0]] : entity.points;
      return (
        <polyline
          points={points.map((p) => `${p.x},${-p.y}`).join(" ")}
          fill="none"
          stroke={stroke}
          strokeWidth={strokeWidth}
          vectorEffect={vectorEffect}
        />
      );
    }
    case "circle":
      return (
        <circle cx={entity.center.x} cy={-entity.center.y} r={entity.radius} fill="none" stroke={stroke} strokeWidth={strokeWidth} vectorEffect={vectorEffect} />
      );
    case "arc": {
      const { center, radius, startAngle, endAngle } = entity;
      const startX = center.x + radius * Math.cos(startAngle);
      const startY = center.y + radius * Math.sin(startAngle);
      const endX = center.x + radius * Math.cos(endAngle);
      const endY = center.y + radius * Math.sin(endAngle);
      const span = (((endAngle - startAngle) % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);
      const largeArc = span > Math.PI ? 1 : 0;
      return (
        <path
          d={`M ${startX} ${-startY} A ${radius} ${radius} 0 ${largeArc} 1 ${endX} ${-endY}`}
          fill="none"
          stroke={stroke}
          strokeWidth={strokeWidth}
          vectorEffect={vectorEffect}
        />
      );
    }
    case "text":
      return (
        <text x={entity.position.x} y={-entity.position.y} fontSize={entity.height || 2.5} fill={stroke}>
          {entity.text}
        </text>
      );
  }
}

function CalibrationOverlay({
  pickA,
  pickB,
  calibA,
  calibB,
  realDistanceMeters,
  flipY = false,
}: {
  pickA: DrawingPoint | null;
  pickB: DrawingPoint | null;
  calibA: DrawingPoint | null;
  calibB: DrawingPoint | null;
  realDistanceMeters?: number;
  flipY?: boolean;
}) {
  const y = (p: DrawingPoint) => (flipY ? -p[1] : p[1]);

  return (
    <>
      {calibA && calibB && (
        <g>
          <line
            x1={calibA[0]}
            y1={y(calibA)}
            x2={calibB[0]}
            y2={y(calibB)}
            stroke="#b85c22"
            strokeWidth={2}
            strokeDasharray="6 4"
            vectorEffect="non-scaling-stroke"
          />
          <circle cx={calibA[0]} cy={y(calibA)} r={4} fill="#b85c22" vectorEffect="non-scaling-stroke" />
          <circle cx={calibB[0]} cy={y(calibB)} r={4} fill="#b85c22" vectorEffect="non-scaling-stroke" />
          {realDistanceMeters != null && (
            <text
              x={(calibA[0] + calibB[0]) / 2}
              y={(y(calibA) + y(calibB)) / 2 - 8}
              textAnchor="middle"
              fontSize={13}
              fontFamily="ui-monospace, monospace"
              fill="#b85c22"
              stroke="white"
              strokeWidth={3}
              paintOrder="stroke"
            >
              {distanceFormatter.format(realDistanceMeters)} m
            </text>
          )}
        </g>
      )}
      {pickA && <circle cx={pickA[0]} cy={y(pickA)} r={5} fill="#0ea5e9" stroke="white" strokeWidth={1.5} vectorEffect="non-scaling-stroke" />}
      {pickA && pickB && (
        <line x1={pickA[0]} y1={y(pickA)} x2={pickB[0]} y2={y(pickB)} stroke="#0ea5e9" strokeWidth={2} vectorEffect="non-scaling-stroke" />
      )}
      {pickB && <circle cx={pickB[0]} cy={y(pickB)} r={5} fill="#0ea5e9" stroke="white" strokeWidth={1.5} vectorEffect="non-scaling-stroke" />}
    </>
  );
}

function CalibrationBar({
  saving,
  error,
  distanceInput,
  onDistanceChange,
  onConfirm,
  onCancel,
}: {
  saving: boolean;
  error: string | null;
  distanceInput: string;
  onDistanceChange: (v: string) => void;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <div className="flex flex-wrap items-center gap-2 border-b border-amber-800 bg-amber-950/30 px-3 py-2 text-sm text-amber-200">
      <span className="text-xs">Distance réelle entre les deux points&nbsp;:</span>
      <input
        type="text"
        inputMode="decimal"
        autoFocus
        value={distanceInput}
        onChange={(e) => onDistanceChange(e.target.value)}
        onKeyDown={(e) => e.key === "Enter" && onConfirm()}
        placeholder="10,00"
        className="w-24 rounded border border-amber-700 bg-slate-900 px-2 py-1 text-center font-mono text-amber-100"
      />
      <span className="text-xs">m</span>
      <button
        type="button"
        onClick={onConfirm}
        disabled={saving}
        className="rounded bg-amber-600 px-3 py-1 font-medium text-slate-950 hover:bg-amber-500 disabled:opacity-50"
      >
        {saving ? "Calibration…" : "Valider"}
      </button>
      <button type="button" onClick={onCancel} className="text-xs text-amber-400 underline">
        Annuler
      </button>
      {error && <span className="text-xs text-rose-400">{error}</span>}
    </div>
  );
}

interface ToolbarProps {
  hasDocument: boolean;
  format: PlanFormatState;
  fileName: string | null;
  scale: number;
  onImportClick: () => void;
  onZoomIn: () => void;
  onZoomOut: () => void;
  onFitWidth: () => void;
  onRotate: () => void;
  currentPage: number;
  numPages: number;
  onPrevPage: () => void;
  onNextPage: () => void;
  onPageInput: (n: number) => void;
  layerVisible: boolean;
  onToggleLayer: () => void;
  layerOpacity: number;
  onLayerOpacity: (n: number) => void;
  saveStatus: SaveStatus;
  saveMessage: string | null;
  calibration: CalibrationDto | null;
  calibrationMode: CalibrationMode;
  canCalibrate: boolean;
  onStartCalibration: () => void;
  onCancelCalibration: () => void;
}

function Toolbar(props: ToolbarProps) {
  const {
    hasDocument,
    format,
    fileName,
    scale,
    onImportClick,
    onZoomIn,
    onZoomOut,
    onFitWidth,
    onRotate,
    currentPage,
    numPages,
    onPrevPage,
    onNextPage,
    onPageInput,
    layerVisible,
    onToggleLayer,
    layerOpacity,
    onLayerOpacity,
    saveStatus,
    saveMessage,
    calibration,
    calibrationMode,
    canCalibrate,
    onStartCalibration,
    onCancelCalibration,
  } = props;

  const isCalibrating = calibrationMode !== "idle";

  return (
    <div className="flex flex-wrap items-center gap-2 border-b border-slate-700 bg-slate-800 px-3 py-2 text-sm text-slate-200">
      <ToolbarButton onClick={onImportClick} title="Importer un plan (PDF, DXF, DWG)">
        ⇪ Importer
      </ToolbarButton>

      <div className="mx-1 h-5 w-px bg-slate-700" />

      <ToolbarButton onClick={onZoomOut} disabled={!hasDocument} title="Zoom arrière">
        −
      </ToolbarButton>
      <span className="w-14 text-center font-mono text-xs tabular-nums text-slate-400">
        {Math.round(scale * 100)}%
      </span>
      <ToolbarButton onClick={onZoomIn} disabled={!hasDocument} title="Zoom avant">
        +
      </ToolbarButton>
      <ToolbarButton onClick={onFitWidth} disabled={!hasDocument} title="Ajuster à la vue">
        ⤢ Ajuster
      </ToolbarButton>
      <ToolbarButton onClick={onRotate} disabled={!hasDocument} title="Pivoter de 90°">
        ⟳ Pivoter
      </ToolbarButton>

      <div className="mx-1 h-5 w-px bg-slate-700" />

      {format === "pdf" ? (
        <>
          <ToolbarButton onClick={onPrevPage} disabled={!hasDocument || currentPage <= 1} title="Page précédente">
            ‹
          </ToolbarButton>
          <span className="font-mono text-xs tabular-nums text-slate-400">
            {hasDocument ? (
              <>
                page{" "}
                <input
                  type="number"
                  value={currentPage}
                  min={1}
                  max={numPages}
                  onChange={(e) => onPageInput(Number(e.target.value))}
                  className="w-10 rounded border border-slate-600 bg-slate-900 px-1 text-center"
                />{" "}
                / {numPages}
              </>
            ) : (
              "page — / —"
            )}
          </span>
          <ToolbarButton onClick={onNextPage} disabled={!hasDocument || currentPage >= numPages} title="Page suivante">
            ›
          </ToolbarButton>
        </>
      ) : (
        <span className="font-mono text-xs text-slate-500">
          {hasDocument ? `vue unique · ${format?.toUpperCase()}` : "—"}
        </span>
      )}

      <div className="mx-1 h-5 w-px bg-slate-700" />

      <label className="flex items-center gap-1.5 text-xs text-slate-400">
        <input type="checkbox" checked={layerVisible} onChange={onToggleLayer} disabled={!hasDocument} />
        Calque du plan
      </label>
      <input
        type="range"
        min={0}
        max={100}
        value={layerOpacity}
        onChange={(e) => onLayerOpacity(Number(e.target.value))}
        disabled={!hasDocument || !layerVisible}
        className="w-20 accent-sky-500"
        title="Opacité du fond de plan"
      />

      <div className="mx-1 h-5 w-px bg-slate-700" />

      {isCalibrating ? (
        <ToolbarButton onClick={onCancelCalibration} title="Annuler la calibration">
          ✕ Annuler la calibration
        </ToolbarButton>
      ) : (
        <ToolbarButton
          onClick={onStartCalibration}
          disabled={!hasDocument || !canCalibrate}
          title={
            canCalibrate
              ? "Calibrer l'échelle du plan"
              : "En attente de l'enregistrement du plan côté serveur"
          }
        >
          ◆ {calibration ? "Recalibrer" : "Calibrer"}
        </ToolbarButton>
      )}
      <span className="font-mono text-xs tabular-nums text-slate-400">
        {calibration
          ? `1 px = ${(calibration.metersPerPixel * 1000).toFixed(1)} mm`
          : "non calibré"}
      </span>

      <div className="ml-auto flex items-center gap-2 text-xs">
        {fileName && <span className="max-w-40 truncate text-slate-400">{fileName}</span>}
        {saveStatus !== "idle" && (
          <span
            className={
              saveStatus === "saved"
                ? "text-emerald-400"
                : saveStatus === "error"
                  ? "text-amber-400"
                  : "text-slate-400"
            }
          >
            {saveStatus === "uploading" ? "Enregistrement…" : saveMessage}
          </span>
        )}
      </div>
    </div>
  );
}

function ToolbarButton({
  children,
  onClick,
  disabled,
  title,
}: {
  children: React.ReactNode;
  onClick: () => void;
  disabled?: boolean;
  title: string;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      title={title}
      className="rounded border border-slate-600 bg-slate-900 px-2.5 py-1 font-medium text-slate-200 hover:border-sky-500 hover:text-sky-300 disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:border-slate-600 disabled:hover:text-slate-200"
    >
      {children}
    </button>
  );
}
