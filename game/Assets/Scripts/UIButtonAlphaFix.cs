using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UIButtonAlphaFix : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float alphaThreshold = 0.5f;

    void Start()
    {
        Image buttonImage = GetComponent<Image>();
        if (buttonImage != null)
        {
            // Tells Unity to ignore clicks on pixels with less than 50% opacity
            buttonImage.alphaHitTestMinimumThreshold = alphaThreshold;
        }
    }
}