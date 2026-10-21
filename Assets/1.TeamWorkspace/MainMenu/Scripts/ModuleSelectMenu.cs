using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The startup menu: pick a module, press A, go.
///
/// Built entirely from code at runtime - no prefab, no scene canvas - for the same reason
/// Module1HUD is: a layout change then touches one C# file instead of a serialized scene, which
/// is the difference between a reviewable diff and an unmergeable one in this repo.
/// <c>ModuleSelectSceneBuilder</c> regenerates the (tiny) scene that hosts it.
///
/// Selection works two ways at once, because a classroom cannot rely on either alone:
///   - <b>gaze</b>: look at a card and it highlights. No aiming a controller ray, which most
///     eight-year-olds cannot do on their first day in a headset;
///   - <b>thumbstick</b> left/right, either hand, for anyone whose head is still.
/// Either way, A (or the trigger) launches.
///
/// A module whose scene is missing from the build list is drawn greyed out with the reason on it,
/// rather than silently failing at the moment a child presses the button.
/// </summary>
public class ModuleSelectMenu : MonoBehaviour
{
    [Header("Assets (wired by ModuleSelectSceneBuilder)")]
    public TMP_FontAsset font;
    public Texture2D logo;
    [Tooltip("A shot of each module, matched to its card by texture name (Module1, Module2, Module3). Optional.")]
    public Texture2D[] pictures;

    [Header("Placement")]
    [Tooltip("Metres in front of the player.")]
    public float distance = 2.0f;
    [Tooltip("World metres per canvas unit. The layout below is authored in millimetres.")]
    public float canvasScale = 0.0015f;
    [Tooltip("Degrees the player may turn away before the menu drifts back in front of them.")]
    public float recentreAngle = 40f;
    public float recentreSpeed = 2.5f;

    [Header("Timing")]
    [Tooltip("Input is ignored for this long, so a button still held from the previous scene cannot pick a module.")]
    public float inputGrace = 0.6f;
    public float launchFade = 0.35f;

    [Header("Colours (sampled from the team's UI art)")]
    public Color panelColor = new Color32(0x97, 0xF1, 0xFF, 0xFF);
    public Color headerColor = new Color32(0x1E, 0x9B, 0xE0, 0xFF);
    public Color accentColor = new Color32(0x3F, 0xCB, 0x4B, 0xFF);
    public Color inkColor = new Color32(0x0B, 0x3A, 0x4A, 0xFF);
    public Color disabledColor = new Color32(0xB9, 0xC6, 0xCB, 0xFF);

    // The canvas is authored in millimetres and then scaled by canvasScale. Card width and the
    // canvas width depend on how many modules are showing, so a fourth module is a data change
    // rather than a layout rewrite.
    const float MinCanvasW = 1560f, BaseCanvasH = 1042f;
    const float TextCardH = 400f, CardGap = 60f;
    // With pictures: header, picture frame, then a body band just tall enough for three lines.
    const float PictureTop = 88f, PictureFrameH = 230f, PictureBodyH = 180f, PictureCardH = PictureTop + PictureFrameH + PictureBodyH + 22f;
    const float SmallW = 480f, SmallH = 92f, SmallGap = 40f;

    float canvasW = MinCanvasW, canvasH = BaseCanvasH, cardW = 700f, cardH = TextCardH;
    float cardTitleSize = 46f, cardBodySize = 34f;

    class Entry
    {
        public string scene;
        public string badge, title, body;
        public bool small;
        public Texture2D picture;

        public RectTransform rect;
        public Image glow, panel;
        public TextMeshProUGUI titleText, bodyText;
        public bool available;
        public bool visible;
        public float hot;              // 0..1 selection weight, smoothed
    }

    readonly List<Entry> entries = new List<Entry>();
    readonly XRButtons input = new XRButtons();

    Transform cam;
    RectTransform root;
    RectTransform faderRoot;
    Image fader;
    TextMeshProUGUI hintText;
    Sprite rounded, pill;
    int uiLayer;
    int selected;
    int gazed = -1;   // the card the head is on, which may differ from a stick selection
    float born;
    bool launching, placed;
    float fade;
    Vector3 targetPos;
    string pendingScene;

    // ------------------------------------------------------------------ lifecycle

    void Awake()
    {
        born = Time.unscaledTime;
        uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0) uiLayer = 5;

        rounded = RoundedSprite(96, 26);
        pill = RoundedSprite(96, 46);

        BuildEntries();
        BuildCanvas();
        BuildFader();
        SelectFirstAvailable();
    }

    void Update()
    {
        if (!ResolveCamera()) return;
        input.Update();
        Place();

        if (launching)
        {
            fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, launchFade));
            fader.color = new Color(0f, 0f, 0f, fade);
            if (fade >= 1f) { launching = false; AppFlow.Load(pendingScene); }
            return;
        }

        if (Time.unscaledTime - born > inputGrace)
        {
            GazeSelect();
            StickSelect();
            if ((input.PrimaryDown || input.TriggerDown) && entries[selected].available)
                Launch(entries[selected]);
        }

        Animate();
        hintText.text = input.HasControllers ? ModuleSelectText.Hint : ModuleSelectText.HintDesktop;
    }

    // ------------------------------------------------------------------ selection

    bool ResolveCamera()
    {
        if (cam != null) return true;
        if (Camera.main != null) cam = Camera.main.transform;
        return cam != null;
    }

    void SelectFirstAvailable()
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].available) { selected = i; return; }
        selected = 0;
    }

    /// <summary>
    /// The cards are all coplanar on the canvas, so "what am I looking at" is one ray-plane
    /// intersection plus a rect test - exact, and cheaper than a physics raycast every frame.
    /// </summary>
    void GazeSelect()
    {
        var plane = new Plane(-root.forward, root.position);
        var ray = new Ray(cam.position, cam.forward);
        if (!plane.Raycast(ray, out float dist)) return;
        Vector3 hit = ray.GetPoint(dist);

        // Nearest entry rather than first hit: the cards and the small buttons below them are
        // close enough that a generous hit box round one overlaps the next, and "first in the
        // list wins" then makes the button underneath unreachable.
        int best = -1;
        float bestDist = float.MaxValue;
        float gazedDist = float.MaxValue;
        for (int i = 0; i < entries.Count; i++)
        {
            // Skip anything that cannot be launched, so gaze never parks the selection on a card
            // that is only on screen to tell a developer its scene is missing.
            if (entries[i].rect == null || !entries[i].available) continue;
            Vector3 local = entries[i].rect.InverseTransformPoint(hit);
            Rect r = entries[i].rect.rect;
            float dx = Mathf.Max(r.xMin - local.x, local.x - r.xMax, 0f);
            float dy = Mathf.Max(r.yMin - local.y, local.y - r.yMax, 0f);
            float d = Mathf.Sqrt(dx * dx + dy * dy);   // 0 when the gaze is inside the rect
            if (i == gazed) gazedDist = d;
            if (d < bestDist) { bestDist = d; best = i; }
        }

        // Forgiving by a fixed margin - a child's head is never quite still - but not so far that
        // looking at the sky keeps a card lit up.
        const float slack = 70f;
        if (best < 0 || bestDist > slack) { gazed = -1; return; }
        if (best == gazed) return;

        // Hysteresis, but only in the dead space between targets. Looking straight at something
        // always counts; looking at the gap between two cards puts the gaze inside neither and
        // nearly equidistant from both, and without this the highlight would flip back and forth
        // on sub-degree head movement, buzzing the controller every time.
        const float stickiness = 40f;
        bool lookingRightAtIt = bestDist <= 0.001f;
        if (!lookingRightAtIt && gazedDist <= bestDist + stickiness) return;

        // Gaze selects only when it moves onto a different card. Selecting whatever is looked at
        // every frame would undo a thumbstick choice on the very next frame, since the head is
        // usually still pointing at the card the stick moved away from.
        gazed = best;
        if (selected == best) return;
        selected = best;
        input.Rumble();
    }

    void StickSelect()
    {
        if (input.StepX == 0) return;
        int i = selected;
        for (int n = 0; n < entries.Count; n++)
        {
            i = (i + input.StepX + entries.Count) % entries.Count;
            if (entries[i].available) break;
        }
        if (i != selected) { selected = i; input.Rumble(); }
    }

    void Launch(Entry e)
    {
        launching = true;
        pendingScene = e.scene;
        input.Rumble(0.6f, 0.12f);
        Debug.Log($"[ModuleSelectMenu] Launching '{e.scene}'.");
    }

    // ------------------------------------------------------------------ presentation

    /// <summary>
    /// World-locked, not head-locked: a panel welded to the eyes is unpleasant to read and would
    /// make gaze selection impossible. It only drifts back once the player has turned well away.
    /// </summary>
    void Place()
    {
        Vector3 forward = Flatten(cam.forward);
        Vector3 wanted = cam.position + forward * distance;
        wanted.y = cam.position.y - 0.12f;

        if (!placed)
        {
            targetPos = wanted;
            root.position = wanted;
            root.rotation = Quaternion.LookRotation(Flatten(wanted - cam.position), Vector3.up);
            placed = true;
        }
        else if (Vector3.Angle(forward, Flatten(targetPos - cam.position)) > recentreAngle)
        {
            targetPos = wanted;
        }

        float t = 1f - Mathf.Exp(-recentreSpeed * Time.unscaledDeltaTime);
        root.position = Vector3.Lerp(root.position, targetPos, t);
        Vector3 away = Flatten(root.position - cam.position);
        root.rotation = Quaternion.Slerp(root.rotation, Quaternion.LookRotation(away, Vector3.up), t);

        // The fade-out quad rides just in front of the eyes.
        faderRoot.position = cam.position + cam.forward * 0.12f;
        faderRoot.rotation = cam.rotation;
    }

    static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    void Animate()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.rect == null) continue;
            bool on = i == selected && e.available;
            e.hot = Mathf.MoveTowards(e.hot, on ? 1f : 0f, Time.unscaledDeltaTime * 6f);

            float pulse = on ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f) : 0f;
            e.rect.localScale = Vector3.one * (1f + 0.035f * e.hot);
            e.glow.color = new Color(accentColor.r, accentColor.g, accentColor.b, e.hot * (0.55f + 0.45f * pulse));
            e.panel.color = e.available ? Color.Lerp(panelColor, Color.white, e.hot * 0.35f) : disabledColor;
        }
    }

    // ------------------------------------------------------------------ build

    void BuildEntries()
    {
        Add(new Entry { scene = AppFlow.Module1Scene, badge = "1", title = ModuleSelectText.Module1Title, body = ModuleSelectText.Module1Body, picture = Picture("Module1") });
        Add(new Entry { scene = AppFlow.Module2Scene, badge = "2", title = ModuleSelectText.Module2Title, body = ModuleSelectText.Module2Body, picture = Picture("Module2") });
        Add(new Entry { scene = AppFlow.Module3Scene, badge = "3", title = ModuleSelectText.Module3Title, body = ModuleSelectText.Module3Body, picture = Picture("Module3") });
        Add(new Entry { scene = AppFlow.Module2TutorialScene, title = ModuleSelectText.TutorialTitle, small = true });
        Add(new Entry { scene = AppFlow.IpMenuScene, title = ModuleSelectText.GamaTitle, small = true });
    }

    /// <summary>
    /// A module that is not in the build list is hidden from a player build and drawn greyed out,
    /// with the reason, in the editor and in development builds.
    ///
    /// Module 3 lives on its own branch: its entry is listed above so that the day that branch
    /// merges it appears here on its own, but until then a class must not be shown a dead card
    /// captioned with a Unity menu path.
    /// </summary>
    Texture2D Picture(string name)
    {
        if (pictures == null) return null;
        foreach (var t in pictures) if (t != null && t.name == name) return t;
        return null;
    }

    static bool ShowDiagnostics => Application.isEditor || Debug.isDebugBuild;

    void Add(Entry e)
    {
        e.available = AppFlow.IsAvailable(e.scene);
        e.visible = e.available || ShowDiagnostics;
        entries.Add(e);
    }

    void BuildCanvas()
    {
        var go = new GameObject("Module Select Canvas") { layer = uiLayer };
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        // Read from ~2 m away, so give TMP headroom to rasterise at.
        scaler.dynamicPixelsPerUnit = 3f;

        // Three modules across need more room than two; four need smaller cards as well.
        int bigCount = 0;
        bool anyPicture = false;
        foreach (var e in entries)
        {
            if (e.small || !e.visible) continue;
            bigCount++;
            anyPicture |= e.picture != null && e.available;
        }
        // Every card gets the picture band if any card has one, so the row stays level.
        cardH = anyPicture ? PictureCardH : TextCardH;
        canvasH = BaseCanvasH + (cardH - TextCardH);
        cardW = bigCount >= 4 ? 480f : bigCount == 3 ? 580f : 700f;
        cardTitleSize = bigCount >= 4 ? 38f : bigCount == 3 ? 42f : 46f;
        cardBodySize = bigCount >= 4 ? 28f : bigCount == 3 ? 32f : 34f;
        canvasW = Mathf.Max(MinCanvasW, bigCount * cardW + (bigCount - 1) * CardGap + 100f);

        root = (RectTransform)go.transform;
        root.sizeDelta = new Vector2(canvasW, canvasH);
        root.localScale = Vector3.one * canvasScale;

        var back = NewImage("Backdrop", root, rounded, new Color(0.02f, 0.10f, 0.14f, 0.55f));
        Stretch(back.rectTransform, 28f);

        float y = -34f;

        if (logo != null)
        {
            const float h = 120f;
            var logoRect = NewRect("Logo", root, new Vector2(h * logo.width / logo.height, h), new Vector2(0f, y));
            var raw = logoRect.gameObject.AddComponent<RawImage>();
            raw.texture = logo;
            raw.raycastTarget = false;
            y -= h + 20f;
        }

        var title = NewText("Title", root, ModuleSelectText.Title, 64, Color.white, new Vector2(canvasW, 76), new Vector2(0f, y));
        title.fontStyle = FontStyles.Bold;
        y -= 84f;

        NewText("Welcome", root, ModuleSelectText.WelcomeLao, 38, new Color(0.78f, 0.93f, 1f),
            new Vector2(canvasW - 160f, 58), new Vector2(0f, y));
        // Room for the Lao line's below-base vowels and for the title pills, which ride 26 above
        // the top of each card.
        y -= 112f;

        float cardY = y;
        float rowW = bigCount * cardW + (bigCount - 1) * CardGap;
        float cursor = -rowW * 0.5f + cardW * 0.5f;
        foreach (var e in entries)
        {
            if (e.small || !e.visible) continue;
            BuildCard(e, new Vector2(cursor, cardY));
            cursor += cardW + CardGap;
        }
        y = cardY - cardH - 46f;

        int smallIndex = 0;
        foreach (var e in entries)
        {
            if (!e.small || !e.visible) continue;
            float x = (smallIndex == 0 ? -1f : 1f) * (SmallW + SmallGap) * 0.5f;
            BuildSmall(e, new Vector2(x, y));
            smallIndex++;
        }
        y -= SmallH + 52f;

        hintText = NewText("Hint", root, ModuleSelectText.Hint, 30, new Color(0.80f, 0.92f, 1f, 0.92f),
            new Vector2(canvasW - 100f, 44), new Vector2(0f, y));
    }

    void BuildCard(Entry e, Vector2 pos)
    {
        e.rect = NewRect("Card " + e.title, root, new Vector2(cardW, cardH), pos);

        e.glow = NewImage("Glow", e.rect, rounded, Color.clear);
        Stretch(e.glow.rectTransform, 14f);

        e.panel = NewImage("Panel", e.rect, rounded, panelColor);
        Stretch(e.panel.rectTransform, 0f);

        // The blue title pill overlaps the top edge, as on the team's Welcome UI art.
        var header = NewImage("Header", e.rect, pill, headerColor);
        var hr = header.rectTransform;
        hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 1f);
        hr.pivot = new Vector2(0.5f, 1f);
        hr.sizeDelta = new Vector2(cardW - 120f, 92f);
        hr.anchoredPosition = new Vector2(0f, 26f);

        e.titleText = Centred(NewText("Title", hr, e.title, cardTitleSize, Color.white, new Vector2(cardW - 150f, 92), Vector2.zero));
        // One line, shrunk to fit: with four cards across, a long title would otherwise wrap out of its pill.
        e.titleText.textWrappingMode = TextWrappingModes.NoWrap;
        FitText(e.titleText, cardTitleSize);
        e.titleText.fontStyle = FontStyles.Bold;

        if (!string.IsNullOrEmpty(e.badge))
        {
            var badge = NewImage("Badge", e.rect, rounded, accentColor);
            var br = badge.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0f, 1f);
            br.pivot = new Vector2(0f, 1f);
            br.sizeDelta = new Vector2(86f, 86f);
            br.anchoredPosition = new Vector2(-18f, 30f);
            Centred(NewText("N", br, e.badge, 52, Color.white, new Vector2(86, 86), Vector2.zero)).fontStyle = FontStyles.Bold;
        }

        float bodyTop = -120f, bodyH = TextCardH - 150f;
        if (cardH == PictureCardH)
        {
            BuildPicture(e, new Vector2(0f, -PictureTop), new Vector2(cardW - 60f, PictureFrameH));
            bodyTop = -PictureTop - PictureFrameH - 10f;
            bodyH = PictureBodyH;
        }

        // Centred in the space under the header, so a two-line and a three-line card still balance.
        e.bodyText = NewText("Body", e.rect, e.body, cardBodySize, inkColor, new Vector2(cardW - 110f, bodyH), new Vector2(0f, bodyTop));
        // The copy wraps to whatever width the card has and shrinks rather than overflowing it.
        e.bodyText.verticalAlignment = VerticalAlignmentOptions.Middle;
        FitText(e.bodyText, cardBodySize);

        if (!e.available) MarkUnavailable(e, new Vector2(0f, -cardH + 64f), cardW - 60f, 26);
    }

    /// <summary>
    /// The module's picture, cropped to fill a rounded frame. A card without one gets a plain
    /// tinted frame, so the cards in a row still line up.
    /// </summary>
    void BuildPicture(Entry e, Vector2 pos, Vector2 size)
    {
        var frame = NewRect("Picture", e.rect, size, pos);
        var mask = frame.gameObject.AddComponent<Image>();
        mask.sprite = rounded;
        mask.type = Image.Type.Sliced;
        mask.color = new Color(inkColor.r, inkColor.g, inkColor.b, 0.18f);
        mask.raycastTarget = false;
        if (e.picture == null || !e.available) return;

        frame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var go = new GameObject("Shot") { layer = uiLayer };
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(frame, false);
        Stretch(rt, 0f);
        var raw = go.AddComponent<RawImage>();
        raw.texture = e.picture;
        raw.raycastTarget = false;

        // Centre-crop the shot to the frame's aspect rather than squashing it.
        float frameAspect = size.x / size.y;
        float texAspect = (float)e.picture.width / e.picture.height;
        raw.uvRect = texAspect > frameAspect
            ? new Rect((1f - frameAspect / texAspect) * 0.5f, 0f, frameAspect / texAspect, 1f)
            : new Rect(0f, (1f - texAspect / frameAspect) * 0.5f, 1f, texAspect / frameAspect);
    }

    void BuildSmall(Entry e, Vector2 pos)
    {
        e.rect = NewRect("Button " + e.title, root, new Vector2(SmallW, SmallH), pos);

        e.glow = NewImage("Glow", e.rect, pill, Color.clear);
        Stretch(e.glow.rectTransform, 10f);

        e.panel = NewImage("Panel", e.rect, pill, panelColor);
        Stretch(e.panel.rectTransform, 0f);

        e.titleText = Centred(NewText("Title", e.rect, e.title, 34, inkColor, new Vector2(SmallW - 40f, SmallH), Vector2.zero));

        if (!e.available) MarkUnavailable(e, new Vector2(0f, -SmallH - 2f), SmallW + 120f, 21);
    }

    /// <summary>
    /// The most common way this menu breaks is a scene missing from the build list, so say so on
    /// the card instead of letting the button press do nothing.
    /// </summary>
    void MarkUnavailable(Entry e, Vector2 pos, float width, float fontSize)
    {
        e.titleText.color = new Color(0.30f, 0.34f, 0.36f);

        if (e.bodyText != null)
        {
            // Reuse the body rather than stacking a label over it: the pitch and the reason
            // together overflow a card, and a developer looking at a dead card wants the reason.
            e.bodyText.text = ModuleSelectText.NotInBuild;
            e.bodyText.color = new Color(0.86f, 0.52f, 0.20f);
            e.bodyText.enableAutoSizing = false;
            e.bodyText.fontSize = fontSize;
        }
        else
        {
            NewText("Unavailable", e.rect, ModuleSelectText.NotInBuild, fontSize,
                new Color(1f, 0.72f, 0.36f), new Vector2(width, 40f), pos);
        }

        Debug.LogWarning($"[ModuleSelectMenu] Scene '{e.scene}' is not in the build list; its card is disabled.");
    }

    /// <summary>
    /// A head-locked black quad for the launch fade. It is a world-space canvas, not a
    /// ScreenSpaceOverlay one, because overlay canvases are not rendered to an XR display at all.
    /// </summary>
    void BuildFader()
    {
        var go = new GameObject("Fader") { layer = uiLayer };
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        faderRoot = (RectTransform)go.transform;
        faderRoot.sizeDelta = new Vector2(2000f, 2000f);   // 2 m square, 12 cm from the eyes
        faderRoot.localScale = Vector3.one * 0.001f;   // fader is sized in metres already

        fader = NewImage("Black", faderRoot, null, new Color(0f, 0f, 0f, 0f));
        Stretch(fader.rectTransform, 0f);
    }

    // ------------------------------------------------------------------ ui helpers

    RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name) { layer = uiLayer };
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name) { layer = uiLayer };
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        if (sprite != null) img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, Vector2 rectSize, Vector2 pos)
    {
        var go = new GameObject(name) { layer = uiLayer };
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = rectSize;
        rt.anchoredPosition = pos;
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    static void FitText(TextMeshProUGUI t, float size)
    {
        t.enableAutoSizing = true;
        t.fontSizeMax = size;
        t.fontSizeMin = Mathf.Round(size * 0.6f);
    }

    static TextMeshProUGUI Centred(TextMeshProUGUI t)
    {
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        t.alignment = TextAlignmentOptions.Center;
        return t;
    }

    static void Stretch(RectTransform rt, float outset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(-outset, -outset);
        rt.offsetMax = new Vector2(outset, outset);
    }

    /// <summary>A white rounded rectangle, 9-sliced so one texture serves every panel size.</summary>
    static Sprite RoundedSprite(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0f);
                float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - d + 0.5f);   // 1 px of antialiasing along the curve
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }
}

/// <summary>
/// Every player-facing string in the menu, in one place, so translating it later is one file.
/// The Lao line is the team's own welcome copy, lifted from
/// <c>Team Assets/UI/Sprites/UI/Main UI.png</c>; the rest is still English and wants a native
/// speaker before this goes in front of a class.
/// </summary>
public static class ModuleSelectText
{
    public const string Title = "Choose a module";
    public const string WelcomeLao = "ຍິນດີຕ້ອນຮັບເຂົ້າສູ່ເກມການຈຳລອງຊີວິດຂອງຍຸງລາຍ!";

    public const string Module1Title = "Be a Mosquito";
    public const string Module1Body =
        "Fly as an Aedes mosquito. Nectar, a mate, a blood meal - then lay your eggs in rainwater.";

    public const string Module2Title = "Protect the House";
    public const string Module2Body =
        "Find where mosquitoes breed. Cover the jars, empty containers, add guppies, clear the rubbish.";

    public const string Module3Title = "Health Check";
    public const string Module3Body =
        "Talk to the villagers and check their symptoms. Hospital, rest at home, or are they fine?";

    public const string TutorialTitle = "Module 2 - Tutorial";
    public const string GamaTitle = "GAMA / IP setup";

    public const string Hint = "Look at a module, then press  A        stick to move   |   hold  B  in game to come back here";
    public const string HintDesktop = "Arrows to move, Space to start        desktop test mode - no headset detected";
    public const string NotInBuild = "Scene missing from the build list (File > Build Profiles)";
}
