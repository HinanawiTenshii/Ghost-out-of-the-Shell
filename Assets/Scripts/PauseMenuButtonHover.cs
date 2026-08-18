using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PauseMenuButtonHover :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private Image targetImage;
    private Color normalColor;
    private Color hoverColor;
    private bool configured;
    private bool pointerInside;
    private const float FadeSpeed = 14f;

    public void Configure(Image image, Color normal, Color hover)
    {
        targetImage = image;
        normalColor = normal;
        hoverColor = hover;
        configured = true;
        targetImage.color = normalColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
    }

    private void Update()
    {
        if (!configured || targetImage == null)
            return;

        Color targetColor = pointerInside ? hoverColor : normalColor;
        float blend = 1f - Mathf.Exp(-FadeSpeed * Time.unscaledDeltaTime);
        targetImage.color = Color.Lerp(targetImage.color, targetColor, blend);
    }

    private void OnDisable()
    {
        pointerInside = false;
        RestoreNormalColor();
    }

    private void RestoreNormalColor()
    {
        if (configured && targetImage != null)
        {
            targetImage.color = normalColor;
        }
    }
}
