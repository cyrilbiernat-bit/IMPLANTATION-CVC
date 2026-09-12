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
        Importez vos plans, dessinez votre réseau, et retrouvez calques,
        métrés et nomenclature par projet.
      </p>
      <Link
        href="/projets"
        className="mt-8 rounded-full bg-sky-500 px-6 py-2.5 font-medium text-slate-950 transition-colors hover:bg-sky-400"
      >
        Voir mes projets
      </Link>
    </div>
  );
}
