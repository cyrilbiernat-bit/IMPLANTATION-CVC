import { MetresView } from "@/components/metres-view/MetresView";
import { ProjectNav } from "@/components/project-nav/ProjectNav";

export default async function MetresPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  return (
    <div className="flex h-dvh flex-col bg-slate-950 p-4">
      <ProjectNav projectId={id} title="Métrés & nomenclature" />
      <div className="flex min-h-0 flex-1">
        <MetresView projectId={id} />
      </div>
    </div>
  );
}
