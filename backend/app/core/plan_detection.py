"""Détection automatique des locaux à partir d'un plan (PDF ou image).

Portée et limites (à annoncer clairement à l'utilisateur)
-----------------------------------------------------------
Cette fonctionnalité NE réalise PAS de reconnaissance géométrique des murs
ou des polygones de pièces : elle repose sur la lecture des étiquettes
textuelles présentes sur le plan (nom du local et surface annotée, par
exemple « Bureau 1 — 18 m² »), exactement comme le ferait un lecteur humain
parcourant les cotations d'un plan. Deux cas sont couverts :

  1. Plan PDF vectoriel (export CAO/BIM courant) : le texte des étiquettes
     est intégré au PDF et directement extrait (pdftotext).
  2. Plan scanné / image (PNG, JPG, TIFF...) ou PDF sans texte vectoriel :
     une reconnaissance optique de caractères (OCR, Tesseract) est utilisée
     sur l'image du plan.

Dans les deux cas, la qualité de la détection dépend directement de la
présence et de la lisibilité des annotations de surface sur le plan fourni.
Les résultats DOIVENT être vérifiés et complétés manuellement avant tout
usage réglementaire.
"""
from __future__ import annotations

import io
import subprocess
import tempfile
from pathlib import Path

from .cctp_analysis import heuristic_extract_rooms

SUPPORTED_IMAGE_EXTENSIONS = {"png", "jpg", "jpeg", "tif", "tiff", "bmp"}
MAX_PDF_PAGES_FOR_OCR = 5


def _extract_pdf_text(file_bytes: bytes) -> str:
    with tempfile.TemporaryDirectory() as tmpdir:
        pdf_path = Path(tmpdir) / "plan.pdf"
        pdf_path.write_bytes(file_bytes)
        try:
            proc = subprocess.run(
                ["pdftotext", "-layout", str(pdf_path), "-"],
                capture_output=True,
                timeout=30,
                check=True,
            )
        except (subprocess.CalledProcessError, FileNotFoundError, subprocess.TimeoutExpired):
            return ""
        return proc.stdout.decode("utf-8", errors="ignore")


def _ocr_image_bytes(image_bytes: bytes) -> str:
    try:
        import pytesseract
        from PIL import Image
    except ImportError:
        return ""
    try:
        image = Image.open(io.BytesIO(image_bytes))
        return pytesseract.image_to_string(image, lang="fra+eng")
    except Exception:
        return ""


def _ocr_pdf(file_bytes: bytes) -> str:
    with tempfile.TemporaryDirectory() as tmpdir:
        pdf_path = Path(tmpdir) / "plan.pdf"
        pdf_path.write_bytes(file_bytes)
        out_prefix = Path(tmpdir) / "page"
        try:
            subprocess.run(
                [
                    "pdftoppm", "-png", "-r", "200",
                    "-l", str(MAX_PDF_PAGES_FOR_OCR),
                    str(pdf_path), str(out_prefix),
                ],
                capture_output=True,
                timeout=60,
                check=True,
            )
        except (subprocess.CalledProcessError, FileNotFoundError, subprocess.TimeoutExpired):
            return ""

        texts = []
        for image_path in sorted(Path(tmpdir).glob("page-*.png")):
            texts.append(_ocr_image_bytes(image_path.read_bytes()))
        return "\n".join(texts)


def analyze_plan(file_bytes: bytes, filename: str) -> dict:
    ext = filename.lower().rsplit(".", 1)[-1] if "." in filename else ""
    warnings: list[str] = []

    if ext == "pdf":
        text = _extract_pdf_text(file_bytes)
        engine = "texte vectoriel PDF (pdftotext)"
        if len(text.strip()) < 20:
            text = _ocr_pdf(file_bytes)
            engine = "OCR (Tesseract) sur pages PDF rendues en image"
            warnings.append(
                "Aucun texte vectoriel exploitable dans le PDF : détection par OCR "
                "(reconnaissance optique), les résultats sont moins fiables et "
                "doivent être vérifiés avec attention."
            )
    elif ext in SUPPORTED_IMAGE_EXTENSIONS:
        text = _ocr_image_bytes(file_bytes)
        engine = "OCR (Tesseract) sur image"
        warnings.append(
            "Détection par OCR sur image : la reconnaissance de texte de faible "
            "résolution, manuscrit ou stylisé peut être imparfaite — vérifier "
            "chaque local détecté."
        )
    else:
        raise ValueError(
            "Format de plan non supporté. Formats acceptés : PDF, PNG, JPG, TIFF, BMP."
        )

    rooms = heuristic_extract_rooms(text)
    if not rooms:
        warnings.append(
            "Aucun local détecté automatiquement. Vérifiez que les surfaces sont "
            "annotées sur le plan (ex. « Bureau 1 — 18 m² ») ou saisissez les "
            "locaux manuellement / via l'import Excel."
        )

    return {
        "engine": engine,
        "rooms": rooms,
        "warnings": warnings,
        "extracted_text_preview": text[:3000],
    }
