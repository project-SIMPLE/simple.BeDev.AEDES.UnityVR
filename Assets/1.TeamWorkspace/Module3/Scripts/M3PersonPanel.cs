using System.Collections.Generic;
using Aedes.Module3.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The conversation with one villager, as a panel standing beside them in the room.
///
/// It is the whole household visit of section 7 in one place: who this is, what you can see
/// (step 3), what the family tells you in plain words (step 2), and what you can do about it -
/// the closed verb list of section 4, never "diagnose" or "treat". Everything on it comes from
/// Observe, so the panel can show nothing the model says the volunteer could not have seen.
///
/// Framing rule (section 5): every villager opens the same panel the same way. Nothing about a
/// sick person's panel looks like a warning; it is a conversation, not an alert.
///
/// Pressable with the ray (point and pull the trigger) or by poking it with a finger.
/// </summary>
public class M3PersonPanel : MonoBehaviour
{
    public static M3PersonPanel Instance { get; private set; }

    [SerializeField] private float closeDistance = 5.5f;

    [Header("Where the panel floats")]
    [Tooltip("How far from the Pilot's head. A menu is read and pressed at arm's reach: beside the "
             + "villager, 3 m away, its text was a few pixels tall.")]
    [SerializeField] private float panelDistance = 1.3f;
    [Tooltip("Degrees to the side of the villager, so the panel does not hide the person you are talking to.")]
    [SerializeField] private float sideAngle = 28f;
    [Tooltip("Canvas units per metre. Lower = bigger panel and text.")]
    [SerializeField] private float unitsPerMetre = 850f;

    public int PersonId { get; private set; } = -1;
    public bool IsOpen => root != null && root.gameObject.activeSelf;

    private RectTransform root;
    private RectTransform stack;
    private TextMeshProUGUI feedback;
    private Transform villager;
    private Vector3 conversationAt;   // where the villager stood when it began
    private string lastFeedbackKey;

    private M3Session S => M3Session.Instance;

    private void Awake()
    {
        Instance = this;
        root = M3Ui.WorldCanvas("Person Panel", transform, new Vector2(620f, 900f), unitsPerMetre);
        M3Ui.Panel(root, "Background", M3Ui.PanelColour);
        stack = M3Ui.Stack(root, "Content", 7f, new RectOffset(26, 26, 22, 22));
        root.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (S != null) { S.OnActionResolved += OnAction; S.OnTurnStarted += OnTurn; }
    }

    private void OnDisable()
    {
        if (S != null) { S.OnActionResolved -= OnAction; S.OnTurnStarted -= OnTurn; }
    }

    private void Start()
    {
        // Subscribe again once M3Session has certainly woken (see M3Hud for the race).
        OnDisable();
        OnEnable();
    }

    private void OnTurn(int _) => Close();

    private void OnAction(ActionResult r)
    {
        if (!IsOpen) return;
        lastFeedbackKey = r.MessageKey;
        Rebuild();
    }

    /// <summary>Opens the conversation with <paramref name="personId"/>, standing beside <paramref name="body"/>.</summary>
    public void Open(int personId, Transform body)
    {
        if (S == null || S.Session == null || !S.Running) return;
        PersonId = personId;
        villager = body;
        conversationAt = body.position;
        lastFeedbackKey = null;

        // Talking to someone is looking in on their household.
        var p = S.Neighbourhood.PersonById(personId);
        if (p != null) M3VisitTracker.Visit(p.HouseholdId);

        Place();
        root.gameObject.SetActive(true);
        Rebuild();
    }

    public void Close()
    {
        if (root != null) root.gameObject.SetActive(false);
        PersonId = -1;
        villager = null;
    }

    private void Update()
    {
        if (!IsOpen) return;
        var head = Camera.main != null ? Camera.main.transform : null;
        if (head == null || villager == null) return;
        // Measured from where the conversation began, not from the body: helping someone rest or
        // sending them to the health centre moves their body (to the bed, back to their spot in the
        // room), and a panel that closed itself the moment the referral went through never showed
        // the reply to it.
        Vector3 d = conversationAt - head.position;
        d.y = 0f;
        if (d.magnitude > closeDistance) Close();
    }

    /// <summary>
    /// At arm's reach in the direction of the villager, turned a little to the right of them so
    /// both can be seen, and facing the Pilot.
    /// </summary>
    private void Place()
    {
        var head = Camera.main.transform;
        Vector3 toVillager = villager.position - head.position;
        toVillager.y = 0f;
        if (toVillager.sqrMagnitude < 0.01f) toVillager = head.forward;
        toVillager.Normalize();

        // Never further than the villager, so the panel is between the two of you.
        float dist = Mathf.Min(panelDistance, Mathf.Max(0.8f, Vector3.Distance(head.position, villager.position) - 0.5f));
        Vector3 dir = Quaternion.Euler(0f, sideAngle, 0f) * toVillager;
        Vector3 at = head.position + dir * dist;
        at.y = head.position.y - 0.1f;
        root.position = at;
        Vector3 look = at - head.position;
        look.y = 0f;
        root.rotation = Quaternion.LookRotation(look, Vector3.up);
    }

    private void Rebuild()
    {
        M3Ui.Clear(stack);
        var sim = S.Neighbourhood;
        var obs = Observe.Person(sim, PersonId);
        if (obs == null) { Close(); return; }

        // Who.
        M3Ui.Line(stack, $"<b>{M3Ui.L(WhoKey(obs.Age))}</b>", 38f, M3Ui.Text);

        // What you can see.
        M3Ui.Line(stack, M3Ui.L("m3.ui.youSee"), 22f, M3Ui.Muted);
        var seen = new List<string>();
        foreach (var sign in obs.Signs) seen.Add(M3Ui.L(sign.Key));
        if (seen.Count == 0) seen.Add(M3Ui.L("m3.see.nothingOutOfTheOrdinary"));
        M3Ui.Line(stack, string.Join("\n", seen), 28f, M3Ui.Text);

        // What the household says.
        if (!string.IsNullOrEmpty(obs.DialogueKey))
        {
            M3Ui.Line(stack, M3Ui.L("m3.ui.theySay"), 22f, M3Ui.Muted);
            M3Ui.Line(stack, $"<i>“{M3Ui.L(obs.DialogueKey, obs.DialogueDays)}”</i>", 28f, M3Ui.Accent);
        }

        if (obs.Condition == VisibleCondition.AtTheHealthCentre)
        {
            AddFooter();
            return;
        }

        // What you can do for them.
        M3Ui.Line(stack, M3Ui.L("m3.ui.forThem"), 22f, M3Ui.Muted);
        foreach (var a in Observe.ActionsFor(sim, PersonId)) AddAction(a);

        // And for the house.
        M3Ui.Line(stack, M3Ui.L("m3.ui.forTheHouse"), 22f, M3Ui.Muted);
        foreach (var a in Observe.ActionsFor(sim, obs.HouseholdId, household: true)) AddAction(a);

        AddFooter();
    }

    private void AddAction(AvailableAction a)
    {
        var action = a.ToAction();
        M3Ui.Button(stack, M3Ui.L(a.LabelKey), 50f, () => S.Do(action), a.Enabled, 26f);
        if (!a.Enabled && !string.IsNullOrEmpty(a.DisabledReasonKey))
            M3Ui.Line(stack, M3Ui.L(a.DisabledReasonKey), 20f, M3Ui.Muted);
    }

    private void AddFooter()
    {
        if (!string.IsNullOrEmpty(lastFeedbackKey))
            feedback = M3Ui.Line(stack, M3Ui.L(lastFeedbackKey), 24f, M3Ui.Accent);
        M3Ui.Button(stack, M3Ui.L("m3.ui.close"), 46f, Close, true, 24f);
        LayoutRebuilder.ForceRebuildLayoutImmediate(stack);
        // Grow the card to fit its content, so a short conversation is not a tall empty panel.
        float h = LayoutUtility.GetPreferredHeight(stack);
        root.sizeDelta = new Vector2(root.sizeDelta.x, Mathf.Max(300f, h));
    }

    private static string WhoKey(AgeBand age) => age switch
    {
        AgeBand.Child => "m3.who.child",
        AgeBand.Elder => "m3.who.elder",
        _ => "m3.who.adult",
    };
}
