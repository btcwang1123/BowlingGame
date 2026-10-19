using System.Collections.Generic;
using UnityEngine;

// Everything measured about one throw, for the post-throw analysis panel and the end-of-game charts.
public class ThrowRecord
{
    public const float PocketOffset = 0.065f;   // ball centre this far beside the head pin = the pocket
    public const float PocketTolerance = 0.04f;

    public int player, frame;
    public float speedKmh, angleDeg, hook;
    public bool wiped;            // ball was wiped right before this throw
    public float oil;             // how oily the ball was (0 clean – 1 slick)
    public bool leftHanded;
    public ThrowStyle style;      // technique and what the ball did (see BallTechniques)
    public readonly List<Vector2> path = new();   // (x, z) down the lane

    // Where the ball met the pins (filled when it crosses the head pin line).
    public bool reachedPins;
    public bool gutter;
    public float entryX, entryAngleDeg;

    // Filled when the pins have settled.
    public int knocked, standingBefore;
    public string mark = "";

    public float PocketX => leftHanded ? -PocketOffset : PocketOffset;
    public float PocketError => entryX - PocketX;   // + = right of the pocket
    public bool InPocket => reachedPins && Mathf.Abs(PocketError) <= PocketTolerance;

    public enum Shape { Straight, Left, Right }
    public Shape BallShape => hook > 0.01f ? Shape.Right : hook < -0.01f ? Shape.Left : Shape.Straight;

    public string ShapeName
    {
        get
        {
            if (style is ThrowStyle.Spinner or ThrowStyle.TwoHanded or ThrowStyle.BigCurve or ThrowStyle.Backup)
                return BallTechniques.StyleName(style);
            string name = BallShape switch { Shape.Right => Loc.T("shape_right"), Shape.Left => Loc.T("shape_left"), _ => Loc.T("shape_straight") };
            return Mathf.Abs(hook) >= 0.1f ? Loc.T("shape_strong") + name : name;
        }
    }

    // 0 none, 1 light, 2 medium, 3 strong.
    public int SpinLevel => Mathf.Abs(hook) < 0.01f ? 0 : Mathf.Abs(hook) < 0.06f ? 1 : Mathf.Abs(hook) < 0.11f ? 2 : 3;
}
