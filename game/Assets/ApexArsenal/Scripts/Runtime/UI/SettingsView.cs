using System;
using UnityEngine;
using UnityEngine.UI;

// Settings card shared by the main menu and the pause overlay: Master / Music / SFX volume and fullscreen.
public class SettingsView : MonoBehaviour
{
    private Selectable first;
    private Action onBack;

    public static SettingsView Create(Transform parent, Action onBack)
    {
        var panel = UIFactory.Panel("Settings", parent, new Vector2(900f, 720f), LabPalette.PanelWhite);
        var view = panel.gameObject.AddComponent<SettingsView>();
        view.onBack = onBack;
        UIFactory.Motion(panel, UIMotion.Kind.PopIn);

        var header = UIFactory.Image("Header", panel, LabPalette.NeonBlue, RoundedSprite.Get(20));
        UIFactory.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 34f), new Vector2(460f, 96f));
        var title = UIFactory.Label("Title", header.transform, "SETTINGS", 58f, Color.white);
        UIFactory.Stretch(title.rectTransform);

        var column = UIFactory.Rect("Rows", panel);
        UIFactory.Place(column, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(780f, 560f));
        UIFactory.Column(column, 26f);

        view.first = UIFactory.Slider("Master", column, "MASTER", VolumeSettings.Master, LabPalette.NeonBlue, v => VolumeSettings.Master = v);
        UIFactory.Slider("Music", column, "MUSIC", VolumeSettings.Music, LabPalette.NeonPink, v => VolumeSettings.Music = v);
        UIFactory.Slider("SFX", column, "SFX", VolumeSettings.Sfx, LabPalette.Lime, v => VolumeSettings.Sfx = v);

        CartoonButton fullscreen = null;
        fullscreen = UIFactory.Button("Fullscreen", column, FullscreenText(), LabPalette.Violet, () =>
        {
            VolumeSettings.Fullscreen = !VolumeSettings.Fullscreen;
            fullscreen.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = FullscreenText();
        }, new Vector2(560f, 96f), 40f);
        UIFactory.Button("Back", column, "BACK", LabPalette.NeonPink, () => view.Back(), new Vector2(560f, 96f), 40f);
        return view;
    }

    private static string FullscreenText() => VolumeSettings.Fullscreen ? "FULLSCREEN: ON" : "FULLSCREEN: OFF";

    public void Open()
    {
        gameObject.SetActive(true);
        UIFactory.Select(first);
    }

    public void Back()
    {
        AudioDirector.Play(Sfx.UiBack, 0.7f);
        gameObject.SetActive(false);
        onBack?.Invoke();
    }
}
