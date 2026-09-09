import {
  Alert,
  Box,
  Button,
  Chip,
  Grid,
  MenuItem,
  Paper,
  Stack,
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
import { useProject } from "../context/ProjectContext";
import { Project } from "../types";

const BUILDING_TYPES = [
  { value: "tertiaire", label: "Tertiaire" },
  { value: "residentiel", label: "Résidentiel" },
  { value: "industriel", label: "Industriel" },
  { value: "commercial", label: "Commercial" },
  { value: "erp", label: "ERP" },
  { value: "hotel", label: "Hôtel" },
  { value: "sante", label: "Santé" },
  { value: "logistique", label: "Logistique" },
];

export default function NewProject() {
  const { currentProject, setCurrentProject } = useProject();
  const [projects, setProjects] = useState<Project[]>([]);
  const [form, setForm] = useState({
    name: "",
    client: "",
    address: "",
    building_type: "tertiaire",
    author: "",
    notes: "",
  });
  const [message, setMessage] = useState<string | null>(null);

  const loadProjects = () => {
    api.get<Project[]>("/projects").then((r) => setProjects(r.data));
  };

  useEffect(() => {
    loadProjects();
  }, []);

  const submit = async () => {
    if (!form.name.trim()) {
      setMessage("Le nom du projet est requis.");
      return;
    }
    const r = await api.post<Project>("/projects", form);
    setCurrentProject({ id: r.data.id, name: r.data.name });
    setMessage(`Projet « ${r.data.name} » créé et sélectionné comme projet courant.`);
    setForm({ name: "", client: "", address: "", building_type: "tertiaire", author: "", notes: "" });
    loadProjects();
  };

  return (
    <Stack spacing={3}>
      {currentProject && (
        <Alert severity="info">
          Projet courant : <b>{currentProject.name}</b> (utilisé automatiquement dans les modes
          Calcul rapide / Calcul expert / Rapports).
        </Alert>
      )}

      <Paper sx={{ p: 3 }}>
        <Typography variant="h6" gutterBottom>
          Créer un nouveau projet
        </Typography>
        <Grid container spacing={2}>
          <Grid item xs={12} md={6}>
            <TextField
              label="Nom du projet"
              fullWidth
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
            />
          </Grid>
          <Grid item xs={12} md={6}>
            <TextField
              label="Client"
              fullWidth
              value={form.client}
              onChange={(e) => setForm({ ...form, client: e.target.value })}
            />
          </Grid>
          <Grid item xs={12} md={6}>
            <TextField
              label="Adresse"
              fullWidth
              value={form.address}
              onChange={(e) => setForm({ ...form, address: e.target.value })}
            />
          </Grid>
          <Grid item xs={12} md={6}>
            <TextField
              select
              label="Type de bâtiment"
              fullWidth
              value={form.building_type}
              onChange={(e) => setForm({ ...form, building_type: e.target.value })}
            >
              {BUILDING_TYPES.map((b) => (
                <MenuItem key={b.value} value={b.value}>
                  {b.label}
                </MenuItem>
              ))}
            </TextField>
          </Grid>
          <Grid item xs={12} md={6}>
            <TextField
              label="Rédacteur"
              fullWidth
              value={form.author}
              onChange={(e) => setForm({ ...form, author: e.target.value })}
            />
          </Grid>
          <Grid item xs={12}>
            <TextField
              label="Notes"
              fullWidth
              multiline
              minRows={2}
              value={form.notes}
              onChange={(e) => setForm({ ...form, notes: e.target.value })}
            />
          </Grid>
        </Grid>
        {message && (
          <Alert severity="success" sx={{ mt: 2 }} onClose={() => setMessage(null)}>
            {message}
          </Alert>
        )}
        <Box sx={{ mt: 2 }}>
          <Button variant="contained" onClick={submit}>
            Créer le projet
          </Button>
        </Box>
      </Paper>

      <Paper sx={{ p: 3 }}>
        <Typography variant="h6" gutterBottom>
          Projets existants
        </Typography>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Nom</TableCell>
              <TableCell>Client</TableCell>
              <TableCell>Type de bâtiment</TableCell>
              <TableCell>Mis à jour</TableCell>
              <TableCell align="right">Action</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {projects.map((p) => (
              <TableRow key={p.id} selected={currentProject?.id === p.id}>
                <TableCell>{p.name}</TableCell>
                <TableCell>{p.client ?? "-"}</TableCell>
                <TableCell>{p.building_type ?? "-"}</TableCell>
                <TableCell>{new Date(p.updated_at).toLocaleString("fr-FR")}</TableCell>
                <TableCell align="right">
                  {currentProject?.id === p.id ? (
                    <Chip size="small" color="primary" label="Sélectionné" />
                  ) : (
                    <Button
                      size="small"
                      onClick={() => setCurrentProject({ id: p.id, name: p.name })}
                    >
                      Sélectionner
                    </Button>
                  )}
                </TableCell>
              </TableRow>
            ))}
            {projects.length === 0 && (
              <TableRow>
                <TableCell colSpan={5}>
                  <Typography color="text.secondary">Aucun projet pour le moment.</Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </Paper>
    </Stack>
  );
}
