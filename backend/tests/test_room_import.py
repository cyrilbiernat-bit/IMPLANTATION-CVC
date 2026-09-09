import io

from openpyxl import Workbook

from app.core.room_import import parse_rooms_excel


def _make_xlsx(headers, rows) -> bytes:
    wb = Workbook()
    ws = wb.active
    ws.append(headers)
    for row in rows:
        ws.append(row)
    buffer = io.BytesIO()
    wb.save(buffer)
    return buffer.getvalue()


def test_parse_rooms_excel_basic():
    data = _make_xlsx(
        ["Nom", "Type", "Surface", "Hauteur", "Fluide", "Charge"],
        [
            ["Bureau 1", "bureau", 20, 2.6, "R32", 0.8],
            ["Local technique", "local_technique", 8, 2.5, "R410A", 5.0],
        ],
    )
    rooms, warnings = parse_rooms_excel(data)
    assert len(rooms) == 2
    assert rooms[0]["room_name"] == "Bureau 1"
    assert rooms[0]["surface_m2"] == 20
    assert rooms[0]["fluid_code"] == "R32"
    assert rooms[1]["charge_kg"] == 5.0


def test_parse_rooms_excel_missing_columns():
    data = _make_xlsx(["Colonne A", "Colonne B"], [["x", "y"]])
    rooms, warnings = parse_rooms_excel(data)
    assert rooms == []
    assert warnings


def test_parse_rooms_excel_default_height_and_type():
    data = _make_xlsx(["Nom", "Surface"], [["Salle", 15]])
    rooms, warnings = parse_rooms_excel(data)
    assert rooms[0]["height_m"] == 2.5
    assert rooms[0]["room_type"] == "bureau"


def test_parse_rooms_excel_skips_invalid_rows():
    data = _make_xlsx(["Nom", "Surface"], [["Salle", "invalide"], ["Bureau", 12]])
    rooms, warnings = parse_rooms_excel(data)
    assert len(rooms) == 1
    assert rooms[0]["room_name"] == "Bureau"
    assert any("ignorée" in w for w in warnings)
