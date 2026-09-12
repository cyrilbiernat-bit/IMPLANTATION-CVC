"use client";

import { useEffect, useState } from "react";
import { fetchProjectMetres, type ProjectMetresDto } from "@/lib/api-client";

const ACCESSORY_LABELS: Record<string, string> = {
  Coude: "Coudes",
  Te: "Tés",
  Reduction: "Réductions",
  Bouche: "Bouches",
  Diffuseur: "Diffuseurs",
  Extracteur: "Extracteurs",
  Cta: "CTA",
};

const numberFormatter = new Intl.NumberFormat("fr-FR", { maximumFractionDigits: 1 });

export function MetresView({ projectId }: { projectId: string }) {
  const [metres, setMetres] = useState<ProjectMetresDto | null>(null);
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
        const data = await fetchProjectMetres(projectId);
        if (!cancelled) setMetres(data);
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

  if (!metres) return null;

  const hasQuantities = metres.totalDuctLengthMeters > 0 || metres.accessoryCounts.length > 0;

  return (
    <div className="flex flex-1 flex-col gap-4 overflow-y-auto rounded-lg border border-slate-700 bg-slate-900 p-5">
      <div className="flex items-center justify-between">
        <p className="text-xs text-slate-500">
          {metres.drawingCount} plan{metres.drawingCount > 1 ? "s" : ""} pris en compte
        </p>
        <button
          type="button"
          onClick={retry}
          className="rounded border border-slate-600 px-2.5 py-1 text-xs font-medium text-slate-300 hover:border-sky-500 hover:text-sky-300"
        >
          ⟳ Actualiser
        </button>
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
              Nomenclature — accessoires, terminaux &amp; équipements
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
