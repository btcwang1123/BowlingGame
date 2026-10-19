using System.Linq;
using System;
using UnityEngine;

// Plays the game by itself when the player is started with -autoplay, logging every roll, then quits
// (exit code 1 if any error was logged). Covers every mode: a 2-player standard game, every challenge
// level once, and a few practice balls. Used to smoke-test a build without anyone touching it, e.g.
//   Bowling.exe -batchmode -nographics -autoplay -logFile out.log
// Add -autoplay-screens <folder> (with graphics) to also save screenshots of each mode.
public class BowlingAutoplay : MonoBehaviour
{
    enum Stage { Standard, Challenges, Practice, Done }

    BowlingGame game;
    Stage stage;
    int rolls, errors, challenge, practiceBalls;
    float wait = 1f;
    readonly System.Random rng = new(1);
    string screenshotDir;
    int shots, menuShot;
    BowlingUI ui;
    static readonly int[] ShotLevels = { 6, 16, 18 }; // moving wall, bomb ball, final challenge
    static readonly string[] MenuScreens = { "Home", "Players", "Practice", "Challenges", "Settings", "Stats", "Shop", "Venues", "Calibrate" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autoplay") < 0) return;
        var game = FindAnyObjectByType<BowlingGame>();
        if (game != null) game.gameObject.AddComponent<BowlingAutoplay>();
    }

    void Awake()
    {
        game = GetComponent<BowlingGame>();
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-autoplay-screens");
        if (i >= 0 && i + 1 < args.Length) screenshotDir = args[i + 1];
        // Run faster than real time (default 4×; -autoplay-speed 1 for real time).
        i = Array.IndexOf(args, "-autoplay-speed");
        float speed = i >= 0 && i + 1 < args.Length && float.TryParse(args[i + 1], out float s) ? s : 4f;
        BowlingFX.NormalTimeScale = speed;
        Time.timeScale = speed;
        ui = GetComponent<BowlingUI>();

        game.RollScored += (mark, knocked) => Debug.Log($"[Autoplay] {game.CurrentMode} roll {++rolls}: {knocked} pins, mark {mark}");
        Application.logMessageReceived += (msg, stack, type) =>
        {
            if (type is LogType.Exception or LogType.Error) errors++;
        };
        Debug.Log("[Autoplay] started");
    }

    void Update()
    {
        if ((wait -= Time.unscaledDeltaTime) > 0f) return;
        wait = 0.4f;

        switch (stage)
        {
            case Stage.Standard:
                if (game.CurrentPhase == BowlingGame.Phase.Menu)
                {
                    // Tour the menus first (for screenshots), then start a 2-player game.
                    if (menuShot < MenuScreens.Length)
                    {
                        // A screenshot is taken at the end of the frame, after this frame's GUI: it shows the new screen.
                        if (ui != null) ui.ShowScreen(MenuScreens[menuShot]);
                        Shot($"menu-{MenuScreens[menuShot]}");
                        menuShot++;
                        wait = 0.8f;
                        return;
                    }
                    if (ui != null) ui.ShowScreen("Home");
                    game.StartStandard(2);
                    return;
                }
                if (game.CanThrow && rolls == 3) Shot("standard-aiming");
                if (game.IsGameOver)
                {
                    if (ShotFirst("standard-result")) return;
                    Debug.Log($"[Autoplay] standard over: P1 {game.Scores[0].RunningTotals()[^1]}, P2 {game.Scores[1].RunningTotals()[^1]}");
                    stage = Stage.Challenges;
                    game.StartChallenge(challenge);
                    return;
                }
                break;

            case Stage.Challenges:
                if (game.IsGameOver)
                {
                    Debug.Log($"[Autoplay] challenge {challenge + 1} {game.Challenge.nameEn}: {game.ChallengeStars} stars in {game.BallsUsed} balls");
                    if (ShotLevels.Contains(challenge) && ShotFirst($"challenge-{challenge + 1}-result")) return;
                    if (++challenge < ChallengeLevels.All.Count) { game.StartChallenge(challenge); return; }
                    stage = Stage.Practice;
                    game.StartPractice(3);
                    return;
                }
                if (game.CanThrow && game.BallsUsed == 0 && ShotLevels.Contains(challenge)) Shot($"challenge-{challenge + 1}");
                break;

            case Stage.Practice:
                if (game.CanThrow && practiceBalls >= 4)
                {
                    if (ShotFirst("practice")) return;
                    stage = Stage.Done;
                    game.BackToMenu();
                    Debug.Log($"[Autoplay] done: rolls {rolls}, errors {errors}");
                    Application.Quit(errors > 0 ? 1 : 0);
                    return;
                }
                break;
            case Stage.Done: return;
        }

        if (game.WaitingForTap) { game.Continue(); return; }
        if (!game.CanThrow) return;
        if (stage == Stage.Practice) practiceBalls++;
        // A mix of good and wild throws, with some hook.
        float x = (float)(rng.NextDouble() * 0.5 - 0.2);
        float speed = 5f + (float)rng.NextDouble() * 5f;
        float angle = (float)(rng.NextDouble() * 2 - 1) * 1.2f;
        float hook = (float)(rng.NextDouble() * 2 - 1) * 0.12f;
        game.Throw(x, speed, angle, hook);
    }

    // Take a screenshot and report true the first time, so the caller waits a beat before moving on
    // (otherwise the screen would already show the next thing). Reports false once it has been taken.
    string pendingShot;
    bool ShotFirst(string name)
    {
        if (screenshotDir == null || pendingShot == name) { pendingShot = null; return false; }
        pendingShot = name;
        Shot(name);
        wait = 1f;
        return true;
    }

    void Shot(string name)
    {
        if (screenshotDir == null) return;
        System.IO.Directory.CreateDirectory(screenshotDir);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(screenshotDir, $"{++shots:00}-{name}.png"));
    }
}
