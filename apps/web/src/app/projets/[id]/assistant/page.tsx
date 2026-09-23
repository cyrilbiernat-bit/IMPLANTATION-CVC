import { AssistantView } from "@/components/assistant-view/AssistantView";
import { ProjectNav } from "@/components/project-nav/ProjectNav";

export default async function AssistantPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;

  return (
    <div className="flex h-dvh flex-col bg-slate-950 p-4">
      <ProjectNav projectId={id} title="Assistant IA" />
      <div className="flex min-h-0 flex-1">
        <AssistantView projectId={id} />
      </div>
    </div>
  );
}
