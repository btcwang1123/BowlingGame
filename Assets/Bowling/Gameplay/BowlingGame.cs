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
    public enum Mode { Standard, Practice, Challenge }
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
    const float ResultTime = 2f;

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

    public int PracticeLeave { get; private set; }
    public int PracticeThrows { get; private set; }
    public int PracticeCleared { get; private set; }

    public int ChallengeIndex { get; private set; }
    public ChallengeLevel Challenge => ChallengeLevels.All[ChallengeIndex];
    public int BallsUsed { get; private set; }
    public int ChallengeStars { get; private set; }   // 0 = failed

    public BowlingScore Score => Scores.Count > 0 ? Scores[Mathf.Min(CurrentPlayer, Scores.Count - 1)] : null;
    public bool IsGameOver => CurrentPhase == Phase.Finished;
    public bool CanThrow => CurrentPhase == Phase.Aiming && !Paused && driver.IsLocalTurn;
    public bool CanReplay => CurrentPhase == Phase.Result && ReplayRecorder.HasEnoughFrames(lastReport.replay) && !Paused;
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
        Application.targetFrameRate = 60;
    }

    void Start() => BackToMenu();

    // ---- Starting and leaving modes (called by the UI) ----

    public void StartStandard(int players)
    {
        GameSettings.PlayerCount = players;
        CurrentMode = Mode.Standard;
        driver = players > 1 ? new LocalMultiplayerMatchDriver() : new LocalMatchDriver();
        Scores.Clear();
        for (int i = 0; i < players; i++) Scores.Add(new BowlingScore());
        CurrentPlayer = 0;
        BeginMode();
        rack.RackAll();
        NextBall();
    }

    public void StartPractice(int leave)
    {
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

    public void Restart()
    {
        switch (CurrentMode)
        {
            case Mode.Standard: StartStandard(Mathf.Max(Scores.Count, 1)); break;
            case Mode.Practice: StartPractice(PracticeLeave); break;
            case Mode.Challenge: StartChallenge(ChallengeIndex); break;
        }
    }

    public void BackToMenu()
    {
        ClearObstacles();
        SetPaused(false);
        fx.CancelSlowMo();
        motion.Cancel();
        Scores.Clear();
        rack.RackAll();
        ball.PickUp();
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
        if (paused) { fx.CancelSlowMo(); motion.Cancel(); swiping = false; }
        Time.timeScale = paused ? 0f : BowlingFX.NormalTimeScale;
    }

    public void SetPracticeLeave(int leave)
    {
        PracticeLeave = Mathf.Clamp(leave, 0, PracticeLeaves.Length - 1);
        if (CurrentPhase == Phase.Aiming) rack.RackPractice(PracticeLeave);
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
        ball.PickUp();
        ball.Configure(GameSettings.BallMassKg, CurrentMode == Mode.Challenge ? Challenge.ballType : BallType.Normal, pins);
        bowler.LeftHanded = GameSettings.LeftHanded;
        bowler.Ready(ball, aimX);
        cameraRig.SnapToAim(aimX);
        SetMessage("");
        SetPhase(Phase.Aiming);
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
            case Phase.Aiming: UpdateAiming(); break;
            case Phase.Rolling: UpdateRolling(); break;
            case Phase.Settling: UpdateSettling(); break;
            case Phase.Result: if (stateTime > ResultTime) AfterResult(); break;
            case Phase.Replay: UpdateReplay(); break;
        }
        if (CurrentMode == Mode.Challenge && CurrentPhase is Phase.Aiming or Phase.Throwing)
            rack.UpdateSway(Challenge, Time.time - levelStartTime);
        cameraRig.UpdateAimLine(aimX, GameSettings.AimGuide && CurrentPhase == Phase.Aiming && !Paused);
    }

    // ---- Aiming: drag to pick the spot, then flick (swipe) or swing the phone (motion) ----

    void UpdateAiming()
    {
        var pointer = Pointer.current;
        if (pointer == null) return;
        Vector2 pos = pointer.position.ReadValue();

        if (pointer.press.wasPressedThisFrame && (PointerOverUI == null || !PointerOverUI(pos)))
        {
            swiping = true;
            positionLocked = false;
            lowestY = pos.y;
            swipe.Clear();
            SetMessage("");
            if (UsingMotion) motion.Grip();
        }
        if (!swiping) return;

        // Drag sideways to pick your spot; it locks once the throw starts
        // (finger moving up for a swipe, or the phone starting to swing).
        lowestY = Mathf.Min(lowestY, pos.y);
        bool throwStarted = UsingMotion ? motion.Swinging : (pos.y - lowestY) / Screen.height > LockUpward;
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
            if (UsingMotion) ReleaseSwing(); else ReleaseSwipe();
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
        var result = motion.Release();
        if (!result.valid) { SetMessage(Loc.T(result.failKey)); return; }
        var (speed, angle, hook) = MotionInput.ToThrow(result);
        Haptics.Buzz();
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

    void StartThrow(float speed, float angle, float hook, bool foul = false)
    {
        speed *= GameSettings.BallSpeedFactor;
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
        SetPhase(Phase.Result);
        stateTime = ResultTime - 1.2f; // a moment to read the result again, then carry on
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
            default: mark = ScoreChallenge(knocked); break;
        }
        if (foulThisThrow) { SetMessage(Loc.T("foul")); sound.Miss(); }
        PlayerStats.RecordThrow(mark, lastSpeed * 3.6f);
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
        }
        NextBall();
    }

    void FinishStandard()
    {
        NewBest = false;
        foreach (var s in Scores) NewBest |= PlayerStats.RecordGame(s.RunningTotals()[^1] ?? 0);
        if (NewBest) NeonGlow.Flash(BowlingFX.Gold);
        sound.GameOver(NewBest);
        SetMessage("");
        SetPhase(Phase.Finished);
    }

    // ---- Camera ----

    void LateUpdate() => cameraRig.Tick(CurrentPhase, aimX, ball.transform.position, fx.ShakeOffset, Time.unscaledDeltaTime);
}
