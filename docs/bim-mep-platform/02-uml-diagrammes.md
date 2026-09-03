# 2. Diagrammes UML

## 2.1 Diagramme de classes — Modèle BIM central

```mermaid
classDiagram
    class BimElement {
        <<abstract>>
        +Guid Id
        +string IfcGuid
        +string Name
        +Level Level
        +Dictionary~string,ParameterValue~ Parameters
        +Transform3D Placement
        +Geometry3D Geometry
        +DateTime CreatedAt
        +string CreatedBy
        +int RevisionNumber
        +GetIfcType() string
        +Validate() ValidationResult
    }

    class Family {
        +Guid Id
        +string Name
        +string Category
        +List~FamilyParameter~ ParameterDefinitions
        +List~FamilyType~ Types
    }

    class FamilyType {
        +Guid Id
        +string Name
        +Family ParentFamily
        +Dictionary~string,ParameterValue~ TypeParameters
        +GeometryTemplate Template
        +CreateOccurrence(Transform3D) BimElement
    }

    class MepDuct {
        +DuctShape Shape
        +double Width
        +double Height
        +double Diameter
        +double Length
        +Material Material
        +Insulation Insulation
        +List~MepConnector~ Connectors
        +Recompute() void
    }

    class MepPipe {
        +double DiameterNominal
        +PipeSystemType SystemType
        +Material Material
        +double Slope
        +List~MepConnector~ Connectors
    }

    class CableTray {
        +double Width
        +double Height
        +TrayType Type
        +List~Cable~ Cables
    }

    class MepEquipment {
        +string ManufacturerRef
        +ManufacturerFamily Manufacturer
        +Dictionary~string,double~ PerformanceCurve
        +List~MepConnector~ Connectors
    }

    class MepConnector {
        +Guid Id
        +ConnectorType Type
        +Point3D Position
        +Vector3D Direction
        +double Size
        +MepConnector ConnectedTo
        +SystemClassification System
    }

    class MepNetwork {
        +Guid Id
        +NetworkKind Kind
        +List~BimElement~ Members
        +Graph3D TopologyGraph
        +Recompute() void
        +ComputeLosses() LossReport
    }

    class Room {
        +Guid Id
        +string Name
        +Level Level
        +Polygon2D Boundary
        +double Area
        +double Volume
        +ThermalLoad Load
    }

    class Level {
        +Guid Id
        +string Name
        +double Elevation
        +double Height
    }

    class Project {
        +Guid Id
        +string Name
        +List~Level~ Levels
        +List~BimElement~ Elements
        +List~MepNetwork~ Networks
        +ProjectPhase Phase
        +LodTarget CurrentLod
    }

    BimElement <|-- MepDuct
    BimElement <|-- MepPipe
    BimElement <|-- CableTray
    BimElement <|-- MepEquipment
    BimElement <|-- Room
    FamilyType "1" --> "*" BimElement : instancie
    Family "1" --> "*" FamilyType
    MepDuct "1" --> "*" MepConnector
    MepPipe "1" --> "*" MepConnector
    MepEquipment "1" --> "*" MepConnector
    MepConnector "0..1" --> "0..1" MepConnector : connecté à
    MepNetwork "1" --> "*" BimElement
    Project "1" --> "*" Level
    Project "1" --> "*" BimElement
    Project "1" --> "*" MepNetwork
    Room "1" --> "1" Level
```

## 2.2 Diagramme de classes — Routage & Clash Detection

```mermaid
classDiagram
    class RoutingGraph3D {
        +List~RoutingNode~ Nodes
        +List~RoutingEdge~ Edges
        +AddObstacle(Geometry3D) void
        +BuildFromScene(Scene) void
    }

    class RoutingNode {
        +Point3D Position
        +bool IsObstacle
        +double Cost
    }

    class RoutingEdge {
        +RoutingNode From
        +RoutingNode To
        +double Weight
        +double PressureLossFactor
    }

    class PathFinder {
        <<interface>>
        +FindPath(Start, End, Constraints) Path3D
    }

    class AStarPathFinder {
        +Heuristic HeuristicFn
        +FindPath(Start, End, Constraints) Path3D
    }

    class DijkstraPathFinder {
        +FindPath(Start, End, Constraints) Path3D
        +FindShortestTree(Sources) Path3D[]
    }

    class RoutingConstraints {
        +double MaxSlope
        +double MinClearance
        +List~SystemType~ AllowedCrossings
        +double MaxPressureLoss
    }

    class RoutingService {
        +RouteNetwork(MepNetwork, RoutingConstraints) MepNetwork
        +OptimizeForWeight(MepNetwork) MepNetwork
        +OptimizeForPressureLoss(MepNetwork) MepNetwork
    }

    PathFinder <|.. AStarPathFinder
    PathFinder <|.. DijkstraPathFinder
    RoutingService --> PathFinder
    RoutingService --> RoutingGraph3D
    RoutingGraph3D --> RoutingNode
    RoutingGraph3D --> RoutingEdge

    class ClashDetector {
        +DetectClashes(List~BimElement~) List~Clash~
        +BuildBvh(List~BimElement~) BvhTree
    }

    class Clash {
        +Guid Id
        +BimElement ElementA
        +BimElement ElementB
        +ClashType Type
        +ClashSeverity Severity
        +Point3D Location
        +double PenetrationDepth
        +ClashResolution SuggestedResolution
    }

    class ClashResolver {
        +ProposeResolution(Clash) ClashResolution
        +ApplyResolution(ClashResolution) void
    }

    class ClashResolution {
        +ResolutionStrategy Strategy
        +Vector3D Offset
        +BimElement AffectedElement
        +bool RequiresRecompute
    }

    ClashDetector --> Clash
    ClashResolver --> ClashResolution
    ClashResolver ..> ClashDetector : consomme
```

## 2.3 Diagramme de composants — Pipeline import PDF/DWG/IFC → maquette

```mermaid
flowchart TB
    subgraph Ingestion
        A1[Adaptateur PDF vectoriel]
        A2[Adaptateur PDF scanné - raster]
        A3[Adaptateur DWG]
        A4[Adaptateur IFC]
    end
    subgraph "Pipeline IA (Python / ONNX)"
        B1[Normalisation<br/>résolution/échelle]
        B2[OCR<br/>textes + cotations]
        B3[Segmentation CV<br/>murs/portes/fenêtres/locaux]
        B4[Vectorisation<br/>polylignes → entités]
        B5[Reconnaissance de symboles<br/>CVC/plb/élec existants]
    end
    subgraph "Reconstruction BIM"
        C1[Générateur de niveaux]
        C2[Générateur de pièces/volumes]
        C3[Générateur de murs/ouvertures]
        C4[Validation géométrique<br/>fermeture de contours]
    end
    A1 --> B1
    A2 --> B1
    A3 --> B4
    A4 --> C1
    B1 --> B2
    B1 --> B3
    B3 --> B4
    B4 --> B5
    B2 --> C2
    B4 --> C3
    B5 --> C3
    C3 --> C4
    C4 --> C1
    C1 --> D[(Modèle BIM<br/>LOD 100/200)]
    C2 --> D
```

## 2.4 Diagramme de séquence — Commande copilote IA

```mermaid
sequenceDiagram
    actor Ing as Ingénieur
    participant UI as Client (Desktop/Web)
    participant Copilot as Copilote IA (NLU + planner)
    participant Cat as Service Catalogue
    participant Bim as Moteur BIM
    participant Route as Service Routage

    Ing->>UI: "Place une CTA de 20 000 m3/h"
    UI->>Copilot: Intent + contexte projet
    Copilot->>Copilot: Parse intention (entité=CTA, débit=20000 m3/h)
    Copilot->>Cat: Recherche familles compatibles (débit, disponibilité)
    Cat-->>Copilot: Liste de références fabricants
    Copilot->>Bim: CreateOccurrence(FamilyType, position proposée)
    Bim-->>Copilot: Élément créé (avec connecteurs)
    Copilot->>Route: RouteNetwork(depuis connecteurs CTA vers réseau existant)
    Route-->>Copilot: Tracé proposé + dimensionnement gaines principales
    Copilot->>Bim: Aperçu (non validé)
    Bim-->>UI: Prévisualisation (élément + réseau, statut "proposition")
    UI-->>Ing: Validation / ajustement
    Ing->>UI: Valider
    UI->>Bim: Commit(transaction)
    Bim-->>UI: Modèle mis à jour + version
```

## 2.5 Diagramme d'états — Cycle de vie d'un élément BIM (LOD)

```mermaid
stateDiagram-v2
    [*] --> LOD100 : Import IA / esquisse
    LOD100 --> LOD200 : Dimensionnement approximatif (APS)
    LOD200 --> LOD300 : Géométrie précise + connexions (APD/PRO)
    LOD300 --> LOD350 : Coordination inter-lots (PRO)
    LOD350 --> LOD400 : Fabrication/installation (EXE)
    LOD400 --> LOD500 : Tel-que-construit (DOE)
    LOD200 --> LOD100 : Retour en esquisse (option écartée)
    LOD300 --> LOD200 : Remise en cause du dimensionnement
    note right of LOD400
        Verrouillage partiel :
        seules les propriétés
        fabricant/installation
        restent modifiables
    end note
```
