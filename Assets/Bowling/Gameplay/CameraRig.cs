using UnityEngine;

// The aiming/follow camera and the on-lane aim guide line.
public class CameraRig
{
    readonly Camera cam;
    readonly LineRenderer aimLine;
    readonly Vector3 aimCameraOffset;
    readonly float aimCameraPitch, followHeight, followDistance, followMaxZ, followPitch;
    Vector3 lastShake;

    public CameraRig(Camera cam, Material aimLineMaterial, Vector3 aimCameraOffset, float aimCameraPitch,
        float followHeight, float followDistance, float followMaxZ, float followPitch)
    {
        this.cam = cam;
        this.aimCameraOffset = aimCameraOffset;
        this.aimCameraPitch = aimCameraPitch;
        this.followHeight = followHeight;
        this.followDistance = followDistance;
        this.followMaxZ = followMaxZ;
        this.followPitch = followPitch;
        aimLine = MakeAimLine(aimLineMaterial);
    }

    Vector3 AimPosition(float aimX) => new Vector3(aimX, 0f, 0f) + aimCameraOffset;

    // Snap straight to the aiming spot (no lerp) — called when a new ball is readied.
    public void SnapToAim(float aimX) =>
        cam.transform.SetPositionAndRotation(AimPosition(aimX), Quaternion.Euler(aimCameraPitch, 0f, 0f));

    public void Tick(BowlingGame.Phase phase, float aimX, Vector3 ballPos, Vector3 shakeOffset, float unscaledDeltaTime)
    {
        Vector3 targetPos;
        Quaternion targetRot;
        if (phase == BowlingGame.Phase.Menu)
        {
            // Slow cinematic drift behind the bowler while the menu is up.
            float t = Time.unscaledTime * 0.15f;
            targetPos = new Vector3(Mathf.Sin(t) * 0.9f, 1.5f + Mathf.Sin(t * 0.7f) * 0.2f, -3.6f);
            targetRot = Quaternion.LookRotation(new Vector3(0f, 0.3f, 18f) - targetPos);
        }
        else if (phase is BowlingGame.Phase.Aiming or BowlingGame.Phase.Throwing or BowlingGame.Phase.Finished)
        {
            targetPos = AimPosition(aimX);
            targetRot = Quaternion.Euler(aimCameraPitch, 0f, 0f);
        }
        else
        {
            float z = Mathf.Min(ballPos.z - followDistance, followMaxZ);
            targetPos = new Vector3(ballPos.x * 0.5f, followHeight, z);
            targetRot = Quaternion.Euler(followPitch, 0f, 0f);
        }

        // Unscaled time, so the camera stays smooth during slow motion.
        float k = 1f - Mathf.Exp(-6f * unscaledDeltaTime);
        Vector3 pos = Vector3.Lerp(cam.transform.position - lastShake, targetPos, k);
        lastShake = shakeOffset;
        cam.transform.SetPositionAndRotation(pos + lastShake, Quaternion.Slerp(cam.transform.rotation, targetRot, k));
    }

    static LineRenderer MakeAimLine(Material mat)
    {
        var line = new GameObject("Aim Guide").AddComponent<LineRenderer>();
        line.sharedMaterial = mat;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = 0.025f;
        line.colorGradient = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(new Color(0.3f, 1f, 1f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.9f), 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) },
        };
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.enabled = false;
        return line;
    }

    public void UpdateAimLine(float aimX, bool show)
    {
        aimLine.enabled = show;
        if (!show) return;
        aimLine.SetPosition(0, new Vector3(aimX, 0.004f, 0.3f));
        aimLine.SetPosition(1, new Vector3(aimX, 0.004f, 14f));
    }
}
