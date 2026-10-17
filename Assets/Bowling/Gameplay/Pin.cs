using UnityEngine;

// One bowling pin. Remembers where it was racked so it can be reset,
// and decides on its own whether it counts as knocked down.
// Challenges can make pins heavy, mark them as gold targets, or sway the whole rack.
[RequireComponent(typeof(Rigidbody))]
public class Pin : MonoBehaviour
{
    // A pin tilted more than this from upright counts as down.
    const float FallenAngle = 30f;
    public const float NormalMass = 1.5f, HeavyMass = 3.75f;

    Rigidbody rb;
    Vector3 rackPosition;
    Quaternion rackRotation;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rackPosition = transform.position;
        rackRotation = transform.rotation;
    }

    public bool IsStanding => gameObject.activeSelf && !IsDown;

    public bool IsDown
    {
        get
        {
            if (transform.position.y < rackPosition.y - 0.05f) return true; // fell off the deck
            return Vector3.Angle(transform.up, Vector3.up) > FallenAngle;
        }
    }

    public bool IsSettled => rb.isKinematic || rb.IsSleeping() ||
                             (rb.linearVelocity.sqrMagnitude < 0.0025f && rb.angularVelocity.sqrMagnitude < 0.01f);

    // Put the pin back on its spot, standing and still.
    public void ResetToRack()
    {
        gameObject.SetActive(true);
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(rackPosition, rackRotation);
        rb.position = rackPosition;
        rb.rotation = rackRotation;
        rb.Sleep();
    }

    public void SetHeavy(bool heavy) => rb.mass = heavy ? HeavyMass : NormalMass;

    // Moving-rack challenge: carry the standing pin to its spot + offset (kinematic, so it can't fall).
    public void Sway(Vector3 offset)
    {
        if (!gameObject.activeSelf) return;
        rb.isKinematic = true;
        rb.MovePosition(rackPosition + offset);
        rb.MoveRotation(rackRotation);
    }

    // Let a swaying pin stand free where it is.
    public void StopSway()
    {
        if (!gameObject.activeSelf || !rb.isKinematic) return;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.Sleep();
    }

    void OnCollisionEnter(Collision c)
    {
        if (c.rigidbody != null && c.rigidbody.TryGetComponent<BowlingBall>(out _)) return; // the ball plays that one
        float impact = c.relativeVelocity.magnitude;
        if (impact > 0.4f && BowlingAudio.Instance != null) BowlingAudio.Instance.PinHit(impact);
    }

    // Challenge target pins glow gold instead of their usual stripe colour.
    public bool IsTarget { get; private set; }
    static readonly Color Gold = new(3f, 2f, 0.4f);
    NeonGlow[] stripes;
    Color[] stripeColors;

    public void SetTarget(bool target)
    {
        if (stripes == null)
        {
            stripes = GetComponentsInChildren<NeonGlow>(true);
            stripeColors = System.Array.ConvertAll(stripes, s => s.color);
        }
        IsTarget = target;
        for (int i = 0; i < stripes.Length; i++) stripes[i].color = target ? Gold : stripeColors[i];
    }

    // Sweep a fallen pin off the deck between rolls.
    public void Remove() => gameObject.SetActive(false);
}
