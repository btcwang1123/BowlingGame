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
    TrailRenderer trail;
    NeonGlow band;
    Color bandColor;
    bool bombArmed;
    IReadOnlyList<Pin> pinsForBomb;

    public BallType Type { get; private set; }
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

    // Stop rolling and become a held, kinematic object.
    public void PickUp()
    {
        IsRolling = false;
        hook = 0f;
        bombArmed = false;
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

    // speed in m/s, angle in degrees (+ = right), hook in m/s² (+ = curves right).
    public void Throw(float speed, float angle, float hookAcceleration)
    {
        Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // fast ball, thin pins
        rb.linearVelocity = dir * speed;
        // Start it rolling instead of sliding: ω = v / r around the axis perpendicular to travel.
        rb.angularVelocity = Vector3.Cross(Vector3.up, dir) * (speed / Radius);
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
            rb.AddForce(Vector3.right * hook, ForceMode.Acceleration);
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
