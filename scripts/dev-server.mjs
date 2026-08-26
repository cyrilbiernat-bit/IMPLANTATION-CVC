/**
 * Serveur web statique sans dépendance pour le développement local.
 * Sert le dépôt et ouvre l'application IMPLANTATION-CVC.
 *
 * Usage : node scripts/dev-server.mjs [port]
 *   PORT ou 1er argument : port d'écoute (défaut 5173)
 */
import { createServer } from "node:http";
import { readFile, stat } from "node:fs/promises";
import { extname, join, normalize, resolve } from "node:path";

const ROOT = resolve(process.cwd());
const PORT = Number(process.env.PORT ?? process.argv[2] ?? 5173);
const ENTRY = "implantation_cvc_plb.html";

const MIME = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".mjs": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".svg": "image/svg+xml",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".ico": "image/x-icon",
  ".map": "application/json; charset=utf-8",
  ".zip": "application/zip",
};

function safePath(urlPath) {
  const decoded = decodeURIComponent(urlPath.split("?")[0]);
  const rel = normalize(decoded).replace(/^(\.\.[/\\])+/, "");
  const full = join(ROOT, rel);
  // Empêche de sortir de la racine du dépôt.
  if (!full.startsWith(ROOT)) return null;
  return full;
}

const server = createServer(async (req, res) => {
  let urlPath = req.url ?? "/";
  if (urlPath === "/" || urlPath === "") {
    urlPath = "/" + ENTRY;
  }
  const filePath = safePath(urlPath);
  if (!filePath) {
    res.writeHead(403).end("Forbidden");
    return;
  }
  try {
    const info = await stat(filePath);
    const target = info.isDirectory() ? join(filePath, "index.html") : filePath;
    const data = await readFile(target);
    const type = MIME[extname(target).toLowerCase()] ?? "application/octet-stream";
    res.writeHead(200, { "Content-Type": type, "Cache-Control": "no-cache" });
    res.end(data);
  } catch {
    res.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" });
    res.end("404 Not Found");
  }
});

server.listen(PORT, "0.0.0.0", () => {
  console.log(`[implantation-cvc] Application servie sur http://localhost:${PORT}/`);
  console.log(`[implantation-cvc] Entrée : ${ENTRY}`);
});
