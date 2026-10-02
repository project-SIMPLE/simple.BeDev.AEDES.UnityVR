using System.Collections.Generic;
using Aedes.Module3.Sim;
using UnityEngine;

/// <summary>
/// Cheap outdoor life for the Module 3 village: wind in the palms and banana plants, dragonflies
/// over the paddies and along the lane, frogs at the yard edges, and a few villagers walking the
/// lane. (The clouds drift too, but that is M3TimeOfDay's: it turns the sky panorama with the sun.)
///
/// Decoration only. None of it is in the simulation, nothing here carries a collider (so nothing
/// can be pointed at, teleported to or walked into), and the walkers are not residents: they have
/// no VillagerView, no health state and no panel, so they cannot read as patients, and they look
/// like everyone else (section 5 - nothing marks anyone).
///
/// Built by M3Surroundings once the trees stand, from its seed, so every run places the same
/// animals on the same paths and screenshots compare run to run. Cost per frame: the wind is in the
/// vertex shader (M3FoliageLit) and costs nothing here; the rest is this one Update moving a dozen
/// transforms with no allocation, and anything beyond its radius from the camera is switched off.
/// The outdoor radii are larger than M3DistanceCulling's indoor 22 m, because a walker popping in
/// on the open lane is seen where a bed popping in behind a wall is not.
/// </summary>
public class M3OutdoorLife : MonoBehaviour
{
    /// <summary>Beyond this many metres from the camera an animal is switched off (and 4 m nearer, back on).</summary>
    private const float AnimalRadius = 30f, WalkerRadius = 40f, Hysteresis = 4f;
    /// <summary>
    /// A walker this close to the Pilot's head stops and waits, so the Pilot never walks through
    /// one. A Pilot going down the middle of the track passes the verges 1.7 m off, outside it.
    /// </summary>
    private const float PilotClearance = 1.6f;
    /// <summary>A dragonfly never comes closer than this to the Pilot's head, and a waiting walker steps aside inside it.</summary>
    private const float FlyClearance = 0.9f, MakeWay = 0.9f;
    private const string WalkParam = "isWalk";

    /// <summary>
    /// Where the walkers keep to: the verges of the dirt track, 1.7 m either side of its centre line (the
    /// track is 3.2 m wide), which is a metre clear of the yard jars whichever way the lane winds - no
    /// front wall stands nearer the centre line than 4.2 m (VillageLayout), and a jar stands 1.5 m out.
    /// </summary>
    private const float NearEdge = -1.7f, FarEdge = 1.7f;

    private struct Dragonfly
    {
        public Transform t;
        public Vector3 centre;
        public float ax, az, height, speed, phase;
        public bool figureEight;
        public bool active;
    }

    private struct Frog
    {
        public Transform t;
        public Vector3 home, hopFrom, hopTo;
        public float hopStart, nextHop;
        public bool active;
    }

    private struct Walker
    {
        public Transform t;
        public Animator animator;
        public Vector3[] path;
        public int next;
        public float speed;
        public bool active, walking, waiting;
    }

    private static readonly int WalkId = Animator.StringToHash(WalkParam);

    private Dragonfly[] dragonflies = new Dragonfly[0];
    private Frog[] frogs = new Frog[0];
    private Walker[] walkers = new Walker[0];
    private System.Random rng;
    private Camera eye;

    /// <summary>
    /// Called by M3Surroundings.Build with the trees already standing under <paramref name="surroundings"/>.
    /// <paramref name="village"/> covers the houses and their yards; <paramref name="plots"/> are the
    /// houses, whose yard edges the frogs sit at.
    /// </summary>
    /// <summary>
    /// Builds the life round <paramref name="village"/> (its bottom at ground level). <paramref name="lane"/>
    /// false means the village is the scene's own, with a road of its own that is not the generated lane:
    /// then nobody walks it and the dragonflies keep to the paddies.
    /// </summary>
    public static M3OutdoorLife Build(Transform surroundings, Bounds village, IReadOnlyList<HouseholdView> plots, int seed, bool lane = true)
    {
        var set = M3OutdoorLifeSet.Load();
        if (set == null)
        {
            Debug.LogWarning($"Module 3: no Resources/{M3OutdoorLifeSet.ResourcePath}, so the village stands still. "
                             + "Run AEDES > Module 3 > Generate Outdoor Life Assets.");
            return null;
        }

        var go = new GameObject("Outdoor Life");
        go.transform.SetParent(surroundings, false);
        go.SetActive(false);   // nothing wakes up until every body has been stripped of what it must not carry
        var life = go.AddComponent<M3OutdoorLife>();
        life.rng = new System.Random(seed * 7919 + 17);
        life.ApplyWind(surroundings, set);
        if (plots != null)
            for (int i = 0; i < plots.Count; i++) life.ApplyWind(plots[i].transform, set);   // the yards' own banana plants
        life.BuildDragonflies(village, set, lane);
        life.BuildFrogs(plots, set, village.min.y);
        if (lane) life.BuildWalkers(village, set);
        go.SetActive(true);
        return life;
    }

    // ---------- wind ----------

    /// <summary>
    /// Swaps the leaf materials of every tree under <paramref name="root"/> for the same materials on
    /// the Foliage Lit shader, per instance, the way M3HouseSurface textures the houses. The prefabs
    /// and T_CoconuTree.mat are shared with Modules 1 and 2 and are not touched; anything under the
    /// root with other materials (houses, jars, furniture) is left alone.
    /// </summary>
    private void ApplyWind(Transform root, M3OutdoorLifeSet set)
    {
        if (set.foliage == null || set.foliage.Length == 0) return;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                for (int k = 0; k < set.foliage.Length; k++)
                {
                    if (materials[i] != set.foliage[k].from || set.foliage[k].to == null) continue;
                    materials[i] = set.foliage[k].to;
                    changed = true;
                }
            }
            if (changed) renderer.sharedMaterials = materials;
        }
    }

    // ---------- dragonflies ----------

    private void BuildDragonflies(Bounds village, M3OutdoorLifeSet set, bool lane)
    {
        if (set.dragonflies == null || set.dragonflies.Length == 0) return;
        // Three over the nearest paddies behind each row's back yards (M3Surroundings.BuildPaddies
        // starts them 14 m past the village bounds and lays them in 13 m cells), three along the
        // lane. Nothing shares a row: each path's centre is drawn from a band, not put on a line.
        var list = new List<Dragonfly>(6);
        for (int i = 0; i < 6; i++)
        {
            var prefab = set.dragonflies[i % set.dragonflies.Length];
            if (prefab == null) continue;
            bool overPaddy = !lane || i < 3;
            float x = Mathf.Lerp(village.min.x + 8f, village.max.x - 8f, (float)rng.NextDouble());
            float paddyDepth = 14f + 3f + 14f * (float)rng.NextDouble();   // over the first two cells
            float z = overPaddy
                ? (i % 2 == 0 ? village.max.z + paddyDepth : village.min.z - paddyDepth)
                : VillageLayout.LaneZ(x) + (rng.Next(2) == 0 ? -1f : 1f) * (0.5f + 3f * (float)rng.NextDouble());
            var d = new Dragonfly
            {
                centre = new Vector3(x, village.min.y, z),
                ax = overPaddy ? 6f + 3f * (float)rng.NextDouble() : 4f + 3f * (float)rng.NextDouble(),
                az = overPaddy ? 4f + 2f * (float)rng.NextDouble() : 1.2f + 0.6f * (float)rng.NextDouble(),
                height = 1.1f + 0.7f * (float)rng.NextDouble(),
                speed = 0.35f + 0.2f * (float)rng.NextDouble(),
                phase = (float)rng.NextDouble() * Mathf.PI * 2f,
                figureEight = i % 2 == 0,
                active = true,
            };
            var fly = Instantiate(prefab, d.centre, Quaternion.identity, transform);
            fly.name = $"Dragonfly_{i}";
            fly.transform.localScale = Vector3.one * DragonflyScale;
            Prepare(fly);
            d.t = fly.transform;
            list.Add(d);
        }
        dragonflies = list.ToArray();
    }

    /// <summary>
    /// The Team Assets dragonfly is modelled life-size (13 cm across), which from a paddy dyke is a
    /// speck; here it is about twice life, 29 cm. The frog is modelled 1.7 m long for Module 1's
    /// mosquito-eye view, and is brought down to 20 cm, a big toad.
    /// </summary>
    private const float DragonflyScale = 2.2f, FrogScale = 0.12f;

    // ---------- frogs ----------

    private void BuildFrogs(IReadOnlyList<HouseholdView> plots, M3OutdoorLifeSet set, float groundY)
    {
        if (set.frog == null || plots == null || plots.Count == 0) return;
        // Three, each somewhere in a front yard's corner - in front of the house wall (which stands
        // 4.8 m out from the plot centre), short of the walkers' verge, clear of the worn side-door
        // path (about 7 m out) and of the jar in front - and never at the same spot on two yards.
        var list = new List<Frog>(3);
        int count = Mathf.Min(3, plots.Count);
        int first = rng.Next(plots.Count);
        int step = Mathf.Max(1, plots.Count / count);
        for (int i = 0; i < count; i++)
        {
            var plot = plots[(first + i * step) % plots.Count].transform;
            float side = rng.Next(2) == 0 ? -1f : 1f;
            var home = plot.TransformPoint(new Vector3(side * (3.4f + 2.4f * (float)rng.NextDouble()), 0f,
                                                       5.0f + 1.8f * (float)rng.NextDouble()));
            home.y = GroundAt(home, groundY);
            var frog = Instantiate(set.frog, home, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), transform);
            frog.name = $"Frog_{i}";
            frog.transform.localScale = Vector3.one * FrogScale;
            Prepare(frog);
            list.Add(new Frog
            {
                t = frog.transform,
                home = home,
                hopFrom = home,
                hopTo = home,
                hopStart = -1f,
                nextHop = 2f + 6f * (float)rng.NextDouble(),
                active = true,
            });
        }
        frogs = list.ToArray();
    }

    /// <summary>The ground under <paramref name="at"/>: whatever a ray down finds (the scene's terrain, a yard), else <paramref name="fallback"/>.</summary>
    private static float GroundAt(Vector3 at, float fallback)
    {
        return Physics.Raycast(new Vector3(at.x, at.y + 10f, at.z), Vector3.down, out var hit, 30f) ? hit.point.y : fallback;
    }

    // ---------- walkers ----------

    private void BuildWalkers(Bounds village, M3OutdoorLifeSet set)
    {
        if (set.walkers == null || set.walkers.Length == 0) return;
        // Loops along the verges, turning across the track beyond the end houses - 5 m in from the
        // village bounds, which is past the last yard and 5 m ahead of where the Pilot spawns. Each
        // loop wanders: its waypoints stray half a metre either way, and no two loops turn at the
        // same place, so the walkers never look like they are on rails.
        float x0 = village.min.x + 5f, x1 = village.max.x - 5f;
        var loops = new[]
        {
            Loop(x0, x1 - 3f * (float)rng.NextDouble(), false),
            Loop(x0 + 4f * (float)rng.NextDouble(), x1, true),
            Loop(Mathf.Lerp(x0, x1, 0.3f + 0.15f * (float)rng.NextDouble()), Mathf.Lerp(x0, x1, 0.7f + 0.2f * (float)rng.NextDouble()), false),
        };
        var list = new List<Walker>(loops.Length);
        for (int i = 0; i < loops.Length; i++)
        {
            var prefab = set.walkers[i % set.walkers.Length];
            if (prefab == null) continue;
            var path = loops[i];
            // Start part-way along the first leg, so they are not all setting out from a corner.
            int leg = rng.Next(path.Length);
            var start = Vector3.Lerp(path[leg], path[(leg + 1) % path.Length], 0.2f + 0.6f * (float)rng.NextDouble());
            var body = Instantiate(prefab, start, Quaternion.LookRotation(path[(leg + 1) % path.Length] - start, Vector3.up), transform);
            body.name = $"Walker_{i}";
            StripResident(body);
            var animator = body.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.cullingMode = AnimatorCullingMode.CullCompletely;
                animator.applyRootMotion = false;   // this Update places the body; the clip must not nudge it
                // Reach() switches the whole body off beyond the walker radius. Without this the Animator
                // comes back in its default state with default parameters (isWalk false), while w.walking
                // still says true and SetBool is never re-sent: the walker would slide along in an idle pose.
                animator.keepAnimatorStateOnDisable = true;
            }
            list.Add(new Walker
            {
                t = body.transform,
                animator = animator,
                path = path,
                next = (leg + 1) % path.Length,
                speed = 0.75f + 0.35f * (float)rng.NextDouble(),
                active = true,
            });
        }
        walkers = list.ToArray();
    }

    /// <summary>
    /// A wandering loop from <paramref name="xa"/> to <paramref name="xb"/> along the near verge, across
    /// the track, and back along the far verge, one waypoint every 8-13 m with its z strayed up to
    /// half a metre either way (never onto the track side: the verges are 0.6 m from the yard jars).
    /// The verges follow the lane as it winds, so each waypoint is set from the lane's z at its own x.
    /// </summary>
    private Vector3[] Loop(float xa, float xb, bool reversed)
    {
        var points = new List<Vector3>();
        for (float x = xa; x < xb; x += 8f + 5f * (float)rng.NextDouble())
            points.Add(new Vector3(x, 0f, VillageLayout.LaneZ(x) + NearEdge + 0.5f * (float)rng.NextDouble()));
        points.Add(new Vector3(xb, 0f, VillageLayout.LaneZ(xb) + NearEdge + 0.5f * (float)rng.NextDouble()));
        for (float x = xb; x > xa; x -= 8f + 5f * (float)rng.NextDouble())
            points.Add(new Vector3(x, 0f, VillageLayout.LaneZ(x) + FarEdge - 0.5f * (float)rng.NextDouble()));
        points.Add(new Vector3(xa, 0f, VillageLayout.LaneZ(xa) + FarEdge - 0.5f * (float)rng.NextDouble()));
        if (reversed) points.Reverse();
        return points.ToArray();
    }

    /// <summary>
    /// A villager prefab is a resident: it carries a talk collider, and the child and elder bodies
    /// carry VillagerView, whose OnEnable reads person 0's condition and dresses the body in it.
    /// PF_CharacterV1 carries Module 1's Human walker and three stacked bodies (see
    /// M3NeighbourhoodBuilder.ShowOneBody). None of that belongs on a passer-by. Removed at once,
    /// while the parent is still inactive, so nothing has a chance to run first.
    /// </summary>
    private static void StripResident(GameObject body)
    {
        foreach (var c in body.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (var v in body.GetComponentsInChildren<VillagerView>(true)) DestroyImmediate(v);
        foreach (var h in body.GetComponentsInChildren<Human>(true)) DestroyImmediate(h);

        // Of several bodies keep the first that is rigged (only Roger is, in PF_CharacterV1).
        var bodies = body.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (bodies.Length < 2) return;
        int keep = 0;
        for (int i = 0; i < bodies.Length; i++)
        {
            if (HasAllBones(bodies[i])) { keep = i; break; }
        }
        for (int i = 0; i < bodies.Length; i++) bodies[i].gameObject.SetActive(i == keep);
    }

    private static bool HasAllBones(SkinnedMeshRenderer body)
    {
        var bones = body.bones;
        if (bones == null || bones.Length == 0) return false;
        foreach (var b in bones) if (b == null) return false;
        return true;
    }

    /// <summary>
    /// An animal: no collider and its animation culled when unseen. It is NOT put on M3DistanceCulling's
    /// layer: that layer stops drawing at 22 m, so a 30 m animal radius did nothing and the animals
    /// popped in at 22 m. Reach() switches an animal off beyond AnimalRadius instead.
    /// </summary>
    private static void Prepare(GameObject animal)
    {
        foreach (var c in animal.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (var a in animal.GetComponentsInChildren<Animator>(true))
        {
            a.cullingMode = AnimatorCullingMode.CullCompletely;
            a.applyRootMotion = false;
            a.keepAnimatorStateOnDisable = true;
        }
    }

    // ---------- per frame ----------

    private void Update()
    {
        float now = Time.time, dt = Time.deltaTime;

        // The XR rig, and with it the main camera, arrives after the scene starts; look again now and then.
        if (eye == null || (Time.frameCount & 31) == 0) eye = Camera.main;
        bool haveEye = eye != null;
        var eyeAt = haveEye ? eye.transform.position : Vector3.zero;

        bool held = M3Session.Instance != null && M3Session.Instance.ClockHeld;

        for (int i = 0; i < dragonflies.Length; i++)
        {
            ref var d = ref dragonflies[i];
            if (!Reach(ref d.active, d.centre, eyeAt, haveEye, AnimalRadius, d.t.gameObject)) continue;
            float t = now * d.speed + d.phase;
            float sx = Mathf.Sin(t), cx = Mathf.Cos(t);
            float x = d.centre.x + d.ax * sx;
            float z = d.centre.z + d.az * (d.figureEight ? Mathf.Sin(2f * t) : cx);
            float y = d.height + 0.25f * Mathf.Sin(t * 1.7f + d.phase);
            // Heading from the path's own derivative, so it always flies nose first.
            float vx = d.ax * cx;
            float vz = d.az * (d.figureEight ? 2f * Mathf.Cos(2f * t) : -sx);
            var at = new Vector3(x, y, z);
            if (haveEye)
            {
                // The lane paths pass through head height: slide round the Pilot's head rather than through it.
                var fromHead = at - eyeAt;
                float m = fromHead.magnitude;
                if (m < FlyClearance && m > 1e-3f) at = eyeAt + fromHead / m * FlyClearance;
            }
            d.t.SetPositionAndRotation(at, Quaternion.LookRotation(new Vector3(vx, 0f, vz), Vector3.up));
        }

        for (int i = 0; i < frogs.Length; i++)
        {
            ref var f = ref frogs[i];
            if (!Reach(ref f.active, f.home, eyeAt, haveEye, AnimalRadius, f.t.gameObject)) continue;
            if (f.hopStart < 0f)
            {
                if (now < f.nextHop) continue;
                // A short hop to somewhere within a metre of home, turning to face it first.
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = 0.3f + 0.6f * (float)rng.NextDouble();
                f.hopFrom = f.t.position;
                f.hopTo = f.home + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                f.hopStart = now;
                var heading = f.hopTo - f.hopFrom;
                heading.y = 0f;
                if (heading.sqrMagnitude > 1e-4f) f.t.rotation = Quaternion.LookRotation(heading, Vector3.up);
                continue;
            }
            float p = (now - f.hopStart) / 0.45f;
            if (p >= 1f)
            {
                f.t.position = f.hopTo;
                f.hopStart = -1f;
                f.nextHop = now + 3f + 7f * (float)rng.NextDouble();
                continue;
            }
            f.t.position = Vector3.Lerp(f.hopFrom, f.hopTo, p) + Vector3.up * (0.5f * p * (1f - p));
        }

        for (int i = 0; i < walkers.Length; i++)
        {
            ref var w = ref walkers[i];
            var at = w.t.position;
            if (!Reach(ref w.active, at, eyeAt, haveEye, WalkerRadius, w.t.gameObject)) continue;

            // Stand still while the headset is being handed over, and when the Pilot is right here.
            if (haveEye)
            {
                float dx = at.x - eyeAt.x, dz = at.z - eyeAt.z;
                float clear = w.waiting ? PilotClearance + 0.8f : PilotClearance;
                w.waiting = dx * dx + dz * dz < clear * clear;
            }
            bool walking = !held && !w.waiting;
            if (walking != w.walking)
            {
                w.walking = walking;
                if (w.animator != null) w.animator.SetBool(WalkId, walking);
            }
            if (!walking)
            {
                // Waiting is only a stop, and there are no colliders: a Pilot who walks on up to a waiting
                // walker would go through them. Inside MakeWay they step back, keeping their heading.
                if (w.waiting && haveEye)
                {
                    var away = new Vector3(at.x - eyeAt.x, 0f, at.z - eyeAt.z);
                    float m = away.magnitude;
                    if (m < MakeWay) w.t.position = at + (m > 1e-3f ? away / m : w.t.forward) * (1.2f * dt);
                }
                continue;
            }

            var target = w.path[w.next];
            var toTarget = target - at;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.05f)
            {
                w.next = (w.next + 1) % w.path.Length;
                continue;
            }
            w.t.rotation = Quaternion.RotateTowards(w.t.rotation, Quaternion.LookRotation(toTarget, Vector3.up), 200f * dt);
            w.t.position = Vector3.MoveTowards(at, target, w.speed * dt);
        }
    }

    /// <summary>
    /// Switches <paramref name="go"/> off beyond <paramref name="radius"/> from the camera and on again
    /// a little nearer, so nothing flickers at the edge. True when it is on and worth moving.
    /// </summary>
    private static bool Reach(ref bool active, Vector3 at, Vector3 eyeAt, bool haveEye, float radius, GameObject go)
    {
        if (!haveEye) return active;
        float dx = at.x - eyeAt.x, dz = at.z - eyeAt.z;
        float d2 = dx * dx + dz * dz;
        float limit = active ? radius + Hysteresis : radius;
        bool near = d2 < limit * limit;
        if (near != active)
        {
            active = near;
            go.SetActive(near);
        }
        return active;
    }
}
