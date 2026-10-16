using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// The game itself: modes (standard 1–4 players, practice, challenges), turns, racks, the throw
// (swipe or phone swing), waiting for pins to settle, scoring, camera and effects.
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
    Camera cam;
    BowlingAudio sound;
    BowlingFX fx;
    ChallengeKit kit;
    MotionInput motion;
    LineRenderer aimLine;

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
    public bool CanThrow => CurrentPhase == Phase.Aiming && !Paused;
    public event System.Action<string, int> RollScored; // mark, pins knocked
    public System.Func<Vector2, bool> PointerOverUI;     // set by BowlingUI

    float stateTime;
    readonly List<(float t, Vector2 pos)> swipe = new();
    bool swiping, positionLocked;
    float lowestY, aimX;
    List<Pin> standingAtThrow = new();
    bool rackNext, hitThisThrow;
    float lastSpeed, previousSpeed;
    bool foulThisThrow;
    float levelStartTime;
    List<Obstacle> obstacles = new();
    Transform obstacleRoot;

    void Awake()
    {
        Time.fixedDeltaTime = 0.01f; // smaller physics steps so the fast ball doesn't skip through pins
        ball = FindAnyObjectByType<BowlingBall>();
        bowler = FindAnyObjectByType<Bowler>();
        kit = FindAnyObjectByType<ChallengeKit>();
        cam = Camera.main;
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
        aimLine = MakeAimLine();
        Application.targetFrameRate = 60;
    }

    void Start() => BackToMenu();

    // ---- Starting and leaving modes (called by the UI) ----

    public void StartStandard(int players)
    {
        GameSettings.PlayerCount = players;
        CurrentMode = Mode.Standard;
        Scores.Clear();
        for (int i = 0; i < players; i++) Scores.Add(new BowlingScore());
        CurrentPlayer = 0;
        BeginMode();
        RackAll();
        NextBall();
    }

    public void StartPractice(int leave)
    {
        CurrentMode = Mode.Practice;
        PracticeLeave = Mathf.Clamp(leave, 0, PracticeLeaves.Length - 1);
        PracticeThrows = PracticeCleared = 0;
        BeginMode();
        RackPractice();
        NextBall();
    }

    public void StartChallenge(int level)
    {
        CurrentMode = Mode.Challenge;
        ChallengeIndex = Mathf.Clamp(level, 0, ChallengeLevels.All.Count - 1);
        BallsUsed = 0;
        ChallengeStars = 0;
        BeginMode();
        levelStartTime = Time.time;
        obstacleRoot = new GameObject("Challenge Obstacles").transform;
        obstacles = Obstacle.Build(Challenge.obstacles, kit, obstacleRoot);
        RackChallenge();
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
        RackAll();
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
        if (CurrentPhase == Phase.Aiming) RackPractice();
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

    // ---- Racks ----

    void RackAll() => Rack(ChallengeLevel.Rack(1, 2, 3, 4, 5, 6, 7, 8, 9, 10), new int[0]);

    void RackPractice()
    {
        int[] leave = PracticeLeaves[PracticeLeave];
        if (leave.Length == 0) leave = RandomLeave();
        Rack(ChallengeLevel.Rack(leave), new int[0]);
    }

    void RackChallenge() => Rack(Challenge.pins, Challenge.targets);

    void Rack(bool[] present, int[] targets)
    {
        for (int i = 0; i < pins.Length; i++)
        {
            pins[i].ResetToRack();
            pins[i].SetTarget(targets.Contains(i + 1));
            pins[i].SetHeavy(CurrentMode == Mode.Challenge && Challenge.heavyPins);
            if (!present[i]) pins[i].Remove();
        }
    }

    // A plausible leave: 1–4 pins from the back two rows, plus sometimes a middle pin.
    static int[] RandomLeave()
    {
        var pool = new List<int> { 4, 5, 6, 7, 8, 9, 10, 2, 3 };
        int count = Random.Range(1, 5);
        return pool.OrderBy(_ => Random.value).Take(count).ToArray();
    }

    void SweepFallen()
    {
        foreach (var pin in pins) if (pin.gameObject.activeSelf && pin.IsDown) pin.Remove();
    }

    // ---- Turn flow ----

    void NextBall()
    {
        fx.CancelSlowMo();
        ball.PickUp();
        ball.Configure(GameSettings.BallMassKg, CurrentMode == Mode.Challenge ? Challenge.ballType : BallType.Normal, pins);
        bowler.LeftHanded = GameSettings.LeftHanded;
        bowler.Ready(ball, aimX);
        cam.transform.SetPositionAndRotation(AimCameraPosition(), Quaternion.Euler(aimCameraPitch, 0f, 0f));
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
        UpdateSway();
        UpdateAimLine();
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
        standingAtThrow = pins.Where(p => p.IsStanding).ToList();
        hitThisThrow = false;
        foulThisThrow = foul;
        previousSpeed = lastSpeed;
        lastSpeed = speed;
        replay.Clear();
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
        if (standingAtThrow.Count == 10 && Mathf.Abs(ball.transform.position.x) < 0.13f)
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

    // Moving-rack challenge: the standing pins slide side to side until the ball is released.
    void UpdateSway()
    {
        if (CurrentMode != Mode.Challenge || Challenge.sway <= 0f) return;
        if (CurrentPhase is not (Phase.Aiming or Phase.Throwing) || Paused) return;
        float x = Challenge.sway * Mathf.Sin(2f * Mathf.PI * Challenge.swaySpeed * (Time.time - levelStartTime));
        foreach (var pin in pins) if (pin.IsStanding) pin.Sway(new Vector3(x, 0f, 0f));
    }

    // ---- Instant replay: every physics step of the roll is recorded, then played back in slow motion ----

    const int MaxReplayFrames = 1200;   // 12 s at 100 steps/s
    const float ReplaySpeed = 0.5f;
    readonly List<(Vector3 pos, Quaternion rot)[]> replay = new();
    Transform[] replayTargets;
    float replayTime;

    public bool CanReplay => CurrentPhase == Phase.Result && replay.Count > 20 && !Paused;

    void FixedUpdate()
    {
        if (CurrentPhase is not (Phase.Rolling or Phase.Settling) || replay.Count >= MaxReplayFrames) return;
        if (replay.Count == 0)
            replayTargets = new[] { ball.transform }.Concat(pins.Select(p => p.transform)).Concat(obstacles.Select(o => o.transform)).ToArray();
        replay.Add(replayTargets.Select(t => (t.position, t.rotation)).ToArray());
    }

    public void StartReplay()
    {
        if (!CanReplay) return;
        foreach (var t in replayTargets) if (t.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
        foreach (var o in obstacles) o.enabled = false;
        replayTime = 0f;
        SetMessage("");
        SetPhase(Phase.Replay);
    }

    void UpdateReplay()
    {
        replayTime += Time.unscaledDeltaTime * ReplaySpeed;
        int frame = Mathf.FloorToInt(replayTime / Time.fixedDeltaTime);
        if (frame >= replay.Count) { EndReplay(); return; }
        ApplyReplayFrame(frame);
    }

    void ApplyReplayFrame(int frame)
    {
        var poses = replay[frame];
        for (int i = 0; i < replayTargets.Length; i++)
        {
            replayTargets[i].SetPositionAndRotation(poses[i].pos, poses[i].rot);
            if (replayTargets[i].TryGetComponent(out Rigidbody rb)) { rb.position = poses[i].pos; rb.rotation = poses[i].rot; }
        }
    }

    void EndReplay()
    {
        ApplyReplayFrame(replay.Count - 1);
        foreach (var pin in pins)
        {
            if (!pin.gameObject.activeSelf) continue;
            var rb = pin.GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }
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

    void UpdateSettling()
    {
        bool settled = stateTime > MinSettleTime && standingAtThrow.All(pin => pin.IsSettled);
        if (!settled && stateTime < MaxSettleTime) return;

        int knocked = standingAtThrow.Count(pin => pin.IsDown);
        if (foulThisThrow)
        {
            // Over the foul line: the pins knocked down don't count and go back up.
            foreach (var pin in standingAtThrow) pin.ResetToRack();
            knocked = 0;
        }
        bool cleared = knocked == standingAtThrow.Count && knocked > 0;
        string mark;
        switch (CurrentMode)
        {
            case Mode.Standard: mark = ScoreStandard(knocked); break;
            case Mode.Practice: mark = ScorePractice(knocked, cleared); break;
            default: mark = ScoreChallenge(knocked); break;
        }
        if (foulThisThrow) { SetMessage(Loc.T("foul")); sound.Miss(); }
        PlayerStats.RecordThrow(mark, lastSpeed * 3.6f);
        RollScored?.Invoke(mark, knocked);
        SetPhase(Phase.Result);
    }

    string ScoreStandard(int knocked)
    {
        var score = Scores[CurrentPlayer];
        bool frameOver = score.AddRoll(knocked, standingAtThrow.Count, out bool freshRack);
        string mark = score.LastMark;
        Celebrate(mark, knocked);
        rackNext = frameOver || freshRack;
        return mark;
    }

    string ScorePractice(int knocked, bool cleared)
    {
        PracticeThrows++;
        bool fullRack = standingAtThrow.Count == 10;
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
                    RackAll();
                }
                else if (rackNext) RackAll();
                else SweepFallen();
                break;

            case Mode.Practice:
                if (rackNext) RackPractice(); else SweepFallen();
                break;

            case Mode.Challenge:
                if (ChallengeStars > 0 || BallsUsed >= Challenge.balls) { SetPhase(Phase.Finished); return; }
                if (rackNext) RackChallenge(); else SweepFallen();
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

    // ---- Camera and aim guide ----

    Vector3 AimCameraPosition() => new Vector3(aimX, 0f, 0f) + aimCameraOffset;
    Vector3 lastShake;

    void LateUpdate()
    {
        Vector3 targetPos;
        Quaternion targetRot;
        if (CurrentPhase == Phase.Menu)
        {
            // Slow cinematic drift behind the bowler while the menu is up.
            float t = Time.unscaledTime * 0.15f;
            targetPos = new Vector3(Mathf.Sin(t) * 0.9f, 1.5f + Mathf.Sin(t * 0.7f) * 0.2f, -3.6f);
            targetRot = Quaternion.LookRotation(new Vector3(0f, 0.3f, 18f) - targetPos);
        }
        else if (CurrentPhase is Phase.Aiming or Phase.Throwing or Phase.Finished)
        {
            targetPos = AimCameraPosition();
            targetRot = Quaternion.Euler(aimCameraPitch, 0f, 0f);
        }
        else
        {
            Vector3 b = ball.transform.position;
            float z = Mathf.Min(b.z - followDistance, followMaxZ);
            targetPos = new Vector3(b.x * 0.5f, followHeight, z);
            targetRot = Quaternion.Euler(followPitch, 0f, 0f);
        }

        // Unscaled time, so the camera stays smooth during slow motion.
        float k = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
        Vector3 pos = Vector3.Lerp(cam.transform.position - lastShake, targetPos, k);
        lastShake = fx.ShakeOffset;
        cam.transform.SetPositionAndRotation(pos + lastShake, Quaternion.Slerp(cam.transform.rotation, targetRot, k));
    }

    LineRenderer MakeAimLine()
    {
        var line = new GameObject("Aim Guide").AddComponent<LineRenderer>();
        line.sharedMaterial = kit.additiveMat;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = 0.025f;
        line.colorGradient = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(new Color(0.3f, 1f, 1f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.9f), 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) },
        };
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.enabled = false;
        return line;
    }

    void UpdateAimLine()
    {
        bool show = GameSettings.AimGuide && CurrentPhase == Phase.Aiming && !Paused;
        aimLine.enabled = show;
        if (!show) return;
        aimLine.SetPosition(0, new Vector3(aimX, 0.004f, 0.3f));
        aimLine.SetPosition(1, new Vector3(aimX, 0.004f, 14f));
    }
}
