using System;
using UnityEngine;

// Player-facing audio and display settings, saved in PlayerPrefs.
public static class VolumeSettings
{
    private const string MasterKey = "apex.volume.master";
    private const string MusicKey = "apex.volume.music";
    private const string SfxKey = "apex.volume.sfx";
    private const string FullscreenKey = "apex.display.fullscreen";

    public static event Action Changed;

    public static float Master
    {
        get => PlayerPrefs.GetFloat(MasterKey, 0.8f);
        set => Set(MasterKey, value);
    }

    public static float Music
    {
        get => PlayerPrefs.GetFloat(MusicKey, 0.55f);
        set => Set(MusicKey, value);
    }

    public static float Sfx
    {
        get => PlayerPrefs.GetFloat(SfxKey, 0.8f);
        set => Set(SfxKey, value);
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        set
        {
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Screen.fullScreen = value;
            Changed?.Invoke();
        }
    }

    // Slider position (0-1) to linear gain. Squaring follows how loud things sound, so the
    // bottom half of the slider isn't wasted on near-silence.
    public static float ToGain(float slider)
    {
        slider = Mathf.Clamp01(slider);
        return slider * slider;
    }

    public static float MusicGain => ToGain(Music);
    public static float SfxGain => ToGain(Sfx);

    private static void Set(string key, float value)
    {
        PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
