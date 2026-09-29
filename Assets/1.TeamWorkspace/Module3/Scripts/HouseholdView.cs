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
        if (screenMount == null || state == ScreenState.Missing) return;

        GameObject prefab = state == ScreenState.Intact
            ? Resolve(ref screenIntactPrefab, Module3Props.WindowScreenIntact)
            : Resolve(ref screenTornPrefab, Module3Props.WindowScreenTorn);

        screenInstance = Place(prefab, screenMount);
    }

    private void ShowFan(bool on)
    {
        if (on == shownFan && fanInstance != null) return;
        shownFan = on;

        if (!on)
        {
            if (fanInstance != null) Destroy(fanInstance);
            return;
        }

        if (fanStand == null || fanInstance != null) return;
        fanInstance = Place(Resolve(ref fanPrefab, Module3Props.ElectricFan), fanStand);
        if (fanInstance != null) fanInstance.AddComponent<FanBladeSpin>();
    }

    private void ShowNet(bool deployed)
    {
        if (netShownAtAll && deployed == shownNet) return;
        shownNet = deployed;
        netShownAtAll = true;

        if (netInstance != null) Destroy(netInstance);
        if (bedNetAnchor == null) return;

        // The two net prefabs share an origin, so this is a straight swap at one transform.
        GameObject prefab = deployed
            ? Resolve(ref netDeployedPrefab, Module3Props.MosquitoNetDeployed)
            : Resolve(ref netRolledUpPrefab, Module3Props.MosquitoNetRolledUp);

        netInstance = Place(prefab, bedNetAnchor);
    }

    private static GameObject Place(GameObject prefab, Transform at)
    {
        if (prefab == null || at == null) return null;
        GameObject go = Instantiate(prefab, at.position, at.rotation, at);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
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
/// </summary>
public class FanBladeSpin : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = 720f;
    private Transform blade;

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
        if (blade != null) blade.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
