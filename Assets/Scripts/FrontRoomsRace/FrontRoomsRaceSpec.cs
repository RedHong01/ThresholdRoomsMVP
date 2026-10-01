using System;
using System.Collections.Generic;

namespace FrontRooms.Race
{
    /// <summary>Roles are semantic; the existing FrontRoomsLevel remains unchanged until integration.</summary>
    public enum RaceNodeRole
    {
        Start,
        Approach,
        Observation,
        Shift,
        Office,
        Pressure,
        Run,
        Threshold,
        Recovery,
        Exit,
    }

    public enum RaceEdgeKind
    {
        Hall,
        Door,
        Window,
        Branch,
    }

    [Serializable]
    public sealed class FrontRoomsRaceNode
    {
        public int id;
        public int mainIndex = -1;
        public RaceNodeRole role;
        public bool isMainRoute;
        public int branchParentId = -1;
        public string label;
        public string readText;
    }

    [Serializable]
    public sealed class FrontRoomsRaceEdge
    {
        public int id;
        public int from;
        public int to;
        public RaceEdgeKind kind;
        public bool isMainRoute;
        public int branchParentId = -1;
        public bool requiresKey;
        public float seconds;
        public float noise;
        public string decision;
    }

    [Serializable]
    public sealed class FrontRoomsRaceValidation
    {
        public bool passed;
        public List<string> errors = new List<string>();
        public int mainNodeCount;
        public int branchNodeCount;
        public float branchRatio;
        public float fastestSeconds;
        public float quietAlternativeSeconds;
        public float routeRatio;
        public int mainEdgeCount;
        public int branchEdgeCount;
        public bool hasDoorDecision;
        public bool hasWindowDecision;
    }

    /// <summary>
    /// A deterministic race slice. It is intentionally data-only so 2D and 3D can consume the
    /// same seed and role graph later without sharing their current render/gameplay code.
    /// </summary>
    [Serializable]
    public sealed class FrontRoomsRaceSpec
    {
        public int seed;
        public uint unsignedSeed;
        public int mainNodeCount;
        public int branchNodeCount;
        public float branchRatio;
        public int startNodeId;
        public int exitNodeId;
        public int shiftNodeId;
        public int officeNodeId;
        public int runNodeId;
        public List<int> mainRoute = new List<int>();
        public List<int> branchNodes = new List<int>();
        public List<FrontRoomsRaceNode> nodes = new List<FrontRoomsRaceNode>();
        public List<FrontRoomsRaceEdge> edges = new List<FrontRoomsRaceEdge>();
        public FrontRoomsRaceValidation validation;
    }

    [Serializable]
    public sealed class FrontRoomsRaceBatchReport
    {
        public string generator = "FrontRoomsRaceGenerator/v1";
        public string purpose = "Data-only procedural race slice; not connected to current gameplay yet.";
        public bool passed;
        public int requestedSeeds;
        public int passedSeeds;
        public List<FrontRoomsRaceSpec> results = new List<FrontRoomsRaceSpec>();
    }
}
