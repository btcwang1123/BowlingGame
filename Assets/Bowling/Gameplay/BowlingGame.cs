using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// The game itself: modes (standard 1–4 players, practice, challenges), turns, the throw
// (swipe or phone swing), waiting for pins to settle, and scoring. Racking/sweeping/sway is
// RackController's job, camera/aim-line is CameraRig's, instant replay is ReplayRecorder's.
// Everything on screen is drawn by BowlingUI, which reads this class.
public class BowlingGame : MonoBehaviour
{
    public enum Mode { Standard, Practice, Challenge, Calibration }
    public enum Phase { Menu, Aiming, Throwing, Rolling, Settling, Result, Replay, Finished }

    [Header("Swipe → throw tuning")]
    [Tooltip("Upward swipe speed (screen heights per second) that gives the slowest / fastest ball.")]
    [SerializeField] float slowSwipe = 0.6f, fastSwipe = 4f;
    [SerializeField] float minSpeed = 4f, maxSpeed = 10f;
    [Tooltip("Ball angle (deg) per degree of swipe angle. Small, because 1° is ~30 cm at the pins.")]
    [SerializeField] float anglePerSwipeAngle = 0.06f;
    [SerializeField] float maxAngle = 2f;
    [Tooltip("Swipe curve below this is treated as straight, so normal swipes don't hook by accident.")]
    [SerializeField] float curveDeadZone = 0.3f;
    [Tooltip("Hook (m/s²) per unit of swipe curve. 0.15 m/s² bends the ball ~40 cm by the pins.")]
    [SerializeField] float hookPerCurve = 0.08f;
    [SerializeField] float maxHook = 0.15f;

    [Header("Camera")]
    [Tooltip("Aiming camera, relative to the ball's spot on the foul line.")]
    [SerializeField] Vector3 aimCameraOffset = new(0.35f, 1.75f, -4.4f);
    [SerializeField] float aimCameraPitch = 11f;
    [SerializeField] float followHeight = 0.9f, followDistance = 2.2f, followMaxZ = 15f, followPitch = 10f;

    // Written by BowlingSceneBuilder; the editor rebuilds scenes older than its SceneVersion.
    [SerializeField, HideInInspector] int sceneVersion;

    const float SwipeWindow = 0.2f;     // only the end of the swipe decides the throw
    const float LockUpward = 0.03f;     // finger moved this far up (screen heights) → stop positioning, it's a throw
    const float MaxRollTime = 10f;
    const float MinSettleTime = 1.5f, MaxSettleTime = 4f;
    const float MinResultTime = 0.35f;   // a tap this soon after the result shows is the throw's own finger lifting
    const float WipeTime = 1.1f;
    const float OilPerThrow = 0.2f;     // the ball picks up lane oil with every throw…
    const float OilHookLoss = 0.45f;    // …and a fully oily ball hooks this much less
    const float ForwardTimeout = 3f;    // backswing seen, but no forward swing this long after → stop
    const float ArmTimeout = 10f;       // OK pressed but no complete swing at all this long → stop

    public static readonly Color[] PlayerColors =
        { new(0.3f, 1f, 1f), new(1f, 0.4f, 0.9f), new(1f, 0.85f, 0.3f), new(0.6f, 1f, 0.4f) };

    // Practice leaves: pin numbers to rack. Empty = random leave each time.
    public static readonly int[][] PracticeLeaves =
    {
        new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, new[] { 7 }, new[] { 10 }, new[] { 7, 10 },
        new[] { 4, 6, 7, 10 }, new[] { 2, 4, 5, 8 }, new[] { 3, 6, 10 }, new[] { 1, 2, 4, 7 }, new int[0],
    };

    BowlingBall ball;
    Bowler bowler;
    Pin[] pins;            // index 0 = pin 1 … index 9 = pin 10
    BowlingAudio sound;
    BowlingFX fx;
    ChallengeKit kit;
    MotionInput motion;
    RackController rack;
    ReplayRecorder replayRecorder;
    CameraRig cameraRig;
    VenueStyler venueStyler;
    IMatchDriver driver = new LocalMatchDriver();

    // ---- State the UI reads ----
    public Mode CurrentMode { get; private set; }
    public Phase CurrentPhase { get; private set; } = Phase.Menu;
    public bool Paused { get; private set; }
    public List<BowlingScore> Scores { get; } = new();
    public int CurrentPlayer { get; private set; }
    public string Message { get; private set; } = "";
    public float MessageTime { get; private set; }
    public bool MessageRainbow { get; private set; }
    public string LastThrowInfo { get; private set; } = "";
    public bool NewBest { get; private set; }
    public float GameTopSpeedKmh { get; private set; }
    public bool UsingMotion => GameSettings.Control == ControlMode.Motion && MotionInput.Available;
    public bool MotionRecording => motion != null && motion.Recording;
    public MotionInput Motion => motion;
    // Motion "ready, then swing" flow: true after the player presses OK and the game is capturing
    // the swing. The ball releases automatically when the swing completes (no need to hold the screen).
    public bool MotionArmed { get; private set; }
    // The armed swing took too long (no forward swing seen): the capture stopped, so a stray
    // movement later can't throw the ball. The player presses OK again.
    public bool SwingTimedOut { get; private set; }
    // 0–1: how much of the current wait has run out, for the on-screen countdown.
    public float SwingTimeoutProgress => !MotionArmed ? 0f
        : Mathf.Max((Time.unscaledTime - armedAt) / ArmTimeout,
            motion.Stage == MotionInput.SwingStage.SwingForward ? (Time.unscaledTime - stageSince) / ForwardTimeout : 0f);

    public int PracticeLeave { get; private set; }
    public int PracticeThrows { get; private set; }
    public int PracticeCleared { get; private set; }

    public int ChallengeIndex { get; private set; }
    public ChallengeLevel Challenge => ChallengeLevels.All[ChallengeIndex];
    public int BallsUsed { get; private set; }
    public int ChallengeStars { get; private set; }   // 0 = failed

    public CalibrationSession Calibration { get; private set; }

    // Throw analysis: every throw this game, and the one in flight / just finished.
    public List<ThrowRecord> ThrowLog { get; } = new();
    public ThrowRecord CurrentThrow { get; private set; }
    public float AverageSpeedKmh => ThrowLog.Count == 0 ? 0f : ThrowLog.Average(t => t.speedKmh);

    // Magnifier: a close look at the pins while aiming or after the throw.
    public bool ZoomPins { get; private set; }
    public bool CanZoom => !Paused && (CurrentPhase is Phase.Aiming or Phase.Settling or Phase.Result) && !MotionArmed && !Wiping;

    // Ball wiping: lane oil builds up on the ball with each throw and takes the bite out of its hook.
    public float BallOil { get; private set; }
    public bool Wiping => wipeTime >= 0f;
    public float WipeProgress => Wiping ? Mathf.Clamp01(wipeTime / WipeTime) : 0f;
    public bool CanWipe => CurrentPhase == Phase.Aiming && !Paused && !Wiping && !MotionArmed && !swiping;

    public int CoinsThisGame { get; private set; }

    // Who is playing a standard game: name, calibration profile, look and ball for each player.
    // Empty outside standard games (practice, challenges and calibration use the defaults).
    public List<SaveData.PlayerSlot> Players { get; } = new();
    SaveData.PlayerSlot Slot => Players.Count == 0 ? null : Players[Mathf.Clamp(CurrentPlayer, 0, Players.Count - 1)];

    public string PlayerName(int p) =>
        p >= 0 && p < Players.Count && !string.IsNullOrWhiteSpace(Players[p].name) ? Players[p].name.Trim() : $"P{p + 1}";

    // The current player's ball and outfit: their own pick, or what's equipped in the pro shop.
    public BallDesign CurrentBall
    {
        get
        {
            var id = Slot?.ball;
            if (string.IsNullOrEmpty(id)) return ProShop.Equipped;
            var b = ProShop.Find(id);
            return ProShop.Owns(b) ? b : ProShop.Equipped;
        }
    }

    public Outfit CurrentOutfit
    {
        get
        {
            var id = Slot?.outfit;
            if (string.IsNullOrEmpty(id)) return Outfits.Equipped;
            var o = Outfits.Find(id);
            return Outfits.Owns(o) ? o : Outfits.Equipped;
        }
    }

    // Ball technique for the current player (one-handed, spinner, two-handed), picked while aiming.
    public BallTechnique Technique
    {
        get => CurrentMode == Mode.Calibration ? BallTechnique.OneHanded : techniques[Mathf.Clamp(CurrentPlayer, 0, techniques.Length - 1)];
        set
        {
            if (CurrentMode == Mode.Calibration) return;
            techniques[Mathf.Clamp(CurrentPlayer, 0, techniques.Length - 1)] = value;
            if (Players.Count > 0) Players[Mathf.Min(CurrentPlayer, Players.Count - 1)].technique = (int)value;
            if (Players.Count <= 1) GameSettings.Technique = (int)value;
        }
    }
    public bool CanChangeTechnique => CurrentPhase == Phase.Aiming && !Paused && !Wiping && !MotionArmed && !swiping && CurrentMode != Mode.Calibration;
    readonly BallTechnique[] techniques = new BallTechnique[4];

    public BowlingScore Score => Scores.Count > 0 ? Scores[Mathf.Min(CurrentPlayer, Scores.Count - 1)] : null;
    public bool IsGameOver => CurrentPhase == Phase.Finished;
    public bool CanThrow => CurrentPhase == Phase.Aiming && !Paused && !Wiping && driver.IsLocalTurn;
    public bool CanReplay => CurrentPhase == Phase.Result && ReplayRecorder.HasEnoughFrames(lastReport.replay) && !Paused;
    // After a throw the result, analysis and replay stay up until the player taps the screen.
    public bool WaitingForTap => CurrentPhase == Phase.Result && !Paused && stateTime >= MinResultTime;
    public event System.Action<string, int> RollScored; // mark, pins knocked
    public event System.Action<RollReport> RollFinished;
    public System.Func<Vector2, bool> PointerOverUI;     // set by BowlingUI

    float stateTime;
    readonly List<(float t, Vector2 pos)> swipe = new();
    bool swiping, positionLocked;
    float lowestY, aimX;
    bool[] standingBeforeMask = new bool[10];
    bool rackNext, hitThisThrow;
    float lastSpeed, previousSpeed, lastAngle, lastHook;
    bool foulThisThrow;
    float levelStartTime;
    List<Obstacle> obstacles = new();
    Transform obstacleRoot;
    RollReport lastReport;
    float wipeTime = -1f;
    bool wipedThisBall;
    float armedAt, stageSince;
    MotionInput.SwingStage lastStage;

    void Awake()
    {
        Time.fixedDeltaTime = 0.01f; // smaller physics steps so the fast ball doesn't skip through pins
        ball = FindAnyObjectByType<BowlingBall>();
        bowler = FindAnyObjectByType<Bowler>();
        kit = FindAnyObjectByType<ChallengeKit>();
        var cam = Camera.main;
        // Sort by rack position so pins[i] is pin number i + 1 (rows from the head pin, left to right).
        pins = FindObjectsByType<Pin>()
            .OrderBy(p => Mathf.Round(p.transform.position.z * 10f)).ThenBy(p => p.transform.position.x).ToArray();
        if (ball == null || bowler == null || pins.Length != 10 || cam == null || kit == null)
        {
            Debug.LogError($"Bowling scene is out of date (version {sceneVersion}) — use Bowling → Rebuild Scene.");
            enabled = false;
            return;
        }
        if (!TryGetComponent(out sound)) sound = gameObject.AddComponent<BowlingAudio>();
        if (!TryGetComponent(out motion)) motion = gameObject.AddComponent<MotionInput>();
        fx = FindAnyObjectByType<BowlingFX>();
        if (fx == null) fx = gameObject.AddComponent<BowlingFX>();
        ball.HitPin += OnBallHitPin;
        ball.Exploded += OnBombExploded;
        AudioListener.volume = GameSettings.Volume;
        rack = new RackController(pins);
        replayRecorder = new ReplayRecorder(ball.transform, pins.Select(p => p.transform).ToArray());
        cameraRig = new CameraRig(cam, kit.additiveMat, aimCameraOffset, aimCameraPitch, followHeight, followDistance, followMaxZ, followPitch);
        venueStyler = new VenueStyler(cam, kit.additiveMat, ball.transform);
        ApplyVenue();
        Application.targetFrameRate = 60;
    }

    void Start() => BackToMenu();

    // ---- Starting and leaving modes (called by the UI) ----

    // A standard game with the last line-up (or defaults) for this many players.
    public void StartStandard(int players)
    {
        var slots = new List<SaveData.PlayerSlot>();
        for (int i = 0; i < players; i++) slots.Add(DefaultSlot(i, players));
        StartStandard(slots);
    }

    public static SaveData.PlayerSlot DefaultSlot(int index, int players)
    {
        if (players <= 1)
            return new SaveData.PlayerSlot { profile = GameSettings.DefaultProfile, technique = GameSettings.Technique };
        var saved = SaveSystem.Data.playerSlots;
        if (saved != null && index < saved.Count)
        {
            var s = saved[index];
            return new SaveData.PlayerSlot
            {
                name = s.name, profile = Mathf.Clamp(s.profile, 0, GameSettings.Profiles.Count - 1),
                outfit = s.outfit, ball = s.ball, technique = s.technique,
            };
        }
        return new SaveData.PlayerSlot { profile = Mathf.Min(index, GameSettings.Profiles.Count - 1) };
    }

    public void StartStandard(IList<SaveData.PlayerSlot> lineUp)
    {
        int players = Mathf.Clamp(lineUp.Count, 1, 4);
        GameSettings.PlayerCount = players;
        CurrentMode = Mode.Standard;
        driver = players > 1 ? new LocalMultiplayerMatchDriver() : new LocalMatchDriver();
        Scores.Clear();
        for (int i = 0; i < players; i++) Scores.Add(new BowlingScore());
        SetPlayers(lineUp.Take(players));
        CurrentPlayer = 0;
        BeginMode();
        rack.RackAll();
        NextBall();
    }

    void SetPlayers(IEnumerable<SaveData.PlayerSlot> lineUp)
    {
        Players.Clear();
        Players.AddRange(lineUp);
        for (int i = 0; i < techniques.Length; i++)
            techniques[i] = i < Players.Count ? (BallTechnique)Mathf.Clamp(Players[i].technique, 0, BallTechniques.All.Length - 1) : BallTechnique.OneHanded;
    }

    // Practice, challenges and calibration: one player with the default profile and gear.
    void SoloPlayer()
    {
        Players.Clear();
        GameSettings.ClearTurnProfile();
        techniques[0] = (BallTechnique)Mathf.Clamp(GameSettings.Technique, 0, BallTechniques.All.Length - 1);
    }

    // ---- A standard game left part-way through ----

    public static bool HasSavedMatch
    {
        get
        {
            var m = SaveSystem.Data.savedMatch;
            return m != null && m.valid && m.players != null && m.players.Count > 0;
        }
    }

    public static SaveData.SavedMatch SavedMatch => SaveSystem.Data.savedMatch;

    // Called as every ball is set up and as soon as every roll is scored, so a standard game can
    // always be picked up again. afterRoll: the roll just counted, so save the turn that comes next
    // (worked out from the score and this roll's knockdown, not the pins, which may be replaying).
    void SaveMatch(bool afterRoll = false)
    {
        if (CurrentMode != Mode.Standard || Scores.Count == 0) return;
        if (Scores.All(s => s.IsGameOver)) { DiscardSavedMatch(); return; }
        int next = CurrentPlayer;
        bool[] standing;
        if (!afterRoll) standing = pins.Select(p => p.IsStanding).ToArray();
        else if (Scores[CurrentPlayer].RollInFrame == 0)
        {
            do next = (next + 1) % Scores.Count; while (Scores[next].IsGameOver);
            standing = Enumerable.Repeat(true, 10).ToArray();
        }
        else if (rackNext) standing = Enumerable.Repeat(true, 10).ToArray();
        else standing = Enumerable.Range(0, 10).Select(i => lastReport.standingBefore[i] && !lastReport.knockedMask[i]).ToArray();
        var m = new SaveData.SavedMatch
        {
            valid = true,
            savedAt = System.DateTime.Now.ToString("MM/dd HH:mm"),
            currentPlayer = next,
            ballOil = BallOil,
            standing = standing,
        };
        for (int i = 0; i < Scores.Count; i++)
        {
            var slot = i < Players.Count ? Players[i] : DefaultSlot(i, Scores.Count);
            m.players.Add(new SaveData.SavedPlayer
            {
                slot = slot,
                knocked = Scores[i].Rolls.ToArray(),
                standingBefore = Scores[i].StandingBefore.ToArray(),
            });
        }
        SaveSystem.Data.savedMatch = m;
        SaveSystem.Save();
    }

    public void DiscardSavedMatch()
    {
        SaveSystem.Data.savedMatch = new SaveData.SavedMatch();
        SaveSystem.Save();
    }

    // Continue the saved game: scores, whose turn, and the pins still standing.
    public bool ResumeMatch()
    {
        if (!HasSavedMatch) return false;
        var m = SavedMatch;
        int players = Mathf.Clamp(m.players.Count, 1, 4);
        GameSettings.PlayerCount = players;
        CurrentMode = Mode.Standard;
        driver = players > 1 ? new LocalMultiplayerMatchDriver() : new LocalMatchDriver();
        Scores.Clear();
        for (int i = 0; i < players; i++) Scores.Add(BowlingScore.FromHistory(m.players[i].knocked, m.players[i].standingBefore));
        SetPlayers(m.players.Take(players).Select(p => p.slot));
        CurrentPlayer = Mathf.Clamp(m.currentPlayer, 0, players - 1);
        BeginMode();
        BallOil = Mathf.Clamp01(m.ballOil);
        var score = Scores[CurrentPlayer];
        if (score.IsGameOver || score.RollInFrame == 0 || m.standing == null || m.standing.Length != 10) rack.RackAll();
        else rack.RackStanding(m.standing);
        NextBall();
        return true;
    }


    public void StartPractice(int leave)
    {
        SoloPlayer();
        CurrentMode = Mode.Practice;
        driver = new LocalMatchDriver();
        PracticeLeave = Mathf.Clamp(leave, 0, PracticeLeaves.Length - 1);
        PracticeThrows = PracticeCleared = 0;
        BeginMode();
        rack.RackPractice(PracticeLeave);
        NextBall();
    }

    public void StartChallenge(int level)
    {
        SoloPlayer();
        CurrentMode = Mode.Challenge;
        driver = new LocalMatchDriver();
        ChallengeIndex = Mathf.Clamp(level, 0, ChallengeLevels.All.Count - 1);
        BallsUsed = 0;
        ChallengeStars = 0;
        BeginMode();
        levelStartTime = Time.time;
        obstacleRoot = new GameObject("Challenge Obstacles").transform;
        obstacles = Obstacle.Build(Challenge.obstacles, kit, obstacleRoot);
        rack.RackChallenge(Challenge);
        NextBall();
    }

    // On-lane calibration: six guided motion throws (see CalibrationSession).
    public void StartCalibration()
    {
        SoloPlayer();
        CurrentMode = Mode.Calibration;
        driver = new LocalMatchDriver();
        Calibration = new CalibrationSession();
        BeginMode();
        rack.RackAll();
        NextBall();
    }

    public void Restart()
    {
        switch (CurrentMode)
        {
            case Mode.Standard:
                DiscardSavedMatch();
                StartStandard(Players.Count > 0 ? new List<SaveData.PlayerSlot>(Players) : Enumerable.Range(0, Mathf.Max(Scores.Count, 1)).Select(i => DefaultSlot(i, Scores.Count)).ToList());
                break;
            case Mode.Practice: StartPractice(PracticeLeave); break;
            case Mode.Challenge: StartChallenge(ChallengeIndex); break;
            case Mode.Calibration: StartCalibration(); break;
        }
    }

    // The pro shop or venue picker changed something: show it right away.
    public void ApplyVenue()
    {
        venueStyler.Apply(GameSettings.Venue);
        if (BowlingMusic.Instance != null) BowlingMusic.Instance.Refresh();
    }

    // Sound pack, music and volume from Settings.
    public void ApplyAudio()
    {
        AudioListener.volume = GameSettings.Volume;
        if (sound != null) sound.ApplyPack();
        if (BowlingMusic.Instance != null) BowlingMusic.Instance.Refresh();
    }

    public void ApplyEquipment()
    {
        var design = CurrentBall;
        ball.ApplyLook(ProShop.Surface(design), design.glow, ProShop.SizeScale[GameSettings.BallSize]);
        bowler.ApplyOutfit(CurrentOutfit);
    }

    public void ToggleZoom()
    {
        if (ZoomPins) { ZoomPins = false; return; }
        if (CanZoom) ZoomPins = true;
    }

    public void WipeBall()
    {
        if (!CanWipe) return;
        ZoomPins = false;
        wipeTime = 0f;
        SetMessage("");
    }

    public void BackToMenu()
    {
        ClearObstacles();
        SetPaused(false);
        fx.CancelSlowMo();
        motion.Cancel();
        MotionArmed = false;
        ZoomPins = false;
        wipeTime = -1f;
        Scores.Clear();
        Players.Clear();
        GameSettings.ClearTurnProfile();
        rack.RackAll();
        ball.PickUp();
        ApplyEquipment();
        bowler.LeftHanded = GameSettings.LeftHanded;
        bowler.Ready(ball, 0f);
        aimX = 0f;
        SetMessage("");
        LastThrowInfo = "";
        SetPhase(Phase.Menu);
    }

    public void SetPaused(bool paused)
    {
        if (paused == Paused) return;
        Paused = paused;
        if (paused) { fx.CancelSlowMo(); motion.Cancel(); MotionArmed = false; swiping = false; }
        Time.timeScale = paused ? 0f : BowlingFX.NormalTimeScale;
    }

    public void SetPracticeLeave(int leave)
    {
        PracticeLeave = Mathf.Clamp(leave, 0, PracticeLeaves.Length - 1);
        if (CurrentPhase == Phase.Aiming) rack.RackPractice(PracticeLeave);
    }

    // Motion mode: the player pressed OK after getting into the ready pose. Start capturing the whole
    // swing; UpdateAiming releases the ball automatically when the swing completes.
    public void ArmMotion()
    {
        if (!UsingMotion || CurrentPhase != Phase.Aiming || Paused || Wiping) return;
        ZoomPins = false;
        motion.Grip(calibrating: CurrentMode == Mode.Calibration);
        MotionArmed = true;
        SwingTimedOut = false;
        armedAt = stageSince = Time.unscaledTime;
        lastStage = MotionInput.SwingStage.SwingBack;
        SetMessage("");
    }

    public void DisarmMotion()
    {
        motion.Cancel();
        MotionArmed = false;
    }

    void BeginMode()
    {
        ClearObstacles();
        SetPaused(false);
        NewBest = false;
        GameTopSpeedKmh = 0f;
        aimX = 0f;
        LastThrowInfo = "";
        previousSpeed = 0f;
        ThrowLog.Clear();
        CurrentThrow = null;
        BallOil = 0f;
        CoinsThisGame = 0;
        ZoomPins = false;
        wipeTime = -1f;
    }

    void ClearObstacles()
    {
        if (obstacleRoot != null) Destroy(obstacleRoot.gameObject);
        obstacleRoot = null;
        obstacles.Clear();
    }

    // ---- Turn flow ----

    void NextBall()
    {
        fx.CancelSlowMo();
        MotionArmed = false;
        SwingTimedOut = false;
        wipeTime = -1f;
        wipedThisBall = false;
        // Each player throws with their own calibration, hand, look and ball.
        if (Players.Count > 0) GameSettings.UseProfileForTurn(Slot.profile);
        else GameSettings.ClearTurnProfile();
        ball.PickUp();
        ApplyEquipment();
        ball.Configure(GameSettings.BallMassKg, CurrentMode == Mode.Challenge ? Challenge.ballType : BallType.Normal, pins);
        var design = CurrentBall;
        ball.SetReaction(design.SkidZ, design.Backend);
        if (CurrentMode == Mode.Calibration) aimX = 0f; // calibration throws are all from the middle
        bowler.LeftHanded = GameSettings.LeftHanded;
        bowler.Ready(ball, aimX);
        cameraRig.SnapToAim(aimX);
        SetMessage("");
        SetPhase(Phase.Aiming);
        if (CurrentMode == Mode.Standard) SaveMatch();
    }

    void SetPhase(Phase p)
    {
        CurrentPhase = p;
        stateTime = 0f;
    }

    void SetMessage(string text, bool rainbow = false)
    {
        Message = text;
        MessageTime = Time.unscaledTime;
        MessageRainbow = rainbow;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && CurrentPhase != Phase.Menu)
            SetPaused(!Paused);
        if (Paused) return;

        stateTime += Time.deltaTime;
        switch (CurrentPhase)
        {
            case Phase.Aiming: if (Wiping) UpdateWipe(); else UpdateAiming(); break;
            case Phase.Rolling: UpdateRolling(); break;
            case Phase.Settling: UpdateSettling(); break;
            case Phase.Result: UpdateResult(); break;
            case Phase.Replay: UpdateReplay(); break;
        }
        if (CurrentMode == Mode.Challenge && CurrentPhase is Phase.Aiming or Phase.Throwing)
            rack.UpdateSway(Challenge, Time.time - levelStartTime);
        cameraRig.UpdateAimLine(aimX, GameSettings.AimGuide && CurrentPhase == Phase.Aiming && !Paused);
    }

    // Spin the ball in the bowler's hands with a few sparkles; at the end it's clean.
    void UpdateWipe()
    {
        wipeTime += Time.deltaTime;
        ball.transform.Rotate(new Vector3(0.3f, 1f, 0.2f), 720f * Time.deltaTime, Space.World);
        if (Random.value < 0.35f) fx.Sparks(ball.transform.position, Color.white, 2, 0.6f);
        if (wipeTime < WipeTime) return;
        wipeTime = -1f;
        BallOil = 0f;
        wipedThisBall = true;
        fx.Sparks(ball.transform.position + Vector3.up * 0.1f, BowlingFX.Cyan, 18, 1.5f);
        Haptics.Buzz();
        SetMessage(Loc.T("wipe_done"));
    }

    // ---- Aiming: drag to pick the spot, then flick (swipe) or swing the phone (motion) ----

    void UpdateAiming()
    {
        if (CurrentMode == Mode.Calibration && !UsingMotion) return; // calibration reads real swings only
        if (UsingMotion) UpdateAimingMotion();
        else UpdateAimingSwipe();
    }

    // Motion: drag left/right to pick the spot; the throw itself starts when the player presses OK
    // (ArmMotion) and is released automatically once the swing completes.
    void UpdateAimingMotion()
    {
        var pointer = Pointer.current;
        if (!MotionArmed && CurrentMode != Mode.Calibration && pointer != null && pointer.press.isPressed)
        {
            Vector2 pos = pointer.position.ReadValue();
            if (PointerOverUI == null || !PointerOverUI(pos))
            {
                float across = Mathf.Clamp01(pos.x / Screen.width) - 0.5f;
                aimX = across * (BowlingLane.HalfWidth - BowlingBall.Radius) * 2f;
                bowler.SetAim(aimX);
            }
        }
        if (!MotionArmed || !motion.Recording) return;
        if (motion.AutoReleaseReady) { ReleaseSwing(); return; }

        // Stuck waiting: the swing was missed (or never came). Stop capturing and ask for OK again,
        // rather than letting some later fidget fling the ball.
        var stage = motion.Stage;
        if (stage != lastStage) { lastStage = stage; stageSince = Time.unscaledTime; }
        bool forwardMissed = stage == MotionInput.SwingStage.SwingForward && Time.unscaledTime - stageSince > ForwardTimeout;
        if (forwardMissed || Time.unscaledTime - armedAt > ArmTimeout)
        {
            DisarmMotion();
            SwingTimedOut = true;
            Haptics.Buzz();
        }
    }

    void UpdateAimingSwipe()
    {
        var pointer = Pointer.current;
        if (pointer == null) return;
        Vector2 pos = pointer.position.ReadValue();

        if (pointer.press.wasPressedThisFrame && (PointerOverUI == null || !PointerOverUI(pos)))
        {
            // A tap while looking through the magnifier just puts it away.
            if (ZoomPins) { ZoomPins = false; return; }
            swiping = true;
            positionLocked = false;
            lowestY = pos.y;
            swipe.Clear();
            SetMessage("");
        }
        if (!swiping) return;

        // Drag sideways to pick your spot; it locks once the finger starts moving up (the throw).
        lowestY = Mathf.Min(lowestY, pos.y);
        bool throwStarted = (pos.y - lowestY) / Screen.height > LockUpward;
        if (!positionLocked && throwStarted) positionLocked = true;
        if (!positionLocked)
        {
            float across = Mathf.Clamp01(pos.x / Screen.width) - 0.5f;
            aimX = across * (BowlingLane.HalfWidth - BowlingBall.Radius) * 2f;
            bowler.SetAim(aimX);
            swipe.Clear(); // only the upward part counts as the throw
        }
        swipe.Add((Time.unscaledTime, pos));

        if (pointer.press.wasReleasedThisFrame)
        {
            swiping = false;
            ReleaseSwipe();
        }
    }

    void ReleaseSwipe()
    {
        float end = swipe[^1].t;
        var recent = swipe.Where(s => s.t >= end - SwipeWindow).ToList();
        if (recent.Count < 3 || recent[^1].t - recent[0].t <= 0f)
        {
            SetMessage(Loc.T("hint_swipe_short"));
            return;
        }

        // Swipe velocity in screen heights per second, so it feels the same on any screen.
        float dt = recent[^1].t - recent[0].t;
        Vector2 v = (recent[^1].pos - recent[0].pos) / dt / Screen.height;
        if (v.y < slowSwipe * 0.5f) { SetMessage(Loc.T("hint_swipe_faster")); return; }

        float speed = Mathf.Lerp(minSpeed, maxSpeed, Mathf.InverseLerp(slowSwipe, fastSwipe, v.y));
        float swipeAngle = Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg;
        float angle = Mathf.Clamp(swipeAngle * anglePerSwipeAngle, -maxAngle, maxAngle);

        // Curve: how much the sideways speed changed between the first and second half of the swipe.
        int mid = recent.Count / 2;
        float LateralSpeed(int a, int b) =>
            (recent[b].pos.x - recent[a].pos.x) / Mathf.Max(recent[b].t - recent[a].t, 0.001f) / Screen.height;
        float curve = LateralSpeed(mid, recent.Count - 1) - LateralSpeed(0, mid);
        if (Mathf.Abs(curve) < curveDeadZone) curve = 0f;
        float hook = Mathf.Clamp(curve * hookPerCurve, -maxHook, maxHook);
        StartThrow(speed, angle, hook);
    }

    void ReleaseSwing()
    {
        MotionArmed = false;
        var result = motion.Release();
        if (!result.valid) { SetMessage(Loc.T(result.failKey)); return; }
        Haptics.Buzz();
        if (CurrentMode == Mode.Calibration)
        {
            var kind = Calibration.CurrentKind;
            Calibration.Add(result);
            var (cs, ca, ch) = Calibration.DemoThrow(result, kind);
            StartThrow(cs, ca, ch, raw: true);
            return;
        }
        var (speed, angle, hook) = MotionInput.ToThrow(result);
        StartThrow(speed, angle, hook, foul: result.late);
    }

    // For BowlingAutoplay and tests: throw from a spot with given numbers.
    public void Throw(float x, float speed, float angle, float hook)
    {
        if (!CanThrow) return;
        aimX = Mathf.Clamp(x, -(BowlingLane.HalfWidth - BowlingBall.Radius), BowlingLane.HalfWidth - BowlingBall.Radius);
        bowler.SetAim(aimX);
        StartThrow(speed, angle, hook);
    }

    // raw: calibration demo throws skip the ball's coverstock and oil, so every player sees the same shape.
    void StartThrow(float speed, float angle, float hook, bool foul = false, bool raw = false)
    {
        speed *= GameSettings.BallSpeedFactor;
        var style = BallTechniques.Classify(angle, hook, GameSettings.LeftHanded);
        if (!raw)
        {
            var design = CurrentBall;
            var shot = BallTechniques.Apply(Technique, speed, angle, hook, GameSettings.LeftHanded);
            speed = shot.speed;
            angle = shot.angle;
            hook = shot.hook * design.HookFactor * (1f - OilHookLoss * design.OilSensitivity * BallOil * shot.oilFactor);
            style = shot.style;
            ball.SetReaction(design.SkidZ < 0f ? design.SkidZ : design.SkidZ * shot.skidScale, design.Backend + shot.backendAdd);
            ball.SetTechnique(shot.topSpin, shot.pinScatter, shot.massScale);
        }
        else ball.SetReaction(-1f, 0f); // an even, plain hook
        ZoomPins = false;
        CurrentThrow = new ThrowRecord
        {
            player = CurrentPlayer,
            frame = CurrentMode == Mode.Standard ? Scores[CurrentPlayer].Frame : ThrowLog.Count,
            speedKmh = speed * 3.6f,
            angleDeg = angle,
            hook = hook,
            wiped = wipedThisBall,
            oil = BallOil,
            leftHanded = GameSettings.LeftHanded,
            style = style,
        };
        if (!raw) BallOil = Mathf.Min(1f, BallOil + OilPerThrow);
        standingBeforeMask = pins.Select(p => p.IsStanding).ToArray();
        hitThisThrow = false;
        foulThisThrow = foul;
        previousSpeed = lastSpeed;
        lastSpeed = speed;
        lastAngle = angle;
        lastHook = hook;
        replayRecorder.BeginRecording();
        SetPhase(Phase.Throwing);
        bowler.Throw(() =>
        {
            foreach (var pin in pins) pin.StopSway(); // a moving rack stops where it is
            ball.Throw(speed, angle, hook);
            GameTopSpeedKmh = Mathf.Max(GameTopSpeedKmh, speed * 3.6f);
            LastThrowInfo = Loc.F("throw_info", speed * 3.6f, angle,
                hook > 0.01f ? Loc.T("spin_right") : hook < -0.01f ? Loc.T("spin_left") : Loc.T("spin_none"));
            if (previousSpeed > 0f) LastThrowInfo += Loc.F("vs_last", (speed - previousSpeed) * 3.6f);
            SetMessage($"{speed * 3.6f:0} km/h");
            SetPhase(Phase.Rolling);
        });
    }

    void OnBallHitPin(Vector3 point, float impact)
    {
        fx.Sparks(point, hitThisThrow ? BowlingFX.Cyan : BowlingFX.Magenta, hitThisThrow ? 6 : 24, 2f + impact * 0.4f);
        if (hitThisThrow) return;
        hitThisThrow = true;
        fx.AddShake(Mathf.Clamp(impact * 0.008f, 0.01f, 0.06f));
        // A full rack hit near the pocket might be a strike: slow down to watch it.
        if (standingBeforeMask.Count(b => b) == 10 && Mathf.Abs(ball.transform.position.x) < 0.13f)
            fx.SlowMo(0.9f);
    }

    void OnBombExploded(Vector3 at)
    {
        fx.Sparks(at + Vector3.up * 0.2f, BowlingFX.Gold, 70, 6f);
        fx.Sparks(at + Vector3.up * 0.2f, BowlingFX.Magenta, 40, 4f);
        fx.AddShake(0.1f);
        NeonGlow.Flash(new Color(1f, 0.5f, 0.1f));
        sound.Boom();
        Haptics.Buzz();
    }

    // ---- Instant replay ----

    void FixedUpdate()
    {
        if (CurrentPhase is Phase.Rolling or Phase.Settling) replayRecorder.Sample();
        if (CurrentPhase == Phase.Rolling && CurrentThrow != null) TrackBall(CurrentThrow);
    }

    // Record the ball's path for the analysis panel, and where it met the pins.
    void TrackBall(ThrowRecord t)
    {
        Vector3 p = ball.transform.position;
        if (p.z > BowlingLane.LaneEndZ + 0.3f || p.y < -0.3f) return;
        if (t.path.Count == 0 || p.z - t.path[^1].y > 0.25f || Mathf.Abs(p.x - t.path[^1].x) > 0.05f)
            t.path.Add(new Vector2(p.x, p.z));
        if (!t.reachedPins && !t.gutter && Mathf.Abs(p.x) > BowlingLane.HalfWidth && p.z < BowlingLane.HeadPinZ) t.gutter = true;
        // Entry: where the ball crosses the line it would touch the head pin from.
        if (!t.reachedPins && !t.gutter && p.z >= BowlingLane.HeadPinZ - 0.2f)
        {
            t.reachedPins = true;
            t.entryX = p.x;
            Vector3 v = ball.Velocity;
            t.entryAngleDeg = Mathf.Atan2(v.x, Mathf.Max(v.z, 0.01f)) * Mathf.Rad2Deg;
        }
    }

    public void StartReplay()
    {
        if (!CanReplay) return;
        replayRecorder.PlayReplay(lastReport.replay, lastReport.standingBefore);
        foreach (var o in obstacles) o.enabled = false; // obstacles just freeze during replay
        SetMessage("");
        SetPhase(Phase.Replay);
    }

    void UpdateReplay()
    {
        if (!replayRecorder.TickPlayback(Time.unscaledDeltaTime)) return;
        foreach (var o in obstacles) o.enabled = true;
        SetPhase(Phase.Result); // back to the result; the player taps to carry on
    }

    // ---- Result: wait for a tap (anywhere but a button) or Space/Enter, then the next ball ----

    void UpdateResult()
    {
        if (!WaitingForTap) return;
        var pointer = Pointer.current;
        bool tapped = pointer != null && pointer.press.wasPressedThisFrame &&
            (PointerOverUI == null || !PointerOverUI(pointer.position.ReadValue()));
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) tapped = true;
        if (tapped) Continue();
    }

    public void Continue()
    {
        if (!WaitingForTap) return;
        ZoomPins = false;
        AfterResult();
    }

    // ---- Rolling and counting ----

    void UpdateRolling()
    {
        Vector3 p = ball.transform.position;
        bool pastPins = p.z > BowlingLane.LaneEndZ + 0.3f;
        bool fellOff = p.y < -0.5f;
        bool stopped = stateTime > 1f && ball.Velocity.sqrMagnitude < 0.01f;
        if (pastPins || fellOff || stopped || stateTime > MaxRollTime)
            SetPhase(Phase.Settling);
    }

    bool AllStandingSettled()
    {
        for (int i = 0; i < pins.Length; i++) if (standingBeforeMask[i] && !pins[i].IsSettled) return false;
        return true;
    }

    void UpdateSettling()
    {
        bool settled = stateTime > MinSettleTime && AllStandingSettled();
        if (!settled && stateTime < MaxSettleTime) return;

        bool[] isDownNow = rack.ComputeIsDown();
        if (foulThisThrow) rack.ResetFouled(standingBeforeMask); // over the foul line: those pins don't count, go back up
        bool[] knockedMask = RollReport.BuildKnockedMask(standingBeforeMask, isDownNow, foulThisThrow);
        int knocked = knockedMask.Count(b => b);
        int standingCount = standingBeforeMask.Count(b => b);
        bool cleared = knocked == standingCount && knocked > 0;
        string mark;
        switch (CurrentMode)
        {
            case Mode.Standard: mark = ScoreStandard(knocked, standingCount); break;
            case Mode.Practice: mark = ScorePractice(knocked, cleared, standingCount); break;
            case Mode.Calibration: mark = ScoreCalibration(knocked); break;
            default: mark = ScoreChallenge(knocked); break;
        }
        if (foulThisThrow) { SetMessage(Loc.T("foul")); sound.Miss(); }
        if (CurrentThrow != null)
        {
            CurrentThrow.knocked = knocked;
            CurrentThrow.standingBefore = standingCount;
            CurrentThrow.mark = mark;
            ThrowLog.Add(CurrentThrow);
        }
        if (CurrentMode != Mode.Calibration)
        {
            PlayerStats.RecordThrow(mark, lastSpeed * 3.6f);
            if (CurrentThrow != null && CurrentThrow.reachedPins && standingCount == 10) PlayerStats.RecordEntry(CurrentThrow.InPocket);
            // Coins: a pin each, plus a bonus for strikes and spares.
            Award(knocked + (mark == "X" ? 10 : mark == "/" ? 5 : 0));
        }
        RollScored?.Invoke(mark, knocked);
        SetPhase(Phase.Result);

        lastReport = new RollReport
        {
            playerIndex = CurrentPlayer,
            standingBefore = standingBeforeMask,
            knockedMask = knockedMask,
            mark = mark,
            foul = foulThisThrow,
            speedMps = lastSpeed,
            angleDeg = lastAngle,
            hookMag = lastHook,
            replay = replayRecorder.Finish(),
        };
        RollFinished?.Invoke(lastReport);
        driver.OnRollFinished(lastReport);
        if (CurrentMode == Mode.Standard) SaveMatch(afterRoll: true);
    }

    string ScoreStandard(int knocked, int standingCount)
    {
        var score = Scores[CurrentPlayer];
        bool frameOver = score.AddRoll(knocked, standingCount, out bool freshRack);
        string mark = score.LastMark;
        Celebrate(mark, knocked);
        rackNext = frameOver || freshRack;
        return mark;
    }

    string ScorePractice(int knocked, bool cleared, int standingCount)
    {
        PracticeThrows++;
        bool fullRack = standingCount == 10;
        if (cleared) PracticeCleared++;
        string mark = cleared ? (fullRack ? "X" : "/") : knocked == 0 ? "-" : knocked.ToString();
        Celebrate(mark, knocked);
        // A full rack gets a second ball at whatever is left; a leave is re-racked after one ball.
        rackNext = !(PracticeLeave == 0 && fullRack && !cleared);
        return mark;
    }

    string ScoreCalibration(int knocked)
    {
        rackNext = true;
        string mark = knocked == 0 ? "-" : knocked.ToString();
        SetMessage(Calibration.Done ? Loc.T("calib_done_short") : Loc.F("calib_step_ok", Calibration.Step, CalibrationSession.Steps.Length));
        return mark;
    }

    void Award(int coins)
    {
        CoinsThisGame += coins;
        ProShop.Earn(coins);
    }

    string ScoreChallenge(int knocked)
    {
        BallsUsed++;
        var level = Challenge;
        bool success = level.goal switch
        {
            ChallengeGoal.ClearAll => pins.All(p => !p.IsStanding),
            ChallengeGoal.KnockTargets => pins.Where(p => p.IsTarget).All(p => !p.IsStanding),
            _ => knocked >= level.minPins,
        };
        string mark = knocked == 0 ? "-" : knocked.ToString();
        if (success)
        {
            ChallengeStars = level.StarsFor(BallsUsed);
            PlayerStats.RecordStars(ChallengeIndex, ChallengeStars);
            Award(ChallengeStars * 15);
            SetMessage(Loc.T("challenge_clear"), rainbow: true);
            sound.Strike();
            NeonGlow.Flash(BowlingFX.Gold);
            fx.Celebrate(strike: true);
            Haptics.Buzz();
        }
        else if (BallsUsed >= level.balls)
        {
            ChallengeStars = 0;
            SetMessage(Loc.T("challenge_fail"));
            sound.Miss();
        }
        else SetMessage(Loc.F("pins_down", knocked));
        rackNext = level.goal == ChallengeGoal.MinPins;
        return mark;
    }

    void Celebrate(string mark, int knocked)
    {
        switch (mark)
        {
            case "X":
                SetMessage(Loc.T("strike"), rainbow: true);
                sound.Strike();
                NeonGlow.Flash(BowlingFX.Magenta);
                fx.Celebrate(strike: true);
                Haptics.Buzz();
                break;
            case "/":
                SetMessage(Loc.T("spare"), rainbow: true);
                sound.Spare();
                NeonGlow.Flash(BowlingFX.Cyan);
                fx.Celebrate(strike: false);
                break;
            case "-": SetMessage(Loc.T("gutter")); sound.Miss(); break;
            default: SetMessage(Loc.F("pins_down", knocked)); break;
        }
    }

    void AfterResult()
    {
        switch (CurrentMode)
        {
            case Mode.Standard:
                if (Scores[CurrentPlayer].RollInFrame == 0) // frame over: next player's turn
                {
                    if (Scores.All(s => s.IsGameOver)) { FinishStandard(); return; }
                    do CurrentPlayer = (CurrentPlayer + 1) % Scores.Count; while (Scores[CurrentPlayer].IsGameOver);
                    rack.RackAll();
                }
                else if (rackNext) rack.RackAll();
                else rack.SweepFallen();
                break;

            case Mode.Practice:
                if (rackNext) rack.RackPractice(PracticeLeave); else rack.SweepFallen();
                break;

            case Mode.Challenge:
                if (ChallengeStars > 0 || BallsUsed >= Challenge.balls) { SetPhase(Phase.Finished); return; }
                if (rackNext) rack.RackChallenge(Challenge); else rack.SweepFallen();
                break;

            case Mode.Calibration:
                if (Calibration.Done) { Calibration.Apply(); SetMessage(""); SetPhase(Phase.Finished); return; }
                rack.RackAll();
                break;
        }
        NextBall();
    }

    void FinishStandard()
    {
        DiscardSavedMatch();
        NewBest = false;
        foreach (var s in Scores) NewBest |= PlayerStats.RecordGame(s.RunningTotals()[^1] ?? 0);
        Award(20 + (Scores[0].RunningTotals()[^1] ?? 0) / 10);
        if (NewBest) NeonGlow.Flash(BowlingFX.Gold);
        sound.GameOver(NewBest);
        SetMessage("");
        SetPhase(Phase.Finished);
    }

    // ---- Camera ----

    void LateUpdate()
    {
        if (ZoomPins && !CanZoom) ZoomPins = false;
        cameraRig.Tick(CurrentPhase, aimX, ball.transform.position, fx.ShakeOffset, Time.unscaledDeltaTime, ZoomPins);
    }
}
