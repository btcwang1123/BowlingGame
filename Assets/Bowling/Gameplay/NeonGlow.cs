using System.Collections.Generic;
using UnityEngine;

// Marks a renderer as a neon tube. All neon tubes breathe slowly, and NeonGlow.Flash()
// makes the whole arena light up (used for strikes and spares).
// Neon materials use the URP Unlit shader with an HDR base colour, which the bloom turns into glow.
// Uses a MaterialPropertyBlock so shared materials are never modified.
[RequireComponent(typeof(Renderer))]
public class NeonGlow : MonoBehaviour
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly List<NeonGlow> All = new();
    static float flashTime = -10f;
    static Color flashColor = Color.white;

    [ColorUsage(false, true)] public Color color = new(0f, 2f, 2f);
    [Tooltip("Offsets the breathing so neighbouring tubes don't pulse in sync.")]
    public float phase;

    Renderer rend;
    MaterialPropertyBlock block;

    void OnEnable()
    {
        rend = GetComponent<Renderer>();
        block ??= new MaterialPropertyBlock();
        All.Add(this);
    }

    void OnDisable() => All.Remove(this);

    // Light everything up in one colour, fading back over about a second.
    public static void Flash(Color color)
    {
        flashColor = color;
        flashTime = Time.unscaledTime;
    }

    void LateUpdate()
    {
        float t = Time.unscaledTime;
        float breathe = 0.85f + 0.15f * Mathf.Sin(t * 1.6f + phase);
        float flash = Mathf.Exp(-(t - flashTime) * 3f);
        // Strobe a few times during the flash.
        float strobe = flash * (0.6f + 0.4f * Mathf.Sign(Mathf.Sin((t - flashTime) * 40f)));
        Color c = Color.Lerp(color * breathe, flashColor * 2.5f, strobe);
        block.SetColor(BaseColor, c);
        rend.SetPropertyBlock(block);
    }
}
