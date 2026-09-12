"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import { fetchProject, type ProjectDto } from "@/lib/api-client";

const TABS = [
  { href: (id: string) => `/projets/${id}/plan`, label: "Plan", suffix: "plan" },
  { href: (id: string) => `/projets/${id}/3d`, label: "Vue 3D", suffix: "3d" },
  { href: (id: string) => `/projets/${id}/metres`, label: "Métrés", suffix: "metres" },
];

export function ProjectNav({ projectId, title }: { projectId: string; title: string }) {
  const pathname = usePathname();
  const [project, setProject] = useState<ProjectDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    fetchProject(projectId)
      .then((p) => {
        if (!cancelled) setProject(p);
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : "Projet introuvable.");
      });
    return () => {
      cancelled = true;
    };
  }, [projectId]);

  return (
    <header className="mb-3 flex flex-wrap items-baseline justify-between gap-3">
      <div>
        <p className="text-xs uppercase tracking-wide text-slate-500">
          {error ? error : project ? project.name : "Chargement du projet…"}
        </p>
        <h1 className="text-lg font-semibold text-slate-100">{title}</h1>
      </div>
      <nav className="flex gap-1 rounded-lg border border-slate-700 bg-slate-900 p-1 text-sm">
        {TABS.map((tab) => {
          const active = pathname?.endsWith(`/${tab.suffix}`);
          return (
            <Link
              key={tab.suffix}
              href={tab.href(projectId)}
              className={`rounded-md px-3 py-1.5 font-medium transition-colors ${
                active ? "bg-sky-500 text-slate-950" : "text-slate-300 hover:bg-slate-800"
              }`}
            >
              {tab.label}
            </Link>
          );
        })}
      </nav>
    </header>
  );
}
