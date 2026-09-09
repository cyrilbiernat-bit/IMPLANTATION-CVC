import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ErrorIcon from "@mui/icons-material/Error";
import WarningIcon from "@mui/icons-material/Warning";
import { Chip } from "@mui/material";
import { CONFORMITY_COLORS } from "../theme";

const ICONS: Record<string, JSX.Element> = {
  Conforme: <CheckCircleIcon fontSize="small" />,
  "Conforme sous conditions": <WarningIcon fontSize="small" />,
  "Non conforme": <ErrorIcon fontSize="small" />,
};

export default function RiskIndicator({ conformity }: { conformity: string }) {
  const color = CONFORMITY_COLORS[conformity] ?? "#616161";
  return (
    <Chip
      icon={ICONS[conformity]}
      label={conformity}
      sx={{
        bgcolor: `${color}1a`,
        color,
        fontWeight: 700,
        border: `1px solid ${color}55`,
        "& .MuiChip-icon": { color },
      }}
    />
  );
}
