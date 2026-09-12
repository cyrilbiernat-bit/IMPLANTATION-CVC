import { Scene3DView } from "@/components/scene-3d/Scene3DView";
import { ProjectNav } from "@/components/project-nav/ProjectNav";

export default async function Scene3DPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  return (
    <div className="flex h-dvh flex-col bg-slate-950 p-4">
      <ProjectNav projectId={id} title="Vue 3D du réseau" />
      <div className="flex min-h-0 flex-1">
        <Scene3DView projectId={id} />
      </div>
    </div>
  );
}
