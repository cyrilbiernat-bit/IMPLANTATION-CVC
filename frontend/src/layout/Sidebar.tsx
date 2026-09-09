import AcUnitIcon from "@mui/icons-material/AcUnit";
import AssignmentIcon from "@mui/icons-material/Assignment";
import CalculateIcon from "@mui/icons-material/Calculate";
import InventoryIcon from "@mui/icons-material/Inventory2";
import NoteAddIcon from "@mui/icons-material/NoteAdd";
import SettingsIcon from "@mui/icons-material/Settings";
import SmartToyIcon from "@mui/icons-material/SmartToy";
import SpeedIcon from "@mui/icons-material/Speed";
import {
  Divider,
  Drawer,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Toolbar,
  Typography,
} from "@mui/material";
import { useLocation, useNavigate } from "react-router-dom";

const DRAWER_WIDTH = 260;

const NAV_ITEMS = [
  { label: "Nouveau projet", path: "/nouveau-projet", icon: <NoteAddIcon /> },
  { label: "Calcul rapide", path: "/calcul-rapide", icon: <SpeedIcon /> },
  { label: "Calcul expert", path: "/calcul-expert", icon: <CalculateIcon /> },
  { label: "Bibliothèque fluides", path: "/bibliotheque-fluides", icon: <AcUnitIcon /> },
  { label: "Bibliothèque équipements", path: "/bibliotheque-equipements", icon: <InventoryIcon /> },
  { label: "Assistant IA (CCTP)", path: "/assistant-ia", icon: <SmartToyIcon /> },
  { label: "Rapports", path: "/rapports", icon: <AssignmentIcon /> },
  { label: "Paramètres", path: "/parametres", icon: <SettingsIcon /> },
];

export default function Sidebar() {
  const navigate = useNavigate();
  const location = useLocation();

  return (
    <Drawer
      variant="permanent"
      sx={{
        width: DRAWER_WIDTH,
        flexShrink: 0,
        [`& .MuiDrawer-paper`]: { width: DRAWER_WIDTH, boxSizing: "border-box" },
      }}
    >
      <Toolbar sx={{ display: "flex", alignItems: "center", gap: 1, px: 2 }}>
        <AcUnitIcon color="primary" />
        <Typography variant="subtitle1" fontWeight={700} noWrap>
          CVC EN 378-1
        </Typography>
      </Toolbar>
      <Divider />
      <List sx={{ py: 1 }}>
        {NAV_ITEMS.map((item) => (
          <ListItemButton
            key={item.path}
            selected={location.pathname === item.path}
            onClick={() => navigate(item.path)}
            sx={{ mx: 1, borderRadius: 1.5, mb: 0.5 }}
          >
            <ListItemIcon sx={{ minWidth: 36 }}>{item.icon}</ListItemIcon>
            <ListItemText
              primary={item.label}
              primaryTypographyProps={{ fontSize: 14, fontWeight: 500 }}
            />
          </ListItemButton>
        ))}
      </List>
    </Drawer>
  );
}

export { DRAWER_WIDTH };
