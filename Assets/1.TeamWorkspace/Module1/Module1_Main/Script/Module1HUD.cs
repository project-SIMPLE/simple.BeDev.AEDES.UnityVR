using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-headset HUD for Module 1, in the same style as Module2HUD: dark rounded panels, a timer and
/// bars top left, the score top right, toasts, a prompt at the bottom and end cards - all built from
/// code at runtime, so the 2.9 MB scene never has to change for a UI tweak.
///
/// It is a skin over the team's game, not a replacement for it. GameManager, PlayerMain and
/// QuestSystem keep driving their scene UI (PlayerUI under the Main Camera) exactly as before; this
/// component switches off that UI's Canvas components so it is not drawn, then mirrors its state
/// every frame: which panels are active, the death message, the warnings.
///
/// The Lao text is the team's: every label is read from the scene object that showed it before
/// (quest names, death and time-out screens, warnings), so editing those texts in the scene still
/// changes what the player reads. Only the blood warning existed solely as a picture (Warning2 UI.png),
/// so its sentence is copied into <see cref="BloodWarningText"/>.
///
/// GameManager adds this component to itself in Awake.
/// </summary>
public class Module1HUD : MonoBehaviour
{
    [Header("Placement (in the rig's own units: the Module 1 rig is scaled 1:100)")]
    [Tooltip("How far in front of the eyes the HUD floats, in rig units. It is scaled with the distance so it always covers the same part of the view as Module 2's HUD at 1.2 m. Kept well out: TextMesh Pro draws nothing at the tiny world scale a 1.2 m HUD would need inside this rig.")]
    public float distance = 10f;
    public float followSpeed = 6f;

    [Header("Text")]
    [Tooltip("Font for all HUD text. Empty: the full Lao font from Resources, else the scene UI's font.")]
    public TMP_FontAsset fontOverride;

    [Header("Timing")]
    public float toastDuration = 3.5f;

    [Header("Colours (same palette as Module2HUD)")]
    public Color accentColor = new Color(0.45f, 0.82f, 1f);
    public Color doneColor = new Color(0.4f, 0.85f, 0.45f);
    public Color warnColor = new Color(1f, 0.55f, 0.1f);
    public Color dangerColor = new Color(0.9f, 0.18f, 0.18f);
    public Color bloodColor = new Color(0.86f, 0.12f, 0.16f);
    public Color nectarColor = new Color(1f, 0.82f, 0.25f);
    public Color textColor = Color.white;
    public Color mutedColor = new Color(0.75f, 0.78f, 0.82f);
    public Color panelColor = new Color(0.04f, 0.05f, 0.07f, 0.72f);
    public Color endPanelColor = new Color(0.10f, 0.05f, 0.04f, 0.92f);
    public Color barBackColor = new Color(0f, 0f, 0f, 0.55f);

    // The blood warning was only ever a picture with the sentence painted in (Warning2 UI.png).
    const string BloodWarningText = "ຕ້ອງການໂປຣຕີນ (ເລືອດ) ດ່ວນ!";
    // Fallbacks, used only if the scene object they normally come from is missing.
    const string EnergyWord = "ພະລັງງານ";          // from PlayerMain.DeathMessage "ພະລັງງານໝົດ"
    const string BloodWord = "ເລືອດ";              // from the quest "ດື່ມເລືອດ"
    const string ScoreWord = "ຄະແນນ";              // from the time-out screen "ຄະແນນ:"

    const float CanvasW = 1100f, CanvasH = 620f;
    const float ToastW = 460f, PromptW = 640f;
    const int MaxToasts = 3;

    // ---- game ----
    GameManager gm;
    PlayerMain player;
    QuestSystem quests;
    Image legacyStatus;

    // ---- strings taken from the scene ----
    string questTitle, deathTitle, deathFooter, timeOutTitle, timeOutFooter, dangerText, nectarWarningText;
    string[] timeOutLabels;
    string energyLabelText = EnergyWord, bloodLabelText = BloodWord, scoreWord = ScoreWord;

    // ---- runtime UI ----
    Transform cam;
    Vector3 smoothForward;
    int uiLayer;
    TMP_FontAsset font;
    RectTransform hudRoot;
    CanvasGroup gameplayGroup, promptGroup, questGroup, deathGroup, timeOutGroup;
    TextMeshProUGUI timerText, scoreText, energyLabel, bloodLabel, promptText;
    Image energyFill, bloodFill, rainIcon, statusDot, dangerVignette, promptBg;
    RectTransform promptRect;
    string lastPrompt;
    TextMeshProUGUI questTitleText;
    readonly TextMeshProUGUI[] questRows = new TextMeshProUGUI[4];
    readonly Image[] questMarks = new Image[4];
    TextMeshProUGUI deathTitleText, deathMessageText, deathFooterText;
    TextMeshProUGUI timeOutTitleText, timeOutLabelsText, timeOutValuesText, timeOutFooterText;
    RectTransform toastContainer;
    readonly List<Toast> toasts = new List<Toast>();
    Sprite roundedSprite, dropSprite, ringSprite, dotSprite;

    readonly bool[] questWasDone = new bool[4];
    bool questsPrimed;

    class Toast { public RectTransform rect; public CanvasGroup group; public float born; }

    // ------------------------------------------------------------------ lifecycle

    void Awake()
    {
        uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0) uiLayer = 5;
        gm = GetComponent<GameManager>();
    }

    void Start()
    {
        if (gm == null) gm = GameManager.instance;
        player = gm != null ? gm.player : PlayerMain.instance;
        quests = FindFirstObjectByType<QuestSystem>();

        ReadSceneText();
        font = fontOverride;
        if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/Lao_SomVang Full SDF");
        if (font == null && gm != null && gm.scoretext != null) font = gm.scoretext.font;

        HideSceneUI();
        BuildSprites();
        BuildHud();
    }

    void OnDestroy()
    {
        if (hudRoot != null) Destroy(hudRoot.gameObject);
    }

    void LateUpdate()
    {
        if (hudRoot == null || gm == null || player == null) return;
        if (!ResolveCamera()) return;
        Follow();

        bool dead = gm.DeathUI != null && gm.DeathUI.activeSelf;
        bool timedOut = gm.TimeOutUI != null && gm.TimeOutUI.activeSelf;
        bool ended = dead || timedOut || player.RestartAble;

        Fade(gameplayGroup, !ended);
        UpdateStatus();
        UpdateBars();
        UpdateQuests(ended);
        UpdatePrompt(ended);
        UpdateDanger(ended);
        UpdateToasts();
        UpdateEnd(dead, timedOut);
    }

    // ------------------------------------------------------------------ scene text

    /// <summary>Takes every label from the scene object that used to show it, so the team's Lao stays the source.</summary>
    void ReadSceneText()
    {
        if (gm == null) return;
        questTitle = ChildText(gm.questUI, "QuestText");
        deathTitle = ChildText(gm.DeathUI, "DeathText");
        deathFooter = ChildText(gm.DeathUI, "DeathText (1)");
        timeOutTitle = ChildText(gm.TimeOutUI, "DeathText");
        timeOutFooter = ChildText(gm.TimeOutUI, "DeathText (1)");
        string labels = ChildText(gm.TimeOutUI, "DeathText (2)");
        if (!string.IsNullOrEmpty(labels))
        {
            timeOutLabels = labels.Replace("\r", "").Split('\n');
            if (timeOutLabels.Length > 0) scoreWord = timeOutLabels[0].Trim().TrimEnd(':', ' ');
        }
        dangerText = AnyText(gm.DangerUI);
        if (quests != null) nectarWarningText = AnyText(quests.NecWarning);
        if (player != null && !string.IsNullOrEmpty(player.DeathMessage) && player.DeathMessage.EndsWith("ໝົດ"))
            energyLabelText = player.DeathMessage.Substring(0, player.DeathMessage.Length - "ໝົດ".Length);
    }

    static string ChildText(GameObject root, string child)
    {
        if (root == null) return null;
        var t = root.transform.Find(child);
        var tmp = t != null ? t.GetComponent<TMP_Text>() : null;
        return tmp != null ? tmp.text : null;
    }

    static string AnyText(GameObject root)
    {
        if (root == null) return null;
        var tmp = root.GetComponentInChildren<TMP_Text>(true);
        return tmp != null ? tmp.text : null;
    }

    /// <summary>
    /// Stops the scene's PlayerUI from being drawn. Only the Canvas components are switched off: the
    /// objects stay, because GameManager and QuestSystem still toggle them and this HUD reads that.
    /// </summary>
    void HideSceneUI()
    {
        if (gm == null || gm.scoretext == null) return;
        var root = gm.scoretext.canvas != null ? gm.scoretext.canvas.rootCanvas : null;
        if (root == null) return;
        foreach (var c in root.GetComponentsInChildren<Canvas>(true)) c.enabled = false;
        var status = root.transform.Find("Status");
        if (status != null) legacyStatus = status.GetComponent<Image>();
    }

    // ------------------------------------------------------------------ per-frame

    bool ResolveCamera()
    {
        if (cam != null) return true;
        if (player != null && player.mainCamera != null) cam = player.mainCamera.transform;
        else if (Camera.main != null) cam = Camera.main.transform;
        if (cam != null) smoothForward = cam.forward;
        return cam != null;
    }

    // Translation follows the head; the view direction is smoothed so the HUD trails a little when
    // the player turns. The rig is scaled, so distance is in its units, and the canvas is sized to
    // span the same angle as Module2HUD's 1.1 m canvas at 1.2 m.
    void Follow()
    {
        float worldDistance = distance * Mathf.Abs(cam.lossyScale.z);
        smoothForward = Vector3.Slerp(smoothForward, cam.forward, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        hudRoot.position = cam.position + smoothForward * worldDistance;
        hudRoot.rotation = Quaternion.LookRotation(smoothForward, cam.up);
        hudRoot.localScale = Vector3.one * (0.001f / 1.2f) * worldDistance;
    }

    void UpdateStatus()
    {
        int t = Mathf.Max(0, gm.time);
        timerText.text = (t / 60) + ":" + (t % 60).ToString("00");
        bool playing = !player.Death && !player.RestartAble;
        timerText.color = t <= 10 ? dangerColor : t <= 30 ? warnColor : textColor;
        timerText.alpha = (t <= 10 && playing) ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 6f)) : 1f;
        rainIcon.enabled = gm.IsRain;
        scoreText.text = scoreWord + "  " + gm.score;
        statusDot.enabled = legacyStatus != null;
        if (legacyStatus != null) statusDot.color = legacyStatus.color;
    }

    void UpdateBars()
    {
        float nec = player.Max_Nec > 0 ? Mathf.Clamp01(player.Current_Nec / player.Max_Nec) : 0f;
        energyFill.fillAmount = nec;
        energyFill.color = player.ishungry
            ? Color.Lerp(warnColor, dangerColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 6f))
            : nectarColor;
        float blood = player.Max_Blood > 0 ? Mathf.Clamp01(player.Current_Blood / player.Max_Blood) : 0f;
        bloodFill.fillAmount = blood;
        bloodFill.color = blood >= 0.999f ? doneColor : bloodColor;
    }

    void UpdateQuests(bool ended)
    {
        if (quests == null || quests.QuestList == null) { questGroup.alpha = 0f; return; }
        int n = Mathf.Min(questRows.Length, quests.QuestList.Length);
        for (int i = 0; i < n; i++)
        {
            bool done = quests.IsDone(i);
            string label = quests.QuestList[i];
            if (i == 3) label += "  " + Mathf.Min(player.EggLayed, QuestSystem.EggsToLay) + "/" + QuestSystem.EggsToLay;
            questRows[i].text = label;
            questRows[i].color = done ? doneColor : textColor;
            questMarks[i].sprite = done ? dotSprite : ringSprite;
            questMarks[i].color = done ? doneColor : mutedColor;

            // Quest completion toasts, after the first frame so already-done quests stay quiet.
            if (questsPrimed && done && !questWasDone[i])
                ShowToast(quests.QuestList[i] + (QuestReward(i) > 0 ? "   +" + QuestReward(i) : ""), doneColor);
            questWasDone[i] = done;
        }
        questsPrimed = true;
        // The left grip still decides, as before: GameManager toggles questUI, the HUD shows it.
        Fade(questGroup, !ended && gm.questUI != null && gm.questUI.activeSelf);
    }

    static int QuestReward(int i) => i == 0 ? 50 : i == 1 ? 100 : i == 2 ? 50 : 0;

    void UpdatePrompt(bool ended)
    {
        string prompt = "";
        Color back = panelColor, fore = textColor;
        bool danger = gm.DangerUI != null && gm.DangerUI.activeSelf;
        if (!ended)
        {
            if (danger && !string.IsNullOrEmpty(dangerText))
            {
                // White on solid red: the message a player has three seconds to act on.
                prompt = dangerText; back = new Color(dangerColor.r, dangerColor.g, dangerColor.b, 0.9f);
            }
            else if (quests != null && quests.NecWarning != null && quests.NecWarning.activeSelf && !string.IsNullOrEmpty(nectarWarningText))
            {
                prompt = nectarWarningText; fore = nectarColor;
            }
            else if (quests != null && quests.BloodWarning != null && quests.BloodWarning.activeSelf)
            {
                prompt = BloodWarningText; fore = new Color(1f, 0.45f, 0.45f);
            }
        }

        Fade(promptGroup, !string.IsNullOrEmpty(prompt));
        if (string.IsNullOrEmpty(prompt)) return;
        promptBg.color = back;
        promptText.color = fore;
        if (prompt == lastPrompt) return;
        lastPrompt = prompt;
        promptText.text = prompt;
        promptRect.sizeDelta = new Vector2(PromptW, Mathf.Max(60f, promptText.GetPreferredValues(prompt, PromptW - 40f, 0f).y + 26f));
    }

    void UpdateDanger(bool ended)
    {
        bool danger = !ended && gm.DangerUI != null && gm.DangerUI.activeSelf;
        float a = danger ? 0.18f + 0.12f * Mathf.Abs(Mathf.Sin(Time.time * 5f)) : 0f;
        dangerVignette.color = new Color(dangerColor.r, dangerColor.g, dangerColor.b, Mathf.MoveTowards(dangerVignette.color.a, a, Time.deltaTime * 2f));
    }

    void UpdateEnd(bool dead, bool timedOut)
    {
        // Time-out wins if both are up: it ends the session, a death only the life.
        bool showTimeOut = timedOut;
        bool showDeath = dead && !timedOut;
        if (showDeath)
        {
            string msg = gm.DeathText != null ? gm.DeathText.text : "";
            if (deathMessageText.text != msg) deathMessageText.text = msg;
        }
        if (showTimeOut)
        {
            timeOutValuesText.text = gm.score + "\n" + gm.DrinkNectarScore + "\n" + gm.MatingScore + "\n" + gm.DrinkBloodScore + "\n" + gm.LayEggScore;
        }
        Fade(deathGroup, showDeath);
        Fade(timeOutGroup, showTimeOut);
        // "Press a button" only once a press would actually do something.
        float pulse = player.RestartAble ? 0.55f + 0.45f * Mathf.Sin(Time.time * 4f) : 0f;
        deathFooterText.alpha = pulse;
        timeOutFooterText.alpha = pulse;
    }

    void ShowToast(string message, Color accent)
    {
        if (string.IsNullOrEmpty(message)) return;
        var rect = NewRect("Toast", toastContainer, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(ToastW, 48));
        var bg = rect.gameObject.AddComponent<Image>();
        bg.sprite = roundedSprite; bg.type = Image.Type.Sliced; bg.color = panelColor; bg.raycastTarget = false;
        var text = NewText("Text", rect, message, 26, accent, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-28, 0), FontStyles.Bold);
        rect.sizeDelta = new Vector2(ToastW, Mathf.Max(48f, text.GetPreferredValues(message, ToastW - 28f, 0f).y + 18f));
        var group = rect.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        toasts.Insert(0, new Toast { rect = rect, group = group, born = Time.time });
        while (toasts.Count > MaxToasts)
        {
            Destroy(toasts[toasts.Count - 1].rect.gameObject);
            toasts.RemoveAt(toasts.Count - 1);
        }
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
            float fadeOut = Mathf.InverseLerp(toastDuration, toastDuration * 0.66f, age);
            toast.group.alpha = Mathf.Min(Mathf.InverseLerp(0f, 0.18f, age), fadeOut);
        }
        float y = 0f;
        for (int i = 0; i < toasts.Count; i++)
        {
            toasts[i].rect.anchoredPosition = new Vector2(0f, y);
            y -= toasts[i].rect.sizeDelta.y + 8f;
        }
    }

    void Fade(CanvasGroup group, bool visible)
    {
        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.deltaTime * 6f);
    }

    // ------------------------------------------------------------------ construction

    void BuildHud()
    {
        hudRoot = NewCanvas("Module1HUD (runtime)", CanvasW, CanvasH, 10);

        // Behind everything else: a red wash while a predator is on you.
        dangerVignette = NewImage("DangerVignette", hudRoot, roundedSprite, new Color(dangerColor.r, dangerColor.g, dangerColor.b, 0f),
            Image.Type.Sliced, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        var gameplay = NewRect("Gameplay", hudRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        gameplayGroup = gameplay.gameObject.AddComponent<CanvasGroup>();

        // --- top left: timer (+ rain drop while it rains), energy and blood bars ---
        timerText = NewText("Timer", gameplay, "0:00", 54, textColor, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28, -22), new Vector2(170, 66), FontStyles.Bold);
        rainIcon = NewImage("Rain", gameplay, dropSprite, accentColor, Image.Type.Simple,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(178, -34), new Vector2(34, 34));
        rainIcon.enabled = false;

        energyLabel = NewText("EnergyLabel", gameplay, energyLabelText, 22, mutedColor, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30, -96), new Vector2(300, 32), FontStyles.Bold);
        energyFill = Bar("Energy", gameplay, new Vector2(30, -132), nectarColor);
        bloodLabel = NewText("BloodLabel", gameplay, bloodLabelText, 22, mutedColor, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30, -158), new Vector2(300, 32), FontStyles.Bold);
        bloodFill = Bar("Blood", gameplay, new Vector2(30, -194), bloodColor);

        // --- top right: score, with the GAMA connection dot beside it ---
        scoreText = NewText("Score", gameplay, "", 40, textColor, TextAlignmentOptions.TopRight,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28, -26), new Vector2(360, 56), FontStyles.Bold);
        statusDot = NewImage("GamaStatus", gameplay, dotSprite, mutedColor, Image.Type.Simple,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30, -92), new Vector2(14, 14));

        // --- life-cycle card, left, while the left grip is held ---
        BuildQuestCard(gameplay);

        // --- toasts, top centre ---
        toastContainer = NewRect("Toasts", gameplay, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(ToastW, 10));

        // --- bottom: warnings ---
        promptRect = NewRect("Prompt", gameplay, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 54), new Vector2(PromptW, 60));
        promptGroup = promptRect.gameObject.AddComponent<CanvasGroup>();
        promptGroup.alpha = 0f;
        promptBg = promptRect.gameObject.AddComponent<Image>();
        promptBg.sprite = roundedSprite; promptBg.type = Image.Type.Sliced; promptBg.color = panelColor; promptBg.raycastTarget = false;
        promptText = NewText("Text", promptRect, "", 30, textColor, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-40, 0), FontStyles.Bold);

        BuildDeathCard(hudRoot);
        BuildTimeOutCard(hudRoot);
    }

    Image Bar(string name, Transform parent, Vector2 pos, Color color)
    {
        var back = NewImage(name + "Back", parent, roundedSprite, barBackColor, Image.Type.Sliced,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(300, 18));
        var fill = NewImage(name + "Fill", back.rectTransform, roundedSprite, color, Image.Type.Filled,
            Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0f;
        return fill;
    }

    void BuildQuestCard(Transform parent)
    {
        var panel = NewRect("Quests", parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28, -40), new Vector2(380, 300));
        questGroup = panel.gameObject.AddComponent<CanvasGroup>();
        questGroup.alpha = 0f;
        var bg = panel.gameObject.AddComponent<Image>();
        bg.sprite = roundedSprite; bg.type = Image.Type.Sliced; bg.color = panelColor; bg.raycastTarget = false;

        questTitleText = NewText("Title", panel, questTitle ?? "", 30, accentColor, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(-48, 44), FontStyles.Bold);
        for (int i = 0; i < questRows.Length; i++)
        {
            float y = -78 - i * 54;
            questMarks[i] = NewImage("Mark" + i, panel, ringSprite, mutedColor, Image.Type.Simple,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26, y - 8), new Vector2(24, 24));
            questRows[i] = NewText("Quest" + i, panel, "", 26, textColor, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(64, y), new Vector2(-84, 46));
        }
    }

    void BuildDeathCard(Transform parent)
    {
        var panel = NewRect("Death", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 400));
        deathGroup = panel.gameObject.AddComponent<CanvasGroup>();
        deathGroup.alpha = 0f;
        var bg = panel.gameObject.AddComponent<Image>();
        bg.sprite = roundedSprite; bg.type = Image.Type.Sliced; bg.color = endPanelColor; bg.raycastTarget = false;

        deathTitleText = NewText("Title", panel, deathTitle ?? "", 64, dangerColor, TextAlignmentOptions.Center,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -36), new Vector2(-40, 90), FontStyles.Bold);
        deathMessageText = NewText("Message", panel, "", 34, textColor, TextAlignmentOptions.Center,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(-80, 110));
        deathFooterText = NewText("Footer", panel, deathFooter ?? "", 30, doneColor, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(-40, 48), FontStyles.Bold);
    }

    void BuildTimeOutCard(Transform parent)
    {
        var panel = NewRect("TimeOut", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 540));
        timeOutGroup = panel.gameObject.AddComponent<CanvasGroup>();
        timeOutGroup.alpha = 0f;
        var bg = panel.gameObject.AddComponent<Image>();
        bg.sprite = roundedSprite; bg.type = Image.Type.Sliced; bg.color = new Color(panelColor.r, panelColor.g, panelColor.b, 0.92f); bg.raycastTarget = false;

        timeOutTitleText = NewText("Title", panel, timeOutTitle ?? "", 60, warnColor, TextAlignmentOptions.Center,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(-40, 84), FontStyles.Bold);
        string labels = timeOutLabels != null ? string.Join("\n", timeOutLabels) : "";
        // Two columns: the team's labels on the left, the numbers right-aligned beside them.
        timeOutLabelsText = NewText("Labels", panel, labels, 32, mutedColor, TextAlignmentOptions.TopLeft,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(110, -136), new Vector2(420, 300));
        timeOutValuesText = NewText("Values", panel, "", 32, textColor, TextAlignmentOptions.TopRight,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(120, -136), new Vector2(160, 300), FontStyles.Bold);
        timeOutLabelsText.lineSpacing = timeOutValuesText.lineSpacing = 12f;
        timeOutFooterText = NewText("Footer", panel, timeOutFooter ?? "", 30, doneColor, TextAlignmentOptions.Center,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(-40, 48), FontStyles.Bold);
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
        // The full Lao font carries a kerning pair that opens a visible gap in "ງງ" (ພະລັງງານ). The
        // team's scene font has no kerning at all, so leave it off to set their text as they did.
        var features = new List<UnityEngine.TextCore.OTL_FeatureTag>(t.fontFeatures);
        features.Remove(UnityEngine.TextCore.OTL_FeatureTag.kern);
        t.fontFeatures = features;
        return t;
    }

    // ------------------------------------------------------------------ procedural sprites (as Module2HUD)

    void BuildSprites()
    {
        roundedSprite = MakeSprite(Raster(32, (u, v) =>
        {
            float r = 10f / 32f;
            Vector2 p = new Vector2(u - 0.5f, v - 0.5f);
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - new Vector2(0.5f - r, 0.5f - r);
            float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
            return d < 0f;
        }), new Vector4(10, 10, 10, 10));
        dropSprite = MakeSprite(Raster(64, (u, v) =>
            InCircle(u, v, 0.5f, 0.36f, 0.28f) || InTri(u, v, 0.5f, 0.97f, 0.251f, 0.4885f, 0.749f, 0.4885f)));
        dotSprite = MakeSprite(Raster(64, (u, v) => InCircle(u, v, 0.5f, 0.5f, 0.46f)));
        ringSprite = MakeSprite(Raster(64, (u, v) => InCircle(u, v, 0.5f, 0.5f, 0.46f) && !InCircle(u, v, 0.5f, 0.5f, 0.32f)));
    }

    static Sprite MakeSprite(Texture2D tex, Vector4 border = default)
    {
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

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
