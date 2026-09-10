"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import type { PDFDocumentProxy, RenderTask } from "pdfjs-dist";
import { uploadDrawing } from "@/lib/api-client";

const ZOOM_STEP = 1.2;
const MIN_SCALE = 0.2;
const MAX_SCALE = 6;

type SaveStatus = "idle" | "uploading" | "saved" | "error";

async function getPdfjs() {
  const pdfjsLib = await import("pdfjs-dist");
  pdfjsLib.GlobalWorkerOptions.workerSrc = new URL(
    "pdfjs-dist/build/pdf.worker.min.mjs",
    import.meta.url,
  ).toString();
  return pdfjsLib;
}

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

  const canvasRef = useRef<HTMLCanvasElement>(null);
  const viewportRef = useRef<HTMLDivElement>(null);
  const panState = useRef<{ x: number; y: number; left: number; top: number } | null>(null);
  const renderTaskRef = useRef<RenderTask | null>(null);

  const loadFile = useCallback(async (file: File) => {
    setLoadError(null);
    setFileName(file.name);
    setCurrentPage(1);
    setRotation(0);
    setScale(1);

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
      const canvas = canvasRef.current;
      if (!canvas) return;
      const context = canvas.getContext("2d");
      if (!context) return;

      canvas.width = viewport.width;
      canvas.height = viewport.height;

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
    if (!pdfDoc || !viewportRef.current) return;
    const page = await pdfDoc.getPage(currentPage);
    const baseViewport = page.getViewport({ scale: 1, rotation });
    const available = viewportRef.current.clientWidth - 48;
    setScale(Math.max(MIN_SCALE, Math.min(MAX_SCALE, available / baseViewport.width)));
  };
  const rotate = () => setRotation((r) => (r + 90) % 360);
  const goToPage = (n: number) => setCurrentPage(Math.min(numPages, Math.max(1, n)));

  // Pan : cliquer-glisser dans la zone de visualisation.
  const onPanStart = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!viewportRef.current) return;
    panState.current = {
      x: e.clientX,
      y: e.clientY,
      left: viewportRef.current.scrollLeft,
      top: viewportRef.current.scrollTop,
    };
  };
  const onPanMove = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!panState.current || !viewportRef.current) return;
    const dx = e.clientX - panState.current.x;
    const dy = e.clientY - panState.current.y;
    viewportRef.current.scrollLeft = panState.current.left - dx;
    viewportRef.current.scrollTop = panState.current.top - dy;
  };
  const onPanEnd = () => {
    panState.current = null;
  };

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
      />

      <input
        id="pdf-file-input"
        type="file"
        accept="application/pdf"
        className="hidden"
        onChange={onFileInputChange}
      />

      <div
        ref={viewportRef}
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
        style={{ cursor: pdfDoc ? "grab" : "default" }}
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
            <canvas
              ref={canvasRef}
              className="bg-white shadow-xl"
              style={{
                opacity: layerVisible ? layerOpacity / 100 : 0,
                transition: "opacity 120ms ease",
              }}
            />
          </div>
        )}
      </div>
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
  } = props;

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
