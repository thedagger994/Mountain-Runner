using UnityEngine;
using UnityEngine.UI;

// Shared builders for the code-created UI, so the HUD and the shop stay visually
// consistent without either owning the other's layout helpers.
public static class UIFactory
{
    public static readonly Color Accent = new Color(1f, 0.72f, 0.28f);

    public static Text MakeText(Transform parent, Font font, string name, string content,
                                int size, TextAnchor anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    public static Image MakePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static Button MakeButton(Transform parent, Font font, string label, Vector2 size,
                                    Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick,
                                    out Text labelText)
    {
        var go = new GameObject(label + " Button");
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = Color.white;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.16f);
        colors.highlightedColor = new Color(1f, 0.86f, 0.5f, 0.32f);
        colors.pressedColor = new Color(0.95f, 0.68f, 0.28f, 0.5f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.05f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        if (onClick != null) button.onClick.AddListener(onClick);

        labelText = MakeText(go.transform, font, "Text", label, 28, TextAnchor.MiddleCenter);
        Stretch(labelText.rectTransform);
        return button;
    }

    public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static void AddOutline(GameObject target)
    {
        var outline = target.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
        outline.effectDistance = new Vector2(2f, -2f);
    }
}
