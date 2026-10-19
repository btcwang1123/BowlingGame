using UnityEngine;
using UnityEngine.InputSystem;
using InputGyroscope = UnityEngine.InputSystem.Gyroscope;

// Reads the phone's gyroscope (and gravity, for aim) while the player holds their thumb on the
// screen, and turns the swing into throw numbers. In the Editor, the sensors come from a phone
// running Unity Remote 5; on a PC without sensors, motion controls are unavailable.
public class MotionInput : MonoBehaviour
{
    public static bool Available
    {
        get
        {
            EnableSensors();
            return InputGyroscope.current != null && InputGyroscope.current.enabled;
        }
    }

    readonly SwingDetector detector = new();
    bool recording;
    Vector3 gravityAtGrip;
    float peakRate;
    float gripTime;
    float lowSince;   // when the swing rate dropped (and stayed) low after its peak; -1 = not low now
    float releasePeak; // how fast the swing must get before a slow-down counts as its follow-through
    (int phases, bool lastActive) progress; // big swing phases so far (backswing, forward swing)

    public bool Recording => recording;
    public bool Swinging => recording && detector.CurrentRate > 2f;

    // For the "ready, then swing" flow (the player presses OK, then swings freely without holding
    // the screen): true once a full forward swing has clearly happened and the arm has slowed into
    // the follow-through, so the game can release the ball without waiting for a thumb-up.
    // The low-rate state must be *sustained*, and come after both a backswing and a forward swing:
    // players often pause at the top of the backswing, and that pause is not the end of the swing.
    public bool AutoReleaseReady =>
        recording && detector.Count >= 10
        && Time.unscaledTime - gripTime >= 0.4f           // ignore the moment just after OK
        && progress.phases >= 2
        && lowSince >= 0f && Time.unscaledTime - lowSince >= 0.1f;

    // What the player should do next, for the on-screen prompt.
    public enum SwingStage { SwingBack, SwingingBack, SwingForward }
    public SwingStage Stage => progress.phases == 0 ? SwingStage.SwingBack
        : progress.phases == 1 && progress.lastActive ? SwingStage.SwingingBack
        : SwingStage.SwingForward;

    static void EnableSensors()
    {
        foreach (InputDevice d in new InputDevice[] { InputGyroscope.current, GravitySensor.current, AttitudeSensor.current, Accelerometer.current })
        {
            if (d == null || d.enabled) continue;
            InputSystem.EnableDevice(d);
            if (d is Sensor sensor)
            {
                try { sensor.samplingFrequency = 100f; } catch { /* not supported on every platform */ }
            }
        }
    }

    void OnEnable() => EnableSensors();

    // Start recording the swing. calibrating: the player is teaching the game their natural swing, so
    // any real swing counts, however gentle; afterwards the bar is set from their calibrated speed.
    public void Grip(bool calibrating = false)
    {
        EnableSensors();
        detector.Begin();
        float reference = GameSettings.Calibrated ? GameSettings.SwingReference : 8f;
        if (calibrating)
        {
            detector.MinForwardRate = 1f;
            detector.MinBackswingRate = 0.8f;
            detector.MinForwardAngle = 0.5f;
            releasePeak = 2.5f;
        }
        else
        {
            detector.MinForwardRate = Mathf.Min(SwingDetector.DefaultMinForwardRate, reference * 0.35f);
            detector.MinBackswingRate = Mathf.Min(1.5f, reference * 0.25f);
            detector.MinForwardAngle = 0.7f;
            releasePeak = Mathf.Min(4.5f, reference * 0.55f);
        }
        recording = true;
        peakRate = 0f;
        lowSince = -1f;
        progress = (0, false);
        gripTime = Time.unscaledTime;
        gravityAtGrip = ReadGravity();
    }

    // The swing has finished (AutoReleaseReady): the ball leaves the hand. The arm has already slowed
    // into the follow-through by now, so the swing is read at the peak of the forward swing.
    public SwingResult Release()
    {
        recording = false;
        return detector.Evaluate(releaseAtPeak: true);
    }

    public void Cancel() => recording = false;

    void Update()
    {
        if (!recording || InputGyroscope.current == null) return;
        var g = ReadGravity();
        detector.Add(new MotionSample
        {
            t = Time.unscaledTime,
            gyro = InputGyroscope.current.angularVelocity.ReadValue(),
            gravity = g.sqrMagnitude > 1e-4f ? g : gravityAtGrip,
        });
        float rate = detector.CurrentRate;
        peakRate = Mathf.Max(peakRate, rate);
        progress = detector.Progress(releasePeak);
        // Track a sustained slow-down after a real swing (peak high enough, now well below it).
        if (peakRate > releasePeak && rate < peakRate * 0.3f) { if (lowSince < 0f) lowSince = Time.unscaledTime; }
        else lowSince = -1f;
    }

    static Vector3 ReadGravity()
    {
        if (GravitySensor.current != null && GravitySensor.current.enabled) return GravitySensor.current.gravity.ReadValue();
        // Fallback: while the phone is still, the accelerometer reads gravity.
        if (Accelerometer.current != null && Accelerometer.current.enabled) return Accelerometer.current.acceleration.ReadValue();
        return Vector3.zero;
    }

    // Turn a swing into ball numbers, using the player's calibration and settings.
    public static (float speed, float angle, float hook) ToThrow(SwingResult r)
    {
        float hand = GameSettings.LeftHanded ? -1f : 1f;
        float speed = 8.5f * r.peakRate / GameSettings.SwingReference * GameSettings.MotionSensitivity;
        // Aim, then subtract the player's left/right trim so a consistent drift can be dialled out.
        float angle = r.yawDeg * 0.08f * (GameSettings.FlipAim ? -1f : 1f) - GameSettings.AimOffsetDeg;
        float flip = GameSettings.FlipSpin ? -1f : 1f;
        float hook;
        if (GameSettings.SpinCalibrated)
        {
            // Calibrated on the lane: the player's own straight / left / right wrist rolls set the
            // zero point, the dead zone and the direction (the scale already carries the sign).
            float twist = r.twist - GameSettings.TwistBias;
            hook = Mathf.Abs(twist) < GameSettings.SpinDeadZone ? 0f : twist * GameSettings.SpinScale * flip;
        }
        else
        {
            float twist = Mathf.Abs(r.twist) < 1f ? 0f : r.twist;
            hook = twist * 0.03f * hand * flip;
        }
        return (Mathf.Clamp(speed, 3.5f, 11f), Mathf.Clamp(angle, -2.5f, 2.5f), Mathf.Clamp(hook, -0.15f, 0.15f));
    }
}
