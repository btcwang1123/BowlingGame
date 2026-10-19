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

    // Outfit: runtime materials per colour slot, the head's original shape, and the current accessories.
    Material litTemplate, unlitTemplate;   // the scene's suit and neon materials, copied per slot
    Material topMat, bottomMat, skinMat, shoeMat, headMat, hairMat, hatMat, darkMat, neonMat;
    Transform head, visor, accessories;
    Vector3 headScale, visorPos, visorScale;
    string appliedOutfit;

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
        head = Find("Helmet");
        visor = Find("Visor");
        litTemplate = head.GetComponent<Renderer>().sharedMaterial;
        unlitTemplate = visor.GetComponent<Renderer>().sharedMaterial;
        headScale = head.localScale;
        visorPos = visor.localPosition;
        visorScale = visor.localScale;
        Apply(Stance);
    }

    Transform Find(string name)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        throw new InvalidOperationException($"Bowler is missing joint '{name}' — rebuild the scene (Bowling → Rebuild Scene).");
    }

    // ---- Outfit ----

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), Smoothness = Shader.PropertyToID("_Smoothness");

    static Material Tinted(Material template, Color color, float smoothness)
    {
        var m = new Material(template) { hideFlags = HideFlags.HideAndDontSave };
        m.SetColor(BaseColor, color);
        if (m.HasProperty(Smoothness)) m.SetFloat(Smoothness, smoothness);
        return m;
    }

    static Color Hdr(Color c) => c * 2.6f;

    // Dress the figure: recolour every part by its slot, swap the headwear, rebuild the accessories.
    public void ApplyOutfit(Outfit o)
    {
        if (appliedOutfit == o.id) return;
        appliedOutfit = o.id;
        if (headMat == skinMat) headMat = null;
        var lit = litTemplate;
        foreach (var m in new[] { topMat, bottomMat, skinMat, shoeMat, headMat, hairMat, hatMat, darkMat, neonMat })
            if (m != null) Destroy(m);
        bool fullHelmet = o.head is HeadStyle.Helmet or HeadStyle.Astronaut or HeadStyle.Robot;
        topMat = Tinted(lit, o.top, o.suit ? 0.55f : 0.3f);
        bottomMat = Tinted(lit, o.bottom, 0.3f);
        skinMat = Tinted(lit, o.skin, 0.35f);
        shoeMat = Tinted(lit, o.shoes, 0.8f);
        hairMat = Tinted(lit, o.hair, 0.4f);
        hatMat = Tinted(lit, o.hat, o.head is HeadStyle.Crown or HeadStyle.Astronaut ? 0.9f : 0.45f);
        darkMat = Tinted(lit, new Color(0.03f, 0.03f, 0.05f), 0.95f);
        headMat = o.head switch
        {
            HeadStyle.Helmet or HeadStyle.Astronaut => Tinted(lit, o.hat, 0.95f),
            HeadStyle.Robot or HeadStyle.Ninja => Tinted(lit, o.top, 0.8f),
            _ => skinMat,
        };
        neonMat = new Material(unlitTemplate) { hideFlags = HideFlags.HideAndDontSave };

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (accessories != null && r.transform.IsChildOf(accessories)) continue;
            bool right = r.transform.IsChildOf(shoulderR) || r.transform.IsChildOf(hipR);
            switch (r.name)
            {
                case "Torso": case "Shoulder Pad": case "Upper Arm": r.sharedMaterial = topMat; break;
                case "Forearm": r.sharedMaterial = o.shortSleeves ? skinMat : topMat; break;
                case "Hips": case "Thigh": case "Shin": r.sharedMaterial = bottomMat; break;
                case "Neck": case "Hand": r.sharedMaterial = o.suit ? topMat : skinMat; break;
                case "Shoe": r.sharedMaterial = shoeMat; break;
                case "Helmet": r.sharedMaterial = headMat; break;
            }
            if (r.TryGetComponent<NeonGlow>(out var glow))
                glow.color = r.name switch
                {
                    "Chest Stripe" or "Sole" => Hdr(o.neonB),
                    "Wristband" => Hdr(right ? o.neonB : o.neonA),
                    "Visor" => Hdr(o.head == HeadStyle.Astronaut ? o.neonB : o.neonA),
                    _ => Hdr(o.neonA),
                };
        }

        head.localScale = o.head == HeadStyle.Astronaut ? headScale * 1.3f : headScale;
        head.gameObject.SetActive(o.head != HeadStyle.Robot);
        visor.gameObject.SetActive(fullHelmet);
        visor.localPosition = o.head == HeadStyle.Astronaut ? visorPos + new Vector3(0f, 0f, 0.035f) : visorPos;
        visor.localScale = o.head == HeadStyle.Astronaut ? new Vector3(visorScale.x * 1.15f, visorScale.y * 2.4f, visorScale.z) : visorScale;

        if (accessories != null) Destroy(accessories.gameObject);
        accessories = new GameObject("Outfit Accessories").transform;
        accessories.SetParent(spine, false);
        BuildAccessories(o);
    }

    GameObject Part(PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        var col = go.GetComponent<Collider>();
        col.enabled = false;
        Destroy(col);
        go.transform.SetParent(accessories, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    void NeonBit(PrimitiveType type, Vector3 pos, Vector3 scale, Color color, Vector3 euler = default)
    {
        var go = Part(type, pos, scale, neonMat, euler);
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<NeonGlow>().color = Hdr(color);
    }

    // Head sits at (0, 0.7, 0) on the spine, about 0.12 m across; the face looks down +Z.
    void BuildAccessories(Outfit o)
    {
        void Hair() => Part(PrimitiveType.Sphere, new Vector3(0f, 0.725f, -0.012f), new Vector3(0.245f, 0.215f, 0.245f), hairMat);
        switch (o.head)
        {
            case HeadStyle.Hair: Hair(); break;
            case HeadStyle.Pompadour:
                Hair();
                Part(PrimitiveType.Sphere, new Vector3(0f, 0.81f, 0.05f), new Vector3(0.17f, 0.1f, 0.16f), hairMat);
                break;
            case HeadStyle.Afro:
                Part(PrimitiveType.Sphere, new Vector3(0f, 0.75f, -0.015f), new Vector3(0.34f, 0.32f, 0.34f), hairMat);
                break;
            case HeadStyle.Cap:
                Part(PrimitiveType.Sphere, new Vector3(0f, 0.73f, -0.01f), new Vector3(0.24f, 0.2f, 0.24f), hatMat);
                Part(PrimitiveType.Cube, new Vector3(0f, 0.765f, 0.115f), new Vector3(0.2f, 0.015f, 0.12f), hatMat, new Vector3(-8f, 0f, 0f));
                break;
            case HeadStyle.Crown:
                Hair();
                Part(PrimitiveType.Cylinder, new Vector3(0f, 0.835f, 0f), new Vector3(0.17f, 0.03f, 0.17f), hatMat);
                for (int i = 0; i < 6; i++)
                {
                    float ang = i * Mathf.PI / 3f;
                    Part(PrimitiveType.Cube, new Vector3(Mathf.Sin(ang) * 0.075f, 0.875f, Mathf.Cos(ang) * 0.075f), Vector3.one * 0.035f, hatMat, new Vector3(45f, ang * Mathf.Rad2Deg, 45f));
                }
                NeonBit(PrimitiveType.Sphere, new Vector3(0f, 0.84f, 0.085f), Vector3.one * 0.03f, o.neonB);
                break;
            case HeadStyle.CatEars:
                Hair();
                foreach (float s in new[] { -1f, 1f })
                {
                    Part(PrimitiveType.Cube, new Vector3(s * 0.065f, 0.805f, 0f), new Vector3(0.06f, 0.06f, 0.025f), hairMat, new Vector3(0f, 0f, 45f));
                    Part(PrimitiveType.Cube, new Vector3(s * 0.065f, 0.8f, 0.006f), new Vector3(0.035f, 0.035f, 0.02f), hatMat, new Vector3(0f, 0f, 45f));
                }
                break;
            case HeadStyle.Mohawk:
                NeonBit(PrimitiveType.Cube, new Vector3(0f, 0.79f, -0.01f), new Vector3(0.035f, 0.1f, 0.21f), o.hair);
                break;
            case HeadStyle.Ninja:
                Part(PrimitiveType.Cube, new Vector3(0f, 0.71f, 0.1f), new Vector3(0.17f, 0.035f, 0.04f), skinMat);
                Part(PrimitiveType.Cylinder, new Vector3(0f, 0.765f, 0f), new Vector3(0.238f, 0.018f, 0.238f), hatMat);
                foreach (float s in new[] { -1f, 1f })
                    Part(PrimitiveType.Cube, new Vector3(s * 0.025f, 0.73f, -0.13f), new Vector3(0.03f, 0.1f, 0.008f), hatMat, new Vector3(25f, 0f, s * 15f));
                break;
            case HeadStyle.Astronaut:
                Part(PrimitiveType.Cube, new Vector3(0f, 0.32f, -0.16f), new Vector3(0.26f, 0.3f, 0.1f), hatMat);
                NeonBit(PrimitiveType.Cube, new Vector3(0f, 0.38f, -0.212f), new Vector3(0.16f, 0.02f, 0.01f), o.neonA);
                break;
            case HeadStyle.Robot:
                Part(PrimitiveType.Cube, new Vector3(0f, 0.7f, 0f), new Vector3(0.21f, 0.21f, 0.21f), headMat);
                Part(PrimitiveType.Cylinder, new Vector3(0f, 0.84f, 0f), new Vector3(0.012f, 0.04f, 0.012f), headMat);
                NeonBit(PrimitiveType.Sphere, new Vector3(0f, 0.895f, 0f), Vector3.one * 0.045f, o.neonA);
                foreach (float s in new[] { -1f, 1f })
                    Part(PrimitiveType.Cylinder, new Vector3(s * 0.115f, 0.7f, 0f), new Vector3(0.05f, 0.012f, 0.05f), darkMat, new Vector3(0f, 0f, 90f));
                break;
        }
        if (o.shades) Part(PrimitiveType.Cube, new Vector3(0f, 0.715f, 0.105f), new Vector3(0.19f, 0.04f, 0.03f), darkMat);
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
