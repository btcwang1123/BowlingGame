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

    public bool Recording => recording;
    public bool Swinging => recording && detector.CurrentRate > 2f;

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

    // Thumb down: start recording the swing.
    public void Grip()
    {
        EnableSensors();
        detector.Begin();
        recording = true;
        gravityAtGrip = ReadGravity();
    }

    // Thumb up: the ball is released. Returns how the swing went.
    public SwingResult Release()
    {
        recording = false;
        return detector.Evaluate();
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
        float angle = r.yawDeg * 0.08f * (GameSettings.FlipAim ? -1f : 1f);
        float twist = Mathf.Abs(r.twist) < 1f ? 0f : r.twist;
        float hook = twist * 0.03f * hand * (GameSettings.FlipSpin ? -1f : 1f);
        return (Mathf.Clamp(speed, 3.5f, 11f), Mathf.Clamp(angle, -2.5f, 2.5f), Mathf.Clamp(hook, -0.15f, 0.15f));
    }
}
