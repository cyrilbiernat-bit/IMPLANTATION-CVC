import { PlanViewer } from "@/components/plan-viewer/PlanViewer";

export default async function PlanPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  return (
    <div className="flex h-dvh flex-col bg-slate-950 p-4">
      <header className="mb-3 flex items-baseline justify-between">
        <div>
          <p className="text-xs uppercase tracking-wide text-slate-500">
            Projet {id} · Modules 1-2
          </p>
          <h1 className="text-lg font-semibold text-slate-100">
            Import &amp; calibration du plan
          </h1>
        </div>
      </header>
      <div className="min-h-0 flex-1">
        <PlanViewer />
      </div>
    </div>
  );
}
