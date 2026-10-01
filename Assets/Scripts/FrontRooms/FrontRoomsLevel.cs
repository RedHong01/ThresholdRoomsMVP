using System.Collections.Generic;
using UnityEngine;

public enum RoomHeight { Low, Standard, Tall, Exit }
public enum RoomRule { Lobby, Shift, Office, Run, Exit }
public enum OpeningKind { Hall, Door, Window }
public enum TileKind { Wall, Floor, Opening }

/// <summary>One room of the floor. Its height decides which exits it owns.</summary>
public sealed class FrontRoom
{
    public int Id;
    public Vector2Int Cell;
    public string Name;
    public RoomHeight Height;
    public RoomRule Rule;
    public RectInt Interior;
    public Vector2Int TellTile;
    public string RuleText;
    public bool HasKey;
    public Vector2Int KeyTile;
    public bool KeyTaken;
    public bool Read;
    public FrontOpening BestExit;
    public readonly List<FrontOpening> OwnExits = new List<FrontOpening>();
    public readonly List<FrontOpening> AllOpenings = new List<FrontOpening>();

    public Vector2 Center => new Vector2(Interior.xMin + Interior.width * 0.5f, Interior.yMin + Interior.height * 0.5f);
}

/// <summary>A gap in a shared wall: an open hall, a door or a window. The owner room's height sets the kind.</summary>
public sealed class FrontOpening
{
    public int Id;
    public OpeningKind Kind;
    public FrontRoom Owner;
    public FrontRoom Other;
    public readonly List<Vector2Int> Tiles = new List<Vector2Int>();
    public bool Open;
    public bool Broken;
    public bool Sealed;
    public float CloseTimer;
    public bool WallIsVertical;
    public Vector2 Center;
    public Vector2 Outward;

    public string Direction =>
        Outward.x > 0.5f ? "east" : Outward.x < -0.5f ? "west" : Outward.y > 0.5f ? "north" : "south";

    public FrontRoom Across(FrontRoom from) => from == Owner ? Other : Owner;

    public Vector2 OutwardFrom(FrontRoom from) => from == Owner ? Outward : -Outward;
}

/// <summary>
/// A five-cell test route: lobby, shifting Level 0, keyed office, red run corridor, exit.
/// Low rooms own halls, the office owns a keyed door, and the tall run corridor owns windows.
/// The office/run boundary deliberately offers both rooms' exits: the office's door costs
/// key-search time, while entering through the run corridor's window costs glass noise.
/// This shared boundary tests the door/window decision without adding a sixth room.
/// </summary>
public sealed class FrontRoomsLevel
{
    public const int Cols = 5;
    public const int Rows = 1;
    public const int CellW = 12;
    public const int CellH = 10;

    public readonly int Width = Cols * CellW + 1;
    public readonly int Height = Rows * CellH + 1;
    public readonly TileKind[,] Tiles;
    public readonly FrontOpening[,] OpeningAt;
    public readonly FrontRoom[,] RoomAt;
    public readonly List<FrontRoom> Rooms = new List<FrontRoom>();
    public readonly List<FrontOpening> Openings = new List<FrontOpening>();
    public readonly List<Vector2Int> ExitTiles = new List<Vector2Int>();
    public readonly int[,] DistToExit;
    public Vector2Int PlayerStart;
    public Vector2Int HunterStart;

    private readonly FrontRoom[,] roomByCell = new FrontRoom[Cols, Rows];

    public FrontRoomsLevel()
    {
        Tiles = new TileKind[Width, Height];
        OpeningAt = new FrontOpening[Width, Height];
        RoomAt = new FrontRoom[Width, Height];
        DistToExit = new int[Width, Height];

        AddRoom(0, 0, "Lobby", RoomHeight.Low, RoomRule.Lobby, new Vector2Int(8, 5),
            "Quiet walking loses a hunt. Running and grabbing keys make noise; glass can be heard from anywhere.");
        AddRoom(1, 0, "Level 0 · Shifting Rooms", RoomHeight.Low, RoomRule.Shift, new Vector2Int(17, 5),
            "The hum drops one second before a shift. Keep an archway on screen to hold it still.");
        AddRoom(2, 0, "Level 4 · Office", RoomHeight.Standard, RoomRule.Office, new Vector2Int(29, 5),
            "The key is in the northeast corner. A closed door holds the hunter for 2.5 seconds; glass stays open.",
            new Vector2Int(33, 8));
        AddRoom(3, 0, "Level ! · Run", RoomHeight.Tall, RoomRule.Run, new Vector2Int(40, 5),
            "Red means run. Keep moving and break the east window to reach the exit.");
        AddRoom(4, 0, "Exit · the noclip patch", RoomHeight.Exit, RoomRule.Exit, new Vector2Int(53, 5),
            "Reach the glowing patch to escape.");

        // Two separate lanes at each Level 0 boundary leave a route when the unseen lane seals.
        // owner → other, kind, offset from the first interior tile of the shared wall
        AddOpening(0, 0, 1, 0, OpeningKind.Hall, 1);    // 0: x12, y2–4
        AddOpening(0, 0, 1, 0, OpeningKind.Hall, 5);    // 1: x12, y6–8
        AddOpening(1, 0, 2, 0, OpeningKind.Hall, 1);    // 2: x24, y2–4
        AddOpening(1, 0, 2, 0, OpeningKind.Hall, 5);    // 3: x24, y6–8
        AddOpening(2, 0, 3, 0, OpeningKind.Door, 1);   // 4: x36, y2–3
        AddOpening(3, 0, 2, 0, OpeningKind.Window, 6); // 5: x36, y7–8; tall room owns it
        AddOpening(3, 0, 4, 0, OpeningKind.Window, 3); // 6: x48, y4–5

        PlayerStart = new Vector2Int(4, 5);
        HunterStart = new Vector2Int(1, 5);
        for (var x = 57; x <= 58; x++)
            for (var y = 4; y <= 6; y++)
                ExitTiles.Add(new Vector2Int(x, y));

        BuildExitDistances();
    }

    private void AddRoom(int col, int row, string name, RoomHeight height, RoomRule rule,
        Vector2Int tell, string ruleText, Vector2Int? key = null)
    {
        var room = new FrontRoom
        {
            Id = Rooms.Count,
            Cell = new Vector2Int(col, row),
            Name = name,
            Height = height,
            Rule = rule,
            Interior = new RectInt(col * CellW + 1, row * CellH + 1, CellW - 1, CellH - 1),
            TellTile = tell,
            RuleText = ruleText,
            HasKey = key.HasValue,
            KeyTile = key ?? Vector2Int.zero,
        };
        Rooms.Add(room);
        roomByCell[col, row] = room;
        for (var x = room.Interior.xMin; x < room.Interior.xMax; x++)
        {
            for (var y = room.Interior.yMin; y < room.Interior.yMax; y++)
            {
                Tiles[x, y] = TileKind.Floor;
                RoomAt[x, y] = room;
            }
        }
    }

    private void AddOpening(int c1, int r1, int c2, int r2, OpeningKind kind, int offset)
    {
        var owner = roomByCell[c1, r1];
        var other = roomByCell[c2, r2];
        var width = kind == OpeningKind.Hall ? 3 : 2;
        var opening = new FrontOpening
        {
            Id = Openings.Count,
            Kind = kind,
            Owner = owner,
            Other = other,
            Open = kind == OpeningKind.Hall,
            Outward = new Vector2(c2 - c1, r2 - r1),
            WallIsVertical = r1 == r2,
        };

        for (var i = 0; i < width; i++)
        {
            Vector2Int tile;
            if (r1 == r2)
            {
                var wallX = Mathf.Max(c1, c2) * CellW;
                tile = new Vector2Int(wallX, r1 * CellH + 1 + offset + i);
            }
            else
            {
                var wallY = Mathf.Max(r1, r2) * CellH;
                tile = new Vector2Int(c1 * CellW + 1 + offset + i, wallY);
            }
            opening.Tiles.Add(tile);
            Tiles[tile.x, tile.y] = TileKind.Opening;
            OpeningAt[tile.x, tile.y] = opening;
        }

        var sum = Vector2.zero;
        foreach (var t in opening.Tiles) sum += new Vector2(t.x + 0.5f, t.y + 0.5f);
        opening.Center = sum / opening.Tiles.Count;

        Openings.Add(opening);
        owner.OwnExits.Add(opening);
        owner.AllOpenings.Add(opening);
        other.AllOpenings.Add(opening);
    }

    private void BuildExitDistances()
    {
        for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
                DistToExit[x, y] = int.MaxValue;

        var queue = new Queue<Vector2Int>();
        foreach (var t in ExitTiles)
        {
            DistToExit[t.x, t.y] = 0;
            queue.Enqueue(t);
        }

        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            foreach (var d in dirs)
            {
                var n = t + d;
                if (!InBounds(n) || Tiles[n.x, n.y] == TileKind.Wall) continue;
                if (DistToExit[n.x, n.y] != int.MaxValue) continue;
                DistToExit[n.x, n.y] = DistToExit[t.x, t.y] + 1;
                queue.Enqueue(n);
            }
        }
    }

    public bool InBounds(Vector2Int t) => t.x >= 0 && t.y >= 0 && t.x < Width && t.y < Height;

    public static Vector2Int TileOf(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));

    public static Vector2 CenterOf(Vector2Int t) => new Vector2(t.x + 0.5f, t.y + 0.5f);

    public FrontRoom RoomOf(Vector2Int t) => InBounds(t) ? RoomAt[t.x, t.y] : null;

    public FrontOpening OpeningOf(Vector2Int t) => InBounds(t) ? OpeningAt[t.x, t.y] : null;

    /// <summary>Can the player stand on this tile right now?</summary>
    public bool PlayerPassable(Vector2Int t)
    {
        if (!InBounds(t)) return false;
        switch (Tiles[t.x, t.y])
        {
            case TileKind.Floor: return true;
            case TileKind.Opening:
                var opening = OpeningAt[t.x, t.y];
                return !opening.Sealed && opening.Open;
            default: return false;
        }
    }

    /// <summary>Walls, shut doors and unbroken (frosted) windows block sight.</summary>
    public bool SeeThrough(Vector2Int t) => PlayerPassable(t);

    public bool LineOfSight(Vector2 a, Vector2 b)
    {
        var delta = b - a;
        var steps = Mathf.CeilToInt(delta.magnitude / 0.2f);
        for (var i = 1; i < steps; i++)
        {
            var p = a + delta * (i / (float)steps);
            if (!SeeThrough(TileOf(p))) return false;
        }
        return true;
    }

    // ---- hunter pathfinding (A*, 8-way, no corner cutting) ----

    private bool HunterPassable(Vector2Int t)
    {
        if (!InBounds(t)) return false;
        switch (Tiles[t.x, t.y])
        {
            case TileKind.Floor: return true;
            case TileKind.Opening:
                var o = OpeningAt[t.x, t.y];
                return !o.Sealed && (o.Kind != OpeningKind.Window || o.Open);
            default: return false;
        }
    }

    /// <summary>Hunter route eligibility. Shut doors are eligible because the hunter can break them.</summary>
    public bool CanHunterTraverse(Vector2Int t) => HunterPassable(t);

    private float EnterCost(Vector2Int t)
    {
        var o = OpeningAt[t.x, t.y];
        return o != null && o.Kind == OpeningKind.Door && !o.Open ? 8f : 0f;
    }

    public Vector2Int NearestHunterTile(Vector2Int t)
    {
        if (HunterPassable(t)) return t;
        for (var r = 1; r <= 3; r++)
            for (var dx = -r; dx <= r; dx++)
                for (var dy = -r; dy <= r; dy++)
                {
                    var n = new Vector2Int(t.x + dx, t.y + dy);
                    if (HunterPassable(n)) return n;
                }
        return t;
    }

    public List<Vector2Int> FindHunterPath(Vector2Int start, Vector2Int goal)
    {
        goal = NearestHunterTile(goal);
        start = NearestHunterTile(start);
        var result = new List<Vector2Int>();
        if (start == goal) { result.Add(goal); return result; }

        var g = new float[Width, Height];
        var parent = new Vector2Int[Width, Height];
        var closed = new bool[Width, Height];
        for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++)
                g[x, y] = float.MaxValue;

        var open = new List<Vector2Int> { start };
        g[start.x, start.y] = 0f;
        parent[start.x, start.y] = start;

        while (open.Count > 0)
        {
            var bestIndex = 0;
            var bestF = float.MaxValue;
            for (var i = 0; i < open.Count; i++)
            {
                var n = open[i];
                var f = g[n.x, n.y] + Octile(n, goal);
                if (f < bestF) { bestF = f; bestIndex = i; }
            }
            var current = open[bestIndex];
            open.RemoveAt(bestIndex);
            if (current == goal) break;
            if (closed[current.x, current.y]) continue;
            closed[current.x, current.y] = true;

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var n = new Vector2Int(current.x + dx, current.y + dy);
                    if (!HunterPassable(n) || closed[n.x, n.y]) continue;
                    if (dx != 0 && dy != 0 &&
                        (!HunterPassable(new Vector2Int(current.x + dx, current.y)) || !HunterPassable(new Vector2Int(current.x, current.y + dy))))
                        continue;
                    var cost = g[current.x, current.y] + (dx != 0 && dy != 0 ? 1.4142f : 1f) + EnterCost(n);
                    if (cost < g[n.x, n.y])
                    {
                        g[n.x, n.y] = cost;
                        parent[n.x, n.y] = current;
                        open.Add(n);
                    }
                }
            }
        }

        if (g[goal.x, goal.y] == float.MaxValue) return result;
        var step = goal;
        while (step != start)
        {
            result.Add(step);
            step = parent[step.x, step.y];
        }
        result.Reverse();
        return result;
    }

    private static float Octile(Vector2Int a, Vector2Int b)
    {
        var dx = Mathf.Abs(a.x - b.x);
        var dy = Mathf.Abs(a.y - b.y);
        return Mathf.Max(dx, dy) + 0.4142f * Mathf.Min(dx, dy);
    }

    /// <summary>The available forward connection closest to the exit, including a neighbor-owned window.</summary>
    public FrontOpening BestExitOf(FrontRoom room)
    {
        FrontOpening best = null;
        var bestDist = int.MaxValue;
        foreach (var o in room.AllOpenings)
        {
            if (o.Sealed || o.Across(room).Cell.x <= room.Cell.x) continue;
            var far = TileOf(o.Center + o.OutwardFrom(room));
            if (!InBounds(far)) continue;
            var d = DistToExit[far.x, far.y];
            if (d < bestDist) { bestDist = d; best = o; }
        }
        return best;
    }
}
