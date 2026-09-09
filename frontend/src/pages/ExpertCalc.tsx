import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import {
  Alert,
  Box,
  Button,
  Grid,
  IconButton,
  MenuItem,
  Paper,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Tabs,
  TextField,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { api, extractErrorMessage } from "../api/client";
import ComplianceGauge from "../components/ComplianceGauge";
import ConcentrationChart from "../components/ConcentrationChart";
import RiskIndicator from "../components/RiskIndicator";
import RoomHeatmap from "../components/RoomHeatmap";
import { useProject } from "../context/ProjectContext";
import {
  ExpertCalcResponse,
  ExpertCircuitInput,
  Fluid,
  MultiRoomResponse,
  MultiRoomRoomInput,
} from "../types";

const ACCESS_CATEGORIES = [
  "Accès général (public)",
  "Accès supervisé",
  "Accès autorisé uniquement (personnel qualifié)",
];
const ROOM_TYPES = ["bureau", "salle_reunion", "chambre", "hotel", "erp", "laboratoire", "local_technique", "commercial", "industriel", "logistique"];

function emptyCircuit(fluidCode: string): ExpertCircuitInput {
  return {
    name: `Circuit ${Math.floor(Math.random() * 1000)}`,
    fluid_code: fluidCode,
    factory_charge_kg: 3,
    pipe_length_m: 20,
    additional_charge_kg_per_m: 0.02,
    altimetry_m: 0,
    equivalent_length_m: undefined,
    zone_count: 1,
    indoor_unit_count: 1,
  };
}

function emptyRoom(fluidCode: string): MultiRoomRoomInput {
  return {
    room_name: `Local ${Math.floor(Math.random() * 1000)}`,
    room_type: "bureau",
    surface_m2: 20,
    height_m: 2.5,
    fluid_code: fluidCode,
    charge_kg: 1,
    access_category: ACCESS_CATEGORIES[0],
  };
}

export default function ExpertCalc() {
  const { currentProject } = useProject();
  const [tab, setTab] = useState(0);
  const [fluids, setFluids] = useState<Fluid[]>([]);

  useEffect(() => {
    api.get<Fluid[]>("/fluids").then((r) => setFluids(r.data));
  }, []);

  return (
    <Stack spacing={2}>
      {!currentProject && (
        <Alert severity="warning">
          Aucun projet courant sélectionné (page « Nouveau projet »). Les résultats détaillés ne
          seront pas historisés tant qu'un projet n'est pas sélectionné, mais le calcul reste
          disponible.
        </Alert>
      )}
      <Tabs value={tab} onChange={(_, v) => setTab(v)}>
        <Tab label="Calcul détaillé (circuits)" />
        <Tab label="Analyse multilocaux" />
      </Tabs>
      {tab === 0 && <ExpertCircuitsPanel fluids={fluids} />}
      {tab === 1 && <MultiRoomPanel fluids={fluids} />}
    </Stack>
  );
}

function ExpertCircuitsPanel({ fluids }: { fluids: Fluid[] }) {
  const { currentProject } = useProject();
  const [roomName, setRoomName] = useState("Local technique");
  const [roomType, setRoomType] = useState("local_technique");
  const [surface, setSurface] = useState(10);
  const [height, setHeight] = useState(2.5);
  const [access, setAccess] = useState(ACCESS_CATEGORIES[0]);
  const [circuits, setCircuits] = useState<ExpertCircuitInput[]>([emptyCircuit("R32")]);
  const [result, setResult] = useState<ExpertCalcResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const updateCircuit = (idx: number, patch: Partial<ExpertCircuitInput>) => {
    setCircuits((cs) => cs.map((c, i) => (i === idx ? { ...c, ...patch } : c)));
  };

  const submit = async () => {
    setError(null);
    try {
      const r = await api.post<ExpertCalcResponse>("/calculations/expert", {
        project_id: currentProject?.id,
        room_name: roomName,
        room_type: roomType,
        surface_m2: surface,
        height_m: height,
        access_category: access,
        circuits,
      });
      setResult(r.data);
    } catch (e: any) {
      setError(extractErrorMessage(e, "Erreur lors du calcul."));
    }
  };

  return (
    <Grid container spacing={3}>
      <Grid item xs={12} lg={7}>
        <Paper sx={{ p: 3 }}>
          <Typography variant="h6" gutterBottom>
            Local & circuits frigorifiques
          </Typography>
          <Grid container spacing={2} sx={{ mb: 2 }}>
            <Grid item xs={12} sm={6}>
              <TextField label="Nom du local" fullWidth value={roomName} onChange={(e) => setRoomName(e.target.value)} />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField select label="Type de local" fullWidth value={roomType} onChange={(e) => setRoomType(e.target.value)}>
                {ROOM_TYPES.map((t) => (
                  <MenuItem key={t} value={t}>
                    {t}
                  </MenuItem>
                ))}
              </TextField>
            </Grid>
            <Grid item xs={6} sm={3}>
              <TextField label="Surface (m²)" type="number" fullWidth value={surface} onChange={(e) => setSurface(Number(e.target.value))} />
            </Grid>
            <Grid item xs={6} sm={3}>
              <TextField label="Hauteur (m)" type="number" fullWidth value={height} onChange={(e) => setHeight(Number(e.target.value))} />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField select label="Catégorie d'accès" fullWidth value={access} onChange={(e) => setAccess(e.target.value)}>
                {ACCESS_CATEGORIES.map((t) => (
                  <MenuItem key={t} value={t}>
                    {t}
                  </MenuItem>
                ))}
              </TextField>
            </Grid>
          </Grid>

          {circuits.map((c, idx) => (
            <Paper key={idx} variant="outlined" sx={{ p: 2, mb: 2 }}>
              <Stack direction="row" justifyContent="space-between" alignItems="center">
                <Typography variant="subtitle2">Circuit {idx + 1}</Typography>
                <IconButton
                  size="small"
                  disabled={circuits.length === 1}
                  onClick={() => setCircuits((cs) => cs.filter((_, i) => i !== idx))}
                >
                  <DeleteIcon fontSize="small" />
                </IconButton>
              </Stack>
              <Grid container spacing={1.5} sx={{ mt: 0.5 }}>
                <Grid item xs={12} sm={6}>
                  <TextField label="Nom" fullWidth size="small" value={c.name} onChange={(e) => updateCircuit(idx, { name: e.target.value })} />
                </Grid>
                <Grid item xs={12} sm={6}>
                  <TextField select label="Fluide" fullWidth size="small" value={c.fluid_code} onChange={(e) => updateCircuit(idx, { fluid_code: e.target.value })}>
                    {fluids.map((f) => (
                      <MenuItem key={f.code} value={f.code}>
                        {f.code}
                      </MenuItem>
                    ))}
                  </TextField>
                </Grid>
                <Grid item xs={6} sm={4}>
                  <TextField
                    label="Charge usine (kg)"
                    type="number"
                    fullWidth
                    size="small"
                    value={c.factory_charge_kg}
                    onChange={(e) => updateCircuit(idx, { factory_charge_kg: Number(e.target.value) })}
                  />
                </Grid>
                <Grid item xs={6} sm={4}>
                  <TextField
                    label="Longueur réseau (m)"
                    type="number"
                    fullWidth
                    size="small"
                    value={c.pipe_length_m}
                    onChange={(e) => updateCircuit(idx, { pipe_length_m: Number(e.target.value) })}
                  />
                </Grid>
                <Grid item xs={6} sm={4}>
                  <TextField
                    label="Longueur équivalente (m)"
                    type="number"
                    fullWidth
                    size="small"
                    value={c.equivalent_length_m ?? ""}
                    onChange={(e) =>
                      updateCircuit(idx, { equivalent_length_m: e.target.value ? Number(e.target.value) : undefined })
                    }
                  />
                </Grid>
                <Grid item xs={6} sm={4}>
                  <TextField
                    label="Charge complémentaire (kg/m)"
                    type="number"
                    fullWidth
                    size="small"
                    value={c.additional_charge_kg_per_m}
                    onChange={(e) => updateCircuit(idx, { additional_charge_kg_per_m: Number(e.target.value) })}
                  />
                </Grid>
                <Grid item xs={6} sm={4}>
                  <TextField
                    label="Altimétrie (m)"
                    type="number"
                    fullWidth
                    size="small"
                    value={c.altimetry_m}
                    onChange={(e) => updateCircuit(idx, { altimetry_m: Number(e.target.value) })}
                  />
                </Grid>
                <Grid item xs={6} sm={4}>
                  <TextField
                    label="Nb zones"
                    type="number"
                    fullWidth
                    size="small"
                    value={c.zone_count}
                    onChange={(e) => updateCircuit(idx, { zone_count: Number(e.target.value) })}
                  />
                </Grid>
                <Grid item xs={6} sm={4}>
                  <TextField
                    label="Nb unités intérieures"
                    type="number"
                    fullWidth
                    size="small"
                    value={c.indoor_unit_count}
                    onChange={(e) => updateCircuit(idx, { indoor_unit_count: Number(e.target.value) })}
                  />
                </Grid>
              </Grid>
            </Paper>
          ))}

          <Button
            startIcon={<AddIcon />}
            onClick={() => setCircuits((cs) => [...cs, emptyCircuit(fluids[0]?.code ?? "R32")])}
          >
            Ajouter un circuit
          </Button>

          {error && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {error}
            </Alert>
          )}

          <Box sx={{ mt: 2 }}>
            <Button variant="contained" size="large" onClick={submit}>
              Calculer
            </Button>
          </Box>
        </Paper>
      </Grid>

      <Grid item xs={12} lg={5}>
        {result && (
          <Stack spacing={2}>
            <Paper sx={{ p: 3 }}>
              <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
                <Typography variant="h6">Résultat NF EN 378-1</Typography>
                <RiskIndicator conformity={result.concentration.conformity} />
              </Stack>
              <Box sx={{ display: "flex", justifyContent: "center", mb: 2 }}>
                <ComplianceGauge marginRatio={result.concentration.margin_ratio} conformity={result.concentration.conformity} />
              </Box>
              <Table size="small">
                <TableBody>
                  <TableRow>
                    <TableCell>Charge totale</TableCell>
                    <TableCell align="right">{result.total_charge_kg} kg</TableCell>
                  </TableRow>
                  <TableRow>
                    <TableCell>Fluide dominant</TableCell>
                    <TableCell align="right">{result.dominant_fluid_code}</TableCell>
                  </TableRow>
                  <TableRow>
                    <TableCell>Volume local</TableCell>
                    <TableCell align="right">{result.concentration.volume_m3} m³</TableCell>
                  </TableRow>
                  <TableRow>
                    <TableCell>Concentration</TableCell>
                    <TableCell align="right">{result.concentration.concentration_kg_m3} kg/m³</TableCell>
                  </TableRow>
                  <TableRow>
                    <TableCell>Limite ({result.concentration.limit_type})</TableCell>
                    <TableCell align="right">{result.concentration.limit_used_kg_m3} kg/m³</TableCell>
                  </TableRow>
                </TableBody>
              </Table>
            </Paper>

            <Paper sx={{ p: 3 }}>
              <Typography variant="subtitle1" gutterBottom>
                Détail par circuit
              </Typography>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Circuit</TableCell>
                    <TableCell align="right">Charge totale</TableCell>
                    <TableCell align="right">Par UI</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {result.circuits.map((c) => (
                    <TableRow key={c.name}>
                      <TableCell>{c.name}</TableCell>
                      <TableCell align="right">{c.total_charge_kg} kg</TableCell>
                      <TableCell align="right">{c.charge_per_indoor_unit_kg} kg</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Paper>

            <Paper sx={{ p: 3 }}>
              <Typography variant="subtitle1" gutterBottom>
                Recommandations
              </Typography>
              <Stack spacing={1}>
                {result.recommendations.map((rec, i) => (
                  <Alert key={i} severity={rec.priority === "haute" ? "error" : rec.priority === "moyenne" ? "warning" : "info"}>
                    <b>{rec.measure}</b> — {rec.reason}
                  </Alert>
                ))}
              </Stack>
            </Paper>
          </Stack>
        )}
      </Grid>
    </Grid>
  );
}

function MultiRoomPanel({ fluids }: { fluids: Fluid[] }) {
  const { currentProject } = useProject();
  const [rooms, setRooms] = useState<MultiRoomRoomInput[]>([emptyRoom("R32"), emptyRoom("R32")]);
  const [result, setResult] = useState<MultiRoomResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const updateRoom = (idx: number, patch: Partial<MultiRoomRoomInput>) => {
    setRooms((rs) => rs.map((r, i) => (i === idx ? { ...r, ...patch } : r)));
  };

  const submit = async () => {
    setError(null);
    try {
      const r = await api.post<MultiRoomResponse>("/calculations/multi-room", {
        project_id: currentProject?.id,
        rooms,
      });
      setResult(r.data);
    } catch (e: any) {
      setError(extractErrorMessage(e, "Erreur lors du calcul."));
    }
  };

  return (
    <Grid container spacing={3}>
      <Grid item xs={12} lg={7}>
        <Paper sx={{ p: 3 }}>
          <Typography variant="h6" gutterBottom>
            Locaux du projet
          </Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Nom</TableCell>
                <TableCell>Type</TableCell>
                <TableCell>Surface (m²)</TableCell>
                <TableCell>Hauteur (m)</TableCell>
                <TableCell>Fluide</TableCell>
                <TableCell>Charge (kg)</TableCell>
                <TableCell></TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {rooms.map((r, idx) => (
                <TableRow key={idx}>
                  <TableCell>
                    <TextField
                      size="small"
                      value={r.room_name}
                      onChange={(e) => updateRoom(idx, { room_name: e.target.value })}
                    />
                  </TableCell>
                  <TableCell>
                    <TextField select size="small" value={r.room_type} onChange={(e) => updateRoom(idx, { room_type: e.target.value })} sx={{ minWidth: 130 }}>
                      {ROOM_TYPES.map((t) => (
                        <MenuItem key={t} value={t}>
                          {t}
                        </MenuItem>
                      ))}
                    </TextField>
                  </TableCell>
                  <TableCell>
                    <TextField
                      size="small"
                      type="number"
                      sx={{ width: 90 }}
                      value={r.surface_m2}
                      onChange={(e) => updateRoom(idx, { surface_m2: Number(e.target.value) })}
                    />
                  </TableCell>
                  <TableCell>
                    <TextField
                      size="small"
                      type="number"
                      sx={{ width: 80 }}
                      value={r.height_m}
                      onChange={(e) => updateRoom(idx, { height_m: Number(e.target.value) })}
                    />
                  </TableCell>
                  <TableCell>
                    <TextField select size="small" sx={{ minWidth: 90 }} value={r.fluid_code} onChange={(e) => updateRoom(idx, { fluid_code: e.target.value })}>
                      {fluids.map((f) => (
                        <MenuItem key={f.code} value={f.code}>
                          {f.code}
                        </MenuItem>
                      ))}
                    </TextField>
                  </TableCell>
                  <TableCell>
                    <TextField
                      size="small"
                      type="number"
                      sx={{ width: 90 }}
                      value={r.charge_kg}
                      onChange={(e) => updateRoom(idx, { charge_kg: Number(e.target.value) })}
                    />
                  </TableCell>
                  <TableCell>
                    <IconButton size="small" disabled={rooms.length === 1} onClick={() => setRooms((rs) => rs.filter((_, i) => i !== idx))}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <Button startIcon={<AddIcon />} sx={{ mt: 1 }} onClick={() => setRooms((rs) => [...rs, emptyRoom(fluids[0]?.code ?? "R32")])}>
            Ajouter un local
          </Button>

          {error && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {error}
            </Alert>
          )}

          <Box sx={{ mt: 2 }}>
            <Button variant="contained" size="large" onClick={submit}>
              Analyser
            </Button>
          </Box>
        </Paper>
      </Grid>

      <Grid item xs={12} lg={5}>
        {result && (
          <Stack spacing={2}>
            <Paper sx={{ p: 3 }}>
              <Typography variant="h6" gutterBottom>
                Conformité globale
              </Typography>
              <Stack direction="row" spacing={2} alignItems="center" sx={{ mb: 1 }}>
                <RiskIndicator conformity={result.summary.global_conformity} />
                <Typography color="text.secondary">
                  Local le plus pénalisant : <b>{result.summary.worst_room}</b>
                </Typography>
              </Stack>
              <RoomHeatmap rooms={result.summary.rooms} />
            </Paper>
            <Paper sx={{ p: 3 }}>
              <Typography variant="h6" gutterBottom>
                Diagramme de concentration
              </Typography>
              <ConcentrationChart rooms={result.summary.rooms} />
            </Paper>
          </Stack>
        )}
      </Grid>
    </Grid>
  );
}
