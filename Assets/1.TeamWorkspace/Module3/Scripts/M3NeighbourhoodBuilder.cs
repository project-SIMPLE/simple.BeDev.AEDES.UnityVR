using System.Collections.Generic;
using Aedes.Module3.Sim;
using UnityEngine;

/// <summary>
/// Builds the neighbourhood from prefabs at runtime, from the simulation's own layout.
///
/// Deliberately not a hand-dressed scene. CLAUDE.md §9 and the roadmap both flag Unity YAML
/// merges as the thing most likely to hurt this project - Module 1's main scene is about 38,000
/// lines and two people cannot edit one at once. A neighbourhood placed by a builder keeps the
/// scene file to a handful of root objects, makes the layout reviewable as numbers rather than as
/// serialised transforms, and means two people can work on Module 3 at the same time.
///
/// The geometry comes from the same parameters GAMA sets. `lanes` and `households_per_lane` decide
/// both how the model's adjacency works and where the houses physically stand, so the two can
/// never drift apart - the plot you can see from the next plot is the plot a mosquito can reach.
/// </summary>
public class M3NeighbourhoodBuilder : MonoBehaviour
{
    [Header("Layout (metres)")]
    [Tooltip("Centre-to-centre spacing along a lane. Section 11 wants plots packed close enough "
             + "that the player can see one house from the next. The Lao house is 8.55 m wide with a "
             + "9.6 m roof, so below ~10 m neighbours touch and their roofs intersect. Visual only: "
             + "adjacency comes from lanes / households_per_lane.")]
    [SerializeField] private float plotWidth = 11f;

    [Tooltip("Distance across the lane, between the two facing rows.")]
    [SerializeField] private float laneSpacing = 16f;

    [Header("Prefabs")]
    [SerializeField] private GameObject housePrefab;
    [SerializeField] private GameObject bedPrefab;
    [SerializeField] private GameObject villagerPrefab;
    [SerializeField] private GameObject containerPrefab;

    [Tooltip("Where the yard containers stand, in front of the house on the lane side. The house "
             + "footprint ends at z = 3.4.")]
    [SerializeField] private float yardDepth = 4.6f;

    [Header("Anchors inside a plot (local to the house)")]
    [SerializeField] private Vector3 bedOffset = new Vector3(0f, 0f, 2.2f);
    [SerializeField] private Vector3 screenOffset = new Vector3(1.6f, 1.4f, 0.1f);
    [SerializeField] private Vector3 fanOffset = new Vector3(-1.4f, 0f, 2.0f);
    [SerializeField] private float villagerSpacing = 0.9f;

    public readonly List<HouseholdView> Households = new List<HouseholdView>();
    public readonly List<VillagerView> Villagers = new List<VillagerView>();

    private bool built;

    private void Start()
    {
        if (M3Session.Instance == null)
        {
            Debug.LogError("Module 3: no M3Session in the scene, so there is no neighbourhood to build.");
            return;
        }

        if (M3Session.Instance.Neighbourhood != null) Build();
        else M3Session.Instance.OnTurnStarted += BuildOnFirstTurn;
    }

    private void BuildOnFirstTurn(int turnIndex)
    {
        M3Session.Instance.OnTurnStarted -= BuildOnFirstTurn;
        Build();
    }

    public void Build()
    {
        if (built) return;
        built = true;

        var sim = M3Session.Instance.Neighbourhood;

        for (int i = 0; i < sim.Households.Count; i++)
        {
            BuildPlot(sim, sim.Households[i]);
        }

        Debug.Log($"Module 3: built {Households.Count} plots and {Villagers.Count} villagers "
                  + $"from seed {M3Session.Instance.Seed}.");
    }

    private void BuildPlot(Neighbourhood sim, Household h)
    {
        // Two rows facing each other across a lane, which is what makes the transmission chain
        // plausible - section 5: houses close together are not set dressing.
        bool farSide = h.LaneY % 2 == 1;
        var plotPosition = new Vector3(h.LaneX * plotWidth, 0f, farSide ? laneSpacing : 0f);
        var facing = Quaternion.Euler(0f, farSide ? 180f : 0f, 0f);

        var plot = new GameObject($"Plot_{h.Id}");
        plot.transform.SetParent(transform, false);
        plot.transform.SetPositionAndRotation(plotPosition, facing);

        if (housePrefab != null) Instantiate(housePrefab, plot.transform);

        var bed = bedPrefab != null
            ? Instantiate(bedPrefab, plot.transform).transform
            : Anchor(plot.transform, "BedAnchor", bedOffset);
        bed.localPosition = bedOffset;

        var screenMount = Anchor(plot.transform, "ScreenMount", screenOffset);
        var fanStand = Anchor(plot.transform, "FanStand", fanOffset);
        var netAnchor = Anchor(plot.transform, "NetAnchor", bedOffset);

        var view = plot.AddComponent<HouseholdView>();
        view.householdId = h.Id;
        view.Bind(screenMount, fanStand, netAnchor);
        Households.Add(view);

        // The containers are scenery and the visible reason there are mosquitoes here. Section 5:
        // they are "not a button, not a task, not a score" - nothing here is interactive.
        if (containerPrefab != null)
        {
            for (int c = 0; c < h.Containers.Count; c++)
            {
                if (!h.Containers[c].IsProductive) continue;
                // In the lane-side yard, in front of the house. At z = -1.8 they were inside the
                // 6.4 m-deep house, where nobody walking the lane could see them.
                var yard = Anchor(plot.transform, $"Container_{h.Containers[c].Id}",
                    new Vector3(-2.5f + c * 0.9f, 0f, yardDepth));
                Instantiate(containerPrefab, yard);
            }
        }

        for (int r = 0; r < h.ResidentIds.Count; r++)
        {
            BuildVillager(sim, plot.transform, h.ResidentIds[r], r, bed);
        }
    }

    private void BuildVillager(Neighbourhood sim, Transform plot, int personId, int index, Transform bed)
    {
        var spot = Anchor(plot, $"Villager_{personId}",
            new Vector3(-1.2f + index * villagerSpacing, 0f, 1.0f));

        var villager = villagerPrefab != null ? Instantiate(villagerPrefab, spot) : new GameObject("Villager");
        if (villagerPrefab == null) villager.transform.SetParent(spot, false);

        var view = villager.GetComponent<VillagerView>();
        if (view == null) view = villager.AddComponent<VillagerView>();
        view.personId = personId;
        view.RestingPlace = bed;
        Villagers.Add(view);
    }

    private static Transform Anchor(Transform parent, string name, Vector3 localPosition)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        return t;
    }

    /// <summary>Where plot <paramref name="householdId"/> stands, for the map and the Coach view.</summary>
    public Vector3 PlotPosition(int householdId)
    {
        for (int i = 0; i < Households.Count; i++)
        {
            if (Households[i].householdId == householdId) return Households[i].transform.position;
        }
        return Vector3.zero;
    }
}
