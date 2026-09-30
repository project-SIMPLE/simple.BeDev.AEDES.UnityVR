using System.Collections.Generic;
using Aedes.Module3.Sim;
using UnityEngine;

/// <summary>
/// Dresses the Module 3 village so it reads as a place people live, not a row of models on a lawn.
///
/// Before this the lane held ten houses, a jar or a scooter in some yards and a few flowers, and
/// from the headset it looked like a housing estate on the day it opened: no boundaries between the
/// plots, nothing overhead, nothing between the houses, no washing, no animals. What a Lao village
/// lane has and this one lacked, in the order it shows from the lane:
///
///  - a boundary to each front yard: a post-and-rail fence, a bamboo picket, a hedge of bushes, or
///    nothing (they differ house to house, and none of them is complete - gaps where a jar or a
///    scooter stands, and always open at the path to the side door);
///  - concrete power poles down one side of the lane, with two wires slung between them;
///  - a lean-to of thatch on bamboo posts over part of some front walls, and a low bamboo platform
///    to sit on under it;
///  - washing on a line in front of the wall, swinging;
///  - a woodpile, a spirit house on a post at a front corner, sandals and a broom at the side door;
///  - trees inside the village: a broad tree behind some houses and in the wider gaps between them,
///    a palm at a yard corner, and a grove at the far end of the lane so it does not run out into grass;
///  - and chickens scratching about the yards and the lane (M3YardLife).
///
/// Built at runtime from the session seed like everything else here (M3Surroundings calls it), so
/// the scene file is untouched and the same seed always dresses the same village. Nothing has a
/// collider, nothing is in the simulation, and nothing could pass for a water container (design
/// section 5: the containers are the simulation's own clues, so no pots, buckets or basins).
///
/// Cost: every built prop of a yard goes into ONE mesh (two, with the small things by the door that
/// are not drawn from two houses away), all on one material (M3PropMesh), so a dressed yard is two
/// draw calls, plus a laundry line and a few prefab bushes or trees where it has them. The poles are
/// one mesh and the wires two line renderers for the whole lane. The hens are two small meshes each.
/// </summary>
public static class M3VillageDressing
{
    // The Lao house in plot space at scale 1 (VillageLayout measures the same model). +z faces the lane;
    // the side door is in the +x wall, opening onto the worn path down the gap to the next house.
    private const float ModelMinX = VillageLayout.ModelMinX, ModelMaxX = VillageLayout.ModelMaxX;
    private const float ModelBackZ = VillageLayout.ModelBackZ, ModelFrontZ = VillageLayout.ModelFrontZ;
    /// <summary>Door.003's centre along the +x wall (M3RoomDresser measured the opening at z -1.64..-0.93).</summary>
    private const float ModelDoorZ = -1.28f;
    /// <summary>Metres out from the +x wall the worn path to the side door runs (M3Surroundings.PathOffset).</summary>
    private const float PathOffset = 1f;

    /// <summary>
    /// Metres from the lane's centre line to the fence, and to a pole. The walkers keep to the verges
    /// 1.2-1.7 m out (M3OutdoorLife), the track is 3.2 m wide; the fence stands beyond them.
    /// </summary>
    private const float FenceFromLane = 2.35f, PoleFromLane = 2.0f;
    /// <summary>Nothing built stands nearer the lane's centre line than this.</summary>
    private const float LaneKeepOut = 2.0f;

    /// <summary>What was built, for the log and the smoke test.</summary>
    public struct Report
    {
        public int plots, renderers, triangles, trees, hens, lines, awnings, dogs, bicycles, woodpiles, shrines, benches;
        public override string ToString()
            => $"{plots} yards dressed: {renderers} renderers, {triangles} built triangles, {trees} trees, {hens} hens, "
             + $"{lines} washing lines, {awnings} lean-tos, {benches} benches, {woodpiles} woodpiles, {shrines} spirit houses, "
             + $"{bicycles} bicycles, {dogs} dogs";
    }

    /// <summary>
    /// Dresses every plot and the lane. <paramref name="village"/> covers the houses and their yards;
    /// <paramref name="nearTrees"/> / <paramref name="farTrees"/> are M3Surroundings' prefab pools, which
    /// this reads by name (PF_Tree, PF_CoconutTree, PF_Bush_*) so the scene needs no new reference.
    /// </summary>
    public static Report Build(Transform surroundings, Bounds village, IReadOnlyList<HouseholdView> plots, int seed,
                               GameObject[] nearTrees, GameObject[] farTrees)
    {
        var report = new Report();
        if (plots == null || plots.Count == 0) return report;

        var root = new GameObject("Village Dressing").transform;
        root.SetParent(surroundings, false);
        var life = root.gameObject.AddComponent<M3YardLife>();
        life.Init(seed);

        var pools = new Pools
        {
            broadTree = Named(farTrees, "PF_Tree"),
            palm = Named(nearTrees, "PF_CoconutTree") ?? Named(farTrees, "PF_CoconutTree"),
            bushes = AllNamed(farTrees, "PF_Bush"),
        };
        var rng = new System.Random(seed * 7907 + 3);
        float scale = M3NeighbourhoodBuilder.HouseScale;

        var yards = new List<Yard>(plots.Count);
        foreach (var plot in plots) yards.Add(new Yard(plot.transform, plot.householdId, seed, scale));
        FindNeighbours(yards);

        // Village-level first: the poles stand in the fence line, so the yards must know where they are.
        Poles(root, village, yards, rng);

        foreach (var yard in yards)
        {
            DressYard(yard, pools, life, ref report);
            report.plots++;
        }

        report.trees += Grove(root, village, yards, pools, rng);
        report.hens = life.HenCount;
        report.lines = life.LineCount;
        // Everything drawn, whatever made it: the built meshes, the hens, the prefab trees and bushes.
        report.renderers = root.GetComponentsInChildren<Renderer>(true).Length;
        foreach (var y in yards) report.renderers += y.root.GetComponentsInChildren<Renderer>(true).Length;
        return report;
    }

    // ---------- one yard ----------

    private sealed class Pools
    {
        public GameObject broadTree, palm;
        public GameObject[] bushes;
    }

    /// <summary>A plot being dressed: its transform, its dice, and what already stands in it.</summary>
    private sealed class Yard
    {
        public readonly Transform plot;
        public readonly int id;
        public readonly System.Random rng;
        public readonly float S;
        public readonly Transform root;
        /// <summary>Everything in the yard, as x/z rectangles in plot space, so nothing is built on top of anything.</summary>
        public readonly List<Rect> taken = new List<Rect>();
        public readonly M3PropMesh far = new M3PropMesh(), near = new M3PropMesh();
        /// <summary>The nearest plot in the same row on this plot's -x side, and how wide the gap between their walls is.</summary>
        public Yard leftNeighbour;
        public float leftGap = float.MaxValue;
        public float eave;
        public bool awning;

        public Yard(Transform plot, int id, int seed, float scale)
        {
            this.plot = plot;
            this.id = id;
            S = scale;
            rng = new System.Random(unchecked(seed * 31 + id * 7919 + 101));

            var dressing = plot.Find("Dressing") ?? new GameObject("Dressing").transform;
            if (dressing.parent != plot) dressing.SetParent(plot, false);
            root = new GameObject("Outside").transform;
            root.SetParent(dressing, false);

            // The house, and the strip of the path down the gap to its door, are never built on. The house's
            // rectangle is its walls exactly: benches, woodpiles and bicycles stand against them.
            taken.Add(Rect.MinMaxRect(ModelMinX * S, ModelBackZ * S, ModelMaxX * S, ModelFrontZ * S));
            taken.Add(Rect.MinMaxRect(ModelMaxX * S + 0.45f, ModelBackZ * S, ModelMaxX * S + PathOffset + 0.8f, ModelFrontZ * S + 5.5f));
            // What the room dresser and the builder put in the yard: the jar, the scooter, bushes, flowers.
            var yard = dressing.Find("Yard");
            if (yard != null)
            {
                foreach (var r in yard.GetComponentsInChildren<Renderer>())
                {
                    var rect = PlotRect(r.bounds);
                    // A flower's mesh bounds are 1.4 m across for a plant that shows as 0.4 m, and a banana
                    // plant's are its leaves, 4 m up in the air: what matters on the ground is a patch round
                    // the stem, or every spot in the yard is "taken" and nothing else can stand there.
                    if (r.name.Contains("Flower")) rect = Around(rect.center, 0.45f);
                    else if (r.name.Contains("Banana") || r.name.Contains("Tree")) rect = Around(rect.center, 1.2f);
                    taken.Add(rect);
                }
            }
            var jar = plot.Find($"Container_{id}");
            if (jar != null) foreach (var r in jar.GetComponentsInChildren<Renderer>()) taken.Add(Grow(PlotRect(r.bounds), 0.15f));
            eave = Eave();
        }

        /// <summary>The roof's underside in plot space, read from the model so a re-scaled house keeps its lean-to under the eave.</summary>
        private float Eave()
        {
            float top = 2.4f * S;
            foreach (Transform child in plot)
            {
                var roof = child.Find("Roof");
                if (roof == null) continue;
                var r = roof.GetComponent<Renderer>();
                if (r != null) top = r.bounds.min.y - plot.position.y;
            }
            return top;
        }

        public Rect PlotRect(Bounds world)
        {
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? world.min.x : world.max.x, (i & 2) == 0 ? world.min.y : world.max.y, (i & 4) == 0 ? world.min.z : world.max.z);
                var local = plot.InverseTransformPoint(corner);
                minX = Mathf.Min(minX, local.x); maxX = Mathf.Max(maxX, local.x);
                minZ = Mathf.Min(minZ, local.z); maxZ = Mathf.Max(maxZ, local.z);
            }
            return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        private static Rect Grow(Rect r, float by) => Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);
        private static Rect Around(Vector2 centre, float size) => new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size);

        public bool Free(float x, float z, float width, float depth)
        {
            var r = new Rect(x - width * 0.5f, z - depth * 0.5f, width, depth);
            foreach (var t in taken) if (t.Overlaps(r)) return false;
            return true;
        }

        public void Take(float x, float z, float width, float depth) => taken.Add(new Rect(x - width * 0.5f, z - depth * 0.5f, width, depth));

        /// <summary>True if plot point (x, z) is at least <paramref name="margin"/> metres from the lane's centre line.</summary>
        public bool LaneClear(float x, float z, float margin)
        {
            var w = plot.TransformPoint(new Vector3(x, 0f, z));
            return VillageLayout.DistanceToLane(w.x, w.z) >= margin;
        }

        /// <summary>Metres from the plot origin to the lane's centre line.</summary>
        public float LaneDistance => VillageLayout.DistanceToLane(plot.position.x, plot.position.z);

        public float Between(float min, float max) => min + (float)rng.NextDouble() * (max - min);
        public bool Chance(int percent) => rng.Next(100) < percent;
        public T Pick<T>(T[] pool) => pool[rng.Next(pool.Length)];
    }

    /// <summary>Which plot stands to the -x side of each, along its row, and how wide the gap between them is.</summary>
    private static void FindNeighbours(List<Yard> yards)
    {
        float S = yards[0].S;
        foreach (var a in yards)
        {
            bool aNear = a.plot.forward.z > 0f;   // faces +z: the low-z row
            var pa = Placement(a.plot);
            float best = float.MinValue;
            foreach (var b in yards)
            {
                if (b == a || (b.plot.forward.z > 0f) != aNear) continue;
                float along = Vector3.Dot(b.plot.position - a.plot.position, a.plot.right);
                if (along >= 0f || along < best) continue;
                best = along;
                a.leftNeighbour = b;
            }
            if (a.leftNeighbour != null) a.leftGap = VillageLayout.FootprintGap(pa, Placement(a.leftNeighbour.plot), S);
        }
    }

    private static PlotPlacement Placement(Transform plot)
        => new PlotPlacement { X = plot.position.x, Z = plot.position.z, Yaw = plot.eulerAngles.y };

    private static void DressYard(Yard y, Pools pools, M3YardLife life, ref Report report)
    {
        // The boundary first: it takes the line the other pieces keep behind.
        float fenceZ = Mathf.Clamp(y.LaneDistance - FenceFromLane, 6.6f, 7.9f);
        int boundary = y.rng.Next(100);
        if (boundary < 42) PostAndRail(y, fenceZ);
        else if (boundary < 76) BambooPicket(y, fenceZ);
        else if (boundary < 84) Hedge(y, fenceZ, pools);   // its bushes are prefab instances, counted at the end

        WallFittings(y);

        if (y.Chance(45)) report.awnings += Awning(y);
        if (y.Chance(30)) report.benches += Bench(y);
        if (y.Chance(50)) report.lines += Laundry(y, life);
        if (y.Chance(38)) report.woodpiles += Woodpile(y);
        if (y.Chance(35)) report.shrines += SpiritHouse(y);
        if (y.Chance(32)) report.bicycles += Bicycle(y);
        if (y.Chance(65)) Sandals(y);
        if (y.Chance(40)) Broom(y);
        report.trees += Trees(y, pools);
        if (y.Chance(38)) report.hens += Hens(y, life);
        if (y.Chance(30)) report.dogs += Dog(y, life);

        if (!y.far.IsEmpty)
        {
            y.far.Spawn("Yard props", y.root);
            report.renderers++;
            report.triangles += y.far.TriangleCount;
        }
        if (!y.near.IsEmpty)
        {
            var near = y.near.Spawn("Yard details", y.root);
            M3DistanceCulling.Mark(near);   // sandals and a broom are not missed from two houses away
            report.renderers++;
            report.triangles += y.near.TriangleCount;
        }
    }

    // ---------- boundaries ----------

    /// <summary>Where the fence runs along the front of the yard, in plot x: from the far corner to short of the door path.</summary>
    private const float FenceX0 = -6.7f, FenceX1 = 5.4f;

    /// <summary>The posts of a fence along z = <paramref name="fz"/>, spaced <paramref name="min"/>-<paramref name="max"/> apart; false where one cannot stand.</summary>
    private static List<(float x, bool stands)> FencePosts(Yard y, float fz, float min, float max)
    {
        var posts = new List<(float, bool)>();
        float x = FenceX0 + y.Between(0f, 0.6f);
        while (x < FenceX1)
        {
            bool stands = y.LaneClear(x, fz, LaneKeepOut) && y.Free(x, fz, 0.25f, 0.25f);
            posts.Add((x, stands));
            x += y.Between(min, max);
        }
        return posts;
    }

    private static bool SpanFree(Yard y, float x0, float x1, float fz)
        => y.Free((x0 + x1) * 0.5f, fz, x1 - x0 - 0.3f, 0.35f) && y.LaneClear((x0 + x1) * 0.5f, fz, LaneKeepOut);

    private static void PostAndRail(Yard y, float fz)
    {
        var posts = FencePosts(y, fz, 1.7f, 2.3f);
        var heights = new float[posts.Count];
        for (int i = 0; i < posts.Count; i++)
        {
            if (!posts[i].stands) continue;
            heights[i] = y.Between(0.88f, 1.02f);
            var lean = Quaternion.Euler(y.Between(-3f, 3f), 0f, y.Between(-3f, 3f));
            y.far.Box(new Vector3(posts[i].x, heights[i] * 0.5f, fz), new Vector3(0.09f, heights[i], 0.09f), lean, y.Chance(50) ? M3PropMesh.Swatch.Wood : M3PropMesh.Swatch.WoodDark);
            y.Take(posts[i].x, fz, 0.2f, 0.2f);
        }
        for (int i = 0; i + 1 < posts.Count; i++)
        {
            if (!posts[i].stands || !posts[i + 1].stands || !SpanFree(y, posts[i].x, posts[i + 1].x, fz)) continue;
            float x0 = posts[i].x, x1 = posts[i + 1].x;
            for (int rail = 0; rail < 2; rail++)
            {
                if (y.Chance(12)) continue;   // a rail gone: nobody mends a fence in a hurry
                float h = rail == 0 ? 0.40f : 0.78f;
                float ya = h * heights[i] / 0.95f, yb = h * heights[i + 1] / 0.95f;
                Rail(y.far, new Vector3(x0, ya, fz), new Vector3(x1, yb, fz), 0.1f, 0.045f, rail == 0 ? M3PropMesh.Swatch.WoodDark : M3PropMesh.Swatch.Wood);
            }
            y.Take((x0 + x1) * 0.5f, fz, x1 - x0, 0.2f);
        }
    }

    /// <summary>A rail: a box from <paramref name="a"/> to <paramref name="b"/>, <paramref name="height"/> tall and <paramref name="thick"/> deep.</summary>
    private static void Rail(M3PropMesh mesh, Vector3 a, Vector3 b, float height, float thick, int swatch)
    {
        var d = b - a;
        float len = d.magnitude;
        if (len < 0.05f) return;
        var rot = Quaternion.FromToRotation(Vector3.right, d / len);
        mesh.Box((a + b) * 0.5f, new Vector3(len, height, thick), rot, swatch);
    }

    private static void BambooPicket(Yard y, float fz)
    {
        var posts = FencePosts(y, fz, 1.8f, 2.1f);
        const float PostH = 1.15f;
        for (int i = 0; i < posts.Count; i++)
        {
            if (!posts[i].stands) continue;
            var top = new Vector3(posts[i].x + y.Between(-0.02f, 0.02f), PostH, fz);
            y.far.Cylinder(new Vector3(posts[i].x, -0.02f, fz), top, 0.045f, 0.04f, 6, M3PropMesh.Swatch.BambooDark);
            y.Take(posts[i].x, fz, 0.2f, 0.2f);
        }
        for (int i = 0; i + 1 < posts.Count; i++)
        {
            if (!posts[i].stands || !posts[i + 1].stands || !SpanFree(y, posts[i].x, posts[i + 1].x, fz)) continue;
            float x0 = posts[i].x, x1 = posts[i + 1].x;
            // Two rails and a row of split-bamboo slats tied to them.
            y.far.Cylinder(new Vector3(x0, 0.98f, fz + 0.03f), new Vector3(x1, 0.98f + y.Between(-0.03f, 0.03f), fz + 0.03f), 0.028f, 0.028f, 5, M3PropMesh.Swatch.Bamboo, false);
            y.far.Cylinder(new Vector3(x0, 0.32f, fz + 0.03f), new Vector3(x1, 0.32f + y.Between(-0.03f, 0.03f), fz + 0.03f), 0.028f, 0.028f, 5, M3PropMesh.Swatch.Bamboo, false);
            bool broken = y.Chance(20);
            for (float sx = x0 + 0.12f; sx < x1 - 0.06f; sx += 0.11f)
            {
                if (broken && sx > x0 + 0.5f && sx < x0 + 0.95f) continue;   // a few slats missing
                float h = y.Between(0.95f, 1.12f);
                var lean = Quaternion.Euler(0f, 0f, y.Between(-2.5f, 2.5f));
                var bottom = new Vector3(sx + y.Between(-0.01f, 0.01f), 0.02f, fz + 0.062f);
                var across = lean * Vector3.right;
                y.far.Slat(bottom, across, 0.048f, h, 0.012f, y.Chance(70) ? M3PropMesh.Swatch.Bamboo : M3PropMesh.Swatch.BambooDark);
            }
            y.Take((x0 + x1) * 0.5f, fz, x1 - x0, 0.2f);
        }
    }

    /// <summary>The bush's palette, two swatches over: a deep leaf green (the olive further along read as boulders in a row).</summary>
    private static readonly Vector2 HedgeSwatch = new Vector2(2, 0);

    /// <summary>A hedge of the shared bushes along the fence line; returns how many renderers it added.</summary>
    private static int Hedge(Yard y, float fz, Pools pools)
    {
        if (pools.bushes == null || pools.bushes.Length == 0) return 0;
        int count = 0;
        var leaf = HedgeSwatch / 16f;
        // The shared bush is 1.5 m across and 0.6 m tall at scale 1: stretched upward and set close, so the
        // row grows together into a hedge rather than standing as a line of domes.
        for (float x = FenceX0 + y.Between(0.2f, 0.9f); x < FenceX1 - 0.3f; x += y.Between(1.1f, 1.5f))
        {
            if (!y.LaneClear(x, fz, LaneKeepOut) || !y.Free(x, fz, 1.0f, 1.0f)) continue;
            var bush = Instantiate(y.Pick(pools.bushes), y.root, new Vector3(x, 0f, fz + y.Between(-0.15f, 0.15f)), y.Between(0f, 360f), y.Between(0.7f, 0.95f));
            var stretch = bush.transform.localScale;
            stretch.y *= y.Between(1.25f, 1.5f);
            bush.transform.localScale = stretch;
            foreach (var r in bush.GetComponentsInChildren<Renderer>()) Tint(r, Color.white * y.Between(0.92f, 1.05f), leaf);
            y.Take(x, fz, 0.9f, 1.0f);
            count++;
        }
        return count;
    }

    // ---------- at the front wall ----------

    /// <summary>
    /// What every house has at its front: a drainpipe down one or both front corners, sometimes a rolled
    /// bamboo blind under the eave. Cheap, and they break the blank wall between the ground and the roof.
    /// </summary>
    private static void WallFittings(Yard y)
    {
        float wallZ = ModelFrontZ * y.S + 0.07f;
        var m = y.far;
        foreach (float corner in new[] { ModelMinX * y.S + 0.25f, ModelMaxX * y.S - 0.25f })
        {
            if (!y.Chance(70)) continue;
            float top = y.eave - 0.02f;
            var head = new Vector3(corner, top, wallZ + 0.02f);
            m.Cylinder(new Vector3(corner, 0.08f, wallZ), new Vector3(corner, top - 0.25f, wallZ), 0.045f, 0.045f, 6, M3PropMesh.Swatch.Tin, false);
            m.Cylinder(new Vector3(corner, top - 0.25f, wallZ), head + Vector3.forward * 0.3f, 0.045f, 0.045f, 6, M3PropMesh.Swatch.Tin, false);
            m.Box(new Vector3(corner, 0.06f, wallZ + 0.12f), new Vector3(0.11f, 0.06f, 0.25f), M3PropMesh.Swatch.Tin);   // the spout
            m.Box(new Vector3(corner, top * 0.5f, wallZ - 0.03f), new Vector3(0.12f, 0.03f, 0.06f), M3PropMesh.Swatch.Dark);   // a bracket
        }
        if (y.Chance(35))
        {
            // A blind rolled up under the eave, over part of the wall, its cords hanging.
            float x0 = y.Between(-4.5f, 0f), x1 = x0 + y.Between(2.4f, 3.6f);
            float yy = y.eave - 0.14f;
            m.Cylinder(new Vector3(x0, yy, wallZ + 0.08f), new Vector3(x1, yy, wallZ + 0.08f), 0.09f, 0.09f, 7, M3PropMesh.Swatch.Bamboo);
            foreach (float cx in new[] { x0 + 0.3f, x1 - 0.3f })
                m.Cylinder(new Vector3(cx, yy - 0.09f, wallZ + 0.16f), new Vector3(cx, yy - 0.6f, wallZ + 0.16f), 0.006f, 0.006f, 4, M3PropMesh.Swatch.Dark, false);
        }
    }

    /// <summary>A wooden bench against the front wall.</summary>
    private static int Bench(Yard y)
    {
        float wallZ = ModelFrontZ * y.S;
        float x = y.Between(-4.5f, 4.5f), z = wallZ + 0.36f;
        for (int tries = 0; tries < 4 && !y.Free(x, z, 1.6f, 0.6f); tries++) x = y.Between(-4.5f, 4.5f);
        if (!y.Free(x, z, 1.6f, 0.6f)) return 0;
        var m = y.far;
        var rot = Quaternion.Euler(0f, y.Between(-3f, 3f), 0f);
        var c = new Vector3(x, 0f, z);
        m.Box(c + Vector3.up * 0.44f, new Vector3(1.4f, 0.05f, 0.36f), rot, M3PropMesh.Swatch.WoodDark);
        foreach (float sx in new[] { -0.6f, 0.6f })
            m.Box(c + rot * new Vector3(sx, 0.21f, 0f), new Vector3(0.06f, 0.42f, 0.3f), rot, M3PropMesh.Swatch.Wood);
        m.Box(c + Vector3.up * 0.3f, new Vector3(1.2f, 0.05f, 0.05f), rot, M3PropMesh.Swatch.Wood);
        y.Take(x, z, 1.6f, 0.6f);
        return 1;
    }

    /// <summary>A lean-to of thatch on bamboo posts over part of the front wall; returns 1 if it stands.</summary>
    private static int Awning(Yard y)
    {
        float wallZ = ModelFrontZ * y.S, postZ = wallZ + 1.5f;
        float x0 = 0.35f, x1 = ModelMaxX * y.S - 0.4f;
        for (int tries = 0; tries < 3 && !y.Free(x0, postZ, 0.4f, 0.4f); tries++) x0 += 0.55f;
        for (int tries = 0; tries < 3 && !y.Free(x1, postZ, 0.4f, 0.4f); tries++) x1 -= 0.55f;
        if (x1 - x0 < 2.6f || !y.LaneClear(x0, postZ, LaneKeepOut) || !y.LaneClear(x1, postZ, LaneKeepOut)) return 0;

        // The roof's edge is at the eave, and its underside climbs from there to the wall (the overhang is
        // 0.6 m, the pitch about 25 degrees), so at the wall there is room above the number board, whose
        // top is at 2.53 m: the thatch starts 0.2 m above the eave's edge height and slopes down past it.
        float topWall = y.eave + 0.2f, topPost = Mathf.Max(topWall - 0.65f, 1.9f);
        var m = y.far;
        m.Cylinder(new Vector3(x0, -0.02f, postZ), new Vector3(x0, topPost, postZ), 0.055f, 0.045f, 6, M3PropMesh.Swatch.Bamboo);
        m.Cylinder(new Vector3(x1, -0.02f, postZ), new Vector3(x1, topPost, postZ), 0.055f, 0.045f, 6, M3PropMesh.Swatch.Bamboo);
        m.Cylinder(new Vector3(x0 - 0.1f, topPost, postZ), new Vector3(x1 + 0.1f, topPost, postZ), 0.04f, 0.04f, 5, M3PropMesh.Swatch.BambooDark);
        // Rafters from the wall down to the beam, and the thatch laid over them.
        int rafters = Mathf.Max(3, Mathf.RoundToInt((x1 - x0) / 0.9f) + 1);
        for (int i = 0; i < rafters; i++)
        {
            float x = Mathf.Lerp(x0, x1, i / (float)(rafters - 1));
            m.Cylinder(new Vector3(x, topWall + 0.02f, wallZ + 0.03f), new Vector3(x, topPost + 0.02f, postZ + 0.25f), 0.03f, 0.03f, 5, M3PropMesh.Swatch.Bamboo, false);
        }
        var from = new Vector3(0f, topWall + 0.08f, wallZ + 0.02f);
        var to = new Vector3(0f, topPost + 0.08f, postZ + 0.3f);
        var slope = to - from;
        var rot = Quaternion.FromToRotation(Vector3.forward, slope.normalized);
        m.Box(new Vector3((x0 + x1) * 0.5f, (from.y + to.y) * 0.5f, (from.z + to.z) * 0.5f), new Vector3(x1 - x0 + 0.5f, 0.06f, slope.magnitude), rot, M3PropMesh.Swatch.Thatch);
        y.Take(x0, postZ, 0.3f, 0.3f);
        y.Take(x1, postZ, 0.3f, 0.3f);
        y.awning = true;

        // Somewhere to sit in the shade: a low bamboo platform, with a basket and a folded cloth on it.
        float px = (x0 + x1) * 0.5f + y.Between(-0.4f, 0.4f), pz = wallZ + 0.95f;
        if (y.Free(px, pz, 1.8f, 1.05f)) Platform(y, px, pz);
        return 1;
    }

    private static void Platform(Yard y, float px, float pz)
    {
        const float W = 1.55f, D = 0.85f, H = 0.42f;
        var m = y.far;
        var yaw = Quaternion.Euler(0f, y.Between(-6f, 6f), 0f);
        var c = new Vector3(px, 0f, pz);
        m.Box(c + yaw * new Vector3(0f, H, 0f), new Vector3(W, 0.05f, D), yaw, M3PropMesh.Swatch.Bamboo);
        m.Box(c + yaw * new Vector3(0f, H - 0.06f, D * 0.5f - 0.03f), new Vector3(W, 0.07f, 0.06f), yaw, M3PropMesh.Swatch.BambooDark);
        m.Box(c + yaw * new Vector3(0f, H - 0.06f, -D * 0.5f + 0.03f), new Vector3(W, 0.07f, 0.06f), yaw, M3PropMesh.Swatch.BambooDark);
        foreach (float sx in new[] { -W * 0.5f + 0.08f, W * 0.5f - 0.08f })
            foreach (float sz in new[] { -D * 0.5f + 0.08f, D * 0.5f - 0.08f })
            {
                var foot = c + yaw * new Vector3(sx, 0f, sz);
                m.Cylinder(foot, foot + Vector3.up * (H - 0.02f), 0.035f, 0.035f, 5, M3PropMesh.Swatch.BambooDark);
            }
        // A basket at one end, a folded cloth at the other.
        var basketAt = c + yaw * new Vector3(W * 0.5f - 0.3f, H + 0.03f, 0f);
        m.Cylinder(basketAt, basketAt + Vector3.up * 0.2f, 0.13f, 0.18f, 8, M3PropMesh.Swatch.Basket);
        m.Cylinder(basketAt + Vector3.up * 0.2f, basketAt + Vector3.up * 0.215f, 0.19f, 0.19f, 8, M3PropMesh.Swatch.WoodDark);
        m.Box(c + yaw * new Vector3(-W * 0.5f + 0.35f, H + 0.06f, 0.05f), new Vector3(0.38f, 0.07f, 0.3f), yaw * Quaternion.Euler(0f, 12f, 0f), y.Pick(ClothSwatches));
        y.Take(px, pz, W + 0.2f, D + 0.2f);
    }

    private static readonly int[] ClothSwatches =
    {
        M3PropMesh.Swatch.ClothIndigo, M3PropMesh.Swatch.ClothMaroon, M3PropMesh.Swatch.ClothCream,
        M3PropMesh.Swatch.ClothOlive, M3PropMesh.Swatch.ClothTeal, M3PropMesh.Swatch.ClothRose,
    };

    /// <summary>A washing line on two bamboo poles in front of the wall; returns 1 if it went up.</summary>
    private static int Laundry(Yard y, M3YardLife life)
    {
        float z = ModelFrontZ * y.S + (y.awning ? 0.75f : 0.6f);
        float x0 = 0.8f + y.Between(0f, 0.3f), x1 = 4.6f + y.Between(-0.3f, 0f);
        const float H = 1.72f;
        // The flowers along the wall take some of these spots: slide each pole along until it stands clear.
        for (int tries = 0; tries < 4 && !y.Free(x0, z, 0.25f, 0.25f); tries++) x0 += 0.35f;
        for (int tries = 0; tries < 4 && !y.Free(x1, z, 0.25f, 0.25f); tries++) x1 -= 0.35f;
        if (x1 - x0 < 2.2f || !y.Free(x0, z, 0.25f, 0.25f) || !y.Free(x1, z, 0.25f, 0.25f)) return 0;

        var m = y.far;
        m.Cylinder(new Vector3(x0, -0.02f, z), new Vector3(x0 + y.Between(-0.03f, 0.03f), H, z), 0.03f, 0.024f, 5, M3PropMesh.Swatch.BambooDark);
        m.Cylinder(new Vector3(x1, -0.02f, z), new Vector3(x1 + y.Between(-0.03f, 0.03f), H, z), 0.03f, 0.024f, 5, M3PropMesh.Swatch.BambooDark);
        float lineY = H - 0.06f, mid = (x0 + x1) * 0.5f;
        m.Cylinder(new Vector3(x0, lineY, z), new Vector3(mid, lineY - 0.04f, z), 0.006f, 0.006f, 4, M3PropMesh.Swatch.Dark, false);
        m.Cylinder(new Vector3(mid, lineY - 0.04f, z), new Vector3(x1, lineY, z), 0.006f, 0.006f, 4, M3PropMesh.Swatch.Dark, false);
        y.Take(x0, z, 0.25f, 0.25f);
        y.Take(x1, z, 0.25f, 0.25f);

        // The cloth hangs from a pivot on the line, so it can swing about it; built in the pivot's space.
        var cloth = new M3PropMesh();
        float span = x1 - x0, cx = -span * 0.5f + 0.3f;
        int pieces = y.rng.Next(3, 5);
        for (int i = 0; i < pieces; i++)
        {
            float w = y.Between(0.32f, 0.5f), h = y.Between(0.5f, 0.85f);
            if (cx + w > span * 0.5f - 0.25f) break;
            cloth.Slat(new Vector3(cx + w * 0.5f, -h, 0f), Vector3.right, w, h, 0.004f, y.Pick(ClothSwatches));
            cx += w + y.Between(0.1f, 0.3f);
        }
        var pivot = new GameObject("Washing line").transform;
        pivot.SetParent(y.root, false);
        pivot.localPosition = new Vector3(mid, lineY - 0.03f, z);
        cloth.Spawn("Washing", pivot);
        life.AddLine(pivot);
        return 1;
    }

    private static int Woodpile(Yard y)
    {
        float wallZ = ModelFrontZ * y.S;
        // Against the front wall at the far end, else at the near end, else against the far side wall.
        if (y.Free(ModelMinX * y.S + 0.8f, wallZ + 0.38f, 1.5f, 0.7f)) Logs(y, new Vector3(ModelMinX * y.S + 0.8f, 0f, wallZ + 0.36f), Vector3.right, 1.3f);
        else if (y.Free(ModelMaxX * y.S - 0.95f, wallZ + 0.38f, 1.5f, 0.7f)) Logs(y, new Vector3(ModelMaxX * y.S - 0.95f, 0f, wallZ + 0.36f), Vector3.right, 1.25f);
        else if (y.Free(ModelMinX * y.S - 0.4f, -1.2f, 0.7f, 1.5f)) Logs(y, new Vector3(ModelMinX * y.S - 0.38f, 0f, -1.2f), Vector3.forward, 1.3f);
        else return 0;
        return 1;
    }

    /// <summary>Split logs stacked in three rows, lying along <paramref name="along"/>, about <paramref name="centre"/>.</summary>
    private static void Logs(Yard y, Vector3 centre, Vector3 along, float length)
    {
        var across = Vector3.Cross(Vector3.up, along).normalized;
        const float R = 0.075f;
        int[] rows = { 4, 3, 2 };
        for (int row = 0; row < rows.Length; row++)
        {
            float yy = R + row * R * 1.75f;
            for (int i = 0; i < rows[row]; i++)
            {
                float off = (i - (rows[row] - 1) * 0.5f) * R * 2.1f;
                float len = length * y.Between(0.88f, 1f);
                var c = centre + across * off + Vector3.up * yy + along * y.Between(-0.06f, 0.06f);
                var a = c - along * (len * 0.5f) + across * y.Between(-0.01f, 0.01f);
                var b = c + along * (len * 0.5f);
                y.far.Cylinder(a, b, R * y.Between(0.8f, 1f), R * y.Between(0.8f, 1f), 6, y.Chance(50) ? M3PropMesh.Swatch.Wood : M3PropMesh.Swatch.WoodDark, false);
                y.far.Cylinder(a - along * 0.001f, a + along * 0.002f, R * 0.9f, R * 0.9f, 6, M3PropMesh.Swatch.LogEnd);
                y.far.Cylinder(b - along * 0.002f, b + along * 0.001f, R * 0.9f, R * 0.9f, 6, M3PropMesh.Swatch.LogEnd);
            }
        }
        var size = Vector3.one * 0.7f + along * length;
        y.Take(centre.x, centre.z, size.x, size.z);
    }

    private static int SpiritHouse(Yard y)
    {
        float z = ModelFrontZ * y.S + 1.8f;
        float x = ModelMinX * y.S + 0.5f;
        if (!y.Free(x, z, 0.7f, 0.7f) || !y.LaneClear(x, z, LaneKeepOut + 0.4f))
        {
            x = ModelMaxX * y.S - 0.9f;
            if (!y.Free(x, z, 0.7f, 0.7f) || !y.LaneClear(x, z, LaneKeepOut + 0.4f)) return 0;
        }
        var m = y.far;
        var c = new Vector3(x, 0f, z);
        var yaw = Quaternion.Euler(0f, y.Between(-12f, 12f), 0f);
        m.Cylinder(c + Vector3.down * 0.02f, c + Vector3.up * 1.12f, 0.065f, 0.055f, 7, M3PropMesh.Swatch.Concrete);
        m.Box(c + Vector3.up * 1.14f, new Vector3(0.52f, 0.045f, 0.46f), yaw, M3PropMesh.Swatch.Concrete);
        m.Box(c + Vector3.up * 1.32f, new Vector3(0.34f, 0.32f, 0.3f), yaw, M3PropMesh.Swatch.ShrineRed);
        m.Box(c + yaw * new Vector3(0f, 1.3f, 0.156f), new Vector3(0.12f, 0.2f, 0.012f), yaw, M3PropMesh.Swatch.Gold);
        m.Box(c + Vector3.up * 1.49f, new Vector3(0.54f, 0.03f, 0.5f), yaw, M3PropMesh.Swatch.WoodDark);
        // A pitched roof, gold-trimmed, with a finial: four triangles to a point.
        var apex = c + Vector3.up * 1.82f;
        var corners = new Vector3[4];
        for (int i = 0; i < 4; i++)
            corners[i] = c + yaw * new Vector3((i == 0 || i == 3) ? -0.28f : 0.28f, 1.5f, i < 2 ? -0.26f : 0.26f);
        m.Triangle(corners[0], apex, corners[1], M3PropMesh.Swatch.Gold);
        m.Triangle(corners[1], apex, corners[2], M3PropMesh.Swatch.Gold);
        m.Triangle(corners[2], apex, corners[3], M3PropMesh.Swatch.Gold);
        m.Triangle(corners[3], apex, corners[0], M3PropMesh.Swatch.Gold);
        m.Cylinder(apex, apex + Vector3.up * 0.12f, 0.012f, 0.004f, 4, M3PropMesh.Swatch.Gold);
        y.Take(x, z, 0.7f, 0.7f);
        return 1;
    }

    private static void Sandals(Yard y)
    {
        float x = ModelMaxX * y.S + 0.3f, zDoor = ModelDoorZ * y.S;
        int pairs = y.rng.Next(1, 3);
        for (int p = 0; p < pairs; p++)
        {
            float z = zDoor + (p == 0 ? -0.5f : 0.4f) + y.Between(-0.08f, 0.08f);
            var yaw = Quaternion.Euler(0f, y.Between(-25f, 25f), 0f);
            int colour = y.Pick(new[] { M3PropMesh.Swatch.SandalBlue, M3PropMesh.Swatch.SandalRose, M3PropMesh.Swatch.Dark, M3PropMesh.Swatch.Rust });
            foreach (float side in new[] { -0.065f, 0.065f })
            {
                var at = new Vector3(x, 0.012f, z) + yaw * new Vector3(side, 0f, 0f);
                y.near.Box(at, new Vector3(0.1f, 0.022f, 0.26f), yaw * Quaternion.Euler(0f, y.Between(-6f, 6f), 0f), colour);
            }
        }
    }

    private static void Broom(Yard y)
    {
        float wallX = ModelMaxX * y.S, z = ModelDoorZ * y.S - 1.0f + y.Between(-0.1f, 0.1f);
        var head = new Vector3(wallX + 0.34f, 0.02f, z);
        var top = new Vector3(wallX + 0.05f, 1.45f, z + 0.03f);
        y.near.Cylinder(head + Vector3.up * 0.28f, top, 0.013f, 0.011f, 5, M3PropMesh.Swatch.Bamboo, false);
        // The head: a fan of grass bound to the handle, flaring out to the ground.
        var dir = (top - head).normalized;
        var rot = Quaternion.FromToRotation(Vector3.up, dir);
        y.near.Box(head + dir * 0.16f, new Vector3(0.14f, 0.32f, 0.045f), rot, M3PropMesh.Swatch.Straw);
        y.near.Box(head + dir * 0.05f, new Vector3(0.24f, 0.1f, 0.05f), rot, M3PropMesh.Swatch.Straw);
        y.near.Cylinder(head + dir * 0.3f, head + dir * 0.34f, 0.03f, 0.03f, 6, M3PropMesh.Swatch.Dark);
    }

    /// <summary>A bicycle leaning against the front wall; returns 1 if it stands.</summary>
    private static int Bicycle(Yard y)
    {
        float wallZ = ModelFrontZ * y.S;
        // Against the front wall at either end, or along a side wall (by the door on the +x side, where the
        // path runs a metre out; the -x side faces the neighbour's path, further off).
        var spots = new[]
        {
            (x: ModelMaxX * y.S - 1.35f, z: wallZ + 0.36f, alongX: true, yaw: 0f),
            (x: ModelMinX * y.S + 1.35f, z: wallZ + 0.36f, alongX: true, yaw: 0f),
            (x: ModelMaxX * y.S + 0.34f, z: y.Between(1.2f, 2.4f), alongX: false, yaw: 90f),
            (x: ModelMinX * y.S - 0.34f, z: y.Between(-1.5f, 2.4f), alongX: false, yaw: 270f),
        };
        float x = 0f, z = 0f, yaw = 0f;
        bool found = false, alongX = true;
        foreach (var spot in spots)
        {
            if (!y.Free(spot.x, spot.z, spot.alongX ? 2.0f : 0.6f, spot.alongX ? 0.6f : 2.0f)) continue;
            x = spot.x; z = spot.z; yaw = spot.yaw; alongX = spot.alongX; found = true;
            break;
        }
        if (!found) return 0;
        // Leaning in against the wall (the wall is at the bike's -z once it is turned), back wheel first.
        var lean = Quaternion.Euler(0f, yaw + (y.Chance(50) ? 0f : 180f), 0f) * Quaternion.Euler(-11f, 0f, 0f);
        var bike = new M3PropMesh();
        int frame = y.Pick(new[] { M3PropMesh.Swatch.Tarp, M3PropMesh.Swatch.ShrineRed, M3PropMesh.Swatch.Dark, M3PropMesh.Swatch.ClothOlive });
        const float R = 0.33f;
        foreach (float wx in new[] { -0.55f, 0.55f })
        {
            bike.Cylinder(new Vector3(wx, R, -0.015f), new Vector3(wx, R, 0.015f), R, R, 14, M3PropMesh.Swatch.Dark);
            bike.Cylinder(new Vector3(wx, R, -0.03f), new Vector3(wx, R, 0.03f), 0.04f, 0.04f, 6, M3PropMesh.Swatch.Tin);
        }
        void Tube(Vector3 a, Vector3 b, float r = 0.018f) => bike.Cylinder(a, b, r, r, 5, frame, false);
        var rearHub = new Vector3(-0.55f, R, 0f);
        var frontHub = new Vector3(0.55f, R, 0f);
        var bracket = new Vector3(0.05f, 0.3f, 0f);
        var seat = new Vector3(-0.12f, 0.86f, 0f);
        var head = new Vector3(0.42f, 0.82f, 0f);
        Tube(rearHub, seat); Tube(rearHub, bracket); Tube(bracket, seat); Tube(seat, head); Tube(head, bracket);
        Tube(head, frontHub, 0.014f); Tube(head, head + new Vector3(0.03f, 0.14f, 0f), 0.016f);
        bike.Box(head + new Vector3(0.03f, 0.15f, 0f), new Vector3(0.03f, 0.03f, 0.5f), M3PropMesh.Swatch.Dark);
        bike.Box(seat + new Vector3(-0.02f, 0.04f, 0f), new Vector3(0.26f, 0.05f, 0.11f), M3PropMesh.Swatch.WoodDark);
        bike.Box(bracket, new Vector3(0.05f, 0.05f, 0.16f), M3PropMesh.Swatch.Dark);
        bike.Box(bracket + new Vector3(0.12f, 0.06f, 0.1f), new Vector3(0.08f, 0.02f, 0.05f), M3PropMesh.Swatch.Dark);
        bike.Box(bracket + new Vector3(-0.12f, -0.06f, -0.1f), new Vector3(0.08f, 0.02f, 0.05f), M3PropMesh.Swatch.Dark);
        var rot = Quaternion.Euler(0f, y.Between(-4f, 4f), 0f) * lean;
        y.far.Append(bike, Matrix4x4.TRS(new Vector3(x, 0.01f, z), rot, Vector3.one));
        y.Take(x, z, alongX ? 1.9f : 0.5f, alongX ? 0.5f : 1.9f);
        return 1;
    }

    private static readonly int[] DogCoats = { M3PropMesh.Swatch.HenBrown, M3PropMesh.Swatch.Sack, M3PropMesh.Swatch.Dark, M3PropMesh.Swatch.Straw };

    /// <summary>A dog asleep in the yard, in the shade if there is any; returns 1 if it lies there.</summary>
    private static int Dog(Yard y, M3YardLife life)
    {
        float wallZ = ModelFrontZ * y.S;
        float x = 0f, z = 0f;
        bool found = false;
        for (int tries = 0; tries < 6 && !found; tries++)
        {
            x = y.Between(-4.5f, 4.5f);
            z = wallZ + y.Between(0.8f, 1.9f);
            found = y.Free(x, z, 1.0f, 1.0f) && y.LaneClear(x, z, LaneKeepOut + 0.6f);
        }
        if (!found)
        {
            // In the path by the side door, then, where a dog lies anyway.
            x = ModelMaxX * y.S + PathOffset + 0.3f;
            z = ModelDoorZ * y.S + 1.6f;
            found = y.Free(x - 0.1f, z, 0.1f, 0.1f);   // the path strip itself is marked taken; anything else there is not
            if (!found) return 0;
        }
        int coat = y.Pick(DogCoats);
        var m = new M3PropMesh();
        m.Ellipsoid(new Vector3(0f, 0.15f, 0f), new Vector3(0.15f, 0.14f, 0.3f), 8, 6, coat);
        m.Ellipsoid(new Vector3(0f, 0.24f, 0.31f), new Vector3(0.075f, 0.07f, 0.1f), Quaternion.Euler(15f, 0f, 0f), 7, 5, coat);
        m.Cylinder(new Vector3(0f, 0.21f, 0.38f), new Vector3(0f, 0.19f, 0.47f), 0.04f, 0.026f, 6, M3PropMesh.Swatch.Dark);
        foreach (float side in new[] { -1f, 1f })
        {
            m.Slat(new Vector3(side * 0.06f, 0.26f, 0.29f), Quaternion.Euler(0f, 0f, side * 35f) * Vector3.right, 0.05f, 0.08f, 0.004f, coat);
            m.Box(new Vector3(side * 0.1f, 0.05f, 0.3f), new Vector3(0.06f, 0.09f, 0.3f), coat);
            m.Ellipsoid(new Vector3(side * 0.14f, 0.09f, -0.12f), new Vector3(0.06f, 0.09f, 0.12f), 6, 4, coat);
        }
        m.Cylinder(new Vector3(0.05f, 0.09f, -0.3f), new Vector3(0.3f, 0.05f, -0.16f), 0.025f, 0.012f, 5, coat);
        var dog = m.Spawn("Dog", y.root);
        dog.transform.localPosition = new Vector3(x, 0.005f, z);
        dog.transform.localRotation = Quaternion.Euler(0f, y.Between(0f, 360f), 0f);
        dog.transform.localScale = Vector3.one * y.Between(0.9f, 1.1f);
        M3DistanceCulling.Mark(dog);
        life.AddSleeper(dog.transform);
        y.Take(x, z, 0.9f, 0.9f);
        return 1;
    }

    // ---------- trees and animals ----------

    private static int Trees(Yard y, Pools pools)
    {
        int count = 0;
        // A broad tree behind the house, its crown over the roof. The shared tree is 4 m tall and 3.6 m
        // across at scale 1; the roof's ridge is at 5 m.
        if (pools.broadTree != null && y.Chance(55))
        {
            float x = y.Between(-3.5f, 3.5f), z = ModelBackZ * y.S - y.Between(4.8f, 6.4f);
            if (y.Free(x, z, 1.6f, 1.6f))
            {
                Instantiate(pools.broadTree, y.root, new Vector3(x, 0f, z), y.Between(0f, 360f), y.Between(2.0f, 2.6f));
                y.Take(x, z, 1.2f, 1.2f);
                count++;
            }
        }
        // Another in the gap to the -x neighbour where the gap has room (the door path runs down the other
        // side of every gap, a metre from the neighbour's wall), or beyond the end of the row.
        if (pools.broadTree != null && y.leftGap >= 3.8f && y.Chance(65))
        {
            float x = ModelMinX * y.S - 1.2f - y.Between(0f, Mathf.Min(1.2f, y.leftGap - 3.8f)), z = y.Between(1.5f, 4.5f);
            if (y.Free(x, z, 1.6f, 1.6f))
            {
                Instantiate(pools.broadTree, y.root, new Vector3(x, 0f, z), y.Between(0f, 360f), y.Between(1.8f, 2.3f));
                y.Take(x, z, 1.2f, 1.2f);
                count++;
            }
        }
        // A palm at a front corner of the yard.
        if (pools.palm != null && y.Chance(28))
        {
            float x = ModelMinX * y.S + y.Between(-0.3f, 0.5f), z = ModelFrontZ * y.S + y.Between(1.4f, 2.2f);
            if (y.Free(x, z, 0.7f, 0.7f) && y.LaneClear(x, z, LaneKeepOut + 0.5f))
            {
                Instantiate(pools.palm, y.root, new Vector3(x, 0f, z), y.Between(0f, 360f), y.Between(0.9f, 1.2f));
                y.Take(x, z, 0.6f, 0.6f);
                count++;
            }
        }
        return count;
    }

    private static readonly int[] Plumage = { M3PropMesh.Swatch.HenWhite, M3PropMesh.Swatch.HenBrown, M3PropMesh.Swatch.HenBrown, M3PropMesh.Swatch.HenBlack };

    /// <summary>A few hens in the yard, or at the mouth of the path; returns how many.</summary>
    private static int Hens(Yard y, M3YardLife life)
    {
        float wallZ = ModelFrontZ * y.S;
        float hx = y.Between(0.5f, 4f), hz = wallZ + y.Between(1.0f, 1.9f);
        if (!y.Free(hx, hz, 1.2f, 1.2f))
        {
            hx = ModelMaxX * y.S + PathOffset + 0.4f;
            hz = wallZ + 1.4f;
        }
        var home = y.plot.TransformPoint(new Vector3(hx, 0f, hz));
        home.y = 0f;
        int count = y.rng.Next(2, 4);
        for (int i = 0; i < count; i++)
        {
            float a = y.Between(0f, Mathf.PI * 2f), r = y.Between(0.2f, 0.9f);
            var at = home + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            Hen(life, at, home, 1.3f, y.Pick(Plumage), y.Between(0f, 360f), y.Between(0.85f, 1.05f));
        }
        return count;
    }

    /// <summary>One hen: a body mesh and a head mesh on a neck pivot, so it can peck.</summary>
    private static void Hen(M3YardLife life, Vector3 at, Vector3 home, float radius, int plumage, float yaw, float size)
    {
        var body = new M3PropMesh();
        body.Ellipsoid(new Vector3(0f, 0.2f, 0f), new Vector3(0.12f, 0.105f, 0.165f), Quaternion.Euler(-12f, 0f, 0f), 8, 6, plumage);
        // Tail: two slats fanning up at the back. Legs: two thin cylinders with a flat foot.
        body.Slat(new Vector3(0.02f, 0.23f, -0.15f), Quaternion.Euler(0f, 0f, 20f) * Vector3.forward, 0.09f, 0.11f, 0.004f, plumage);
        body.Slat(new Vector3(-0.02f, 0.23f, -0.15f), Quaternion.Euler(0f, 0f, -20f) * Vector3.forward, 0.09f, 0.1f, 0.004f, plumage);
        foreach (float side in new[] { -0.04f, 0.04f })
        {
            body.Cylinder(new Vector3(side, 0.11f, 0.02f), new Vector3(side, 0.005f, 0.02f), 0.009f, 0.009f, 4, M3PropMesh.Swatch.Leg, false);
            body.Box(new Vector3(side, 0.006f, 0.045f), new Vector3(0.04f, 0.01f, 0.09f), M3PropMesh.Swatch.Leg);
        }
        var head = new M3PropMesh();
        head.Cylinder(Vector3.zero, new Vector3(0f, 0.1f, 0.06f), 0.032f, 0.026f, 6, plumage, false);
        head.Ellipsoid(new Vector3(0f, 0.12f, 0.07f), new Vector3(0.042f, 0.04f, 0.05f), 6, 4, plumage);
        head.Cylinder(new Vector3(0f, 0.115f, 0.11f), new Vector3(0f, 0.105f, 0.16f), 0.014f, 0.002f, 4, M3PropMesh.Swatch.Beak);
        head.Box(new Vector3(0f, 0.165f, 0.065f), new Vector3(0.012f, 0.03f, 0.055f), M3PropMesh.Swatch.Comb);
        head.Box(new Vector3(0f, 0.085f, 0.1f), new Vector3(0.01f, 0.025f, 0.02f), M3PropMesh.Swatch.Comb);

        var hen = body.Spawn("Hen", life.transform);
        hen.transform.position = at;
        hen.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        hen.transform.localScale = Vector3.one * size;
        var neck = head.Spawn("Head", hen.transform);
        neck.transform.localPosition = new Vector3(0f, 0.25f, 0.12f);
        life.AddHen(hen.transform, neck.transform, new Vector3(home.x, 0f, home.z), radius);
    }

    // ---------- the lane ----------

    /// <summary>Concrete power poles down the low-z side of the lane, wired together; returns the renderers made.</summary>
    private static int Poles(Transform root, Bounds village, List<Yard> yards, System.Random rng)
    {
        var mesh = new M3PropMesh();
        var tops = new List<Vector3>();
        // The first pole stands well past the head of the lane, where the Pilot starts (M3XRPlayer spawns
        // 8 m in from the village bounds): one right there filled the first view of the village.
        float x = village.min.x + 11f + (float)rng.NextDouble() * 4f;
        while (x < village.max.x - 1f)
        {
            var at = LaneSide(x, -PoleFromLane);
            // Not at the mouth of a path to a door, and not on anything already in a yard.
            bool blocked = false;
            foreach (var y in yards)
            {
                var local = y.plot.InverseTransformPoint(at);
                if (local.x < -9f || local.x > 10f || local.z < 0f || local.z > 10f) continue;
                if (!y.Free(local.x, local.z, 0.5f, 0.5f)) { blocked = true; break; }
            }
            if (blocked) { x += 1.5f; continue; }

            var top = at + Vector3.up * 6.3f;
            mesh.Cylinder(at + Vector3.down * 0.1f, top, 0.13f, 0.09f, 8, M3PropMesh.Swatch.Pole);
            // The crossarm lies across the lane; the wires run along it from insulator to insulator.
            var across = Vector3.Cross(Vector3.up, LaneDirection(x)).normalized;
            var armRot = Quaternion.LookRotation(across, Vector3.up);
            mesh.Box(top + Vector3.down * 0.3f, new Vector3(0.09f, 0.09f, 0.95f), armRot, M3PropMesh.Swatch.WoodDark);
            foreach (float side in new[] { -0.36f, 0.36f })
            {
                var pin = top + Vector3.down * 0.25f + across * side;
                mesh.Cylinder(pin, pin + Vector3.up * 0.13f, 0.04f, 0.035f, 5, M3PropMesh.Swatch.Dark);
            }
            tops.Add(top + Vector3.down * 0.12f);
            foreach (var y in yards)
            {
                var local = y.plot.InverseTransformPoint(at);
                if (local.x > -9f && local.x < 10f && local.z > 0f && local.z < 10f) y.Take(local.x, local.z, 0.45f, 0.45f);
            }
            x += 16f + (float)rng.NextDouble() * 6f;
        }
        if (tops.Count == 0) return 0;

        // A service drop from the nearest pole to every house within reach, to the front corner under the
        // eave: for the far row that is a wire slung right across the lane. Thin cylinders in the poles' mesh.
        foreach (var y in yards)
        {
            int nearest = -1;
            float best = 24f;
            var corner = y.plot.TransformPoint(new Vector3(ModelMaxX * y.S - 0.3f, y.eave + 0.05f, ModelFrontZ * y.S + 0.1f));
            var other = y.plot.TransformPoint(new Vector3(ModelMinX * y.S + 0.3f, y.eave + 0.05f, ModelFrontZ * y.S + 0.1f));
            for (int i = 0; i < tops.Count; i++)
            {
                float d = Mathf.Min(Vector3.Distance(tops[i], corner), Vector3.Distance(tops[i], other));
                if (d < best) { best = d; nearest = i; }
            }
            if (nearest < 0) continue;
            var end = Vector3.Distance(tops[nearest], corner) <= Vector3.Distance(tops[nearest], other) ? corner : other;
            var from = tops[nearest] + Vector3.down * 0.5f;
            Vector3 previous = from;
            const int Segments = 7;
            for (int k = 1; k <= Segments; k++)
            {
                float t = k / (float)Segments;
                var at = Vector3.Lerp(from, end, t) + Vector3.down * (0.3f * 4f * t * (1f - t));
                mesh.Cylinder(previous, at, 0.012f, 0.012f, 4, M3PropMesh.Swatch.Dark, false);
                previous = at;
            }
            mesh.Box(end + Vector3.down * 0.06f, new Vector3(0.1f, 0.14f, 0.06f), M3PropMesh.Swatch.Dark);   // the box on the wall
        }

        int renderers = 1;
        if (tops.Count >= 2)
        {
            for (int w = 0; w < 2; w++)
            {
                float side = w == 0 ? -0.36f : 0.36f;
                var points = new List<Vector3>();
                for (int i = 0; i + 1 < tops.Count; i++)
                {
                    var a = tops[i] + Vector3.Cross(Vector3.up, tops[i + 1] - tops[i]).normalized * side;
                    var b = tops[i + 1] + Vector3.Cross(Vector3.up, tops[i + 1] - tops[i]).normalized * side;
                    const int Samples = 10;
                    for (int k = i == 0 ? 0 : 1; k <= Samples; k++)
                    {
                        float t = k / (float)Samples;
                        points.Add(Vector3.Lerp(a, b, t) + Vector3.down * (0.45f * 4f * t * (1f - t)));
                    }
                }
                Wire(root, $"Wire {w}", points);
                renderers++;
                if (w == 0) Birds(mesh, points, rng);
            }
        }
        mesh.Spawn("Power poles", root);
        return renderers;
    }

    /// <summary>A few small birds perched along one or two spans of the wire, into the poles' own mesh.</summary>
    private static void Birds(M3PropMesh mesh, List<Vector3> wire, System.Random rng)
    {
        const int Samples = 10;
        int spans = (wire.Count - 1) / Samples;
        if (spans < 1) return;
        int flocks = spans >= 3 ? 2 : 1;
        for (int f = 0; f < flocks; f++)
        {
            int span = rng.Next(spans);
            int start = span * Samples;
            int birds = 3 + rng.Next(4);
            float t = 0.25f + (float)rng.NextDouble() * 0.2f;
            for (int b = 0; b < birds; b++)
            {
                float u = t * Samples;
                int i = Mathf.Clamp((int)u, 0, Samples - 1);
                var at = Vector3.Lerp(wire[start + i], wire[start + i + 1], u - i);
                var along = (wire[start + i + 1] - wire[start + i]).normalized * (rng.Next(2) == 0 ? 1f : -1f);
                Bird(mesh, at, along);
                t += 0.06f + (float)rng.NextDouble() * 0.08f;
                if (t > 0.85f) break;
            }
        }
    }

    private static void Bird(M3PropMesh mesh, Vector3 perch, Vector3 facing)
    {
        var rot = Quaternion.LookRotation(facing, Vector3.up);
        var body = perch + Vector3.up * 0.06f;
        mesh.Ellipsoid(body, new Vector3(0.035f, 0.033f, 0.06f), rot * Quaternion.Euler(-20f, 0f, 0f), 6, 4, M3PropMesh.Swatch.Dark);
        mesh.Ellipsoid(body + rot * new Vector3(0f, 0.035f, 0.05f), new Vector3(0.024f, 0.022f, 0.026f), 5, 3, M3PropMesh.Swatch.Dark);
        mesh.Slat(body + rot * new Vector3(0f, -0.01f, -0.07f), rot * Vector3.right, 0.03f, 0.05f, 0.003f, M3PropMesh.Swatch.Dark);
    }

    private static Material wireMaterial;

    private static void Wire(Transform root, string name, List<Vector3> points)
    {
        if (wireMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            wireMaterial = new Material(shader) { name = "M3 wire", color = new Color(0.1f, 0.09f, 0.08f), hideFlags = HideFlags.DontSave };
            if (wireMaterial.HasProperty("_BaseColor")) wireMaterial.SetColor("_BaseColor", new Color(0.1f, 0.09f, 0.08f));
            if (wireMaterial.HasProperty("_Smoothness")) wireMaterial.SetFloat("_Smoothness", 0f);
        }
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = points.Count;
        line.SetPositions(points.ToArray());
        line.startWidth = line.endWidth = 0.03f;
        line.numCornerVertices = 0;
        line.numCapVertices = 0;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        line.sharedMaterial = wireMaterial;
    }

    /// <summary>The point <paramref name="offset"/> metres off the lane's centre line at <paramref name="x"/>, toward +z for positive.</summary>
    private static Vector3 LaneSide(float x, float offset)
    {
        float slope = VillageLayout.LaneSlope(x);
        float norm = Mathf.Sqrt(1f + slope * slope);
        return new Vector3(x - slope / norm * offset, 0f, VillageLayout.LaneZ(x) + offset / norm);
    }

    private static Vector3 LaneDirection(float x) => new Vector3(1f, 0f, VillageLayout.LaneSlope(x)).normalized;

    /// <summary>Trees closing the far end of the lane, and one at its head; returns how many stood.</summary>
    private static int Grove(Transform root, Bounds village, List<Yard> yards, Pools pools, System.Random rng)
    {
        int count = 0;
        float endX = village.max.x + 1f + (float)rng.NextDouble() * 3f;
        var spots = new List<(float x, float off, bool palm, float scale)>
        {
            (endX, 5.5f + (float)rng.NextDouble() * 2f, false, 2.5f),
            (endX + 2f, -(5f + (float)rng.NextDouble() * 2f), false, 2.7f),
            (endX + 6f, 8.5f, true, 1.15f),
            (endX + 5f, -9f, true, 1.25f),
            (endX + 9f, 3.5f, false, 2.3f),
            (village.min.x - 4f - (float)rng.NextDouble() * 2f, 6f + (float)rng.NextDouble() * 2f, false, 2.4f),
        };
        foreach (var s in spots)
        {
            var prefab = s.palm ? pools.palm : pools.broadTree;
            if (prefab == null) continue;
            var at = LaneSide(s.x, s.off);
            bool blocked = false;
            foreach (var y in yards)
            {
                var local = y.plot.InverseTransformPoint(at);
                if (local.x > -8f && local.x < 9f && local.z > -7f && local.z < 9f && !y.Free(local.x, local.z, 1.5f, 1.5f)) { blocked = true; break; }
            }
            if (blocked) continue;
            var tree = Instantiate(prefab, root, at, (float)rng.NextDouble() * 360f, s.scale * (0.9f + 0.2f * (float)rng.NextDouble()));
            tree.transform.position = at;
            count++;
        }
        return count;
    }

    // ---------- helpers ----------

    private static GameObject Named(GameObject[] pool, string name)
    {
        if (pool == null) return null;
        foreach (var p in pool) if (p != null && p.name == name) return p;
        return null;
    }

    private static GameObject[] AllNamed(GameObject[] pool, string prefix)
    {
        var found = new List<GameObject>();
        if (pool != null)
            foreach (var p in pool)
                if (p != null && p.name.StartsWith(prefix) && !found.Contains(p)) found.Add(p);
        return found.ToArray();
    }

    /// <summary>A scenery instance under <paramref name="parent"/> at a local position: no colliders, turned and scaled.</summary>
    private static GameObject Instantiate(GameObject prefab, Transform parent, Vector3 localPosition, float yaw, float scale)
    {
        var go = Object.Instantiate(prefab, parent);
        go.name = prefab.name;
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale *= scale;
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
        {
            if (Application.isPlaying) Object.Destroy(c);
            else Object.DestroyImmediate(c);
        }
        return go;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
    private static MaterialPropertyBlock block;

    /// <summary>Colours a bush the way M3RoomDresser does: a tint, and its UVs moved to another swatch of its palette.</summary>
    private static void Tint(Renderer r, Color colour, Vector2 swatchOffset)
    {
        if (block == null) block = new MaterialPropertyBlock();
        block.Clear();
        block.SetColor(BaseColorId, colour);
        block.SetVector(BaseMapStId, new Vector4(1f, 1f, swatchOffset.x, swatchOffset.y));
        r.SetPropertyBlock(block);
    }
}
