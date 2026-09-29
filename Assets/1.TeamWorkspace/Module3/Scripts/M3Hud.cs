using System.Collections.Generic;
using System.Text;
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
/// So this is large type. On a desktop it is a screen-space overlay. In the headset an overlay is
/// not drawn at all, so there it becomes a world-space canvas that follows the Pilot's head at
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

    private TMP_Text statusLine;
    private TMP_Text coachPanel;
    private GameObject handoverPanel;
    private TMP_Text handoverText;
    private GameObject debriefPanel;
    private TMP_Text debriefText;
    private TMP_Text banner;
    private Canvas canvas;
    private bool worldSpace;
    private Vector3 smoothForward = Vector3.forward;

    private float handoverShownAt = -1f;
    private readonly StringBuilder sb = new StringBuilder();

    private void Awake()
    {
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
        s.OnActionResolved += ShowActionFeedback;
        subscribedTo = s;
    }

    private void OnDisable()
    {
        if (subscribedTo == null) return;
        subscribedTo.OnHandover -= ShowHandover;
        subscribedTo.OnSessionFinished -= ShowDebrief;
        subscribedTo.OnActionResolved -= ShowActionFeedback;
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
        banner.text = asking ? $"<b>{Localized("m3.ui.endTurnAsk")}</b>" : "";
        banner.transform.parent.gameObject.SetActive(asking);
    }

    private void ShowStartCard()
    {
        var s = M3Session.Instance;
        sb.Clear();
        sb.AppendLine($"<size={baseFontSize + 26}><b>{Localized("m3.ui.playerN", s.Session.PilotIndex + 1)}</b></size>");
        sb.AppendLine();
        sb.AppendLine(Localized("m3.ui.startCard"));
        sb.AppendLine();
        sb.AppendLine($"<size={baseFontSize - 2}><color=#E5B25D>{Localized("m3.ui.pressAReady")}</color></size>");
        handoverText.text = sb.ToString();
        handoverPanel.SetActive(true);
        handoverShownAt = Time.time;
        waitedForPilot = true;
    }

    // -------------------------------------------------------------------------------------

    private void UpdateStatusLine()
    {
        var s = M3Session.Instance;
        if (s == null || s.Session == null || statusLine == null) return;

        var sim = s.Neighbourhood;

        // After the last handover the session's turn index sits one past the end: the bar read
        // "Turn 19/18  Round 7" under the debrief, with the clock frozen.
        if (!s.Running)
        {
            statusLine.text = Localized("m3.hud.sessionOver", sim.Day, sim.NetsRemaining);
            return;
        }

        float left = Mathf.Max(0f, s.TurnSecondsRemaining);

        // The countdown is the Coach's instrument as much as the Pilot's, so it goes first and
        // turns red where a class can see it from across a table.
        string clock = $"{Mathf.FloorToInt(left / 60f)}:{Mathf.FloorToInt(left % 60f):00}";
        string colour = left <= 30f ? "#E4572E" : "#F2F2F2";

        statusLine.text = Localized("m3.hud.status",
            $"<color={colour}><b>{clock}</b></color>",
            s.Session.TurnIndex + 1, s.Session.TotalTurns, s.Session.RoundIndex + 1,
            s.Session.PilotIndex + 1, sim.Day, sim.NetsRemaining);
    }

    private void ShowActionFeedback(ActionResult result)
    {
        if (coachPanel == null || string.IsNullOrEmpty(result.MessageKey)) return;

        // Section 10: an unnecessary referral is "gently corrected, not punished", so this reads
        // as advice rather than as an error, and nothing about it is red.
        coachPanel.text = Localized(result.MessageKey);
    }

    private void ShowHandover(HandoverBrief brief)
    {
        if (handoverPanel == null) return;

        sb.Clear();
        sb.AppendLine($"<size={baseFontSize + 26}><b>{Localized(CoachBrief.TimeJumpKey(brief.DaysElapsed), brief.DaysElapsed)}</b></size>");
        sb.AppendLine();

        var lines = CoachBrief.Compose(brief);
        if (lines.Count == 0)
        {
            sb.AppendLine(Localized("m3.coach.nothingUrgent"));
        }
        else
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                string mark = l.Urgency == BriefUrgency.Now ? "<color=#E4572E>!</color>"
                            : l.Urgency == BriefUrgency.Soon ? "<color=#E5B25D>-</color>"
                            : "<color=#9AA5B1>.</color>";
                sb.AppendLine($"{mark}  {Localized(l.MessageKey, l.Args)}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"<size={baseFontSize - 6}><color=#9AA5B1>{Localized("m3.coach.handItOver")}</color></size>");

        var session = M3Session.Instance;
        waitedForPilot = session != null && session.WaitingForPilot;
        if (waitedForPilot)
        {
            // The next Pilot presses A once the headset is on; the clock starts then, not before.
            sb.AppendLine();
            sb.AppendLine($"<size={baseFontSize - 2}><color=#E5B25D>{Localized("m3.ui.pressAReady")}</color></size>");
        }

        handoverText.text = sb.ToString();
        handoverPanel.SetActive(true);
        handoverShownAt = Time.time;
        // Without a ready press, hold the clock for the length of the screen instead.
        if (!waitedForPilot && session != null) session.HoldTurnClock(handoverSeconds);
    }

    private void ShowDebrief(HandoverBrief finalBrief)
    {
        if (debriefPanel == null) return;

        var s = M3Session.Instance;
        var sim = s.Neighbourhood;
        var map = OutbreakMap.Build(sim);

        // Section 5 fixes this order: the map, then the replay, and only then the score.
        // "At the end, before any score is shown, the game replays what would have happened."
        sb.Clear();
        sb.AppendLine($"<size={baseFontSize + 26}><b>{Localized("m3.debrief.title")}</b></size>");
        sb.AppendLine();
        sb.AppendLine(Localized("m3.debrief.caseCount", map.TotalCases, map.SecondaryCases));
        sb.AppendLine(Localized("m3.debrief.preventable", map.PreventableCases));

        if (map.HouseholdsWhereTheChainStopped.Count > 0)
        {
            sb.AppendLine(Localized("m3.debrief.chainsStopped", map.HouseholdsWhereTheChainStopped.Count));
        }

        sb.AppendLine();
        var counterfactual = new CounterfactualComparison
        {
            AsPlayed = map,
            HadPatientZeroBeenCovered = OutbreakMap.Build(s.BuildCounterfactual().Neighbourhood),
        };
        sb.AppendLine($"<b>{Localized(counterfactual.HeadlineKey)}</b>");
        if (!counterfactual.SquadMatchedIt)
        {
            sb.AppendLine(Localized("m3.debrief.casesAvoided", counterfactual.CasesAvoided));
        }

        sb.AppendLine();
        sb.AppendLine(Localized(InvisibleCaseDebrief.PromptKeyFor(map), map.InvisibleCases));

        sb.AppendLine();
        var card = Scoring.Evaluate(sim, s.Session);
        for (int i = 0; i < card.Lines.Count; i++)
        {
            var line = card.Lines[i];
            string text = Localized(line.MessageKey);
            sb.AppendLine(line.IsNote
                ? $"<color=#9AA5B1>{text} (x{line.Count})</color>"
                : $"{text} (x{line.Count})");
        }

        sb.AppendLine();
        sb.AppendLine($"<size={baseFontSize + 10}><b>{FieldJournalExport.ClosingPrompt}</b></size>");

        debriefText.text = sb.ToString();
        debriefPanel.SetActive(true);
        if (handoverPanel != null) handoverPanel.SetActive(false);
    }

    private static string Localized(string key, params object[] args)
    {
        if (string.IsNullOrEmpty(key)) return "";
        if (LocalizationManager.Instance == null) return key;
        return args == null || args.Length == 0
            ? LocalizationManager.Instance.GetLocalizedValue(key)
            : LocalizationManager.Instance.GetLocalizedValue(key, args);
    }

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

        statusLine = Text(canvasGo.transform, "Status", new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(24f, -48f), new Vector2(-24f, -8f), TextAlignmentOptions.TopLeft, baseFontSize);

        coachPanel = Text(canvasGo.transform, "Feedback", new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(24f, 16f), new Vector2(-24f, 72f), TextAlignmentOptions.BottomLeft, baseFontSize - 4);

        var bannerPanel = Panel(canvasGo.transform, "Banner", new Color(0.05f, 0.06f, 0.08f, 0.95f));
        var bannerRt = (RectTransform)bannerPanel.transform;
        bannerRt.anchorMin = new Vector2(0.5f, 0.5f);
        bannerRt.anchorMax = new Vector2(0.5f, 0.5f);
        bannerRt.sizeDelta = new Vector2(760f, 120f);
        banner = Text(bannerPanel.transform, "BannerText", Vector2.zero, Vector2.one,
            new Vector2(20f, 10f), new Vector2(-20f, -10f), TextAlignmentOptions.Center, baseFontSize + 4);
        bannerPanel.SetActive(false);

        handoverPanel = Panel(canvasGo.transform, "Handover", new Color(0.04f, 0.05f, 0.07f, 0.94f));
        handoverText = Text(handoverPanel.transform, "HandoverText", Vector2.zero, Vector2.one,
            new Vector2(96f, 72f), new Vector2(-96f, -72f), TextAlignmentOptions.TopLeft, baseFontSize + 4);
        FitToPanel(handoverText);
        handoverPanel.SetActive(false);

        debriefPanel = Panel(canvasGo.transform, "Debrief", new Color(0.04f, 0.05f, 0.07f, 0.97f));
        debriefText = Text(debriefPanel.transform, "DebriefText", Vector2.zero, Vector2.one,
            new Vector2(96f, 56f), new Vector2(-96f, -56f), TextAlignmentOptions.TopLeft, baseFontSize);
        FitToPanel(debriefText);
        debriefPanel.SetActive(false);
    }

    /// <summary>
    /// The handover and debrief run to different lengths (and Lao runs longer than English), and
    /// text that outgrows the panel spilled onto the scene below it - the debrief's closing
    /// question ended up outside the box. Shrink to fit instead.
    /// </summary>
    private void FitToPanel(TMP_Text text)
    {
        text.enableAutoSizing = true;
        text.fontSizeMax = text.fontSize;
        text.fontSizeMin = Mathf.Max(14f, text.fontSize * 0.55f);
        text.overflowMode = TextOverflowModes.Truncate;
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

        go.AddComponent<Image>().color = colour;
        return go;
    }

    private TMP_Text Text(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                          Vector2 offsetMin, Vector2 offsetMax, TextAlignmentOptions align, int size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = align;
        text.color = new Color(0.95f, 0.95f, 0.95f);
        text.richText = true;
        text.text = "";
        return text;
    }
}
