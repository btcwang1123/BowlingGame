using UnityEngine;

// Game juice: spark bursts, camera shake and slow motion. The spark ParticleSystem is
// built by BowlingSceneBuilder (so its material is included in builds).
public class BowlingFX : MonoBehaviour
{
    public static readonly Color Cyan = new(0.1f, 1f, 1f), Magenta = new(1f, 0.15f, 0.85f),
        Purple = new(0.55f, 0.2f, 1f), Gold = new(1f, 0.8f, 0.2f);

    // Game speed outside slow motion and pauses. Autoplay raises it to test faster.
    public static float NormalTimeScale = 1f;

    [SerializeField] ParticleSystem sparks;

    float shake;
    float slowMoUntil = -1f;
    Vector3 shakeOffset;

    // Camera offset to add this frame; the game applies it after positioning the camera.
    public Vector3 ShakeOffset => shakeOffset;

    void Awake()
    {
        if (sparks == null) sparks = GetComponentInChildren<ParticleSystem>();
    }

    public void Sparks(Vector3 position, Color color, int count, float speed = 3f)
    {
        if (sparks == null) return;
        var p = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };
        for (int i = 0; i < count; i++)
        {
            p.velocity = Random.onUnitSphere * Random.Range(0.4f, 1f) * speed + Vector3.up * speed * 0.5f;
            p.startColor = Color.Lerp(color, Color.white, Random.Range(0f, 0.35f));
            p.startSize = Random.Range(0.025f, 0.07f);
            p.startLifetime = Random.Range(0.5f, 1.1f);
            sparks.Emit(p, 1);
        }
    }

    // Burst over the whole rack in mixed neon colours.
    public void Celebrate(bool strike)
    {
        Color[] colors = strike ? new[] { Cyan, Magenta, Gold, Purple } : new[] { Cyan, Purple };
        int bursts = strike ? 8 : 4;
        for (int i = 0; i < bursts; i++)
        {
            var pos = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.2f, 0.9f), BowlingLane.HeadPinZ + Random.Range(0f, 0.9f));
            Sparks(pos, colors[i % colors.Length], strike ? 30 : 18, strike ? 4.5f : 3f);
        }
        AddShake(strike ? 0.06f : 0.03f);
    }

    public void AddShake(float amount) => shake = Mathf.Min(shake + amount, 0.12f);

    // Slow time for a moment (in real seconds), easing back to normal.
    public void SlowMo(float realSeconds, float scale = 0.3f)
    {
        slowMoUntil = Time.unscaledTime + realSeconds;
        Time.timeScale = scale * NormalTimeScale;
    }

    public void CancelSlowMo()
    {
        slowMoUntil = -1f;
        Time.timeScale = NormalTimeScale;
    }

    void Update()
    {
        if (slowMoUntil > 0f && Time.unscaledTime > slowMoUntil)
        {
            Time.timeScale = Mathf.MoveTowards(Time.timeScale, NormalTimeScale, Time.unscaledDeltaTime * 2.5f * NormalTimeScale);
            if (Time.timeScale >= NormalTimeScale) slowMoUntil = -1f;
        }

        shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 0.25f);
        shakeOffset = shake > 0f ? Random.insideUnitSphere * shake : Vector3.zero;
    }

    void OnDisable() => Time.timeScale = NormalTimeScale;
}
