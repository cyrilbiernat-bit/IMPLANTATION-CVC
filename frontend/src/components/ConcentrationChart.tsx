import { Box, Stack, Typography } from "@mui/material";
import { CONFORMITY_COLORS } from "../theme";

interface RoomEntry {
  room_name: string;
  conformity: string;
  concentration_kg_m3: number;
  limit_used_kg_m3: number;
}

/** Diagramme en barres comparant la concentration calculée à la limite, par local. */
export default function ConcentrationChart({ rooms }: { rooms: RoomEntry[] }) {
  if (!rooms.length) return null;
  const max = Math.max(...rooms.map((r) => Math.max(r.concentration_kg_m3, r.limit_used_kg_m3))) * 1.1;

  return (
    <Stack spacing={1.5}>
      {rooms.map((r) => {
        const color = CONFORMITY_COLORS[r.conformity] ?? "#616161";
        const concPct = max ? (r.concentration_kg_m3 / max) * 100 : 0;
        const limitPct = max ? (r.limit_used_kg_m3 / max) * 100 : 0;
        return (
          <Box key={r.room_name}>
            <Stack direction="row" justifyContent="space-between">
              <Typography variant="body2" fontWeight={600}>
                {r.room_name}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {r.concentration_kg_m3.toFixed(4)} / {r.limit_used_kg_m3.toFixed(4)} kg/m³
              </Typography>
            </Stack>
            <Box sx={{ position: "relative", height: 14, bgcolor: "#eceff1", borderRadius: 1, mt: 0.5 }}>
              <Box
                sx={{
                  position: "absolute",
                  left: 0,
                  top: 0,
                  bottom: 0,
                  width: `${Math.min(concPct, 100)}%`,
                  bgcolor: color,
                  borderRadius: 1,
                }}
              />
              <Box
                sx={{
                  position: "absolute",
                  left: `${Math.min(limitPct, 100)}%`,
                  top: -2,
                  bottom: -2,
                  width: 2,
                  bgcolor: "#212121",
                }}
              />
            </Box>
          </Box>
        );
      })}
    </Stack>
  );
}
