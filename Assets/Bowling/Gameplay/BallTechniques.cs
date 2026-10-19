using UnityEngine;

// How the player holds and releases the ball. Neither a swipe nor a phone swing can tell a
// spinner's top-of-the-ball grip or a two-handed release apart from a normal throw, so the player
// picks the technique before the throw (the "technique" button while aiming); the gesture still
// decides speed, line and how much it curves. One-handed throws are also sorted into what they
// turned out to be: straight, hook, backup (curving toward the bowling hand) or a big curve
// (sent out wide and brought back with a lot of hook).
public enum BallTechnique { OneHanded, Spinner, TwoHanded }

public enum ThrowStyle { Straight, Hook, Backup, BigCurve, Spinner, TwoHanded }

public static class BallTechniques
{
    public static readonly BallTechnique[] All = { BallTechnique.OneHanded, BallTechnique.Spinner, BallTechnique.TwoHanded };

    public static string Name(BallTechnique t) => Loc.T("tech_" + Key(t));
    public static string Description(BallTechnique t) => Loc.T("tech_" + Key(t) + "_desc");
    public static string StyleName(ThrowStyle s) => Loc.T("style_" + s.ToString().ToLowerInvariant());

    static string Key(BallTechnique t) => t switch
    {
        BallTechnique.Spinner => "spinner",
        BallTechnique.TwoHanded => "twohand",
        _ => "onehand",
    };

    public static BallTechnique Next(BallTechnique t) => All[((int)t + 1) % All.Length];

    // What a technique does to a throw.
    public struct Shot
    {
        public float speed, angle, hook;
        public float skidScale;     // × the ball's skid length (when it starts to turn)
        public float backendAdd;    // + the ball's backend sharpness
        public float topSpin;       // rad/s around the vertical axis (spinner)
        public float pinScatter;    // 0–1: extra sideways kick the spinning ball gives the pins
        public float massScale;     // × ball mass (two-handed carries more energy into the pins)
        public float oilFactor;     // × how much the ball's oil takes off the hook
        public ThrowStyle style;
    }

    public const float MaxHook = 0.15f, MaxTwoHandedHook = 0.21f;

    // hook > 0 curves right; a right-hander's natural hook curves left. angle > 0 aims right.
    public static Shot Apply(BallTechnique t, float speed, float angle, float hook, bool leftHanded)
    {
        var s = new Shot { speed = speed, angle = angle, hook = hook, skidScale = 1f, massScale = 1f, oilFactor = 1f };
        switch (t)
        {
            case BallTechnique.Spinner:
                // Spun like a top: barely hooks, skids long, the oil hardly matters, and it sends
                // the pins spinning sideways — accurate for spares, chaotic on a full rack.
                s.speed = speed * 0.93f;
                s.hook = hook * 0.3f;
                s.skidScale = 1.6f;
                s.topSpin = (leftHanded ? -1f : 1f) * 32f;
                s.pinScatter = 1f;
                s.oilFactor = 0.2f;
                s.style = ThrowStyle.Spinner;
                break;

            case BallTechnique.TwoHanded:
                // Lots of revs: a bigger, later, sharper hook and more energy into the pins.
                s.speed = speed * 1.04f;
                s.hook = Mathf.Clamp(hook * 1.6f, -MaxTwoHandedHook, MaxTwoHandedHook);
                s.backendAdd = 0.35f;
                s.massScale = 1.08f;
                s.style = ThrowStyle.TwoHanded;
                break;

            default:
                s.style = Classify(angle, hook, leftHanded);
                if (s.style == ThrowStyle.Backup) s.hook = hook * 0.8f;   // a backup has fewer revs
                else if (s.style == ThrowStyle.BigCurve)
                {
                    // Out wide, then a long, early arc back: turns sooner and more, a little slower.
                    s.hook = Mathf.Clamp(hook * 1.3f, -MaxHook * 1.25f, MaxHook * 1.25f);
                    s.skidScale = 0.55f;
                    s.backendAdd = -0.3f;
                    s.speed = speed * 0.95f;
                }
                break;
        }
        return s;
    }

    // One-handed throws, sorted by what the gesture did.
    public static ThrowStyle Classify(float angle, float hook, bool leftHanded)
    {
        if (Mathf.Abs(hook) < 0.01f) return ThrowStyle.Straight;
        bool backup = leftHanded ? hook < 0f : hook > 0f;
        if (backup) return ThrowStyle.Backup;
        bool outward = angle * hook < 0f && Mathf.Abs(angle) >= 0.6f;
        return outward && Mathf.Abs(hook) >= 0.09f ? ThrowStyle.BigCurve : ThrowStyle.Hook;
    }
}
