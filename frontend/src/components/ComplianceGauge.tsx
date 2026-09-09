import { Box, Typography } from "@mui/material";
import { CONFORMITY_COLORS } from "../theme";

interface Props {
  marginRatio: number; // concentration / limite (1.0 = à la limite)
  conformity: string;
}

/** Jauge semi-circulaire de conformité NF EN 378-1. */
export default function ComplianceGauge({ marginRatio, conformity }: Props) {
  const clamped = Math.min(marginRatio, 2);
  const pct = Math.min(clamped / 2, 1); // 0..1 across the 0..200% range
  const angle = -90 + pct * 180;
  const color = CONFORMITY_COLORS[conformity] ?? "#616161";

  const size = 180;
  const cx = size / 2;
  const cy = size / 2;
  const r = 70;

  const needleX = cx + r * 0.85 * Math.cos((angle * Math.PI) / 180);
  const needleY = cy + r * 0.85 * Math.sin((angle * Math.PI) / 180);

  const arc = (startPct: number, endPct: number, arcColor: string) => {
    const a1 = -90 + startPct * 180;
    const a2 = -90 + endPct * 180;
    const x1 = cx + r * Math.cos((a1 * Math.PI) / 180);
    const y1 = cy + r * Math.sin((a1 * Math.PI) / 180);
    const x2 = cx + r * Math.cos((a2 * Math.PI) / 180);
    const y2 = cy + r * Math.sin((a2 * Math.PI) / 180);
    return (
      <path
        d={`M ${x1} ${y1} A ${r} ${r} 0 0 1 ${x2} ${y2}`}
        stroke={arcColor}
        strokeWidth={16}
        fill="none"
        strokeLinecap="butt"
      />
    );
  };

  return (
    <Box sx={{ display: "flex", flexDirection: "column", alignItems: "center" }}>
      <svg width={size} height={size / 1.7} viewBox={`0 0 ${size} ${size / 1.7 + 10}`}>
        {arc(0, 0.5, "#2e7d32")}
        {arc(0.5, 0.75, "#ed6c02")}
        {arc(0.75, 1, "#c62828")}
        <line
          x1={cx}
          y1={cy}
          x2={needleX}
          y2={needleY}
          stroke="#212121"
          strokeWidth={3}
          strokeLinecap="round"
        />
        <circle cx={cx} cy={cy} r={5} fill="#212121" />
      </svg>
      <Typography variant="h5" fontWeight={700} sx={{ color, mt: -1 }}>
        {(marginRatio * 100).toFixed(0)}%
      </Typography>
      <Typography variant="caption" color="text.secondary">
        concentration / limite applicable
      </Typography>
    </Box>
  );
}
