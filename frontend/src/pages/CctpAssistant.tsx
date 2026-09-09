import SmartToyIcon from "@mui/icons-material/SmartToy";
import {
  Alert,
  Box,
  Button,
  Chip,
  Grid,
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
import { useState } from "react";
import { api } from "../api/client";
import { CctpRoomExtracted } from "../types";

export default function CctpAssistant() {
  const [text, setText] = useState("");
  const [rooms, setRooms] = useState<CctpRoomExtracted[]>([]);
  const [engine, setEngine] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const analyze = async () => {
    setLoading(true);
    try {
      const r = await api.post("/ai/analyze-cctp", { text });
      setRooms(r.data.rooms ?? []);
      setEngine(r.data.engine ?? null);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Grid container spacing={3}>
      <Grid item xs={12} md={6}>
        <Paper sx={{ p: 3 }}>
          <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 2 }}>
            <SmartToyIcon color="primary" />
            <Typography variant="h6">Analyse automatique d'un CCTP</Typography>
          </Stack>
          <Alert severity="info" sx={{ mb: 2 }}>
            Collez le texte d'un CCTP ci-dessous. L'assistant identifie les locaux mentionnés
            (nom, type, surface) et propose un type de système CVC pour chacun. Sans clé API
            configurée (ANTHROPIC_API_KEY), un moteur d'extraction heuristique local est utilisé.
          </Alert>
          <TextField
            label="Texte du CCTP"
            multiline
            minRows={12}
            fullWidth
            value={text}
            onChange={(e) => setText(e.target.value)}
            placeholder={"Exemple :\nBureau direction : 25 m2\nSalle de réunion : 18 m2\nLocal technique : 6 m2"}
          />
          <Box sx={{ mt: 2 }}>
            <Button variant="contained" onClick={analyze} disabled={loading || !text.trim()}>
              {loading ? "Analyse en cours..." : "Analyser le CCTP"}
            </Button>
          </Box>
        </Paper>
      </Grid>
      <Grid item xs={12} md={6}>
        <Paper sx={{ p: 3 }}>
          <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
            <Typography variant="h6">Locaux extraits</Typography>
            {engine && <Chip size="small" label={`moteur : ${engine}`} />}
          </Stack>
          {rooms.length === 0 && (
            <Typography color="text.secondary">
              Aucun local extrait pour le moment. Lancez une analyse pour peupler cette liste ;
              vous pourrez ensuite reporter ces locaux dans le mode Calcul rapide ou l'analyse
              multilocaux.
            </Typography>
          )}
          {rooms.length > 0 && (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Local</TableCell>
                  <TableCell>Type</TableCell>
                  <TableCell align="right">Surface (m²)</TableCell>
                  <TableCell>Système suggéré</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {rooms.map((r, i) => (
                  <TableRow key={i}>
                    <TableCell>{r.room_name}</TableCell>
                    <TableCell>{r.room_type}</TableCell>
                    <TableCell align="right">{r.surface_m2 ?? "-"}</TableCell>
                    <TableCell>
                      <Chip size="small" label={r.suggested_system_type ?? "-"} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </Paper>
      </Grid>
    </Grid>
  );
}
