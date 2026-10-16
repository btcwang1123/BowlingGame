using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// For every challenge level, simulates a grid of first-ball throws (spot × angle × speed × hook,
// and several moments for moving obstacles) and reports how often the goal is met with one ball
// and the most pins knocked. A 0% level with a 1-ball three-star rule needs retuning; a near-100%
// level is too easy.
public static class BowlingChallengeCheck
{
    static readonly float[] Xs = { -0.32f, -0.24f, -0.16f, -0.08f, 0f, 0.08f, 0.16f, 0.24f, 0.32f };
    static readonly float[] Angles = { -1.5f, -0.75f, 0f, 0.75f, 1.5f };
    static readonly float[] Speeds = { 6.5f, 9f };
    static readonly float[] Hooks = { -0.1f, 0f, 0.1f };

    [MenuItem("Bowling/Challenge Check")]
    public static void Run()
    {
        var oldMode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        var report = new StringBuilder();
        try
        {
            for (int i = 0; i < ChallengeLevels.All.Count; i++) report.AppendLine(CheckLevel(i));
        }
        finally { Physics.simulationMode = oldMode; }
        Debug.Log("[BowlingChallengeCheck]\n" + report);
    }

    static string CheckLevel(int index)
    {
        var level = ChallengeLevels.All[index];
        EditorSceneManager.OpenScene(BowlingSceneBuilder.ScenePath);
        var kit = Object.FindAnyObjectByType<ChallengeKit>();
        var ball = Object.FindAnyObjectByType<BowlingBall>().GetComponent<Rigidbody>();
        var pins = Object.FindObjectsByType<Pin>()
            .OrderBy(p => Mathf.Round(p.transform.position.z * 10f)).ThenBy(p => p.transform.position.x).ToArray();
        var home = pins.Select(p => (p.transform.position, p.transform.rotation)).ToArray();
        var obstacles = Obstacle.Build(level.obstacles, kit, new GameObject("Obstacles").transform);
        bool moving = level.obstacles.Any(o => o.kind is ObstacleKind.Mover or ObstacleKind.Spinner);
        float[] starts = moving ? new[] { 0f, 0.7f, 1.4f, 2.1f } : new[] { 0f };
        // A moving rack stops wherever it is at release: try it at several points of its travel.
        float[] rackShifts = level.sway > 0f ? new[] { -level.sway, -level.sway / 2f, 0f, level.sway / 2f, level.sway } : new[] { 0f };
        float ballMass = ball.mass * (level.ballType == BallType.Heavy ? 2.2f : 1f);
        var pinBodies = pins.Select(p => p.GetComponent<Rigidbody>()).ToArray();

        int tries = 0, wins = 0, best = 0;
        foreach (float t0 in starts)
        foreach (float shift in rackShifts)
        foreach (float x in Xs)
        foreach (float angle in Angles)
        foreach (float speed in Speeds)
        foreach (float hook in Hooks)
        {
            // Rack.
            for (int p = 0; p < pins.Length; p++)
            {
                var rb = pins[p].GetComponent<Rigidbody>();
                pins[p].gameObject.SetActive(level.pins[p]);
                pins[p].transform.SetPositionAndRotation(home[p].position + Vector3.right * shift, home[p].rotation);
                rb.mass = level.heavyPins ? Pin.HeavyMass : Pin.NormalMass;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            // Throw.
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            ball.isKinematic = false;
            ball.mass = ballMass;
            bool bombArmed = level.ballType == BallType.Bomb;
            ball.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            ball.transform.SetPositionAndRotation(new Vector3(x, BowlingBall.Radius + 0.002f, 0f), Quaternion.identity);
            ball.linearVelocity = dir * speed;
            ball.angularVelocity = Vector3.Cross(Vector3.up, dir) * (speed / BowlingBall.Radius);
            foreach (var o in obstacles) o.Step(t0);
            Physics.SyncTransforms();

            for (float t = 0f; t < 6f; t += 0.01f)
            {
                foreach (var o in obstacles) o.Step(t0 + t);
                Vector3 bp = ball.position;
                if (bombArmed && BowlingBall.ShouldExplode(bp, pins.Where(p => p.gameObject.activeSelf).Select(p => p.transform.position)))
                {
                    bombArmed = false;
                    BowlingBall.Explode(bp, pinBodies.Where(b => b.gameObject.activeSelf));
                }
                if (hook != 0f && Mathf.Abs(bp.x) < BowlingLane.HalfWidth && bp.z < 17.5f)
                    ball.AddForce(Vector3.right * hook, ForceMode.Acceleration);
                Physics.Simulate(0.01f);
            }

            ball.mass = ballMass / (level.ballType == BallType.Heavy ? 2.2f : 1f);
            bool Down(int p) => !pins[p].gameObject.activeSelf || pins[p].transform.position.y < home[p].position.y - 0.05f ||
                                Vector3.Angle(pins[p].transform.up, Vector3.up) > 30f;
            int knocked = Enumerable.Range(0, 10).Count(p => level.pins[p] && Down(p));
            bool success = level.goal switch
            {
                ChallengeGoal.ClearAll => Enumerable.Range(0, 10).All(p => !level.pins[p] || Down(p)),
                ChallengeGoal.KnockTargets => level.targets.All(n => Down(n - 1)),
                _ => knocked >= level.minPins,
            };
            tries++;
            if (success) wins++;
            best = Mathf.Max(best, knocked);
        }

        int present = level.pins.Count(b => b);
        return $"{index + 1,2}. {level.nameEn,-20} one-ball success {100f * wins / tries,5:0.0}% ({wins}/{tries})  best {best}/{present}  3★ needs {level.threeStarBalls} ball(s)";
    }
}
