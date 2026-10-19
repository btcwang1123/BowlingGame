using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Watches frame time; after a sustained run of slow frames it permanently drops Bloom quality
// and shadow distance so lower-end iPhones settle into something playable instead of visibly
// stuttering at full quality. Add to a persistent GameObject in the scene (Manual: assign
// `volume` to the scene's neon post-process Volume).
public class PerformanceGovernor : MonoBehaviour
{
    [SerializeField] private Volume volume;
    [SerializeField] private float targetFrameSeconds = 1f / 55f; // a little slack under 60 fps
    [SerializeField] private int consecutiveSlowFramesToDowngrade = 90; // ~1.5s at 60fps

    int consecutiveSlowFrames;
    bool downgraded;
    Bloom bloom;

    void Awake()
    {
        if (volume == null) volume = FindAnyObjectByType<Volume>();
        if (volume != null && volume.profile != null) volume.profile.TryGet(out bloom);
    }

    void Update()
    {
        if (downgraded) return;

        consecutiveSlowFrames = Time.unscaledDeltaTime > targetFrameSeconds ? consecutiveSlowFrames + 1 : 0;
        if (consecutiveSlowFrames >= consecutiveSlowFramesToDowngrade) Downgrade();
    }

    void Downgrade()
    {
        downgraded = true;

        if (bloom != null)
        {
            bloom.highQualityFiltering.value = false;
            bloom.maxIterations.value = Mathf.Min(bloom.maxIterations.value, 3);
        }

        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            urp.shadowDistance = Mathf.Min(urp.shadowDistance, 15f);
        QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Low;

        Debug.Log("[PerformanceGovernor] Sustained slow frames detected; downgraded bloom and shadows.");
    }
}
