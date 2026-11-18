using System.Collections.Generic;
using UnityEngine;

// One persistent audio hub: music, 2D UI/announcer sounds, and volume settings applied everywhere,
// including the players' own AudioSources (only their volume is set; their scripts are untouched).
public class AudioDirector : MonoBehaviour
{
    private static AudioDirector instance;
    private AudioSource musicSource;
    private AudioSource sfxSource;
    private readonly List<(AudioSource source, float baseVolume)> worldSources = new();

    public static AudioDirector Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("Audio Director");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<AudioDirector>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.ignoreListenerPause = true;
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.ignoreListenerPause = true;
        VolumeSettings.Changed += Apply;
        Apply();
    }

    private void OnDestroy()
    {
        if (instance == this)
            VolumeSettings.Changed -= Apply;
    }

    public void PlayMusic(float pitch = 1f)
    {
        if (musicSource.clip == null)
            musicSource.clip = SynthAudio.Music;
        musicSource.pitch = pitch;
        if (!musicSource.isPlaying)
            musicSource.Play();
    }

    public void SetMusicPitch(float pitch) => musicSource.pitch = pitch;

    public static void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
    {
        var director = Instance;
        director.sfxSource.pitch = pitch;
        director.sfxSource.PlayOneShot(SynthAudio.Get(sfx), volume);
    }

    // Plays a positional sound in the arena, scaled by the SFX setting.
    public static void PlayAt(Sfx sfx, Vector3 position, float volume = 1f)
    {
        var go = new GameObject($"SFX {sfx}");
        go.transform.position = position;
        var source = go.AddComponent<AudioSource>();
        source.clip = SynthAudio.Get(sfx);
        source.spatialBlend = 0.6f;
        source.minDistance = 6f;
        source.volume = volume * VolumeSettings.SfxGain;
        source.Play();
        Destroy(go, source.clip.length + 0.1f);
    }

    // Brings an existing AudioSource (e.g. a player's gun) under the SFX volume slider.
    public void Register(AudioSource source)
    {
        if (source == null || source == musicSource || source == sfxSource)
            return;
        foreach (var entry in worldSources)
            if (entry.source == source)
                return;
        worldSources.Add((source, source.volume));
        source.volume *= VolumeSettings.SfxGain;
    }

    public void RegisterAll(GameObject root)
    {
        foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
            Register(source);
    }

    private void Apply()
    {
        AudioListener.volume = VolumeSettings.ToGain(VolumeSettings.Master);
        if (musicSource != null)
            musicSource.volume = VolumeSettings.MusicGain * 0.7f;
        if (sfxSource != null)
            sfxSource.volume = VolumeSettings.SfxGain;
        worldSources.RemoveAll(entry => entry.source == null);
        foreach (var (source, baseVolume) in worldSources)
            source.volume = baseVolume * VolumeSettings.SfxGain;
    }
}
