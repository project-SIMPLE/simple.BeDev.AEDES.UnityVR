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
    public static readonly Color Text = new Color(0.93f, 0.94f, 0.95f, 1f);   // off-white: pure white haloes on a dark panel in a headset
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

    /// <summary>Gives <paramref name="image"/> the rounded panel shape, with corners <paramref name="radius"/> canvas units round.</summary>
    public static Image Round(Image image, float radius = 10f)
    {
        image.sprite = Rounded;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 10f / Mathf.Max(1f, radius);   // the sprite's corners are 10 px
        return image;
    }

    // One shape language for every surface: cards are the roundest, then buttons and chips.
    public const float CardRadius = 24f, ButtonRadius = 16f, ChipRadius = 14f;

    /// <summary>A hairline across a card, between its header and its body.</summary>
    public static readonly Color RuleColour = new Color(1f, 1f, 1f, 0.12f);

    /// <summary>
    /// A card: a rounded panel centred on its parent, <paramref name="width"/> units wide and as tall as
    /// its content, whose rows stack top to bottom and share one left edge.
    /// </summary>
    public static RectTransform Card(Transform parent, string name, float width, RectOffset padding, float spacing)
    {
        var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, 100f);
        Round(rt.gameObject.AddComponent<Image>(), CardRadius).color = PanelColour;
        var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = padding;
        v.spacing = spacing;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        rt.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    /// <summary>A hairline the width of the card.</summary>
    public static Image Rule(Transform parent)
    {
        var img = Rect("Rule", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        img.color = RuleColour;
        img.raycastTarget = false;
        var le = img.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = le.minHeight = 2f;
        le.flexibleWidth = 1f;
        return img;
    }

    /// <summary>Keeps <paramref name="c"/> at least <paramref name="width"/> units wide, so a changing number does not make its row twitch.</summary>
    public static LayoutElement MinWidth(Component c, float width)
    {
        var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = width;
        return le;
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

    /// <summary>
    /// The text for <paramref name="key"/> in the current language, with {0}.. filled in. Never the raw key
    /// while the CSV has an English line for it: with no LocalizationManager in the scene (Module 3 entered
    /// from a menu that has none), or one that does not know the key, a headset in a classroom showed
    /// "m3.ui...." placeholders. Then the English column is read straight from the CSV.
    /// </summary>
    public static string L(string key, params object[] args)
    {
        if (string.IsNullOrEmpty(key)) return "";
        string value = LocalizationManager.Instance != null ? LocalizationManager.Instance.GetLocalizedValue(key) : null;
        if (string.IsNullOrEmpty(value) || value == key) value = English(key) ?? key;
        if (args == null || args.Length == 0) return value;
        try { return string.Format(value, args); }
        catch (FormatException) { return value; }
    }

    private static System.Collections.Generic.Dictionary<string, string> english;

    private static string English(string key)
    {
        if (english == null)
        {
            english = new System.Collections.Generic.Dictionary<string, string>();
            var csv = Resources.Load<TextAsset>("Localization/LocalizationData");
            if (csv != null)
            {
                var rows = Aedes.Module3.Sim.Csv.Parse(csv.text);
                for (int r = 1; r < rows.Count; r++)
                    if (rows[r].Count > 1 && rows[r][1].Length > 0) english[rows[r][0].Trim()] = rows[r][1];
            }
        }
        if (english.TryGetValue(key, out string text)) return text;
        Debug.LogWarning($"Module 3: no text for '{key}' in the localization CSV.");
        return null;
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

    // ---------------------------------------------------------------------------------------
    // Icons. Module 3 talks in pictures first (M3Icons); words are the caption, not the button.
    // ---------------------------------------------------------------------------------------

    /// <summary>The backing of a chip: a little lighter than the panel it sits on.</summary>
    public static readonly Color ChipColour = new Color(1f, 1f, 1f, 0.08f);

    /// <summary>An icon <paramref name="size"/> units square, sized for a layout group.</summary>
    public static Image Icon(Transform parent, string icon, float size, Color colour)
    {
        var rt = Rect("Icon: " + icon, parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        rt.sizeDelta = new Vector2(size, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = M3Icons.Get(icon);
        img.preserveAspect = true;
        img.color = colour;
        img.raycastTarget = false;
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = size;
        le.preferredHeight = le.minHeight = size;
        return img;
    }

    /// <summary>Puts <paramref name="rt"/> at a point of its parent, <paramref name="size"/> units square, outside any layout.</summary>
    public static RectTransform Pin(RectTransform rt, Vector2 anchor, float size, Vector2 offset = default)
    {
        var le = rt.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = offset;
        return rt;
    }

    /// <summary>A horizontal row that sizes its children from their LayoutElements.</summary>
    public static RectTransform Row(Transform parent, string name, float spacing,
                                    TextAnchor align = TextAnchor.MiddleLeft, RectOffset padding = null)
    {
        var rt = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = align;
        h.padding = padding ?? new RectOffset();
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        return rt;
    }

    /// <summary>One line of text in a row, as wide as it needs to be.</summary>
    public static TextMeshProUGUI Word(Transform row, string text, float size, Color colour)
    {
        var t = Label(row, "Word", size, colour, TextAlignmentOptions.MidlineLeft);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.text = text;
        return t;
    }

    /// <summary>Space that takes up whatever a row has left, pushing what follows it to the far end.</summary>
    public static void Spacer(Transform row)
    {
        var rt = Rect("Spacer", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        rt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
    }

    /// <summary>
    /// Something the Pilot has noticed, as an icon on a soft backing, with an optional small icon and
    /// word after it ("fever, 3 days" is a thermometer, a sun and a 3). The backing is what the ray
    /// hovers, so a chip can carry its sentence as a caption (<see cref="M3Hover"/>).
    /// </summary>
    public static RectTransform Chip(Transform parent, string icon, float size, Color colour,
                                     string smallIcon = null, string word = null)
    {
        int pad = Mathf.RoundToInt(size * 0.14f);
        var row = Row(parent, "Chip: " + icon, size * 0.1f, TextAnchor.MiddleCenter,
                      new RectOffset(pad, pad + (word != null ? pad : 0), pad, pad));
        Round(row.gameObject.AddComponent<Image>(), ChipRadius).color = ChipColour;
        Icon(row, icon, size, colour);
        if (smallIcon != null) Icon(row, smallIcon, size * 0.5f, colour);
        if (word != null) Word(row, word, size * 0.62f, colour);
        return row;
    }

    /// <summary>
    /// A square button showing <paramref name="icon"/>. One that has been done stays where it was,
    /// dimmed, with a tick on its corner, so the row never shuffles and the Pilot sees what is left.
    /// </summary>
    public static Button IconButton(Transform parent, string icon, float size, UnityAction onClick,
                                    bool enabled = true, bool done = false)
    {
        var rt = Rect("Button: " + icon, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var img = Round(rt.gameObject.AddComponent<Image>(), ButtonRadius);
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

        Pin(Icon(rt, icon, size * 0.64f, enabled ? Text : Muted).rectTransform, new Vector2(0.5f, 0.5f), size * 0.64f);
        if (done)
        {
            var tick = Round(Rect("Done", rt, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>(), size * 0.2f);
            tick.color = Accent;
            tick.raycastTarget = false;
            Pin(tick.rectTransform, new Vector2(1f, 0f), size * 0.4f, new Vector2(-size * 0.08f, size * 0.08f));
            Pin(Icon(tick.rectTransform, M3Icons.Check, size * 0.3f, PanelColour).rectTransform, new Vector2(0.5f, 0.5f), size * 0.3f);
        }

        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = size;
        le.preferredHeight = le.minHeight = size;
        return button;
    }

    /// <summary>A house with its number in it: how the handover names a household.</summary>
    public static Image HouseIcon(Transform parent, int number, float size, Color colour)
    {
        var img = Icon(parent, M3Icons.House, size, colour);
        var t = Label(img.rectTransform, "Number", size * 0.4f, colour, TextAlignmentOptions.Center);
        // Inside the walls: the house's body is the lower 14%..60% of the icon.
        t.rectTransform.offsetMin = new Vector2(0f, size * 0.13f);
        t.rectTransform.offsetMax = new Vector2(0f, -size * 0.4f);
        t.fontStyle = FontStyles.Bold;
        t.text = number.ToString();
        return img;
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
