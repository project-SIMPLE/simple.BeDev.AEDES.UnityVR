using Aedes.Module3.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Everything the squad reads: the running HUD, the handover screen, and the debrief.
///
/// Built for the CAST, not for the headset. Section 9 puts two of the three students outside the
/// headset with jobs to do - the Coach holds the map of what has been visited and warns the Pilot
/// what is being missed, the Analyst writes the record - and both of them are looking at a phone
/// or a laptop screen mirroring the play. Section 8 adds that the countdown "should be visible to
/// the Coach, not only to the Pilot".
///
/// So it is pictures and numbers in large type, not paragraphs: a clock, a net and a sun with their
/// counts; a handover that is a list of houses, each with what was seen there and what it needs; a
/// debrief of a few big numbers. On a desktop it is a screen-space overlay. In the headset an overlay
/// is not drawn at all, so there it becomes a world-space canvas that follows the Pilot's head at
/// arm's length - and because casting mirrors the headset view, the Coach and the Analyst still
/// read the same countdown on their screens. A HUD that only works inside the headset would leave
/// two thirds of the squad watching, which is the failure mode the squad structure exists to
/// prevent.
///
/// The canvas is built in code so the scene file stays small enough to merge - see
/// Module3SceneBuilder.
/// </summary>
public class M3Hud : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private int baseFontSize = 28;

    [Tooltip("Seconds the handover screen stays up when the session does not wait for the Pilot.")]
    [SerializeField] private float handoverSeconds = 12f;

    [Header("In the headset")]
    [Tooltip("How far in front of the Pilot the HUD floats.")]
    [SerializeField] private float vrDistance = 1.7f;
    [Tooltip("Width of the HUD in metres at that distance (the canvas is 1280 x 720 units).")]
    [SerializeField] private float vrWidth = 2.3f;
    [Tooltip("How quickly the HUD catches up when the Pilot turns their head.")]
    [SerializeField] private float vrFollowSpeed = 4f;

    private RectTransform statusBar;
    private TMP_Text clockText, netsText, dayText;
    private Image netsIcon;
    private float netsFlash;
    private GameObject handoverPanel;
    private RectTransform handoverContent;
    private GameObject debriefPanel;
    private RectTransform debriefContent;
    private GameObject bannerPanel;
    private TMP_Text bannerText;
    /// <summary>Text and icon sizes, worked out once from the HUD's scale in the headset (BuildCanvas).</summary>
    private float textSize, bigText, iconSize, bigIcon;
    private Canvas canvas;
    private bool worldSpace;
    private Vector3 smoothForward = Vector3.forward;

    private float handoverShownAt = -1f;

    private void Awake()
    {
        if (font == null) font = Resources.Load<TMP_FontAsset>(M3Ui.FontResource);
        M3Ui.Font = font;   // shared with the conversation panel and the other in-world UI
        BuildCanvas();
    }

    // M3Session sets Instance in its Awake, and Awake/OnEnable order across GameObjects is not
    // defined. When this OnEnable ran first it silently subscribed to nothing, so the handover
    // screen, the debrief and action feedback never appeared. Start runs after every Awake in the
    // scene, so try again there.
    private M3Session subscribedTo;

    private void OnEnable() => Subscribe();

    private void Start()
    {
        Subscribe();
        // The XR player builds its rig in its own Awake, so by Start there is a headset camera.
        if (M3XRPlayer.Instance != null && M3XRPlayer.Instance.Rig != null) MakeWorldSpace();
    }

    private void Subscribe()
    {
        var s = M3Session.Instance;
        if (s == null || subscribedTo == s) return;
        s.OnHandover += ShowHandover;
        s.OnSessionFinished += ShowDebrief;
        s.OnActionResolved += OnAction;
        subscribedTo = s;
    }

    private void OnDisable()
    {
        if (subscribedTo == null) return;
        subscribedTo.OnHandover -= ShowHandover;
        subscribedTo.OnSessionFinished -= ShowDebrief;
        subscribedTo.OnActionResolved -= OnAction;
        subscribedTo = null;
    }

    private void Update()
    {
        UpdateStatusLine();
        UpdateBanner();

        var s = M3Session.Instance;
        if (s == null || s.Session == null || handoverPanel == null) return;

        if (s.Running && s.WaitingForPilot)
        {
            // The turn is held until the Pilot presses A. The first turn has no handover to show,
            // so it gets a plain "you are player N" card instead.
            if (!handoverPanel.activeSelf) ShowStartCard();
        }
        else if (handoverPanel.activeSelf && handoverShownAt >= 0f
                 && (!waitedForPilot || Time.time - handoverShownAt > 0.25f)
                 && (waitedForPilot || Time.time - handoverShownAt > handoverSeconds))
        {
            handoverShownAt = -1f;
            handoverPanel.SetActive(false);
        }
    }

    private bool waitedForPilot;

    private void LateUpdate()
    {
        if (!worldSpace || Camera.main == null) return;

        // Float at arm's length, catching up with the head a little behind it - glued to the eyes a
        // HUD is uncomfortable, and it makes the whole view feel like a screen stuck to the face.
        var head = Camera.main.transform;
        Vector3 flat = head.forward;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f) flat = smoothForward;
        smoothForward = Vector3.Slerp(smoothForward, flat.normalized, 1f - Mathf.Exp(-vrFollowSpeed * Time.deltaTime));
        Vector3 at = head.position + smoothForward * vrDistance;
        at.y = head.position.y - 0.05f;
        canvas.transform.position = at;
        canvas.transform.rotation = Quaternion.LookRotation(smoothForward, Vector3.up);
    }

    private void MakeWorldSpace()
    {
        if (worldSpace || canvas == null) return;
        worldSpace = true;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 20;
        var rt = (RectTransform)canvas.transform;
        rt.sizeDelta = new Vector2(1280f, 720f);
        rt.localScale = Vector3.one * (vrWidth / 1280f);
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null) scaler.enabled = false;   // a world canvas is sized by its transform
        canvas.transform.SetParent(null, true);
        // Drawn after the world like Module 2's HUD: indoors the canvas sits further out than most
        // walls, which cut into the status line.
        M3Ui.OnTop(canvas);
        smoothForward = Camera.main.transform.forward;
        smoothForward.y = 0f;
        if (smoothForward.sqrMagnitude < 0.01f) smoothForward = Vector3.forward;
        smoothForward.Normalize();
    }

    private void UpdateBanner()
    {
        if (bannerPanel == null) return;
        var xr = M3XRPlayer.Instance;
        bool asking = xr != null && xr.ConfirmingEndTurn;
        if (bannerPanel.activeSelf == asking) return;
        // Worded as it opens, not when it was built: the HUD is built in Awake, which can run before the
        // LocalizationManager has read its CSV - the banner said "m3.ui.endTurnShort".
        if (asking) bannerText.text = Localized("m3.ui.endTurnShort");
        bannerPanel.SetActive(asking);
    }

    /// <summary>The first turn has no handover, so it gets this instead: whose turn it is, and the game in pictures.</summary>
    private void ShowStartCard()
    {
        var s = M3Session.Instance;
        M3Ui.Clear(handoverContent);

        var who = M3Ui.Row(handoverContent, "Player", iconSize * 0.3f, TextAnchor.MiddleCenter);
        M3Ui.Icon(who, M3Icons.Person, bigIcon, M3Ui.Text);
        Bold(M3Ui.Word(who, Localized("m3.ui.playerN", s.Session.PilotIndex + 1), bigText, M3Ui.Text));

        // Go into the houses, talk to people, help them: the whole turn in pictures.
        var story = M3Ui.Row(handoverContent, "How to play", iconSize * 0.35f, TextAnchor.MiddleCenter);
        M3Ui.Icon(story, M3Icons.House, bigIcon * 1.6f, M3Ui.Text);
        M3Ui.Icon(story, M3Icons.Arrow, iconSize, M3Ui.Muted);
        M3Ui.Icon(story, M3Icons.Talk, bigIcon * 1.6f, M3Ui.Text);
        M3Ui.Icon(story, M3Icons.Arrow, iconSize, M3Ui.Muted);
        M3Ui.Icon(story, M3Icons.Net, bigIcon * 1.6f, M3Ui.Text);
        M3Ui.Icon(story, M3Icons.Water, bigIcon * 1.6f, M3Ui.Text);
        M3Ui.Icon(story, M3Icons.HealthCentre, bigIcon * 1.6f, M3Ui.Text);

        var line = M3Ui.Line(handoverContent, Localized("m3.ui.startShort"), textSize, M3Ui.Muted);
        line.alignment = TextAlignmentOptions.Center;

        ReadyRow(handoverContent);
        handoverPanel.SetActive(true);
        handoverShownAt = Time.time;
        waitedForPilot = true;
    }

    /// <summary>"Put the headset on, then Ready on your watch", as a headset, an arrow and the watch.</summary>
    private void ReadyRow(Transform parent)
    {
        var row = M3Ui.Row(parent, "Ready", iconSize * 0.35f, TextAnchor.MiddleCenter);
        M3Ui.Icon(row, M3Icons.Headset, bigIcon * 1.2f, M3Ui.Accent);
        M3Ui.Icon(row, M3Icons.Arrow, iconSize, M3Ui.Accent);
        M3Ui.Icon(row, M3Icons.Watch, bigIcon * 1.2f, M3Ui.Accent);
        Bold(M3Ui.Word(row, Localized("m3.ui.wristReady"), bigText * 0.8f, M3Ui.Accent));
    }

    // -------------------------------------------------------------------------------------

    private void UpdateStatusLine()
    {
        var s = M3Session.Instance;
        if (s == null || s.Session == null || statusBar == null) return;

        var sim = s.Neighbourhood;
        // After the last handover the clock means nothing: only the day and the nets stay.
        var clock = clockText.transform.parent.gameObject;
        if (clock.activeSelf != s.Running) clock.SetActive(s.Running);
        if (s.Running)
        {
            float left = Mathf.Max(0f, s.TurnSecondsRemaining);
            // The countdown is the Coach's instrument as much as the Pilot's: Module 2's warning orange
            // in the last 30 s, where a class can see it from across a table.
            clockText.text = $"{Mathf.FloorToInt(left / 60f)}:{Mathf.FloorToInt(left % 60f):00}";
            clockText.color = left <= 30f ? M3Ui.Warn : M3Ui.Text;
        }
        netsText.text = sim.NetsRemaining.ToString();
        dayText.text = sim.Day.ToString();

        // Asked for a net with none left: the count flashes, so the reason is where the Pilot looks for it.
        netsFlash = Mathf.Max(0f, netsFlash - Time.deltaTime);
        float f = netsFlash > 0f ? 0.5f - 0.5f * Mathf.Cos(netsFlash * 18f) : 0f;
        var c = Color.Lerp(M3Ui.Text, M3Ui.Warn, f);
        netsText.color = c;
        netsIcon.color = c;
    }

    private void OnAction(ActionResult result)
    {
        if (result.Outcome == ActionOutcome.NoNetsRemaining) netsFlash = 1.4f;
    }

    private void ShowHandover(HandoverBrief brief)
    {
        if (handoverPanel == null) return;
        M3Ui.Clear(handoverContent);

        // How much time has passed: a calendar and "3 days later".
        var when = M3Ui.Row(handoverContent, "When", iconSize * 0.3f, TextAnchor.MiddleCenter);
        M3Ui.Icon(when, M3Icons.Calendar, bigIcon, M3Ui.Text);
        Bold(M3Ui.Word(when, Localized(CoachBrief.TimeJumpKey(brief.DaysElapsed), brief.DaysElapsed), bigText, M3Ui.Text));

        // What is waiting, one row each: the house, what was seen there, and what it needs.
        var lines = CoachBrief.Compose(brief);
        if (lines.Count == 0)
        {
            var calm = M3Ui.Row(handoverContent, "Nothing urgent", iconSize * 0.3f, TextAnchor.MiddleCenter);
            M3Ui.Icon(calm, M3Icons.Check, iconSize, M3Ui.Accent);
            M3Ui.Icon(calm, M3Icons.Eye, iconSize, M3Ui.Text);
        }
        for (int i = 0; i < lines.Count; i++) BriefRow(lines[i]);

        var session = M3Session.Instance;
        waitedForPilot = session != null && session.WaitingForPilot;
        // The next Pilot presses Ready once the headset is on; the clock starts then, not before.
        if (waitedForPilot) ReadyRow(handoverContent);

        handoverPanel.SetActive(true);
        handoverShownAt = Time.time;
        // Without a ready press, hold the clock for the length of the screen instead.
        if (!waitedForPilot && session != null) session.HoldTurnClock(handoverSeconds);
    }

    private void BriefRow(BriefLine l)
    {
        var row = M3Ui.Row(handoverContent, l.MessageKey, iconSize * 0.3f, TextAnchor.MiddleCenter);
        // How soon, as a bar down the left: red now, orange soon, grey for news.
        var bar = M3Ui.Rect("Urgency", row, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        bar.color = l.Urgency == BriefUrgency.Now ? M3Ui.Danger : l.Urgency == BriefUrgency.Soon ? M3Ui.Warn : M3Ui.Muted;
        var le = bar.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = 10f;
        le.preferredHeight = le.minHeight = bigIcon;

        int n = l.Args != null && l.Args.Length > 0 && l.Args[0] is int i ? i : 0;
        switch (l.MessageKey)
        {
            case "m3.brief.bleeding": Needs(row, n, M3Icons.Bleeding, M3Icons.HealthCentre); break;
            case "m3.brief.vomiting": Needs(row, n, M3Icons.Vomiting, M3Icons.HealthCentre); break;
            case "m3.brief.uncovered": Needs(row, n, M3Icons.Fever, M3Icons.Net); break;
            case "m3.brief.lookingBetter": Needs(row, n, M3Icons.FeverDown, M3Icons.Eye); break;
            case "m3.coach.neverVisited": Counted(row, M3Icons.HouseUnvisited, "x" + n); break;
            case "m3.coach.newCases": Counted(row, M3Icons.PersonNew, "+" + n); break;
            case "m3.coach.noNetsLeft": Counted(row, M3Icons.Net, "0"); break;
            default: M3Ui.Word(row, Localized(l.MessageKey, l.Args), textSize, M3Ui.Text); break;
        }
    }

    /// <summary>House <paramref name="house"/>: what was seen there, an arrow, and what it needs.</summary>
    private void Needs(Transform row, int house, string seen, string needs)
    {
        M3Ui.HouseIcon(row, house, bigIcon, M3Ui.Text);
        M3Ui.Icon(row, seen, bigIcon, M3Ui.Text);
        M3Ui.Icon(row, M3Icons.Arrow, iconSize * 0.8f, M3Ui.Muted);
        M3Ui.Icon(row, needs, bigIcon, M3Ui.Accent);
    }

    private void Counted(Transform row, string icon, string count)
    {
        M3Ui.Icon(row, icon, bigIcon, M3Ui.Text);
        Bold(M3Ui.Word(row, count, bigText * 0.8f, M3Ui.Text));
    }

    private void ShowDebrief(HandoverBrief finalBrief)
    {
        if (debriefPanel == null) return;
        M3Ui.Clear(debriefContent);

        var s = M3Session.Instance;
        var sim = s.Neighbourhood;
        var map = OutbreakMap.Build(sim);

        // Section 5 fixes this order: the map, then the replay, and only then the score.
        Bold(M3Ui.Line(debriefContent, Localized("m3.debrief.title"), bigText, M3Ui.Text));

        // The outbreak, in numbers.
        var tiles = M3Ui.Row(debriefContent, "Outbreak", iconSize * 0.2f, TextAnchor.UpperCenter);
        Tile(tiles, M3Icons.People, map.TotalCases, "m3.short.fellIll");
        Tile(tiles, M3Icons.House, map.SecondaryCases, "m3.short.caughtHere");
        Tile(tiles, M3Icons.NoNet, map.PreventableCases, "m3.short.fromUncovered");
        Tile(tiles, M3Icons.HiddenPerson, map.InvisibleCases, "m3.short.neverFelt");
        Tile(tiles, M3Icons.Shield, map.HouseholdsWhereTheChainStopped.Count, "m3.short.chainsStopped");

        // The replay: the same village with a net on the first patient on day one, as two bars of the
        // cases caught here. Numbers, not a verdict - the old headline ("you covered the first patient
        // on day one") showed for a squad that had put up no net whenever the replay came out no
        // better, which D9 says happens.
        var counterfactual = new CounterfactualComparison
        {
            AsPlayed = map,
            HadPatientZeroBeenCovered = OutbreakMap.Build(s.BuildCounterfactual().Neighbourhood),
        };
        int played = map.SecondaryCases, replay = counterfactual.HadPatientZeroBeenCovered.SecondaryCases;
        int most = Mathf.Max(1, Mathf.Max(played, replay));
        Bar(M3Icons.People, Localized("m3.short.asPlayed"), played, most, M3Ui.Muted);
        Bar(M3Icons.Net, Localized("m3.short.netDayOne"), replay, most, M3Ui.Accent);

        // The score lines, as small tiles.
        var card = Scoring.Evaluate(sim, s.Session);
        if (card.Lines.Count > 0)
        {
            var score = M3Ui.Row(debriefContent, "Score", iconSize * 0.2f, TextAnchor.UpperCenter);
            for (int i = 0; i < card.Lines.Count; i++)
            {
                var line = card.Lines[i];
                Tile(score, ScoreIcon(line.Reason), line.Count, ScoreKey(line.Reason), small: true, muted: line.IsNote);
            }
        }

        var prompt = Bold(M3Ui.Line(debriefContent, FieldJournalExport.ClosingPrompt, textSize * 1.25f, M3Ui.Accent));
        prompt.alignment = TextAlignmentOptions.Center;

        debriefPanel.SetActive(true);
        if (handoverPanel != null) handoverPanel.SetActive(false);
    }

    /// <summary>An icon, a big number and a few words under it.</summary>
    private void Tile(Transform row, string icon, int count, string captionKey, bool small = false, bool muted = false)
    {
        var col = M3Ui.Stack(row, "Tile: " + icon, 2f, new RectOffset(8, 8, 4, 4));
        col.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
        var le = col.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = small ? 200f : 214f;
        Color c = muted ? M3Ui.Muted : M3Ui.Text;
        M3Ui.Icon(col, icon, small ? iconSize * 0.8f : iconSize, c);
        var number = Bold(M3Ui.Line(col, small ? "x" + count : count.ToString(), small ? textSize * 1.2f : bigText, c));
        number.alignment = TextAlignmentOptions.Center;
        var words = M3Ui.Line(col, Localized(captionKey), textSize * 0.8f, M3Ui.Muted);
        words.alignment = TextAlignmentOptions.Top;
    }

    /// <summary>One bar of the replay: an icon, what it is, a bar as long as the count, and the count.</summary>
    private void Bar(string icon, string label, int value, int most, Color colour)
    {
        var row = M3Ui.Row(debriefContent, "Bar: " + label, iconSize * 0.25f);
        M3Ui.Icon(row, icon, iconSize * 0.8f, colour);
        var name = M3Ui.Word(row, label, textSize, M3Ui.Text);
        var nle = name.gameObject.AddComponent<LayoutElement>();
        nle.preferredWidth = nle.minWidth = 330f;
        var bar = M3Ui.Round(M3Ui.Rect("Bar", row, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>());
        bar.color = colour;
        var ble = bar.gameObject.AddComponent<LayoutElement>();
        ble.preferredWidth = ble.minWidth = Mathf.Max(12f, 560f * value / most);
        ble.preferredHeight = ble.minHeight = iconSize * 0.5f;
        Bold(M3Ui.Word(row, value.ToString(), textSize * 1.2f, M3Ui.Text));
    }

    private static string ScoreIcon(ScoreReason r) => r switch
    {
        ScoreReason.ChainBroken => M3Icons.Shield,
        ScoreReason.ReferredCorrectly => M3Icons.HealthCentre,
        ScoreReason.GoodHomeCare => M3Icons.Water,
        ScoreReason.MissedWarningSign => M3Icons.Bleeding,
        _ => M3Icons.Talk,
    };

    private static string ScoreKey(ScoreReason r) => r switch
    {
        ScoreReason.ChainBroken => "m3.short.score.chainBroken",
        ScoreReason.ReferredCorrectly => "m3.short.score.referredCorrectly",
        ScoreReason.GoodHomeCare => "m3.short.score.goodHomeCare",
        ScoreReason.MissedWarningSign => "m3.short.score.missedWarningSign",
        _ => "m3.short.score.unnecessaryReferral",
    };

    private static TMP_Text Bold(TMP_Text t)
    {
        t.fontStyle |= FontStyles.Bold;
        return t;
    }

    private static string Localized(string key, params object[] args) => M3Ui.L(key, args);

    // -------------------------------------------------------------------------------------

    private void BuildCanvas()
    {
        var canvasGo = new GameObject("M3 HUD Canvas");
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);

        // Every size here holds the 1.5 degree rule at the HUD's distance in the headset.
        float hudUnitsPerMetre = 1280f / vrWidth;
        textSize = Mathf.Round(M3TextSize.AtLeast(baseFontSize + 4, hudUnitsPerMetre, vrDistance));
        bigText = Mathf.Round(textSize * 1.7f);
        iconSize = Mathf.Round(textSize * 1.8f);
        bigIcon = Mathf.Round(textSize * 2.2f);

        // Top left: the clock, the nets left and the day, each an icon and a number on a dark backing
        // that hugs them, so they read against a bright sky or a pale wall.
        statusBar = M3Ui.Row(canvasGo.transform, "Status", textSize * 0.9f, TextAnchor.MiddleLeft,
                             new RectOffset(18, 22, 8, 8));
        statusBar.anchorMin = statusBar.anchorMax = statusBar.pivot = new Vector2(0f, 1f);
        statusBar.anchoredPosition = new Vector2(24f, -12f);
        M3Ui.Round(statusBar.gameObject.AddComponent<Image>()).color = M3TextBacking.DefaultColour;
        var fit = statusBar.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        clockText = Counter(statusBar, M3Icons.Clock, out _);
        netsText = Counter(statusBar, M3Icons.Net, out netsIcon);
        dayText = Counter(statusBar, M3Icons.Day, out _);

        // "End your turn?" while the Pilot decides on the watch.
        bannerPanel = Panel(canvasGo.transform, "Banner", new Color(0.04f, 0.05f, 0.07f, 0.95f));
        var bannerRt = (RectTransform)bannerPanel.transform;
        bannerRt.anchorMin = bannerRt.anchorMax = new Vector2(0.5f, 0.5f);
        bannerRt.sizeDelta = new Vector2(620f, 150f);
        var ask = M3Ui.Row(bannerRt, "Ask", textSize, TextAnchor.MiddleCenter);
        M3Ui.Icon(ask, M3Icons.Watch, bigIcon, M3Ui.Accent);
        bannerText = Bold(M3Ui.Word(ask, "", bigText, M3Ui.Text));
        bannerPanel.SetActive(false);

        handoverPanel = Panel(canvasGo.transform, "Handover", new Color(0.04f, 0.05f, 0.07f, 0.94f));
        handoverContent = M3Ui.Stack(handoverPanel.transform, "Content", textSize * 1.1f, new RectOffset(96, 96, 64, 64));
        // The start card and the handover are a few rows of pictures: they sit in the middle of the card.
        handoverContent.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        handoverPanel.SetActive(false);

        debriefPanel = Panel(canvasGo.transform, "Debrief", new Color(0.04f, 0.05f, 0.07f, 0.97f));
        debriefContent = M3Ui.Stack(debriefPanel.transform, "Content", textSize * 0.7f, new RectOffset(72, 72, 44, 44));
        debriefPanel.SetActive(false);
    }

    /// <summary>One of the status bar's icon-and-number pairs.</summary>
    private TMP_Text Counter(Transform bar, string icon, out Image image)
    {
        var pair = M3Ui.Row(bar, "Counter: " + icon, textSize * 0.3f);
        image = M3Ui.Icon(pair, icon, textSize * 1.2f, M3Ui.Text);
        return Bold(M3Ui.Word(pair, "", textSize * 1.1f, M3Ui.Text));
    }

    private static GameObject Panel(Transform parent, string name, Color colour)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        M3Ui.Round(go.AddComponent<Image>()).color = colour;
        return go;
    }
}
