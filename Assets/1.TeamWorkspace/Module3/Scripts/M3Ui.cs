using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Building blocks for Module 3's in-headset UI: world-space canvases that the XR ray and poke
/// interactors can press, text, panels and buttons, and the localization lookup they all use.
///
/// Built in code like the rest of the module's UI (M3Hud, Module2HUD), so there is no prefab or
/// scene YAML to merge.
/// </summary>
public static class M3Ui
{
    public static readonly Color PanelColour = new Color(0.05f, 0.06f, 0.08f, 0.92f);
    public static readonly Color ButtonColour = new Color(0.20f, 0.42f, 0.33f, 1f);
    public static readonly Color ButtonDisabled = new Color(0.25f, 0.27f, 0.30f, 1f);
    public static readonly Color Muted = new Color(0.68f, 0.72f, 0.77f, 1f);
    public static readonly Color Text = new Color(0.95f, 0.95f, 0.95f, 1f);
    public static readonly Color Accent = new Color(0.90f, 0.70f, 0.36f, 1f);

    /// <summary>The Lao-capable font the HUD uses; null falls back to the TMP default.</summary>
    public static TMP_FontAsset Font;

    public static string L(string key, params object[] args)
    {
        if (string.IsNullOrEmpty(key)) return "";
        if (LocalizationManager.Instance == null) return key;
        return args == null || args.Length == 0
            ? LocalizationManager.Instance.GetLocalizedValue(key)
            : LocalizationManager.Instance.GetLocalizedValue(key, args);
    }

    /// <summary>
    /// A world-space canvas <paramref name="widthMetres"/> wide, laid out in UI units at
    /// <paramref name="unitsPerMetre"/>, pressable by XR rays and pokes.
    /// </summary>
    public static RectTransform WorldCanvas(string name, Transform parent, Vector2 sizeUnits, float unitsPerMetre = 1000f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 30;   // above the head-following HUD (20): the nearer thing draws on top
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
        go.AddComponent<TrackedDeviceGraphicRaycaster>();
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = sizeUnits;
        rt.localScale = Vector3.one / unitsPerMetre;
        if (Camera.main != null) canvas.worldCamera = Camera.main;
        return rt;
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                                     Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    public static Image Panel(Transform parent, string name, Color colour)
    {
        var rt = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = colour;
        return img;
    }

    public static TextMeshProUGUI Label(Transform parent, string name, float size, Color colour,
                                        TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
    {
        var rt = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (Font != null) t.font = Font;
        t.fontSize = size;
        t.color = colour;
        t.alignment = align;
        t.richText = true;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Adds a LayoutElement asking for a fixed or preferred height.</summary>
    public static T Height<T>(T c, float height) where T : Component
    {
        var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        return c;
    }

    public static Button Button(Transform parent, string label, float height, UnityAction onClick,
                                bool enabled = true, float fontSize = 30f)
    {
        var rt = Rect("Button: " + label, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = enabled ? ButtonColour : ButtonDisabled;
        var button = rt.gameObject.AddComponent<Button>();
        button.interactable = enabled;
        var colours = button.colors;
        colours.normalColor = Color.white;
        colours.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colours.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colours.disabledColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colours.colorMultiplier = 1.2f;
        button.colors = colours;
        if (onClick != null) button.onClick.AddListener(onClick);
        var text = Label(rt, "Text", fontSize, enabled ? Text : Muted, TextAlignmentOptions.Center);
        text.rectTransform.offsetMin = new Vector2(16f, 4f);
        text.rectTransform.offsetMax = new Vector2(-16f, -4f);
        text.text = label;
        Height(button, height);
        return button;
    }

    /// <summary>A vertical stack that sizes its children from their LayoutElements.</summary>
    public static RectTransform Stack(Transform parent, string name, float spacing, RectOffset padding)
    {
        var rt = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.padding = padding;
        v.childControlHeight = true;
        v.childControlWidth = true;
        v.childForceExpandHeight = false;
        v.childForceExpandWidth = true;
        return rt;
    }

    /// <summary>A text row inside a stack, sized to its content.</summary>
    public static TextMeshProUGUI Line(Transform stack, string text, float size, Color colour)
    {
        // No ContentSizeFitter: TextMeshPro is its own layout element, and a fitter on a child of a
        // layout group fights the group over the height (rows overlapped).
        var t = Label(stack, "Line", size, colour);
        t.text = text;
        return t;
    }

    public static void Clear(Transform t)
    {
        // Detach first: Destroy is deferred, and a layout group would otherwise still stack the old
        // rows under the new ones for a frame.
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var child = t.GetChild(i);
            child.SetParent(null, false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }
}
