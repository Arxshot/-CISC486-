using System;
using UnityEngine;
using UnityEngine.EventSystems;

// Runs a callback when its UI element gains focus (mouse hover, keyboard or gamepad).
public class SelectHint : MonoBehaviour, ISelectHandler
{
    public Action OnSelected;

    public void OnSelect(BaseEventData eventData) => OnSelected?.Invoke();

    public static void Attach(Component target, Action onSelected) => target.gameObject.AddComponent<SelectHint>().OnSelected = onSelected;
}
