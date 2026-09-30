using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Furnishes each house and its yard, differently for every household, so the neighbourhood looks
/// lived in rather than ten copies of one room with a bed, a fan and a net in it.
///
/// Everything here is reused Team Assets furniture and plants (section 11: the neighbourhood should
/// feel like the world the student already worked in), chosen and placed from the household id and
/// the session seed, so a replayed seed rebuilds the same rooms. The pieces are picked from slots
/// that were measured against the house - clear of the side door, the path from it to the bed and to
/// the family in the far corner, the bed, the fan, the net and the window the screen goes in - and
/// then shifted by their own bounds, so a prefab's pivot never decides where it ends up.
///
/// Scenery only: no colliders (they would stop the CharacterController, catch the teleport ray or
/// the ray that opens a villager's panel), nothing interactive, and nothing tied to anybody's health.
/// Nothing that could be taken for a water container either - no jars, buckets, basins, tyres,
/// vases or sinks - since those are the simulation's own clues. Colour differences between houses are
/// per-instance tints (MaterialPropertyBlock); the shared house prefab and materials are untouched.
///
/// The prefab references live in an asset under Resources rather than on a scene object, so the
/// generated scene needs no change and a build always carries them (AEDES > Module 3 > Generate Room
/// Dresser Asset writes it).
/// </summary>
public class M3RoomDresser : ScriptableObject
{
    public const string ResourcePath = "Module3/M3RoomDresser";

    [Header("Furniture")]
    [SerializeField] private GameObject table;
    [SerializeField] private GameObject deskAndChair;
    [SerializeField] private GameObject tvDesk;
    [SerializeField] private GameObject refrigerator;
    [SerializeField] private GameObject[] carpets;
    [SerializeField] private GameObject platesAndBowls;
    [SerializeField] private GameObject stove;
    [SerializeField] private GameObject spiceRack;
    [SerializeField] private GameObject lamp;
    [SerializeField] private GameObject[] pictures;

    [Header("Yard")]
    [SerializeField] private GameObject[] plantPots;
    [SerializeField] private GameObject[] bushes;
    [SerializeField] private GameObject[] flowers;
    [SerializeField] private GameObject bananaTree;
    [Tooltip("PF_M3_Motorbike: the city pack's scooter with its rider taken off (Module3MotorbikeAssets).")]
    [SerializeField] private GameObject motorbike;

    [Header("Variety")]
    [Tooltip("Most pieces the main room gets; the side-room kitchen has its own two or three.")]
    [Range(5, 8)]
    [SerializeField] private int maxRoomPieces = 8;
    [Range(0, 100)]
    [SerializeField] private int bananaTreePercent = 40;
    [Range(0, 100)]
    [SerializeField] private int motorbikePercent = 40;
    [Tooltip("Share of the yard's plant pots that have been knocked over - still full of earth and plant.")]
    [Range(0, 100)]
    [SerializeField] private int tippedPotPercent = 15;

    private enum Piece { Table, DeskAndChair, TvDesk, Refrigerator, Carpet, PlatesAndBowls, Stove, SpiceRack, Lamp, Picture, PlantPot, Bush, Flower, BananaTree, Motorbike }

    /// <summary>Which inner wall face a piece backs onto; None stands free.</summary>
    private enum Wall { None, Front, Back, West, East, SideWest, SideFront }

    // The rooms as modelled (scale 1), measured in the running scene by raycasts against the house's
    // collider and multiplied by the house scale like every other anchor in the plot. +z is the
    // lane-side front wall, which has no door; the visitor's door is in the east (+x) wall at
    // z -1.64..-0.93 and the east window at z 0.57..2.0; the back window the screen goes in spans
    // x 0.54..1.96. The small side room at -x opens off the main room at z 0.3..1.3.
    private const float MainWestX = -1.864f, MainEastX = 3.936f, MainBackZ = -2.721f, MainFrontZ = 3.079f;
    private const float SideWestX = -3.864f, SideFrontZ = 3.079f;
    /// <summary>Metres left between a piece and the wall it backs onto.</summary>
    private const float WallGap = 0.03f;

    /// <summary>A place a piece can go. Model units for position, degrees in plot space for facing.</summary>
    private struct Spot
    {
        public Piece piece;
        /// <summary>Bounds centre; for a piece against a wall, the coordinate across the wall is ignored.</summary>
        public float x, z;
        public Wall wall;
        /// <summary>Direction the piece's front faces: 0 = +z (the lane), 90 = +x (the door), 180, 270.</summary>
        public float facing;
        /// <summary>Metres above the floor of the piece's centre, for things hung on a wall; 0 stands on the floor.</summary>
        public float hang;
        /// <summary>Something standing on this piece (plates, a lamp), in the piece's own local space.</summary>
        public Piece? onTop;
        public Vector3 onTopAt;

        public Spot(Piece piece, float x, float z, Wall wall = Wall.None, float facing = 0f, float hang = 0f)
        {
            this.piece = piece; this.x = x; this.z = z; this.wall = wall; this.facing = facing; this.hang = hang;
            onTop = null; onTopAt = Vector3.zero;
        }

        public Spot With(Piece top, Vector3 at) { onTop = top; onTopAt = at; return this; }
    }

    /// <summary>Local direction each piece's front faces, as an angle like Spot.facing.</summary>
    private static float FrontOf(Piece p)
    {
        switch (p)
        {
            case Piece.Picture: return 90f;        // a thin panel with its image on +x
            case Piece.Refrigerator: return 180f;  // door toward -z
            case Piece.Stove: return 180f;
            default: return 0f;
        }
    }

    // Tops measured in the scene: the table at 0.61 m, the desk and the TV bench at 0.71 m.
    private static readonly Vector3 OnTable = new Vector3(0.3f, 0.612f, 0.05f);
    private static readonly Vector3 OnDesk = new Vector3(-0.55f, 0.709f, -0.12f);
    private static readonly Vector3 OnTvDesk = new Vector3(0.62f, 0.709f, -0.12f);
    private static readonly Vector3 OnCarpet = new Vector3(0.1f, 0.11f, -0.05f);

    // Alternatives for each corner of the house; one is chosen per corner, or none. Measured clear of
    // the walkways: door -> family runs along z -1.9..-0.6, door -> bed crosses the middle, and the bed
    // (x -0.6..0.6, z 1.5..2.9), the fan (-1.4, 2.0) and the side-room door leaf along z 1.2 keep 0.6 m.
    private static readonly Spot[][] FrontCorner =
    {
        // A TV bench against the front wall and a mat in front of it with the meal laid out.
        new[] { new Spot(Piece.TvDesk, 2.79f, 0f, Wall.Front, 180f).With(Piece.Lamp, OnTvDesk),
                new Spot(Piece.Carpet, 2.79f, 1.85f).With(Piece.PlatesAndBowls, OnCarpet) },
        // A table and chairs.
        new[] { new Spot(Piece.Table, 2.72f, 0f, Wall.Front).With(Piece.PlatesAndBowls, OnTable) },
        // Eating on the floor: a mat against the front wall.
        new[] { new Spot(Piece.Carpet, 2.79f, 0f, Wall.Front).With(Piece.PlatesAndBowls, OnCarpet) },
        // A desk for homework.
        new[] { new Spot(Piece.DeskAndChair, 2.72f, 0f, Wall.Front, 180f).With(Piece.Lamp, OnDesk) },
    };

    private static readonly Spot[][] BackCorner =
    {
        // Between the back window and the door wall; anything deeper than 0.7 m would reach the door path.
        new[] { new Spot(Piece.Refrigerator, 3.5f, 0f, Wall.Back, 0f) },
        new[] { new Spot(Piece.DeskAndChair, 3.0f, 0f, Wall.Back, 0f).With(Piece.Lamp, OnDesk) },
        new[] { new Spot(Piece.TvDesk, 3.0f, 0f, Wall.Back, 0f) },
        new[] { new Spot(Piece.SpiceRack, 3.7f, 0f, Wall.Back, 0f), new Spot(Piece.PlantPot, 3.2f, -2.5f) },
    };

    private static readonly Spot[][] Kitchen =
    {
        // The side room: cooking on the floor against the west wall, the rack beside it.
        new[] { new Spot(Piece.Stove, 0f, 2.2f, Wall.SideWest, 90f),
                new Spot(Piece.SpiceRack, 0f, 1.35f, Wall.SideWest, 90f),
                new Spot(Piece.PlatesAndBowls, -3.2f, 2.5f) },
        // Against the front wall instead, with a fridge in the corner by the partition.
        new[] { new Spot(Piece.Stove, -3.25f, 0f, Wall.SideFront, 180f),
                new Spot(Piece.Refrigerator, -2.4f, 0f, Wall.SideFront, 180f) },
        new[] { new Spot(Piece.Stove, 0f, 2.3f, Wall.SideWest, 90f),
                new Spot(Piece.PlatesAndBowls, -3.25f, 1.6f) },
    };

    /// <summary>Single pieces that fill a room out once the corners are chosen.</summary>
    private static readonly Spot[] Extras =
    {
        new Spot(Piece.Picture, 2.3f, 0f, Wall.Front, 180f, 1.75f),
        new Spot(Piece.Picture, 3.3f, 0f, Wall.Front, 180f, 1.75f),
        new Spot(Piece.Picture, 2.6f, 0f, Wall.Back, 0f, 1.6f),
        new Spot(Piece.Picture, 0f, 2.45f, Wall.West, 90f, 1.7f),
        new Spot(Piece.Picture, 0f, 2.6f, Wall.East, 270f, 1.6f),
        new Spot(Piece.Picture, -3.0f, 0f, Wall.SideFront, 180f, 1.6f),
        new Spot(Piece.PlantPot, 3.72f, -0.15f),     // by the east wall, between the door and the window
        new Spot(Piece.PlantPot, -1.62f, 2.85f),     // the front corner behind the fan
    };

    // Per-house tints, all in the village's own range: lime-washed cream, ochre, faded sage, bare.
    private static readonly Color[] WallTints =
    {
        new Color(1f, 1f, 1f), new Color(1f, 0.95f, 0.86f), new Color(1f, 0.9f, 0.76f),
        new Color(0.93f, 0.97f, 0.87f), new Color(1f, 0.92f, 0.88f), new Color(0.95f, 0.93f, 0.9f),
    };
    private static readonly Color[] RoofTints =
    {
        new Color(1f, 1f, 1f), new Color(0.9f, 0.84f, 0.8f), new Color(1f, 0.93f, 0.84f), new Color(0.82f, 0.78f, 0.76f),
    };
    // The mats and the prints take their colours from swatches of the 16 x 16 T_GradientMap palette, so
    // moving their UVs by whole swatches (a per-instance _BaseMap_ST) recolours them, which a tint cannot:
    // as modelled the mats are slate grey and the prints purple and blue. These offsets land on the
    // palette's terracotta, cream, rose, maroon and olive (mats) and its browns and dull greens (prints).
    private static readonly Vector2[] CarpetSwatches =
    {
        new Vector2(4, 0), new Vector2(6, -8), new Vector2(7, -8), new Vector2(8, -8), new Vector2(2, 0), new Vector2(6, 0),
    };
    private static readonly Vector2[] PictureSwatches =
    {
        new Vector2(5, -8), new Vector2(8, 0), new Vector2(4, -8), new Vector2(12, -8),
    };
    // The bushes sample the palette's pure green; two columns over is a deep leaf green, fourteen an olive.
    private static readonly Vector2[] BushSwatches = { new Vector2(2, 0), new Vector2(14, 0) };
    // Mostly the scooter's own red, some weathered to maroon or near-black.
    private static readonly Color[] MotorbikeTints =
    {
        new Color(1f, 1f, 1f), new Color(1f, 1f, 1f), new Color(0.62f, 0.46f, 0.44f), new Color(0.42f, 0.4f, 0.42f),
    };
    private static readonly Color[] JarTints =
    {
        new Color(1f, 1f, 1f), new Color(1.08f, 0.98f, 0.9f), new Color(0.86f, 0.8f, 0.74f), new Color(1.12f, 1.0f, 0.86f),
    };

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    private static MaterialPropertyBlock block;
    private static M3RoomDresser loaded;
    private static bool warned;

    /// <summary>
    /// Furnishes plot <paramref name="plot"/> (house <paramref name="householdId"/>, standing with +z toward
    /// the lane) for the session <paramref name="seed"/>. Called by M3NeighbourhoodBuilder once the house,
    /// bed and yard jar stand.
    /// </summary>
    public static void Dress(Transform plot, int householdId, int seed, float houseScale, float floorY)
    {
        if (loaded == null) loaded = Resources.Load<M3RoomDresser>(ResourcePath);
        if (loaded == null)
        {
            if (!warned) Debug.LogWarning($"Module 3: no Resources/{ResourcePath}, so the houses stay unfurnished. "
                                          + "Run AEDES > Module 3 > Generate Room Dresser Asset.");
            warned = true;
            return;
        }
        loaded.DressPlot(plot, householdId, seed, houseScale, floorY);
    }

    private System.Random rng;
    private Transform plot;
    private float scale, floorY;

    private void DressPlot(Transform plotRoot, int householdId, int seed, float houseScale, float floor)
    {
        rng = new System.Random(unchecked(seed * 486187739 + householdId * 16777619) ^ 0x5bd1e995);
        plot = plotRoot;
        scale = houseScale;
        floorY = floor;

        var root = new GameObject("Dressing").transform;
        root.SetParent(plot, false);
        var indoors = new GameObject("Indoors").transform;
        indoors.SetParent(root, false);
        var yard = new GameObject("Yard").transform;
        yard.SetParent(root, false);

        TintHouse();
        FurnishRooms(indoors);
        FillYard(yard);
        VaryJar(householdId);

        // Everything indoors, and the small things in the yard, are left undrawn from two houses away.
        M3DistanceCulling.Mark(indoors.gameObject);
    }

    // ---------- indoors ----------

    private void FurnishRooms(Transform parent)
    {
        var used = new HashSet<Piece>();

        // The side-room kitchen is a room of its own, furnished apart from the main room's budget.
        if (rng.Next(100) < 90) PlaceOneOf(parent, Kitchen, int.MaxValue, used);

        // The main room: the front corner always (it is what the room is for), the back corner usually,
        // then pictures and plants until the room has its share.
        int budget = rng.Next(5, maxRoomPieces + 1);
        int count = PlaceOneOf(parent, FrontCorner, budget, used);
        if (rng.Next(100) < 85) count += PlaceOneOf(parent, BackCorner, budget - count, used);

        var extras = new List<Spot>(Extras);
        Shuffle(extras);
        int pictures = 0;
        foreach (var spot in extras)
        {
            if (count >= budget) break;
            if (spot.piece == Piece.Picture && ++pictures > 2) continue;
            Place(parent, spot, used);
            count++;
        }
    }

    /// <summary>Places one of <paramref name="options"/> that fits in <paramref name="room"/> pieces, whole; returns how many it placed.</summary>
    private int PlaceOneOf(Transform parent, Spot[][] options, int room, HashSet<Piece> used)
    {
        var order = new List<Spot[]>(options);
        Shuffle(order);
        foreach (var group in order)
        {
            int size = PiecesIn(group);
            if (size > room || Repeats(group, used)) continue;
            foreach (var spot in group) Place(parent, spot, used);
            return size;
        }
        return 0;
    }

    private static int PiecesIn(Spot[] group)
    {
        int n = 0;
        foreach (var s in group) n += s.onTop.HasValue ? 2 : 1;
        return n;
    }

    /// <summary>One fridge, one TV and one table to a house; small things may repeat.</summary>
    private static bool Repeats(Spot[] group, HashSet<Piece> used)
    {
        foreach (var s in group)
        {
            if (s.piece == Piece.PlantPot || s.piece == Piece.Picture || s.piece == Piece.PlatesAndBowls) continue;
            if (used.Contains(s.piece)) return true;
        }
        return false;
    }

    private void Place(Transform parent, Spot spot, HashSet<Piece> used)
    {
        var go = Spawn(spot.piece, parent);
        if (go == null) return;
        used.Add(spot.piece);

        bool hung = spot.hang > 0f;
        bool small = spot.piece == Piece.PlantPot || spot.piece == Piece.Picture;
        float jitterYaw = spot.piece == Piece.Carpet ? Between(-8f, 8f) : spot.wall != Wall.None ? Between(-2f, 2f) : Between(-15f, 15f);
        if (hung) jitterYaw = 0f;
        go.transform.localRotation = Quaternion.Euler(0f, spot.facing - FrontOf(spot.piece) + jitterYaw, 0f);
        go.transform.localScale *= small ? Between(0.88f, 1.12f) : Between(0.96f, 1.04f);

        if (spot.onTop.HasValue)
        {
            var top = Spawn(spot.onTop.Value, go.transform);
            if (top != null)
            {
                top.transform.localPosition = spot.onTopAt;
                top.transform.localRotation = Quaternion.Euler(0f, Between(-25f, 25f), 0f);
                used.Add(spot.onTop.Value);
            }
        }

        // Along the wall a little either way, so the same slot is not the same spot in every house.
        float slide = spot.wall == Wall.None ? 0f : Between(-0.08f, 0.08f);
        Fit(go, spot, slide, floorY);
    }

    /// <summary>
    /// Moves <paramref name="go"/> so its bounds sit where <paramref name="spot"/> says: centred on the spot,
    /// its back against the wall it names, and resting on the floor (or hung at its height).
    /// </summary>
    private void Fit(GameObject go, Spot spot, float slide, float floor)
    {
        var b = PlotBounds(go);
        var shift = new Vector3(spot.x * scale + slide - b.center.x, 0f, spot.z * scale + slide - b.center.z);
        switch (spot.wall)
        {
            case Wall.Front: shift.z = MainFrontZ * scale - WallGap - b.max.z; break;
            case Wall.SideFront: shift.z = SideFrontZ * scale - WallGap - b.max.z; break;
            case Wall.Back: shift.z = MainBackZ * scale + WallGap - b.min.z; break;
            case Wall.West: shift.x = MainWestX * scale + WallGap - b.min.x; break;
            case Wall.SideWest: shift.x = SideWestX * scale + WallGap - b.min.x; break;
            case Wall.East: shift.x = MainEastX * scale - WallGap - b.max.x; break;
        }
        shift.y = spot.hang > 0f ? floor + spot.hang - b.center.y : floor + 0.002f - b.min.y;
        go.transform.localPosition += shift;
    }

    /// <summary>The renderers' combined bounds in plot space. Plots only turn by 0 or 180 degrees, so this stays axis-aligned.</summary>
    private Bounds PlotBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        var b = new Bounds(plot.InverseTransformPoint(go.transform.position), Vector3.zero);
        bool first = true;
        foreach (var r in renderers)
        {
            var w = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = w.center + Vector3.Scale(w.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var local = plot.InverseTransformPoint(corner);
                if (first) { b = new Bounds(local, Vector3.zero); first = false; }
                else b.Encapsulate(local);
            }
        }
        return b;
    }

    // ---------- the yard ----------

    // Model units, lane side of the house (its front wall is at z 3.4). The worn path down the gap to the
    // side door runs at x ~5.1 and stays clear, as does the jar's spot at (-2.5, 4.43).
    private static readonly float[] PotClusterX = { -3.6f, -0.9f, 1.4f, 3.5f };
    private const float PotRowZ = 3.62f;

    private void FillYard(Transform parent)
    {
        // Pots along the front wall, in one or two little clusters.
        int clusters = rng.Next(1, 3);
        var xs = new List<float>(PotClusterX);
        Shuffle(xs);
        for (int c = 0; c < clusters; c++)
        {
            int pots = rng.Next(1, 4);
            for (int i = 0; i < pots; i++)
            {
                var at = new Vector3(xs[c] * scale + i * 0.48f + Between(-0.06f, 0.06f), 0f, PotRowZ * scale + Between(-0.05f, 0.12f));
                YardPiece(parent, Piece.PlantPot, at, Between(0.85f, 1.25f), rng.Next(100) < tippedPotPercent);
            }
        }

        // Flowers, some years more than others.
        int flowerCount = rng.Next(0, 4);
        float fx = Between(0.3f, 2.8f) * scale, fz = Between(3.9f, 4.6f) * scale;
        for (int i = 0; i < flowerCount; i++)
            YardPiece(parent, Piece.Flower, new Vector3(fx + Between(-0.6f, 0.6f), 0f, fz + Between(-0.4f, 0.4f)), Between(0.8f, 1.2f), false);

        // A bush at one front corner, or none.
        int bushAt = rng.Next(3);
        if (bushAt < 2)
            YardPiece(parent, Piece.Bush, new Vector3((bushAt == 0 ? -3.85f : 3.75f) * scale, 0f, Between(4.3f, 4.6f) * scale), Between(0.7f, 1.05f), false);

        // A motorbike parked in the front yard, between the jar's spot and the flowers, clear of the lane
        // and of the path down the gap to the door.
        if (rng.Next(100) < motorbikePercent) ParkMotorbike(parent);

        // Behind the house: a banana plant at some plots, a bush at others.
        if (rng.Next(100) < bananaTreePercent)
            YardPiece(parent, Piece.BananaTree, new Vector3(Between(-2.6f, 2.6f) * scale, 0f, -4.7f * scale), Between(0.8f, 1.15f), false);
        else if (rng.Next(2) == 0)
            YardPiece(parent, Piece.Bush, new Vector3(Between(-2.6f, 2.6f) * scale, 0f, -3.6f * scale), Between(0.8f, 1.1f), false);
    }

    private void YardPiece(Transform parent, Piece piece, Vector3 at, float size, bool tipped)
    {
        var go = Spawn(piece, parent);
        if (go == null) return;
        float yaw = Between(0f, 360f);
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * (tipped ? Quaternion.Euler(0f, 0f, 88f) : Quaternion.identity);
        go.transform.localScale *= size;
        var b = PlotBounds(go);
        go.transform.localPosition += new Vector3(at.x - b.center.x, 0.002f - b.min.y, at.z - b.center.z);
        // Outdoors the pots and flowers are small enough to leave undrawn at a distance; the plants
        // that stand as tall as a person stay on, or they would pop in along the lane.
        if (piece == Piece.PlantPot || piece == Piece.Flower) M3DistanceCulling.Mark(go);
    }

    private void ParkMotorbike(Transform parent)
    {
        var go = Spawn(Piece.Motorbike, parent);
        if (go == null) return;
        // Side on to the lane, pointing either way, leaning a few degrees onto its stand.
        float yaw = (rng.Next(2) == 0 ? 90f : 270f) + Between(-10f, 10f);
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, Between(4f, 7f));
        var at = new Vector3(Between(-0.85f, -0.6f) * scale, 0f, Between(4.5f, 4.65f) * scale);
        var b = PlotBounds(go);
        go.transform.localPosition += new Vector3(at.x - b.center.x, 0.002f - b.min.y, at.z - b.center.z);
        var paint = MotorbikeTints[rng.Next(MotorbikeTints.Length)];
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) Tint(r, paint);
    }

    /// <summary>The yard jar the builder stood here, if any: a different size, turn and clay from its neighbours'.</summary>
    private void VaryJar(int householdId)
    {
        var anchor = plot.Find($"Container_{householdId}");
        if (anchor == null || anchor.childCount == 0) return;
        var jar = anchor.GetChild(0);
        jar.localRotation = Quaternion.Euler(0f, Between(0f, 360f), 0f);
        jar.localScale *= Between(0.85f, 1.15f);
        var clay = JarTints[rng.Next(JarTints.Length)];
        foreach (var r in jar.GetComponentsInChildren<Renderer>())
        {
            // Only the clay: the water surface keeps its own colour, so standing water still reads the same everywhere.
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && !mats[i].name.Contains("Pond")) Tint(r, clay, i);
        }
    }

    // ---------- the house itself ----------

    private void TintHouse()
    {
        Transform house = null;
        foreach (Transform child in plot)
            if (child.Find("Roof") != null) { house = child; break; }
        if (house == null) return;

        var walls = WallTints[rng.Next(WallTints.Length)];
        var roof = RoofTints[rng.Next(RoofTints.Length)];
        foreach (var r in house.GetComponentsInChildren<Renderer>(true))
        {
            // The walls and floor are one mesh. The tint survives M3HouseSurface swapping the materials,
            // since a property block belongs to the renderer, not the material.
            if (r.name == "Main_Floor") Tint(r, walls);
            else if (r.name == "Roof") Tint(r, roof);
        }
    }

    // ---------- helpers ----------

    private GameObject Spawn(Piece piece, Transform parent)
    {
        var prefab = PrefabFor(piece);
        if (prefab == null) return null;
        var go = Instantiate(prefab, parent);
        go.name = prefab.name;
        // Props must not block the player, the teleport ray or the ray that opens a villager's panel.
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
        {
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }

        Vector2? swatch = piece == Piece.Carpet ? CarpetSwatches[rng.Next(CarpetSwatches.Length)]
                        : piece == Piece.Picture ? PictureSwatches[rng.Next(PictureSwatches.Length)]
                        : (Vector2?)null;
        if (swatch.HasValue)
            foreach (var r in go.GetComponentsInChildren<Renderer>()) Tint(r, Color.white, -1, swatch.Value / 16f);
        else if (piece == Piece.Bush)
        {
            var leaf = BushSwatches[rng.Next(BushSwatches.Length)] / 16f;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) Tint(r, Color.white * Between(0.85f, 1f), -1, leaf);
        }
        return go;
    }

    private GameObject PrefabFor(Piece piece)
    {
        switch (piece)
        {
            case Piece.Table: return table;
            case Piece.DeskAndChair: return deskAndChair;
            case Piece.TvDesk: return tvDesk;
            case Piece.Refrigerator: return refrigerator;
            case Piece.Carpet: return Pick(carpets);
            case Piece.PlatesAndBowls: return platesAndBowls;
            case Piece.Stove: return stove;
            case Piece.SpiceRack: return spiceRack;
            case Piece.Lamp: return lamp;
            case Piece.Picture: return Pick(pictures);
            case Piece.PlantPot: return Pick(plantPots);
            case Piece.Bush: return Pick(bushes);
            case Piece.Flower: return Pick(flowers);
            case Piece.BananaTree: return bananaTree;
            case Piece.Motorbike: return motorbike;
            default: return null;
        }
    }

    private GameObject Pick(GameObject[] pool) => pool != null && pool.Length > 0 ? pool[rng.Next(pool.Length)] : null;

    /// <summary>Colours one renderer (or one of its materials) without touching the shared material.</summary>
    private static void Tint(Renderer r, Color colour, int materialIndex = -1, Vector2? swatchOffset = null)
    {
        if (block == null) block = new MaterialPropertyBlock();
        block.Clear();
        block.SetColor(BaseColorId, colour);
        if (swatchOffset.HasValue) block.SetVector(BaseMapStId, new Vector4(1f, 1f, swatchOffset.Value.x, swatchOffset.Value.y));
        if (materialIndex < 0) r.SetPropertyBlock(block);
        else r.SetPropertyBlock(block, materialIndex);
    }

    private float Between(float min, float max) => min + (float)rng.NextDouble() * (max - min);

    private void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
