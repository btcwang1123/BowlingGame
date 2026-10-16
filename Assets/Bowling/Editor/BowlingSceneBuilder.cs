using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Builds Assets/Bowling/Scenes/Bowling.unity from scratch: a neon "cosmic bowling" arena with
// lane, gutters, pit, 10 pins, the bowler character, ball, effects, lights and post-processing.
// Run it from the menu (Bowling > Rebuild Scene) or headless with -executeMethod BowlingSceneBuilder.Build.
// After that the scene is an ordinary scene you can edit by hand; rebuilding overwrites it.
public static class BowlingSceneBuilder
{
    const string Root = "Assets/Bowling";
    public const string ScenePath = Root + "/Scenes/Bowling.unity";

    // Bump this whenever the scene layout changes: an older scene is rebuilt automatically
    // (see BowlingPlayModeStart), so nobody has to remember to click Rebuild Scene.
    public const int SceneVersion = 4;

    public static bool SceneIsCurrent() =>
        System.IO.File.Exists(ScenePath) &&
        System.Text.RegularExpressions.Regex.IsMatch(System.IO.File.ReadAllText(ScenePath), $@"sceneVersion: {SceneVersion}\r?$",
            System.Text.RegularExpressions.RegexOptions.Multiline);

    // Tuned with Bowling > Physics Sweep. Public so the sweep can try values around them.
    public const float PinFriction = 0.4f, PinBounce = 0.35f, BallBounce = 0.05f;

    // HDR neon colours (values above 1 are what the bloom picks up).
    static readonly Color NeonCyan = new(0f, 2.2f, 2.6f), NeonMagenta = new(2.6f, 0.3f, 2.2f),
        NeonPurple = new(1.2f, 0.4f, 3f), NeonGold = new(3f, 2f, 0.4f);

    static Material laneMat, darkMat, floorMat, suitMat, helmetMat, pinMat, ballMat, holeMat;
    static Material cyanMat, magentaMat, purpleMat, goldMat, additiveMat;

    [MenuItem("Bowling/Rebuild Scene")]
    public static void Build()
    {
        foreach (var dir in new[] { "Scenes", "Materials", "Prefabs", "Settings" })
            if (!AssetDatabase.IsValidFolder($"{Root}/{dir}")) AssetDatabase.CreateFolder(Root, dir);

        laneMat = MakeMaterial("Lane", new Color(0.04f, 0.04f, 0.09f), 0.93f);
        darkMat = MakeMaterial("Dark", new Color(0.015f, 0.015f, 0.03f), 0.15f);
        floorMat = MakeMaterial("Floor", new Color(0.008f, 0.006f, 0.016f), 0.05f);
        suitMat = MakeMaterial("BowlerSuit", new Color(0.1f, 0.09f, 0.17f), 0.55f);
        helmetMat = MakeMaterial("BowlerHelmet", new Color(0.04f, 0.04f, 0.07f), 0.95f);
        pinMat = MakeMaterial("Pin", new Color(0.95f, 0.95f, 1f), 0.85f, new Color(0.12f, 0.16f, 0.3f)); // blacklight glow
        ballMat = MakeMaterial("Ball", new Color(0.25f, 0.05f, 0.55f), 0.95f, new Color(0.35f, 0.05f, 0.8f));
        holeMat = MakeMaterial("BallHole", Color.black, 0.2f);
        cyanMat = MakeNeonMaterial("NeonCyan", NeonCyan);
        magentaMat = MakeNeonMaterial("NeonMagenta", NeonMagenta);
        purpleMat = MakeNeonMaterial("NeonPurple", NeonPurple);
        goldMat = MakeNeonMaterial("NeonGold", NeonGold);
        additiveMat = MakeAdditiveMaterial("GlowAdditive", MakeGlowTexture());

        // Oiled lane: the ball slides; pins grip (see friction combine modes below).
        var laneFriction = MakePhysicsMaterial("LaneOil", 0.04f, 0.06f, 0f, PhysicsMaterialCombine.Average);
        var pinPhysics = MakePhysicsMaterial("PinPhysics", PinFriction, PinFriction, PinBounce, PhysicsMaterialCombine.Maximum);
        var ballPhysics = MakePhysicsMaterial("BallPhysics", 0.1f, 0.1f, BallBounce, PhysicsMaterialCombine.Minimum);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        BuildLane(laneFriction, pinPhysics);
        BuildArena();
        BuildPins(pinPhysics);
        var ball = BuildBall(ballPhysics);
        BuildBowler(ball);
        BuildFX();
        BuildLightingAndCamera();

        // Materials the game uses to build challenge obstacles and the aim guide at runtime.
        var kit = new GameObject("Challenge Kit").AddComponent<ChallengeKit>();
        kit.darkMat = darkMat;
        kit.cyanMat = cyanMat;
        kit.magentaMat = magentaMat;
        kit.goldMat = goldMat;
        kit.additiveMat = additiveMat;
        kit.wallPhysics = MakePhysicsMaterial("WallPhysics", 0.2f, 0.2f, 0.3f, PhysicsMaterialCombine.Average);
        kit.neonCyan = NeonCyan;
        kit.neonMagenta = NeonMagenta;
        kit.neonGold = NeonGold;

        var gameObject = new GameObject("Bowling Game");
        var game = new SerializedObject(gameObject.AddComponent<BowlingGame>());
        game.FindProperty("sceneVersion").intValue = SceneVersion;
        game.ApplyModifiedPropertiesWithoutUndo();
        gameObject.AddComponent<BowlingUI>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log($"[BowlingSceneBuilder] Built {ScenePath}");
    }

    // ---- Lane: top surface at y = 0, foul line at z = 0 ----

    static void BuildLane(PhysicsMaterial laneFriction, PhysicsMaterial pinPhysics)
    {
        float half = BowlingLane.HalfWidth;
        float start = -3f, end = BowlingLane.LaneEndZ, back = 21.5f;
        var lane = new GameObject("Lane");

        // One collider for the whole lane: a seam between two boxes makes the ball hop over the pins.
        // The ball still slides on oil while pins grip, because pins use Maximum friction combine,
        // which wins over the lane's Average; the ball uses Minimum.
        Block("Lane Surface", lane, new Vector3(0f, -0.05f, (start + end) / 2f), new Vector3(half * 2f, 0.1f, end - start), laneMat, laneFriction);

        const float gutterWidth = 0.24f, gutterDepth = 0.08f;
        foreach (float side in new[] { -1f, 1f })
        {
            string s = side < 0 ? "L" : "R";
            float gx = side * (half + gutterWidth / 2f);
            Block($"Gutter {s}", lane, new Vector3(gx, -gutterDepth - 0.05f, (start + end) / 2f),
                new Vector3(gutterWidth, 0.1f, end - start), darkMat, laneFriction);
            float wx = side * (half + gutterWidth + 0.05f);
            Block($"Side Wall {s}", lane, new Vector3(wx, 0.2f, (start + back) / 2f),
                new Vector3(0.1f, 0.6f, back - start), darkMat, null);

            // Neon: lane edge, and the top of the side wall.
            Neon($"Lane Edge {s}", lane, new Vector3(side * (half - 0.008f), 0.0015f, (start + end) / 2f),
                new Vector3(0.012f, 0.003f, end - start), cyanMat, NeonCyan, 0f);
            Neon($"Wall Top {s}", lane, new Vector3(wx, 0.51f, (start + back) / 2f),
                new Vector3(0.03f, 0.02f, back - start), magentaMat, NeonMagenta, 1f);
        }

        // Pit behind the pins, and the back cushion.
        float pitWidth = (half + gutterWidth + 0.1f) * 2f;
        Block("Pit Floor", lane, new Vector3(0f, -0.45f, (end + back) / 2f), new Vector3(pitWidth, 0.1f, back - end), darkMat, pinPhysics);
        Block("Back Cushion", lane, new Vector3(0f, 0.3f, back + 0.05f), new Vector3(pitWidth, 1.6f, 0.1f), darkMat, pinPhysics);

        Neon("Foul Line", lane, new Vector3(0f, 0.0015f, 0f), new Vector3(half * 2f, 0.003f, 0.025f), magentaMat, NeonMagenta, 0f);

        // Aiming arrows: chevrons on boards 5, 10, 15 and 20 from each edge, the middle one furthest.
        const float board = 1.05f / 39f;
        foreach (int fromEdge in new[] { 5, 10, 15, 20 })
            foreach (float side in fromEdge == 20 ? new[] { 1f } : new[] { -1f, 1f })
            {
                float x = side * (half - fromEdge * board) * (fromEdge == 20 ? 0f : 1f);
                float z = 3.7f + fromEdge * 0.06f;
                Chevron(lane, new Vector3(x, 0.0015f, z), fromEdge % 10 == 0 ? purpleMat : cyanMat, fromEdge % 10 == 0 ? NeonPurple : NeonCyan);
            }
    }

    static void Chevron(GameObject parent, Vector3 tip, Material mat, Color color)
    {
        foreach (float side in new[] { -1f, 1f })
        {
            var arm = Neon("Arrow", parent, Vector3.zero, new Vector3(0.012f, 0.003f, 0.11f), mat, color, tip.z);
            arm.transform.rotation = Quaternion.Euler(0f, side * 25f, 0f);
            arm.transform.position = tip + arm.transform.rotation * new Vector3(0f, 0f, -0.055f);
        }
    }

    // ---- Arena: neon arches, masking panel above the pins, neighbouring lanes, floor ----

    static void BuildArena()
    {
        var arena = new GameObject("Arena");

        var floor = Block("Floor", arena, new Vector3(0f, -0.15f, 20f), new Vector3(80f, 0.05f, 100f), floorMat, null);
        Object.DestroyImmediate(floor.GetComponent<Collider>());

        var archColors = new[] { (cyanMat, NeonCyan), (magentaMat, NeonMagenta), (purpleMat, NeonPurple), (cyanMat, NeonCyan) };
        float[] archZ = { 2.5f, 6.5f, 10.5f, 14.5f };
        for (int i = 0; i < archZ.Length; i++)
        {
            var (mat, color) = archColors[i];
            var arch = new GameObject($"Arch {i + 1}");
            arch.transform.SetParent(arena.transform);
            foreach (float side in new[] { -1f, 1f })
                Neon("Post", arch, new Vector3(side * 1.1f, 1.2f, archZ[i]), new Vector3(0.05f, 2.7f, 0.05f), mat, color, i * 0.9f);
            Neon("Beam", arch, new Vector3(0f, 2.55f, archZ[i]), new Vector3(2.25f, 0.05f, 0.05f), mat, color, i * 0.9f);
        }

        // Masking panel hiding the pinsetter, with neon stripes.
        var mask = Block("Masking Panel", arena, new Vector3(0f, 1.55f, 19.9f), new Vector3(2.3f, 1.3f, 0.1f), darkMat, null);
        Object.DestroyImmediate(mask.GetComponent<Collider>());
        Neon("Mask Stripe Top", arena, new Vector3(0f, 2.15f, 19.84f), new Vector3(2.2f, 0.04f, 0.02f), magentaMat, NeonMagenta, 0.5f);
        Neon("Mask Stripe Mid", arena, new Vector3(0f, 1.55f, 19.84f), new Vector3(1.6f, 0.03f, 0.02f), goldMat, NeonGold, 1.5f);
        Neon("Mask Stripe Low", arena, new Vector3(0f, 0.95f, 19.84f), new Vector3(2.2f, 0.04f, 0.02f), cyanMat, NeonCyan, 2.5f);

        // Neighbouring lanes, for atmosphere (no physics).
        foreach (float x in new[] { -2.4f, 2.4f })
        {
            var other = Block("Neighbour Lane", arena, new Vector3(x, -0.05f, 8f), new Vector3(1.05f, 0.1f, 22f), laneMat, null);
            Object.DestroyImmediate(other.GetComponent<Collider>());
            foreach (float side in new[] { -1f, 1f })
                Neon("Neighbour Edge", arena, new Vector3(x + side * 0.52f, 0.0015f, 8f), new Vector3(0.012f, 0.003f, 22f),
                    purpleMat, NeonPurple, x);
        }
    }

    // ---- Pins ----

    static void BuildPins(PhysicsMaterial pinPhysics)
    {
        var pinPrefab = MakePinPrefab(pinPhysics);
        var rack = new GameObject("Pins");
        var spots = BowlingLane.PinSpots();
        for (int i = 0; i < spots.Length; i++)
        {
            var pin = (GameObject)PrefabUtility.InstantiatePrefab(pinPrefab, rack.transform);
            pin.name = $"Pin {i + 1}";
            pin.transform.position = spots[i];
        }
    }

    // Pin: pivot at its base, ~38 cm tall. Body is a capsule plus a flat base so it can stand up.
    static GameObject MakePinPrefab(PhysicsMaterial physics)
    {
        var pin = new GameObject("Pin");

        // Looks: a real pin profile. Physics: a capsule (simpler and more stable than a mesh collider).
        var body = new GameObject("Body");
        body.transform.SetParent(pin.transform, false);
        body.AddComponent<MeshFilter>().sharedMesh = MakePinMesh();
        body.AddComponent<MeshRenderer>().sharedMaterial = pinMat;

        var capsule = pin.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 0.2f, 0f);
        capsule.radius = 0.06f;
        capsule.height = 0.36f;
        capsule.sharedMaterial = physics;

        // Two neon rings round the neck.
        foreach (var (y, d) in new[] { (0.258f, 0.05f), (0.282f, 0.054f) })
            NeonPart(PrimitiveType.Cylinder, "Stripe", pin, new Vector3(0f, y, 0f), new Vector3(d, 0.005f, d), magentaMat, NeonMagenta);

        var foot = pin.AddComponent<BoxCollider>();
        foot.center = new Vector3(0f, 0.011f, 0f);
        foot.size = new Vector3(0.06f, 0.022f, 0.06f);
        foot.sharedMaterial = physics;

        var rb = pin.AddComponent<Rigidbody>();
        rb.mass = 1.5f;
        rb.automaticCenterOfMass = false;
        rb.centerOfMass = new Vector3(0f, 0.15f, 0f); // real pins are bottom-heavy
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // smooth in slow motion
        rb.solverIterations = 12;
        pin.AddComponent<Pin>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(pin, Root + "/Prefabs/Pin.prefab");
        Object.DestroyImmediate(pin);
        return prefab;
    }

    // ---- Ball: glowing, with finger holes and a neon band so you can see it spin ----

    static BowlingBall BuildBall(PhysicsMaterial physics)
    {
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Ball";
        ball.transform.position = new Vector3(0f, BowlingBall.Radius + 0.002f, 0f);
        ball.transform.localScale = Vector3.one * BowlingBall.Radius * 2f;
        ball.GetComponent<Renderer>().sharedMaterial = ballMat;
        ball.GetComponent<Collider>().sharedMaterial = physics;

        // Child sizes are in ball space, where the ball's radius is 0.5.
        var band = Primitive(PrimitiveType.Cylinder, "Band", ball, Vector3.zero, new Vector3(1.012f, 0.05f, 1.012f), cyanMat);
        band.AddComponent<NeonGlow>().color = NeonCyan;
        Vector3[] holes = { new(0f, 0.47f, 0.12f), new(-0.1f, 0.45f, -0.12f), new(0.1f, 0.45f, -0.12f) };
        foreach (var h in holes) Primitive(PrimitiveType.Sphere, "Finger Hole", ball, h, Vector3.one * 0.13f, holeMat);

        var rb = ball.AddComponent<Rigidbody>();
        rb.mass = 6.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.maxAngularVelocity = 200f; // default 7 rad/s is far too low for a rolling ball

        var trail = ball.AddComponent<TrailRenderer>();
        trail.sharedMaterial = additiveMat;
        trail.time = 0.4f;
        trail.minVertexDistance = 0.04f;
        trail.widthMultiplier = 0.2f;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.colorGradient = Gradient2(new Color(0.3f, 1f, 1f, 0.9f), new Color(1f, 0.2f, 0.9f, 0f));
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.emitting = false;

        return ball.AddComponent<BowlingBall>();
    }

    // ---- Bowler: a jointed neon athlete. Joint names are what Bowler.cs looks up. ----

    static void BuildBowler(BowlingBall ball)
    {
        var root = new GameObject("Bowler");
        root.transform.position = new Vector3(-0.22f, 0f, -1.9f);

        var pelvis = Joint("Pelvis", root, new Vector3(0f, 0.95f, 0f));
        Primitive(PrimitiveType.Sphere, "Hips", pelvis, new Vector3(0f, 0f, 0f), new Vector3(0.3f, 0.2f, 0.2f), suitMat);
        NeonPart(PrimitiveType.Cylinder, "Belt", pelvis, new Vector3(0f, 0.04f, 0f), new Vector3(0.305f, 0.012f, 0.205f), cyanMat, NeonCyan);

        var spine = Joint("Spine", pelvis, new Vector3(0f, 0.05f, 0f));
        Primitive(PrimitiveType.Capsule, "Torso", spine, new Vector3(0f, 0.28f, 0f), new Vector3(0.34f, 0.26f, 0.22f), suitMat);
        NeonPart(PrimitiveType.Cube, "Chest Stripe", spine, new Vector3(0f, 0.38f, 0.105f), new Vector3(0.24f, 0.025f, 0.02f), magentaMat, NeonMagenta);
        NeonPart(PrimitiveType.Cube, "Back Stripe", spine, new Vector3(0f, 0.3f, -0.105f), new Vector3(0.03f, 0.3f, 0.02f), cyanMat, NeonCyan);
        Primitive(PrimitiveType.Cylinder, "Neck", spine, new Vector3(0f, 0.57f, 0f), new Vector3(0.08f, 0.04f, 0.08f), suitMat);
        Primitive(PrimitiveType.Sphere, "Helmet", spine, new Vector3(0f, 0.7f, 0f), new Vector3(0.23f, 0.24f, 0.23f), helmetMat);
        NeonPart(PrimitiveType.Cube, "Visor", spine, new Vector3(0f, 0.71f, 0.09f), new Vector3(0.18f, 0.05f, 0.06f), cyanMat, NeonCyan);

        foreach (float side in new[] { 1f, -1f })
        {
            string s = side > 0 ? "R" : "L";
            var shoulder = Joint($"Shoulder{s}", spine, new Vector3(side * 0.22f, 0.5f, 0f));
            Primitive(PrimitiveType.Sphere, "Shoulder Pad", shoulder, Vector3.zero, Vector3.one * 0.11f, suitMat);
            Primitive(PrimitiveType.Capsule, "Upper Arm", shoulder, new Vector3(0f, -0.14f, 0f), new Vector3(0.085f, 0.14f, 0.085f), suitMat);
            var elbow = Joint($"Elbow{s}", shoulder, new Vector3(0f, -0.29f, 0f));
            Primitive(PrimitiveType.Capsule, "Forearm", elbow, new Vector3(0f, -0.13f, 0f), new Vector3(0.075f, 0.13f, 0.075f), suitMat);
            NeonPart(PrimitiveType.Cylinder, "Wristband", elbow, new Vector3(0f, -0.22f, 0f), new Vector3(0.085f, 0.015f, 0.085f), side > 0 ? magentaMat : cyanMat, side > 0 ? NeonMagenta : NeonCyan);
            Primitive(PrimitiveType.Sphere, "Hand", elbow, new Vector3(0f, -0.29f, 0f), Vector3.one * 0.08f, suitMat);
            if (side > 0) Joint("BallSocket", elbow, new Vector3(0f, -0.37f, 0.02f));

            var hip = Joint($"Hip{s}", pelvis, new Vector3(side * 0.1f, -0.05f, 0f));
            Primitive(PrimitiveType.Capsule, "Thigh", hip, new Vector3(0f, -0.21f, 0f), new Vector3(0.13f, 0.21f, 0.13f), suitMat);
            var knee = Joint($"Knee{s}", hip, new Vector3(0f, -0.43f, 0f));
            Primitive(PrimitiveType.Capsule, "Shin", knee, new Vector3(0f, -0.2f, 0f), new Vector3(0.11f, 0.2f, 0.11f), suitMat);
            Primitive(PrimitiveType.Cube, "Shoe", knee, new Vector3(0f, -0.43f, 0.05f), new Vector3(0.11f, 0.07f, 0.26f), helmetMat);
            NeonPart(PrimitiveType.Cube, "Sole", knee, new Vector3(0f, -0.465f, 0.05f), new Vector3(0.115f, 0.015f, 0.27f), magentaMat, NeonMagenta);
        }

        root.AddComponent<Bowler>();
    }

    static GameObject Joint(string name, GameObject parent, Vector3 localPos)
    {
        var j = new GameObject(name);
        j.transform.SetParent(parent.transform, false);
        j.transform.localPosition = localPos;
        return j;
    }

    // ---- Effects: spark particles ----

    static void BuildFX()
    {
        var fx = new GameObject("FX");
        fx.AddComponent<BowlingFX>();

        var go = new GameObject("Sparks");
        go.transform.SetParent(fx.transform);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.startSpeed = 0f;
        main.gravityModifier = 0.9f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 1500;

        var emission = ps.emission;
        emission.enabled = false; // the game calls Emit() directly

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = Gradient2(Color.white, new Color(1f, 1f, 1f, 0f));

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = additiveMat;
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.04f;
        r.lengthScale = 1.5f;
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    // ---- Lights, camera, fog and post-processing (bloom makes the neon glow) ----

    static void BuildLightingAndCamera()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.035f, 0.03f, 0.07f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.02f, 0.01f, 0.05f);
        RenderSettings.fogStartDistance = 18f;
        RenderSettings.fogEndDistance = 45f;

        var sun = Object.FindAnyObjectByType<Light>();
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(60f, -25f, 0f);
            sun.color = new Color(0.55f, 0.55f, 1f);
            sun.intensity = 0.15f;
        }
        // At most 4 point lights: URP lights each object with up to 4 additional lights.
        PointLight("Pin Light Magenta", new Vector3(-1.2f, 2.2f, 17.6f), new Color(1f, 0.2f, 0.85f), 4f, 7f);
        PointLight("Pin Light Cyan", new Vector3(1.2f, 2.2f, 17.6f), new Color(0.2f, 1f, 1f), 4f, 7f);
        PointLight("Lane Light Purple", new Vector3(0f, 2.8f, 8f), new Color(0.6f, 0.3f, 1f), 3f, 11f);
        PointLight("Bowler Light", new Vector3(0.9f, 2.4f, -2.6f), new Color(0.4f, 0.9f, 1f), 2.5f, 6f);

        var cam = Camera.main;
        cam.transform.SetPositionAndRotation(new Vector3(0.35f, 1.75f, -4.4f), Quaternion.Euler(11f, 0f, 0f));
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 80f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.01f, 0.05f);
        var camData = cam.GetUniversalAdditionalCameraData();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

        var volume = new GameObject("Post Processing").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = MakeVolumeProfile();
    }

    static void PointLight(string name, Vector3 pos, Color color, float intensity, float range)
    {
        var light = new GameObject(name).AddComponent<Light>();
        light.type = LightType.Point;
        light.transform.position = pos;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
    }

    static VolumeProfile MakeVolumeProfile()
    {
        string path = $"{Root}/Settings/NeonVolume.asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(1.6f);
        bloom.scatter.Override(0.75f);
        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);
        var color = profile.Add<ColorAdjustments>(true);
        color.saturation.Override(20f);
        color.contrast.Override(12f);
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.35f);
        vignette.smoothness.Override(0.5f);
        var aberration = profile.Add<ChromaticAberration>(true);
        aberration.intensity.Override(0.12f);

        foreach (var component in profile.components)
        {
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        EditorUtility.SetDirty(profile);
        return profile;
    }

    // ---- Helpers ----

    static GameObject Block(string name, GameObject parent, Vector3 center, Vector3 size, Material mat, PhysicsMaterial physics)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (physics != null) go.GetComponent<Collider>().sharedMaterial = physics;
        go.isStatic = true;
        return go;
    }

    // A glowing strip: no collider, no shadow, animated by NeonGlow.
    static GameObject Neon(string name, GameObject parent, Vector3 center, Vector3 size, Material mat, Color color, float phase)
    {
        var go = Block(name, parent, center, size, mat, null);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.isStatic = false; // NeonGlow animates it
        var r = go.GetComponent<Renderer>();
        r.shadowCastingMode = ShadowCastingMode.Off;
        var glow = go.AddComponent<NeonGlow>();
        glow.color = color;
        glow.phase = phase;
        return go;
    }

    // A primitive used only for looks: no collider.
    static GameObject Primitive(PrimitiveType type, string name, GameObject parent, Vector3 localPos, Vector3 localScale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject NeonPart(PrimitiveType type, string name, GameObject parent, Vector3 localPos, Vector3 localScale, Material mat, Color color)
    {
        var go = Primitive(type, name, parent, localPos, localScale, mat);
        go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        go.AddComponent<NeonGlow>().color = color;
        return go;
    }

    static Gradient Gradient2(Color from, Color to)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
            new[] { new GradientAlphaKey(from.a, 0f), new GradientAlphaKey(to.a, 1f) });
        return g;
    }

    static Material MakeMaterial(string name, Color color, float smoothness, Color? emission = null)
    {
        var mat = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        // Set the keyword through LocalKeyword: plain EnableKeyword didn't stick on these materials.
        var keyword = new UnityEngine.Rendering.LocalKeyword(mat.shader, "_EMISSION");
        mat.SetKeyword(keyword, emission.HasValue);
        mat.SetColor("_EmissionColor", emission ?? Color.black);
        mat.globalIlluminationFlags = emission.HasValue ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Neon tube: unlit HDR colour, so it glows under bloom regardless of lighting.
    static Material MakeNeonMaterial(string name, Color hdr)
    {
        var mat = LoadOrCreateMaterial(name, "Universal Render Pipeline/Unlit");
        mat.SetColor("_BaseColor", hdr);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Additive unlit material for the ball trail and sparks.
    static Material MakeAdditiveMaterial(string name, Texture2D texture)
    {
        var mat = LoadOrCreateMaterial(name, "Universal Render Pipeline/Particles/Unlit");
        mat.SetTexture("_BaseMap", texture);
        mat.SetColor("_BaseColor", new Color(2f, 2f, 2f, 1f)); // HDR so it blooms
        mat.SetFloat("_Surface", 1f);   // transparent
        mat.SetFloat("_Blend", 2f);     // additive
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material LoadOrCreateMaterial(string name, string shader)
    {
        string path = $"{Root}/Materials/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader.name != shader) mat.shader = Shader.Find(shader);
        return mat;
    }

    // A ten-pin as a surface of revolution, from the regulation profile (height, radius) in metres.
    static Mesh MakePinMesh()
    {
        string path = $"{Root}/Materials/PinMesh.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        Vector2[] profile =
        {
            new(0f, 0.026f), new(0.01f, 0.034f), new(0.04f, 0.048f), new(0.08f, 0.058f), new(0.114f, 0.0605f),
            new(0.15f, 0.058f), new(0.19f, 0.046f), new(0.225f, 0.031f), new(0.254f, 0.0228f), new(0.28f, 0.025f),
            new(0.31f, 0.03f), new(0.343f, 0.0323f), new(0.362f, 0.028f), new(0.374f, 0.018f), new(0.379f, 0.008f), new(0.381f, 0f),
        };
        const int seg = 32;
        var verts = new System.Collections.Generic.List<Vector3>();
        var normals = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();

        for (int i = 0; i < profile.Length; i++)
        {
            // Normal of the outline at this point: perpendicular to the neighbouring slope.
            Vector2 a = profile[Mathf.Max(i - 1, 0)], b = profile[Mathf.Min(i + 1, profile.Length - 1)];
            Vector2 slope = (b - a).normalized;          // (dHeight, dRadius)
            Vector2 n2 = new Vector2(slope.x, -slope.y); // (radial, up)
            for (int s = 0; s <= seg; s++)
            {
                float ang = s * Mathf.PI * 2f / seg;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                verts.Add(dir * profile[i].y + Vector3.up * profile[i].x);
                normals.Add((dir * n2.x + Vector3.up * n2.y).normalized);
            }
        }
        for (int i = 0; i < profile.Length - 1; i++)
            for (int s = 0; s < seg; s++)
            {
                int v0 = i * (seg + 1) + s, v1 = v0 + seg + 1;
                tris.AddRange(new[] { v0, v1, v0 + 1, v0 + 1, v1, v1 + 1 });
            }
        // Flat base.
        int centre = verts.Count;
        verts.Add(Vector3.zero);
        normals.Add(Vector3.down);
        int ring = verts.Count;
        for (int s = 0; s <= seg; s++)
        {
            float ang = s * Mathf.PI * 2f / seg;
            verts.Add(new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * profile[0].y);
            normals.Add(Vector3.down);
        }
        for (int s = 0; s < seg; s++) tris.AddRange(new[] { centre, ring + s, ring + s + 1 });

        var mesh = new Mesh { name = "Pin" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // Soft round dot, used by the trail and sparks.
    static Texture2D MakeGlowTexture()
    {
        string path = $"{Root}/Materials/GlowDot.asset";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null) return tex;
        const int size = 64;
        tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        AssetDatabase.CreateAsset(tex, path);
        return tex;
    }

    static PhysicsMaterial MakePhysicsMaterial(string name, float dynamicFriction, float staticFriction, float bounciness, PhysicsMaterialCombine frictionCombine)
    {
        string path = $"{Root}/Materials/{name}.asset";
        var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (mat == null)
        {
            mat = new PhysicsMaterial(name);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.dynamicFriction = dynamicFriction;
        mat.staticFriction = staticFriction;
        mat.bounciness = bounciness;
        mat.frictionCombine = frictionCombine;
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
