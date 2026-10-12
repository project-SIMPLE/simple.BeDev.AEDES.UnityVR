using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Building blocks for Module 3's in-headset UI: world-space canvases that the XR ray and poke
/// interactors can press, text, panels and buttons, and the localization lookup they all use.
///
/// Built in code like the rest of the module's UI (M3Hud, Module2HUD), so there is no prefab or
/// scene YAML to merge.
///
/// Looks like Module 2's HUD, which Module 1's copies too, so the three modules read as one game:
/// its palette, its rounded dark panels, the full Lao font with kerning off, and the UI layer drawn
/// by an overlay camera so a house wall never cuts into the text.
/// </summary>
public static class M3Ui
{
    // Module2HUD's palette (panelColor, accentColor, mutedColor, warnColor, dangerColor). The panels
    // keep Module 3's higher opacity: they carry paragraphs, not a timer and a score.
    public static readonly Color PanelColour = new Color(0.04f, 0.05f, 0.07f, 0.92f);
    public static readonly Color ButtonColour = new Color(0.20f, 0.42f, 0.33f, 1f);
    public static readonly Color ButtonDisabled = new Color(0.25f, 0.27f, 0.30f, 1f);
    public static readonly Color Muted = new Color(0.75f, 0.78f, 0.82f, 1f);
    public static readonly Color Text = new Color(0.95f, 0.95f, 0.95f, 1f);
    public static readonly Color Accent = new Color(0.45f, 0.82f, 1f, 1f);
    public static readonly Color Warn = new Color(1f, 0.55f, 0.1f, 1f);
    public static readonly Color Danger = new Color(0.9f, 0.18f, 0.18f, 1f);

    // The same colours for rich text.
    public const string AccentHex = "#73D1FF";
    public const string MutedHex = "#BFC7D1";
    public const string WarnHex = "#FF8C1A";
    public const string DangerHex = "#E62E2E";

    /// <summary>The font Modules 1 and 2 use: the whole Lao block plus Basic Latin, built by LaoFontTool.</summary>
    public const string FontResource = "Fonts/Lao_SomVang Full SDF";

    /// <summary>The Lao-capable font the HUD uses; null falls back to the TMP default.</summary>
    public static TMP_FontAsset Font;

    /// <summary>
    /// The full Lao font carries a kerning pair that opens a visible gap in "ງງ" (ພະລັງງານ); Module 1
    /// turns kerning off for the same reason.
    /// </summary>
    public static void NoKerning(TMP_Text text)
    {
        var features = new System.Collections.Generic.List<UnityEngine.TextCore.OTL_FeatureTag>(text.fontFeatures);
        if (features.Remove(UnityEngine.TextCore.OTL_FeatureTag.kern)) text.fontFeatures = features;
    }

    private static Sprite rounded;

    /// <summary>Module2HUD's panel shape: a rounded rectangle, 9-sliced with 10 px corners.</summary>
    public static Sprite Rounded
    {
        get
        {
            if (rounded != null) return rounded;
            const int size = 32, ss = 4;
            const float r = 10f / size;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float u = (x + (sx + 0.5f) / ss) / size - 0.5f, v = (y + (sy + 0.5f) / ss) / size - 0.5f;
                            Vector2 q = new Vector2(Mathf.Abs(u), Mathf.Abs(v)) - new Vector2(0.5f - r, 0.5f - r);
                            float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
                            if (d < 0f) hits++;
                        }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * hits / (ss * ss)));
                }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(10, 10, 10, 10));
            rounded.name = "M3 Rounded Panel";
            rounded.hideFlags = HideFlags.HideAndDontSave;
            return rounded;
        }
    }

    /// <summary>Gives <paramref name="image"/> the rounded panel shape.</summary>
    public static Image Round(Image image)
    {
        image.sprite = Rounded;
        image.type = Image.Type.Sliced;
        return image;
    }

    /// <summary>
    /// Puts a world-space canvas on the UI layer, drawn by an overlay camera after the world, so it is
    /// never buried in a wall - Module2HUD's arrangement. Children made later through
    /// <see cref="Rect"/> take their parent's layer.
    /// </summary>
    public static void OnTop(Component canvas)
    {
        int ui = LayerMask.NameToLayer("UI");
        if (ui < 0) return;
        foreach (var t in canvas.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ui;
        EnsureOverlayCamera(ui);
    }

    private static Camera overlayCamera;

    private static void EnsureOverlayCamera(int ui)
    {
        var main = Camera.main;
        if (main == null || (overlayCamera != null && overlayCamera.transform.parent == main.transform)) return;
        var baseData = main.GetComponent<UniversalAdditionalCameraData>();
        if (baseData == null)
        {
            Debug.LogWarning("Module 3: no URP camera data on the main camera - the HUD will be depth-sorted with the world.");
            return;
        }

        main.cullingMask &= ~(1 << ui);
        var go = new GameObject("M3 UI Overlay Camera (runtime)");
        go.transform.SetParent(main.transform, false);
        overlayCamera = go.AddComponent<Camera>();
        overlayCamera.clearFlags = CameraClearFlags.Nothing;
        overlayCamera.cullingMask = 1 << ui;
        overlayCamera.nearClipPlane = main.nearClipPlane;
        overlayCamera.farClipPlane = main.farClipPlane;
        overlayCamera.fieldOfView = main.fieldOfView;
        overlayCamera.depth = main.depth + 1f;
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderType = CameraRenderType.Overlay;
        data.renderShadows = false;
        data.renderPostProcessing = false;
        // clearDepth is serialized true by default, which is what draws the overlay over the world.
        if (!baseData.cameraStack.Contains(overlayCamera)) baseData.cameraStack.Add(overlayCamera);
    }

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
        go.layer = parent.gameObject.layer;   // rows added later stay on the canvas's (UI) layer
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
        var img = Round(rt.gameObject.AddComponent<Image>());
        img.color = colour;
        return img;
    }

    public static TextMeshProUGUI Label(Transform parent, string name, float size, Color colour,
                                        TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
    {
        var rt = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (Font != null) t.font = Font;
        NoKerning(t);
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

    /// <param name="labelWidth">
    /// When above zero, the button grows past <paramref name="height"/> if the label wraps to a second
    /// line at this width (the text's own width: the button's minus its padding), so a bigger font
    /// or a longer translation is never clipped by a fixed-height button.
    /// </param>
    public static Button Button(Transform parent, string label, float height, UnityAction onClick,
                                bool enabled = true, float fontSize = 30f, float labelWidth = 0f)
    {
        var rt = Rect("Button: " + label, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var img = Round(rt.gameObject.AddComponent<Image>());
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
        rt.gameObject.AddComponent<M3ButtonJuice>();   // eased hover/press, and a buzz in the hand
        var text = Label(rt, "Text", fontSize, enabled ? Text : Muted, TextAlignmentOptions.Center);
        text.rectTransform.offsetMin = new Vector2(16f, 4f);
        text.rectTransform.offsetMax = new Vector2(-16f, -4f);
        text.text = label;
        if (labelWidth > 0f) height = Mathf.Max(height, text.GetPreferredValues(label, labelWidth, 0f).y + 8f);
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
