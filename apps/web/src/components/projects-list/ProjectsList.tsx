"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { createProject, fetchProjects, type ProjectDto } from "@/lib/api-client";

const dateFormatter = new Intl.DateTimeFormat("fr-FR", { dateStyle: "medium", timeStyle: "short" });

export function ProjectsList() {
  const [projects, setProjects] = useState<ProjectDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [nameInput, setNameInput] = useState("");
  const [creating, setCreating] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const data = await fetchProjects();
        if (!cancelled) setProjects(data);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : "Échec de la lecture des projets.");
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const submitCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!nameInput.trim()) return;
    setCreating(true);
    setError(null);
    try {
      const project = await createProject(nameInput.trim());
      setProjects((prev) => [project, ...(prev ?? [])]);
      setNameInput("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Échec de la création du projet.");
    } finally {
      setCreating(false);
    }
  };

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-6 px-6 py-10">
      <div>
        <p className="text-xs uppercase tracking-widest text-sky-500">BIM CVC · Projets</p>
        <h1 className="mt-2 text-2xl font-semibold text-slate-100">Vos projets</h1>
      </div>

      <form onSubmit={submitCreate} className="flex gap-2">
        <input
          type="text"
          value={nameInput}
          onChange={(e) => setNameInput(e.target.value)}
          placeholder="Nom du projet (ex. Tour Ariane — lot CVC)"
          className="flex-1 rounded-lg border border-slate-700 bg-slate-900 px-3 py-2 text-slate-100 placeholder:text-slate-600"
        />
        <button
          type="submit"
          disabled={creating || !nameInput.trim()}
          className="rounded-lg bg-sky-500 px-4 py-2 font-medium text-slate-950 hover:bg-sky-400 disabled:opacity-50"
        >
          {creating ? "Création…" : "+ Nouveau projet"}
        </button>
      </form>
      {error && <p className="text-sm text-rose-400">{error}</p>}

      {projects === null ? (
        <p className="text-sm text-slate-500">Chargement…</p>
      ) : projects.length === 0 ? (
        <p className="text-sm text-slate-500">Aucun projet pour l&apos;instant — créez-en un ci-dessus.</p>
      ) : (
        <ul className="flex flex-col divide-y divide-slate-800 rounded-lg border border-slate-800">
          {projects.map((p) => (
            <li key={p.id}>
              <Link
                href={`/projets/${p.id}/plan`}
                className="flex items-center justify-between px-4 py-3 hover:bg-slate-900"
              >
                <span className="font-medium text-slate-100">{p.name}</span>
                <span className="text-xs text-slate-500">{dateFormatter.format(new Date(p.createdAt))}</span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
