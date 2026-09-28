using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// In-headset HUD for Module 2 (household vector control).
///
/// Same approach as Module1HUD: everything is built from code at runtime - no prefab, no scene
/// canvas - so a UI change never has to touch the 28k-line scene and nothing can go missing on
/// merge. Every object goes on the UI layer.
///
/// Module 1's rig already carried an overlay camera in its URP stack; the Module 2 rig has a single
/// base camera that renders every layer, so this component builds the overlay camera itself at
/// runtime (UI layer only, depth cleared) and takes UI off the base camera's culling mask. That is
/// what keeps the HUD on top of the house instead of being buried in a wall when the player steps
/// up to a jar. Set useOverlayCamera = false to fall back to a plain world-space canvas.
///
/// The HUD only reads M2Manager state each frame. Game code pushes one-off events through Toast(),
/// ShowIntro(), ShowEnd() and OnSiteCleared(). All player-facing copy lives in Module2Text at the
/// bottom of this file.
///
/// M2Manager adds this component to itself if the scene does not already carry one, so the
/// serialized fields below are only there for tuning in the inspector.
/// </summary>
public class Module2HUD : MonoBehaviour
{
    public static Module2HUD Instance { get; private set; }

    [Header("Placement (world units - the Module 2 rig is unscaled, so 1.0 is 1 m)")]
    public float distance = 1.2f;
    public float followSpeed = 6f;

    [Header("Text")]
    [Tooltip("Font used for all HUD text. Leave empty for the TMP default - but note that default is LiberationSans, which has no Lao glyphs, so a Lao build must set this.")]
    public TMP_FontAsset fontOverride;

    [Header("Rendering")]
    [Tooltip("Build a URP overlay camera so the HUD always draws on top of the house.")]
    public bool useOverlayCamera = true;

    [Header("Timing")]
    public float toastDuration = 3.5f;
    public float promptRefreshInterval = 0.2f;

    [Header("Distances")]
    [Tooltip("Height above a container that its beacon floats at.")]
    public float beaconHeight = 0.45f;
    [Tooltip("How close an open site has to be before its prompt appears.")]
    public float promptDistance = 2.5f;

    [Header("Colours")]
    public Color accentColor = new Color(0.45f, 0.82f, 1f);
    public Color doneColor = new Color(0.4f, 0.85f, 0.45f);
    public Color warnColor = new Color(1f, 0.55f, 0.1f);
    public Color dangerColor = new Color(0.9f, 0.18f, 0.18f);
    public Color textColor = Color.white;
    public Color mutedColor = new Color(0.75f, 0.78f, 0.82f);
    public Color panelColor = new Color(0.04f, 0.05f, 0.07f, 0.72f);
    [Tooltip("Deliberately different from the intro panel: both screens ask for A, and looking alike made a restart read as nothing happening.")]
    public Color endPanelColor = new Color(0.10f, 0.05f, 0.04f, 0.92f);
    public Color barBackColor = new Color(0f, 0f, 0f, 0.55f);

    // Canvas size in millimetres (canvas scale is 0.001, so 1 unit = 1 mm at `distance`).
    const float CanvasW = 1100f, CanvasH = 620f;
    const float ToastW = 460f, PromptW = 640f;
    const int MaxToasts = 3;

    // ---- runtime references ----
    Transform cam;
    Camera baseCamera, overlayCamera;
    Vector3 smoothForward;
    int uiLayer;
    TMP_FontAsset font;

    RectTransform hudRoot;
    CanvasGroup gameplayGroup, promptGroup, introGroup, endGroup;

    TextMeshProUGUI timerText, dayTagText, scoreText, sitesText, objectiveText, promptText;
    TextMeshProUGUI energyLabel, biteText;
    Image energyFill, biteFlash;
    float biteFlashUntil = -1f;
    RectTransform promptRect;
    string lastPrompt;
    Image objectiveArrow, sitesFill;
    TextMeshProUGUI introKicker, introTitle, introBody, introFooter, introInputDebug;
    TextMeshProUGUI endKicker, endTitle, endSubtitle, endStats, endFact, endFooter;

    RectTransform beaconRoot;
    Image beacon;
    RectTransform toastContainer;
    readonly List<ToastEntry> toasts = new List<ToastEntry>();

    Sprite roundedSprite, arrowSprite, dropSprite;

    // ---- state ----
    Component nearestSite;
    float nearestDistance;
    float nextPromptRefresh;
    bool warnedOneMinute, warnedThirtySeconds;
    bool overlayReady;

    class ToastEntry
    {
        public RectTransform rect;
        public CanvasGroup group;
        public float born;
    }

    // ------------------------------------------------------------------ lifecycle

    void Awake()
    {
        Instance = this;
        uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0) uiLayer = 5;
        // Prefer the regenerated Lao font (full Lao block + Latin, see Editor/LaoFontTool). TMP's own
        // default is LiberationSans, which renders every Lao glyph as a blank.
        font = fontOverride;
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/Lao_SomVang Full SDF");
        if (font == null) font = TMP_Settings.defaultFontAsset;

        BuildSprites();
        BuildHud();

        gameplayGroup.alpha = 0f;
        promptGroup.alpha = 0f;
        introGroup.alpha = 0f;
        endGroup.alpha = 0f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (hudRoot != null) Destroy(hudRoot.gameObject);
        if (beaconRoot != null) Destroy(beaconRoot.gameObject);
        if (overlayCamera != null)
        {
            // Take the overlay back out of the stack before it is destroyed, or URP keeps a null
            // entry and logs every frame.
            if (baseCamera != null)
            {
                var data = baseCamera.GetComponent<UniversalAdditionalCameraData>();
                if (data != null) data.cameraStack.Remove(overlayCamera);
            }
            Destroy(overlayCamera.gameObject);
        }
    }

    void LateUpdate()
    {
        if (!ResolveCamera()) return;
        if (useOverlayCamera && !overlayReady) SetupOverlayCamera();
        Follow();

        var m = M2Manager.Instance;
        if (m == null) return;

        Fade(gameplayGroup, m.IsPlaying);

        UpdateStatus(m);
        UpdateVitals(m);
        UpdateSites(m);
        RefreshNearest(m);
        UpdateObjective(m);
        UpdatePrompt(m);
        UpdateToasts();
        UpdateBeacon(m);
        UpdateIntro(m);

        if (introGroup.alpha > 0f)
            introFooter.alpha = 0.55f + 0.45f * Mathf.Sin(Time.time * 4f);
        if (endGroup.alpha > 0f)
            endFooter.alpha = 0.55f + 0.45f * Mathf.Sin(Time.time * 4f);
    }

    // ------------------------------------------------------------------ public API

    public void ShowIntro()
    {
        introKicker.text = Module2Text.IntroKicker;
        introTitle.text = Module2Text.IntroTitle;
        introBody.text = Module2Text.IntroBody;
        introFooter.text = Module2Text.IntroFooter;
        introGroup.alpha = 1f;
    }

    public void HideIntro()
    {
        introGroup.alpha = 0f;
    }

    /// <summary>Short message stacked below the timer. Newest on top, at most three at once.</summary>
    public void Toast(string message, Color? accent = null)
    {
        if (string.IsNullOrEmpty(message)) return;

        var rect = NewRect("Toast", toastContainer, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(ToastW, 44));
        var bg = rect.gameObject.AddComponent<Image>();
        bg.sprite = roundedSprite; bg.type = Image.Type.Sliced; bg.color = panelColor; bg.raycastTarget = false;
        var text = NewText("Text", rect, message, 24, accent ?? textColor, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-28, 0));
        rect.sizeDelta = new Vector2(ToastW, Mathf.Max(44f, text.GetPreferredValues(message, ToastW - 28f, 0f).y + 18f));

        var group = rect.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        toasts.Insert(0, new ToastEntry { rect = rect, group = group, born = Time.time });

        while (toasts.Count > MaxToasts)
        {
            Destroy(toasts[toasts.Count - 1].rect.gameObject);
            toasts.RemoveAt(toasts.Count - 1);
        }
    }

    /// <summary>A mosquito reached the player. B4's relay trigger is 10 bites.</summary>
    public void OnBitten(int count, int limit)
    {
        biteFlashUntil = Time.time + 0.35f;
        if (count >= limit - 2 && count < limit) Toast(Module2Text.BitesNearLimit, dangerColor);
    }

    public void OnSiteCleared(Component site, int cleared, int total)
    {
        if (site == nearestSite) nearestSite = null;
        nextPromptRefresh = 0f;
        Toast(Module2Text.SiteCleared(SiteLabel(site), cleared, total), doneColor);
    }

    public void ShowEnd(M2Manager.RoundEndReason reason, int score, int sitesCleared, int sitesTotal, int swatted, int trash, int sourceReductionPercent, int bites, bool fromSimulation)
    {
        endKicker.text = Module2Text.EndKicker;
        endTitle.text = Module2Text.EndTitle(reason);
        endSubtitle.text = Module2Text.EndSubtitle(reason, sitesCleared, sitesTotal);
        endStats.text = Module2Text.EndStats(score, sitesCleared, sitesTotal, swatted, trash, sourceReductionPercent, bites);
        // The consequence is the teaching moment, so it sits above the trivia rather than in it.
        endFact.text = Module2Text.Consequence(sitesTotal - sitesCleared, sourceReductionPercent, fromSimulation)
                       + "\n" + Module2Text.RandomFact();
        endFooter.text = Module2Text.EndFooter;
        endTitle.color = reason == M2Manager.RoundEndReason.AllSitesCleared ? doneColor : warnColor;

        endGroup.alpha = 1f;
        promptGroup.alpha = 0f;
        ClearToasts();
    }

    /// <summary>
    /// Switches off the score and timer labels baked into VR_Player_Module_2 so they do not sit
    /// behind this HUD saying the same thing twice. They stay wired, so deleting this component
    /// brings the old labels straight back.
    /// </summary>
    public void HideLegacyLabels(TextMeshProUGUI[] scoreLabels, TextMeshProUGUI timerLabel)
    {
        if (scoreLabels != null)
            foreach (var label in scoreLabels)
                if (label != null) label.gameObject.SetActive(false);
        if (timerLabel != null) timerLabel.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ per-frame updates

    bool ResolveCamera()
    {
        if (cam != null) return true;
        if (Camera.main != null)
        {
            baseCamera = Camera.main;
            cam = baseCamera.transform;
            smoothForward = cam.forward;
        }
        return cam != null;
    }

    // The Module 2 rig has one base camera that renders every layer, so a world-space HUD would be
    // occluded by the house. An overlay camera that clears depth and renders only the UI layer puts
    // the HUD on top, which is how Module 1's rig is set up in its scene.
    void SetupOverlayCamera()
    {
        overlayReady = true;

        var baseData = baseCamera.GetComponent<UniversalAdditionalCameraData>();
        if (baseData == null)
        {
            Debug.LogWarning("[Module2HUD] No URP camera data on the main camera - the HUD will be depth-sorted with the world.");
            return;
        }

        baseCamera.cullingMask &= ~(1 << uiLayer);

        var go = new GameObject("Module2HUD Overlay Camera (runtime)");
        go.transform.SetParent(baseCamera.transform, false);
        overlayCamera = go.AddComponent<Camera>();
        overlayCamera.clearFlags = CameraClearFlags.Nothing;
        overlayCamera.cullingMask = 1 << uiLayer;
        overlayCamera.nearClipPlane = baseCamera.nearClipPlane;
        overlayCamera.farClipPlane = baseCamera.farClipPlane;
        overlayCamera.fieldOfView = baseCamera.fieldOfView;
        overlayCamera.depth = baseCamera.depth + 1f;

        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderType = CameraRenderType.Overlay;
        data.renderShadows = false;
        data.renderPostProcessing = false;
        // clearDepth is read-only on UniversalAdditionalCameraData; it is serialized true by
        // default, which is exactly what an overlay needs to draw over the world.

        if (!baseData.cameraStack.Contains(overlayCamera)) baseData.cameraStack.Add(overlayCamera);
    }

    // Translation follows the head instantly; the view direction is smoothed so the HUD trails a
    // little when the player turns instead of being glued to the eyes.
    void Follow()
    {
        smoothForward = Vector3.Slerp(smoothForward, cam.forward, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        hudRoot.position = cam.position + smoothForward * distance;
        hudRoot.rotation = Quaternion.LookRotation(smoothForward, cam.up);
    }

    void UpdateStatus(M2Manager m)
    {
        int t = Mathf.CeilToInt(m.TimeRemaining);
        timerText.text = (t / 60) + ":" + (t % 60).ToString("00");
        timerText.color = t <= 10 ? dangerColor : t <= 30 ? warnColor : textColor;
        timerText.alpha = (t <= 10 && m.IsPlaying) ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 6f)) : 1f;

        scoreText.text = Module2Text.Score(m.score);
        dayTagText.text = m.isNight ? Module2Text.NightTag : Module2Text.DayTag;
        dayTagText.color = m.isNight ? accentColor : mutedColor;

        if (!m.IsPlaying) return;

        if (!warnedOneMinute && m.TimeRemaining <= 60f && m.RoundLength > 60f)
        {
            warnedOneMinute = true;
            Toast(Module2Text.OneMinuteLeft, warnColor);
        }
        if (!warnedThirtySeconds && m.TimeRemaining <= 30f)
        {
            warnedThirtySeconds = true;
            Toast(Module2Text.ThirtySecondsLeft, warnColor);
        }
    }

    void UpdateVitals(M2Manager m)
    {
        energyLabel.text = Module2Text.EnergyLabel;
        energyFill.fillAmount = Mathf.Clamp01(m.Energy);
        energyFill.color = m.Energy < 0.35f
            ? Color.Lerp(warnColor, dangerColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 6f))
            : doneColor;

        biteText.text = Module2Text.Bites(m.BiteCount, m.BiteLimit);
        biteText.color = m.BiteCount >= m.BiteLimit - 2 ? dangerColor : textColor;

        float alpha = biteFlashUntil > Time.time ? 0.35f * Mathf.InverseLerp(0f, 0.35f, biteFlashUntil - Time.time) : 0f;
        biteFlash.color = new Color(dangerColor.r, dangerColor.g, dangerColor.b, alpha);
    }

    void UpdateSites(M2Manager m)
    {
        int total = m.SiteCount;
        int cleared = m.SitesCleared;
        sitesText.text = Module2Text.Sites(cleared, total);
        sitesFill.fillAmount = total > 0 ? (float)cleared / total : 0f;
        sitesFill.color = (total > 0 && cleared >= total) ? doneColor : accentColor;
    }

    void RefreshNearest(M2Manager m)
    {
        if (Time.time < nextPromptRefresh) return;
        nextPromptRefresh = Time.time + Mathf.Max(promptRefreshInterval, 0.05f);

        nearestSite = null;
        nearestDistance = float.MaxValue;

        foreach (var site in m.OpenSites)
        {
            if (site == null) continue;
            float d = Vector3.Distance(cam.position, site.transform.position);
            if (d < nearestDistance)
            {
                nearestDistance = d;
                nearestSite = site;
            }
        }
    }

    void UpdateObjective(M2Manager m)
    {
        bool show = m.IsPlaying && nearestSite != null;
        objectiveArrow.enabled = show;

        if (!show)
        {
            objectiveText.text = m.IsPlaying && m.SiteCount > 0 ? Module2Text.AllClear : "";
            objectiveText.color = doneColor;
            return;
        }

        Vector3 local = cam.InverseTransformDirection(nearestSite.transform.position - cam.position);
        float bearing = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        objectiveArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -bearing);

        objectiveText.text = Module2Text.Target(SiteLabel(nearestSite), nearestDistance);
        objectiveText.color = textColor;
    }

    void UpdatePrompt(M2Manager m)
    {
        string prompt = "";

        // Normally the prompt appears when you are close. During the opening guidance it stays up at
        // any distance, because the child does not yet know that picking things up is the verb.
        float reach = m.Onboarding ? float.MaxValue : promptDistance;
        if (m.IsPlaying && nearestSite != null && nearestDistance <= reach)
            prompt = SitePrompt(nearestSite);

        Fade(promptGroup, !string.IsNullOrEmpty(prompt));
        if (prompt == lastPrompt) return;

        lastPrompt = prompt;
        if (string.IsNullOrEmpty(prompt)) return;

        promptText.text = prompt;
        promptRect.sizeDelta = new Vector2(PromptW, Mathf.Max(56f, promptText.GetPreferredValues(prompt, PromptW - 40f, 0f).y + 26f));
    }

    void UpdateBeacon(M2Manager m)
    {
        bool show = m.IsPlaying && nearestSite != null;
        beacon.enabled = show;
        if (!show) return;

        beaconRoot.position = nearestSite.transform.position + Vector3.up * beaconHeight;
        beaconRoot.rotation = Quaternion.LookRotation(beaconRoot.position - cam.position, Vector3.up);

        // Pulse harder during the opening guidance, then settle to a gentle marker.
        float speed = m.Onboarding ? 6f : 2.5f;
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * speed);
        beaconRoot.localScale = Vector3.one * 0.001f * pulse;

        var colour = m.Onboarding ? doneColor : accentColor;
        beacon.color = new Color(colour.r, colour.g, colour.b, m.Onboarding ? 1f : 0.7f);
    }

    void UpdateToasts()
    {
        for (int i = toasts.Count - 1; i >= 0; i--)
        {
            var toast = toasts[i];
            float age = Time.time - toast.born;
            if (age > toastDuration)
            {
                Destroy(toast.rect.gameObject);
                toasts.RemoveAt(i);
                continue;
            }
            // Quick fade in, slow fade out over the last third of the life.
            float fadeOut = Mathf.InverseLerp(toastDuration, toastDuration * 0.66f, age);
            toast.group.alpha = Mathf.Min(Mathf.InverseLerp(0f, 0.18f, age), fadeOut);
        }

        // Newest on top; each one sits below the previous.
        float y = 0f;
        for (int i = 0; i < toasts.Count; i++)
        {
            var rect = toasts[i].rect;
            rect.anchoredPosition = new Vector2(0f, y);
            y -= rect.sizeDelta.y + 8f;
        }
    }

    void UpdateIntro(M2Manager m)
    {
        if (m.RoundStarted || introGroup.alpha <= 0f) return;

        introInputDebug.text = m.showInputDebug ? m.ConfirmDebug : "";

        // IntroArmed keeps a button that was already held from skipping the briefing on frame one.
        if (m.IntroArmed && m.IntroConfirmPressed()) m.BeginRound();
    }

    void ClearToasts()
    {
        foreach (var toast in toasts)
            if (toast.rect != null) Destroy(toast.rect.gameObject);
        toasts.Clear();
    }

    void Fade(CanvasGroup group, bool visible)
    {
        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.deltaTime * 6f);
    }

    static string SiteLabel(Component site)
    {
        if (site is Jar) return Module2Text.SiteJar;
        if (site is FishCon) return Module2Text.SiteFishBowl;
        if (site is WaterButton) return Module2Text.SiteContainer;
        return Module2Text.SiteGeneric;
    }

    static string SitePrompt(Component site)
    {
        if (site is Jar jar) return jar.LidHeld ? Module2Text.PromptJarCarrying : Module2Text.PromptJar;
        if (site is FishCon) return Module2Text.PromptFish;
        if (site is WaterButton) return Module2Text.PromptTip;
        return Module2Text.PromptGeneric;
    }

    // ------------------------------------------------------------------ construction

    void BuildHud()
    {
        hudRoot = NewCanvas("Module2HUD (runtime)", CanvasW, CanvasH, 10);

        var gameplay = NewRect("Gameplay", hudRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        gameplayGroup = gameplay.gameObject.AddComponent<CanvasGroup>();

        // --- top left: timer + day tag ---
        timerText = NewText("Timer", gameplay, "0:00", 54, textColor, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28, -22), new Vector2(240, 66), FontStyles.Bold);
        dayTagText = NewText("DayTag", gameplay, "", 22, mutedColor, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30, -88), new Vector2(460, 30));

        // --- top left, under the day tag: Energy bar and Bite counter. B4 specifies exactly these two
        // for M2; everything else on this HUD is additional.
        energyLabel = NewText("EnergyLabel", gameplay, "", 20, mutedColor, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30, -120), new Vector2(300, 26), FontStyles.Bold);
        var energyBack = NewImage("EnergyBack", gameplay, roundedSprite, barBackColor, Image.Type.Sliced,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30, -150), new Vector2(300, 16));
        energyFill = NewImage("EnergyFill", energyBack.rectTransform, roundedSprite, doneColor, Image.Type.Filled,
            Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        energyFill.fillMethod = Image.FillMethod.Horizontal;
        energyFill.fillAmount = 1f;

        biteText = NewText("Bites", gameplay, "", 30, textColor, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30, -182), new Vector2(300, 38), FontStyles.Bold);

        // --- top right: score ---
        scoreText = NewText("Score", gameplay, "", 40, textColor, TextAlignmentOptions.TopRight,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28, -26), new Vector2(320, 52), FontStyles.Bold);

        // --- top right under the score: breeding-site progress ---
        sitesText = NewText("Sites", gameplay, "", 24, accentColor, TextAlignmentOptions.TopRight,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28, -84), new Vector2(360, 30), FontStyles.Bold);
        NewImage("SitesIcon", gameplay, dropSprite, accentColor, Image.Type.Simple,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-300, -82), new Vector2(26, 26));
        var barBack = NewImage("SitesBarBack", gameplay, roundedSprite, barBackColor, Image.Type.Sliced,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28, -118), new Vector2(300, 14));
        sitesFill = NewImage("SitesBarFill", barBack.rectTransform, roundedSprite, accentColor, Image.Type.Filled,
            Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        sitesFill.fillMethod = Image.FillMethod.Horizontal;
        sitesFill.fillAmount = 0f;

        // --- centre: objective arrow + label ---
        objectiveArrow = NewImage("ObjectiveArrow", gameplay, arrowSprite, accentColor, Image.Type.Simple,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(54, 54));
        objectiveText = NewText("ObjectiveText", gameplay, "", 26, textColor, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 108), new Vector2(620, 34));

        // --- toasts, stacked under the timer ---
        toastContainer = NewRect("Toasts", gameplay, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(ToastW, 10));

        // --- bottom: contextual prompt ---
        promptRect = NewRect("Prompt", gameplay, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 54), new Vector2(PromptW, 56));
        promptGroup = promptRect.gameObject.AddComponent<CanvasGroup>();
        var promptBg = promptRect.gameObject.AddComponent<Image>();
        promptBg.sprite = roundedSprite; promptBg.type = Image.Type.Sliced; promptBg.color = panelColor; promptBg.raycastTarget = false;
        promptText = NewText("Text", promptRect, "", 28, textColor, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-40, 0));

        // A beacon parked on the nearest open container. On the UI layer, so the overlay camera draws
        // it through the house - a child cannot hunt for something occluded by a wall in 90 seconds.
        beaconRoot = NewCanvas("Module2HUD Beacon (runtime)", 200, 200, 9);
        beacon = NewImage("Beacon", beaconRoot, dropSprite, accentColor, Image.Type.Simple,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 120));
        beacon.enabled = false;

        // Full-field red flash on a bite: wordless feedback, which matters for players who cannot read
        // the counter quickly.
        biteFlash = NewImage("BiteFlash", hudRoot, roundedSprite, new Color(dangerColor.r, dangerColor.g, dangerColor.b, 0f),
            Image.Type.Sliced, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        BuildIntroPanel(hudRoot);
        BuildEndPanel(hudRoot);
    }

    void BuildIntroPanel(Transform parent)
    {
        var panel = NewRect("Intro", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 600));
        introGroup = panel.gameObject.AddComponent<CanvasGroup>();
        var bg = panel.gameObject.AddComponent<Image>();
        bg.sprite = roundedSprite; bg.type = Image.Type.Sliced; bg.color = new Color(panelColor.r, panelColor.g, panelColor.b, 0.9f); bg.raycastTarget = false;

        introKicker = NewText("Kicker", panel, "", 20, mutedColor, TextAlignmentOptions.Center,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(-40, 26), FontStyles.Bold);
        introTitle = NewText("Title", panel, "", 46, accentColor, TextAlignmentOptions.Center,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -46), new Vector2(-40, 60), FontStyles.Bold);
        introBody = NewText("Body", panel, "", 26, textColor, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -16), new Vector2(-80, -170));
        introFooter = NewText("Footer", panel, "", 30, doneColor, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 46), new Vector2(-40, 44), FontStyles.Bold);
        // On-headset input diagnostics: which confirm source is actually alive. M2Manager.showInputDebug
        // controls it; it exists because the A button fires only intermittently and guessing from the
        // filesystem got us nowhere.
        introInputDebug = NewText("InputDebug", panel, "", 17, mutedColor, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 16), new Vector2(-40, 26));
    }

    void BuildEndPanel(Transform parent)
    {
        var panel = NewRect("End", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 560));
        endGroup = panel.gameObject.AddComponent<CanvasGroup>();
        var bg = panel.gameObject.AddComponent<Image>();
        bg.sprite = roundedSprite; bg.type = Image.Type.Sliced; bg.color = endPanelColor; bg.raycastTarget = false;

        endKicker = NewText("Kicker", panel, "", 20, mutedColor, TextAlignmentOptions.Center,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(-40, 26), FontStyles.Bold);
        endTitle = NewText("Title", panel, "", 52, textColor, TextAlignmentOptions.Center,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -48), new Vector2(-40, 64), FontStyles.Bold);
        endSubtitle = NewText("Subtitle", panel, "", 25, mutedColor, TextAlignmentOptions.Center,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -94), new Vector2(-60, 70));
        endStats = NewText("Stats", panel, "", 30, textColor, TextAlignmentOptions.Center,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -176), new Vector2(-60, 170));
        endFact = NewText("Fact", panel, "", 23, accentColor, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 76), new Vector2(-80, 90), FontStyles.Italic);
        endFooter = NewText("Footer", panel, "", 28, doneColor, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(-40, 44), FontStyles.Bold);
    }

    RectTransform NewCanvas(string name, float w, float h, int sortingOrder)
    {
        var go = new GameObject(name);
        go.layer = uiLayer;
        var rt = go.AddComponent<RectTransform>();
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = sortingOrder;
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        rt.sizeDelta = new Vector2(w, h);
        rt.localScale = Vector3.one * 0.001f;
        return rt;
    }

    RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.layer = uiLayer;
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    Image NewImage(string name, Transform parent, Sprite sprite, Color color, Image.Type type, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = NewRect(name, parent, anchorMin, anchorMax, pivot, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = type;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 sizeDelta, FontStyles style = FontStyles.Normal)
    {
        var rt = NewRect(name, parent, anchorMin, anchorMax, pivot, pos, sizeDelta);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    // ------------------------------------------------------------------ procedural sprites

    void BuildSprites()
    {
        // Rounded rectangle, 9-sliced (border 10 px = 10 mm corners at multiplier 1).
        roundedSprite = MakeSprite(Raster(32, (u, v) =>
        {
            float r = 10f / 32f;
            Vector2 p = new Vector2(u - 0.5f, v - 0.5f);
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - new Vector2(0.5f - r, 0.5f - r);
            float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
            return d < 0f;
        }), new Vector4(10, 10, 10, 10));

        // Chevron arrow pointing up.
        arrowSprite = MakeSprite(Raster(64, (u, v) =>
            InTri(u, v, 0.5f, 0.95f, 0.08f, 0.1f, 0.92f, 0.1f) && !InTri(u, v, 0.5f, 0.5f, 0.24f, 0.05f, 0.76f, 0.05f)));

        // Water drop: circle plus a triangle up to the apex.
        dropSprite = MakeSprite(Raster(64, (u, v) =>
            InCircle(u, v, 0.5f, 0.36f, 0.28f) || InTri(u, v, 0.5f, 0.97f, 0.251f, 0.4885f, 0.749f, 0.4885f)));
    }

    static Sprite MakeSprite(Texture2D tex, Vector4 border = default)
    {
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    // Supersampled hard-edged shape -> antialiased alpha.
    static Texture2D Raster(int size, System.Func<float, float, bool> inside, int ss = 4)
    {
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float u = (x + (sx + 0.5f) / ss) / size;
                        float v = (y + (sy + 0.5f) / ss) / size;
                        if (inside(u, v)) hits++;
                    }
                byte a = (byte)(255 * hits / (ss * ss));
                px[y * size + x] = new Color32(255, 255, 255, a);
            }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static bool InCircle(float u, float v, float cx, float cy, float r)
    {
        float dx = u - cx, dy = v - cy;
        return dx * dx + dy * dy <= r * r;
    }

    static bool InTri(float u, float v, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float d1 = Sign(u, v, ax, ay, bx, by);
        float d2 = Sign(u, v, bx, by, cx, cy);
        float d3 = Sign(u, v, cx, cy, ax, ay);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0;
        bool pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    static float Sign(float px, float py, float ax, float ay, float bx, float by)
    {
        return (px - bx) * (ay - by) - (ax - bx) * (py - by);
    }
}
