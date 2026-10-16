using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Physics tuning without pressing Play: opens the bowling scene, throws the ball along a set of
// lines, steps physics for a few seconds and logs how many pins fell. Changes are never saved.
public static class BowlingPhysicsCheck
{
    const string ScenePath = "Assets/Bowling/Scenes/Bowling.unity";

    // Offsets from the centre line (m, + = right). 0.06 is the right-hand pocket (between pins 1 and 3).
    static readonly float[] Offsets = { 0f, 0.03f, 0.06f, 0.1f, 0.15f, 0.25f };

    [MenuItem("Bowling/Physics Check")]
    public static void Run() => Debug.Log("[BowlingPhysicsCheck]\n" + Table("current scene", null));

    // Try combinations of pin settings and print a table for each, to pick values that feel real.
    [MenuItem("Bowling/Physics Sweep")]
    public static void Sweep()
    {
        BowlingSceneBuilder.Build(); // sweep the scene as the builder makes it now
        var sb = new StringBuilder();
        const float friction = BowlingSceneBuilder.PinFriction;
        foreach (float ballBounce in new[] { BowlingSceneBuilder.BallBounce, 0.4f })
        foreach (float bounce in new[] { BowlingSceneBuilder.PinBounce, 0.6f })
        {
            string label = $"ball bounce {ballBounce:0.00}, pin bounce {bounce:0.00}";
            sb.AppendLine(Table(label, pins =>
            {
                var mat = new PhysicsMaterial("sweep")
                {
                    bounciness = bounce, dynamicFriction = friction, staticFriction = friction,
                    frictionCombine = PhysicsMaterialCombine.Maximum,
                };
                foreach (var p in pins)
                    foreach (var c in p.GetComponentsInChildren<Collider>()) c.sharedMaterial = mat;
                var ball = UnityEngine.Object.FindAnyObjectByType<BowlingBall>().GetComponent<Collider>();
                ball.sharedMaterial = new PhysicsMaterial("ball")
                {
                    bounciness = ballBounce, dynamicFriction = 0.1f, staticFriction = 0.1f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                };
            }, 0.01f, new[] { 6f, 7f, 8f, 9f, 10f }));
        }
        Debug.Log("[BowlingPhysicsCheck] sweep\n" + sb);
    }

    // Same throws under different collision modes and physics step sizes, to spot step artifacts.
    [MenuItem("Bowling/Physics Step Sweep")]
    public static void StepSweep()
    {
        var sb = new StringBuilder();
        foreach (float dt in new[] { 0.01f, 0.005f })
        foreach (var mode in new[] { CollisionDetectionMode.Discrete, CollisionDetectionMode.ContinuousSpeculative, CollisionDetectionMode.ContinuousDynamic })
            sb.AppendLine(Table($"pins {mode}, dt {dt}", pins =>
            {
                foreach (var p in pins) p.GetComponent<Rigidbody>().collisionDetectionMode = mode;
            }, dt, new[] { 6f, 7f, 8f, 9f, 10f }));
        Debug.Log("[BowlingPhysicsCheck] step sweep\n" + sb);
    }

    static string Table(string label, Action<Pin[]> tweak, float dt = 0.01f, float[] speeds = null)
    {
        var sb = new StringBuilder($"{label}\n  speed |");
        foreach (float o in Offsets) sb.Append($" {o,5:0.00}");
        sb.AppendLine("  | total");
        foreach (float speed in speeds ?? new[] { 6f, 8f, 10f })
        {
            sb.Append($"  {speed,4:0} m/s|");
            int total = 0;
            foreach (float o in Offsets)
            {
                int down = Throw(o, speed, tweak, dt);
                total += down;
                sb.Append($" {down,5}");
            }
            sb.AppendLine($"  | {total}");
        }
        return sb.ToString();
    }

    // Returns pins down after one straight throw from the foul line.
    public static int Throw(float offset, float speed, Action<Pin[]> tweak = null, float dt = 0.01f)
    {
        var oldMode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        try
        {
            EditorSceneManager.OpenScene(ScenePath);
            var ball = UnityEngine.Object.FindAnyObjectByType<BowlingBall>().GetComponent<Rigidbody>();
            var pins = UnityEngine.Object.FindObjectsByType<Pin>();
            tweak?.Invoke(pins);

            ball.isKinematic = false;
            ball.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            ball.position = ball.transform.position = new Vector3(offset, BowlingBall.Radius + 0.002f, 0f);
            ball.linearVelocity = new Vector3(0f, 0f, speed);
            ball.angularVelocity = new Vector3(speed / BowlingBall.Radius, 0f, 0f);
            Physics.SyncTransforms();

            for (float t = 0f; t < 7f; t += dt) Physics.Simulate(dt);

            return pins.Count(p => p.transform.position.y < -0.05f || Vector3.Angle(p.transform.up, Vector3.up) > 30f);
        }
        finally { Physics.simulationMode = oldMode; }
    }
}
