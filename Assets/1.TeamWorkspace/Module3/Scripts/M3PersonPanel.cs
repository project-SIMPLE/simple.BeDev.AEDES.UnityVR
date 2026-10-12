using System.Collections.Generic;
using Aedes.Module3.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The conversation with one villager, as a small card standing beside them in the room.
///
/// It is the household visit of section 7 in pictures: what you notice (a thermometer and how many
/// days, a drop of blood, a bucket, no water within reach, a net already up) and what you can do for
/// them (water, rest, repellent, the health centre) and for the house (shut up at dawn and dusk) - the
/// closed verb list of section 4, never "diagnose" or "treat". Everything on it comes from Observe, so
/// the card can show nothing the model says the volunteer could not have seen.
///
/// No paragraphs: each icon's sentence appears in the caption line only while the Pilot points at it,
/// and a done action keeps its place with a tick. The net, the window screen and the fan are not on
/// the card at all - they are things in the room, and the Pilot acts on them directly (M3PropAction).
///
/// Framing rule (section 5): every villager opens the same card the same way, in the same colours.
/// Nothing about a sick person's card looks like a warning; it is a conversation, not an alert.
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
    private TextMeshProUGUI caption;
    private Transform villager;
    private Vector3 conversationAt;   // where the villager stood when it began
    private string lastFeedbackKey;
    /// <summary>What the card says back after the last action, if that action needs saying anything.</summary>
    private string replyKey;
    /// <summary>The sentence of whatever the Pilot is pointing at, shown instead of the reply while they point.</summary>
    private string hoverKey;
    private object[] hoverArgs;

    private M3Session S => M3Session.Instance;

    private void Awake()
    {
        Instance = this;
        root = M3Ui.WorldCanvas("Person Panel", transform, new Vector2(PanelWidth, 400f), unitsPerMetre);
        // On the UI layer, after the world: a card hung beside a villager by a wall used to lose its
        // right third inside the wall.
        M3Ui.OnTop(root);
        M3Ui.Panel(root, "Background", M3Ui.PanelColour);
        stack = M3Ui.Stack(root, "Content", 18f, new RectOffset((int)PanelPadding, (int)PanelPadding, 22, 22));
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
        replyKey = ReplyKey(r);
        Rebuild();
    }

    /// <summary>
    /// Most actions answer in the room - water appears by the bed, the fan spins up - and need no words.
    /// The referral is the one that teaches by its reply (section 10), so it keeps a short one.
    /// </summary>
    private static string ReplyKey(ActionResult r) => r.Outcome switch
    {
        ActionOutcome.ReferralCorrect => "m3.short.referralCorrect",
        ActionOutcome.ReferralNotNeeded => r.MessageKey == "m3.action.referralNotNeededWell"
            ? "m3.short.referralNotNeededWell" : "m3.short.referralNotNeeded",
        _ => null,
    };

    /// <summary>Opens the conversation with <paramref name="personId"/>, standing beside <paramref name="body"/>.</summary>
    public void Open(int personId, Transform body)
    {
        if (S == null || S.Session == null || !S.Running) return;
        if (PersonId >= 0 && PersonId != personId) M3VillagerLife.Talking(PersonId, false);   // turned to someone else
        PersonId = personId;
        villager = body;
        conversationAt = body.position;
        lastFeedbackKey = null;
        replyKey = null;
        hoverKey = null;

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
        hoverKey = null;
        var sim = S.Neighbourhood;
        var obs = Observe.Person(sim, PersonId);
        if (obs == null) { Close(); return; }

        // What you notice, and the way out.
        var top = M3Ui.Row(stack, "Noticed", 12f);
        AddNoticed(top, obs);
        M3Ui.Spacer(top);
        var close = M3Ui.IconButton(top, M3Icons.Close, CloseSize, Close);
        close.GetComponent<Image>().color = M3Ui.ChipColour;
        M3Hover.On(close, on => Hover(on, "m3.ui.close"));

        if (obs.Condition != VisibleCondition.AtTheHealthCentre)
        {
            // What you can do: for them, then - after a thin rule - for the house.
            var row = M3Ui.Row(stack, "Actions", ButtonGap, TextAnchor.MiddleCenter);
            foreach (var a in Observe.ActionsFor(sim, PersonId))
                if (OnTheCard(a.Kind)) AddAction(row, a);
            var rule = M3Ui.Rect("Rule", row, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            rule.color = M3Ui.ChipColour;
            rule.raycastTarget = false;
            var le = rule.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = 4f;
            le.preferredHeight = le.minHeight = ButtonSize * 0.8f;
            foreach (var a in Observe.ActionsFor(sim, obs.HouseholdId, household: true))
                if (OnTheCard(a.Kind)) AddAction(row, a);
        }

        // Room for two lines, always, so the card does not grow and shrink under a moving ray.
        caption = M3Ui.Line(stack, "", ReadingSize, M3Ui.Muted);
        caption.alignment = TextAlignmentOptions.Top;
        M3Ui.Height(caption, ReadingSize * 2.6f);
        ShowCaption();

        LayoutRebuilder.ForceRebuildLayoutImmediate(stack);
        // Grow the card to fit its content.
        float h = LayoutUtility.GetPreferredHeight(stack);
        root.sizeDelta = new Vector2(root.sizeDelta.x, Mathf.Max(200f, h));
        KeepInsideTheRoom();
    }

    /// <summary>
    /// What the Pilot can see and hear, one chip each. The household's words and the signs in the room
    /// often say the same thing (they are bleeding / blood on a cloth), so the chip is the thing itself
    /// and both sentences are its caption.
    /// </summary>
    private void AddNoticed(Transform row, PersonObservation obs)
    {
        string icon = obs.DialogueKey switch
        {
            "m3.say.hotSinceYesterday" or "m3.say.hotForDays" => M3Icons.Fever,
            "m3.say.feelingBetterToday" => M3Icons.FeverDown,
            "m3.say.bleeding" => M3Icons.Bleeding,
            "m3.say.cannotKeepWaterDown" => M3Icons.Vomiting,
            "m3.say.atHealthCentre" => M3Icons.HealthCentre,
            _ => M3Icons.Fine,
        };
        // How long they have been unwell is the one number the household gives: a sun and a count.
        bool counted = obs.Condition != VisibleCondition.Seems_Well && obs.Condition != VisibleCondition.AtTheHealthCentre;
        int days = Mathf.Max(1, obs.DialogueDays);
        var said = M3Ui.Chip(row, icon, ChipSize, M3Ui.Text, counted ? M3Icons.Day : null, counted ? days.ToString() : null);
        string sentence = M3Ui.L(obs.DialogueKey, obs.DialogueDays);
        foreach (var sign in obs.Signs)
            if (sign.Key != "m3.see.netIsUp" && sign.Key != "m3.see.noWaterWithinReach") sentence += "\n" + M3Ui.L(sign.Key);
        M3Hover.On(said, on => HoverText(on, sentence));

        foreach (var sign in obs.Signs)
        {
            if (sign.Key == "m3.see.netIsUp") Sign(row, M3Icons.NetUp, sign.Key);
            else if (sign.Key == "m3.see.noWaterWithinReach") Sign(row, M3Icons.NoWater, sign.Key);
        }
    }

    private void Sign(Transform row, string icon, string key)
    {
        var chip = M3Ui.Chip(row, icon, ChipSize, M3Ui.Text);
        M3Hover.On(chip, on => Hover(on, key));
    }

    /// <summary>The net, the screen and the fan are acted on in the room itself, not from the card.</summary>
    private static bool OnTheCard(ActionKind kind) => kind switch
    {
        ActionKind.BringWater or ActionKind.HelpRest or ActionKind.GiveRepellent
            or ActionKind.ReferToHealthCentre or ActionKind.AdviseClosingHours => true,
        _ => false,
    };

    private static string IconFor(ActionKind kind) => kind switch
    {
        ActionKind.BringWater => M3Icons.Water,
        ActionKind.HelpRest => M3Icons.Rest,
        ActionKind.GiveRepellent => M3Icons.Repellent,
        ActionKind.ReferToHealthCentre => M3Icons.HealthCentre,
        ActionKind.AdviseClosingHours => M3Icons.CloseAtDusk,
        _ => M3Icons.Check,
    };

    private void AddAction(Transform row, AvailableAction a)
    {
        var action = a.ToAction();
        // Every verb on the card is disabled only because it is done already (Observe gives the reason).
        bool done = !a.Enabled;
        var button = M3Ui.IconButton(row, IconFor(a.Kind), ButtonSize, () => S.Do(action), a.Enabled, done);
        string key = done && !string.IsNullOrEmpty(a.DisabledReasonKey) ? a.DisabledReasonKey : a.LabelKey;
        M3Hover.On(button, on => Hover(on, key));
    }

    private void Hover(bool on, string key) => SetHover(on, key, null);
    private void HoverText(bool on, string text) => SetHover(on, null, text);

    private string hoverText;

    private void SetHover(bool on, string key, string text)
    {
        if (on) { hoverKey = key; hoverText = text; }
        else if ((key != null && hoverKey == key) || (text != null && hoverText == text)) { hoverKey = null; hoverText = null; }
        ShowCaption();
    }

    private void ShowCaption()
    {
        if (caption == null) return;
        bool pointing = hoverKey != null || hoverText != null;
        caption.text = hoverText ?? (hoverKey != null ? M3Ui.L(hoverKey) : replyKey != null ? M3Ui.L(replyKey) : "");
        caption.color = pointing ? M3Ui.Muted : M3Ui.Accent;
    }

    /// <summary>
    /// The caption follows the 1.5 degree rule: the smallest size whose letters are 1.5 degrees tall at
    /// 1.5 m, worked out from this panel's own scale so it still holds if the scale is changed.
    /// </summary>
    private float ReadingSize => M3TextSize.Floor(unitsPerMetre);

    // Five buttons and a rule across: 5 x 120 + 5 x 16 gaps + 4 + padding. A button is 14 cm square,
    // big enough to poke without looking twice and to hit with the ray from across the room.
    private const float PanelWidth = 740f, PanelPadding = 26f;
    private const float ButtonSize = 120f, ButtonGap = 16f, ChipSize = 72f, CloseSize = 80f;

    /// <summary>The lowest the card may hang above the floor, and the highest its top may reach: a room is about 2.7 m under its roof.</summary>
    private const float LowestAboveFloor = 0.15f, HighestAboveFloor = 2.5f;

    /// <summary>
    /// The card is centred a little below the Pilot's eyes. A Pilot taller than about 1.9 m would have had its top
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
}
