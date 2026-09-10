"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { PageViewport, PDFDocumentProxy, RenderTask } from "pdfjs-dist";
import {
  calibrateDrawing,
  uploadDrawing,
  type CalibrationDto,
} from "@/lib/api-client";

const ZOOM_STEP = 1.2;
const MIN_SCALE = 0.2;
const MAX_SCALE = 6;

type SaveStatus = "idle" | "uploading" | "saved" | "error";
type CalibrationMode = "idle" | "picking-a" | "picking-b" | "confirm";
type PdfPoint = [number, number];

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

export function PdfViewer() {
  const [fileName, setFileName] = useState<string | null>(null);
  const [pdfDoc, setPdfDoc] = useState<PDFDocumentProxy | null>(null);
  const [numPages, setNumPages] = useState(0);
  const [currentPage, setCurrentPage] = useState(1);
  const [scale, setScale] = useState(1);
  const [rotation, setRotation] = useState(0);
  const [layerVisible, setLayerVisible] = useState(true);
  const [layerOpacity, setLayerOpacity] = useState(100);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saveStatus, setSaveStatus] = useState<SaveStatus>("idle");
  const [saveMessage, setSaveMessage] = useState<string | null>(null);
  const [isDraggingFile, setIsDraggingFile] = useState(false);
  const [drawingId, setDrawingId] = useState<string | null>(null);

  // Module 2 — calibration.
  const [calibration, setCalibration] = useState<CalibrationDto | null>(null);
  const [calibrationMode, setCalibrationMode] = useState<CalibrationMode>("idle");
  const [pickedPoints, setPickedPoints] = useState<{ a?: PdfPoint; b?: PdfPoint }>({});
  const [distanceInput, setDistanceInput] = useState("");
  const [calibrationSaving, setCalibrationSaving] = useState(false);
  const [calibrationError, setCalibrationError] = useState<string | null>(null);
  const [pdfViewport, setPdfViewport] = useState<PageViewport | null>(null);
  const [canvasSize, setCanvasSize] = useState({ width: 0, height: 0 });

  const canvasRef = useRef<HTMLCanvasElement>(null);
  const scrollRef = useRef<HTMLDivElement>(null);
  const panState = useRef<{ x: number; y: number; left: number; top: number } | null>(null);
  const renderTaskRef = useRef<RenderTask | null>(null);

  const loadFile = useCallback(async (file: File) => {
    setLoadError(null);
    setFileName(file.name);
    setCurrentPage(1);
    setRotation(0);
    setScale(1);
    setCalibration(null);
    setCalibrationMode("idle");
    setPickedPoints({});
    setDrawingId(null);

    try {
      const pdfjsLib = await getPdfjs();
      const buffer = await file.arrayBuffer();
      const doc = await pdfjsLib.getDocument({ data: buffer }).promise;
      setPdfDoc(doc);
      setNumPages(doc.numPages);
    } catch (err) {
      console.error(err);
      setLoadError("Impossible de lire ce PDF. Le fichier est-il valide ?");
      setPdfDoc(null);
      setNumPages(0);
      return;
    }

    setSaveStatus("uploading");
    setSaveMessage(null);
    try {
      const drawing = await uploadDrawing(file);
      setSaveStatus("saved");
      setSaveMessage(`Enregistré côté serveur — ${drawing.nbPages} page(s)`);
      setDrawingId(drawing.id);
    } catch (err) {
      console.error(err);
      setSaveStatus("error");
      setSaveMessage(
        "Aperçu local uniquement : l'API de persistance n'a pas répondu.",
      );
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
    if (file && file.type === "application/pdf") void loadFile(file);
  };

  // Rendu de la page courante sur le canvas à chaque changement d'échelle,
  // rotation ou page.
  useEffect(() => {
    if (!pdfDoc || !canvasRef.current) return;
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

      const task = page.render({
        canvasContext: context,
        viewport,
      });
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
  }, [pdfDoc, currentPage, scale, rotation]);

  const zoomIn = () => setScale((s) => Math.min(MAX_SCALE, s * ZOOM_STEP));
  const zoomOut = () => setScale((s) => Math.max(MIN_SCALE, s / ZOOM_STEP));
  const fitWidth = async () => {
    if (!pdfDoc || !scrollRef.current) return;
    const page = await pdfDoc.getPage(currentPage);
    const baseViewport = page.getViewport({ scale: 1, rotation });
    const available = scrollRef.current.clientWidth - 48;
    setScale(Math.max(MIN_SCALE, Math.min(MAX_SCALE, available / baseViewport.width)));
  };
  const rotate = () => setRotation((r) => (r + 90) % 360);
  const goToPage = (n: number) => setCurrentPage(Math.min(numPages, Math.max(1, n)));

  // Pan : cliquer-glisser dans la zone de visualisation (désactivé pendant
  // la calibration pour ne pas confondre un déplacement avec un clic de
  // pointage).
  const onPanStart = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!scrollRef.current || calibrationMode !== "idle") return;
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
  };

  const cancelCalibration = () => {
    setCalibrationMode("idle");
    setPickedPoints({});
    setCalibrationError(null);
  };

  const onCanvasClick = (e: React.MouseEvent<HTMLCanvasElement>) => {
    if (calibrationMode !== "picking-a" && calibrationMode !== "picking-b") return;
    const viewport = pdfViewport;
    const canvas = canvasRef.current;
    if (!viewport || !canvas) return;

    const rect = canvas.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;
    const [pdfX, pdfY] = viewport.convertToPdfPoint(x, y) as PdfPoint;

    if (calibrationMode === "picking-a") {
      setPickedPoints({ a: [pdfX, pdfY] });
      setCalibrationMode("picking-b");
    } else {
      setPickedPoints((prev) => ({ ...prev, b: [pdfX, pdfY] }));
      setCalibrationMode("confirm");
    }
  };

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

  // Coordonnées canvas (dépendantes du zoom/rotation courants) des points
  // en cours de pointage et de la calibration déjà enregistrée.
  const overlay = useMemo(() => {
    if (!pdfViewport) return null;
    const toCanvas = (pt: PdfPoint) => pdfViewport.convertToViewportPoint(pt[0], pt[1]) as PdfPoint;

    return {
      pickA: pickedPoints.a ? toCanvas(pickedPoints.a) : null,
      pickB: pickedPoints.b ? toCanvas(pickedPoints.b) : null,
      calibA:
        calibration && calibration.pageNumber === currentPage
          ? toCanvas([calibration.pointA.x, calibration.pointA.y])
          : null,
      calibB:
        calibration && calibration.pageNumber === currentPage
          ? toCanvas([calibration.pointB.x, calibration.pointB.y])
          : null,
    };
  }, [pdfViewport, pickedPoints, calibration, currentPage]);

  return (
    <div className="flex h-full flex-col overflow-hidden rounded-lg border border-slate-700 bg-slate-900">
      <Toolbar
        hasDocument={!!pdfDoc}
        fileName={fileName}
        scale={scale}
        onImportClick={() =>
          document.getElementById("pdf-file-input")?.click()
        }
        onZoomIn={zoomIn}
        onZoomOut={zoomOut}
        onFitWidth={fitWidth}
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

      <input
        id="pdf-file-input"
        type="file"
        accept="application/pdf"
        className="hidden"
        onChange={onFileInputChange}
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
        style={{
          cursor: !pdfDoc
            ? "default"
            : calibrationMode === "picking-a" || calibrationMode === "picking-b"
              ? "crosshair"
              : "grab",
        }}
      >
        {!pdfDoc && (
          <button
            type="button"
            onClick={() => document.getElementById("pdf-file-input")?.click()}
            className={`flex h-full w-full flex-col items-center justify-center gap-3 border-2 border-dashed text-slate-400 transition-colors ${
              isDraggingFile
                ? "border-sky-400 bg-sky-950/30 text-sky-300"
                : "border-slate-700 hover:border-slate-500 hover:text-slate-300"
            }`}
          >
            <span className="text-4xl">⇪</span>
            <span className="font-medium">
              Glissez un plan PDF ici, ou cliquez pour l&apos;importer
            </span>
            {loadError && (
              <span className="text-sm text-rose-400">{loadError}</span>
            )}
          </button>
        )}

        {pdfDoc && (
          <div className="flex min-h-full min-w-full items-center justify-center p-6">
            <div className="relative" style={{ lineHeight: 0 }}>
              <canvas
                ref={canvasRef}
                onClick={onCanvasClick}
                className="bg-white shadow-xl"
                style={{
                  opacity: layerVisible ? layerOpacity / 100 : 0,
                  transition: "opacity 120ms ease",
                }}
              />
              {overlay && (canvasSize.width > 0 || canvasSize.height > 0) && (
                <svg
                  className="pointer-events-none absolute inset-0"
                  width={canvasSize.width}
                  height={canvasSize.height}
                  viewBox={`0 0 ${canvasSize.width} ${canvasSize.height}`}
                >
                  <CalibrationOverlay
                    pickA={overlay.pickA}
                    pickB={overlay.pickB}
                    calibA={overlay.calibA}
                    calibB={overlay.calibB}
                    realDistanceMeters={calibration?.realDistanceMeters}
                  />
                </svg>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

function CalibrationOverlay({
  pickA,
  pickB,
  calibA,
  calibB,
  realDistanceMeters,
}: {
  pickA: PdfPoint | null;
  pickB: PdfPoint | null;
  calibA: PdfPoint | null;
  calibB: PdfPoint | null;
  realDistanceMeters?: number;
}) {
  return (
    <>
      {calibA && calibB && (
        <g>
          <line
            x1={calibA[0]}
            y1={calibA[1]}
            x2={calibB[0]}
            y2={calibB[1]}
            stroke="#b85c22"
            strokeWidth={2}
            strokeDasharray="6 4"
          />
          <circle cx={calibA[0]} cy={calibA[1]} r={4} fill="#b85c22" />
          <circle cx={calibB[0]} cy={calibB[1]} r={4} fill="#b85c22" />
          {realDistanceMeters != null && (
            <text
              x={(calibA[0] + calibB[0]) / 2}
              y={(calibA[1] + calibB[1]) / 2 - 8}
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
      {pickA && (
        <circle cx={pickA[0]} cy={pickA[1]} r={5} fill="#0ea5e9" stroke="white" strokeWidth={1.5} />
      )}
      {pickA && pickB && (
        <line x1={pickA[0]} y1={pickA[1]} x2={pickB[0]} y2={pickB[1]} stroke="#0ea5e9" strokeWidth={2} />
      )}
      {pickB && (
        <circle cx={pickB[0]} cy={pickB[1]} r={5} fill="#0ea5e9" stroke="white" strokeWidth={1.5} />
      )}
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
      <ToolbarButton onClick={onImportClick} title="Importer un PDF">
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
      <ToolbarButton onClick={onFitWidth} disabled={!hasDocument} title="Ajuster à la largeur">
        ⤢ Ajuster
      </ToolbarButton>
      <ToolbarButton onClick={onRotate} disabled={!hasDocument} title="Pivoter de 90°">
        ⟳ Pivoter
      </ToolbarButton>

      <div className="mx-1 h-5 w-px bg-slate-700" />

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

      <div className="mx-1 h-5 w-px bg-slate-700" />

      <label className="flex items-center gap-1.5 text-xs text-slate-400">
        <input type="checkbox" checked={layerVisible} onChange={onToggleLayer} disabled={!hasDocument} />
        Calque PDF
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
