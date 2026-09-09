import { Box, Grid, Paper, Typography } from "@mui/material";
import { CONFORMITY_COLORS } from "../theme";

interface RoomEntry {
  room_name: string;
  conformity: string;
  margin_ratio: number;
}

export default function RoomHeatmap({ rooms }: { rooms: RoomEntry[] }) {
  if (!rooms.length) return null;
  return (
    <Grid container spacing={1.5}>
      {rooms.map((r) => {
        const color = CONFORMITY_COLORS[r.conformity] ?? "#616161";
        return (
          <Grid item xs={6} sm={4} md={3} key={r.room_name}>
            <Paper
              variant="outlined"
              sx={{
                p: 1.5,
                borderLeft: `6px solid ${color}`,
                bgcolor: `${color}0d`,
              }}
            >
              <Typography variant="body2" fontWeight={600} noWrap>
                {r.room_name}
              </Typography>
              <Box sx={{ display: "flex", justifyContent: "space-between", mt: 0.5 }}>
                <Typography variant="caption" sx={{ color }} fontWeight={700}>
                  {r.conformity}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {(r.margin_ratio * 100).toFixed(0)}%
                </Typography>
              </Box>
            </Paper>
          </Grid>
        );
      })}
    </Grid>
  );
}
