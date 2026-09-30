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
        root = M3Ui.WorldCanvas("Person Panel", transform, new Vector2(PanelWidth, 900f), unitsPerMetre);
        M3Ui.Panel(root, "Background", M3Ui.PanelColour);
        stack = M3Ui.Stack(root, "Content", 7f, new RectOffset((int)PanelPadding, (int)PanelPadding, 22, 22));
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
        if (PersonId >= 0 && PersonId != personId) M3VillagerLife.Talking(PersonId, false);   // turned to someone else
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
        if (IsOpen)
        {
            M3PanelPop.Play(root);
            M3VillagerLife.Talking(PersonId, true);   // they nod, and gesture a little while you talk
        }
    }

    public void Close()
    {
        if (PersonId >= 0) M3VillagerLife.Talking(PersonId, false);
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
        M3Ui.Line(stack, M3Ui.L("m3.ui.youSee"), ReadingSize, M3Ui.Muted);
        var seen = new List<string>();
        foreach (var sign in obs.Signs) seen.Add(M3Ui.L(sign.Key));
        if (seen.Count == 0) seen.Add(M3Ui.L("m3.see.nothingOutOfTheOrdinary"));
        M3Ui.Line(stack, string.Join("\n", seen), ReadingSize, M3Ui.Text);

        // What the household says.
        if (!string.IsNullOrEmpty(obs.DialogueKey))
        {
            M3Ui.Line(stack, M3Ui.L("m3.ui.theySay"), ReadingSize, M3Ui.Muted);
            M3Ui.Line(stack, $"<i>“{M3Ui.L(obs.DialogueKey, obs.DialogueDays)}”</i>", ReadingSize, M3Ui.Accent);
        }

        if (obs.Condition == VisibleCondition.AtTheHealthCentre)
        {
            AddFooter();
            return;
        }

        // What you can do for them.
        M3Ui.Line(stack, M3Ui.L("m3.ui.forThem"), ReadingSize, M3Ui.Muted);
        foreach (var a in Observe.ActionsFor(sim, PersonId)) AddAction(a);

        // And for the house.
        M3Ui.Line(stack, M3Ui.L("m3.ui.forTheHouse"), ReadingSize, M3Ui.Muted);
        foreach (var a in Observe.ActionsFor(sim, obs.HouseholdId, household: true)) AddAction(a);

        AddFooter();
    }

    /// <summary>
    /// The section labels ("You see:", "They tell you:", ...) were 22 units: a degree tall at arm's length,
    /// too small to read across a room. This is the smallest size whose letters are 1.5 degrees tall at
    /// 1.5 m, worked out from this panel's own scale so it still holds if the scale is changed. What you
    /// see and what they say go up with the labels, so a label is never bigger than the words it labels;
    /// the colour (muted, white, gold) tells them apart.
    /// </summary>
    private float ReadingSize => M3TextSize.Floor(unitsPerMetre);

    // The card's width and the padding round its content (see Awake). The buttons and the reply lines
    // used to sit below the reading size (26 / 28 / 24 units, against 34): smaller than the labels
    // above them, and under the 1.5 degree rule. They follow ReadingSize now, and the card is a little
    // wider so the longer labels still fit on one line.
    private const float PanelWidth = 680f, PanelPadding = 26f;
    /// <summary>The text width inside a button: the card minus its padding, minus the button's own 16 + 16.</summary>
    private const float ButtonLabelWidth = PanelWidth - 2f * PanelPadding - 32f;

    private void AddAction(AvailableAction a)
    {
        var action = a.ToAction();
        M3Ui.Button(stack, M3Ui.L(a.LabelKey), ReadingSize * 1.6f, () => S.Do(action), a.Enabled, ReadingSize, ButtonLabelWidth);
        if (!a.Enabled && !string.IsNullOrEmpty(a.DisabledReasonKey))
            M3Ui.Line(stack, M3Ui.L(a.DisabledReasonKey), ReadingSize, M3Ui.Muted);
    }

    private void AddFooter()
    {
        if (!string.IsNullOrEmpty(lastFeedbackKey))
            feedback = M3Ui.Line(stack, M3Ui.L(lastFeedbackKey), ReadingSize, M3Ui.Accent);
        M3Ui.Button(stack, M3Ui.L("m3.ui.close"), ReadingSize * 1.6f, Close, true, ReadingSize, ButtonLabelWidth);
        LayoutRebuilder.ForceRebuildLayoutImmediate(stack);
        // Grow the card to fit its content, so a short conversation is not a tall empty panel.
        float h = LayoutUtility.GetPreferredHeight(stack);
        root.sizeDelta = new Vector2(root.sizeDelta.x, Mathf.Max(300f, h));
        KeepInsideTheRoom();
    }

    /// <summary>The lowest the card may hang above the floor, and the highest its top may reach: a room is about 2.7 m under its roof.</summary>
    private const float LowestAboveFloor = 0.15f, HighestAboveFloor = 2.5f;

    /// <summary>
    /// The card is centred a little below the Pilot's eyes, and the bigger text makes it up to 1.8 m tall
    /// (more in a language with taller lines). A Pilot taller than about 1.9 m would have had its top
    /// through the ceiling, and a short child its bottom in the floor. Slides it up or down to fit
    /// between the floor and the ceiling. The floor is the XR rig's origin (M3XRPlayer asks for
    /// floor tracking, so that is where the Pilot stands). A ray down from the head does not do: it
    /// starts just above the Pilot's own body capsule and hits that.
    /// </summary>
    private void KeepInsideTheRoom()
    {
        var head = Camera.main != null ? Camera.main.transform : null;
        if (head == null) return;
        var player = M3XRPlayer.Instance;
        float floor = player != null && player.Rig != null ? player.Rig.transform.position.y : head.position.y - 1.6f;
        float half = root.sizeDelta.y / unitsPerMetre * 0.5f;
        float lowest = floor + LowestAboveFloor + half, highest = floor + HighestAboveFloor - half;
        var at = root.position;
        at.y = lowest <= highest ? Mathf.Clamp(at.y, lowest, highest) : floor + (LowestAboveFloor + HighestAboveFloor) * 0.5f;
        root.position = at;
    }

    private static string WhoKey(AgeBand age) => age switch
    {
        AgeBand.Child => "m3.who.child",
        AgeBand.Elder => "m3.who.elder",
        _ => "m3.who.adult",
    };
}
