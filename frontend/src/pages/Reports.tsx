import DescriptionIcon from "@mui/icons-material/Description";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import TableChartIcon from "@mui/icons-material/TableChart";
import {
  Alert,
  Box,
  Button,
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
import RiskIndicator from "../components/RiskIndicator";
import { useProject } from "../context/ProjectContext";
import { Project } from "../types";

interface CalcHistoryRow {
  id: number;
  mode: string;
  fluid_code: string;
  charge_kg: number;
  concentration_kg_m3: number;
  conformity: string;
  created_at: string;
}

export default function Reports() {
  const { currentProject, setCurrentProject } = useProject();
  const [projects, setProjects] = useState<Project[]>([]);
  const [history, setHistory] = useState<CalcHistoryRow[]>([]);

  useEffect(() => {
    api.get<Project[]>("/projects").then((r) => setProjects(r.data));
  }, []);

  useEffect(() => {
    if (!currentProject) {
      setHistory([]);
      return;
    }
    api
      .get<CalcHistoryRow[]>("/calculations/history", { params: { project_id: currentProject.id } })
      .then((r) => setHistory(r.data));
  }, [currentProject]);

  const download = (format: "pdf" | "docx" | "xlsx") => {
    if (!currentProject) return;
    window.open(`/api/reports/${currentProject.id}/${format}`, "_blank");
  };

  return (
    <Stack spacing={3}>
      <Paper sx={{ p: 3 }}>
        <Typography variant="h6" gutterBottom>
          Projet
        </Typography>
        <TextField
          select
          label="Sélectionner un projet"
          fullWidth
          sx={{ maxWidth: 420 }}
          value={currentProject?.id ?? ""}
          onChange={(e) => {
            const p = projects.find((pr) => pr.id === Number(e.target.value));
            if (p) setCurrentProject({ id: p.id, name: p.name });
          }}
        >
          {projects.map((p) => (
            <MenuItem key={p.id} value={p.id}>
              {p.name}
            </MenuItem>
          ))}
        </TextField>
      </Paper>

      {!currentProject && <Alert severity="info">Sélectionnez un projet pour consulter son historique et générer un rapport.</Alert>}

      {currentProject && (
        <>
          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>
              Historique des calculs — {currentProject.name}
            </Typography>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Date</TableCell>
                  <TableCell>Mode</TableCell>
                  <TableCell>Fluide</TableCell>
                  <TableCell align="right">Charge (kg)</TableCell>
                  <TableCell align="right">Concentration (kg/m³)</TableCell>
                  <TableCell>Conformité</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {history.map((h) => (
                  <TableRow key={h.id}>
                    <TableCell>{new Date(h.created_at).toLocaleString("fr-FR")}</TableCell>
                    <TableCell>{h.mode}</TableCell>
                    <TableCell>{h.fluid_code}</TableCell>
                    <TableCell align="right">{h.charge_kg}</TableCell>
                    <TableCell align="right">{h.concentration_kg_m3}</TableCell>
                    <TableCell>
                      <RiskIndicator conformity={h.conformity} />
                    </TableCell>
                  </TableRow>
                ))}
                {history.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <Typography color="text.secondary">
                        Aucun calcul enregistré pour ce projet. Réalisez un calcul rapide ou
                        expert en associant le projet courant.
                      </Typography>
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </Paper>

          <Paper sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>
              Génération du rapport
            </Typography>
            <Typography color="text.secondary" sx={{ mb: 2 }}>
              Le rapport intègre les données du projet, le résumé des hypothèses, les résultats
              détaillés, les recommandations et les références normatives NF EN 378-1.
            </Typography>
            <Stack direction="row" spacing={2}>
              <Button
                variant="contained"
                startIcon={<PictureAsPdfIcon />}
                onClick={() => download("pdf")}
                disabled={history.length === 0}
              >
                Export PDF
              </Button>
              <Button
                variant="outlined"
                startIcon={<DescriptionIcon />}
                onClick={() => download("docx")}
                disabled={history.length === 0}
              >
                Export DOCX
              </Button>
              <Button
                variant="outlined"
                startIcon={<TableChartIcon />}
                onClick={() => download("xlsx")}
                disabled={history.length === 0}
              >
                Export XLSX
              </Button>
            </Stack>
          </Paper>
        </>
      )}
    </Stack>
  );
}
