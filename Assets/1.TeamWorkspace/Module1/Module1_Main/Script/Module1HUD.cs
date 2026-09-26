using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-headset HUD for Module 1 (play as a mosquito).
///
/// Everything is built from code at runtime - no prefab, no scene canvas - so a UI change never
/// has to touch the 38k-line scene and nothing can go missing on merge. Every object goes on the
/// UI layer: the Main Camera has an overlay camera in its URP stack that renders only that layer
/// with depth cleared, so the HUD is always drawn on top of the world (and on top of thermal vision).
///
/// Two canvases:
///   - a lazily-following world-space canvas in front of the player: timer, score, objective
///     arrow, energy/blood bars, quest list, contextual prompt, toasts, intro and end panels;
///   - a head-locked vignette canvas for the low-energy and danger warnings.
///
/// The HUD only reads game state (GameManager / PlayerMain / QuestSystem) each frame. Game code
/// pushes one-off events through Toast(), ShowIntro(), ShowEnd(), ShowQuestPanel(), SetDanger().
/// All player-facing copy lives in Module1Text at the bottom of this file.
///
/// GameManager adds this component to itself if the scene does not already carry one, so the
/// serialized fields below are only there for tuning in the inspector.
/// </summary>
public class Module1HUD : MonoBehaviour
{
    public static Module1HUD Instance { get; private set; }

    [Header("Placement (world units - the player rig is scaled 0.5, so 1.0 here reads as ~2 m)")]
    public float distance = 1.0f;
    public float followSpeed = 6f;
    public float vignetteDistance = 0.3f;

    [Header("Timing")]
    public float toastDuration = 3.5f;
    public float questPanelOnStart = 8f;
    public float questPanelOnChange = 5f;

    [Header("Thresholds")]
    [Range(0f, 1f)] public float lowEnergyFraction = 0.3f;

    [Header("Colours")]
    public Color nectarColor = new Color(1f, 0.78f, 0.2f);
    public Color bloodColor = new Color(0.85f, 0.18f, 0.18f);
    public Color doneColor = new Color(0.4f, 0.85f, 0.45f);
    public Color warnColor = new Color(1f, 0.55f, 0.1f);
    public Color dangerColor = new Color(0.9f, 0.05f, 0.05f);
    public Color rainColor = new Color(0.55f, 0.8f, 1f);
    public Color textColor = Color.white;
    public Color mutedColor = new Color(0.75f, 0.78f, 0.82f);
    public Color panelColor = new Color(0.04f, 0.05f, 0.07f, 0.72f);
    public Color barBackColor = new Color(0f, 0f, 0f, 0.55f);

    // Canvas size in millimetres (canvas scale is 0.001, so 1 unit = 1 mm at `distance`).
    const float CanvasW = 1100f, CanvasH = 620f;
    const float ToastW = 440f, PromptW = 600f;
    const int MaxToasts = 3;

    // ---- runtime references ----
    Transform cam;
    Vector3 smoothForward;
    int uiLayer;
    TMP_FontAsset font;

    RectTransform hudRoot, vignetteRoot;
    CanvasGroup gameplayGroup, questGroup, promptGroup, introGroup, endGroup;
    Image vignette;

    TextMeshProUGUI timerText, rainTag, scoreText, objectiveText, promptText;
    RectTransform promptRect;
    string lastPrompt;
    Image objectiveArrow;
    Image nectarFill, bloodFill;
    TextMeshProUGUI introTitle, introBody, introFooter;
    TextMeshProUGUI endTitle, endSubtitle, endStats, endFact, endFooter;

    RectTransform toastContainer;
    readonly List<ToastEntry> toasts = new List<ToastEntry>();
    QuestRow[] questRows;

    // ---- state ----
    float questPanelUntil;
    bool danger;
    bool lowEnergyToastArmed = true;
    int lastObjective = int.MinValue;
    float objectiveRefreshAt;
    GameObject[] flowers = System.Array.Empty<GameObject>();
    Human[] humans = System.Array.Empty<Human>();
    Wild_Mosquitos[] mosquitoes = System.Array.Empty<Wild_Mosquitos>();
    WaterContainer[] containers = System.Array.Empty<WaterContainer>();

    // ---- sprites (generated once) ----
    Sprite roundedSprite, dropSprite, flowerSprite, arrowSprite, tickSprite, vignetteSprite;

    class ToastEntry
    {
        public RectTransform rect;
        public CanvasGroup group;
        public float born;
    }

    class QuestRow
    {
        public Image box, tick;
        public TextMeshProUGUI title, hint;
    }

    // ------------------------------------------------------------------ lifecycle

    void Awake()
    {
        Instance = this;
        uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0) uiLayer = 5;
        font = TMP_Settings.defaultFontAsset;

        BuildSprites();
        BuildHud();
        BuildVignette();

        // Start hidden; GameManager decides what to show.
        gameplayGroup.alpha = 0f;
        questGroup.alpha = 0f;
        promptGroup.alpha = 0f;
        introGroup.alpha = 0f;
        endGroup.alpha = 0f;
        vignette.color = new Color(0, 0, 0, 0);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (hudRoot != null) Destroy(hudRoot.gameObject);
        if (vignetteRoot != null) Destroy(vignetteRoot.gameObject);
    }

    void LateUpdate()
    {
        if (!ResolveCamera()) return;
        Follow();

        var gm = GameManager.instance;
        var player = PlayerMain.instance;
        if (gm == null || player == null) return;

        bool playing = gm.IsPlaying;
        Fade(gameplayGroup, playing);

        UpdateStatus(gm);
        UpdateBars(player);
        UpdateQuests(gm, player);
        UpdateObjective(gm, player);
        UpdatePrompt(gm, player);
        UpdateToasts();
        UpdateVignette(player);

        if (introGroup.alpha > 0f)
            introFooter.alpha = 0.55f + 0.45f * Mathf.Sin(Time.time * 4f);
        if (endGroup.alpha > 0f && player.RestartAble)
            endFooter.alpha = 0.55f + 0.45f * Mathf.Sin(Time.time * 4f);
    }

    // ------------------------------------------------------------------ public API

    public void ShowIntro()
    {
        introTitle.text = Module1Text.IntroTitle;
        introBody.text = Module1Text.IntroBody;
        introFooter.text = Module1Text.IntroFooter;
        introGroup.alpha = 1f;
    }

    public void HideIntro()
    {
        introGroup.alpha = 0f;
    }

    /// <summary>Keeps the quest list on screen for a while (it is also shown while the left grip is held).</summary>
    public void ShowQuestPanel(float seconds)
    {
        questPanelUntil = Mathf.Max(questPanelUntil, Time.time + seconds);
    }

    public void OnQuestCompleted(int index)
    {
        ShowQuestPanel(questPanelOnChange);
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

    public void SetDanger(bool on)
    {
        danger = on;
    }

    /// <summary>True while a DangerSource has the player inside its volume.</summary>
    public bool DangerActive => danger;

    public void ShowEnd(GameManager.GameOverReason reason, int score, int questsDone, int questsTotal, int eggs)
    {
        endTitle.text = Module1Text.EndTitle(reason);
        endSubtitle.text = Module1Text.EndSubtitle(reason);
        endStats.text = Module1Text.EndStats(score, questsDone, questsTotal, eggs);
        endFact.text = Module1Text.RandomFact();
        endFooter.text = Module1Text.EndFooter;
        endTitle.color = reason == GameManager.GameOverReason.TimeOut ? textColor : warnColor;
        endGroup.alpha = 1f;
        promptGroup.alpha = 0f;
        questGroup.alpha = 0f;
    }

    // ------------------------------------------------------------------ per-frame updates

    bool ResolveCamera()
    {
        if (cam != null) return true;
        // Two cameras are tagged MainCamera (the thermal one is a child at the same pose), so
        // prefer the one PlayerMain drives.
        if (PlayerMain.instance != null && PlayerMain.instance.mainCamera != null)
            cam = PlayerMain.instance.mainCamera.transform;
        else if (Camera.main != null)
            cam = Camera.main.transform;
        if (cam != null) smoothForward = cam.forward;
        return cam != null;
    }

    // Translation follows the head instantly; the view direction is smoothed so the HUD trails a
    // little when the player turns instead of being glued to the eyes (same idea as Module 2's
    // CanvasFollower, but also following pitch, since a flying mosquito looks up and down a lot).
    void Follow()
    {
        smoothForward = Vector3.Slerp(smoothForward, cam.forward, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        hudRoot.position = cam.position + smoothForward * distance;
        hudRoot.rotation = Quaternion.LookRotation(smoothForward, cam.up);

        vignetteRoot.position = cam.position + cam.forward * vignetteDistance;
        vignetteRoot.rotation = cam.rotation;
    }

    void UpdateStatus(GameManager gm)
    {
        int t = Mathf.Max(0, gm.time);
        timerText.text = (t / 60) + ":" + (t % 60).ToString("00");
        timerText.color = t <= 10 ? dangerColor : t <= 30 ? warnColor : textColor;
        if (t <= 10 && gm.IsPlaying)
            timerText.alpha = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
        else
            timerText.alpha = 1f;

        scoreText.text = Module1Text.Score(gm.score);
        rainTag.text = gm.IsRain ? Module1Text.RainTag : "";
    }

    void UpdateBars(PlayerMain player)
    {
        float nec = player.Max_Nec > 0 ? Mathf.Clamp01(player.Current_Nec / player.Max_Nec) : 0f;
        float blood = player.Max_Blood > 0 ? Mathf.Clamp01(player.Current_Blood / player.Max_Blood) : 0f;
        nectarFill.fillAmount = nec;
        bloodFill.fillAmount = blood;

        bool low = nec < lowEnergyFraction;
        nectarFill.color = low
            ? Color.Lerp(nectarColor, warnColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 8f))
            : nectarColor;

        // One-shot warning when energy first drops low; re-armed once it has been refilled.
        if (low && lowEnergyToastArmed && GameManager.instance.IsPlaying)
        {
            lowEnergyToastArmed = false;
            Toast(Module1Text.LowEnergy, warnColor);
        }
        else if (nec > 0.5f)
            lowEnergyToastArmed = true;
    }

    void UpdateQuests(GameManager gm, PlayerMain player)
    {
        var quests = gm.quests;
        bool show = gm.IsPlaying && (player.L_gripValue || Time.time < questPanelUntil);
        Fade(questGroup, show);
        if (quests == null || quests.Quests == null) return;

        for (int i = 0; i < questRows.Length && i < quests.Quests.Length; i++)
        {
            var q = quests.Quests[i];
            var row = questRows[i];
            string title = q.progress != null ? q.title + "  " + q.progress : q.title;
            row.title.text = title;
            row.title.color = q.done ? doneColor : textColor;
            row.hint.text = q.done ? Module1Text.QuestDone : q.hint;
            row.box.color = q.done ? doneColor : new Color(1f, 1f, 1f, 0.18f);
            row.tick.enabled = q.done;
        }
    }

    // Arrow + label under the timer pointing at the nearest thing the player needs next.
    void UpdateObjective(GameManager gm, PlayerMain player)
    {
        if (Time.time >= objectiveRefreshAt)
        {
            objectiveRefreshAt = Time.time + 3f;
            flowers = GameObject.FindGameObjectsWithTag("Flower");
            humans = FindObjectsByType<Human>(FindObjectsSortMode.None);
            mosquitoes = FindObjectsByType<Wild_Mosquitos>(FindObjectsSortMode.None);
            containers = FindObjectsByType<WaterContainer>(FindObjectsSortMode.None);
        }

        int objective = gm.quests != null ? gm.quests.CurrentObjective : -1;
        float nec = player.Max_Nec > 0 ? player.Current_Nec / player.Max_Nec : 1f;
        bool bloodFull = player.Current_Blood >= player.Max_Blood;

        // A short hint the first time each objective becomes current (this is where the thermal
        // vision tip lives).
        if (objective != lastObjective)
        {
            if (lastObjective != int.MinValue && gm.IsPlaying)
                Toast(Module1Text.ObjectiveHint(objective));
            lastObjective = objective;
        }

        string label;
        Vector3 target;
        bool found;
        if (nec < lowEnergyFraction + 0.05f)
        {
            found = Nearest(flowers, out target);
            label = Module1Text.TargetFlowerUrgent;
        }
        else
        {
            switch (objective)
            {
                case 0:
                    found = Nearest(flowers, out target); label = Module1Text.TargetFlower; break;
                case 1:
                    found = NearestMale(out target); label = Module1Text.TargetMale; break;
                case 2:
                    found = Nearest(humans, out target); label = Module1Text.TargetHuman; break;
                default:
                    if (bloodFull && player.isMate)
                    {
                        found = NearestFilledContainer(out target); label = Module1Text.TargetContainer;
                    }
                    else if (!player.isMate)
                    {
                        found = NearestMale(out target); label = Module1Text.TargetMale;
                    }
                    else
                    {
                        found = Nearest(humans, out target); label = Module1Text.TargetHuman;
                    }
                    break;
            }
        }

        if (!found)
        {
            objectiveArrow.enabled = false;
            objectiveText.text = Module1Text.TargetNone(label);
            objectiveText.color = mutedColor;
            return;
        }

        Vector3 to = target - cam.position;
        float dist = to.magnitude;
        float vertical = to.y;
        to.y = 0f;
        Vector3 fwd = smoothForward; fwd.y = 0f;
        float angle = to.sqrMagnitude > 0.0001f && fwd.sqrMagnitude > 0.0001f ? Vector3.SignedAngle(fwd, to, Vector3.up) : 0f;

        objectiveArrow.enabled = dist > 1.5f;
        objectiveArrow.rectTransform.localRotation = Quaternion.Euler(0, 0, -angle);
        objectiveText.color = textColor;
        objectiveText.text = Module1Text.Target(label, dist, vertical);
    }

    void UpdatePrompt(GameManager gm, PlayerMain player)
    {
        string msg = null;
        Color color = textColor;

        if (danger)
        {
            msg = Module1Text.DangerPrompt;
            color = dangerColor;
        }
        else
        {
            bool bloodFull = player.Current_Blood >= player.Max_Blood;
            bool necFull = player.Current_Nec >= player.Max_Nec;
            bool holding = player.R_primaryValue;
            switch (player.CurrentInteraction)
            {
                case PlayerMain.Interaction.Flower:
                    msg = necFull ? Module1Text.EnergyFull : holding ? Module1Text.DrinkingNectar : Module1Text.PromptDrinkNectar;
                    break;
                case PlayerMain.Interaction.Human:
                    if (bloodFull) msg = player.isMate ? Module1Text.BloodFullMated : Module1Text.BloodFullNotMated;
                    else msg = holding ? Module1Text.DrinkingBlood : Module1Text.PromptDrinkBlood;
                    break;
                case PlayerMain.Interaction.Mate:
                    msg = player.isMate ? Module1Text.AlreadyMated : Module1Text.PromptMate;
                    break;
                case PlayerMain.Interaction.FemaleMosquito:
                    msg = Module1Text.FemaleMosquito;
                    break;
                case PlayerMain.Interaction.Container:
                    var c = player.CurrentContainer;
                    if (c != null && !c.isFill) { msg = Module1Text.ContainerDry; color = mutedColor; }
                    else if (!player.isMate) { msg = Module1Text.NeedMate; color = mutedColor; }
                    else if (!bloodFull) { msg = Module1Text.NeedBlood; color = mutedColor; }
                    else msg = Module1Text.PromptLayEggs;
                    break;
            }
        }

        bool show = gm.IsPlaying && msg != null;
        if (show)
        {
            if (msg != lastPrompt)
            {
                lastPrompt = msg;
                promptText.text = msg;
                promptRect.sizeDelta = new Vector2(PromptW, Mathf.Max(52f, promptText.GetPreferredValues(msg, PromptW - 28f, 0f).y + 18f));
            }
            promptText.color = color;
        }
        Fade(promptGroup, show, 12f);
    }

    void UpdateToasts()
    {
        float y = 0f;
        for (int i = toasts.Count - 1; i >= 0; i--)
        {
            var t = toasts[i];
            float age = Time.time - t.born;
            if (age > toastDuration)
            {
                Destroy(t.rect.gameObject);
                toasts.RemoveAt(i);
            }
        }
        for (int i = 0; i < toasts.Count; i++)
        {
            var t = toasts[i];
            float age = Time.time - t.born;
            float a = Mathf.Min(age / 0.2f, (toastDuration - age) / 0.4f);
            t.group.alpha = Mathf.Clamp01(a);
            var pos = t.rect.anchoredPosition;
            pos.y = Mathf.Lerp(pos.y, -y, 1f - Mathf.Exp(-14f * Time.deltaTime));
            t.rect.anchoredPosition = pos;
            y += t.rect.sizeDelta.y + 6f;
        }
    }

    void UpdateVignette(PlayerMain player)
    {
        Color c;
        float a;
        if (danger)
        {
            c = dangerColor;
            a = 0.6f + 0.15f * Mathf.Sin(Time.time * 10f);
        }
        else
        {
            float nec = player.Max_Nec > 0 ? player.Current_Nec / player.Max_Nec : 1f;
            float t = lowEnergyFraction > 0 ? Mathf.Clamp01((lowEnergyFraction - nec) / lowEnergyFraction) : 0f;
            c = warnColor;
            a = t * (0.45f + 0.15f * Mathf.Sin(Time.time * 5f));
        }
        if (GameManager.instance != null && !GameManager.instance.IsPlaying) a = 0f;
        c.a = Mathf.MoveTowards(vignette.color.a, a, Time.deltaTime * 2f);
        vignette.color = c;
    }

    static void Fade(CanvasGroup g, bool visible, float speed = 6f)
    {
        g.alpha = Mathf.MoveTowards(g.alpha, visible ? 1f : 0f, Time.deltaTime * speed);
    }

    // ------------------------------------------------------------------ target search

    bool Nearest(GameObject[] list, out Vector3 pos)
    {
        pos = Vector3.zero;
        float best = float.MaxValue;
        foreach (var go in list)
        {
            if (go == null) continue;
            float d = (go.transform.position - cam.position).sqrMagnitude;
            if (d < best) { best = d; pos = go.transform.position; }
        }
        return best < float.MaxValue;
    }

    bool Nearest<T>(T[] list, out Vector3 pos) where T : Component
    {
        pos = Vector3.zero;
        float best = float.MaxValue;
        foreach (var c in list)
        {
            if (c == null) continue;
            float d = (c.transform.position - cam.position).sqrMagnitude;
            if (d < best) { best = d; pos = c.transform.position; }
        }
        return best < float.MaxValue;
    }

    bool NearestMale(out Vector3 pos)
    {
        pos = Vector3.zero;
        float best = float.MaxValue;
        foreach (var m in mosquitoes)
        {
            if (m == null || m.Gender != Wild_Mosquitos.genderlist.male) continue;
            float d = (m.transform.position - cam.position).sqrMagnitude;
            if (d < best) { best = d; pos = m.transform.position; }
        }
        return best < float.MaxValue;
    }

    bool NearestFilledContainer(out Vector3 pos)
    {
        pos = Vector3.zero;
        float best = float.MaxValue;
        foreach (var c in containers)
        {
            if (c == null || !c.isFill) continue;
            float d = (c.transform.position - cam.position).sqrMagnitude;
            if (d < best) { best = d; pos = c.transform.position; }
        }
        return best < float.MaxValue;
    }

    // ------------------------------------------------------------------ construction

    void BuildHud()
    {
        hudRoot = NewCanvas("Module1HUD (runtime)", CanvasW, CanvasH, 10);
        var root = hudRoot;

        // --- gameplay layer: everything that is only meaningful while playing
        var gameplay = NewRect("Gameplay", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        gameplayGroup = gameplay.gameObject.AddComponent<CanvasGroup>();

        // Timer, top centre
        timerText = NewText("Timer", gameplay, "5:00", 60, textColor, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(320, 70), FontStyles.Bold);
        rainTag = NewText("RainTag", gameplay, "", 22, rainColor, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -76), new Vector2(420, 28));

        // Objective arrow + label
        var objective = NewRect("Objective", gameplay, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -104), new Vector2(420, 40));
        objectiveArrow = NewImage("Arrow", objective, arrowSprite, textColor, Image.Type.Simple,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(24, 0), new Vector2(30, 30));
        objectiveText = NewText("Label", objective, "", 26, textColor, TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(50, 0), new Vector2(-50, 40));

        // Score, top left
        scoreText = NewText("Score", gameplay, "Score 0", 34, textColor, TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -18), new Vector2(300, 44), FontStyles.Bold);

        // Energy (nectar) bar, bottom left; blood bar, bottom right (fills toward the centre)
        nectarFill = BuildBar(gameplay, Module1Text.EnergyLabel, flowerSprite, nectarColor, right: false);
        bloodFill = BuildBar(gameplay, Module1Text.BloodLabel, dropSprite, bloodColor, right: true);

        // Contextual prompt, bottom centre
        var prompt = NewRect("Prompt", gameplay, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 92), new Vector2(PromptW, 52));
        promptRect = prompt;
        promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();
        var promptBg = prompt.gameObject.AddComponent<Image>();
        promptBg.sprite = roundedSprite; promptBg.type = Image.Type.Sliced; promptBg.color = panelColor; promptBg.raycastTarget = false;
        promptText = NewText("Text", prompt, "", 28, textColor, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-28, 0));

        // Toasts, under the objective line
        toastContainer = NewRect("Toasts", gameplay, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -166), new Vector2(ToastW, 200));

        // Quest list, right side
        BuildQuestPanel(gameplay);

        // --- modal panels (outside the gameplay group so they stay visible when it fades)
        var modals = NewRect("Modals", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        BuildIntroPanel(modals);
        BuildEndPanel(modals);
    }

    Image BuildBar(Transform parent, string label, Sprite icon, Color color, bool right)
    {
        float ax = right ? 1f : 0f;
        Vector2 anchor = new Vector2(ax, 0f);
        float sign = right ? -1f : 1f;
        var bar = NewRect(right ? "BloodBar" : "EnergyBar", parent, anchor, anchor, anchor, new Vector2(sign * 24, 22), new Vector2(320, 48));

        NewImage("Icon", bar, icon, color, Image.Type.Simple, anchor, anchor, anchor, Vector2.zero, new Vector2(46, 46));
        NewText("Label", bar, label, 21, mutedColor, right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left,
            anchor, anchor, anchor, new Vector2(sign * 56, 26), new Vector2(250, 24));
        var back = NewImage("Back", bar, roundedSprite, barBackColor, Image.Type.Sliced, anchor, anchor, anchor, new Vector2(sign * 56, 0), new Vector2(260, 22));
        back.pixelsPerUnitMultiplier = 1f;
        var fill = NewImage("Fill", back.rectTransform, roundedSprite, color, Image.Type.Filled, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-6, -6));
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = right ? (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0.5f;
        return fill;
    }

    void BuildQuestPanel(Transform parent)
    {
        var panel = NewRect("Quests", parent, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20, 20), new Vector2(310, 350));
        questGroup = panel.gameObject.AddComponent<CanvasGroup>();
        var bg = panel.gameObject.AddComponent<Image>();
        bg.sprite = roundedSprite; bg.type = Image.Type.Sliced; bg.color = panelColor; bg.raycastTarget = false;

        NewText("Title", panel, Module1Text.QuestPanelTitle, 24, mutedColor, TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(-32, 30), FontStyles.Bold);
        NewText("GripHint", panel, Module1Text.QuestPanelHint, 16, mutedColor, TextAlignmentOptions.Right,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(-32, 22));

        questRows = new QuestRow[QuestSystem.QuestCount];
        for (int i = 0; i < questRows.Length; i++)
        {
            float y = -52 - i * 66;
            var row = new QuestRow();
            row.box = NewImage("Box" + i, panel, roundedSprite, new Color(1, 1, 1, 0.18f), Image.Type.Sliced,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16, y), new Vector2(28, 28));
            row.box.pixelsPerUnitMultiplier = 1.4f;
            row.tick = NewImage("Tick", row.box.rectTransform, tickSprite, Color.white, Image.Type.Simple,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-4, -4));
            row.tick.enabled = false;
            row.title = NewText("Title" + i, panel, "", 24, textColor, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(54, y + 2), new Vector2(-70, 30));
            row.hint = NewText("Hint" + i, panel, "", 18, mutedColor, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(54, y - 26), new Vector2(-70, 40));
            questRows[i] = row;
        }
    }

    void BuildIntroPanel(Transform parent)
    {
        var panel = NewRect("Intro", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(940, 600));
        introGroup = panel.gameObject.AddComponent<CanvasGroup>();
        var bg = panel.gameObject.AddComponent<Image>();
        bg.sprite = roundedSprite; bg.type = Image.Type.Sliced; bg.color = new Color(panelColor.r, panelColor.g, panelColor.b, 0.9f); bg.raycastTarget = false;

        introTitle = NewText("Title", panel, "", 48, nectarColor, TextAlignmentOptions.Center,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(-40, 60), FontStyles.Bold);
        introBody = NewText("Body", panel, "", 26, textColor, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -16), new Vector2(-80, -170));
        introFooter = NewText("Footer", panel, "", 30, doneColor, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(-40, 44), FontStyles.Bold);
    }

    void BuildEndPanel(Transform parent)
    {
        var panel = NewRect("End", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(800, 540));
        endGroup = panel.gameObject.AddComponent<CanvasGroup>();
        var bg = panel.gameObject.AddComponent<Image>();
        bg.sprite = roundedSprite; bg.type = Image.Type.Sliced; bg.color = new Color(panelColor.r, panelColor.g, panelColor.b, 0.9f); bg.raycastTarget = false;

        endTitle = NewText("Title", panel, "", 52, textColor, TextAlignmentOptions.Center,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(-40, 64), FontStyles.Bold);
        endSubtitle = NewText("Subtitle", panel, "", 25, mutedColor, TextAlignmentOptions.Center,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -94), new Vector2(-60, 70));
        endStats = NewText("Stats", panel, "", 32, textColor, TextAlignmentOptions.Center,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -180), new Vector2(-60, 140));
        endFact = NewText("Fact", panel, "", 23, nectarColor, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 76), new Vector2(-80, 90), FontStyles.Italic);
        endFooter = NewText("Footer", panel, "", 28, doneColor, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(-40, 44), FontStyles.Bold);
    }

    void BuildVignette()
    {
        // Big enough to cover the headset FOV at vignetteDistance; only the edges are opaque.
        // 1.2 m square at vignetteDistance: with a ~90-110 deg per-eye FOV the edge of the view
        // lands around half way up the radial gradient, so the central ~25 deg stays clear and
        // only the periphery colours up.
        vignetteRoot = NewCanvas("Module1HUD Vignette (runtime)", 1200, 1200, 5);
        vignette = NewImage("Vignette", vignetteRoot, vignetteSprite, new Color(0, 0, 0, 0), Image.Type.Simple,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
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

        // Blood drop: circle plus a triangle up to the apex, tangent to the circle.
        dropSprite = MakeSprite(Raster(64, (u, v) =>
            InCircle(u, v, 0.5f, 0.36f, 0.28f) || InTri(u, v, 0.5f, 0.97f, 0.251f, 0.4885f, 0.749f, 0.4885f)));

        // Flower: centre plus five petals.
        flowerSprite = MakeSprite(Raster(64, (u, v) =>
        {
            if (InCircle(u, v, 0.5f, 0.5f, 0.12f)) return true;
            for (int k = 0; k < 5; k++)
            {
                float a = (90f + 72f * k) * Mathf.Deg2Rad;
                if (InCircle(u, v, 0.5f + 0.28f * Mathf.Cos(a), 0.5f + 0.28f * Mathf.Sin(a), 0.16f)) return true;
            }
            return false;
        }));

        // Chevron arrow pointing up.
        arrowSprite = MakeSprite(Raster(64, (u, v) =>
            InTri(u, v, 0.5f, 0.95f, 0.08f, 0.1f, 0.92f, 0.1f) && !InTri(u, v, 0.5f, 0.5f, 0.24f, 0.05f, 0.76f, 0.05f)));

        // Tick mark.
        tickSprite = MakeSprite(Raster(32, (u, v) =>
            SegDist(u, v, 0.2f, 0.5f, 0.42f, 0.28f) < 0.09f || SegDist(u, v, 0.42f, 0.28f, 0.82f, 0.72f) < 0.09f));

        // Radial vignette: clear in the middle, solid at the edges.
        vignetteSprite = MakeSprite(RasterAlpha(128, (u, v) =>
        {
            float d = new Vector2(u - 0.5f, v - 0.5f).magnitude / 0.7071f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.55f, d));
        }));
    }

    static Sprite MakeSprite(Texture2D tex, Vector4 border = default)
    {
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    // Supersampled hard-edged shape -> antialiased alpha.
    static Texture2D Raster(int size, System.Func<float, float, bool> inside, int ss = 4)
    {
        var px = new Color32[size * size];
        float inv = 1f / (size * ss);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                        if (inside((x * ss + sx + 0.5f) * inv, (y * ss + sy + 0.5f) * inv)) hits++;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * hits / (ss * ss)));
            }
        return ToTexture(size, px);
    }

    static Texture2D RasterAlpha(int size, System.Func<float, float, float> alpha)
    {
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(alpha((x + 0.5f) / size, (y + 0.5f) / size))));
        return ToTexture(size, px);
    }

    static Texture2D ToTexture(int size, Color32[] px)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    static bool InCircle(float u, float v, float cx, float cy, float r)
    {
        float dx = u - cx, dy = v - cy;
        return dx * dx + dy * dy < r * r;
    }

    static bool InTri(float u, float v, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float d1 = (u - bx) * (ay - by) - (ax - bx) * (v - by);
        float d2 = (u - cx) * (by - cy) - (bx - cx) * (v - cy);
        float d3 = (u - ax) * (cy - ay) - (cx - ax) * (v - ay);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0;
        bool pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    static float SegDist(float u, float v, float ax, float ay, float bx, float by)
    {
        float vx = bx - ax, vy = by - ay;
        float t = Mathf.Clamp01(((u - ax) * vx + (v - ay) * vy) / (vx * vx + vy * vy));
        float px = ax + t * vx - u, py = ay + t * vy - v;
        return Mathf.Sqrt(px * px + py * py);
    }
}

/// <summary>
/// Every string the Module 1 HUD shows, in one place. Only glyphs from the baked LiberationSans
/// atlas (ASCII + Latin-1 + bullet) are safe here; the fallback font is dynamic and may not be
/// available on device.
/// </summary>
public static class Module1Text
{
    public const string EnergyLabel = "ENERGY (nectar)";
    public const string BloodLabel = "BLOOD";
    public const string RainTag = "Raining - containers are filling up";
    public static string Score(int score) => "Score " + score;

    // Quests
    public const string QuestPanelTitle = "LIFE CYCLE";
    public const string QuestPanelHint = "hold left grip to show";
    public const string QuestNectar = "Drink nectar";
    public const string QuestNectarHint = "Find a flower and hold A. Nectar is your energy.";
    public const string QuestMate = "Find a mate";
    public const string QuestMateHint = "Fly next to a male mosquito and press A.";
    public const string QuestBlood = "Drink blood";
    public const string QuestBloodHint = "Hold A on a person. Right trigger = thermal vision.";
    public const string QuestEggs = "Lay eggs";
    public const string QuestEggsHint = "Full blood meal, then press A in a container of rainwater.";
    public const string QuestDone = "Done";
    public static string QuestComplete(string title) => title + " complete";

    // Objective arrow
    public const string TargetFlower = "Flower";
    public const string TargetFlowerUrgent = "Flower - energy low!";
    public const string TargetMale = "Male mosquito";
    public const string TargetHuman = "Person";
    public const string TargetContainer = "Rainwater container";
    public static string TargetNone(string what) => what + ": none nearby";
    public static string Target(string what, float dist, float vertical)
    {
        string s = what + "  " + Mathf.RoundToInt(dist) + " m";
        if (vertical > 2f) s += "  (above)";
        else if (vertical < -2f) s += "  (below)";
        return s;
    }

    public static string ObjectiveHint(int objective)
    {
        switch (objective)
        {
            case 0: return "Flowers grow all around the gardens - hold A on one to drink";
            case 1: return "Look for a male mosquito and press A next to it";
            case 2: return "Hold the right trigger: thermal vision shows people, even through walls";
            case 3: return "Jars, buckets and old tyres hold rainwater - lay your eggs there";
            default: return "Life cycle complete! Keep laying eggs for more points";
        }
    }

    // Prompts
    public const string PromptDrinkNectar = "Hold A to drink nectar";
    public const string DrinkingNectar = "Drinking nectar...";
    public const string EnergyFull = "Energy full";
    public const string PromptDrinkBlood = "Hold A to drink blood";
    public const string DrinkingBlood = "Feeding...";
    public const string BloodFullMated = "Blood full - find a container of rainwater and lay your eggs";
    public const string BloodFullNotMated = "Blood full - now find a mate";
    public const string PromptMate = "Press A to mate";
    public const string AlreadyMated = "Already mated";
    public const string FemaleMosquito = "That's a female - you need a male to mate";
    public const string ContainerDry = "This container is dry - wait for the rain";
    public const string NeedMate = "You need a mate before you can lay eggs";
    public const string NeedBlood = "You need a full blood meal to lay eggs";
    public const string PromptLayEggs = "Press A to lay eggs";
    public const string DangerPrompt = "DANGER - get away!";

    // Toasts
    public const string StartToast = "Go! Find a flower first - nectar keeps you flying";
    public const string LowEnergy = "Energy low - find a flower!";
    public const string RainStarted = "It's raining - containers are filling with water";
    public const string RainStopped = "The rain has stopped";
    public const string OneMinuteLeft = "One minute left!";
    public const string ThirtySecondsLeft = "30 seconds left!";
    public const string EggsLaid = "Eggs laid";

    // Intro
    public const string IntroTitle = "You are an Aedes mosquito";
    public const string IntroBody =
        "Complete your life cycle before the clock runs out:\n" +
        "<color=#FFC733>1.</color>  Drink nectar from flowers - it is your energy. Run out and you fall.\n" +
        "<color=#FFC733>2.</color>  Find a male mosquito and mate.\n" +
        "<color=#FFC733>3.</color>  Drink blood from a person - you need it to make eggs.\n" +
        "<color=#FFC733>4.</color>  Lay eggs in a container of rainwater. Rain comes mid-game.\n" +
        "\n" +
        "<color=#BFC7D1>Left stick</color>  fly       <color=#BFC7D1>Right stick</color>  turn, up / down\n" +
        "<color=#BFC7D1>A (hold)</color>  drink, mate, lay eggs\n" +
        "<color=#BFC7D1>Right trigger (hold)</color>  thermal vision - see people through walls\n" +
        "<color=#BFC7D1>Left grip (hold)</color>  your life-cycle list";
    public const string IntroFooter = "Press A to begin";

    // End
    public const string EndFooter = "Press any button to fly again";
    public static string EndTitle(GameManager.GameOverReason reason)
    {
        switch (reason)
        {
            case GameManager.GameOverReason.Starved: return "Out of energy";
            case GameManager.GameOverReason.Eaten: return "Eaten!";
            default: return "Time's up";
        }
    }
    public static string EndSubtitle(GameManager.GameOverReason reason)
    {
        switch (reason)
        {
            case GameManager.GameOverReason.Starved: return "Mosquitoes burn nectar to fly. Yours ran out - keep the energy bar topped up from flowers.";
            case GameManager.GameOverReason.Eaten: return "Predators like dragonflies and fish eat mosquitoes - one reason guppies are put in water jars.";
            default: return "A mosquito's day is over. Here is how your life cycle went.";
        }
    }
    public static string EndStats(int score, int questsDone, int questsTotal, int eggs)
    {
        return "Score  " + score + "\n" +
               "Life cycle  " + questsDone + " / " + questsTotal + "\n" +
               "Eggs laid  " + eggs;
    }

    static readonly string[] Facts =
    {
        "One Aedes female lays about 100 eggs per clutch, in as little water as a bottle cap holds.",
        "Aedes eggs can survive months in a dry container and hatch as soon as the rain returns.",
        "Only female mosquitoes bite - they need the protein in blood to produce eggs.",
        "Emptying and scrubbing water containers once a week breaks the mosquito life cycle.",
        "Aedes mosquitoes bite mostly during the day, and they rarely fly further than 200 m in their life.",
    };
    public static string RandomFact() => Facts[Random.Range(0, Facts.Length)];
}
