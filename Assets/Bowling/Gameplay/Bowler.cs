using System;
using UnityEngine;

// The bowler: a jointed figure (built by BowlingSceneBuilder) animated in code.
// Holds the ball while aiming, then plays approach → backswing → slide/release → follow-through,
// and calls back at the release moment so the game can throw the ball from the hand.
public class Bowler : MonoBehaviour
{
    // Joint angles in degrees. Pitch is rotation about X: + leans the torso forward,
    // and swings a limb that hangs down backwards. Limb pitches are relative to their parent.
    [Serializable]
    struct Pose
    {
        public float rootZ, pelvisY, spine;
        public float shoulderR, elbowR, shoulderL, elbowL, rollL;
        public float hipR, kneeR, hipL, kneeL;

        public static Pose Lerp(Pose a, Pose b, float t)
        {
            float L(float x, float y) => Mathf.LerpUnclamped(x, y, t);
            return new Pose
            {
                rootZ = L(a.rootZ, b.rootZ), pelvisY = L(a.pelvisY, b.pelvisY), spine = L(a.spine, b.spine),
                shoulderR = L(a.shoulderR, b.shoulderR), elbowR = L(a.elbowR, b.elbowR),
                shoulderL = L(a.shoulderL, b.shoulderL), elbowL = L(a.elbowL, b.elbowL), rollL = L(a.rollL, b.rollL),
                hipR = L(a.hipR, b.hipR), kneeR = L(a.kneeR, b.kneeR), hipL = L(a.hipL, b.hipL), kneeL = L(a.kneeL, b.kneeL),
            };
        }
    }

    // Ball held at chest, left hand supporting it.
    static readonly Pose Stance = new()
    {
        rootZ = -1.9f, pelvisY = 0.95f, spine = 8f,
        shoulderR = -35f, elbowR = -75f, shoulderL = -30f, elbowL = -70f, rollL = 25f,
    };
    // A step forward with the arm swung high behind.
    static readonly Pose Backswing = new()
    {
        rootZ = -1.2f, pelvisY = 0.9f, spine = 20f,
        shoulderR = 80f, elbowR = 0f, shoulderL = -60f, elbowL = -10f, rollL = -40f,
        hipR = -25f, kneeR = 15f, hipL = 20f, kneeL = 25f,
    };
    // Sliding into the foul line, low, ball at the bottom of the swing.
    static readonly Pose Release = new()
    {
        rootZ = -0.55f, pelvisY = 0.66f, spine = 35f,
        shoulderR = -45f, elbowR = 0f, shoulderL = -20f, elbowL = -10f, rollL = -75f,
        hipR = 50f, kneeR = 5f, hipL = -55f, kneeL = 75f,
    };
    // Arm finishes high and points at the target.
    static readonly Pose FollowThrough = new()
    {
        rootZ = -0.45f, pelvisY = 0.68f, spine = 30f,
        shoulderR = -160f, elbowR = -20f, shoulderL = -10f, elbowL = -10f, rollL = -80f,
        hipR = 50f, kneeR = 5f, hipL = -55f, kneeL = 75f,
    };

    const float BackswingTime = 0.28f, ReleaseTime = 0.55f, FollowTime = 0.85f;
    const float BallDropStart = 0.4f;   // from here the ball eases from the hand down to the lane
    const float HandOffsetX = 0.22f;    // the right hand hangs this far right of the body centre

    Transform pelvis, spine, shoulderR, elbowR, shoulderL, elbowL, hipR, kneeR, hipL, kneeL, ballSocket;
    BowlingBall heldBall;
    float aimX, throwTime = -1f, breath;
    Vector3 releasePoint;
    Action onRelease;

    public bool IsThrowing => throwTime >= 0f && throwTime < FollowTime;

    // Left-handed bowlers are the same figure mirrored, holding the ball on the left.
    public bool LeftHanded
    {
        get => transform.localScale.x < 0f;
        set => transform.localScale = new Vector3(value ? -1f : 1f, 1f, 1f);
    }

    void Awake()
    {
        pelvis = Find("Pelvis"); spine = Find("Spine");
        shoulderR = Find("ShoulderR"); elbowR = Find("ElbowR");
        shoulderL = Find("ShoulderL"); elbowL = Find("ElbowL");
        hipR = Find("HipR"); kneeR = Find("KneeR"); hipL = Find("HipL"); kneeL = Find("KneeL");
        ballSocket = Find("BallSocket");
        Apply(Stance);
    }

    Transform Find(string name)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        throw new InvalidOperationException($"Bowler is missing joint '{name}' — rebuild the scene (Bowling → Rebuild Scene).");
    }

    // Pick up the ball and stand ready, with the ball over the given spot on the lane.
    public void Ready(BowlingBall ball, float ballX)
    {
        heldBall = ball;
        throwTime = -1f;
        SetAim(ballX);
    }

    public void SetAim(float ballX) => aimX = ballX;

    // Play the throw. onRelease fires when the ball leaves the hand, at lane height on the foul line.
    public void Throw(Action onRelease)
    {
        this.onRelease = onRelease;
        throwTime = 0f;
        releasePoint = new Vector3(aimX, BowlingBall.Radius + 0.002f, 0f);
    }

    void LateUpdate()
    {
        Pose pose;
        if (throwTime < 0f)
        {
            breath += Time.deltaTime;
            pose = Stance;
            pose.pelvisY += Mathf.Sin(breath * 2f) * 0.008f; // idle bob so it doesn't look frozen
        }
        else
        {
            float before = throwTime;
            throwTime += Time.deltaTime;
            pose = PoseAt(throwTime);
            if (before < ReleaseTime && throwTime >= ReleaseTime) ReleaseBall();
        }

        transform.position = new Vector3(aimX - HandOffsetX * transform.localScale.x, 0f, pose.rootZ);
        Apply(pose);
        if (heldBall != null) heldBall.Hold(HeldBallPosition());
    }

    static Pose PoseAt(float t)
    {
        if (t < BackswingTime) return Pose.Lerp(Stance, Backswing, Smooth(t / BackswingTime));
        if (t < ReleaseTime) return Pose.Lerp(Backswing, Release, Smooth((t - BackswingTime) / (ReleaseTime - BackswingTime)));
        return Pose.Lerp(Release, FollowThrough, Smooth(Mathf.Clamp01((t - ReleaseTime) / (FollowTime - ReleaseTime))));
    }

    static float Smooth(float x) => x * x * (3f - 2f * x);

    Vector3 HeldBallPosition()
    {
        Vector3 hand = ballSocket.position;
        if (throwTime < BallDropStart) return hand;
        // The figure's arm doesn't quite reach the floor, so ease the ball down to the release point.
        float u = Smooth(Mathf.Clamp01((throwTime - BallDropStart) / (ReleaseTime - BallDropStart)));
        return Vector3.Lerp(hand, releasePoint, u);
    }

    void ReleaseBall()
    {
        if (heldBall == null) return;
        heldBall.Hold(releasePoint);
        heldBall = null;
        onRelease?.Invoke();
    }

    void Apply(Pose p)
    {
        pelvis.localPosition = new Vector3(0f, p.pelvisY, 0f);
        spine.localRotation = Quaternion.Euler(p.spine, 0f, 0f);
        shoulderR.localRotation = Quaternion.Euler(p.shoulderR, 0f, 0f);
        elbowR.localRotation = Quaternion.Euler(p.elbowR, 0f, 0f);
        shoulderL.localRotation = Quaternion.Euler(p.shoulderL, 0f, p.rollL);
        elbowL.localRotation = Quaternion.Euler(p.elbowL, 0f, 0f);
        hipR.localRotation = Quaternion.Euler(p.hipR, 0f, 0f);
        kneeR.localRotation = Quaternion.Euler(p.kneeR, 0f, 0f);
        hipL.localRotation = Quaternion.Euler(p.hipL, 0f, 0f);
        kneeL.localRotation = Quaternion.Euler(p.kneeL, 0f, 0f);
    }
}
