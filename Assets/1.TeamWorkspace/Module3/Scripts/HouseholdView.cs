using Aedes.Module3.Sim;
using UnityEngine;

/// <summary>
/// Shows one household as the simulation currently has it, and re-reads on every turn start.
///
/// Section 8: "Every handover should show consequence. The point of passing the headset is that
/// the world is different when you get it back." So this holds no state of its own - a house is
/// whatever the model says it is at the moment you walk in, which is what makes a return visit
/// worth making.
/// </summary>
public class HouseholdView : MonoBehaviour
{
    [Tooltip("Which plot in the neighbourhood this is.")]
    public int householdId;

    [Header("Where things go")]
    [Tooltip("Window opening the screen mounts into. Uses the prefab's Socket_Mount.")]
    [SerializeField] private Transform screenMount;
    [SerializeField] private Transform fanStand;
    [SerializeField] private Transform bedNetAnchor;

    [Tooltip("PF_ElectricFan is a 0.3 m table fan; stood on the floor it looks like a toy beside a "
             + "full-size door. Scaled uniformly (a non-uniform scale would skew the spinning blade) "
             + "up to a floor-standing fan.")]
    [SerializeField] private float fanScale = 2.5f;

    [Header("Prefabs (leave empty to load from Module3Props)")]
    [SerializeField] private GameObject screenIntactPrefab;
    [SerializeField] private GameObject screenTornPrefab;
    [SerializeField] private GameObject fanPrefab;
    [SerializeField] private GameObject netDeployedPrefab;
    [SerializeField] private GameObject netRolledUpPrefab;

    private GameObject screenInstance, fanInstance, netInstance;
    private ScreenState shownScreens = (ScreenState)(-1);
    private bool shownFan, shownNet;
    private bool netShownAtAll;
    // The room as first built is simply there; only changes after that are animated (M3PropSettle).
    private bool animateChanges;

    /// <summary>Wired by M3NeighbourhoodBuilder, which creates the anchors as it lays out a plot.</summary>
    public void Bind(Transform screen, Transform fan, Transform net)
    {
        screenMount = screen;
        fanStand = fan;
        bedNetAnchor = net;
        bound = true;
        Refresh();
    }

    // AddComponent runs OnEnable before the builder has set householdId or called Bind, so an
    // unconditional Refresh there drew house 0's state with no anchors - and cached it, which then
    // stopped the real house's screen from ever being placed when its state matched.
    private bool bound;

    private void OnEnable()
    {
        if (M3Session.Instance != null)
        {
            M3Session.Instance.OnTurnStarted += OnTurnStarted;
            M3Session.Instance.OnActionResolved += OnActionResolved;
        }
        if (bound) Refresh();
    }

    private void OnDisable()
    {
        if (M3Session.Instance != null)
        {
            M3Session.Instance.OnTurnStarted -= OnTurnStarted;
            M3Session.Instance.OnActionResolved -= OnActionResolved;
        }
    }

    private void OnTurnStarted(int turnIndex) => Refresh();

    // Nothing called Refresh after an action, so a net, screen or fan appeared three days later
    // at the next turn instead of when the Pilot put it up.
    private void OnActionResolved(ActionResult result) => Refresh();

    /// <summary>Called after any action so the room changes under the player's hands.</summary>
    public void Refresh()
    {
        var sim = M3Session.Instance != null ? M3Session.Instance.Neighbourhood : null;
        if (sim == null) return;

        var h = sim.HouseholdById(householdId);
        if (h == null) return;

        ShowScreens(h.Screens);
        ShowFan(h.HasFan);
        ShowNet(AnyoneHereHasANet(sim, h));
        animateChanges = true;
    }

    private static bool AnyoneHereHasANet(Neighbourhood sim, Household h)
    {
        for (int i = 0; i < h.ResidentIds.Count; i++)
        {
            var p = sim.PersonById(h.ResidentIds[i]);
            if (p != null && p.HasNet) return true;
        }
        return false;
    }

    private void ShowScreens(ScreenState state)
    {
        if (state == shownScreens) return;
        shownScreens = state;

        if (screenInstance != null) Destroy(screenInstance);
        if (screenMount == null) return;

        if (state == ScreenState.Missing)
        {
            // An empty window. The screen that will fill it is placed but not drawn, so it can be taken
            // hold of where it will go; the disc in the middle of the opening says what reaching there does.
            screenInstance = Place(Resolve(ref screenIntactPrefab, Module3Props.WindowScreenIntact), screenMount);
            if (screenInstance != null)
                foreach (var r in screenInstance.GetComponentsInChildren<Renderer>()) r.enabled = false;
            M3PropAction.Attach(screenInstance, M3Icons.Screen, ScreenAction, discInTheMiddle: true);
            return;
        }

        GameObject prefab = state == ScreenState.Intact
            ? Resolve(ref screenIntactPrefab, Module3Props.WindowScreenIntact)
            : Resolve(ref screenTornPrefab, Module3Props.WindowScreenTorn);

        screenInstance = Place(prefab, screenMount);
        // A torn screen is mended by taking hold of it.
        if (state == ScreenState.Torn) M3PropAction.Attach(screenInstance, M3Icons.Screen, ScreenAction);
        if (!animateChanges) return;
        if (state == ScreenState.Intact) M3PropSettle.SlideIn(screenInstance, Vector3.forward);   // +z is into the room
        else M3PropSettle.ShakeIn(screenInstance);
    }

    private PlayerAction? ScreenAction()
    {
        var h = Household;
        return h != null && h.Screens != ScreenState.Intact
            ? PlayerAction.OnHousehold(ActionKind.RepairScreen, householdId) : (PlayerAction?)null;
    }

    private void ShowFan(bool on)
    {
        if (on == shownFan && fanInstance != null) return;
        shownFan = on;

        // Every house has its fan; the question is whether it is going. A still one is switched on by
        // taking hold of it.
        if (fanInstance == null)
        {
            if (fanStand == null) return;
            fanInstance = Place(Resolve(ref fanPrefab, Module3Props.ElectricFan), fanStand);
            if (fanInstance == null) return;
            fanInstance.transform.localScale = Vector3.one * fanScale;
            var fresh = fanInstance.AddComponent<FanBladeSpin>();
            if (!on) fresh.StandStill();
            else if (animateChanges) fresh.SpinUpFromRest();
            M3PropAction.Attach(fanInstance, M3Icons.Fan, FanAction);
            return;
        }

        var spin = fanInstance.GetComponent<FanBladeSpin>();
        if (spin == null) return;
        if (on) spin.SpinUp();
        else if (animateChanges) spin.SpinDown();
        else spin.StandStill();
    }

    private PlayerAction? FanAction()
    {
        var h = Household;
        return h != null && !h.HasFan ? PlayerAction.OnHousehold(ActionKind.SetFan, householdId) : (PlayerAction?)null;
    }

    private void ShowNet(bool deployed)
    {
        if (netShownAtAll && deployed == shownNet) return;
        shownNet = deployed;
        netShownAtAll = true;

        if (netInstance != null) Destroy(netInstance);
        if (bedNetAnchor == null) return;

        // Both prefabs are placed at the bed's centre, but only the deployed net belongs there. The
        // rolled bundle is modelled a metre above its origin, so left at the centre it stood on
        // the chest of whoever was resting - who is exactly who the net is for.
        GameObject prefab = deployed
            ? Resolve(ref netDeployedPrefab, Module3Props.MosquitoNetDeployed)
            : Resolve(ref netRolledUpPrefab, Module3Props.MosquitoNetRolledUp);

        netInstance = Place(prefab, bedNetAnchor);
        if (!deployed) HangFromBedPost(netInstance);
        // The rolled net goes up when taken hold of. The one over the bed comes down the same way, to
        // carry on to someone else (D6) - but it never invites it: its disc shows only while pointed at,
        // and only its canopy can be held, so reaching for the person in the bed still reaches them.
        if (deployed) M3PropAction.Attach(netInstance, M3Icons.Net, NetDownAction, discOnlyOnHover: true, colliderTop: 0.3f);
        else M3PropAction.Attach(netInstance, M3Icons.Net, NetUpAction);
        if (!animateChanges) return;
        if (deployed) M3PropSettle.DropNet(netInstance);
        else M3PropSettle.GatherNet(netInstance);
    }

    private Household Household
    {
        get
        {
            var sim = M3Session.Instance != null ? M3Session.Instance.Neighbourhood : null;
            return sim?.HouseholdById(householdId);
        }
    }

    /// <summary>
    /// The net goes over the bed, so it covers whoever is lying in it: someone unwell, or someone
    /// whose fever has just broken (VillagerView lays both down). With nobody in bed it goes to the
    /// first person at home, as a well household would hang it.
    /// </summary>
    private PlayerAction? NetUpAction()
    {
        var sim = M3Session.Instance != null ? M3Session.Instance.Neighbourhood : null;
        var h = sim?.HouseholdById(householdId);
        if (h == null || AnyoneHereHasANet(sim, h)) return null;
        int chosen = -1;
        for (int i = 0; i < h.ResidentIds.Count; i++)
        {
            var o = Observe.Person(sim, h.ResidentIds[i]);
            if (o == null || o.Condition == VisibleCondition.AtTheHealthCentre) continue;
            if (o.IsUnwell || o.Condition == VisibleCondition.Improving) { chosen = o.PersonId; break; }
            if (chosen < 0) chosen = o.PersonId;
        }
        return chosen >= 0 ? PlayerAction.OnPerson(ActionKind.PutUpNet, chosen) : (PlayerAction?)null;
    }

    private PlayerAction? NetDownAction()
    {
        var sim = M3Session.Instance != null ? M3Session.Instance.Neighbourhood : null;
        var h = sim?.HouseholdById(householdId);
        if (h == null) return null;
        for (int i = 0; i < h.ResidentIds.Count; i++)
        {
            var p = sim.PersonById(h.ResidentIds[i]);
            if (p != null && p.HasNet) return PlayerAction.OnPerson(ActionKind.ReclaimNet, p.Id);
        }
        return null;
    }

    /// <summary>
    /// Slides a rolled-up net so its own Socket_Hook meets the bed's Socket_NetHook. It goes by the
    /// net's hook rather than its origin, so replacement art can shape the bundle however it likes
    /// as long as the hook stays where the bundle hangs from. This component sits on the plot and
    /// the bed is one of its children, so the socket is found from here; a bed without one leaves
    /// the net where it was placed.
    /// </summary>
    private void HangFromBedPost(GameObject net)
    {
        if (net == null) return;

        var bedHook = M3Sockets.Find(transform, Module3Props.Socket.BedNetHook);
        var netHook = M3Sockets.Find(net.transform, Module3Props.Socket.Hook);
        if (bedHook == null || netHook == null) return;

        net.transform.position += bedHook.position - netHook.position;
    }

    private static GameObject Place(GameObject prefab, Transform at)
    {
        if (prefab == null || at == null) return null;
        GameObject go = Instantiate(prefab, at.position, at.rotation, at);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        M3DistanceCulling.Mark(go);   // room props are not drawn from two houses away
        return go;
    }

    private static GameObject Resolve(ref GameObject cached, string path)
    {
        if (cached != null) return cached;
#if UNITY_EDITOR
        cached = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (cached == null) Debug.LogWarning($"Module 3: no prefab at {path}");
#else
        // In a player build the prefab must be wired in the inspector or live under Resources.
        Debug.LogWarning($"Module 3: prefab reference not assigned for {path}");
#endif
        return cached;
    }
}

/// <summary>
/// Turns the fan's blade. The blade is a separate mesh with its origin on the rotation axis for
/// exactly this reason - a welded blade would make the prop unusable.
///
/// A fan that has just been switched on winds up to speed over about a second, and one switched off
/// winds down, rather than going from still to full speed in a frame. Only the look lags: the
/// household has its fan from the moment the action resolves.
/// </summary>
public class FanBladeSpin : MonoBehaviour
{
    [Tooltip("Full speed, which the blade winds up to.")]
    [SerializeField] private float degreesPerSecond = 720f;
    [Tooltip("Seconds to wind from still to full speed, or back.")]
    [SerializeField] private float rampSeconds = 1f;
    private Transform blade;
    private float power = 1f;          // 0 still .. 1 full speed; a fan placed with the room is already going
    private bool running = true;
    private bool removeWhenStopped;

    /// <summary>True once this fan has asked to be destroyed: it is going, whatever anyone tells it now.</summary>
    public bool Removing { get; private set; }

    /// <summary>How fast the blade is turning now, degrees per second.</summary>
    public float Speed => degreesPerSecond * Mathf.SmoothStep(0f, 1f, power);

    /// <summary>Start from a standstill and wind up to <see cref="degreesPerSecond"/>.</summary>
    public void SpinUpFromRest()
    {
        power = 0f;
        SpinUp();
    }

    /// <summary>Switched off and still, as a fan nobody has turned on yet.</summary>
    public void StandStill()
    {
        power = 0f;
        running = false;
        removeWhenStopped = false;
    }

    public void SpinUp()
    {
        running = true;
        removeWhenStopped = false;
    }

    public void SpinDown(bool removeWhenStopped = false)
    {
        running = false;
        this.removeWhenStopped = removeWhenStopped;
    }

    private void Awake()
    {
        blade = transform.Find(Module3Props.FanBlade);
        if (blade == null)
        {
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                if (t.name == Module3Props.FanBlade) { blade = t; break; }
            }
        }
    }

    private void Update()
    {
        power = Mathf.MoveTowards(power, running ? 1f : 0f, Time.deltaTime / Mathf.Max(rampSeconds, 0.01f));
        if (blade != null) blade.Rotate(0f, 0f, Speed * Time.deltaTime, Space.Self);
        if (!running && power <= 0f && removeWhenStopped && !Removing)
        {
            Removing = true;
            Destroy(gameObject);
        }
    }
}
