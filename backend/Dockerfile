FROM python:3.11-slim

WORKDIR /app

# poppler-utils : extraction de texte/rendu des plans PDF (pdftotext, pdftoppm)
# tesseract-ocr(-fra) : reconnaissance optique de caractères pour les plans
# scannés / images (détection automatique de locaux, cf. core/plan_detection.py)
RUN apt-get update && apt-get install -y --no-install-recommends \
    poppler-utils tesseract-ocr tesseract-ocr-fra \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app ./app

EXPOSE 8000

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
