using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runtime-generated MVP for the Threshold Rooms pitch.
/// It deliberately uses legacy Unity input calls so a clean Mac or Windows
/// checkout can play without an InputActions asset or PlayerInput prefab.
/// </summary>
public sealed class ThresholdRunPrototype : MonoBehaviour
{
    private enum RunState { Playing, Paused, Won, Failed }
    private enum RoomId { Level0, Level4, LevelBang, Exit }

    private sealed class Fragment
    {
        public GameObject Object;
        public Vector2 Position;
        public bool Collected;
        public bool Bonus;
    }

    [Header("Run tuning")]
    [SerializeField] private float runDuration = 180f;
    [SerializeField] private float walkSpeed = 4.2f;
    [SerializeField] private float sprintSpeed = 7.2f;
    [SerializeField] private float staminaMax = 100f;
    [SerializeField] private float sprintDrainPerSecond = 25f;
    [SerializeField] private float staminaRecoverPerSecond = 24f;
    [SerializeField] private float collectHoldSeconds = 1f;

    private readonly List<Fragment> fragments = new List<Fragment>();
    private readonly Color level0Color = new Color(0.76f, 0.65f, 0.26f);
    private readonly Color level4Color = new Color(0.24f, 0.33f, 0.28f);
    private readonly Color levelBangColor = new Color(0.55f, 0.08f, 0.10f);

    private GameObject player;
    private GameObject pursuer;
    private GameObject level0Marker;
    private GameObject worldDim;
    private GameObject threatFlash;
    private GameObject tutorialPanel;
    private GameObject pausePanel;
    private GameObject resultPanel;
    private Text hudText;
    private Text roomRuleText;
    private Text promptText;
    private Text tutorialText;
    private Text resultText;
    private Text pauseText;
    private Camera gameplayCamera;
    private RunState state;
    private RoomId room = RoomId.Level0;
    private Vector2 playerPosition;
    private Vector2 pursuerPosition;
    private float remainingTime;
    private float stamina;
    private float collectProgress;
    private float threatFlashTimer;
    private float lastThreatCueTime = -10f;
    private int tutorialStep;
    private bool tutorialSkipped;
    private bool level0Shifted;
    private bool blackWindowTriggered;
    private bool threatStarted;
    private bool sprintWasSeen;
    private bool firstMoveSeen;

    private const float MinX = -19f;
    private const float MaxX = 22f;
    private const float MinY = -3.5f;
    private const float MaxY = 3.5f;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        Time.timeScale = 1f;
        BuildHud();
        BuildWorld();
        ResetRun();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab) && state == RunState.Playing)
        {
            tutorialSkipped = true;
            tutorialPanel.SetActive(false);
        }

        if (Input.GetKeyDown(KeyCode.Escape) && (state == RunState.Playing || state == RunState.Paused))
        {
            TogglePause();
            return;
        }

        if ((state == RunState.Won || state == RunState.Failed) && Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        if (state != RunState.Playing)
        {
            return;
        }

        UpdateRunClock();
        UpdateMovement();
        UpdateRoomEffects();
        UpdateCollectionAndExit();
        UpdatePursuer();
        UpdateCamera();
        UpdateTutorial();
        UpdateHud();

        if (threatFlashTimer > 0f)
        {
            threatFlashTimer -= Time.deltaTime;
            threatFlash.SetActive(true);
            var color = threatFlash.GetComponent<Image>().color;
            color.a = Mathf.Clamp01(threatFlashTimer * 0.8f);
            threatFlash.GetComponent<Image>().color = color;
        }
        else
        {
            threatFlash.SetActive(false);
        }
    }

    private void BuildWorld()
    {
        gameplayCamera = new GameObject("MVP Camera").AddComponent<Camera>();
        gameplayCamera.orthographic = true;
        gameplayCamera.orthographicSize = 6.2f;
        gameplayCamera.backgroundColor = new Color(0.03f, 0.035f, 0.03f);
        gameplayCamera.transform.position = new Vector3(0f, 0f, -20f);

        MakeBlock("Level0_Backdrop", new Vector2(-11.5f, 0f), new Vector2(15f, 9f), new Color(0.16f, 0.14f, 0.07f), 2f);
        MakeBlock("Level4_Backdrop", new Vector2(2f, 0f), new Vector2(12f, 9f), new Color(0.07f, 0.13f, 0.11f), 2f);
        MakeBlock("LevelBang_Backdrop", new Vector2(14f, 0f), new Vector2(12f, 9f), new Color(0.18f, 0.025f, 0.03f), 2f);
        MakeBlock("Level0_Floor", new Vector2(-11.5f, -4.2f), new Vector2(15f, 0.18f), level0Color, 1f);
        MakeBlock("Level4_Floor", new Vector2(2f, -4.2f), new Vector2(12f, 0.18f), level4Color, 1f);
        MakeBlock("LevelBang_Floor", new Vector2(14f, -4.2f), new Vector2(12f, 0.18f), levelBangColor, 1f);
        MakeBlock("Level0_Boundary", new Vector2(-4f, 0f), new Vector2(0.12f, 8.3f), new Color(0.8f, 0.7f, 0.25f), 0.5f);
        MakeBlock("Level4_Boundary", new Vector2(8f, 0f), new Vector2(0.12f, 8.3f), new Color(0.3f, 0.7f, 0.5f), 0.5f);

        MakeWorldLabel("LEVEL 0 / THRESHOLD", new Vector2(-17.5f, 3.65f), level0Color);
        MakeWorldLabel("LEVEL 4 / OFFICE", new Vector2(-1.5f, 3.65f), new Color(0.4f, 0.8f, 0.6f));
        MakeWorldLabel("LEVEL ! / RUN", new Vector2(9.5f, 3.65f), new Color(1f, 0.25f, 0.25f));

        level0Marker = MakeBlock("Peripheral_Marker", new Vector2(-6.9f, 2.2f), new Vector2(0.55f, 0.55f), new Color(0.95f, 0.75f, 0.2f), 0f);
        MakeBlock("SupplyZone", new Vector2(0.2f, 2.1f), new Vector2(2.2f, 1.2f), new Color(0.13f, 0.33f, 0.23f), 0.2f);
        MakeWorldLabel("ALMOND WATER / RECOVER", new Vector2(-1.0f, 2.55f), new Color(0.55f, 1f, 0.75f));
        MakeBlock("BlackWindowTrap", new Vector2(5.5f, 2.1f), new Vector2(1.2f, 1.2f), new Color(0.015f, 0.015f, 0.015f), 0f);
        MakeWorldLabel("BLACK WINDOW", new Vector2(4.6f, 2.9f), new Color(1f, 0.3f, 0.3f));
        MakeBlock("ExitDoor", new Vector2(21.2f, 0f), new Vector2(0.6f, 7.5f), new Color(0.8f, 0.8f, 0.68f), -0.1f);
        MakeWorldLabel("EXIT", new Vector2(20.5f, 3.6f), Color.white);

        MakeBlackoutZone();
        AddFragment(new Vector2(-8.4f, 1.1f), false, new Color(1f, 0.8f, 0.15f));
        AddFragment(new Vector2(2.7f, -1.35f), false, new Color(0.45f, 1f, 0.75f));
        AddFragment(new Vector2(13.9f, 2.05f), true, new Color(1f, 0.25f, 0.25f));

        player = MakeBlock("Player", new Vector2(-18f, -1.6f), new Vector2(0.72f, 0.72f), Color.white, -1f);
        pursuer = MakeBlock("Hound_Pursuer", new Vector2(-21f, -1.6f), new Vector2(0.78f, 0.78f), new Color(1f, 0.15f, 0.12f), -0.5f);
        pursuer.SetActive(false);

        worldDim = MakeUiImage("BlackoutOverlay", new Color(0f, 0f, 0f, 0f), false);
        threatFlash = MakeUiImage("ThreatFlash", new Color(0.85f, 0f, 0f, 0f), false);
    }

    private void MakeBlackoutZone()
    {
        MakeBlock("Level0_BlackoutZone", new Vector2(-12.3f, 0f), new Vector2(2.4f, 7.8f), new Color(0.02f, 0.02f, 0.02f), 0.1f);
        MakeWorldLabel("BLACKOUT / FOLLOW LIGHT", new Vector2(-13.8f, -3.55f), new Color(0.5f, 0.5f, 0.5f));
    }

    private void BuildHud()
    {
        var canvasObject = new GameObject("MVP HUD");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        hudText = MakeUiText("HUD", canvas.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(800f, 120f), 30, Color.white, TextAnchor.UpperLeft);
        roomRuleText = MakeUiText("RoomRule", canvas.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -35f), new Vector2(1050f, 90f), 27, new Color(1f, 0.82f, 0.32f), TextAnchor.UpperCenter);
        promptText = MakeUiText("Prompt", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 55f), new Vector2(1400f, 70f), 28, Color.white, TextAnchor.MiddleCenter);

        tutorialPanel = MakeUiPanel("TutorialPanel", canvas.transform, new Color(0.015f, 0.02f, 0.018f, 0.94f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120f, 420f));
        tutorialText = MakeUiText("TutorialText", tutorialPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1020f, 330f), 32, Color.white, TextAnchor.MiddleCenter);

        pausePanel = MakeUiPanel("PausePanel", canvas.transform, new Color(0.02f, 0.025f, 0.02f, 0.94f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 260f));
        pauseText = MakeUiText("PauseText", pausePanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 220f), 34, Color.white, TextAnchor.MiddleCenter);
        pausePanel.SetActive(false);

        resultPanel = MakeUiPanel("ResultPanel", canvas.transform, new Color(0.02f, 0.025f, 0.02f, 0.97f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 360f));
        resultText = MakeUiText("ResultText", resultPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 320f), 34, Color.white, TextAnchor.MiddleCenter);
        resultPanel.SetActive(false);
    }

    private void ResetRun()
    {
        state = RunState.Playing;
        room = RoomId.Level0;
        playerPosition = new Vector2(-18f, -1.6f);
        pursuerPosition = new Vector2(-21f, -1.6f);
        remainingTime = runDuration;
        stamina = staminaMax;
        collectProgress = 0f;
        threatStarted = false;
        level0Shifted = false;
        blackWindowTriggered = false;
        sprintWasSeen = false;
        firstMoveSeen = false;
        tutorialStep = 0;
        tutorialSkipped = false;
        player.transform.position = new Vector3(playerPosition.x, playerPosition.y, -1f);
        pursuer.transform.position = new Vector3(pursuerPosition.x, pursuerPosition.y, -0.5f);
        pursuer.SetActive(false);
        worldDim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        resultPanel.SetActive(false);
        pausePanel.SetActive(false);
        tutorialPanel.SetActive(true);
        Time.timeScale = 1f;
        UpdateHud();
    }

    private void UpdateRunClock()
    {
        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            FailRun("TIMEOUT / 时间耗尽");
        }
    }

    private void UpdateMovement()
    {
        var input = Vector2.zero;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.y -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.y += 1f;

        if (input.sqrMagnitude > 0.001f)
        {
            firstMoveSeen = true;
            if (!tutorialSkipped && tutorialStep == 0) tutorialStep = 1;
        }

        var sprinting = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && input.sqrMagnitude > 0.001f && stamina > 0.1f;
        var speed = sprinting ? sprintSpeed : walkSpeed;
        if (sprinting)
        {
            stamina = Mathf.Max(0f, stamina - sprintDrainPerSecond * Time.deltaTime);
            if (!sprintWasSeen)
            {
                sprintWasSeen = true;
                if (!tutorialSkipped && tutorialStep == 4) tutorialStep = 5;
            }
        }
        else if (room == RoomId.Level4 && Vector2.Distance(playerPosition, new Vector2(0.2f, 2.1f)) < 1.55f)
        {
            stamina = Mathf.Min(staminaMax, stamina + staminaRecoverPerSecond * Time.deltaTime);
        }
        else
        {
            stamina = Mathf.Min(staminaMax, stamina + 5f * Time.deltaTime);
        }

        playerPosition += input.normalized * speed * Time.deltaTime;
        playerPosition.x = Mathf.Clamp(playerPosition.x, MinX, MaxX);
        playerPosition.y = Mathf.Clamp(playerPosition.y, MinY, MaxY);
        player.transform.position = new Vector3(playerPosition.x, playerPosition.y, -1f);
    }

    private void UpdateRoomEffects()
    {
        var previousRoom = room;
        room = playerPosition.x < -4f ? RoomId.Level0 : playerPosition.x < 8f ? RoomId.Level4 : playerPosition.x < 20.5f ? RoomId.LevelBang : RoomId.Exit;
        if (room == RoomId.Level4 && previousRoom == RoomId.Level0 && !tutorialSkipped && tutorialStep == 5) tutorialStep = 6;

        var inBlackout = room == RoomId.Level0 && playerPosition.x > -13.5f && playerPosition.x < -11.1f;
        worldDim.GetComponent<Image>().color = inBlackout ? new Color(0f, 0f, 0f, 0.48f) : new Color(0f, 0f, 0f, 0f);
        if (!inBlackout && room == RoomId.Level0 && firstMoveSeen && !level0Shifted && playerPosition.x > -10.8f)
        {
            level0Shifted = true;
            level0Marker.transform.position = new Vector3(-7.6f, 2.2f, 0f);
            if (!tutorialSkipped && tutorialStep == 1) tutorialStep = 2;
        }

        if (room == RoomId.Level4 && !blackWindowTriggered && Vector2.Distance(playerPosition, new Vector2(5.5f, 2.1f)) < 1.1f)
        {
            blackWindowTriggered = true;
            remainingTime = Mathf.Max(0f, remainingTime - 4f);
            PulseThreat("BLACK WINDOW / 黑窗陷阱");
        }
    }

    private void UpdateCollectionAndExit()
    {
        Fragment nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var fragment in fragments)
        {
            if (fragment.Collected) continue;
            var distance = Vector2.Distance(playerPosition, fragment.Position);
            if (distance < nearestDistance)
            {
                nearest = fragment;
                nearestDistance = distance;
            }
        }

        if (nearest != null && nearestDistance < 1.65f)
        {
            promptText.text = string.Format("HOLD E  {0:0.0}s  /  长按 E 收集  ·  噪声会吸引追逐者", Mathf.Max(0f, collectHoldSeconds - collectProgress));
            if (Input.GetKey(KeyCode.E))
            {
                collectProgress += Time.deltaTime;
                if (collectProgress >= collectHoldSeconds)
                {
                    nearest.Collected = true;
                    nearest.Object.SetActive(false);
                    collectProgress = 0f;
                    if (!threatStarted)
                    {
                        threatStarted = true;
                        pursuer.SetActive(true);
                        pursuerPosition = playerPosition + new Vector2(-3.5f, 0f);
                        PulseThreat("NOISE PING / 收集制造噪声");
                    }
                    else
                    {
                        PulseThreat("COLLECTION PING / 收集噪声");
                    }
                    if (!tutorialSkipped && tutorialStep == 3) tutorialStep = 4;
                }
            }
            else
            {
                collectProgress = Mathf.Max(0f, collectProgress - Time.deltaTime * 2f);
            }
            return;
        }

        if (room == RoomId.Exit || playerPosition.x >= 20f)
        {
            var count = CollectedCount();
            promptText.text = count >= 2 ? "RUN TO EXIT  /  冲向出口" : string.Format("EXIT LOCKED  ·  NEED 2/3 FRAGMENTS  /  还需要 {0} 枚", 2 - count);
            if (count >= 2 && playerPosition.x >= 21f)
            {
                WinRun();
            }
            return;
        }

        if (room == RoomId.Level4 && Vector2.Distance(playerPosition, new Vector2(0.2f, 2.1f)) < 1.55f)
        {
            promptText.text = "ALMOND WATER  ·  STAMINA RECOVERING  /  耐力恢复中";
            return;
        }

        promptText.text = string.Empty;
    }

    private void UpdatePursuer()
    {
        if (!threatStarted || !pursuer.activeSelf) return;
        var direction = playerPosition - pursuerPosition;
        if (direction.sqrMagnitude > 0.01f)
        {
            pursuerPosition += direction.normalized * 3.35f * Time.deltaTime;
            pursuer.transform.position = new Vector3(pursuerPosition.x, pursuerPosition.y, -0.5f);
        }
        var distance = Vector2.Distance(playerPosition, pursuerPosition);
        if (distance < 0.82f)
        {
            FailRun("HOUND REACHED YOU / 追逐者追上了你");
        }
        if (distance < 3.6f && Time.time - lastThreatCueTime > 1.25f)
        {
            lastThreatCueTime = Time.time;
            PulseThreat("THREAT CLOSE / 追逐者接近");
        }
    }

    private void UpdateCamera()
    {
        if (gameplayCamera == null) return;
        var targetX = Mathf.Clamp(playerPosition.x, -9.5f, 12.5f);
        gameplayCamera.transform.position = Vector3.Lerp(gameplayCamera.transform.position, new Vector3(targetX, 0f, -20f), Time.deltaTime * 5f);
    }

    private void UpdateTutorial()
    {
        if (tutorialSkipped || tutorialStep >= 7)
        {
            tutorialPanel.SetActive(false);
            return;
        }

        tutorialPanel.SetActive(true);
        var text = string.Empty;
        switch (tutorialStep)
        {
            case 0:
                text = "THRESHOLD RUN / 穿越房间\n\nWASD / ARROW KEYS  移动\nSHIFT  冲刺　　 E  交互与长按收集\nESC  暂停　　　 TAB  跳过教程\n\n在快速逃离与停留收集之间做决定。\nMOVE TO BEGIN / 移动开始";
                break;
            case 1:
                text = "T1  ENTER THE THRESHOLD\n\n前方是 Level 0。\n保持移动，先读懂房间再决定是否停下。";
                break;
            case 2:
                text = "T2  BLACKOUT RULE\n\n停电区会压低视野。\nFOLLOW THE LIGHT / 跟随光源，离开后路线会发生一次轻微变化。";
                break;
            case 3:
                text = "T3  COLLECT WITH A COST\n\n靠近黄色碎片，长按 E 1 秒。\nCOLLECTING MAKES NOISE / 收集会制造噪声。";
                break;
            case 4:
                text = "T4  THE HOUND IS A TIMER\n\n追逐者已经被噪声吸引。\nSHIFT TO SPRINT / 按 Shift 冲刺，别让它追上。";
                break;
            case 5:
                text = "T5  ROOM 4 TRADE-OFF\n\n绿色补给区会恢复耐力；黑窗是陷阱。\n水能让你更快，但停留仍会消耗时间。";
                break;
            case 6:
                text = "T6  RUN FOR YOUR LIFE\n\n红色走廊不提供藏身处。\n持有 2/3 碎片后冲向 EXIT；第三枚是高风险 bonus。";
                break;
        }
        tutorialText.text = text + "\n\nTAB  SKIP TUTORIAL / 跳过教程";
    }

    private void UpdateHud()
    {
        if (hudText == null) return;
        var minutes = Mathf.FloorToInt(remainingTime / 60f);
        var seconds = Mathf.FloorToInt(remainingTime % 60f);
        var roomName = room == RoomId.Level0 ? "LEVEL 0 / THRESHOLD" : room == RoomId.Level4 ? "LEVEL 4 / OFFICE" : room == RoomId.LevelBang ? "LEVEL ! / RUN" : "EXIT";
        var threat = threatStarted ? "ACTIVE" : "QUIET";
        hudText.text = string.Format("TIME  {0:00}:{1:00}\nSTAMINA  {2:000}\nFRAGMENTS  {3}/3\nROOM  {4}\nTHREAT  {5}", minutes, seconds, Mathf.RoundToInt(stamina), CollectedCount(), roomName, threat);
        roomRuleText.text = room == RoomId.Level0 ? "ROOM RULE  ·  FOLLOW THE LIGHT / 跟随光源" : room == RoomId.Level4 ? "ROOM RULE  ·  WATER RECOVERS, BLACK WINDOWS TRAP" : room == RoomId.LevelBang ? "ROOM RULE  ·  RUN / NO HIDING / 不要停下" : "ROOM RULE  ·  EXIT REQUIRES 2/3";
    }

    private void TogglePause()
    {
        if (state == RunState.Playing)
        {
            state = RunState.Paused;
            Time.timeScale = 0f;
            pauseText.text = "PAUSED / 已暂停\n\nESC  RESUME / 继续\nR  RETRY / 重开";
            pausePanel.SetActive(true);
        }
        else if (state == RunState.Paused)
        {
            state = RunState.Playing;
            Time.timeScale = 1f;
            pausePanel.SetActive(false);
        }
    }

    private void WinRun()
    {
        state = RunState.Won;
        Time.timeScale = 0f;
        resultPanel.SetActive(true);
        resultText.text = string.Format("RUN COMPLETE / 成功逃离\n\nFRAGMENTS  {0}/3\nTIME LEFT  {1:0}s\nBONUS  {2}\n\nR  RETRY / 重开", CollectedCount(), remainingTime, fragments[2].Collected ? "TAKEN / 已取得" : "SKIPPED / 已放弃");
    }

    private void FailRun(string reason)
    {
        if (state != RunState.Playing) return;
        state = RunState.Failed;
        Time.timeScale = 0f;
        resultPanel.SetActive(true);
        resultText.text = string.Format("RUN FAILED / 逃脱失败\n\n{0}\nFRAGMENTS  {1}/3\n\nR  RETRY / 重开", reason, CollectedCount());
    }

    private void PulseThreat(string message)
    {
        threatFlashTimer = 0.65f;
        promptText.text = message;
    }

    private int CollectedCount()
    {
        var count = 0;
        foreach (var fragment in fragments) if (fragment.Collected) count++;
        return count;
    }

    private void AddFragment(Vector2 position, bool bonus, Color color)
    {
        var obj = MakeBlock(bonus ? "BonusFragment" : "ThresholdFragment", position, new Vector2(0.7f, 0.7f), color, -0.2f);
        fragments.Add(new Fragment { Object = obj, Position = position, Bonus = bonus });
    }

    private GameObject MakeBlock(string name, Vector2 position, Vector2 scale, Color color, float z)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.position = new Vector3(position.x, position.y, z);
        block.transform.localScale = new Vector3(scale.x, scale.y, 0.25f);
        var renderer = block.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Unlit/Color"));
        renderer.material.color = color;
        return block;
    }

    private void MakeWorldLabel(string value, Vector2 position, Color color)
    {
        var label = new GameObject(value);
        label.transform.position = new Vector3(position.x, position.y, -0.3f);
        var text = label.AddComponent<TextMesh>();
        text.text = value;
        text.fontSize = 42;
        text.characterSize = 0.09f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = color;
    }

    private GameObject MakeUiImage(string name, Color color, bool active)
    {
        var obj = new GameObject(name);
        var canvas = GameObject.Find("MVP HUD");
        obj.transform.SetParent(canvas != null ? canvas.transform : null, false);
        obj.transform.SetAsFirstSibling();
        var image = obj.AddComponent<Image>();
        image.color = color;
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        obj.SetActive(active);
        return obj;
    }

    private GameObject MakeUiPanel(string name, Transform parent, Color color, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        var image = panel.AddComponent<Image>();
        image.color = color;
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return panel;
    }

    private Text MakeUiText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor alignment)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var text = obj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return text;
    }
}
