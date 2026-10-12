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
    /// <summary>The card in the middle of the view: the start card, a handover or the debrief.</summary>
    private RectTransform card;
    private GameObject banner;
    private TMP_Text bannerText;

    // One type scale and one spacing grid for the whole HUD, in canvas units (556 to the metre, read
    // at 1.7 m). Small is the 1.5 degree floor (M3TextSize); everything else steps up from it.
    private float small, body, title;
    private const float IconS = 36f, IconM = 52f, IconL = 64f;
    private const float Pad = 40f, Gap = 24f, Tight = 12f;
    /// <summary>Cards stay inside the middle ~40 degrees of the view, where reading needs no head turn.</summary>
    private const float CardWidth = 680f, DebriefWidth = 920f;
    private Canvas canvas;
    private bool worldSpace;
    private Vector3 smoothForward = Vector3.forward;

    private float handoverShownAt = -1f;
    private bool CardUp => card != null && card.gameObject.activeSelf;

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
        if (s == null || s.Session == null) return;

        if (s.Running && s.WaitingForPilot)
        {
            // The turn is held until the Pilot presses A. The first turn has no handover to show,
            // so it gets a plain "you are player N" card instead.
            if (!CardUp) ShowStartCard();
        }
        else if (s.Running && CardUp && handoverShownAt >= 0f
                 && (!waitedForPilot || Time.time - handoverShownAt > 0.25f)
                 && (waitedForPilot || Time.time - handoverShownAt > handoverSeconds))
        {
            handoverShownAt = -1f;
            card.gameObject.SetActive(false);
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
        if (banner == null) return;
        var xr = M3XRPlayer.Instance;
        bool asking = xr != null && xr.ConfirmingEndTurn;
        if (banner.activeSelf == asking) return;
        // Worded as it opens, not when it was built: the HUD is built in Awake, which can run before the
        // LocalizationManager has read its CSV - the banner said "m3.ui.endTurnShort".
        if (asking) bannerText.text = Localized("m3.ui.endTurnShort");
        banner.SetActive(asking);
    }

    // -------------------------------------------------------------------------------------
    // The card. Every card is built the same way: a header (icon and title), a hairline, the body,
    // and - when the next Pilot has to press Ready - a footer. All rows share the card's left edge.
    // -------------------------------------------------------------------------------------

    private RectTransform NewCard(float width)
    {
        if (card != null) Destroy(card.gameObject);
        card = M3Ui.Card(canvas.transform, "Card", width, new RectOffset((int)Pad, (int)Pad, (int)Pad - 8, (int)Pad), Gap);
        handoverShownAt = Time.time;
        return card;
    }

    private void Header(string icon, string text)
    {
        var row = M3Ui.Row(card, "Header", Tight + 4f);
        M3Ui.Icon(row, icon, IconM, M3Ui.Text);
        Bold(M3Ui.Word(row, text, title, M3Ui.Text));
        M3Ui.Rule(card);
    }

    /// <summary>"Put the headset on, then Ready on your watch": a headset, an arrow, the watch and the word, on a soft chip.</summary>
    private void ReadyFooter()
    {
        // In a row of its own, so the chip is as wide as its content rather than the card.
        var holder = M3Ui.Row(card, "Footer", 0f);
        var row = M3Ui.Row(holder, "Ready", Tight, TextAnchor.MiddleLeft, new RectOffset(16, 24, 10, 10));
        M3Ui.Round(row.gameObject.AddComponent<Image>(), M3Ui.ChipRadius).color = new Color(M3Ui.Accent.r, M3Ui.Accent.g, M3Ui.Accent.b, 0.14f);
        M3Ui.Icon(row, M3Icons.Headset, IconM, M3Ui.Accent);
        M3Ui.Icon(row, M3Icons.Arrow, IconS, M3Ui.Accent);
        M3Ui.Icon(row, M3Icons.Watch, IconM, M3Ui.Accent);
        Bold(M3Ui.Word(row, Localized("m3.ui.wristReady"), body, M3Ui.Accent));
    }

    /// <summary>The first turn has no handover, so it gets this instead: whose turn it is, and the game in pictures.</summary>
    private void ShowStartCard()
    {
        var s = M3Session.Instance;
        NewCard(CardWidth);
        Header(M3Icons.Person, Localized("m3.ui.playerN", s.Session.PilotIndex + 1));

        // Go into the houses, talk to people, help them: the whole turn in pictures.
        var story = M3Ui.Row(card, "How to play", Tight);
        M3Ui.Icon(story, M3Icons.House, IconL, M3Ui.Text);
        M3Ui.Icon(story, M3Icons.Arrow, IconS, M3Ui.Muted);
        M3Ui.Icon(story, M3Icons.Talk, IconL, M3Ui.Text);
        M3Ui.Icon(story, M3Icons.Arrow, IconS, M3Ui.Muted);
        M3Ui.Icon(story, M3Icons.Net, IconL, M3Ui.Accent);
        M3Ui.Icon(story, M3Icons.Water, IconL, M3Ui.Accent);
        M3Ui.Icon(story, M3Icons.HealthCentre, IconL, M3Ui.Accent);
        M3Ui.Line(card, Localized("m3.ui.startShort"), body, M3Ui.Muted);

        ReadyFooter();
        waitedForPilot = true;
    }

    // -------------------------------------------------------------------------------------

    private void UpdateStatusLine()
    {
        var s = M3Session.Instance;
        if (s == null || s.Session == null || statusBar == null) return;

        // One thing at a time: while a card is up, the status bar would only show through it.
        bool show = !CardUp;
        if (statusBar.gameObject.activeSelf != show) statusBar.gameObject.SetActive(show);
        if (!show) return;

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
        NewCard(CardWidth);
        // How much time has passed: a calendar and "3 days later".
        Header(M3Icons.Calendar, Localized(CoachBrief.TimeJumpKey(brief.DaysElapsed), brief.DaysElapsed));

        // What is waiting, one row each: how soon, the house, what was seen there, and what it needs.
        // Every row has the same columns, so the houses line up under each other.
        var lines = CoachBrief.Compose(brief);
        var list = M3Ui.Stack(card, "Brief", Tight, new RectOffset());
        if (lines.Count == 0)
        {
            var calm = M3Ui.Row(list, "Nothing urgent", Tight);
            Dot(calm, M3Ui.Accent);
            M3Ui.Icon(calm, M3Icons.Check, IconM, M3Ui.Accent);
            M3Ui.Icon(calm, M3Icons.Eye, IconM, M3Ui.Text);
        }
        for (int i = 0; i < lines.Count; i++) BriefRow(list, lines[i]);

        var session = M3Session.Instance;
        waitedForPilot = session != null && session.WaitingForPilot;
        // The next Pilot presses Ready once the headset is on; the clock starts then, not before.
        if (waitedForPilot) ReadyFooter();
        // Without a ready press, hold the clock for the length of the screen instead.
        else if (session != null) session.HoldTurnClock(handoverSeconds);
    }

    /// <summary>How soon, as a dot at the start of the row: red now, orange soon, grey for news.</summary>
    private static void Dot(Transform row, Color colour)
    {
        var dot = M3Ui.Round(M3Ui.Rect("Urgency", row, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)
                                 .gameObject.AddComponent<Image>(), 8f);
        dot.color = colour;
        var le = dot.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = 16f;
        le.preferredHeight = le.minHeight = 16f;
    }

    private void BriefRow(Transform list, BriefLine l)
    {
        var row = M3Ui.Row(list, l.MessageKey, Tight + 4f);
        Dot(row, l.Urgency == BriefUrgency.Now ? M3Ui.Danger : l.Urgency == BriefUrgency.Soon ? M3Ui.Warn : M3Ui.Muted);

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
            default: M3Ui.Word(row, Localized(l.MessageKey, l.Args), body, M3Ui.Text); break;
        }
    }

    /// <summary>House <paramref name="house"/>: what was seen there, an arrow, and what it needs.</summary>
    private void Needs(Transform row, int house, string seen, string needs)
    {
        M3Ui.HouseIcon(row, house, IconL, M3Ui.Text);
        M3Ui.Icon(row, seen, IconL, M3Ui.Text);
        M3Ui.Icon(row, M3Icons.Arrow, IconS, M3Ui.Muted);
        M3Ui.Icon(row, needs, IconL, M3Ui.Accent);
    }

    /// <summary>A count in the house column: an icon where the house would be, then the number.</summary>
    private void Counted(Transform row, string icon, string count)
    {
        M3Ui.Icon(row, icon, IconL, M3Ui.Text);
        Bold(M3Ui.Word(row, count, title, M3Ui.Text));
    }

    private void ShowDebrief(HandoverBrief finalBrief)
    {
        var s = M3Session.Instance;
        var sim = s.Neighbourhood;
        var map = OutbreakMap.Build(sim);

        NewCard(DebriefWidth);
        handoverShownAt = -1f;   // stays up: the session is over
        // Section 5 fixes this order: the map, then the replay, and only then the score.
        Header(M3Icons.Mosquito, Localized("m3.debrief.title"));

        // The outbreak, in numbers: five tiles of the same width.
        var tiles = M3Ui.Row(card, "Outbreak", 0f, TextAnchor.UpperLeft);
        Tile(tiles, M3Icons.People, map.TotalCases, "m3.short.fellIll");
        Tile(tiles, M3Icons.House, map.SecondaryCases, "m3.short.caughtHere");
        Tile(tiles, M3Icons.NoNet, map.PreventableCases, "m3.short.fromUncovered");
        Tile(tiles, M3Icons.HiddenPerson, map.InvisibleCases, "m3.short.neverFelt");
        Tile(tiles, M3Icons.Shield, map.HouseholdsWhereTheChainStopped.Count, "m3.short.chainsStopped");
        M3Ui.Rule(card);

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
        var bars = M3Ui.Stack(card, "Replay", Tight, new RectOffset());
        Bar(bars, M3Icons.People, Localized("m3.short.asPlayed"), played, most, M3Ui.Muted);
        Bar(bars, M3Icons.Net, Localized("m3.short.netDayOne"), replay, most, M3Ui.Accent);

        // The score lines, as smaller tiles on the same columns as the ones above.
        var scorecard = Scoring.Evaluate(sim, s.Session);
        if (scorecard.Lines.Count > 0)
        {
            M3Ui.Rule(card);
            var score = M3Ui.Row(card, "Score", 0f, TextAnchor.UpperLeft);
            for (int i = 0; i < scorecard.Lines.Count; i++)
            {
                var line = scorecard.Lines[i];
                Tile(score, ScoreIcon(line.Reason), line.Count, ScoreKey(line.Reason), small: true, muted: line.IsNote);
            }
        }

        // The question the class takes away, on its own accent band.
        var ask = M3Ui.Row(card, "Prompt", Tight, TextAnchor.MiddleLeft, new RectOffset(20, 20, 14, 14));
        M3Ui.Round(ask.gameObject.AddComponent<Image>(), M3Ui.ChipRadius).color = new Color(M3Ui.Accent.r, M3Ui.Accent.g, M3Ui.Accent.b, 0.14f);
        M3Ui.Icon(ask, M3Icons.Talk, IconM, M3Ui.Accent);
        Bold(M3Ui.Word(ask, FieldJournalExport.ClosingPrompt, body, M3Ui.Accent));
    }

    /// <summary>An icon, a number and a few words under it, all centred in a fixed-width column.</summary>
    private void Tile(Transform row, string icon, int count, string captionKey, bool small = false, bool muted = false)
    {
        var col = M3Ui.Stack(row, "Tile: " + icon, 4f, new RectOffset(4, 4, 0, 0));
        col.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
        // A fixed column: without flexibleWidth 0 a lone score tile took the card's whole width.
        M3Ui.MinWidth(col, (DebriefWidth - 2f * Pad) / 5f).flexibleWidth = 0f;
        Color c = muted ? M3Ui.Muted : M3Ui.Text;
        var img = M3Ui.Icon(col, icon, small ? IconS : IconM, c);
        img.GetComponent<LayoutElement>().flexibleWidth = 0f;
        var number = Bold(M3Ui.Line(col, small ? "x" + count : count.ToString(), small ? body : title, c));
        number.alignment = TextAlignmentOptions.Center;
        var words = M3Ui.Line(col, Localized(captionKey), this.small, M3Ui.Muted);
        words.alignment = TextAlignmentOptions.Top;
    }

    /// <summary>One bar of the replay: an icon, what it is, a bar as long as the count, and the count.</summary>
    private void Bar(Transform parent, string icon, string label, int value, int most, Color colour)
    {
        var row = M3Ui.Row(parent, "Bar: " + label, Tight + 4f);
        M3Ui.Icon(row, icon, IconS, colour);
        M3Ui.MinWidth(M3Ui.Word(row, label, body, M3Ui.Text), 340f);
        var bar = M3Ui.Round(M3Ui.Rect("Bar", row, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero)
                                 .gameObject.AddComponent<Image>(), 10f);
        bar.color = colour;
        var ble = bar.gameObject.AddComponent<LayoutElement>();
        ble.preferredWidth = ble.minWidth = Mathf.Max(20f, 340f * value / most);
        ble.preferredHeight = ble.minHeight = 20f;
        Bold(M3Ui.Word(row, value.ToString(), body, M3Ui.Text));
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

        // The type scale, from the 1.5 degree floor at the HUD's distance in the headset.
        float hudUnitsPerMetre = 1280f / vrWidth;
        small = M3TextSize.Floor(hudUnitsPerMetre, vrDistance) + 1f;
        body = Mathf.Round(M3TextSize.AtLeast(baseFontSize + 4, hudUnitsPerMetre, vrDistance));
        title = Mathf.Round(body * 1.5f);

        // The clock, the nets left and the day: one pill, centred a little above the line of sight -
        // where a glance up finds it and it never sits over a villager or the person card. A corner
        // of the view is 30+ degrees out, which in a headset means turning the head to read it.
        statusBar = M3Ui.Row(canvasGo.transform, "Status", Gap, TextAnchor.MiddleCenter, new RectOffset(24, 24, 10, 10));
        statusBar.anchorMin = statusBar.anchorMax = new Vector2(0.5f, 1f);
        statusBar.pivot = new Vector2(0.5f, 1f);
        statusBar.anchoredPosition = new Vector2(0f, -96f);
        M3Ui.Round(statusBar.gameObject.AddComponent<Image>(), M3Ui.CardRadius).color = M3Ui.PanelColour;
        var fit = statusBar.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        clockText = Counter(statusBar, M3Icons.Clock, "0:00", out _);
        StatusRule(statusBar);
        netsText = Counter(statusBar, M3Icons.Net, "00", out netsIcon);
        StatusRule(statusBar);
        dayText = Counter(statusBar, M3Icons.Day, "00", out _);

        // "End your turn?" while the Pilot decides on the watch: a small card at eye level.
        var ask = M3Ui.Row(canvasGo.transform, "Banner", Tight + 4f, TextAnchor.MiddleCenter, new RectOffset(32, 36, 20, 20));
        ask.anchorMin = ask.anchorMax = ask.pivot = new Vector2(0.5f, 0.5f);
        M3Ui.Round(ask.gameObject.AddComponent<Image>(), M3Ui.CardRadius).color = M3Ui.PanelColour;
        var askFit = ask.gameObject.AddComponent<ContentSizeFitter>();
        askFit.horizontalFit = askFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        M3Ui.Icon(ask, M3Icons.Watch, IconM, M3Ui.Accent);
        bannerText = Bold(M3Ui.Word(ask, "", title, M3Ui.Text));
        banner = ask.gameObject;
        banner.SetActive(false);
    }

    /// <summary>One of the status bar's icon-and-number pairs, as wide as its widest value so it never twitches.</summary>
    private TMP_Text Counter(Transform bar, string icon, string widest, out Image image)
    {
        var pair = M3Ui.Row(bar, "Counter: " + icon, 8f);
        image = M3Ui.Icon(pair, icon, IconS, M3Ui.Text);
        var text = Bold(M3Ui.Word(pair, widest, body, M3Ui.Text));
        M3Ui.MinWidth(text, text.GetPreferredValues(widest).x + 2f);
        text.alignment = TextAlignmentOptions.Center;
        text.text = "";
        return text;
    }

    private static void StatusRule(Transform bar)
    {
        var rule = M3Ui.Rect("Rule", bar, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
        rule.color = M3Ui.RuleColour;
        rule.raycastTarget = false;
        var le = rule.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = 2f;
        le.preferredHeight = le.minHeight = IconS;
    }
}
