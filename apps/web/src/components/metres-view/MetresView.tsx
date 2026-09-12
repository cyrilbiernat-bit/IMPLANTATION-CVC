"use client";

import { useEffect, useState } from "react";
import {
  fetchNomenclature,
  fetchProjectMetres,
  nomenclatureExportUrl,
  type NomenclatureRowDto,
  type ProjectMetresDto,
} from "@/lib/api-client";

const ACCESSORY_LABELS: Record<string, string> = {
  Coude: "Coudes",
  Te: "Tés",
  Reduction: "Réductions",
  Bouche: "Bouches",
  Diffuseur: "Diffuseurs",
  Extracteur: "Extracteurs",
  Cta: "CTA",
};

const TYPE_LABELS: Record<string, string> = {
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

const numberFormatter = new Intl.NumberFormat("fr-FR", { maximumFractionDigits: 1 });

function dimensionsLabel(row: NomenclatureRowDto): string {
  if (row.diameterMm != null) return `⌀${numberFormatter.format(row.diameterMm)} mm`;
  if (row.widthMm != null && row.heightMm != null) {
    return `${numberFormatter.format(row.widthMm)}×${numberFormatter.format(row.heightMm)} mm`;
  }
  return "—";
}

export function MetresView({ projectId }: { projectId: string }) {
  const [metres, setMetres] = useState<ProjectMetresDto | null>(null);
  const [rows, setRows] = useState<NomenclatureRowDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const [reloadToken, setReloadToken] = useState(0);
  const retry = () => setReloadToken((t) => t + 1);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      setError(null);
      try {
        const [metresData, nomenclatureData] = await Promise.all([
          fetchProjectMetres(projectId),
          fetchNomenclature(projectId),
        ]);
        if (!cancelled) {
          setMetres(metresData);
          setRows(nomenclatureData);
        }
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : "Échec de la lecture des métrés.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [projectId, reloadToken]);

  if (loading) {
    return <div className="flex flex-1 items-center justify-center text-slate-400">Calcul des métrés…</div>;
  }

  if (error) {
    return (
      <div className="flex flex-1 flex-col items-center justify-center gap-3 text-slate-400">
        <p className="text-rose-400">{error}</p>
        <button
          type="button"
          onClick={retry}
          className="rounded border border-slate-600 px-3 py-1.5 text-sm hover:border-sky-500 hover:text-sky-300"
        >
          Réessayer
        </button>
      </div>
    );
  }

  if (!metres || !rows) return null;

  const hasQuantities = metres.totalDuctLengthMeters > 0 || metres.accessoryCounts.length > 0;

  return (
    <div className="flex flex-1 flex-col gap-5 overflow-y-auto rounded-lg border border-slate-700 bg-slate-900 p-5">
      <div className="flex items-center justify-between">
        <p className="text-xs text-slate-500">
          {metres.drawingCount} plan{metres.drawingCount > 1 ? "s" : ""} pris en compte
        </p>
        <div className="flex gap-2">
          {rows.length > 0 && (
            <a
              href={nomenclatureExportUrl(projectId)}
              className="rounded border border-slate-600 px-2.5 py-1 text-xs font-medium text-slate-300 hover:border-sky-500 hover:text-sky-300"
            >
              ⭳ Exporter en CSV
            </a>
          )}
          <button
            type="button"
            onClick={retry}
            className="rounded border border-slate-600 px-2.5 py-1 text-xs font-medium text-slate-300 hover:border-sky-500 hover:text-sky-300"
          >
            ⟳ Actualiser
          </button>
        </div>
      </div>

      {!hasQuantities ? (
        <p className="flex-1 text-center text-sm text-slate-500">
          Aucun objet CVC posé sur les plans de ce projet pour l&apos;instant.
        </p>
      ) : (
        <>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            <MetricTile label="Longueur de réseau" value={`${numberFormatter.format(metres.totalDuctLengthMeters)} m`} />
            <MetricTile label="Surface à calorifuger" value={`${numberFormatter.format(metres.totalInsulationAreaM2)} m²`} />
            <MetricTile label="Poids estimé" value={`${numberFormatter.format(metres.totalWeightKg)} kg`} />
          </div>

          <div>
            <h2 className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-slate-400">
              Décompte par type d&apos;accessoire, terminal &amp; équipement
            </h2>
            {metres.accessoryCounts.length === 0 ? (
              <p className="text-sm text-slate-500">Aucun accessoire posé.</p>
            ) : (
              <table className="w-full max-w-md text-sm">
                <tbody className="divide-y divide-slate-800">
                  {metres.accessoryCounts.map((a) => (
                    <tr key={a.type}>
                      <td className="py-1.5 text-slate-300">{ACCESSORY_LABELS[a.type] ?? a.type}</td>
                      <td className="py-1.5 text-right font-mono text-slate-100">{a.count}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>

          <div>
            <h2 className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-slate-400">
              Nomenclature détaillée
            </h2>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[720px] text-sm" data-testid="nomenclature-table">
                <thead>
                  <tr className="border-b border-slate-700 text-left text-[11px] uppercase tracking-wide text-slate-500">
                    <th className="py-1.5 pr-3">Id</th>
                    <th className="py-1.5 pr-3">Plan</th>
                    <th className="py-1.5 pr-3">Calque</th>
                    <th className="py-1.5 pr-3">Type</th>
                    <th className="py-1.5 pr-3">Dimensions</th>
                    <th className="py-1.5 pr-3 text-right">Longueur</th>
                    <th className="py-1.5 pr-3 text-right">Débit</th>
                    <th className="py-1.5 pr-3 text-right">Vitesse</th>
                    <th className="py-1.5 pr-3 text-right">Pression</th>
                    <th className="py-1.5 pr-3 text-right">Poids</th>
                    <th className="py-1.5 text-right">Calorifuge</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800">
                  {rows.map((r) => (
                    <tr key={r.objectId}>
                      <td className="py-1.5 pr-3 font-mono text-slate-500">{r.objectId.slice(0, 8)}</td>
                      <td className="py-1.5 pr-3 text-slate-300">{r.drawingFileName}</td>
                      <td className="py-1.5 pr-3 text-slate-300">{r.layerName}</td>
                      <td className="py-1.5 pr-3 text-slate-300">{TYPE_LABELS[r.type] ?? r.type}</td>
                      <td className="py-1.5 pr-3 font-mono text-slate-300">{dimensionsLabel(r)}</td>
                      <td className="py-1.5 pr-3 text-right font-mono text-slate-100">
                        {r.lengthMeters != null ? `${numberFormatter.format(r.lengthMeters)} m` : "—"}
                      </td>
                      <td className="py-1.5 pr-3 text-right font-mono text-slate-100">
                        {r.debitM3h != null ? `${numberFormatter.format(r.debitM3h)} m³/h` : "—"}
                      </td>
                      <td className="py-1.5 pr-3 text-right font-mono text-slate-100">
                        {r.vitesseMs != null ? `${numberFormatter.format(r.vitesseMs)} m/s` : "—"}
                      </td>
                      <td className="py-1.5 pr-3 text-right font-mono text-slate-100">
                        {r.pressionPa != null ? `${numberFormatter.format(r.pressionPa)} Pa` : "—"}
                      </td>
                      <td className="py-1.5 pr-3 text-right font-mono text-slate-100">
                        {r.weightKg != null ? `${numberFormatter.format(r.weightKg)} kg` : "—"}
                      </td>
                      <td className="py-1.5 text-right font-mono text-slate-100">
                        {r.insulationAreaM2 != null ? `${numberFormatter.format(r.insulationAreaM2)} m²` : "—"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}
    </div>
  );
}

function MetricTile({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2.5">
      <p className="text-[11px] uppercase tracking-wide text-slate-500">{label}</p>
      <p className="mt-1 font-mono text-lg text-slate-100">{value}</p>
    </div>
  );
}
