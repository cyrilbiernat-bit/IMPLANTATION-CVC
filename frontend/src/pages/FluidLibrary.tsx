import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import EditIcon from "@mui/icons-material/Edit";
import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Grid,
  IconButton,
  MenuItem,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { api } from "../api/client";
import { Fluid } from "../types";

const SAFETY_GROUPS = ["A1", "A2L", "A2", "A3", "B1", "B2L", "B2"];

const EMPTY_FORM = {
  code: "",
  name: "",
  safety_group: "A2L",
  lfl_kg_m3: undefined as number | undefined,
  rcl_kg_m3: undefined as number | undefined,
  atel_kg_m3: undefined as number | undefined,
  odl_kg_m3: undefined as number | undefined,
  gwp: undefined as number | undefined,
  molar_mass_g_mol: undefined as number | undefined,
  source: "",
};

export default function FluidLibrary() {
  const [fluids, setFluids] = useState<Fluid[]>([]);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<Fluid | null>(null);
  const [form, setForm] = useState(EMPTY_FORM);

  const load = () => api.get<Fluid[]>("/fluids").then((r) => setFluids(r.data));

  useEffect(() => {
    load();
  }, []);

  const openCreate = () => {
    setEditing(null);
    setForm(EMPTY_FORM);
    setDialogOpen(true);
  };

  const openEdit = (f: Fluid) => {
    setEditing(f);
    setForm({
      code: f.code,
      name: f.name,
      safety_group: f.safety_group,
      lfl_kg_m3: f.lfl_kg_m3 ?? undefined,
      rcl_kg_m3: f.rcl_kg_m3 ?? undefined,
      atel_kg_m3: f.atel_kg_m3 ?? undefined,
      odl_kg_m3: f.odl_kg_m3 ?? undefined,
      gwp: f.gwp ?? undefined,
      molar_mass_g_mol: f.molar_mass_g_mol ?? undefined,
      source: f.source ?? "",
    });
    setDialogOpen(true);
  };

  const save = async () => {
    if (editing) {
      await api.put(`/fluids/${editing.id}`, form);
    } else {
      await api.post("/fluids", form);
    }
    setDialogOpen(false);
    load();
  };

  const remove = async (f: Fluid) => {
    await api.delete(`/fluids/${f.id}`);
    load();
  };

  return (
    <Box>
      <Alert severity="warning" sx={{ mb: 2 }}>
        Les valeurs LFL / RCL / ATEL / ODL fournies par défaut sont indicatives (ASHRAE 34 /
        ISO 817). Elles doivent être vérifiées par un professionnel qualifié par rapport à
        l'édition en vigueur de la norme NF EN 378-1 et aux FDS constructeur avant toute
        utilisation réglementaire. Cette bibliothèque est entièrement éditable.
      </Alert>

      <Paper sx={{ p: 3 }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
          <Typography variant="h6">Fluides frigorigènes ({fluids.length})</Typography>
          <Button startIcon={<AddIcon />} variant="contained" onClick={openCreate}>
            Ajouter un fluide
          </Button>
        </Box>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Code</TableCell>
              <TableCell>Désignation</TableCell>
              <TableCell>Groupe</TableCell>
              <TableCell align="right">LFL</TableCell>
              <TableCell align="right">RCL</TableCell>
              <TableCell align="right">ATEL</TableCell>
              <TableCell align="right">ODL</TableCell>
              <TableCell align="right">GWP</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {fluids.map((f) => (
              <TableRow key={f.id}>
                <TableCell>
                  <b>{f.code}</b>
                </TableCell>
                <TableCell>{f.name}</TableCell>
                <TableCell>
                  <Chip size="small" label={f.safety_group} color={f.safety_group.startsWith("A1") ? "default" : "warning"} />
                </TableCell>
                <TableCell align="right">{f.lfl_kg_m3 ?? "-"}</TableCell>
                <TableCell align="right">{f.rcl_kg_m3 ?? "-"}</TableCell>
                <TableCell align="right">{f.atel_kg_m3 ?? "-"}</TableCell>
                <TableCell align="right">{f.odl_kg_m3 ?? "-"}</TableCell>
                <TableCell align="right">{f.gwp ?? "-"}</TableCell>
                <TableCell align="right">
                  <IconButton size="small" onClick={() => openEdit(f)}>
                    <EditIcon fontSize="small" />
                  </IconButton>
                  <IconButton size="small" onClick={() => remove(f)}>
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>

      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{editing ? `Modifier ${editing.code}` : "Ajouter un fluide"}</DialogTitle>
        <DialogContent>
          <Grid container spacing={2} sx={{ mt: 0.5 }}>
            <Grid item xs={6}>
              <TextField label="Code" fullWidth value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} disabled={!!editing} />
            </Grid>
            <Grid item xs={6}>
              <TextField select label="Groupe de sécurité" fullWidth value={form.safety_group} onChange={(e) => setForm({ ...form, safety_group: e.target.value })}>
                {SAFETY_GROUPS.map((g) => (
                  <MenuItem key={g} value={g}>
                    {g}
                  </MenuItem>
                ))}
              </TextField>
            </Grid>
            <Grid item xs={12}>
              <TextField label="Désignation" fullWidth value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
            </Grid>
            <Grid item xs={6}>
              <TextField
                label="LFL (kg/m³)"
                type="number"
                fullWidth
                value={form.lfl_kg_m3 ?? ""}
                onChange={(e) => setForm({ ...form, lfl_kg_m3: e.target.value ? Number(e.target.value) : undefined })}
              />
            </Grid>
            <Grid item xs={6}>
              <TextField
                label="RCL (kg/m³)"
                type="number"
                fullWidth
                value={form.rcl_kg_m3 ?? ""}
                onChange={(e) => setForm({ ...form, rcl_kg_m3: e.target.value ? Number(e.target.value) : undefined })}
              />
            </Grid>
            <Grid item xs={6}>
              <TextField
                label="ATEL (kg/m³)"
                type="number"
                fullWidth
                value={form.atel_kg_m3 ?? ""}
                onChange={(e) => setForm({ ...form, atel_kg_m3: e.target.value ? Number(e.target.value) : undefined })}
              />
            </Grid>
            <Grid item xs={6}>
              <TextField
                label="ODL (kg/m³)"
                type="number"
                fullWidth
                value={form.odl_kg_m3 ?? ""}
                onChange={(e) => setForm({ ...form, odl_kg_m3: e.target.value ? Number(e.target.value) : undefined })}
              />
            </Grid>
            <Grid item xs={6}>
              <TextField
                label="GWP"
                type="number"
                fullWidth
                value={form.gwp ?? ""}
                onChange={(e) => setForm({ ...form, gwp: e.target.value ? Number(e.target.value) : undefined })}
              />
            </Grid>
            <Grid item xs={6}>
              <TextField
                label="Masse molaire (g/mol)"
                type="number"
                fullWidth
                value={form.molar_mass_g_mol ?? ""}
                onChange={(e) => setForm({ ...form, molar_mass_g_mol: e.target.value ? Number(e.target.value) : undefined })}
              />
            </Grid>
            <Grid item xs={12}>
              <TextField label="Source" fullWidth value={form.source} onChange={(e) => setForm({ ...form, source: e.target.value })} />
            </Grid>
          </Grid>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>Annuler</Button>
          <Button variant="contained" onClick={save}>
            Enregistrer
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
