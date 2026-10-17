using System.Collections.Generic;
using UnityEngine;

// A challenge obstacle. Moving ones are kinematic and follow a function of time,
// so the game (FixedUpdate) and the editor's solvability check (Step) move them identically.
public class Obstacle : MonoBehaviour
{
    ObstacleDef def;
    Rigidbody rb;
    float startTime;

    public void Init(ObstacleDef d)
    {
        def = d;
        rb = GetComponent<Rigidbody>();
        startTime = Time.time;
        Step(0f);
    }

    void FixedUpdate() => Step(Time.time - startTime);

    // Place the obstacle where it should be t seconds into the level.
    public void Step(float t)
    {
        if (rb == null) return;
        switch (def.kind)
        {
            case ObstacleKind.Mover:
                float x = def.x + def.amplitude * Mathf.Sin(2f * Mathf.PI * (def.speed * t + def.phase));
                rb.MovePosition(new Vector3(x, transform.position.y, def.z));
                break;
            case ObstacleKind.Spinner:
                rb.MoveRotation(Quaternion.Euler(0f, def.phase + def.speed * t, 0f));
                break;
        }
    }

    // Builds obstacles from definitions. Materials come from the scene's ChallengeKit, so they are in builds.
    public static List<Obstacle> Build(IEnumerable<ObstacleDef> defs, ChallengeKit kit, Transform parent)
    {
        var list = new List<Obstacle>();
        foreach (var d in defs) list.Add(BuildOne(d, kit, parent));
        return list;
    }

    static Obstacle BuildOne(ObstacleDef d, ChallengeKit kit, Transform parent)
    {
        var root = new GameObject(d.kind.ToString());
        root.transform.SetParent(parent, false);
        Color neon = d.kind == ObstacleKind.Spinner ? kit.neonGold : d.kind == ObstacleKind.Mover ? kit.neonMagenta : kit.neonCyan;
        Material neonMat = d.kind == ObstacleKind.Spinner ? kit.goldMat : d.kind == ObstacleKind.Mover ? kit.magentaMat : kit.cyanMat;

        switch (d.kind)
        {
            case ObstacleKind.Block:
            case ObstacleKind.Mover:
            {
                root.transform.position = new Vector3(d.x, d.size.y / 2f, d.z);
                var body = Part(PrimitiveType.Cube, "Body", root.transform, Vector3.zero, d.size, kit.darkMat, keepCollider: true);
                body.GetComponent<Collider>().sharedMaterial = kit.wallPhysics;
                Glow(Part(PrimitiveType.Cube, "Edge", root.transform, new Vector3(0f, d.size.y / 2f, 0f),
                    new Vector3(d.size.x + 0.01f, 0.02f, d.size.z + 0.01f), neonMat), neon);
                foreach (float s in new[] { -1f, 1f })
                    Glow(Part(PrimitiveType.Cube, "Side", root.transform, new Vector3(s * d.size.x / 2f, 0f, -d.size.z / 2f - 0.003f),
                        new Vector3(0.015f, d.size.y, 0.01f), neonMat), neon);
                break;
            }
            case ObstacleKind.Spinner:
            {
                root.transform.position = new Vector3(d.x, d.size.y / 2f + 0.005f, d.z);
                var bar = Part(PrimitiveType.Cube, "Bar", root.transform, Vector3.zero, d.size, kit.darkMat, keepCollider: true);
                bar.GetComponent<Collider>().sharedMaterial = kit.wallPhysics;
                Glow(Part(PrimitiveType.Cube, "Edge", root.transform, new Vector3(0f, d.size.y / 2f, 0f),
                    new Vector3(d.size.x + 0.01f, 0.02f, d.size.z + 0.01f), neonMat), neon);
                Glow(Part(PrimitiveType.Cylinder, "Hub", root.transform, new Vector3(0f, d.size.y / 2f + 0.02f, 0f),
                    new Vector3(0.08f, 0.02f, 0.08f), neonMat), neon);
                break;
            }
            case ObstacleKind.Bump:
            {
                // A wide cylinder lying across the lane, sunk so only the top `height` sticks up.
                float r = d.size.z, h = d.size.y;
                root.transform.position = new Vector3(0f, h - r, d.z);
                root.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                var hump = Part(PrimitiveType.Cylinder, "Hump", root.transform, Vector3.zero, new Vector3(2f * r, d.size.x / 2f, 2f * r), kit.darkMat, keepCollider: true);
                hump.GetComponent<Collider>().sharedMaterial = kit.wallPhysics;
                Glow(Part(PrimitiveType.Cube, "Crest", root.transform, new Vector3(r + 0.001f, 0f, 0f),
                    new Vector3(0.004f, d.size.x, 0.02f), neonMat), neon);
                break;
            }
        }

        if (d.kind is ObstacleKind.Mover or ObstacleKind.Spinner)
        {
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
        var obstacle = root.AddComponent<Obstacle>();
        obstacle.Init(d);
        return obstacle;
    }

    static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (!keepCollider) DestroyCollider(go);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static void DestroyCollider(GameObject go)
    {
        var c = go.GetComponent<Collider>();
        if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
    }

    static void Glow(GameObject go, Color color)
    {
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<NeonGlow>().color = color;
    }
}
