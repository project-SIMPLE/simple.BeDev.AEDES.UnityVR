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
    [SerializeField] private float plotWidth = 14.5f;

    [Tooltip("Distance across the lane, between the two facing rows.")]
    [SerializeField] private float laneSpacing = 18f;

    [Header("Prefabs")]
    [SerializeField] private GameObject housePrefab;
    [SerializeField] private GameObject bedPrefab;
    [SerializeField] private GameObject villagerPrefab;

    [Tooltip("Purpose-built child bodies (Animation Brief section 7, option 2): their own skeleton and "
             + "proportions, not a scaled adult. One is picked per person. Empty falls back to "
             + "villagerPrefab scaled down.")]
    [SerializeField] private GameObject[] childPrefabs;

    [Tooltip("Purpose-built elder bodies. Empty falls back to villagerPrefab, scaled.")]
    [SerializeField] private GameObject[] elderPrefabs;
    [SerializeField] private GameObject containerPrefab;

    [Tooltip("Where the yard containers stand, in front of the house on the lane side. The house "
             + "footprint ends at z = 3.4.")]
    [SerializeField] private float yardDepth = 6.2f;

    [Header("House size")]
    [Tooltip("How much the Lao house model is scaled up. As modelled its doorway is 1.53 m clear - lower "
             + "than the 1.7 m villagers who live in it, and lower than a standing adult's head - so the "
             + "Pilot's headset pushed through the wall above the door. 1.4 makes it 2.1 m, with the "
             + "house 12 x 9 m inside. Everything placed in house space (bed, screen, fan, villagers, "
             + "yard) is multiplied by this too; plotWidth and laneSpacing are metres and already allow for it.")]
    [SerializeField] private float houseScale = 1.4f;

    [Tooltip("Height of the house floor above the ground in the model (0.178 m); scaled with the house. "
             + "Things standing inside - beds, villagers - sit on it, not on the ground under it.")]
    [SerializeField] private float modelFloorHeight = 0.178f;
    private float FloorY => modelFloorHeight * houseScale;

    [Tooltip("Share of houses that show a water jar in the yard - never more than one each.")]
    [Range(0, 100)]
    [SerializeField] private int yardJarPercent = 50;

    [Header("Anchors inside a plot (local to the house)")]
    [SerializeField] private Vector3 bedOffset = new Vector3(0f, 0f, 2.2f);

    // In the window on the back wall (SM_WindowLeft (1), centred at 1.10, 1.04, -2.87 in house
    // space); the screen's pivot is its bottom edge. It was at (1.6, 1.4, 0.1) - the middle of the
    // room - where it hung at head height as a black panel with no window behind it.
    [SerializeField] private Vector3 screenOffset = new Vector3(1.1f, 0.54f, -2.78f);
    [SerializeField] private Vector3 fanOffset = new Vector3(-1.4f, 0f, 2.0f);
    [SerializeField] private float villagerSpacing = 0.9f;

    public readonly List<HouseholdView> Households = new List<HouseholdView>();
    public readonly List<VillagerView> Villagers = new List<VillagerView>();

    private static M3NeighbourhoodBuilder current;
    /// <summary>How much larger than its model the house stands (1 before the neighbourhood exists).</summary>
    public static float HouseScale => current != null ? current.houseScale : 1f;
    private static readonly List<HouseholdView> none = new List<HouseholdView>();
    /// <summary>The plots of the neighbourhood that is standing now (empty before it is built).</summary>
    public static IReadOnlyList<HouseholdView> HouseholdViews => current != null ? current.Households : none;

    private void Awake() => current = this;

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

        var surroundings = GetComponent<M3Surroundings>();
        if (surroundings != null && Households.Count > 0)
        {
            var village = new Bounds(Households[0].transform.position, Vector3.zero);
            foreach (var h in Households) village.Encapsulate(h.transform.position);
            village.Expand(new Vector3(16f, 0f, 14f)); // the houses themselves, plus their yards
            surroundings.Build(village);
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

        if (housePrefab != null)
        {
            var house = Instantiate(housePrefab, plot.transform);
            house.transform.localScale = Vector3.one * houseScale;
            OpenTheDoors(house);
            AddTeleportFloor(plot.transform);
        }

        var bed = bedPrefab != null
            ? Instantiate(bedPrefab, plot.transform).transform
            : Anchor(plot.transform, "BedAnchor", bedOffset);
        bed.localPosition = bedOffset * houseScale + Vector3.up * FloorY;

        var screenMount = Anchor(plot.transform, "ScreenMount", screenOffset * houseScale);
        screenMount.localScale = Vector3.one * houseScale;   // the window is scaled with the house
        var fanStand = Anchor(plot.transform, "FanStand", fanOffset * houseScale + Vector3.up * FloorY);
        var netAnchor = Anchor(plot.transform, "NetAnchor", bedOffset * houseScale + Vector3.up * FloorY);

        var view = plot.AddComponent<HouseholdView>();
        view.householdId = h.Id;
        view.Bind(screenMount, fanStand, netAnchor);
        Households.Add(view);

        // The containers are scenery and the visible reason there are mosquitoes here. Section 5:
        // they are "not a button, not a task, not a score" - nothing here is interactive.
        //
        // The model gives every household two to five productive containers (and always at least
        // two, so a cleared yard is never a promise - section 5), but drawing one jar per container
        // lined every yard with identical pots. Scenery only needs to say "water stands here": one
        // jar at most, and a stable per-house roll leaves some yards bare. This is presentation
        // only - the simulation's container counts are untouched.
        if (containerPrefab != null && h.ProductiveContainerCount > 0
            && (int)(((uint)h.Id * 2654435761u >> 16) % 100u) < yardJarPercent)
        {
            // In the lane-side yard, in front of the house. At z = -1.8 they were inside the
            // 6.4 m-deep house, where nobody walking the lane could see them.
            var yard = Anchor(plot.transform, $"Container_{h.Id}", new Vector3(-2.5f * houseScale, 0f, yardDepth));
            Instantiate(containerPrefab, yard);
        }

        for (int r = 0; r < h.ResidentIds.Count; r++)
        {
            BuildVillager(sim, plot.transform, h.ResidentIds[r], r, bed);
        }
    }

    /// <summary>
    /// A thin floor-only surface inside the house that the teleport ray can land on. The house's own
    /// floor mesh includes the walls, so making that teleportable would let the ray put a Pilot on a
    /// wall or in the doorway.
    /// </summary>
    private void AddTeleportFloor(Transform plot)
    {
        var go = new GameObject("Teleport Floor");
        go.transform.SetParent(plot, false);
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(8.0f * houseScale, 0.04f, 5.8f * houseScale);
        box.center = new Vector3(0.1f * houseScale, FloorY - 0.02f, 0.2f * houseScale);
        M3XRPlayer.MakeTeleportArea(go);
    }

    /// <summary>
    /// The Lao house model has its doors closed: each is a solid slab, with a collider, filling a
    /// real opening in the wall - so a Pilot walking up to a house was stopped dead at the door
    /// (found by walking in with the thumbstick; a fly-through camera never touched them). The
    /// pivot of each leaf is its hinge edge, so swinging it 90 degrees about the vertical opens the
    /// doorway and leaves the leaf standing flat against the wall.
    /// </summary>
    private static void OpenTheDoors(GameObject house)
    {
        foreach (var t in house.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("Door"))
                t.localRotation = Quaternion.Euler(0f, -90f, 0f) * t.localRotation;
        }
    }

    private void BuildVillager(Neighbourhood sim, Transform plot, int personId, int index, Transform bed)
    {
        // A loose group in the far corner of the room, turned to face the door the volunteer comes
        // in by. The first layout fanned them across the middle of the room - straight in the path
        // between the door and the bed, so a Pilot walked into one of them - and before that they
        // stood in a row facing a wall. Model-space numbers, scaled with the house.
        int count = sim.HouseholdById(sim.PersonById(personId).HouseholdId).ResidentIds.Count;
        Vector3 door = new Vector3(3.0f, 0f, -1.27f) * houseScale;
        float rowStep = Mathf.Min(0.7f, 2.9f / Mathf.Max(count, 1));
        Vector3 local = new Vector3(-1.6f + (index % 2) * 0.7f, 0f, -2.4f + index * rowStep) * houseScale;
        local.y = FloorY;   // on the floor, not the ground beneath it
        var spot = Anchor(plot, $"Villager_{personId}", local);
        var faceDoor = door - local;
        faceDoor.y = 0f;
        spot.localRotation = Quaternion.LookRotation(faceDoor, Vector3.up);

        var person = sim.PersonById(personId);
        var body = BodyPrefabFor(person.Age, personId);
        var villager = body != null ? Instantiate(body, spot) : new GameObject("Villager");
        if (body == null) villager.transform.SetParent(spot, false);
        ShowOneBody(villager, personId);

        // The prefab is Module 1's street NPC: its Human component walks waypoints and sets
        // isWalk on Start, so every villager ran on the spot indoors. They stand at home here.
        var walker = villager.GetComponent<Human>();
        if (walker != null) walker.enabled = false;
        var animator = villager.GetComponentInChildren<Animator>();
        if (animator != null) animator.SetBool("isWalk", false);

        // A purpose-built child or elder body already has the right proportions; only the fallback
        // adult body needs scaling by age. Everyone gets a little natural variation.
        bool ownBody = body != villagerPrefab;
        float size = ownBody ? 1f : person.Age == AgeBand.Child ? 0.65f : person.Age == AgeBand.Elder ? 0.94f : 1f;
        size *= 1f + ((personId * 37) % 7 - 3) * 0.012f;
        villager.transform.localScale = Vector3.one * size;

        var view = villager.GetComponent<VillagerView>();
        if (view == null) view = villager.AddComponent<VillagerView>();
        view.personId = personId;
        view.RestingPlace = bed;
        view.WireFrom(villager);
        // Instantiate/AddComponent already ran OnEnable -> Refresh with personId still 0, so on
        // the first turn every villager showed person 0's condition: a patient with a fever stood
        // there looking well until the next turn. Refresh now that it knows who it is.
        view.Refresh();
        Villagers.Add(view);

        // Someone to talk to: point at them and pull the trigger, or reach out. Same on every
        // villager, sick or well (section 5 - nothing marks a sick person).
        if (villager.GetComponentInChildren<Collider>() == null)
        {
            var capsule = villager.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.85f, 0f);
            capsule.height = 1.7f;
            capsule.radius = 0.32f;
        }
        SlimTalkCollider(villager);
        var target = villager.GetComponent<M3VillagerTarget>() ?? villager.AddComponent<M3VillagerTarget>();
        target.personId = personId;
    }

    /// <summary>
    /// Keeps a villager's collider about body-sized. The street-NPC prefab ships a 0.5 m-radius
    /// capsule (a metre wide), the villagers stand 0.7 m apart, so their colliders overlapped and
    /// pointing at one person opened whoever was nearer along the ray - found when the second
    /// conversation of a visit kept opening the first person's panel. Only the person you point
    /// at should respond.
    /// </summary>
    private static void SlimTalkCollider(GameObject villager)
    {
        foreach (var capsule in villager.GetComponentsInChildren<CapsuleCollider>(true))
        {
            if (capsule.isTrigger) continue;
            capsule.radius = Mathf.Min(capsule.radius, 0.28f);
            capsule.height = Mathf.Max(capsule.height, capsule.radius * 2f);
        }
    }

    /// <summary>The body for this person: a purpose-built one for their age band if the scene has any.</summary>
    private GameObject BodyPrefabFor(AgeBand age, int personId)
    {
        var pool = age == AgeBand.Child ? childPrefabs : age == AgeBand.Elder ? elderPrefabs : null;
        if (pool != null && pool.Length > 0)
        {
            var pick = pool[(int)((uint)personId % (uint)pool.Length)];
            if (pick != null) return pick;
        }
        return villagerPrefab;
    }

    /// <summary>
    /// PF_CharacterV1 carries three complete bodies (fodo, nasa, Roger - each 1.6-1.7 m tall, each a
    /// different head) as siblings, all active, and nothing chooses between them, so every adult
    /// rendered all three stacked in one spot. Keep one per person, chosen from their id so it is
    /// stable across rebuilds, and switch the others off. A prefab with a single body is untouched.
    /// </summary>
    private static void ShowOneBody(GameObject villager, int personId)
    {
        var bodies = villager.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        RepairBones(bodies);
        if (bodies.Length < 2) return;
        int keep = (int)((uint)personId % (uint)bodies.Length);
        for (int i = 0; i < bodies.Length; i++) bodies[i].gameObject.SetActive(i == keep);
    }

    /// <summary>
    /// In PF_CharacterV1 only Roger is rigged: fodo and nasa have every entry of their bone list
    /// empty, and a skinned mesh with no bones draws nothing at all - upright or lying, whatever its
    /// bounds say. Two thirds of the adults were invisible (a sick one on a bed showed an empty
    /// mattress). All three meshes are skinned to the same 31-bone skeleton - their bind poses are
    /// identical - so the unrigged bodies take the rigged one's bones. Done here rather than in the
    /// prefab because Module 1's street NPCs stack all three bodies from the same prefab, and rigging
    /// them there would show three overlapping people.
    /// </summary>
    private static void RepairBones(SkinnedMeshRenderer[] bodies)
    {
        SkinnedMeshRenderer donor = null;
        foreach (var body in bodies)
            if (HasAllBones(body)) { donor = body; break; }
        if (donor == null) return;

        foreach (var body in bodies)
        {
            if (HasAllBones(body) || body.sharedMesh == null) continue;
            if (!SameSkeleton(body.sharedMesh, donor.sharedMesh)) continue;
            body.bones = donor.bones;
            if (body.rootBone == null) body.rootBone = donor.rootBone;
        }
    }

    private static bool HasAllBones(SkinnedMeshRenderer body)
    {
        var bones = body.bones;
        if (bones == null || bones.Length == 0) return false;
        foreach (var b in bones) if (b == null) return false;
        return true;
    }

    private static bool SameSkeleton(Mesh a, Mesh b)
    {
        var pa = a.bindposes;
        var pb = b.bindposes;
        if (pa.Length != pb.Length) return false;
        for (int i = 0; i < pa.Length; i++)
            for (int k = 0; k < 16; k++)
                if (Mathf.Abs(pa[i][k] - pb[i][k]) > 1e-5f) return false;
        return true;
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
