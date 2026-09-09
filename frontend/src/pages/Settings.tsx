import {
  Alert,
  Avatar,
  Box,
  Button,
  Grid,
  Paper,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useEffect, useRef, useState } from "react";
import { api } from "../api/client";
import { CompanySettings } from "../types";

export default function Settings() {
  const [settings, setSettings] = useState<CompanySettings | null>(null);
  const [saved, setSaved] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const load = () => api.get<CompanySettings>("/settings").then((r) => setSettings(r.data));

  useEffect(() => {
    load();
  }, []);

  const save = async () => {
    if (!settings) return;
    await api.put("/settings", {
      company_name: settings.company_name,
      address: settings.address,
      contact_email: settings.contact_email,
      contact_phone: settings.contact_phone,
    });
    setSaved(true);
    setTimeout(() => setSaved(false), 3000);
  };

  const uploadLogo = async (file: File) => {
    const formData = new FormData();
    formData.append("file", file);
    await api.post("/settings/logo", formData, { headers: { "Content-Type": "multipart/form-data" } });
    load();
  };

  if (!settings) return null;

  return (
    <Grid container spacing={3}>
      <Grid item xs={12} md={7}>
        <Paper sx={{ p: 3 }}>
          <Typography variant="h6" gutterBottom>
            Coordonnées du bureau d'études
          </Typography>
          <Typography color="text.secondary" sx={{ mb: 2 }}>
            Ces informations apparaissent en page de garde des rapports générés (PDF/DOCX).
          </Typography>
          <Stack spacing={2}>
            <TextField
              label="Nom de la société"
              value={settings.company_name ?? ""}
              onChange={(e) => setSettings({ ...settings, company_name: e.target.value })}
            />
            <TextField
              label="Adresse"
              value={settings.address ?? ""}
              onChange={(e) => setSettings({ ...settings, address: e.target.value })}
            />
            <TextField
              label="E-mail de contact"
              value={settings.contact_email ?? ""}
              onChange={(e) => setSettings({ ...settings, contact_email: e.target.value })}
            />
            <TextField
              label="Téléphone"
              value={settings.contact_phone ?? ""}
              onChange={(e) => setSettings({ ...settings, contact_phone: e.target.value })}
            />
            {saved && <Alert severity="success">Paramètres enregistrés.</Alert>}
            <Box>
              <Button variant="contained" onClick={save}>
                Enregistrer
              </Button>
            </Box>
          </Stack>
        </Paper>
      </Grid>
      <Grid item xs={12} md={5}>
        <Paper sx={{ p: 3 }}>
          <Typography variant="h6" gutterBottom>
            Logo société
          </Typography>
          <Stack spacing={2} alignItems="flex-start">
            <Avatar
              variant="rounded"
              sx={{ width: 96, height: 96, bgcolor: "grey.200" }}
              src={settings.logo_path ? `/api/settings/logo-file` : undefined}
            >
              {!settings.logo_path && "Logo"}
            </Avatar>
            <input
              ref={fileInputRef}
              type="file"
              accept="image/*"
              hidden
              onChange={(e) => {
                if (e.target.files?.[0]) uploadLogo(e.target.files[0]);
              }}
            />
            <Button variant="outlined" onClick={() => fileInputRef.current?.click()}>
              Importer un logo
            </Button>
          </Stack>
        </Paper>
      </Grid>
    </Grid>
  );
}
