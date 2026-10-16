using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders a few screenshots of the bowling scene to Logs/Preview without entering Play mode.
// Handy for checking the look after a rebuild (Bowling > Render Preview).
public static class BowlingPreview
{
    [MenuItem("Bowling/Render Preview")]
    public static void Render()
    {
        EditorSceneManager.OpenScene("Assets/Bowling/Scenes/Bowling.unity");

        // Put the bowler in its stance pose (normally done in Play mode).
        var bowler = Object.FindAnyObjectByType<Bowler>();
        typeof(Bowler).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(bowler, null);
        var ball = Object.FindAnyObjectByType<BowlingBall>();
        var socket = bowler.GetComponentsInChildren<Transform>();
        foreach (var t in socket) if (t.name == "BallSocket") ball.transform.position = t.position;

        var cam = Camera.main;
        Directory.CreateDirectory("Logs/Preview");
        Shot(cam, new Vector3(0.35f, 1.75f, -4.4f), 11f, "Logs/Preview/aim.png");
        Shot(cam, new Vector3(0f, 0.9f, 15f), 10f, "Logs/Preview/pins.png");
        Shot(cam, new Vector3(1.4f, 1.3f, -0.6f), 15f, "Logs/Preview/bowler.png", lookAt: bowler.transform.position + Vector3.up * 0.9f);

        // The throw, frozen at a few moments, seen from the side.
        foreach (var (t, file) in new[] { (0.28f, "backswing"), (0.55f, "release"), (0.85f, "follow") })
        {
            PoseBowler(bowler, t);
            Vector3 body = bowler.transform.position + Vector3.up * 0.7f;
            Shot(cam, body + new Vector3(2.6f, 0.2f, 0.3f), 0f, $"Logs/Preview/{file}.png", lookAt: body);
        }
        Debug.Log("[BowlingPreview] wrote Logs/Preview/*.png");
    }

    // Uses Bowler's private PoseAt/Apply so the preview shows exactly what the game plays.
    static void PoseBowler(Bowler bowler, float t)
    {
        const BindingFlags any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
        object pose = typeof(Bowler).GetMethod("PoseAt", any).Invoke(null, new object[] { t });
        float rootZ = (float)pose.GetType().GetField("rootZ").GetValue(pose);
        bowler.transform.position = new Vector3(bowler.transform.position.x, 0f, rootZ);
        typeof(Bowler).GetMethod("Apply", any).Invoke(bowler, new[] { pose });
    }

    static void Shot(Camera cam, Vector3 pos, float pitch, string path, Vector3? lookAt = null)
    {
        cam.transform.position = pos;
        cam.transform.rotation = lookAt is Vector3 target
            ? Quaternion.LookRotation(target - pos)
            : Quaternion.Euler(pitch, 0f, 0f);

        const int w = 1280, h = 720;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = prev;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        Object.DestroyImmediate(tex);
    }
}
