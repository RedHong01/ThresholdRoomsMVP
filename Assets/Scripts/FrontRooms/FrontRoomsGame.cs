using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// FrontRooms — Week 2 functional prototype.
///
/// Question it answers: when every room shows its exits and what each one costs the moment you
/// walk in, does the hunter's distance turn "open hall / door / window" into a real split-second
/// choice, or does one exit always win?
///
/// Everything (floor, UI, sound) is generated at runtime so a fresh Mac or Windows clone plays
/// without imported assets. Each room exit is logged to a CSV so playtests produce numbers.
/// </summary>
[ExecuteAlways]
public sealed class FrontRoomsGame : MonoBehaviour
{
    private enum Phase { Title, Playing, Paused, Escaped, Caught }
    private enum HunterState { Listen, Hunt, Search, Chase, BreakDoor }
    private enum ControlMode { WASD, Cursor }

    private sealed class Decision
    {
        public float Time;
        public string From;
        public string To;
        public string Exit;
        public float HunterDistance;
        public HunterState HunterState;
        public float SecondsInRoom;
        public bool Read;
        public int Keys;
        public ControlMode Mode;
    }

    private sealed class Ring
    {
        public SpriteRenderer Renderer;
        public float Age;
        public float Life;
        public float Size;
    }

    private sealed class OpeningView
    {
        public SpriteRenderer Slab;
        public SpriteRenderer Backing;
        public readonly List<SpriteRenderer> Shards = new List<SpriteRenderer>();
    }

    private sealed class PilotStep
    {
        public string Kind;
        public Vector2 Target;
        public float Seconds;
        public string Name;
        public bool Run;
    }

    // Survive a reload so "R" keeps the chosen control scheme and skips the title card.
    private static ControlMode controlMode = ControlMode.WASD;
    private static bool skipTitle;
    private static int runCounter;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3.1f;
    [SerializeField] private float runSpeed = 5.3f;

    [Header("Hunter")]
    [SerializeField] private float hunterHuntSpeed = 2.7f;
    [SerializeField] private float hunterChaseSpeed = 4.0f;
    [SerializeField] private float sightRange = 6.5f;
    [SerializeField] private float breakDoorSeconds = 2.5f;
    [SerializeField] private float catchRadius = 0.6f;

    [Header("Actions")]
    [SerializeField] private float breakWindowSeconds = 1.0f;
    [SerializeField] private float readSeconds = 1.5f;

    [Header("Noise radius (tiles)")]
    [SerializeField] private float runNoise = 5.5f;
    [SerializeField] private float keyNoise = 8f;
    [SerializeField] private float doorNoise = 4f;
    [SerializeField] private float slamNoise = 3f;

    private const float PlayerRadius = 0.28f;
    private const float WindowNoise = 999f;
    private const float WindowReach = 0.9f;

    // S00 palette from the deck, plus Backrooms greybox tones.
    private static readonly Color Ink = Hex("0A0A0A");
    private static readonly Color Paper = Hex("FFFFFF");
    private static readonly Color Muted = Hex("6B6B6B");
    private static readonly Color Hair = Hex("D9D9D9");
    private static readonly Color Accent = Hex("F4DF3B");
    private static readonly Color Media = Hex("141414");
    private static readonly Color WallTone = Hex("D9CB86");
    private static readonly Color FloorLow = Hex("5F5530");
    private static readonly Color FloorStandard = Hex("7D6F3F");
    private static readonly Color FloorTall = Hex("9A8C57");
    private static readonly Color FloorExit = Hex("2B3A42");
    private static readonly Color Threshold = Hex("4E4628");
    private static readonly Color Glass = Hex("9FD3E0");

    private FrontRoomsLevel level;
    private Transform world;
    private Camera cam;
    private Sprite square;
    private Sprite disc;
    private Sprite ring;
    private Sprite triangle;
    private Font font;
    private Font monoFont;
    private Font bayonFont;
    private Font serifFont;

    private Transform player;
    private Vector2 playerPos;
    private Transform hunter;
    private Vector2 hunterPos;
    private HunterState hunterState = HunterState.Listen;
    private Vector2 hunterTarget;
    private List<Vector2Int> hunterPath = new List<Vector2Int>();
    private float repathTimer;
    private float stateTimer;
    private float lostSightTimer;
    private float bangTimer;
    private float hunterStepTimer;
    private FrontOpening breakingDoor;
    private Vector2 lastSeenPlayer;

    private readonly HashSet<int> keys = new HashSet<int>();
    private readonly Dictionary<FrontOpening, OpeningView> openingViews = new Dictionary<FrontOpening, OpeningView>();
    private readonly Dictionary<FrontRoom, Transform> keyViews = new Dictionary<FrontRoom, Transform>();
    private readonly Dictionary<FrontRoom, Transform> arrowViews = new Dictionary<FrontRoom, Transform>();
    private readonly List<Ring> rings = new List<Ring>();
    private readonly List<SpriteRenderer> lightPanels = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> pathDots = new List<SpriteRenderer>();
    private readonly List<Decision> decisions = new List<Decision>();
    private SpriteRenderer exitGlow;

    private Phase phase = Phase.Title;
    private float runTime;
    private FrontRoom currentRoom;
    private float roomEnterTime;
    private FrontOpening lastOpening;
    private float stillTimer;
    private float runStepTimer;
    private float windowProgress;
    private float heartbeatTimer;
    private float shake;
    private string transientPrompt;
    private float transientTimer;
    private bool mapView;
    private bool debugView;
    private string outcomeReason;
    private string savedCsvPath;

    private AudioSource humSource;
    private AudioSource sfx;
    private AudioSource hunterSfx;
    private AudioClip clipPlayerStep, clipPlayerRunStep, clipHunterStep, clipKey, clipDoorOpen, clipDoorSlam, clipDoorBang, clipGlass, clipHeartbeat, clipCaught, clipEscape;

    private RectTransform canvasRect;
    private Text roomTitle, hunterValue, hunterStateText, promptText, overlayText;
    private Image logoImage;
    private Sprite brandLogo;
    private Image hunterBar, vignette;
    private GameObject overlay;
    private readonly List<Text> mapLabels = new List<Text>();

    // Autotest pilot (-autotest <folder>): plays a fixed route, captures screenshots, quits.
    private List<PilotStep> pilotSteps;
    private int pilotIndex;
    private float pilotTimer;
    private string pilotDir;
    private Vector2 pilotMove;
    private bool pilotRun;
    private bool pilotBreak;
    private bool pilotFinishing;
    private bool pilotRead;
    private string pilotRoute = "door";
    private bool pilotFailed;
    private int readsCompleted;
    private int windowsBroken;
    private int hunterDoorsBroken;
    private int shifts;
    private float readProgress;
    private float shiftTimer;
    private bool shiftWarning;
    private bool hunterAwake;
    private float hunterReleaseTime = 6f;
    private readonly List<string> events = new List<string>();
    private readonly Dictionary<FrontRoom, SpriteRenderer> tellViews = new Dictionary<FrontRoom, SpriteRenderer>();

    // ------------------------------------------------------------------ setup

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            var preview = EditorPreviewTransform();
            if (preview != null) preview.gameObject.SetActive(true);
        }
    }

    private Transform EditorPreviewTransform()
    {
        for (var i = 0; i < transform.childCount; i++)
            if (transform.GetChild(i).name == "EDITOR_PREVIEW / FrontRooms2D") return transform.GetChild(i);
        return null;
    }

    private void InitializeLevel()
    {
        if (level != null) return;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        monoFont = Resources.Load<Font>("Fonts/IBMPlexMono-Regular") ?? font;
        bayonFont = Resources.Load<Font>("Fonts/Bayon-Regular") ?? font;
        serifFont = Resources.Load<Font>("Fonts/SourceSerif4-Variable") ?? font;
        level = new FrontRoomsLevel();
        level.Openings[3].Sealed = true;
    }

    /// <summary>
    /// Writes the 2D top-down slice into the scene while editing. The generated
    /// sprites, camera, room dressing and interaction markers are ordinary scene
    /// objects, so they can be selected and adjusted before entering Play Mode.
    /// </summary>
    public void EnsureEditorPreview()
    {
        if (Application.isPlaying) return;
        InitializeLevel();
        var existing = EditorPreviewTransform();
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            world = existing;
            cam = existing.GetComponentInChildren<Camera>(true);
            player = existing.Find("Player");
            hunter = existing.Find("Hunter");
            return;
        }

        MakeSprites();
        BuildCamera();
        BuildWorld();
        if (world == null) return;
        world.name = "EDITOR_PREVIEW / FrontRooms2D";
        world.SetParent(transform, true);
        if (cam != null) cam.transform.SetParent(world, true);
    }

    private void Awake()
    {
        InitializeLevel();
        if (!Application.isPlaying)
        {
            EnsureEditorPreview();
            return;
        }

        Application.targetFrameRate = 60;
        Time.timeScale = 1f;
        openingViews.Clear(); keyViews.Clear(); arrowViews.Clear(); tellViews.Clear();
        MakeSprites();
        var preview = EditorPreviewTransform();
        if (preview != null)
        {
            world = preview;
            cam = preview.GetComponentInChildren<Camera>(true);
            player = preview.Find("Player");
            hunter = preview.Find("Hunter");
            RebindSerializedWorld();
        }
        BuildAudio();
        if (world == null) BuildWorld();
        BuildHud();
        ReadCommandLine();

        playerPos = FrontRoomsLevel.CenterOf(level.PlayerStart);
        hunterPos = FrontRoomsLevel.CenterOf(level.HunterStart);
        hunterTarget = hunterPos;
        runCounter++;

        if (skipTitle && pilotSteps == null) StartRun();
        else ShowTitle();
    }

    private void ReadCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "-autotest" && i + 1 < args.Length)
            {
                pilotDir = args[i + 1];
                Directory.CreateDirectory(pilotDir);

            }
            if (args[i] == "-cursor") controlMode = ControlMode.Cursor;
            if (args[i] == "-route" && i + 1 < args.Length) pilotRoute = args[i + 1];
        }
        if (pilotDir != null) BuildPilot();
    }

    private void MakeSprites()
    {
        square = Sprite.Create(SolidTexture(4), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        disc = Sprite.Create(ShapeTexture(64, 0), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
        ring = Sprite.Create(ShapeTexture(128, 1), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 128f);
        triangle = Sprite.Create(ShapeTexture(64, 2), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
    }

    private static Texture2D SolidTexture(int n)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (var i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>shape 0 = disc, 1 = ring, 2 = triangle pointing +x.</summary>
    private static Texture2D ShapeTexture(int n, int shape)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (var y = 0; y < n; y++)
        {
            for (var x = 0; x < n; x++)
            {
                var u = (x + 0.5f) / n;
                var v = (y + 0.5f) / n;
                var d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                float a;
                if (shape == 0) a = Mathf.Clamp01((1f - d) * n * 0.5f);
                else if (shape == 1) a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.93f) * n * 0.35f);
                else
                {
                    var halfWidth = 0.42f * (1f - (u - 0.08f) / 0.84f);
                    a = u > 0.08f && u < 0.92f && Mathf.Abs(v - 0.5f) < halfWidth ? 1f : 0f;
                }
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private void BuildCamera()
    {
        if (cam != null) return;
        cam = new GameObject("Camera").AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 7.2f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Media;
        var start = FrontRoomsLevel.CenterOf(level.PlayerStart);
        cam.transform.position = new Vector3(start.x, start.y, -20f);
        cam.gameObject.AddComponent<AudioListener>();
    }

    private void BuildAudio()
    {
        clipPlayerStep = FrontRoomsAudio.PlayerStep();
        clipPlayerRunStep = FrontRoomsAudio.PlayerRunStep();
        clipHunterStep = FrontRoomsAudio.HunterStep();
        clipKey = FrontRoomsAudio.Key();
        clipDoorOpen = FrontRoomsAudio.DoorOpen();
        clipDoorSlam = FrontRoomsAudio.DoorSlam();
        clipDoorBang = FrontRoomsAudio.DoorBang();
        clipGlass = FrontRoomsAudio.Glass();
        clipHeartbeat = FrontRoomsAudio.Heartbeat();
        clipCaught = FrontRoomsAudio.Caught();
        clipEscape = FrontRoomsAudio.Escape();

        BuildCamera();

        humSource = cam.gameObject.AddComponent<AudioSource>();
        humSource.clip = FrontRoomsAudio.Hum();
        humSource.loop = true;
        humSource.volume = 0.35f;
        humSource.Play();
        sfx = cam.gameObject.AddComponent<AudioSource>();
        hunterSfx = cam.gameObject.AddComponent<AudioSource>();
        // 2D prototype uses stereo position to keep the hunter legible without
        // adding a UI panel: left/right pan follows its offset from the player.
        hunterSfx.spatialBlend = 0f;
        hunterSfx.panStereo = 0f;
    }

    private void BuildWorld()
    {
        world = new GameObject("World").transform;
        var rng = new System.Random(7);

        for (var x = 0; x < level.Width; x++)
        {
            for (var y = 0; y < level.Height; y++)
            {
                var t = new Vector2Int(x, y);
                var pos = FrontRoomsLevel.CenterOf(t);
                switch (level.Tiles[x, y])
                {
                    case TileKind.Floor:
                        var baseColor = level.RoomAt[x, y].Rule == RoomRule.Run ? Hex("581D22") : FloorColor(level.RoomAt[x, y].Height);
                        var jitter = 1f + ((float)rng.NextDouble() - 0.5f) * 0.07f;
                        Spr("floor", square, pos, Vector2.one, baseColor * jitter + new Color(0, 0, 0, 1f - baseColor.a * jitter), 0);
                        break;
                    case TileKind.Wall:
                        // The wall surface carries the room identity in the 2D slice:
                        // yellow lobby, desaturated shift, office beige, red loop, and
                        // the cooler exit threshold. This keeps the top-down view from
                        // collapsing into one flat yellow field.
                        var wallRoom = RoomForWall(x, y);
                        Spr("wall / " + (wallRoom == null ? "shared" : wallRoom.Rule.ToString()), square, pos, Vector2.one,
                            wallRoom == null ? WallTone : WallColor(wallRoom.Rule), 2);
                        break;
                    case TileKind.Opening:
                        Spr("threshold", square, pos, Vector2.one, Threshold, 0);
                        break;
                }
            }
        }

        foreach (var room in level.Rooms) BuildRoomDressing(room, rng);
        foreach (var opening in level.Openings) BuildOpening(opening, rng);

        var exitCenter = Vector2.zero;
        foreach (var t in level.ExitTiles) exitCenter += FrontRoomsLevel.CenterOf(t);
        exitCenter /= level.ExitTiles.Count;
        exitGlow = Spr("exit patch", square, exitCenter, new Vector2(2f, 3f), Paper, 3);

        player = new GameObject("Player").transform;
        player.SetParent(world, false);
        Spr("outline", disc, Vector2.zero, Vector2.one * 0.76f, Ink, 10, player);
        Spr("body", disc, Vector2.zero, Vector2.one * 0.6f, Accent, 11, player);

        hunter = new GameObject("Hunter").transform;
        hunter.SetParent(world, false);
        Spr("outline", disc, Vector2.zero, Vector2.one * 0.86f, Ink, 12, hunter);
        Spr("body", disc, Vector2.zero, Vector2.one * 0.7f, Paper, 13, hunter);
        Spr("core", disc, Vector2.zero, Vector2.one * 0.22f, Ink, 14, hunter);

        for (var i = 0; i < 60; i++)
        {
            var dot = Spr("path", disc, Vector2.zero, Vector2.one * 0.14f, new Color(1f, 1f, 1f, 0.5f), 8);
            dot.gameObject.SetActive(false);
            pathDots.Add(dot);
        }
    }

    private List<GameObject> DirectNamed(string name)
    {
        var result = new List<GameObject>();
        if (world == null) return result;
        foreach (Transform child in world)
            if (child.name == name) result.Add(child.gameObject);
        return result;
    }

    private Transform DirectPrefix(string prefix)
    {
        if (world == null) return null;
        foreach (Transform child in world)
            if (child.name.StartsWith(prefix, StringComparison.Ordinal)) return child;
        return null;
    }

    private SpriteRenderer FirstSprite(Transform root, string name)
    {
        if (root == null) return null;
        foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            if (renderer.name == name) return renderer;
        return null;
    }

    private void RebindSerializedWorld()
    {
        if (world == null) return;
        openingViews.Clear(); keyViews.Clear(); arrowViews.Clear(); tellViews.Clear();
        lightPanels.Clear(); pathDots.Clear(); mapLabels.Clear();
        var floor = DirectNamed("floor");
        var playerBody = FirstSprite(player, "body");
        if (floor.Count > 0) square = floor[0].GetComponent<SpriteRenderer>().sprite;
        if (playerBody != null) disc = playerBody.sprite;
        if (playerBody != null) ring = FirstSprite(DirectPrefix("Key ·"), "bow")?.sprite ?? ring;
        var arrowRoot = DirectPrefix("Read arrow ·");
        if (arrowRoot != null) triangle = FirstSprite(arrowRoot, "arrow")?.sprite ?? triangle;

        var path = DirectNamed("path");
        foreach (var item in path) { var renderer = item.GetComponent<SpriteRenderer>(); if (renderer != null) pathDots.Add(renderer); }
        var lights = DirectNamed("light");
        foreach (var item in lights) { var renderer = item.GetComponent<SpriteRenderer>(); if (renderer != null) lightPanels.Add(renderer); }
        exitGlow = DirectNamed("exit patch").Count > 0 ? DirectNamed("exit patch")[0].GetComponent<SpriteRenderer>() : null;

        foreach (var room in level.Rooms)
        {
            var tell = FirstSprite(world, "Tell · " + room.Name);
            if (tell != null) tellViews[room] = tell;
            var key = DirectPrefix("Key · " + room.Name);
            if (key != null) keyViews[room] = key;
            var arrow = DirectPrefix("Read arrow · " + room.Name);
            if (arrow != null) arrowViews[room] = arrow;
        }

        var halls = DirectNamed("shifting archway");
        var doorBackings = DirectNamed("door backing");
        var doors = DirectNamed("door");
        var windowFrames = DirectNamed("window frame");
        var glasses = DirectNamed("glass");
        var shards = DirectNamed("shard");
        var hallIndex = 0; var doorIndex = 0; var windowIndex = 0; var shardIndex = 0;
        foreach (var opening in level.Openings)
        {
            var view = new OpeningView();
            if (opening.Kind == OpeningKind.Hall)
            {
                if (hallIndex < halls.Count) view.Slab = halls[hallIndex++].GetComponent<SpriteRenderer>();
            }
            else if (opening.Kind == OpeningKind.Door)
            {
                if (doorIndex < doorBackings.Count) view.Backing = doorBackings[doorIndex].GetComponent<SpriteRenderer>();
                if (doorIndex < doors.Count) view.Slab = doors[doorIndex++].GetComponent<SpriteRenderer>();
            }
            else
            {
                if (windowIndex < windowFrames.Count) view.Backing = windowFrames[windowIndex].GetComponent<SpriteRenderer>();
                if (windowIndex < glasses.Count) view.Slab = glasses[windowIndex++].GetComponent<SpriteRenderer>();
                for (var n = 0; n < 8 && shardIndex < shards.Count; n++) view.Shards.Add(shards[shardIndex++].GetComponent<SpriteRenderer>());
            }
            openingViews[opening] = view;
            if (view.Slab != null) view.Slab.gameObject.SetActive(opening.Kind == OpeningKind.Hall ? opening.Sealed : !opening.Open);
        }
    }

    private static Color FloorColor(RoomHeight h)
    {
        switch (h)
        {
            case RoomHeight.Low: return FloorLow;
            case RoomHeight.Standard: return FloorStandard;
            case RoomHeight.Tall: return FloorTall;
            default: return FloorExit;
        }
    }

    private FrontRoom RoomForWall(int x, int y)
    {
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        foreach (var d in dirs)
        {
            var nx = x + d.x;
            var ny = y + d.y;
            if (nx < 0 || ny < 0 || nx >= level.Width || ny >= level.Height) continue;
            var room = level.RoomAt[nx, ny];
            if (room != null) return room;
        }
        return null;
    }

    private static Color WallColor(RoomRule rule)
    {
        switch (rule)
        {
            case RoomRule.Shift: return Hex("B4A66F");
            case RoomRule.Office: return Hex("C8B980");
            case RoomRule.Run: return Hex("71262D");
            case RoomRule.Exit: return Hex("607C75");
            default: return Hex("D4C47C");
        }
    }

    private void BuildRoomDressing(FrontRoom room, System.Random rng)
    {
        if (room.Rule != RoomRule.Run && room.Rule != RoomRule.Exit)
        {
            var tell = Spr("Tell · " + room.Name, square, FrontRoomsLevel.CenterOf(room.TellTile), new Vector2(0.65f, 0.85f), Paper, 5);
            tellViews[room] = tell;
            Spr("Tell mark", square, FrontRoomsLevel.CenterOf(room.TellTile), new Vector2(0.4f, 0.12f), Ink, 6);
        }
        // Footprints are readable landmarks leading from the threshold toward the tell.
        for (var i = 0; i < 5 && room.Rule != RoomRule.Exit; i++)
            Spr("footprint", disc, new Vector2(room.Interior.xMin + 0.7f + i * 0.45f, room.Center.y + (i % 2 == 0 ? 0.12f : -0.12f)), new Vector2(0.13f, 0.25f), new Color(0f, 0f, 0f, 0.35f), 2);
        if (room.Rule == RoomRule.Office)
            for (var i = 0; i < 3; i++)
            {
                var desk = new Vector2(room.Interior.xMin + 2.5f + i * 2.6f, room.Interior.yMin + 4.5f);
                Spr("desk quarter", square, desk, new Vector2(1.7f, 0.9f), Hex("3B3430"), 2);
                Spr("paper", square, desk + new Vector2(0.25f, 0.08f), new Vector2(0.4f, 0.3f), Hair, 3);
            }
        // Fluorescent panels: fewer and dimmer under a low ceiling, larger in tall rooms.
        var count = room.Height == RoomHeight.Low ? 1 : room.Height == RoomHeight.Standard ? 2 : room.Height == RoomHeight.Tall ? 3 : 1;
        var size = room.Height == RoomHeight.Tall ? new Vector2(2.4f, 0.5f) : room.Height == RoomHeight.Low ? new Vector2(1.4f, 0.32f) : new Vector2(1.9f, 0.42f);
        for (var i = 0; i < count; i++)
        {
            var x = room.Interior.xMin + (i + 1f) * room.Interior.width / (count + 1f);
            var panel = Spr("light", square, new Vector2(x, room.Center.y + (i % 2 == 0 ? 0.6f : -0.6f)), size, new Color(1f, 1f, 0.9f, 0.16f), 1);
            lightPanels.Add(panel);
        }

        for (var i = 0; i < 3; i++)
        {
            var sx = room.Interior.xMin + 0.8f + (float)rng.NextDouble() * (room.Interior.width - 1.6f);
            var sy = room.Interior.yMin + 0.8f + (float)rng.NextDouble() * (room.Interior.height - 1.6f);
            Spr("stain", disc, new Vector2(sx, sy), Vector2.one * (0.6f + (float)rng.NextDouble() * 0.9f), new Color(0f, 0f, 0f, 0.08f), 1);
        }

        if (room.HasKey)
        {
            var key = new GameObject("Key · " + room.Name).transform;
            key.SetParent(world, false);
            key.position = FrontRoomsLevel.CenterOf(room.KeyTile);
            Spr("backing", disc, Vector2.zero, Vector2.one * 0.72f, Ink, 5, key);
            Spr("bow", ring, new Vector2(-0.14f, 0f), Vector2.one * 0.3f, Accent, 6, key);
            Spr("blade", square, new Vector2(0.1f, 0f), new Vector2(0.3f, 0.08f), Accent, 6, key);
            Spr("tooth", square, new Vector2(0.2f, -0.06f), new Vector2(0.06f, 0.1f), Accent, 6, key);
            keyViews[room] = key;
        }

        if (room.OwnExits.Count > 0)
        {
            var arrow = new GameObject("Read arrow · " + room.Name).transform;
            arrow.SetParent(world, false);
            Spr("arrow", triangle, Vector2.zero, Vector2.one * 0.8f, Accent, 9, arrow);
            arrow.gameObject.SetActive(false);
            arrowViews[room] = arrow;
        }
    }

    private void BuildOpening(FrontOpening o, System.Random rng)
    {
        var view = new OpeningView();
        var length = o.Tiles.Count;
        var along = o.WallIsVertical ? new Vector2(0.4f, length) : new Vector2(length, 0.4f);
        var core = o.WallIsVertical ? new Vector2(0.24f, length - 0.1f) : new Vector2(length - 0.1f, 0.24f);
        if (o.Kind == OpeningKind.Hall)
        {
            view.Slab = Spr("shifting archway", square, o.Center, o.WallIsVertical ? new Vector2(1f, length) : new Vector2(length, 1f), WallTone, 3);
            view.Slab.gameObject.SetActive(o.Sealed);
        }
        else if (o.Kind == OpeningKind.Door)
        {
            view.Backing = Spr("door backing", square, o.Center, along, Ink, 3);
            view.Slab = Spr("door", square, o.Center, core, Accent, 4);
        }
        else if (o.Kind == OpeningKind.Window)
        {
            view.Backing = Spr("window frame", square, o.Center, along, Ink, 3);
            view.Slab = Spr("glass", square, o.Center, core, Glass, 4);
            for (var i = 0; i < 8; i++)
            {
                var offset = new Vector2(((float)rng.NextDouble() - 0.5f) * 1.8f, ((float)rng.NextDouble() - 0.5f) * 1.8f);
                var shard = Spr("shard", square, o.Center + offset, Vector2.one * (0.08f + (float)rng.NextDouble() * 0.1f), Glass, 4);
                shard.transform.rotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 90f);
                shard.gameObject.SetActive(false);
                view.Shards.Add(shard);
            }
        }
        openingViews[o] = view;
    }

    private SpriteRenderer Spr(string name, Sprite sprite, Vector2 pos, Vector2 size, Color color, int order, Transform parent = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : world, false);
        if (parent != null) go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        else go.transform.position = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.color = color;
        r.sortingOrder = order;
        return r;
    }

    // ------------------------------------------------------------------ HUD

    private void BuildHud()
    {
        var canvasGo = new GameObject("HUD");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = canvasGo.GetComponent<RectTransform>();

        vignette = MakeImage(canvasGo.transform, "Proximity", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);

        // The play HUD follows the 12-column system: 72 px outer margins, 24 px rhythm.
        // Gameplay information sits directly over the room. There is no persistent header
        // window: the world remains the dominant surface and typography carries the hierarchy.
        roomTitle = MakeText(canvasGo.transform, "Room", new Vector2(0f, 1f), new Vector2(72f, -68f), new Vector2(1060f, 64f), 50, Paper, TextAnchor.UpperLeft, serifFont);
        roomTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
        roomTitle.verticalOverflow = VerticalWrapMode.Overflow;
        hunterValue = MakeText(canvasGo.transform, "Hunter distance", new Vector2(1f, 1f), new Vector2(-72f, -68f), new Vector2(700f, 32f), 20, Paper, TextAnchor.UpperRight, monoFont);
        hunterValue.horizontalOverflow = HorizontalWrapMode.Overflow;
        // A single accent rule keeps threat legible without turning it into a progress window.
        hunterBar = MakeImage(canvasGo.transform, "Threat rule", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-72f, -102f), new Vector2(240f, 4f), Accent);
        hunterBar.rectTransform.pivot = new Vector2(1f, 0.5f);
        // Internal diagnostics are available only with F1; never mixed into the normal play view.
        hunterStateText = MakeText(canvasGo.transform, "Debug", new Vector2(1f, 1f), new Vector2(-72f, -106f), new Vector2(500f, 32f), 13, Hair, TextAnchor.UpperRight, monoFont);
        // One contextual line only. It is typography over the room, with no prompt window.
        promptText = MakeText(canvasGo.transform, "Context", new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(1200f, 48f), 24, Paper, TextAnchor.MiddleCenter, serifFont);
        promptText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.95f);

        foreach (var room in level.Rooms)
        {
            var label = MakeText(canvasGo.transform, "Map note", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(305f, 120f), 20, Paper, TextAnchor.MiddleCenter, serifFont);
            label.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.95f);
            label.gameObject.SetActive(false);
            mapLabels.Add(label);
        }
        overlay = new GameObject("Menu");
        overlay.transform.SetParent(canvasGo.transform, false);
        var overlayImage = overlay.AddComponent<Image>();
        overlayImage.color = new Color(Ink.r, Ink.g, Ink.b, 0.96f);
        var rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        overlayText = MakeText(overlay.transform, "Menu text", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300f, 820f), 24, Paper, TextAnchor.MiddleCenter, bayonFont);
        overlayText.supportRichText = true;
        overlayText.color = Ink;
        overlayText.rectTransform.anchoredPosition = new Vector2(0f, -170f);
        // The provided SVG is the black FrontRooms brand mark. Keep the title
        // surface light so the original artwork remains unchanged and legible.
        logoImage = MakeImage(overlay.transform, "FrontRooms brand logo", new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, -160f), new Vector2(965f, 192f), Color.white);
        logoImage.preserveAspect = true;
        var logoTexture = Resources.Load<Texture2D>("Brand/FrontRoomsLogo");
        if (logoTexture != null)
        {
            brandLogo = Sprite.Create(logoTexture, new Rect(0f, 0f, logoTexture.width, logoTexture.height), new Vector2(.5f, .5f), 100f);
            brandLogo.name = "FrontRooms brand logo (runtime)";
            logoImage.sprite = brandLogo;
        }
    }

    private Text MakeText(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, Color color, TextAnchor align, Font face = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = face ?? font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.lineSpacing = 1.05f;
        text.raycastTarget = false;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(align == TextAnchor.UpperRight || align == TextAnchor.LowerRight ? 1f : align == TextAnchor.UpperLeft || align == TextAnchor.LowerLeft ? 0f : 0.5f,
                                 align == TextAnchor.UpperLeft || align == TextAnchor.UpperRight || align == TextAnchor.UpperCenter ? 1f :
                                 align == TextAnchor.LowerLeft || align == TextAnchor.LowerRight ? 0f : 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return text;
    }

    private Image MakeImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return image;
    }

    // ------------------------------------------------------------------ flow

    private void ShowTitle()
    {
        phase = Phase.Title;
        overlay.SetActive(true);
        if (logoImage != null) logoImage.gameObject.SetActive(true);
        overlay.GetComponent<Image>().color = new Color(.93f, .92f, .88f, .98f);
        overlayText.text = "<size=24>Read the room before the hunter catches you.</size>\n\n" +
            "<size=20>WASD  Move     Shift  Run\n" +
            "Hold Q  Read a nearby note     Hold E  Break nearby glass</size>\n\n" +
            "<color=#F4DF3B>SPACE  Start</color>\n\n" +
            "<size=13><color=#999999>During play: Tab  Map / notes     Esc  Pause</color></size>";
    }

    private void StartRun()
    {
        phase = Phase.Playing;
        overlay.SetActive(false);
        if (logoImage != null) logoImage.gameObject.SetActive(false);
        skipTitle = true;
        runTime = 0f;
        EnterRoom(level.RoomOf(level.PlayerStart));
        Log("run start · control " + controlMode);
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        HandleGlobalKeys();
        if (pilotSteps != null) TickPilot();

        if (phase == Phase.Playing)
        {
            var dt = Time.deltaTime;
            runTime += dt;
            UpdatePlayer(dt);
            UpdateRoomTracking();
            UpdateInteractions(dt);
            UpdateDoors(dt);
            UpdateRoomRule(dt);
            UpdateHunter(dt);
            CheckEnd();
        }

        UpdateRings(Time.unscaledDeltaTime);
        UpdateVisuals();
        UpdateCamera();
        UpdateHud();
    }

    private void HandleGlobalKeys()
    {
        if (Input.GetKeyDown(KeyCode.F)) Screen.fullScreen = !Screen.fullScreen;
        if (Input.GetKeyDown(KeyCode.F1)) debugView = !debugView;
        if (Input.GetKeyDown(KeyCode.Tab)) mapView = !mapView;

        if (Input.GetKeyDown(KeyCode.C) && phase != Phase.Title)
        {
            controlMode = controlMode == ControlMode.WASD ? ControlMode.Cursor : ControlMode.WASD;
            Flash(controlMode == ControlMode.WASD ? "WASD controls" : "Cursor controls: hold left mouse to move");
            Event("control", controlMode.ToString());
            if (phase == Phase.Paused) SetPaused(true);
            if (phase == Phase.Caught || phase == Phase.Escaped) ShowResults();
        }

        switch (phase)
        {
            case Phase.Title:
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) StartRun();
                break;
            case Phase.Playing:
                if (Input.GetKeyDown(KeyCode.Escape)) SetPaused(true);
                break;
            case Phase.Paused:
                if (Input.GetKeyDown(KeyCode.Escape)) SetPaused(false);
                if (Input.GetKeyDown(KeyCode.R)) Restart();
                break;
            case Phase.Escaped:
            case Phase.Caught:
                if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space)) Restart();
                break;
        }
    }

    private void SetPaused(bool paused)
    {
        phase = paused ? Phase.Paused : Phase.Playing;
        Time.timeScale = paused ? 0f : 1f;
        overlay.SetActive(paused);
        if (logoImage != null) logoImage.gameObject.SetActive(false);
        if (paused) overlayText.text = "<size=88><b>PAUSED</b></size>\n\n" +
            "<size=24>" + (controlMode == ControlMode.WASD ? "WASD  Move     Shift  Run\n" : "Hold left mouse  Move     Shift  Run\n") +
            "Hold Q  Read a nearby note\n" +
            (controlMode == ControlMode.WASD ? "Hold E  Break nearby glass\n" : "Hold right mouse  Break nearby glass\n") +
            "Walk over a key to take it. Touch its door to open it.</size>\n\n" +
            "<size=20>Tab  Map / notes     C  Switch controls (" + controlMode + ")\n" +
            "R  Restart     F  Fullscreen</size>\n\n<color=#F4DF3B>Esc  Resume</color>";
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ------------------------------------------------------------------ player

    private void UpdatePlayer(float dt)
    {
        var move = Vector2.zero;
        var run = false;
        if (pilotSteps != null)
        {
            move = pilotMove;
            run = pilotRun;
        }
        else if (controlMode == ControlMode.WASD)
        {
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.y += 1f;
            run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
        else if (Input.GetMouseButton(0))
        {
            var mouse = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 20f));
            var toMouse = (Vector2)mouse - playerPos;
            if (toMouse.magnitude > 0.2f) move = toMouse;
            run = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        if (Input.GetKey(KeyCode.Q) || pilotRead) move = Vector2.zero;
        if (move.sqrMagnitude < 0.0001f)
        {
            stillTimer += dt;
            runStepTimer = 0f;
            return;
        }

        stillTimer = 0f;
        var speed = run ? runSpeed : walkSpeed;
        var before = playerPos;
        playerPos = TryMove(playerPos, move.normalized * speed * dt);

        if (Vector2.Distance(before, playerPos) > 0.001f)
        {
            runStepTimer += dt;
            if (runStepTimer >= (run ? 0.33f : 0.52f))
            {
                runStepTimer = 0f;
                sfx.PlayOneShot(run ? clipPlayerRunStep : clipPlayerStep, run ? 0.52f : 0.20f);
                if (run) MakeNoise(playerPos, runNoise, "running");
            }
        }
        else runStepTimer = 0f;
    }

    private Vector2 TryMove(Vector2 pos, Vector2 delta)
    {
        var p = pos;
        var steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.18f));
        var step = delta / steps;
        for (var i = 0; i < steps; i++)
        {
            var nx = new Vector2(p.x + step.x, p.y);
            if (Free(nx)) p = nx;
            var ny = new Vector2(p.x, p.y + step.y);
            if (Free(ny)) p = ny;
        }
        return p;
    }

    private bool Free(Vector2 c)
    {
        for (var i = 0; i < 4; i++)
        {
            var corner = c + new Vector2(i % 2 == 0 ? -PlayerRadius : PlayerRadius, i < 2 ? -PlayerRadius : PlayerRadius);
            var tile = FrontRoomsLevel.TileOf(corner);
            if (level.PlayerPassable(tile)) continue;
            var door = level.OpeningOf(tile);
            if (door != null && door.Kind == OpeningKind.Door) BumpDoor(door);
            return false;
        }
        return true;
    }

    private void BumpDoor(FrontOpening door)
    {
        if (door.Open) return;
        if (keys.Contains(door.Owner.Id))
        {
            door.Open = true;
            door.CloseTimer = 0f;
            sfx.PlayOneShot(clipDoorOpen, 0.8f);
            MakeNoise(door.Center, doorNoise, "door opens");
            Event("door_opened", door.Owner.Name);
            Log("door opened · " + door.Owner.Name + " → " + door.Other.Name);
        }
        else
        {
            var where = currentRoom == door.Owner ? "its key is somewhere in this room" : "its key is in " + door.Owner.Name;
            Flash("Locked. Find the office key.");
        }
    }

    // ------------------------------------------------------------------ rooms, keys, windows, reading

    private void UpdateRoomTracking()
    {
        var tile = FrontRoomsLevel.TileOf(playerPos);
        var opening = level.OpeningOf(tile);
        if (opening != null) lastOpening = opening;
        var room = level.RoomOf(tile);
        if (room == null || room == currentRoom) return;
        RecordDecision(currentRoom, room);
        EnterRoom(room);
    }

    private void EnterRoom(FrontRoom room)
    {
        currentRoom = room;
        roomEnterTime = runTime;
        stillTimer = 0f;
        windowProgress = 0f;
        readProgress = 0f;
        if (room.Rule == RoomRule.Run)
        {
            Flash("Run. Break the window ahead.");
            MakeNoise(playerPos, WindowNoise, "red corridor alarm");
        }
        humSource.pitch = room.Height == RoomHeight.Low ? 0.9f : room.Height == RoomHeight.Tall ? 1.08f : 1f;
        humSource.volume = room.Height == RoomHeight.Low ? 0.42f : 0.32f;
        Log("enter " + room.Name);
    }

    private void RecordDecision(FrontRoom from, FrontRoom to)
    {
        if (from == null) return;
        var via = lastOpening != null && (lastOpening.Owner == from && lastOpening.Other == to || lastOpening.Owner == to && lastOpening.Other == from) ? lastOpening : null;
        var d = new Decision
        {
            Time = runTime,
            From = from.Name,
            To = to.Name,
            Exit = via == null ? "?" : via.Kind.ToString().ToUpperInvariant(),
            HunterDistance = Vector2.Distance(playerPos, hunterPos),
            HunterState = hunterState,
            SecondsInRoom = runTime - roomEnterTime,
            Read = from.Read,
            Keys = keys.Count,
            Mode = controlMode,
        };
        decisions.Add(d);
        Log(string.Format("decision · {0} → {1} via {2} · hunter {3:0.0} m ({4}) · {5:0.0} s in room", d.From, d.To, d.Exit, d.HunterDistance, d.HunterState, d.SecondsInRoom));
    }

    private void UpdateInteractions(float dt)
    {
        var room = currentRoom;
        if (room == null) return;

        if (room.HasKey && !room.KeyTaken && Vector2.Distance(playerPos, FrontRoomsLevel.CenterOf(room.KeyTile)) < 0.6f)
        {
            room.KeyTaken = true;
            keys.Add(room.Id);
            keyViews[room].gameObject.SetActive(false);
            sfx.PlayOneShot(clipKey, 0.9f);
            MakeNoise(playerPos, keyNoise, "key grabbed");
            Flash("Key taken. The yellow door will open.");
            Event("key", room.Name);
            Log("key · " + room.Name);
        }

        // Window: hold E (or right mouse in cursor mode) while next to unbroken glass.
        FrontOpening window = null;
        var best = WindowReach;
        foreach (var o in room.AllOpenings)
        {
            if (o.Kind != OpeningKind.Window || o.Broken) continue;
            var d = DistanceToOpening(playerPos, o);
            if (d < best) { best = d; window = o; }
        }
        if (window != null)
        {
            var holding = pilotSteps != null ? pilotBreak : Input.GetKey(KeyCode.E) || (controlMode == ControlMode.Cursor && Input.GetMouseButton(1));
            if (holding)
            {
                windowProgress += dt;
                if (windowProgress >= breakWindowSeconds) BreakWindow(window);
            }
            else windowProgress = Mathf.Max(0f, windowProgress - dt * 2f);
        }
        else windowProgress = 0f;

        // Reading is an intentional, silent hold on a visible tell, not a shortest-route oracle.
        var canRead = !room.Read && room.Rule != RoomRule.Run && room.Rule != RoomRule.Exit &&
            Vector2.Distance(playerPos, FrontRoomsLevel.CenterOf(room.TellTile)) < 1.4f;
        var holdingRead = pilotSteps != null ? pilotRead : Input.GetKey(KeyCode.Q);
        if (canRead && holdingRead && stillTimer > 0f)
        {
            if (readProgress <= 0f) Event("read_started", room.Name);
            readProgress += dt;
            if (readProgress >= readSeconds)
            {
                room.Read = true;
                readsCompleted++;
                readProgress = 0f;
                if (tellViews.ContainsKey(room)) tellViews[room].color = Accent;
                Flash(room.RuleText);
                transientTimer = 6f;
                Event("read", room.Name);
                Log("read · " + room.Name);
            }
        }
        else
        {
            if (readProgress > 0f) Event("read_cancelled", room.Name + " after " + readProgress.ToString("0.00", CultureInfo.InvariantCulture) + " s");
            readProgress = 0f;
        }
    }

    private void UpdateRoomRule(float dt)
    {
        // A camera-visible archway never shifts. TAB deliberately holds every archway in view.
        var gateA = level.Openings[2];
        var gateB = level.Openings[3];
        var a = cam.WorldToViewportPoint(gateA.Center);
        var b = cam.WorldToViewportPoint(gateB.Center);
        bool visible = mapView || InView(a) || InView(b);
        bool occupied = DistanceToOpening(playerPos, gateA) < 1.5f || DistanceToOpening(playerPos, gateB) < 1.5f ||
            DistanceToOpening(hunterPos, gateA) < 1.5f || DistanceToOpening(hunterPos, gateB) < 1.5f;
        if (visible || occupied) { shiftTimer = 0f; shiftWarning = false; }
        else
        {
            shiftTimer += dt;
            if (shiftTimer >= 3f && !shiftWarning)
            {
                shiftWarning = true;
                Event("shift_warning", "hum drops");
                if (currentRoom.Rule == RoomRule.Shift || currentRoom.Rule == RoomRule.Lobby) Flash("The hum drops...");
            }
            if (shiftTimer >= 4f)
            {
                gateA.Sealed = !gateA.Sealed;
                gateB.Sealed = !gateA.Sealed;
                openingViews[gateA].Slab.gameObject.SetActive(gateA.Sealed);
                openingViews[gateB].Slab.gameObject.SetActive(gateB.Sealed);
                shiftTimer = 0f;
                shiftWarning = false;
                shifts++;
                Repath(hunterTarget);
                Event("shift", gateA.Sealed ? "upper hall open" : "lower hall open");
            }
        }
        humSource.volume = shiftWarning ? 0.04f : currentRoom.Rule == RoomRule.Run ? 0.2f : 0.32f;
        if (currentRoom.Rule == RoomRule.Run) humSource.pitch = 1.35f;
    }

    private static bool InView(Vector3 p) => p.z > 0f && p.x >= 0f && p.x <= 1f && p.y >= 0f && p.y <= 1f;

    private void Event(string kind, string detail)
    {
        events.Add(string.Join(",", runTime.ToString("0.000", CultureInfo.InvariantCulture), kind, Csv(detail),
            Vector2.Distance(playerPos, hunterPos).ToString("0.00", CultureInfo.InvariantCulture), controlMode.ToString()));
    }

    private static float DistanceToOpening(Vector2 p, FrontOpening o)
    {
        var best = float.MaxValue;
        foreach (var t in o.Tiles)
        {
            var dx = Mathf.Max(t.x - p.x, 0f, p.x - (t.x + 1f));
            var dy = Mathf.Max(t.y - p.y, 0f, p.y - (t.y + 1f));
            best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dy * dy));
        }
        return best;
    }

    private void BreakWindow(FrontOpening window)
    {
        windowsBroken++;
        Event("glass", window.Owner.Name);
        window.Broken = true;
        window.Open = true;
        windowProgress = 0f;
        var view = openingViews[window];
        view.Slab.gameObject.SetActive(false);
        view.Backing.color = new Color(Ink.r, Ink.g, Ink.b, 0.35f);
        foreach (var s in view.Shards) s.gameObject.SetActive(true);
        sfx.PlayOneShot(clipGlass, 1f);
        shake = 0.35f;
        MakeNoise(window.Center, WindowNoise, "glass");
        Flash("The hunter heard the glass.");
        Log("window broken · " + window.Owner.Name + " ↔ " + window.Other.Name);
    }

    private void UpdateDoors(float dt)
    {
        foreach (var o in level.Openings)
        {
            if (o.Kind != OpeningKind.Door || !o.Open || o.Broken) continue;
            var playerClear = DistanceToOpening(playerPos, o) > 1.1f;
            var hunterClear = DistanceToOpening(hunterPos, o) > 0.8f;
            if (playerClear && hunterClear)
            {
                o.CloseTimer += dt;
                if (o.CloseTimer >= 0.6f)
                {
                    o.Open = false;
                    o.CloseTimer = 0f;
                    sfx.PlayOneShot(clipDoorSlam, 0.6f);
                    MakeNoise(o.Center, slamNoise, "door shuts");
                }
            }
            else o.CloseTimer = 0f;
        }
    }

    // ------------------------------------------------------------------ hunter (Curtain states: listen → hunt → search, chase, break the door)

    private void MakeNoise(Vector2 position, float radius, string what)
    {
        SpawnRing(position, Mathf.Min(radius, 14f), radius >= WindowNoise ? 1.2f : 0.7f);
        if (phase != Phase.Playing) return;
        var heard = Vector2.Distance(hunterPos, position) <= radius;
        Event("noise", what + (heard ? " / heard" : " / out of range"));
        if (!heard || hunterState == HunterState.Chase) return;
        if (hunterAwake) hunterTarget = position;
        else { hunterTarget = position; return; }
        hunterTarget = position;
        if (hunterState != HunterState.BreakDoor) SetHunterState(HunterState.Hunt);
        Repath(hunterTarget);
        Log("hunter heard " + what);
    }

    private void SetHunterState(HunterState s)
    {
        if (hunterState == s) return;
        hunterState = s;
        stateTimer = 0f;
        Log("hunter · " + s);
    }

    private void Repath(Vector2 target)
    {
        hunterPath = level.FindHunterPath(FrontRoomsLevel.TileOf(hunterPos), FrontRoomsLevel.TileOf(target));
        repathTimer = 0f;
    }

    private void UpdateHunter(float dt)
    {
        if (!hunterAwake)
        {
            if (runTime < hunterReleaseTime) return;
            hunterAwake = true;
            // A fixed first patrol target makes reading cost distance even before the first loud action.
            hunterTarget = new Vector2(15.5f, 3.5f);
            SetHunterState(HunterState.Hunt);
            Repath(hunterTarget);
            Event("hunter_released", "initial patrol");
        }
        stateTimer += dt;
        hunterStepTimer += dt;
        var hunterBefore = hunterPos;
        repathTimer += dt;
        var distance = Vector2.Distance(hunterPos, playerPos);
        var sees = distance <= sightRange && level.LineOfSight(hunterPos, playerPos);
        if (sees)
        {
            lastSeenPlayer = playerPos;
            lostSightTimer = 0f;
            if (hunterState != HunterState.Chase && hunterState != HunterState.BreakDoor)
            {
                SetHunterState(HunterState.Chase);
                Repath(playerPos);
            }
        }

        switch (hunterState)
        {
            case HunterState.Listen:
                if (stateTimer > 1.5f)
                {
                    var patrolRoom = level.RoomOf(FrontRoomsLevel.TileOf(hunterPos));
                    var nextId = Mathf.Min(level.Rooms.Count - 1, (patrolRoom != null ? patrolRoom.Id : 0) + 1);
                    hunterTarget = level.Rooms[nextId].Center;
                    SetHunterState(HunterState.Hunt);
                    Repath(hunterTarget);
                }
                break;
            case HunterState.Hunt:
                if (FollowPath(hunterHuntSpeed, dt)) SetHunterState(HunterState.Search);
                break;
            case HunterState.Search:
                var wander = hunterPos + new Vector2(Mathf.Sin(stateTimer * 2.3f), Mathf.Cos(stateTimer * 1.7f)) * 0.3f * dt;
                if (level.PlayerPassable(FrontRoomsLevel.TileOf(wander))) hunterPos = wander;
                if (stateTimer > 2.5f) SetHunterState(HunterState.Listen);
                break;
            case HunterState.Chase:
                if (repathTimer > 0.3f) Repath(sees ? playerPos : lastSeenPlayer);
                if (sees && distance < 1.5f) hunterPos = Vector2.MoveTowards(hunterPos, playerPos, hunterChaseSpeed * dt);
                else FollowPath(hunterChaseSpeed, dt);
                if (!sees)
                {
                    lostSightTimer += dt;
                    if (lostSightTimer > 1.5f)
                    {
                        hunterTarget = lastSeenPlayer;
                        SetHunterState(HunterState.Hunt);
                        Repath(hunterTarget);
                    }
                }
                break;
            case HunterState.BreakDoor:
                bangTimer += dt;
                if (bangTimer >= 0.5f)
                {
                    bangTimer = 0f;
                    var volume = Mathf.Clamp01(1f - Vector2.Distance(playerPos, hunterPos) / 18f);
                    sfx.PlayOneShot(clipDoorBang, 0.25f + volume * 0.75f);
                    SpawnRing(breakingDoor.Center, 2.5f, 0.5f);
                }
                if (stateTimer >= breakDoorSeconds)
                {
                    hunterDoorsBroken++;
                    Event("door_broken", breakingDoor.Owner.Name);
                    breakingDoor.Broken = true;
                    breakingDoor.Open = true;
                    openingViews[breakingDoor].Slab.color = Muted;
                    openingViews[breakingDoor].Slab.transform.localScale *= 0.55f;
                    Log("hunter broke door · " + breakingDoor.Owner.Name);
                    breakingDoor = null;
                    if (level.LineOfSight(hunterPos, playerPos) && Vector2.Distance(hunterPos, playerPos) <= sightRange)
                    {
                        SetHunterState(HunterState.Chase);
                        Repath(playerPos);
                    }
                    else
                    {
                        SetHunterState(HunterState.Hunt);
                        Repath(hunterTarget);
                    }
                }
                break;
        }

        if (Vector2.Distance(hunterBefore, hunterPos) > 0.001f)
        {
            // The hunter's cadence tightens during chase and the low, spatial
            // clip is panned toward its side of the 2D field.
            var cadence = hunterState == HunterState.Chase ? 0.30f : 0.46f;
            if (hunterStepTimer >= cadence)
            {
                hunterStepTimer = 0f;
                PlayHunterStep();
            }
        }

        if (Vector2.Distance(hunterPos, playerPos) < catchRadius && level.LineOfSight(hunterPos, playerPos)) EndRun(false, "The hunter reached you in " + (currentRoom != null ? currentRoom.Name : "the dark") + ".");
    }

    private void PlayHunterStep()
    {
        var distance = Vector2.Distance(playerPos, hunterPos);
        var volume = Mathf.Clamp01(1f - distance / 18f) * (hunterState == HunterState.Chase ? 1f : 0.72f);
        hunterSfx.panStereo = Mathf.Clamp((hunterPos.x - playerPos.x) / 8f, -1f, 1f);
        hunterSfx.PlayOneShot(clipHunterStep, 0.28f + volume * 0.72f);
        SpawnRing(hunterPos, Mathf.Clamp(1.2f + volume * 2.2f, 1.2f, 3.4f), 0.34f);
    }

    /// <summary>Walk the current path. Returns true when the path is used up.</summary>
    private bool FollowPath(float speed, float dt)
    {
        if (hunterPath.Count == 0) return true;
        var next = hunterPath[0];
        if (!level.CanHunterTraverse(next)) { Repath(hunterTarget); return false; }
        var door = level.OpeningOf(next);
        if (door != null && door.Kind == OpeningKind.Door && !door.Open)
        {
            breakingDoor = door;
            bangTimer = 0.5f;
            SetHunterState(HunterState.BreakDoor);
            return false;
        }
        var target = FrontRoomsLevel.CenterOf(next);
        hunterPos = Vector2.MoveTowards(hunterPos, target, speed * dt);
        if (Vector2.Distance(hunterPos, target) < 0.05f) hunterPath.RemoveAt(0);
        return hunterPath.Count == 0;
    }

    private void CheckEnd()
    {
        if (phase != Phase.Playing) return;
        if (level.ExitTiles.Contains(FrontRoomsLevel.TileOf(playerPos))) EndRun(true, "You found the noclip patch.");
    }

    private void EndRun(bool escaped, string reason)
    {
        if (phase != Phase.Playing) return;
        phase = escaped ? Phase.Escaped : Phase.Caught;
        outcomeReason = reason;
        sfx.PlayOneShot(escaped ? clipEscape : clipCaught, 1f);
        Log((escaped ? "ESCAPED" : "CAUGHT") + string.Format(" · {0:0.0} s · {1} decisions", runTime, decisions.Count));
        Event("outcome", escaped ? (readsCompleted >= 3 && keys.Count > 0 ? "informed_escape" : "fast_escape") : "caught");
        SaveCsv();
        ShowResults();
    }

    // ------------------------------------------------------------------ results + telemetry

    private void ShowResults()
    {
        overlay.SetActive(true);
        if (logoImage != null) logoImage.gameObject.SetActive(false);
        var escaped = phase == Phase.Escaped;
        var heading = escaped ? "Escaped" : "Caught";
        var detail = escaped ? (readsCompleted >= 3 && keys.Count > 0 ? "You left with all three notes and the key." : "You found a way out.") : "Try another route, or spend less time looking.";
        overlayText.text = "<size=88><b>" + heading.ToUpperInvariant() + "</b></size>\n\n" +
            "<size=24>" + detail + "</size>\n" +
            string.Format("<size=20>{0:0} seconds   ·   {1}/3 notes</size>", runTime, readsCompleted) +
            "\n\n<color=#F4DF3B>R  Try again</color>";
    }

    private void SaveCsv()
    {
        try
        {
            var dir = pilotDir ?? Application.persistentDataPath;
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "frontrooms_playtests.csv");
            var writeHeader = !File.Exists(path);
            var sb = new StringBuilder();
            if (writeHeader) sb.AppendLine("run_id,timestamp,control,outcome,total_s,step,t_s,from,to,exit,hunter_m,hunter_state,seconds_in_room,read,keys");
            var runId = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + runCounter;
            var outcome = phase == Phase.Escaped ? "escaped" : "caught";
            var step = 0;
            foreach (var d in decisions)
            {
                if (d.From == d.To) continue;
                sb.AppendLine(string.Join(",", runId, DateTime.Now.ToString("s"), d.Mode, outcome, runTime.ToString("0.0", CultureInfo.InvariantCulture), (++step).ToString(),
                    d.Time.ToString("0.0", CultureInfo.InvariantCulture), Csv(d.From), Csv(d.To), d.Exit, d.HunterDistance.ToString("0.0", CultureInfo.InvariantCulture), d.HunterState, d.SecondsInRoom.ToString("0.0", CultureInfo.InvariantCulture), d.Read ? "1" : "0", d.Keys.ToString()));
            }
            if (step == 0) sb.AppendLine(string.Join(",", runId, DateTime.Now.ToString("s"), controlMode, outcome, runTime.ToString("0.0", CultureInfo.InvariantCulture), "0", "", "", "", "", "", "", "", "", ""));
            File.AppendAllText(path, sb.ToString());
            savedCsvPath = path;
            File.WriteAllText(Path.Combine(dir, "events-" + runId + ".csv"), "time_s,event,detail,hunter_m,control\n" + string.Join("\n", events));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[FrontRooms] could not write playtest CSV: " + e.Message);
        }
    }

    private static string Csv(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

    // ------------------------------------------------------------------ visuals

    private void SpawnRing(Vector2 pos, float radius, float life)
    {
        var r = Spr("noise", ring, pos, Vector2.one * 0.2f, Accent, 7);
        rings.Add(new Ring { Renderer = r, Life = life, Size = radius * 2f });
    }

    private void UpdateRings(float dt)
    {
        for (var i = rings.Count - 1; i >= 0; i--)
        {
            var r = rings[i];
            r.Age += dt;
            var k = r.Age / r.Life;
            if (k >= 1f)
            {
                Destroy(r.Renderer.gameObject);
                rings.RemoveAt(i);
                continue;
            }
            r.Renderer.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, r.Size, Mathf.Sqrt(k));
            var c = Accent;
            c.a = 0.85f * (1f - k);
            r.Renderer.color = c;
        }
    }

    private void UpdateVisuals()
    {
        var t = Time.unscaledTime;
        player.position = new Vector3(playerPos.x, playerPos.y, 0f);
        hunter.position = new Vector3(hunterPos.x, hunterPos.y, 0f);
        var chasing = hunterState == HunterState.Chase;
        hunter.localScale = Vector3.one * (chasing ? 1f + 0.08f * Mathf.Sin(t * 14f) : 1f);

        foreach (var pair in keyViews)
            pair.Value.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * 4f));
        foreach (var pair in arrowViews)
            if (pair.Value.gameObject.activeSelf) pair.Value.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(t * 5f));

        foreach (var pair in openingViews)
        {
            var o = pair.Key;
            if (o.Kind == OpeningKind.Door && !o.Broken)
            {
                var c = Accent;
                c.a = o.Open ? 0.25f : 1f;
                pair.Value.Slab.color = c;
            }
        }

        for (var i = 0; i < lightPanels.Count; i++)
        {
            var flicker = Mathf.PerlinNoise(i * 3.1f, t * 2.2f) < 0.12f ? 0.04f : 0.16f;
            var c = lightPanels[i].color;
            c.a = flicker;
            lightPanels[i].color = c;
        }

        var glow = Paper;
        glow.a = 0.55f + 0.3f * Mathf.Sin(t * 3f);
        exitGlow.color = glow;

        var showPath = debugView && hunterPath.Count > 0;
        for (var i = 0; i < pathDots.Count; i++)
        {
            var on = showPath && i < hunterPath.Count;
            pathDots[i].gameObject.SetActive(on);
            if (on) pathDots[i].transform.position = FrontRoomsLevel.CenterOf(hunterPath[i]);
        }
    }

    private void UpdateCamera()
    {
        var aspect = cam.aspect;
        Vector2 center;
        float size;
        if (mapView)
        {
            size = Mathf.Max(level.Height * 0.5f, level.Width * 0.5f / aspect) + 1.2f;
            center = new Vector2(level.Width * 0.5f, level.Height * 0.5f);
        }
        else
        {
            size = 7.2f;
            var halfW = size * aspect;
            center = new Vector2(
                ClampAxis(playerPos.x, halfW, level.Width),
                ClampAxis(playerPos.y, size, level.Height));
        }
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, size, Time.unscaledDeltaTime * 6f);
        var current = (Vector2)cam.transform.position;
        var next = Vector2.Lerp(current, center, Time.unscaledDeltaTime * 6f);
        if (shake > 0f)
        {
            shake -= Time.unscaledDeltaTime;
            next += UnityEngine.Random.insideUnitCircle * shake * 0.5f;
        }
        cam.transform.position = new Vector3(next.x, next.y, -20f);
    }

    private static float ClampAxis(float value, float half, float extent)
    {
        var min = half - 1.5f;
        var max = extent - half + 1.5f;
        return min > max ? extent * 0.5f : Mathf.Clamp(value, min, max);
    }

    private void UpdateHud()
    {
        var distance = Vector2.Distance(playerPos, hunterPos);
        var closeness = Mathf.Clamp01(1f - distance / 18f);
        hunterValue.text = string.Format("HUNTER  /  {0:00} M", distance);
        hunterValue.color = closeness > 0.65f ? Accent : Paper;
        hunterBar.rectTransform.sizeDelta = new Vector2(240f * closeness, 4f);
        hunterStateText.text = debugView ? StateWord(hunterState) : string.Empty;
        vignette.color = new Color(0f, 0f, 0f, phase == Phase.Playing ? closeness * closeness * 0.35f : 0f);
        if (phase == Phase.Playing)
        {
            heartbeatTimer -= Time.deltaTime;
            if (distance < 10f && heartbeatTimer <= 0f)
            {
                sfx.PlayOneShot(clipHeartbeat, Mathf.Lerp(0.2f, 1f, closeness));
                heartbeatTimer = Mathf.Lerp(1.2f, 0.42f, closeness);
            }
        }
        roomTitle.text = mapView ? "MAP / NOTES" : currentRoom == null ? "" : RoomName(currentRoom);
        promptText.text = mapView && phase == Phase.Playing ? "TAB  BACK" : CurrentPrompt();
        UpdateMapLabels();
    }

    private static string RoomName(FrontRoom room)
    {
        switch (room.Rule)
        {
            case RoomRule.Lobby: return "LOBBY";
            case RoomRule.Shift: return "LEVEL 0";
            case RoomRule.Office: return "LEVEL 4 / OFFICE";
            case RoomRule.Run: return "LEVEL ! / RUN";
            default: return "EXIT";
        }
    }

    private string CurrentPrompt()
    {
        if (phase != Phase.Playing) return string.Empty;
        transientTimer -= Time.deltaTime;
        if (windowProgress > 0.02f) return string.Format("Breaking glass  {0:0}%", 100f * windowProgress / breakWindowSeconds);
        if (readProgress > 0f) return string.Format("Reading  {0:0}%", 100f * readProgress / readSeconds);
        if (currentRoom == null) return string.Empty;
        foreach (var o in currentRoom.AllOpenings)
        {
            if (DistanceToOpening(playerPos, o) > 1.1f) continue;
            if (o.Kind == OpeningKind.Window && !o.Broken && DistanceToOpening(playerPos, o) < WindowReach)
                return controlMode == ControlMode.WASD ? "Hold E  Break glass" : "Hold right mouse  Break glass";
            if (o.Kind == OpeningKind.Door && !o.Open && !o.Broken)
                return keys.Contains(o.Owner.Id) ? "Move into the door to open it" : "Locked. Find the office key.";
        }
        if (!currentRoom.Read && tellViews.ContainsKey(currentRoom) && Vector2.Distance(playerPos, FrontRoomsLevel.CenterOf(currentRoom.TellTile)) < 1.4f)
            return "Hold Q  Read note";
        if (transientTimer > 0f) return transientPrompt;
        return string.Empty;
    }

    private void Flash(string message)
    {
        transientPrompt = message;
        transientTimer = 2.6f;
    }

    private void UpdateMapLabels()
    {
        for (var i = 0; i < mapLabels.Count; i++)
        {
            var label = mapLabels[i];
            var on = mapView && phase != Phase.Title;
            label.gameObject.SetActive(on);
            if (!on) continue;
            var room = level.Rooms[i];
            var marker = room == currentRoom ? "YOU ARE HERE" :
                room.Rule == RoomRule.Exit ? "EXIT" :
                room.Read ? "NOTE READ" : "";
            label.text = RoomName(room) + (string.IsNullOrEmpty(marker) ? string.Empty : "\n<size=13>" + marker + "</size>");
            label.supportRichText = true;
            PlaceLabel(label.rectTransform, room.Center);
        }
    }

    private void PlaceLabel(RectTransform rect, Vector2 worldPos)
    {
        var screen = cam.WorldToScreenPoint(new Vector3(worldPos.x, worldPos.y, 0f));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
        rect.localPosition = local;
    }

    private static string StateWord(HunterState s)
    {
        switch (s)
        {
            case HunterState.Listen: return "LISTENING";
            case HunterState.Hunt: return "WALKING TO YOUR LAST NOISE";
            case HunterState.Search: return "SEARCHING";
            case HunterState.Chase: return "CHASING YOU";
            default: return "BREAKING A DOOR";
        }
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }

    private static void Log(string message) => Debug.Log("[FrontRooms] " + message);

    // ------------------------------------------------------------------ autotest pilot

    private void BuildPilot()
    {
        pilotSteps = new List<PilotStep>();
        void Shot(string name) => pilotSteps.Add(new PilotStep { Kind = "shot", Name = name, Seconds = 0.25f });
        void Move(float x, float y, bool run = true) => pilotSteps.Add(new PilotStep { Kind = "move", Target = new Vector2(x, y), Run = run, Seconds = 12f });
        void Wait(float seconds) => pilotSteps.Add(new PilotStep { Kind = "wait", Seconds = seconds });
        void Read() => pilotSteps.Add(new PilotStep { Kind = "read", Seconds = 1.6f });
        void Break() => pilotSteps.Add(new PilotStep { Kind = "break", Seconds = 1.1f });
        Wait(0.6f); Shot("01_title");
        pilotSteps.Add(new PilotStep { Kind = "start" });
        if (pilotRoute == "caught")
        {
            Wait(18f);
            pilotSteps.Add(new PilotStep { Kind = "quit", Seconds = 0.5f });
            return;
        }
        if (pilotRoute == "door") { Move(8.5f, 5.5f); Read(); Wait(1.5f); Shot("02_read_note"); }
        Move(10.5f, 3.5f); Move(14.5f, 3.5f);
        if (pilotRoute == "door") { Move(17.5f, 5.5f); Read(); Shot("03_level0_rule"); }
        // Choose the currently open hall using the same geometry a player sees.
        pilotSteps.Add(new PilotStep { Kind = "hall", Seconds = 12f });
        Move(27f, 5.5f);
        if (pilotRoute == "door")
        {
            Move(29.5f, 5.5f); Read(); Shot("04_office_rule");
            Move(33.5f, 8.5f); Shot("05_key");
            Move(34.5f, 3f); Move(39f, 3f); Shot("06_door_closes");
            pilotSteps.Add(new PilotStep { Kind = "waitDoor", Seconds = 25f });
            Shot("07_hunter_breaking_door");
            Move(46.5f, 5f); Move(47.45f, 5f); Break();
        }
        else
        {
            Move(35.45f, 8f); Break(); Shot("05_office_glass");
            Move(39f, 8f); Move(47.45f, 5f); Break();
        }
        Shot("08_final_window");
        Move(51f, 5f); Move(57.5f, 5f);
        Wait(0.3f); Shot("09_result");
        pilotSteps.Add(new PilotStep { Kind = "quit", Seconds = 0.5f });
    }

    [Serializable] private sealed class PilotReport
    {
        public bool passed;
        public string route, outcome, control;
        public float seconds;
        public int reads, keys, windows, hunterDoors, roomDecisions, shifts;
        public string evidence = "Scripted player simulation; does not establish human playability or feedback.";
    }

    private void FinishPilot()
    {
        bool expected = pilotRoute == "caught" ? phase == Phase.Caught :
            phase == Phase.Escaped && decisions.Count >= 4 && windowsBroken >= (pilotRoute == "door" ? 1 : 2);
        if (pilotRoute == "door") expected &= readsCompleted == 3 && keys.Count == 1 && hunterDoorsBroken >= 1 && shifts >= 1;
        if (pilotRoute == "fast") expected &= readsCompleted == 0 && keys.Count == 0;
        var report = new PilotReport { passed = !pilotFailed && expected, route = pilotRoute, outcome = phase.ToString(),
            control = controlMode.ToString(), seconds = runTime, reads = readsCompleted, keys = keys.Count, windows = windowsBroken,
            hunterDoors = hunterDoorsBroken, roomDecisions = decisions.Count, shifts = shifts };
        File.WriteAllText(Path.Combine(pilotDir, "result.json"), JsonUtility.ToJson(report, true));
        Log("AUTOTEST " + (report.passed ? "PASS" : "FAIL"));
        Application.Quit(report.passed ? 0 : 1);
    }

    private void TickPilot()
    {
        pilotMove = Vector2.zero;
        pilotRun = false;
        pilotBreak = false;
        pilotRead = false;
        if (pilotIndex >= pilotSteps.Count) return;
        if (!pilotFinishing && (phase == Phase.Caught || phase == Phase.Escaped))
        {
            pilotFinishing = true;
            pilotSteps = new List<PilotStep> {
                new PilotStep { Kind = "wait", Seconds = 0.3f },
                new PilotStep { Kind = "shot", Name = "09_result", Seconds = 0.3f },
                new PilotStep { Kind = "quit", Seconds = 0.3f }
            };
            pilotIndex = 0;
            pilotTimer = 0f;
        }
        var step = pilotSteps[pilotIndex];
        pilotTimer += Time.unscaledDeltaTime;
        var done = false;
        switch (step.Kind)
        {
            case "wait": done = pilotTimer >= step.Seconds; break;
            case "start": if (phase == Phase.Title) StartRun(); done = true; break;
            case "read": pilotRead = true; done = pilotTimer >= step.Seconds; break;
            case "shot":
                if (!Application.isBatchMode && pilotTimer <= Time.unscaledDeltaTime + 0.0001f)
                    ScreenCapture.CaptureScreenshot(Path.Combine(pilotDir, step.Name + ".png"));
                done = pilotTimer >= step.Seconds;
                break;
            case "hall":
                var gate = !level.Openings[2].Sealed ? level.Openings[2] : level.Openings[3];
                var target = playerPos.x < 22.5f ? new Vector2(22.7f, gate.Center.y) : new Vector2(26.5f, gate.Center.y);
                pilotMove = target - playerPos;
                pilotRun = true;
                done = playerPos.x > 26.2f;
                if (pilotTimer > step.Seconds) { pilotFailed = true; done = true; Log("pilot hall timeout"); }
                break;
            case "waitDoor":
                done = hunterState == HunterState.BreakDoor;
                if (pilotTimer > step.Seconds) { pilotFailed = true; done = true; Log("pilot door timeout"); }
                break;
            case "move":
                var to = step.Target - playerPos;
                done = to.magnitude < 0.12f;
                if (pilotTimer > step.Seconds) { pilotFailed = true; done = true; Log("pilot stuck at " + playerPos + " toward " + step.Target); }
                if (!done) { pilotMove = to; pilotRun = step.Run; }
                break;
            case "break": pilotBreak = true; done = pilotTimer >= step.Seconds; break;
            case "map":
                if (pilotTimer <= Time.unscaledDeltaTime + 0.0001f) mapView = !mapView;
                done = pilotTimer >= step.Seconds;
                break;
            case "quit": if (pilotTimer >= step.Seconds) FinishPilot(); break;
        }
        if (done) { pilotIndex++; pilotTimer = 0f; }
    }
}
