"""Génération de rapport PDF (page de garde, sommaire, calculs, recommandations)."""
from __future__ import annotations

import io

from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import cm
from reportlab.platypus import (
    Image,
    PageBreak,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)

CONFORMITY_COLORS = {
    "Conforme": colors.HexColor("#2e7d32"),
    "Conforme sous conditions": colors.HexColor("#ed6c02"),
    "Non conforme": colors.HexColor("#c62828"),
}


def build_report_pdf(
    project: dict,
    results: list[dict],
    company: dict | None = None,
    logo_path: str | None = None,
) -> bytes:
    buffer = io.BytesIO()
    doc = SimpleDocTemplate(
        buffer,
        pagesize=A4,
        topMargin=2 * cm,
        bottomMargin=2 * cm,
        leftMargin=2 * cm,
        rightMargin=2 * cm,
    )
    styles = getSampleStyleSheet()
    title_style = ParagraphStyle(
        "TitleCustom", parent=styles["Title"], fontSize=22, spaceAfter=12
    )
    h2 = ParagraphStyle("H2Custom", parent=styles["Heading2"], spaceBefore=14, spaceAfter=8)
    body = styles["BodyText"]

    story = []

    # --- Page de garde ---
    if logo_path:
        try:
            story.append(Image(logo_path, width=4 * cm, height=4 * cm))
            story.append(Spacer(1, 1 * cm))
        except Exception:
            pass

    story.append(Paragraph("Rapport de vérification NF EN 378-1", title_style))
    story.append(Paragraph("Concentration de fluide frigorigène", styles["Heading3"]))
    story.append(Spacer(1, 1.5 * cm))
    story.append(Paragraph(f"<b>Projet :</b> {project.get('name', '-')}", body))
    if project.get("client"):
        story.append(Paragraph(f"<b>Client :</b> {project['client']}", body))
    if project.get("address"):
        story.append(Paragraph(f"<b>Adresse :</b> {project['address']}", body))
    if project.get("author"):
        story.append(Paragraph(f"<b>Rédacteur :</b> {project['author']}", body))
    if company and company.get("company_name"):
        story.append(Spacer(1, 1 * cm))
        story.append(Paragraph(f"<b>Bureau d'études :</b> {company['company_name']}", body))
        if company.get("address"):
            story.append(Paragraph(company["address"], body))
        if company.get("contact_email"):
            story.append(Paragraph(company["contact_email"], body))
    story.append(PageBreak())

    # --- Sommaire ---
    story.append(Paragraph("Sommaire", h2))
    toc_items = [
        "1. Données du projet",
        "2. Résumé des hypothèses",
        "3. Résultats de calcul NF EN 378-1",
        "4. Recommandations",
        "5. Références normatives",
    ]
    for item in toc_items:
        story.append(Paragraph(item, body))
    story.append(PageBreak())

    # --- Données projet / hypothèses ---
    story.append(Paragraph("1. Données du projet", h2))
    story.append(Paragraph(f"Type de bâtiment : {project.get('building_type', '-')}", body))
    if project.get("notes"):
        story.append(Paragraph(f"Notes : {project['notes']}", body))

    story.append(Paragraph("2. Résumé des hypothèses", h2))
    story.append(
        Paragraph(
            "Les calculs de concentration sont réalisés selon la méthodologie de la norme "
            "NF EN 378-1 : Volume = Surface x Hauteur ; Concentration = Masse relâchée / "
            "Volume ; comparaison à la limite applicable (RCL, LFL pratique, ATEL ou ODL "
            "selon le groupe de sécurité du fluide). Les valeurs de seuils utilisées "
            "proviennent de la bibliothèque fluides de l'application et doivent être "
            "vérifiées par un professionnel qualifié.",
            body,
        )
    )
    story.append(PageBreak())

    # --- Résultats ---
    story.append(Paragraph("3. Résultats de calcul NF EN 378-1", h2))
    table_data = [
        ["Local", "Fluide", "Charge (kg)", "Volume (m3)", "Concentration (kg/m3)", "Limite", "Conformité"]
    ]
    for r in results:
        table_data.append(
            [
                r.get("room_name", "-"),
                r.get("fluid_code", "-"),
                f"{r.get('charge_kg', 0):.2f}",
                f"{r.get('volume_m3', 0):.2f}",
                f"{r.get('concentration_kg_m3', 0):.4f}",
                f"{r.get('limit_used_kg_m3', 0):.4f} ({r.get('limit_type', '-')})",
                r.get("conformity", "-"),
            ]
        )

    table = Table(table_data, repeatRows=1)
    style_cmds = [
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#1565c0")),
        ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
        ("FONTSIZE", (0, 0), (-1, -1), 8),
        ("GRID", (0, 0), (-1, -1), 0.5, colors.grey),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#f2f2f2")]),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
    ]
    for i, r in enumerate(results, start=1):
        c = CONFORMITY_COLORS.get(r.get("conformity"), colors.black)
        style_cmds.append(("TEXTCOLOR", (6, i), (6, i), c))
        style_cmds.append(("FONTNAME", (6, i), (6, i), "Helvetica-Bold"))
    table.setStyle(TableStyle(style_cmds))
    story.append(table)
    story.append(PageBreak())

    # --- Recommandations ---
    story.append(Paragraph("4. Recommandations", h2))
    for r in results:
        recs = r.get("recommendations") or []
        if not recs:
            continue
        story.append(Paragraph(f"<b>{r.get('room_name', 'Local')}</b>", body))
        for rec in recs:
            story.append(
                Paragraph(
                    f"• {rec.get('measure')} — {rec.get('reason')} "
                    f"(priorité : {rec.get('priority')})",
                    body,
                )
            )
        story.append(Spacer(1, 0.3 * cm))

    story.append(PageBreak())

    # --- Références normatives ---
    story.append(Paragraph("5. Références normatives", h2))
    story.append(
        Paragraph(
            "NF EN 378-1 : Systèmes de réfrigération et pompes à chaleur — Exigences de "
            "sécurité et d'environnement — Partie 1 : Exigences de base, définitions, "
            "classification et critères de choix. Classification de sécurité des fluides "
            "frigorigènes : ISO 817 / ASHRAE 34.",
            body,
        )
    )
    story.append(
        Paragraph(
            "Ce rapport est généré automatiquement à titre d'aide à la vérification. Il ne "
            "se substitue pas à l'analyse d'un professionnel qualifié et doit être validé "
            "avant intégration à un dossier réglementaire.",
            styles["Italic"],
        )
    )

    doc.build(story)
    return buffer.getvalue()
