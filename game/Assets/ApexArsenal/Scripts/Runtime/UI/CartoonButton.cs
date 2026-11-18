using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A chunky cartoon button: it squashes and grows on hover/select, flashes its accent colour,
// and blips. Animations use unscaled time so they keep working while the game is paused.
public class CartoonButton : Button
{
    public Image Fill;
    public Color IdleColor = Color.white;
    public Color HotColor = Color.white;
    public bool PlaySounds = true;

    // Optional sprite art (the original menu buttons): swapped per state on Art.
    public Image Art;
    public LabStyle.ButtonArt ArtSprites;

    private Vector3 targetScale = Vector3.one;
    private float wobble;

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        bool hot = state == SelectionState.Highlighted || state == SelectionState.Selected;
        bool pressed = state == SelectionState.Pressed;
        targetScale = pressed ? new Vector3(1.08f, 0.9f, 1f) : hot ? Vector3.one * 1.07f : Vector3.one;
        if (Fill != null)
            Fill.color = state == SelectionState.Disabled ? LabPalette.PanelGrey : hot || pressed ? HotColor : IdleColor;
        if (Art != null && ArtSprites != null && ArtSprites.IsValid)
        {
            Sprite sprite = pressed ? ArtSprites.pressed : hot ? ArtSprites.hover : null;
            Art.sprite = sprite != null ? sprite : ArtSprites.idle;
            Art.color = state == SelectionState.Disabled ? new Color(0.6f, 0.6f, 0.6f, 1f) : Color.white;
        }
        if (instant)
            transform.localScale = targetScale;
    }

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        wobble = 1f;
        if (PlaySounds && Application.isPlaying)
            AudioDirector.Play(Sfx.UiMove, 0.6f);
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        // Hovering with the mouse also moves keyboard/gamepad focus, so there's only ever one "hot" button.
        if (IsInteractable() && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (PlaySounds && IsInteractable())
            AudioDirector.Play(Sfx.UiClick, 0.8f);
        base.OnPointerClick(eventData);
    }

    public override void OnSubmit(BaseEventData eventData)
    {
        if (PlaySounds && IsInteractable())
            AudioDirector.Play(Sfx.UiClick, 0.8f);
        targetScale = new Vector3(1.1f, 0.88f, 1f);
        base.OnSubmit(eventData);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        wobble = Mathf.Max(0f, wobble - dt * 3f);
        float squash = Mathf.Sin(Time.unscaledTime * 30f) * wobble * 0.05f;
        var goal = targetScale + new Vector3(squash, -squash, 0f);
        transform.localScale = Vector3.Lerp(transform.localScale, goal, 1f - Mathf.Exp(-dt * 18f));
    }
}
