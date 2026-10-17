using UnityEngine;

// Phone vibration, respecting the Vibration setting. Does nothing on desktop.
public static class Haptics
{
    public static void Buzz()
    {
#if UNITY_IOS || UNITY_ANDROID
        if (GameSettings.Vibration) Handheld.Vibrate();
#endif
    }
}
