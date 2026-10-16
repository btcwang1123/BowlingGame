using UnityEngine;

// Materials and colours the game needs to build things at runtime (challenge obstacles, aim guide).
// Lives in the scene and is filled in by BowlingSceneBuilder, so the assets are included in builds.
public class ChallengeKit : MonoBehaviour
{
    public Material darkMat, cyanMat, magentaMat, goldMat, additiveMat;
    public PhysicsMaterial wallPhysics;
    [ColorUsage(false, true)] public Color neonCyan = new(0f, 2.2f, 2.6f);
    [ColorUsage(false, true)] public Color neonMagenta = new(2.6f, 0.3f, 2.2f);
    [ColorUsage(false, true)] public Color neonGold = new(3f, 2f, 0.4f);
}
