using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Deterministic checks of the authored level's traversal rules. These do not claim
/// to verify player input, timing, rendering, audio, or the hunter's runtime FSM.
/// Run with -executeMethod FrontRoomsVerification.Run; failures throw after a JSON report is saved.
/// </summary>
public static class FrontRoomsVerification
{
    [Serializable]
    private sealed class CheckResult
    {
        public string name;
        public bool passed;
        public string detail;
    }

    [Serializable]
    private sealed class VerificationReport
    {
        public string suite = "FrontRooms level traversal";
        public string timestampUtc;
        public string unityVersion;
        public string scope = "Pure level data, passability, sight, and pathfinding. Runtime interaction and playtest evidence are separate.";
        public int passed;
        public int failed;
        public List<CheckResult> checks = new List<CheckResult>();
    }

    [MenuItem("FrontRooms/Verify Level Rules")]
    public static void Run()
    {
        var report = new VerificationReport
        {
            timestampUtc = DateTime.UtcNow.ToString("o"),
            unityVersion = Application.unityVersion,
        };
        Check(report, "Five-room route and all three exit kinds", VerifyRoute);
        Check(report, "All room centers and escape tiles reachable after unlocking", VerifyReachability);
        Check(report, "Key, tell, spawn, and exit tiles lie inside their rooms", VerifyLandmarks);
        Check(report, "Closed door blocks player and sight but allows a breach plan", VerifyDoor);
        Check(report, "Closed window blocks both actors until broken", VerifyWindow);
        Check(report, "Sealed hall blocks player, hunter, sight, and route", VerifySealedHall);
        Check(report, "Walls block sight and same-room floor permits sight", VerifySight);
        Check(report, "Hunter cannot cut a blocked diagonal corner", VerifyCornerCutting);

        var output = ReportPath();
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
        Debug.Log(string.Format("[FrontRoomsVerification] {0} passed, {1} failed. {2}", report.passed, report.failed, output));
        if (report.failed != 0)
            throw new InvalidOperationException("FrontRooms level verification failed; inspect " + output);
    }

    private static void Check(VerificationReport report, string name, Action verify)
    {
        var result = new CheckResult { name = name };
        try
        {
            verify();
            result.passed = true;
            result.detail = "Passed";
            report.passed++;
        }
        catch (Exception exception)
        {
            result.detail = exception.Message;
            report.failed++;
            Debug.LogError("[FrontRoomsVerification] " + name + ": " + exception.Message);
        }
        report.checks.Add(result);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void VerifyRoute()
    {
        var level = new FrontRoomsLevel();
        Require(level.Rooms.Count == 5, "Expected the five-room functional slice.");
        var rules = new[] { RoomRule.Lobby, RoomRule.Shift, RoomRule.Office, RoomRule.Run, RoomRule.Exit };
        var kinds = new HashSet<OpeningKind>();
        for (var index = 0; index < rules.Length; index++)
        {
            Require(level.Rooms[index].Rule == rules[index], "Room " + index + " has the wrong biome rule.");
            if (index == rules.Length - 1) continue;
            Require(level.Rooms[index].AllOpenings.Exists(opening => opening.Across(level.Rooms[index]) == level.Rooms[index + 1]),
                "Missing route connection after room " + index + ".");
        }
        foreach (var opening in level.Openings)
        {
            kinds.Add(opening.Kind);
            Require(Mathf.Abs(opening.Owner.Id - opening.Other.Id) == 1, "Opening skips a room in the linear slice.");
            Require(opening.Tiles.Count > 0, "An opening has no traversable tiles.");
            foreach (var tile in opening.Tiles)
                Require(level.OpeningOf(tile) == opening, "Opening lookup does not match its authored tiles.");
        }
        Require(kinds.Contains(OpeningKind.Hall) && kinds.Contains(OpeningKind.Door) && kinds.Contains(OpeningKind.Window),
            "The slice must include halls, a key door, and breakable glass.");
    }

    private static void VerifyReachability()
    {
        var level = new FrontRoomsLevel();
        foreach (var opening in level.Openings)
        {
            opening.Sealed = false;
            opening.Open = true;
        }
        var visited = new HashSet<Vector2Int> { level.PlayerStart };
        var pending = new Queue<Vector2Int>();
        pending.Enqueue(level.PlayerStart);
        var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (pending.Count > 0)
        {
            var tile = pending.Dequeue();
            foreach (var direction in directions)
            {
                var next = tile + direction;
                if (!level.PlayerPassable(next) || !visited.Add(next)) continue;
                pending.Enqueue(next);
            }
        }
        foreach (var room in level.Rooms)
            Require(visited.Contains(FrontRoomsLevel.TileOf(room.Center)), "Unreachable room: " + room.Name);
        Require(level.ExitTiles.Count > 0, "No escape tiles authored.");
        foreach (var tile in level.ExitTiles)
            Require(visited.Contains(tile), "Unreachable escape tile: " + tile);
        var hunterPath = level.FindHunterPath(level.HunterStart, level.ExitTiles[0]);
        Require(hunterPath.Count > 0 && hunterPath[hunterPath.Count - 1] == level.ExitTiles[0], "Hunter cannot traverse the unlocked route.");
        VerifyPathEdges(level, level.HunterStart, hunterPath);
    }

    private static void VerifyLandmarks()
    {
        var level = new FrontRoomsLevel();
        Require(level.RoomOf(level.PlayerStart) == level.Rooms[0], "Player spawn is not in the lobby.");
        Require(level.PlayerPassable(level.PlayerStart), "Player spawn is blocked.");
        Require(level.CanHunterTraverse(level.HunterStart), "Hunter spawn is blocked.");
        var keyCount = 0;
        foreach (var room in level.Rooms)
        {
            if (room.HasKey)
            {
                keyCount++;
                Require(level.RoomOf(room.KeyTile) == room, "Key is outside its owning room: " + room.Name);
                Require(level.PlayerPassable(room.KeyTile), "Key tile is blocked.");
            }
            if (room.Rule == RoomRule.Shift || room.Rule == RoomRule.Office)
                Require(level.RoomOf(room.TellTile) == room, "Readable tell is outside its room: " + room.Name);
        }
        Require(keyCount > 0, "Office key is missing.");
        foreach (var tile in level.ExitTiles)
            Require(level.RoomOf(tile) == level.Rooms[4], "Escape patch is outside the last room.");
    }

    private static FrontOpening IsolateOpening(FrontRoomsLevel level, OpeningKind kind)
    {
        FrontOpening selected = null;
        foreach (var opening in level.Openings)
        {
            opening.Sealed = true;
            if (selected == null && opening.Kind == kind) selected = opening;
        }
        Require(selected != null, "No " + kind + " opening exists.");
        selected.Sealed = false;
        selected.Broken = false;
        return selected;
    }

    private static Vector2Int Side(FrontOpening opening, int direction)
    {
        return FrontRoomsLevel.TileOf(opening.Center + opening.Outward * direction);
    }

    private static void VerifyDoor()
    {
        var level = new FrontRoomsLevel();
        var door = IsolateOpening(level, OpeningKind.Door);
        door.Open = false;
        foreach (var tile in door.Tiles)
        {
            Require(!level.PlayerPassable(tile), "Player passes a locked door.");
            Require(!level.SeeThrough(tile), "Closed door allows sight.");
            Require(level.CanHunterTraverse(tile), "Hunter cannot plan to breach a closed door.");
        }
        var from = Side(door, -1);
        var to = Side(door, 1);
        var path = level.FindHunterPath(from, to);
        Require(path.Count > 0 && path.Exists(tile => level.OpeningOf(tile) == door), "Breach plan does not route through the isolated door.");
        VerifyPathEdges(level, from, path);
        door.Open = true;
        foreach (var tile in door.Tiles)
            Require(level.PlayerPassable(tile) && level.SeeThrough(tile), "Unlocked open door still blocks the player or sight.");
        door.Broken = true;
        foreach (var tile in door.Tiles)
            Require(level.PlayerPassable(tile) && level.CanHunterTraverse(tile), "Broken open door blocks an actor.");
    }

    private static void VerifyWindow()
    {
        var level = new FrontRoomsLevel();
        var window = IsolateOpening(level, OpeningKind.Window);
        window.Open = false;
        foreach (var tile in window.Tiles)
            Require(!level.PlayerPassable(tile) && !level.CanHunterTraverse(tile) && !level.SeeThrough(tile),
                "Intact window must block both actors and sight.");
        var from = Side(window, -1);
        var to = Side(window, 1);
        Require(level.FindHunterPath(from, to).Count == 0, "Hunter traverses the only intact window route.");
        window.Broken = true;
        window.Open = true;
        foreach (var tile in window.Tiles)
            Require(level.PlayerPassable(tile) && level.CanHunterTraverse(tile) && level.SeeThrough(tile),
                "Broken window should permit both actors and sight.");
        var path = level.FindHunterPath(from, to);
        Require(path.Count > 0 && path.Exists(tile => level.OpeningOf(tile) == window), "Hunter cannot use broken glass.");
        VerifyPathEdges(level, from, path);
    }

    private static void VerifySealedHall()
    {
        var level = new FrontRoomsLevel();
        var hall = IsolateOpening(level, OpeningKind.Hall);
        hall.Open = true;
        var from = Side(hall, -1);
        var to = Side(hall, 1);
        Require(level.FindHunterPath(from, to).Count > 0, "Control case: unsealed hall has no route.");
        hall.Sealed = true;
        foreach (var tile in hall.Tiles)
            Require(!level.PlayerPassable(tile) && !level.CanHunterTraverse(tile) && !level.SeeThrough(tile),
                "Sealed hall is still traversable or transparent.");
        Require(!level.LineOfSight(FrontRoomsLevel.CenterOf(from), FrontRoomsLevel.CenterOf(to)), "Sight crosses a sealed hall.");
        Require(level.FindHunterPath(from, to).Count == 0, "Hunter path ignores the sealed route.");
        hall.Sealed = false;
        Require(level.FindHunterPath(from, to).Count > 0, "Unsealing the hall does not restore connectivity.");
    }

    private static void VerifySight()
    {
        var level = new FrontRoomsLevel();
        var room = level.Rooms[0];
        Require(level.LineOfSight(room.Center, room.Center + Vector2.right), "Open room blocks its own sight line.");
        var wallX = FrontRoomsLevel.CellW;
        var wallY = 1;
        Require(level.Tiles[wallX, wallY] == TileKind.Wall, "Test wall landmark changed; update this sight fixture.");
        Require(!level.LineOfSight(new Vector2(wallX - 0.5f, wallY + 0.5f), new Vector2(wallX + 1.5f, wallY + 0.5f)),
            "Line of sight passes through an authored solid wall.");
    }

    private static void VerifyCornerCutting()
    {
        var level = new FrontRoomsLevel();
        for (var x = 0; x < level.Width; x++)
            for (var y = 0; y < level.Height; y++)
                level.Tiles[x, y] = TileKind.Wall;
        var start = new Vector2Int(2, 2);
        var goal = new Vector2Int(3, 3);
        level.Tiles[start.x, start.y] = TileKind.Floor;
        level.Tiles[goal.x, goal.y] = TileKind.Floor;
        Require(level.FindHunterPath(start, goal).Count == 0, "Hunter cuts diagonally between two blocked neighbors.");
        level.Tiles[3, 2] = TileKind.Floor;
        var path = level.FindHunterPath(start, goal);
        Require(path.Count == 2, "Hunter should take two cardinal steps around the blocked corner.");
        VerifyPathEdges(level, start, path);
    }

    private static void VerifyPathEdges(FrontRoomsLevel level, Vector2Int previous, List<Vector2Int> path)
    {
        foreach (var tile in path)
        {
            var delta = tile - previous;
            Require(Mathf.Abs(delta.x) <= 1 && Mathf.Abs(delta.y) <= 1, "Hunter path skips a tile.");
            Require(level.CanHunterTraverse(tile), "Path enters a blocked tile.");
            if (delta.x != 0 && delta.y != 0)
                Require(level.CanHunterTraverse(new Vector2Int(previous.x + delta.x, previous.y)) &&
                        level.CanHunterTraverse(new Vector2Int(previous.x, previous.y + delta.y)),
                    "Hunter path cuts a blocked diagonal corner.");
            previous = tile;
        }
    }

    private static string ReportPath()
    {
        var args = Environment.GetCommandLineArgs();
        for (var index = 0; index + 1 < args.Length; index++)
            if (args[index] == "-frontroomsVerificationOutput") return Path.GetFullPath(args[index + 1]);
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "FrontRooms", "level-verification.json");
    }
}
