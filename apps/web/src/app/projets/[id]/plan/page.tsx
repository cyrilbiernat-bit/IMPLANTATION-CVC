import { PlanViewer } from "@/components/plan-viewer/PlanViewer";
import { ProjectNav } from "@/components/project-nav/ProjectNav";

export default async function PlanPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  return (
    <div className="flex h-dvh flex-col bg-slate-950 p-4">
      <ProjectNav projectId={id} title="Import, calibration & dessin du plan" />
      <div className="min-h-0 flex-1">
        <PlanViewer projectId={id} />
      </div>
    </div>
  );
}
