import { AppBar, Box, Toolbar, Typography } from "@mui/material";
import { Outlet, useLocation } from "react-router-dom";
import Sidebar, { DRAWER_WIDTH } from "./Sidebar";

const TITLES: Record<string, string> = {
  "/nouveau-projet": "Nouveau projet",
  "/calcul-rapide": "Calcul rapide — Prédimensionnement",
  "/calcul-expert": "Calcul expert — Analyse détaillée NF EN 378-1",
  "/bibliotheque-fluides": "Bibliothèque fluides frigorigènes",
  "/bibliotheque-equipements": "Bibliothèque équipements constructeurs",
  "/assistant-ia": "Assistant IA — Analyse de CCTP",
  "/rapports": "Rapports",
  "/parametres": "Paramètres",
};

export default function MainLayout() {
  const location = useLocation();
  const title = TITLES[location.pathname] ?? "Calcul de concentration de fluide frigorigène";

  return (
    <Box sx={{ display: "flex", minHeight: "100vh", bgcolor: "background.default" }}>
      <Sidebar />
      <Box sx={{ flexGrow: 1, width: `calc(100% - ${DRAWER_WIDTH}px)` }}>
        <AppBar position="sticky" color="inherit">
          <Toolbar>
            <Typography variant="h6">{title}</Typography>
          </Toolbar>
        </AppBar>
        <Box sx={{ p: 3 }}>
          <Outlet />
        </Box>
      </Box>
    </Box>
  );
}
