import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  FormControlLabel,
  Grid,
  MenuItem,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import TextField from "@mui/material/TextField";
import { useEffect, useMemo, useState } from "react";
import { api, extractErrorMessage } from "../api/client";
import ComplianceGauge from "../components/ComplianceGauge";
import RiskIndicator from "../components/RiskIndicator";
import { ACCESS_CATEGORIES, COMFORT_AC_SYSTEM_TYPES, MOUNTING_TYPES } from "../constants";
import { useProject } from "../context/ProjectContext";
import { Fluid, Manufacturer, QuickCalcResponse } from "../types";

const BUILDING_TYPES = ["tertiaire", "residentiel", "industriel", "commercial", "erp", "hotel", "sante", "logistique"];
const ROOM_TYPES = ["bureau", "salle_reunion", "chambre", "hotel", "erp", "laboratoire", "local_technique", "commercial", "industriel", "logistique"];
const SYSTEM_TYPES = ["DRV", "Multi-split", "Split", "Groupe eau glacee", "PAC", "Centrale frigorifique", "Meuble frigorifique"];
const CLIMATE_ZONES = ["H1", "H2", "H3"];
const FLAMMABLE_GROUPS = ["A2L", "A2", "A3"];

export default function QuickCalc() {
  const { currentProject, setCurrentProject } = useProject();
  const [fluids, setFluids] = useState<Fluid[]>([]);
  const [form, setForm] = useState({
    building_type: "tertiaire",
    room_name: "Bureau 1",
    room_type: "bureau",
    surface_m2: 25,
    height_m: 2.6,
    system_type: "",
    fluid_code: "R32",
    cooling_power_kw: undefined as number | undefined,
    heating_power_kw: undefined as number | undefined,
    indoor_units: 1,
    access_category: ACCESS_CATEGORIES[0],
    climate_zone: "H2",
    mounting_type: "wall",
    is_lowest_basement_level: false,
    equipment_id: undefined as number | undefined,
  });
  const [result, setResult] = useState<QuickCalcResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [manufacturers, setManufacturers] = useState<Manufacturer[]>([]);

  useEffect(() => {
    api.get<Fluid[]>("/fluids").then((r) => setFluids(r.data));
    api.get<Manufacturer[]>("/equipment/manufacturers").then((r) => setManufacturers(r.data));
  }, []);

  const selectEquipment = (equipmentId: string) => {
    if (!equipmentId) {
      setForm({ ...form, equipment_id: undefined });
      return;
    }
    const id = Number(equipmentId);
    const equip = manufacturers.flatMap((m) => m.equipments).find((e) => e.id === id);
    setForm({
      ...form,
      equipment_id: id,
      fluid_code: equip?.fluid_code ?? form.fluid_code,
      system_type: equip?.system_type ?? form.system_type,
    });
  };

  const selectedFluid = useMemo(
    () => fluids.find((f) => f.code === form.fluid_code),
    [fluids, form.fluid_code]
  );
  const isFlammable = selectedFluid ? FLAMMABLE_GROUPS.includes(selectedFluid.safety_group) : false;
  const effectiveSystemType = form.system_type || "DRV";
  const showMountingType = isFlammable && COMFORT_AC_SYSTEM_TYPES.includes(effectiveSystemType);

  const submit = async () => {
    setError(null);
    setLoading(true);
    try {
      const payload: Record<string, unknown> = {
        ...form,
        project_id: currentProject?.id,
        project_name: currentProject ? undefined : `Étude rapide — ${form.room_name}`,
        system_type: form.system_type || undefined,
      };
      const r = await api.post<QuickCalcResponse>("/calculations/quick", payload);
      setResult(r.data);
      if (!currentProject && r.data.project_id) {
        setCurrentProject({ id: r.data.project_id, name: payload.project_name as string });
      }
    } catch (e: any) {
      setError(extractErrorMessage(e, "Erreur lors du calcul."));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Grid container spacing={3}>
      <Grid item xs={12} lg={5}>
        <Paper sx={{ p: 3 }}>
          <Typography variant="h6" gutterBottom>
            Prédimensionnement
          </Typography>
          <Stack spacing={2}>
            <TextField
              select
              label="Type de bâtiment"
              value={form.building_type}
              onChange={(e) => setForm({ ...form, building_type: e.target.value })}
            >
              {BUILDING_TYPES.map((t) => (
                <MenuItem key={t} value={t}>
                  {t}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              label="Nom du local"
              value={form.room_name}
              onChange={(e) => setForm({ ...form, room_name: e.target.value })}
            />
            <TextField
              select
              label="Type de local"
              value={form.room_type}
              onChange={(e) => setForm({ ...form, room_type: e.target.value })}
            >
              {ROOM_TYPES.map((t) => (
                <MenuItem key={t} value={t}>
                  {t}
                </MenuItem>
              ))}
            </TextField>
            <Stack direction="row" spacing={2}>
              <TextField
                label="Surface (m²)"
                type="number"
                fullWidth
                value={form.surface_m2}
                onChange={(e) => setForm({ ...form, surface_m2: Number(e.target.value) })}
              />
              <TextField
                label="Hauteur sous plafond (m)"
                type="number"
                fullWidth
                value={form.height_m}
                onChange={(e) => setForm({ ...form, height_m: Number(e.target.value) })}
              />
            </Stack>
            <Typography variant="caption" color="text.secondary">
              Volume calculé : {(form.surface_m2 * form.height_m).toFixed(1)} m³
            </Typography>

            <TextField
              select
              label="Système (laisser vide pour proposition automatique)"
              value={form.system_type}
              disabled={!!form.equipment_id}
              onChange={(e) => setForm({ ...form, system_type: e.target.value })}
            >
              <MenuItem value="">— Proposition automatique —</MenuItem>
              {SYSTEM_TYPES.map((t) => (
                <MenuItem key={t} value={t}>
                  {t}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label="Fluide frigorigène"
              value={form.fluid_code}
              disabled={!!form.equipment_id}
              onChange={(e) => setForm({ ...form, fluid_code: e.target.value })}
            >
              {fluids.map((f) => (
                <MenuItem key={f.code} value={f.code}>
                  {f.code} — {f.name} ({f.safety_group})
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label="Équipement (optionnel — présaisie de la charge depuis la bibliothèque)"
              value={form.equipment_id ?? ""}
              onChange={(e) => selectEquipment(e.target.value)}
            >
              <MenuItem value="">— Aucun (charge estimée par ratio) —</MenuItem>
              {manufacturers.map((m) => [
                <MenuItem key={`h-${m.id}`} disabled sx={{ fontWeight: 700, opacity: "1 !important" }}>
                  {m.name}
                </MenuItem>,
                ...m.equipments.map((e) => (
                  <MenuItem key={e.id} value={e.id} sx={{ pl: 4 }}>
                    {e.reference} — {e.system_type}, {e.fluid_code}, {e.cooling_power_kw} kW ({e.factory_charge_kg} kg
                    usine)
                  </MenuItem>
                )),
              ])}
            </TextField>
            {form.equipment_id && (
              <Typography variant="caption" color="text.secondary" sx={{ mt: -1 }}>
                La charge sera présaisie depuis cet équipement (fluide et système verrouillés en conséquence).
              </Typography>
            )}

            <Stack direction="row" spacing={2}>
              <TextField
                label="Puissance frigorifique (kW)"
                type="number"
                fullWidth
                helperText="Optionnel — estimée automatiquement sinon"
                value={form.cooling_power_kw ?? ""}
                onChange={(e) =>
                  setForm({ ...form, cooling_power_kw: e.target.value ? Number(e.target.value) : undefined })
                }
              />
              <TextField
                label="Puissance chauffage (kW)"
                type="number"
                fullWidth
                helperText="Optionnel"
                value={form.heating_power_kw ?? ""}
                onChange={(e) =>
                  setForm({ ...form, heating_power_kw: e.target.value ? Number(e.target.value) : undefined })
                }
              />
            </Stack>

            <TextField
              label="Nombre d'unités intérieures"
              type="number"
              value={form.indoor_units}
              onChange={(e) => setForm({ ...form, indoor_units: Number(e.target.value) })}
            />

            <TextField
              select
              label="Catégorie d'accès du local"
              value={form.access_category}
              onChange={(e) => setForm({ ...form, access_category: e.target.value })}
            >
              {ACCESS_CATEGORIES.map((t) => (
                <MenuItem key={t} value={t}>
                  {t}
                </MenuItem>
              ))}
            </TextField>

            <TextField
              select
              label="Zone climatique"
              value={form.climate_zone}
              onChange={(e) => setForm({ ...form, climate_zone: e.target.value })}
            >
              {CLIMATE_ZONES.map((t) => (
                <MenuItem key={t} value={t}>
                  {t}
                </MenuItem>
              ))}
            </TextField>

            {showMountingType && (
              <TextField
                select
                label="Emplacement d'installation (fluide inflammable — Formule C.1)"
                value={form.mounting_type}
                onChange={(e) => setForm({ ...form, mounting_type: e.target.value })}
              >
                {MOUNTING_TYPES.map((t) => (
                  <MenuItem key={t.value} value={t.value}>
                    {t.label}
                  </MenuItem>
                ))}
              </TextField>
            )}

            <FormControlLabel
              control={
                <Checkbox
                  checked={form.is_lowest_basement_level}
                  onChange={(e) => setForm({ ...form, is_lowest_basement_level: e.target.checked })}
                />
              }
              label="Local situé à l'étage le plus bas en sous-sol (C.3.2.3)"
            />

            {error && <Alert severity="error">{error}</Alert>}

            <Button variant="contained" size="large" onClick={submit} disabled={loading}>
              {loading ? "Calcul en cours..." : "Calculer"}
            </Button>
          </Stack>
        </Paper>
      </Grid>

      <Grid item xs={12} lg={7}>
        {!result && (
          <Paper sx={{ p: 3, height: "100%", display: "flex", alignItems: "center", justifyContent: "center" }}>
            <Typography color="text.secondary">
              Renseignez les paramètres du local et cliquez sur « Calculer » pour obtenir une
              estimation instantanée de la charge de fluide et de la conformité NF EN 378-1.
            </Typography>
          </Paper>
        )}
        {result && (
          <Stack spacing={2}>
            <Paper sx={{ p: 3 }}>
              <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
                <Typography variant="h6">Résultats instantanés</Typography>
                <Stack direction="row" spacing={1} alignItems="center">
                  <Chip
                    size="small"
                    variant="outlined"
                    label={
                      result.concentration.method_used === "A"
                        ? "Méthode C.2 (climatisation/PAC confort)"
                        : "Méthode C.3 (autre solution)"
                    }
                  />
                  <RiskIndicator conformity={result.concentration.conformity} />
                </Stack>
              </Stack>
              <Grid container spacing={2} alignItems="center">
                <Grid item xs={12} sm={5} sx={{ display: "flex", justifyContent: "center" }}>
                  <ComplianceGauge
                    marginRatio={result.concentration.margin_ratio}
                    conformity={result.concentration.conformity}
                  />
                </Grid>
                <Grid item xs={12} sm={7}>
                  <Table size="small">
                    <TableBody>
                      <TableRow>
                        <TableCell>Système proposé</TableCell>
                        <TableCell align="right">
                          <Chip size="small" label={result.suggested_system_type} />
                        </TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell>Besoin froid / chaud</TableCell>
                        <TableCell align="right">
                          {result.loads.cooling_power_kw} kW / {result.loads.heating_power_kw} kW
                        </TableCell>
                      </TableRow>
                      {result.charge_estimate.equipment_reference && (
                        <TableRow>
                          <TableCell>Équipement sélectionné</TableCell>
                          <TableCell align="right">
                            <Chip size="small" label={result.charge_estimate.equipment_reference} />
                          </TableCell>
                        </TableRow>
                      )}
                      <TableRow>
                        <TableCell>
                          {result.charge_estimate.equipment_reference ? "Charge (fiche équipement)" : "Charge probable estimée"}
                        </TableCell>
                        <TableCell align="right">{result.charge_estimate.total_charge_kg} kg</TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell>Charge / unité intérieure</TableCell>
                        <TableCell align="right">
                          {result.charge_estimate.charge_per_indoor_unit_kg} kg
                        </TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell>Volume du local</TableCell>
                        <TableCell align="right">{result.concentration.volume_m3} m³</TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell>Concentration calculée</TableCell>
                        <TableCell align="right">
                          {result.concentration.concentration_kg_m3} kg/m³
                        </TableCell>
                      </TableRow>
                      <TableRow>
                        <TableCell>Limite applicable ({result.concentration.limit_type})</TableCell>
                        <TableCell align="right">
                          {result.concentration.limit_used_kg_m3 ?? "-"}
                          {result.concentration.method_used === "A" ? " kg" : " kg/m³"}
                        </TableCell>
                      </TableRow>
                      {result.concentration.method_used === "B" && (
                        <>
                          <TableRow>
                            <TableCell>RCL / QLMV / QLAV</TableCell>
                            <TableCell align="right">
                              {result.concentration.general.rcl_kg_m3 ?? "-"} /{" "}
                              {result.concentration.general.qlmv_kg_m3 ?? "-"} /{" "}
                              {result.concentration.general.qlav_kg_m3 ?? "-"} kg/m³
                            </TableCell>
                          </TableRow>
                          <TableRow>
                            <TableCell>Mesures compensatoires requises</TableCell>
                            <TableCell align="right">{result.concentration.general.measures_required}</TableCell>
                          </TableRow>
                        </>
                      )}
                      {result.concentration.method_used === "A" && result.concentration.split_system && (
                        <TableRow>
                          <TableCell>Surface minimale requise (Amin)</TableCell>
                          <TableCell align="right">
                            {result.concentration.split_system.amin_m2 ?? "-"} m²
                          </TableCell>
                        </TableRow>
                      )}
                    </TableBody>
                  </Table>
                </Grid>
              </Grid>
              {result.concentration.notes.length > 0 && (
                <Stack spacing={0.5} sx={{ mt: 2 }}>
                  {result.concentration.notes.map((note, i) => (
                    <Typography key={i} variant="caption" color="text.secondary">
                      • {note}
                    </Typography>
                  ))}
                </Stack>
              )}
            </Paper>

            <Paper sx={{ p: 3 }}>
              <Typography variant="h6" gutterBottom>
                Alertes et recommandations NF EN 378-1
              </Typography>
              <Stack spacing={1}>
                {result.recommendations.map((rec, i) => (
                  <Alert
                    key={i}
                    severity={
                      rec.priority === "haute" ? "error" : rec.priority === "moyenne" ? "warning" : "info"
                    }
                  >
                    <b>{rec.measure}</b> — {rec.reason}
                  </Alert>
                ))}
              </Stack>
            </Paper>

            {result.equipment_suggestions.length > 0 && (
              <Paper sx={{ p: 3 }}>
                <Typography variant="h6" gutterBottom>
                  Équipements compatibles (bibliothèque)
                </Typography>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Référence</TableCell>
                      <TableCell>Puissance froid</TableCell>
                      <TableCell>Charge usine</TableCell>
                      <TableCell>Longueur max.</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {result.equipment_suggestions.map((e) => (
                      <TableRow key={e.id}>
                        <TableCell>{e.reference}</TableCell>
                        <TableCell>{e.cooling_power_kw} kW</TableCell>
                        <TableCell>{e.factory_charge_kg} kg</TableCell>
                        <TableCell>{e.max_pipe_length_m ?? "-"} m</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Paper>
            )}
          </Stack>
        )}
      </Grid>
    </Grid>
  );
}
