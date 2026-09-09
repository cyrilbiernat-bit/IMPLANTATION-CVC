import { Navigate, Route, Routes } from "react-router-dom";
import MainLayout from "./layout/MainLayout";
import CctpAssistant from "./pages/CctpAssistant";
import EquipmentLibrary from "./pages/EquipmentLibrary";
import ExpertCalc from "./pages/ExpertCalc";
import FluidLibrary from "./pages/FluidLibrary";
import NewProject from "./pages/NewProject";
import QuickCalc from "./pages/QuickCalc";
import Reports from "./pages/Reports";
import Settings from "./pages/Settings";

export default function App() {
  return (
    <Routes>
      <Route element={<MainLayout />}>
        <Route index element={<Navigate to="/nouveau-projet" replace />} />
        <Route path="/nouveau-projet" element={<NewProject />} />
        <Route path="/calcul-rapide" element={<QuickCalc />} />
        <Route path="/calcul-expert" element={<ExpertCalc />} />
        <Route path="/bibliotheque-fluides" element={<FluidLibrary />} />
        <Route path="/bibliotheque-equipements" element={<EquipmentLibrary />} />
        <Route path="/assistant-ia" element={<CctpAssistant />} />
        <Route path="/rapports" element={<Reports />} />
        <Route path="/parametres" element={<Settings />} />
        <Route path="*" element={<Navigate to="/nouveau-projet" replace />} />
      </Route>
    </Routes>
  );
}
