using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrontRooms.Race
{
    /// <summary>
    /// Produces a small deterministic race graph without touching FrontRoomsLevel or gameplay.
    /// The graph is deliberately role-first: the future 2D/3D adapters can place these roles in
    /// their own coordinate systems while keeping the same seed and route decisions.
    /// </summary>
    public static class FrontRoomsRaceGenerator
    {
        private const int MinMainNodes = 8;
        private const int MaxMainNodes = 12;
        private const float MinBranchRatio = 0.20f;
        private const float MaxBranchRatio = 0.35f;

        private static readonly RaceNodeRole[] FillerRoles =
        {
            RaceNodeRole.Approach,
            RaceNodeRole.Observation,
            RaceNodeRole.Pressure,
            RaceNodeRole.Threshold,
            RaceNodeRole.Recovery,
        };

        private struct RaceRng
        {
            private uint state;

            public RaceRng(int seed)
            {
                state = unchecked((uint)seed);
                if (state == 0u) state = 0xA341316Cu;
            }

            public uint NextUInt()
            {
                // xorshift32 keeps the full signed 32-bit seed space deterministic on every platform.
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state;
            }

            public int Range(int minInclusive, int maxExclusive)
            {
                if (maxExclusive <= minInclusive) return minInclusive;
                return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
            }

            public float Value() => (NextUInt() & 0x00FFFFFFu) / 16777216f;

            public float Range(float minInclusive, float maxInclusive)
            {
                return Mathf.Lerp(minInclusive, maxInclusive, Value());
            }
        }

        public static FrontRoomsRaceSpec Generate(int seed)
        {
            var rng = new RaceRng(seed);
            var spec = new FrontRoomsRaceSpec
            {
                seed = seed,
                unsignedSeed = unchecked((uint)seed),
                mainNodeCount = rng.Range(MinMainNodes, MaxMainNodes + 1),
            };

            var minimumBranches = Mathf.CeilToInt(spec.mainNodeCount * MinBranchRatio);
            var maximumBranches = Mathf.FloorToInt(spec.mainNodeCount * MaxBranchRatio);
            maximumBranches = Mathf.Max(minimumBranches, maximumBranches);
            spec.branchNodeCount = rng.Range(minimumBranches, maximumBranches + 1);
            spec.branchRatio = spec.branchNodeCount / (float)spec.mainNodeCount;

            var shiftIndex = Mathf.Clamp(Mathf.RoundToInt(spec.mainNodeCount * 0.26f), 1, spec.mainNodeCount - 4);
            var officeIndex = Mathf.Clamp(Mathf.RoundToInt(spec.mainNodeCount * 0.50f), shiftIndex + 1, spec.mainNodeCount - 3);
            var runIndex = Mathf.Clamp(Mathf.RoundToInt(spec.mainNodeCount * 0.74f), officeIndex + 1, spec.mainNodeCount - 2);

            spec.shiftNodeId = shiftIndex;
            spec.officeNodeId = officeIndex;
            spec.runNodeId = runIndex;
            spec.startNodeId = 0;
            spec.exitNodeId = spec.mainNodeCount - 1;

            for (var i = 0; i < spec.mainNodeCount; i++)
            {
                var role = i == 0 ? RaceNodeRole.Start :
                    i == spec.exitNodeId ? RaceNodeRole.Exit :
                    i == shiftIndex ? RaceNodeRole.Shift :
                    i == officeIndex ? RaceNodeRole.Office :
                    i == runIndex ? RaceNodeRole.Run :
                    FillerRoles[rng.Range(0, FillerRoles.Length)];

                var node = new FrontRoomsRaceNode
                {
                    id = i,
                    mainIndex = i,
                    role = role,
                    isMainRoute = true,
                    label = LabelFor(role, i + 1),
                    readText = ReadTextFor(role),
                };
                spec.nodes.Add(node);
                spec.mainRoute.Add(i);
            }

            var doorEdgeIndex = officeIndex;
            var windowEdgeIndex = Mathf.Clamp(runIndex - 1, 1, spec.mainNodeCount - 2);
            if (windowEdgeIndex == doorEdgeIndex)
                windowEdgeIndex = Mathf.Min(spec.mainNodeCount - 2, windowEdgeIndex + 1);

            for (var i = 0; i < spec.mainNodeCount - 1; i++)
            {
                var kind = i == doorEdgeIndex ? RaceEdgeKind.Door :
                    i == windowEdgeIndex ? RaceEdgeKind.Window : RaceEdgeKind.Hall;
                var edge = MainEdge(spec, i, kind, ref rng);
                spec.edges.Add(edge);
            }

            AddBranches(spec, ref rng);
            spec.validation = Validate(spec);
            return spec;
        }

        public static FrontRoomsRaceValidation Validate(FrontRoomsRaceSpec spec)
        {
            var result = new FrontRoomsRaceValidation
            {
                mainNodeCount = spec == null ? 0 : spec.mainNodeCount,
                branchNodeCount = spec == null ? 0 : spec.branchNodeCount,
                branchRatio = spec == null ? 0f : spec.branchRatio,
            };

            if (spec == null)
            {
                result.errors.Add("spec is null");
                result.passed = false;
                return result;
            }

            if (spec.mainNodeCount < MinMainNodes || spec.mainNodeCount > MaxMainNodes)
                result.errors.Add("main node count is outside 8–12");
            if (spec.branchRatio < MinBranchRatio || spec.branchRatio > MaxBranchRatio)
                result.errors.Add("branch ratio is outside 20–35%");
            if (spec.nodes.Count != spec.mainNodeCount + spec.branchNodeCount)
                result.errors.Add("node list does not match main + branch counts");
            if (spec.mainRoute.Count != spec.mainNodeCount)
                result.errors.Add("main route does not contain every main node");

            var roles = new Dictionary<RaceNodeRole, int>();
            foreach (var node in spec.nodes)
            {
                if (!roles.ContainsKey(node.role)) roles[node.role] = node.id;
                if (node.id < 0 || node.id >= spec.nodes.Count)
                    result.errors.Add("node id is outside the generated list");
            }
            RequireRole(roles, RaceNodeRole.Start, result);
            RequireRole(roles, RaceNodeRole.Shift, result);
            RequireRole(roles, RaceNodeRole.Office, result);
            RequireRole(roles, RaceNodeRole.Run, result);
            RequireRole(roles, RaceNodeRole.Exit, result);
            if (roles.ContainsKey(RaceNodeRole.Start) && roles[RaceNodeRole.Start] != spec.startNodeId)
                result.errors.Add("start role is not the first main node");
            if (roles.ContainsKey(RaceNodeRole.Exit) && roles[RaceNodeRole.Exit] != spec.exitNodeId)
                result.errors.Add("exit role is not the last main node");
            if (roles.ContainsKey(RaceNodeRole.Shift) && roles.ContainsKey(RaceNodeRole.Office) && roles[RaceNodeRole.Shift] >= roles[RaceNodeRole.Office])
                result.errors.Add("shift must precede office");
            if (roles.ContainsKey(RaceNodeRole.Office) && roles.ContainsKey(RaceNodeRole.Run) && roles[RaceNodeRole.Office] >= roles[RaceNodeRole.Run])
                result.errors.Add("office must precede run");
            if (roles.ContainsKey(RaceNodeRole.Run) && roles.ContainsKey(RaceNodeRole.Exit) && roles[RaceNodeRole.Run] >= roles[RaceNodeRole.Exit])
                result.errors.Add("run must precede exit");

            var mainCost = 0f;
            var hasDoor = false;
            var hasWindow = false;
            var mainEdges = new Dictionary<string, FrontRoomsRaceEdge>();
            foreach (var edge in spec.edges)
            {
                if (edge.from < 0 || edge.from >= spec.nodes.Count || edge.to < 0 || edge.to >= spec.nodes.Count)
                {
                    result.errors.Add("edge endpoint is outside the generated node list");
                    continue;
                }
                if (edge.kind == RaceEdgeKind.Door) hasDoor = true;
                if (edge.kind == RaceEdgeKind.Window) hasWindow = true;
                if (edge.isMainRoute)
                {
                    mainCost += edge.seconds;
                    mainEdges[edge.from + ":" + edge.to] = edge;
                    result.mainEdgeCount++;
                }
                else result.branchEdgeCount++;
            }
            for (var i = 0; i < spec.mainRoute.Count - 1; i++)
            {
                var key = spec.mainRoute[i] + ":" + spec.mainRoute[i + 1];
                if (!mainEdges.ContainsKey(key)) result.errors.Add("main route has a missing edge at index " + i);
            }
            if (!hasDoor) result.errors.Add("main route has no keyed door decision");
            if (!hasWindow) result.errors.Add("main route has no window decision");

            var quiet = float.MaxValue;
            foreach (var branchId in spec.branchNodes)
            {
                var branch = spec.nodes.Find(n => n.id == branchId);
                if (branch == null || branch.branchParentId < 0 || branch.branchParentId + 1 >= spec.mainNodeCount)
                {
                    result.errors.Add("branch node has no valid main parent");
                    continue;
                }
                var detourCost = 0f;
                var detourEdges = 0;
                foreach (var edge in spec.edges)
                {
                    if (!edge.isMainRoute && edge.branchParentId == branch.branchParentId)
                    {
                        detourCost += edge.seconds;
                        detourEdges++;
                    }
                }
                var replacedKey = branch.branchParentId + ":" + (branch.branchParentId + 1);
                FrontRoomsRaceEdge replaced;
                if (mainEdges.TryGetValue(replacedKey, out replaced)) detourCost += mainCost - replaced.seconds;
                if (detourEdges != 2) result.errors.Add("branch node does not have a two-edge detour");
                quiet = Mathf.Min(quiet, detourCost);
            }
            if (quiet == float.MaxValue) quiet = mainCost;
            result.fastestSeconds = mainCost;
            result.quietAlternativeSeconds = quiet;
            result.routeRatio = mainCost <= 0f ? 0f : quiet / mainCost;
            result.hasDoorDecision = hasDoor;
            result.hasWindowDecision = hasWindow;
            if (result.routeRatio < 1.15f || result.routeRatio > 2.40f)
                result.errors.Add("branch detour does not create a useful race-time difference");

            result.passed = result.errors.Count == 0;
            return result;
        }

        private static FrontRoomsRaceEdge MainEdge(FrontRoomsRaceSpec spec, int index, RaceEdgeKind kind, ref RaceRng rng)
        {
            var seconds = kind == RaceEdgeKind.Door ? rng.Range(4.0f, 5.2f) :
                kind == RaceEdgeKind.Window ? rng.Range(1.25f, 1.75f) : rng.Range(1.0f, 1.45f);
            var noise = kind == RaceEdgeKind.Window ? 1f : kind == RaceEdgeKind.Door ? .15f : .05f;
            return new FrontRoomsRaceEdge
            {
                id = spec.edges.Count,
                from = index,
                to = index + 1,
                kind = kind,
                isMainRoute = true,
                requiresKey = kind == RaceEdgeKind.Door,
                seconds = seconds,
                noise = noise,
                decision = kind == RaceEdgeKind.Door ? "key route / quiet" :
                    kind == RaceEdgeKind.Window ? "window route / loud" : "hall / baseline",
            };
        }

        private static void AddBranches(FrontRoomsRaceSpec spec, ref RaceRng rng)
        {
            var candidates = new List<int>();
            for (var i = 1; i < spec.mainNodeCount - 2; i++) candidates.Add(i);
            Shuffle(candidates, ref rng);
            for (var i = 0; i < spec.branchNodeCount; i++)
            {
                var parent = candidates[i % candidates.Count];
                var id = spec.nodes.Count;
                var role = i % 2 == 0 ? RaceNodeRole.Observation : RaceNodeRole.Recovery;
                spec.nodes.Add(new FrontRoomsRaceNode
                {
                    id = id,
                    mainIndex = -1,
                    role = role,
                    isMainRoute = false,
                    branchParentId = parent,
                    label = LabelFor(role, id + 1),
                    readText = ReadTextFor(role),
                });
                spec.branchNodes.Add(id);

                var replaced = FindMainEdge(spec, parent, parent + 1);
                var detourTotal = replaced == null ? rng.Range(3.5f, 5.5f) : replaced.seconds + rng.Range(3.5f, 5.5f);
                var first = detourTotal * rng.Range(.38f, .52f);
                spec.edges.Add(new FrontRoomsRaceEdge
                {
                    id = spec.edges.Count,
                    from = parent,
                    to = id,
                    kind = RaceEdgeKind.Branch,
                    isMainRoute = false,
                    branchParentId = parent,
                    seconds = first,
                    noise = .02f,
                    decision = "detour / quieter",
                });
                spec.edges.Add(new FrontRoomsRaceEdge
                {
                    id = spec.edges.Count,
                    from = id,
                    to = parent + 1,
                    kind = RaceEdgeKind.Branch,
                    isMainRoute = false,
                    branchParentId = parent,
                    seconds = detourTotal - first,
                    noise = .02f,
                    decision = "detour / quieter",
                });
            }
        }

        private static FrontRoomsRaceEdge FindMainEdge(FrontRoomsRaceSpec spec, int from, int to)
        {
            foreach (var edge in spec.edges)
                if (edge.isMainRoute && edge.from == from && edge.to == to) return edge;
            return null;
        }

        private static void RequireRole(Dictionary<RaceNodeRole, int> roles, RaceNodeRole role, FrontRoomsRaceValidation result)
        {
            if (!roles.ContainsKey(role)) result.errors.Add("missing required role " + role);
        }

        private static void Shuffle(List<int> values, ref RaceRng rng)
        {
            for (var i = values.Count - 1; i > 0; i--)
            {
                var j = rng.Range(0, i + 1);
                var t = values[i]; values[i] = values[j]; values[j] = t;
            }
        }

        private static string LabelFor(RaceNodeRole role, int number)
        {
            return role.ToString().ToUpperInvariant() + " / " + number.ToString("00");
        }

        private static string ReadTextFor(RaceNodeRole role)
        {
            switch (role)
            {
                case RaceNodeRole.Shift: return "The route changes when the archway leaves your view.";
                case RaceNodeRole.Office: return "A key route buys quiet time, but costs a search.";
                case RaceNodeRole.Run: return "The loud route is short. The quiet detour is longer.";
                case RaceNodeRole.Observation: return "Look before committing to the next threshold.";
                case RaceNodeRole.Recovery: return "A slower room gives the hunter less information.";
                default: return "Read the room before choosing the next opening.";
            }
        }
    }
}
