using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Builds every piece of Apex Arsenal UI in code: rounded panels with ink outlines and drop shadows,
// chunky buttons, sliders and outlined labels. No image assets needed.
public static class UIFactory
{
    public static TMP_FontAsset Font;

    public static Canvas CreateCanvas(string name, int sortingOrder = 0)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static EventSystem EnsureEventSystem()
    {
        var existing = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
        if (existing != null)
            return existing;
        var go = new GameObject("EventSystem", typeof(EventSystem));
        var module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
        return go.GetComponent<EventSystem>();
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    public static RectTransform Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        return rect;
    }

    public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static Image Image(string name, Transform parent, Color color, Sprite sprite = null)
    {
        var rect = Rect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
        }
        return image;
    }

    // Full-screen tinted backdrop that blocks clicks to whatever is behind it.
    public static Image Dim(Transform parent, float alpha = 0.55f)
    {
        var dim = Image("Dim", parent, LabPalette.WithAlpha(LabPalette.Ink, alpha));
        Stretch(dim.rectTransform);
        return dim;
    }

    // A cartoon card: ink drop shadow, ink outline, coloured body. Returns the body to fill with content.
    public static RectTransform Panel(string name, Transform parent, Vector2 size, Color body, int radius = 28, float outline = 6f)
    {
        var root = Rect(name, parent);
        root.sizeDelta = size;
        var shadow = Image("Shadow", root, LabPalette.WithAlpha(LabPalette.Ink, 0.45f), RoundedSprite.Get(radius));
        Stretch(shadow.rectTransform);
        shadow.rectTransform.anchoredPosition = new Vector2(8f, -10f);
        var border = Image("Outline", root, LabPalette.Ink, RoundedSprite.Get(radius));
        Stretch(border.rectTransform);
        var fill = Image("Body", root, body, RoundedSprite.Get(Mathf.Max(4, radius - (int)outline)));
        Stretch(fill.rectTransform, outline);
        return root;
    }

    public static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, float outline = 0.18f)
    {
        var rect = Rect(name, parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (Font != null)
            label.font = Font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.fontStyle = FontStyles.Bold;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        if (outline > 0f)
        {
            label.outlineWidth = outline;
            label.outlineColor = LabPalette.Ink;
        }
        return label;
    }

    public static CartoonButton Button(string name, Transform parent, string text, Color accent, Action onClick, Vector2? size = null, float fontSize = 46f)
    {
        Vector2 dimensions = size ?? new Vector2(520f, 104f);
        var root = Rect(name, parent);
        root.sizeDelta = dimensions;
        var layout = root.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = dimensions.x;
        layout.preferredHeight = dimensions.y;

        var shadow = Image("Shadow", root, LabPalette.WithAlpha(LabPalette.Ink, 0.5f), RoundedSprite.Get(30));
        Stretch(shadow.rectTransform);
        shadow.rectTransform.anchoredPosition = new Vector2(0f, -10f);
        shadow.raycastTarget = false;
        var border = Image("Outline", root, LabPalette.Ink, RoundedSprite.Get(30));
        Stretch(border.rectTransform);
        var fill = Image("Fill", root, LabPalette.PanelWhite, RoundedSprite.Get(24));
        Stretch(fill.rectTransform, 6f);
        fill.raycastTarget = false;
        var stripe = Image("Accent", fill.transform, accent, RoundedSprite.Get(10));
        Place(stripe.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(18f, dimensions.y - 40f));
        stripe.raycastTarget = false;

        var label = Label("Label", fill.transform, text, fontSize, LabPalette.Ink, TextAlignmentOptions.Center, 0f);
        Stretch(label.rectTransform);

        var button = root.gameObject.AddComponent<CartoonButton>();
        button.targetGraphic = border;
        button.transition = Selectable.Transition.None;
        button.Fill = fill;
        button.IdleColor = LabPalette.PanelWhite;
        button.HotColor = Color.Lerp(LabPalette.PanelWhite, accent, 0.55f);
        if (onClick != null)
            button.onClick.AddListener(() => onClick());
        return button;
    }

    // A button drawn with the original menu art (idle / hover / pressed sprites), sized to the sprite's
    // aspect at the given height. Keeps the cartoon squash, wobble and sounds of CartoonButton.
    public static CartoonButton ArtButton(string name, Transform parent, LabStyle.ButtonArt art, float height, Action onClick)
    {
        UnityEngine.Rect source = art.idle.rect;
        var dimensions = new Vector2(height * source.width / source.height, height);
        var root = Rect(name, parent);
        root.sizeDelta = dimensions;
        var layout = root.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = dimensions.x;
        layout.preferredHeight = dimensions.y;

        var image = root.gameObject.AddComponent<Image>();
        image.sprite = art.idle;
        image.preserveAspect = true;

        var button = root.gameObject.AddComponent<CartoonButton>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.Art = image;
        button.ArtSprites = art;
        if (onClick != null)
            button.onClick.AddListener(() => onClick());
        return button;
    }

    public static Slider Slider(string name, Transform parent, string caption, float value, Color accent, Action<float> onChanged)
    {
        var row = Rect(name, parent);
        row.sizeDelta = new Vector2(760f, 90f);
        var layout = row.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 760f;
        layout.preferredHeight = 90f;

        var title = Label("Caption", row, caption, 38f, LabPalette.Ink, TextAlignmentOptions.Left, 0f);
        Place(title.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(240f, 80f));

        var track = Rect("Slider", row);
        Place(track, new Vector2(1f, 0.5f), new Vector2(-110f, 0f), new Vector2(380f, 44f));
        var border = Image("Track Outline", track, LabPalette.Ink, RoundedSprite.Get(22));
        Stretch(border.rectTransform);
        var background = Image("Track", border.transform, LabPalette.PanelGrey, RoundedSprite.Get(16));
        Stretch(background.rectTransform, 6f);

        var fillArea = Rect("Fill Area", track);
        Stretch(fillArea, 6f);
        var fill = Image("Fill", fillArea, accent, RoundedSprite.Get(16));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.sizeDelta = Vector2.zero;

        var handleArea = Rect("Handle Area", track);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(20f, 0f);
        handleArea.offsetMax = new Vector2(-20f, 0f);
        var handleOutline = Image("Handle", handleArea, LabPalette.Ink, RoundedSprite.Circle());
        handleOutline.rectTransform.sizeDelta = new Vector2(62f, 62f);
        var handleFill = Image("Knob", handleOutline.transform, LabPalette.PanelWhite, RoundedSprite.Circle());
        Stretch(handleFill.rectTransform, 8f);
        handleFill.raycastTarget = false;

        var readout = Label("Value", row, "", 34f, LabPalette.Ink, TextAlignmentOptions.Right, 0f);
        Place(readout.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(100f, 80f));

        var slider = track.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handleOutline.rectTransform;
        slider.targetGraphic = handleFill;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        var colors = slider.colors;
        colors.highlightedColor = Color.Lerp(Color.white, accent, 0.6f);
        colors.selectedColor = accent;
        slider.colors = colors;
        slider.SetValueWithoutNotify(value);
        readout.text = Mathf.RoundToInt(value * 100f) + "%";
        float lastSound = 0f;
        slider.onValueChanged.AddListener(v =>
        {
            readout.text = Mathf.RoundToInt(v * 100f) + "%";
            onChanged?.Invoke(v);
            if (Time.unscaledTime - lastSound > 0.07f)
            {
                lastSound = Time.unscaledTime;
                AudioDirector.Play(Sfx.UiMove, 0.5f, 0.8f + v * 0.6f);
            }
        });
        return slider;
    }

    public static VerticalLayoutGroup Column(RectTransform rect, float spacing = 22f)
    {
        var column = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = spacing;
        column.childAlignment = TextAnchor.MiddleCenter;
        column.childControlWidth = false;
        column.childControlHeight = false;
        column.childForceExpandWidth = false;
        column.childForceExpandHeight = false;
        return column;
    }

    public static void Select(Selectable selectable)
    {
        if (selectable == null)
            return;
        var events = EventSystem.current;
        if (events == null)
            return;
        // Clear first so OnSelect fires (and the button animates) even if it was already selected.
        events.SetSelectedGameObject(null);
        events.SetSelectedGameObject(selectable.gameObject);
    }

    public static T Motion<T>(T component, UIMotion.Kind kind, float delay = 0f, float amount = 1f, float speed = 1f) where T : Component
    {
        var motion = component.gameObject.AddComponent<UIMotion>();
        motion.kind = kind;
        motion.delay = delay;
        motion.amount = amount;
        motion.speed = speed;
        return component;
    }
}
