import { PdfViewer } from "@/components/pdf-viewer/PdfViewer";

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
            Projet {id} · Module 1
          </p>
          <h1 className="text-lg font-semibold text-slate-100">
            Import du plan PDF
          </h1>
        </div>
      </header>
      <div className="min-h-0 flex-1">
        <PdfViewer />
      </div>
    </div>
  );
}
