import shutil

import pytest
from reportlab.lib.pagesizes import A4
from reportlab.pdfgen import canvas

from app.core.plan_detection import analyze_plan

TESSERACT_AVAILABLE = shutil.which("tesseract") is not None


def _make_pdf_with_text(lines: list[str]) -> bytes:
    import io

    buffer = io.BytesIO()
    c = canvas.Canvas(buffer, pagesize=A4)
    y = 800
    for line in lines:
        c.drawString(50, y, line)
        y -= 20
    c.save()
    return buffer.getvalue()


def test_analyze_plan_pdf_vector_text():
    pdf_bytes = _make_pdf_with_text(["Bureau direction: 25 m2", "Local technique: 6 m2"])
    result = analyze_plan(pdf_bytes, "plan.pdf")
    assert "vectoriel" in result["engine"]
    assert len(result["rooms"]) >= 1


def test_analyze_plan_unsupported_format():
    with pytest.raises(ValueError):
        analyze_plan(b"not a real file", "plan.docx")


@pytest.mark.skipif(not TESSERACT_AVAILABLE, reason="tesseract-ocr not installed")
def test_analyze_plan_image_ocr():
    from PIL import Image, ImageDraw

    img = Image.new("RGB", (900, 200), color="white")
    draw = ImageDraw.Draw(img)
    draw.text((20, 20), "Bureau 1: 20 m2", fill="black")
    import io

    buf = io.BytesIO()
    img.save(buf, format="PNG")
    result = analyze_plan(buf.getvalue(), "plan.png")
    assert "OCR" in result["engine"]
    # OCR on a synthetic image is best-effort; just ensure it runs without error
    # and returns the expected response shape.
    assert "rooms" in result and "warnings" in result
