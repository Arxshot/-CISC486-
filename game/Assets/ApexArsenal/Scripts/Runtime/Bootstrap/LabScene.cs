using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Shared scene dressing for the code-built scenes: style registration, lighting, camera and the
// post-processing (bloom makes the neon strips glow).
public static class LabScene
{
    public static void UseStyle(LabStyle style)
    {
        LabStyle.Active = style;
        UIFactory.Font = style != null ? style.font : null;
        DashTrail.Template = style != null ? style.glass : null;
    }

    public static void SetupLighting()
    {
        var sun = new GameObject("Lab Key Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.97f, 0.93f);
        sun.intensity = 1.25f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.55f;
        sun.transform.rotation = Quaternion.Euler(58f, -35f, 0f);
        RenderSettings.sun = sun;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.78f, 0.86f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.82f, 0.8f, 0.9f);
        RenderSettings.ambientGroundColor = new Color(0.35f, 0.33f, 0.4f);
        RenderSettings.fog = false;
    }

    public static Camera CreateCamera()
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var camera = go.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.09f, 0.1f, 0.13f);
        go.AddComponent<AudioListener>();
        var data = camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        var volumeObject = new GameObject("Lab Post Processing");
        var volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(1.1f);
        bloom.scatter.Override(0.65f);
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.22f);
        vignette.color.Override(new Color(0.08f, 0.05f, 0.15f));
        var color = profile.Add<ColorAdjustments>(true);
        color.saturation.Override(12f);
        color.contrast.Override(8f);
        volume.sharedProfile = profile;
        return camera;
    }
}
