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
/// So this is a screen-space overlay with large type. A HUD that only works inside the headset
/// would leave two thirds of the squad watching, which is the failure mode the squad structure
/// exists to prevent.
///
/// The canvas is built in code so the scene file stays small enough to merge - see
/// Module3SceneBuilder.
/// </summary>
public class M3Hud : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private int baseFontSize = 28;

    [Tooltip("Seconds the handover screen stays up before the next turn can start.")]
    [SerializeField] private float handoverSeconds = 12f;

    private TMP_Text statusLine;
    private TMP_Text coachPanel;
    private GameObject handoverPanel;
    private TMP_Text handoverText;
    private GameObject debriefPanel;
    private TMP_Text debriefText;

    private float handoverShownAt = -1f;
    private readonly StringBuilder sb = new StringBuilder();

    private void Awake()
    {
        BuildCanvas();
    }

    // M3Session sets Instance in its Awake, and Awake/OnEnable order across GameObjects is not
    // defined. When this OnEnable ran first it silently subscribed to nothing, so the handover
    // screen, the debrief and action feedback never appeared. Start runs after every Awake in the
    // scene, so try again there.
    private M3Session subscribedTo;

    private void OnEnable() => Subscribe();
    private void Start() => Subscribe();

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

        if (handoverShownAt > 0f && Time.time - handoverShownAt > handoverSeconds)
        {
            handoverShownAt = -1f;
            if (handoverPanel != null) handoverPanel.SetActive(false);
        }
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
            statusLine.text = $"<b>Session over</b>   Day {sim.Day}   Nets {sim.NetsRemaining}";
            return;
        }

        float left = Mathf.Max(0f, s.TurnSecondsRemaining);

        // The countdown is the Coach's instrument as much as the Pilot's, so it goes first and
        // turns red where a class can see it from across a table.
        string clock = $"{Mathf.FloorToInt(left / 60f)}:{Mathf.FloorToInt(left % 60f):00}";
        string colour = left <= 30f ? "#E4572E" : "#F2F2F2";

        statusLine.text =
            $"<color={colour}><b>{clock}</b></color>   "
            + $"Turn {s.Session.TurnIndex + 1}/{s.Session.TotalTurns}   "
            + $"Round {s.Session.RoundIndex + 1}   Player {s.Session.PilotIndex + 1}   "
            + $"Day {sim.Day}   Nets {sim.NetsRemaining}";
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

        handoverText.text = sb.ToString();
        handoverPanel.SetActive(true);
        handoverShownAt = Time.time;
        // The next Pilot's turn starts when they can see, not while the old one is still reading.
        if (M3Session.Instance != null) M3Session.Instance.HoldTurnClock(handoverSeconds);
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

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);

        statusLine = Text(canvasGo.transform, "Status", new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(24f, -48f), new Vector2(-24f, -8f), TextAlignmentOptions.TopLeft, baseFontSize);

        coachPanel = Text(canvasGo.transform, "Feedback", new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(24f, 16f), new Vector2(-24f, 72f), TextAlignmentOptions.BottomLeft, baseFontSize - 4);

        handoverPanel = Panel(canvasGo.transform, "Handover", new Color(0.04f, 0.05f, 0.07f, 0.94f));
        handoverText = Text(handoverPanel.transform, "HandoverText", Vector2.zero, Vector2.one,
            new Vector2(96f, 72f), new Vector2(-96f, -72f), TextAlignmentOptions.TopLeft, baseFontSize + 4);
        handoverPanel.SetActive(false);

        debriefPanel = Panel(canvasGo.transform, "Debrief", new Color(0.04f, 0.05f, 0.07f, 0.97f));
        debriefText = Text(debriefPanel.transform, "DebriefText", Vector2.zero, Vector2.one,
            new Vector2(96f, 56f), new Vector2(-96f, -56f), TextAlignmentOptions.TopLeft, baseFontSize);
        debriefPanel.SetActive(false);
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
