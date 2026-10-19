using System;
using System.Collections.Generic;
using UnityEngine;

// The ball. Kinematic while the bowler holds it, then physics takes over at release.
// "hook" is a small sideways push while it rolls down the lane, so a curved
// swipe (or a wrist twist) makes the ball curve.
// Challenge balls: Heavy (much more mass) and Bomb (blows the pins apart when it reaches them).
[RequireComponent(typeof(Rigidbody))]
public class BowlingBall : MonoBehaviour
{
    public const float Radius = 0.109f;
    public const float BombRadius = 1.1f;   // pins within this of the blast are pushed away
    const float BombImpulse = 7f;           // N·s at the centre of the blast

    // The hook stops near the pins so the ball doesn't steer through the rack.
    [SerializeField] float hookEndZ = 17.5f;

    Rigidbody rb;
    float hook;
    // Hook profile from the equipped ball: where it starts to turn and how sharply (see SetReaction).
    float skidZ = -1f, backend, reactionNorm = 1f;
    const float EarlyRead = 0.25f;   // share of full hook a ball already has while it is still skidding
    TrailRenderer trail;
    NeonGlow band;
    Color bandColor;
    bool bombArmed;
    IReadOnlyList<Pin> pinsForBomb;
    // Technique (see BallTechniques): spin around the vertical axis and how hard it scatters pins.
    float topSpin, pinScatter;
    bool hitAPin;

    public BallType Type { get; private set; }
    public float SizeScale { get; private set; } = 1f;
    public float CurrentRadius => Radius * SizeScale;
    public bool IsRolling { get; private set; }
    public Vector3 Velocity => rb.linearVelocity;

    // Fired when the rolling ball hits a pin: contact point and impact speed (m/s).
    public event Action<Vector3, float> HitPin;
    // Fired when a bomb ball goes off, at the blast centre.
    public event Action<Vector3> Exploded;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        trail = GetComponentInChildren<TrailRenderer>();
        band = GetComponentInChildren<NeonGlow>();
        if (band != null) bandColor = band.color;
        PickUp();
    }

    // Set the ball's weight and kind for the next throws. pins are what a bomb pushes.
    public void Configure(float massKg, BallType type, IReadOnlyList<Pin> pins)
    {
        Type = type;
        rb.mass = type == BallType.Heavy ? massKg * 2.2f : massKg;
        pinsForBomb = pins;
        if (band != null)
            band.color = type switch
            {
                BallType.Heavy => new Color(3f, 2f, 0.4f),   // gold
                BallType.Bomb => new Color(3f, 0.5f, 0.2f),  // hot orange
                _ => bandColor,
            };
    }

    // How the equipped ball turns: it mostly skids until skidZ (metres past the foul line), then
    // hooks, sharply when backend is near 1. Scaled so the total hook by the pins stays what the
    // throw asked for: a long, angular ball moves the same boards but enters the pins at a steeper angle.
    public void SetReaction(float skidMetres, float backendSharpness)
    {
        skidZ = skidMetres;
        backend = Mathf.Clamp01(backendSharpness);
        float shaped = 0f, flat = 0f;
        const int steps = 64;
        for (int i = 0; i < steps; i++)
        {
            float z = (i + 0.5f) / steps * hookEndZ, lever = BowlingLane.HeadPinZ - z;
            shaped += RawProfile(z) * lever;
            flat += lever;
        }
        reactionNorm = shaped > 0f ? flat / shaped : 1f;
    }

    float RawProfile(float z)
    {
        float ramp = Mathf.Lerp(4f, 1.5f, backend);
        float t = Mathf.Clamp01((z - (skidZ - ramp)) / (2f * ramp));
        return Mathf.Lerp(EarlyRead, 1f + backend, t * t * (3f - 2f * t));
    }

    float HookProfile(float z) => skidZ < 0f ? 1f : RawProfile(z) * reactionNorm;

    // The equipped ball's look and size (from the pro shop).
    public void ApplyLook(Texture2D surface, Color glow, float sizeScale)
    {
        if (!Mathf.Approximately(sizeScale, SizeScale))
        {
            transform.localScale = transform.localScale / SizeScale * sizeScale;
            SizeScale = sizeScale;
        }
        if (TryGetComponent<Renderer>(out var rend))
        {
            var mat = rend.material; // runtime instance, never the asset
            mat.SetTexture("_BaseMap", surface);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_EmissionMap", surface);
            mat.SetColor("_EmissionColor", glow * 0.25f);
            mat.EnableKeyword("_EMISSION");
        }
        bandColor = glow * 2.6f;
        if (band != null && Type == BallType.Normal) band.color = bandColor;
    }

    // Stop rolling and become a held, kinematic object.
    public void PickUp()
    {
        IsRolling = false;
        hook = 0f;
        bombArmed = false;
        topSpin = pinScatter = 0f;
        hitAPin = false;
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        // Kinematic bodies can't use ContinuousDynamic, so switch modes along with it.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.isKinematic = true;
        if (trail != null) { trail.emitting = false; trail.Clear(); }
    }

    // Move the held ball (the bowler calls this every frame; replays too).
    public void Hold(Vector3 position)
    {
        transform.position = position;
        rb.position = position;
    }

    // The next throw's technique: spin (rad/s, vertical axis), pin scatter (0–1), and a mass
    // multiplier on top of Configure's weight. Cleared when the ball is picked up again.
    public void SetTechnique(float spinRate, float scatter, float massScale)
    {
        topSpin = spinRate;
        pinScatter = scatter;
        rb.mass *= massScale;
    }

    // speed in m/s, angle in degrees (+ = right), hook in m/s² (+ = curves right).
    public void Throw(float speed, float angle, float hookAcceleration)
    {
        Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        // A larger ball sits higher: lift it so it doesn't start inside the lane.
        var start = rb.position;
        start.y = Mathf.Max(start.y, CurrentRadius + 0.002f);
        rb.position = transform.position = start;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // fast ball, thin pins
        rb.linearVelocity = dir * speed;
        // Start it rolling instead of sliding: ω = v / r around the axis perpendicular to travel.
        rb.angularVelocity = Vector3.Cross(Vector3.up, dir) * (speed / CurrentRadius);
        // A spinner turns like a top, with only a little forward roll.
        if (topSpin != 0f) rb.angularVelocity = rb.angularVelocity * 0.3f + Vector3.up * topSpin;
        hitAPin = false;
        hook = hookAcceleration;
        IsRolling = true;
        bombArmed = Type == BallType.Bomb;
        if (trail != null) { trail.Clear(); trail.emitting = true; }
    }

    void OnCollisionEnter(Collision c)
    {
        if (!IsRolling) return;
        float impact = c.relativeVelocity.magnitude;
        var audio = BowlingAudio.Instance;
        if (c.rigidbody != null && c.rigidbody.TryGetComponent<Pin>(out _))
        {
            if (audio != null) audio.BallHitPin(impact);
            if (pinScatter > 0f) Scatter(c.rigidbody);
            HitPin?.Invoke(c.GetContact(0).point, impact);
        }
        else if (impact > 1f && audio != null) audio.Thud(impact); // dropping into the gutter or pit, hitting the back
    }

    void FixedUpdate()
    {
        if (!IsRolling) return;
        if (bombArmed && pinsForBomb != null && ShouldExplode(transform.position, ActivePinPositions()))
        {
            bombArmed = false;
            Explode(transform.position, ActivePinBodies());
            Exploded?.Invoke(transform.position);
        }
        if (hook == 0f) return;
        bool onLane = Mathf.Abs(transform.position.x) < BowlingLane.HalfWidth;
        if (onLane && transform.position.z < hookEndZ)
            rb.AddForce(Vector3.right * (hook * HookProfile(transform.position.z)), ForceMode.Acceleration);
    }

    // Spinner: the spinning ball flicks each pin it touches sideways, and loses a little more speed
    // on the first one (spinners deflect).
    void Scatter(Rigidbody pin)
    {
        Vector3 away = pin.worldCenterOfMass - transform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 1e-6f) return;
        Vector3 side = Vector3.Cross(Vector3.up, away.normalized) * Mathf.Sign(topSpin);
        pin.AddForce((side * 0.55f + Vector3.up * 0.08f) * pinScatter, ForceMode.Impulse);
        if (!hitAPin) rb.linearVelocity *= 0.9f;
        hitAPin = true;
    }

    IEnumerable<Vector3> ActivePinPositions()
    {
        foreach (var p in pinsForBomb) if (p.gameObject.activeSelf) yield return p.transform.position;
    }

    IEnumerable<Rigidbody> ActivePinBodies()
    {
        foreach (var p in pinsForBomb) if (p.gameObject.activeSelf) yield return p.GetComponent<Rigidbody>();
    }

    // The bomb goes off when it touches a pin, or reaches the pin deck. Static so the
    // editor's challenge check uses exactly the same rule.
    public static bool ShouldExplode(Vector3 ball, IEnumerable<Vector3> pins)
    {
        if (ball.z > BowlingLane.HeadPinZ - 0.15f && ball.y > -0.05f) return true;
        foreach (var p in pins)
        {
            var d = new Vector2(ball.x - p.x, ball.z - p.z);
            if (d.magnitude < Radius + 0.08f) return true;
        }
        return false;
    }

    public static void Explode(Vector3 centre, IEnumerable<Rigidbody> bodies)
    {
        foreach (var body in bodies)
        {
            if (body == null || body.isKinematic) continue;
            Vector3 away = body.worldCenterOfMass - centre;
            float d = away.magnitude;
            if (d > BombRadius) continue;
            away.y = Mathf.Max(away.y, 0f) + 0.35f * d; // knock them up and out
            float strength = BombImpulse * (1f - d / BombRadius);
            body.AddForce(away.normalized * strength, ForceMode.Impulse);
            body.AddTorque(UnityEngine.Random.onUnitSphere * strength * 0.05f, ForceMode.Impulse);
        }
    }
}
