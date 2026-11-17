using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The Apex Arsenal front end: title, mode select, chamber select and settings, all built in code
// over a live 3D chamber backdrop.
public class MainMenuScreen : MonoBehaviour
{
    private RectTransform mainPage;
    private RectTransform chamberPage;
    private SettingsView settings;
    private Selectable mainFirst;
    private Selectable chamberFirst;
    private TextMeshProUGUI hint;
    private TextMeshProUGUI modeChip;
    private Image modeChipBody;
    private TextMeshProUGUI randomSeedLabel;
    private InputAction back;
    private GameMode mode;
    private int randomSeed;
    private Action<ArenaPresetId, int> preview;
    private RectTransform menuColumn;

    // Menu column layout, top to bottom (canvas units at 1920x1080): logo, tagline, then four 104-high buttons.
    private const float ColumnTop = 30f;
    private const float ColumnBottomReserve = 130f; // hint + controls footer
    private static readonly Vector2 LogoSize = new(560f, 237f); // GameTitle.png is 1928x816
    private const float TaglineTop = 249f;
    private const float ButtonsTop = 331f;
    private const float ColumnHeight = ButtonsTop + 4 * 104f + 3 * 24f + 12f; // + room for the hover grow

    public static MainMenuScreen Create(Action<ArenaPresetId, int> previewChamber)
    {
        UIFactory.EnsureEventSystem();
        var canvas = UIFactory.CreateCanvas("Main Menu");
        var screen = canvas.gameObject.AddComponent<MainMenuScreen>();
        screen.preview = previewChamber;
        screen.Build(canvas.transform);
        return screen;
    }

    private void Build(Transform root)
    {
        // Dark wash on the left so the neon logo and buttons read clearly over the 3D chamber.
        var wash = UIFactory.Image("Wash", root, LabPalette.WithAlpha(LabPalette.Ink, 0.62f));
        wash.rectTransform.anchorMin = Vector2.zero;
        wash.rectTransform.anchorMax = new Vector2(0.42f, 1f);
        wash.rectTransform.offsetMin = wash.rectTransform.offsetMax = Vector2.zero;
        var washEdge = UIFactory.Image("Wash Edge", root, LabPalette.NeonBlue);
        washEdge.rectTransform.anchorMin = new Vector2(0.42f, 0f);
        washEdge.rectTransform.anchorMax = new Vector2(0.42f, 1f);
        washEdge.rectTransform.sizeDelta = new Vector2(10f, 0f);

        BuildMenuColumn(root);
        BuildChamberPage(root);

        settings = SettingsView.Create(root, ShowMain);
        UIFactory.Place((RectTransform)settings.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(900f, 720f));
        settings.gameObject.SetActive(false);

        var footer = UIFactory.Label("Controls", root, "MOVE  WASD / L-STICK      AIM  MOUSE / R-STICK      SHOOT  CLICK / RT      DASH  SHIFT / B      PAUSE  ESC / START", 24f, LabPalette.PanelWhite, TextAlignmentOptions.Center, 0.25f);
        UIFactory.Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1800f, 40f));

        back = new InputAction("Back", InputActionType.Button);
        back.AddBinding("<Keyboard>/escape");
        back.AddBinding("<Gamepad>/buttonEast");
        back.performed += _ => Back();
        back.Enable();

        ShowMain();
    }

    private void OnDestroy() => back?.Dispose();

    // Logo, tagline and buttons share one column anchored to the top-left, so they can never overlap.
    // If the screen is too short for the whole column (e.g. a wide Game view), the column shrinks to fit.
    private void BuildMenuColumn(Transform root)
    {
        menuColumn = UIFactory.Rect("Menu Column", root);
        UIFactory.Place(menuColumn, new Vector2(0f, 1f), new Vector2(110f, -ColumnTop), new Vector2(560f, ColumnHeight));
        BuildTitle(menuColumn);
        BuildMainPage(menuColumn);

        hint = UIFactory.Label("Hint", root, "", 30f, LabPalette.PanelWhite, TextAlignmentOptions.TopLeft, 0.2f);
        UIFactory.Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(112f, 70f), new Vector2(680f, 50f));
        hint.textWrappingMode = TextWrappingModes.Normal;
    }

    private void LateUpdate()
    {
        if (menuColumn == null)
            return;
        float available = ((RectTransform)transform).rect.height - ColumnTop - ColumnBottomReserve;
        float scale = Mathf.Clamp(available / ColumnHeight, 0.5f, 1f);
        menuColumn.localScale = new Vector3(scale, scale, 1f);
    }

    private void BuildTitle(RectTransform column)
    {
        // GameTitle.png, centred over the 520-wide buttons.
        Sprite logoSprite = FindLogo();
        if (logoSprite != null)
        {
            var logo = UIFactory.Image("Logo", column, Color.white, null);
            logo.sprite = logoSprite;
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            UIFactory.Place(logo.rectTransform, new Vector2(0f, 1f), new Vector2(-20f, 0f), new Vector2(LogoSize.x, LogoSize.y));
            UIFactory.Motion(logo, UIMotion.Kind.Bob, 0f, 0.6f, 0.8f);
            Debug.Log($"Apex Arsenal menu: logo '{logoSprite.name}' ({logoSprite.texture.width}x{logoSprite.texture.height}) drawn at {LogoSize.x}x{LogoSize.y} above the buttons.", logo);
        }
        else
        {
            Debug.LogWarning("Apex Arsenal menu: no GameTitle sprite found, showing the text title instead. Run Apex Arsenal > Build Scenes & Assets.");
            var title = UIFactory.Rect("Title", column);
            UIFactory.Place(title, new Vector2(0f, 1f), Vector2.zero, new Vector2(LogoSize.x, LogoSize.y));
            UIFactory.Motion(title, UIMotion.Kind.Bob, 0f, 0.6f, 0.8f);
            var shadow = UIFactory.Label("Shadow", title, "APEX\nARSENAL", 110f, LabPalette.NeonPink, TextAlignmentOptions.TopLeft, 0.2f);
            UIFactory.Stretch(shadow.rectTransform);
            shadow.rectTransform.anchoredPosition = new Vector2(8f, -8f);
            shadow.lineSpacing = -28f;
            var main = UIFactory.Label("Main", title, "APEX\nARSENAL", 110f, LabPalette.NeonBlue, TextAlignmentOptions.TopLeft, 0.2f);
            UIFactory.Stretch(main.rectTransform);
            main.lineSpacing = -28f;
        }

        var pill = UIFactory.Image("Tagline", column, LabPalette.NeonPink, RoundedSprite.Get(24));
        UIFactory.Place(pill.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -TaglineTop), new Vector2(520f, 52f));
        var tagline = UIFactory.Label("Text", pill.transform, "LASER-TAG TEST PROGRAM", 28f, Color.white, TextAlignmentOptions.Center, 0.2f);
        UIFactory.Stretch(tagline.rectTransform);
        tagline.characterSpacing = 6f;
    }

    private static Sprite FindLogo()
    {
        var style = LabStyle.Active;
        if (style != null && style.logo != null)
            return style.logo;
#if UNITY_EDITOR
        // Editor safety net: read the sprite straight from UI_Graphics if the style hasn't been rebuilt.
        foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/UI_Graphics/GameTitle.png"))
            if (asset is Sprite sprite)
                return sprite;
#endif
        return null;
    }

    private void BuildMainPage(RectTransform column)
    {
        mainPage = UIFactory.Rect("Main Page", column);
        UIFactory.Place(mainPage, new Vector2(0f, 1f), new Vector2(0f, -ButtonsTop), new Vector2(560f, ColumnHeight - ButtonsTop));
        UIFactory.Column(mainPage, 24f).childAlignment = TextAnchor.UpperLeft;

        var coop = UIFactory.Button("Coop", mainPage, "CO-OP", LabPalette.NeonBlue, () => ShowChambers(GameMode.Coop));
        var pvp = UIFactory.Button("PvP", mainPage, "PVP", LabPalette.NeonPink, () => ShowChambers(GameMode.PvP));
        var options = UIFactory.Button("Settings", mainPage, "SETTINGS", LabPalette.Lime, ShowSettings);
        var quit = UIFactory.Button("Quit", mainPage, "QUIT", LabPalette.Violet, SceneFlow.Quit);
        mainFirst = coop;

        SelectHint.Attach(coop, () => hint.text = "1-4 players vs. the lab's sentry turrets.");
        SelectHint.Attach(pvp, () => hint.text = "2-4 player free-for-all. Last one standing wins.");
        SelectHint.Attach(options, () => hint.text = "Volume and display.");
        SelectHint.Attach(quit, () => hint.text = "Leave the testing facility.");

        UIFactory.Motion(mainPage, UIMotion.Kind.PopIn);
    }

    private void BuildChamberPage(Transform root)
    {
        chamberPage = UIFactory.Rect("Chamber Page", root);
        UIFactory.Stretch(chamberPage);

        var panel = UIFactory.Panel("Card", chamberPage, new Vector2(1240f, 760f), LabPalette.PanelWhite);
        UIFactory.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1240f, 760f));
        UIFactory.Motion(panel, UIMotion.Kind.PopIn);

        var header = UIFactory.Label("Header", panel, "CHOOSE A TEST CHAMBER", 60f, LabPalette.Ink, TextAlignmentOptions.Left, 0f);
        UIFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(60f, -40f), new Vector2(900f, 80f));

        modeChipBody = UIFactory.Image("Mode", panel, LabPalette.NeonBlue, RoundedSprite.Get(22));
        UIFactory.Place(modeChipBody.rectTransform, new Vector2(1f, 1f), new Vector2(-60f, -48f), new Vector2(220f, 64f));
        modeChip = UIFactory.Label("Text", modeChipBody.transform, "CO-OP", 36f, Color.white);
        UIFactory.Stretch(modeChip.rectTransform);

        var grid = UIFactory.Rect("Chambers", panel);
        UIFactory.Place(grid, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1120f, 440f));
        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(540f, 200f);
        layout.spacing = new Vector2(40f, 40f);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 2;

        Color[] accents = { LabPalette.NeonBlue, LabPalette.NeonPink, LabPalette.Lime, LabPalette.Violet };
        for (int i = 0; i < ArenaPresets.All.Length; i++)
        {
            var preset = ArenaPresets.All[i];
            var card = UIFactory.Button(preset.Id.ToString(), grid, preset.Title, accents[i], () => Launch(preset.Id), new Vector2(540f, 200f), 36f);
            var label = card.GetComponentInChildren<TextMeshProUGUI>();
            label.rectTransform.offsetMin = new Vector2(60f, 70f);
            label.rectTransform.offsetMax = new Vector2(-20f, -20f);
            label.alignment = TextAlignmentOptions.Left;
            var tagline = UIFactory.Label("Tagline", card.Fill.transform, preset.Tagline, 26f, LabPalette.Ink, TextAlignmentOptions.TopLeft, 0f);
            UIFactory.Stretch(tagline.rectTransform);
            tagline.rectTransform.offsetMin = new Vector2(60f, 20f);
            tagline.rectTransform.offsetMax = new Vector2(-20f, -110f);
            tagline.textWrappingMode = TextWrappingModes.Normal;
            tagline.fontStyle = FontStyles.Normal;
            if (preset.Id == ArenaPresetId.Random)
                randomSeedLabel = tagline;
            var id = preset.Id;
            SelectHint.Attach(card, () => preview?.Invoke(id, id == ArenaPresetId.Random ? randomSeed : 0));
            if (i == 0)
                chamberFirst = card;
        }

        var backButton = UIFactory.Button("Back", panel, "BACK", LabPalette.PanelGrey, Back, new Vector2(320f, 90f), 38f);
        UIFactory.Place((RectTransform)backButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(320f, 90f));
    }

    private void ShowMain()
    {
        mainPage.gameObject.SetActive(true);
        chamberPage.gameObject.SetActive(false);
        UIFactory.Select(mainFirst);
    }

    private void ShowChambers(GameMode selected)
    {
        mode = selected;
        randomSeed = UnityEngine.Random.Range(1000, 99999);
        if (randomSeedLabel != null)
            randomSeedLabel.text = $"A fresh chamber, generated just for you.  SEED {randomSeed}";
        modeChip.text = mode == GameMode.Coop ? "CO-OP" : "PVP";
        modeChipBody.color = mode == GameMode.Coop ? LabPalette.NeonBlue : LabPalette.NeonPink;
        mainPage.gameObject.SetActive(false);
        chamberPage.gameObject.SetActive(true);
        UIFactory.Select(chamberFirst);
    }

    private void ShowSettings()
    {
        mainPage.gameObject.SetActive(false);
        settings.Open();
    }

    private void Back()
    {
        if (settings.gameObject.activeSelf)
            settings.Back();
        else if (chamberPage.gameObject.activeSelf)
        {
            AudioDirector.Play(Sfx.UiBack, 0.7f);
            ShowMain();
        }
    }

    private void Launch(ArenaPresetId preset)
    {
        AudioDirector.Play(Sfx.CountGo, 0.6f);
        SceneFlow.StartMatch(mode, preset, preset == ArenaPresetId.Random ? randomSeed : MatchSettings.Seed);
    }
}
