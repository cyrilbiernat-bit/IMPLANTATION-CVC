import { createContext, ReactNode, useContext, useEffect, useState } from "react";

interface CurrentProject {
  id: number;
  name: string;
}

interface ProjectContextValue {
  currentProject: CurrentProject | null;
  setCurrentProject: (p: CurrentProject | null) => void;
}

const ProjectContext = createContext<ProjectContextValue | undefined>(undefined);

const STORAGE_KEY = "cvc_en378_current_project";

export function ProjectProvider({ children }: { children: ReactNode }) {
  const [currentProject, setCurrentProjectState] = useState<CurrentProject | null>(() => {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  });

  useEffect(() => {
    try {
      if (currentProject) {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(currentProject));
      } else {
        localStorage.removeItem(STORAGE_KEY);
      }
    } catch {
      // ignore storage errors (private browsing, quota, etc.)
    }
  }, [currentProject]);

  return (
    <ProjectContext.Provider
      value={{ currentProject, setCurrentProject: setCurrentProjectState }}
    >
      {children}
    </ProjectContext.Provider>
  );
}

export function useProject() {
  const ctx = useContext(ProjectContext);
  if (!ctx) throw new Error("useProject must be used within ProjectProvider");
  return ctx;
}
