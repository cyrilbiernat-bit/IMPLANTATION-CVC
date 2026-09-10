import Link from "next/link";

export default function Home() {
  return (
    <div className="flex flex-1 flex-col items-center justify-center bg-slate-950 px-6 text-center">
      <p className="text-xs uppercase tracking-widest text-sky-500">
        BIM CVC · MVP
      </p>
      <h1 className="mt-3 max-w-lg text-3xl font-semibold text-slate-100">
        Implantation CVC 100&nbsp;% web
      </h1>
      <p className="mt-3 max-w-md text-slate-400">
        Module 1 — importez un plan PDF et naviguez-y avant de dessiner votre
        réseau.
      </p>
      <Link
        href="/projets/demo/plan"
        className="mt-8 rounded-full bg-sky-500 px-6 py-2.5 font-medium text-slate-950 transition-colors hover:bg-sky-400"
      >
        Ouvrir le projet de démonstration
      </Link>
    </div>
  );
}
