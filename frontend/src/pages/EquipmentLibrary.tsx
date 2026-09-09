import {
  Box,
  Chip,
  Grid,
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
import { useEffect, useMemo, useState } from "react";
import { api } from "../api/client";
import { Equipment, Manufacturer } from "../types";

export default function EquipmentLibrary() {
  const [manufacturers, setManufacturers] = useState<Manufacturer[]>([]);
  const [equipment, setEquipment] = useState<Equipment[]>([]);
  const [manufacturerFilter, setManufacturerFilter] = useState("");
  const [systemFilter, setSystemFilter] = useState("");

  useEffect(() => {
    api.get<Manufacturer[]>("/equipment/manufacturers").then((r) => setManufacturers(r.data));
  }, []);

  useEffect(() => {
    const params: Record<string, string> = {};
    if (manufacturerFilter) params.manufacturer = manufacturerFilter;
    if (systemFilter) params.system_type = systemFilter;
    api.get<Equipment[]>("/equipment", { params }).then((r) => setEquipment(r.data));
  }, [manufacturerFilter, systemFilter]);

  const manufacturerNameById = useMemo(() => {
    const map: Record<number, string> = {};
    manufacturers.forEach((m) => (map[m.id] = m.name));
    return map;
  }, [manufacturers]);

  const systemTypes = useMemo(() => Array.from(new Set(equipment.map((e) => e.system_type))), [equipment]);

  return (
    <Box>
      <Paper sx={{ p: 3, mb: 2 }}>
        <Grid container spacing={2}>
          <Grid item xs={12} sm={6}>
            <TextField select label="Constructeur" fullWidth value={manufacturerFilter} onChange={(e) => setManufacturerFilter(e.target.value)}>
              <MenuItem value="">Tous les constructeurs</MenuItem>
              {manufacturers.map((m) => (
                <MenuItem key={m.id} value={m.name}>
                  {m.name}
                </MenuItem>
              ))}
            </TextField>
          </Grid>
          <Grid item xs={12} sm={6}>
            <TextField select label="Type de système" fullWidth value={systemFilter} onChange={(e) => setSystemFilter(e.target.value)}>
              <MenuItem value="">Tous les types</MenuItem>
              {systemTypes.map((s) => (
                <MenuItem key={s} value={s}>
                  {s}
                </MenuItem>
              ))}
            </TextField>
          </Grid>
        </Grid>
      </Paper>

      <Paper sx={{ p: 3 }}>
        <Typography variant="h6" gutterBottom>
          Équipements ({equipment.length})
        </Typography>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Constructeur</TableCell>
              <TableCell>Référence</TableCell>
              <TableCell>Type</TableCell>
              <TableCell>Fluide</TableCell>
              <TableCell align="right">Puissance froid</TableCell>
              <TableCell align="right">Puissance chaud</TableCell>
              <TableCell align="right">Charge usine</TableCell>
              <TableCell align="right">Charge compl. (kg/m)</TableCell>
              <TableCell align="right">Long. max</TableCell>
              <TableCell align="right">Long. équiv. max</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {equipment.map((e) => (
              <TableRow key={e.id}>
                <TableCell>{manufacturerNameById[e.manufacturer_id] ?? "-"}</TableCell>
                <TableCell>{e.reference}</TableCell>
                <TableCell>
                  <Chip size="small" label={e.system_type} />
                </TableCell>
                <TableCell>{e.fluid_code}</TableCell>
                <TableCell align="right">{e.cooling_power_kw} kW</TableCell>
                <TableCell align="right">{e.heating_power_kw ?? "-"} kW</TableCell>
                <TableCell align="right">{e.factory_charge_kg} kg</TableCell>
                <TableCell align="right">{e.additional_charge_kg_per_m}</TableCell>
                <TableCell align="right">{e.max_pipe_length_m ?? "-"} m</TableCell>
                <TableCell align="right">{e.max_equivalent_length_m ?? "-"} m</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>
    </Box>
  );
}
