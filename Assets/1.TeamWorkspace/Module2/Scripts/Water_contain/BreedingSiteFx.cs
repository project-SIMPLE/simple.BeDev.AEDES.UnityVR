using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes an open breeding site look like one: a few pale wrigglers swimming at the water's surface and
/// a slow column of dark mosquitoes rising from it. It is what connects "clear the water" to "clear the
/// mosquitoes" - the swarm is sustained by the open sites, and without this nothing on screen said so.
///
/// Built entirely at runtime from primitive meshes and two small materials in Resources/Module2, so no
/// scene or prefab has to change. M2Manager attaches one to each site the round uses and calls Clear()
/// when the site is dealt with: the wrigglers shrink away and the last mosquitoes drift off.
/// </summary>
public class BreedingSiteFx : MonoBehaviour
{
    const string LarvaMaterial = "Module2/M2_SiteLarva";
    const string MoteMaterial = "Module2/M2_SiteMote";

    // ---- shared, loaded once ----
    static Material s_larvaMat, s_moteMat;
    static Mesh s_sphere, s_capsule;
    static bool s_loaded, s_failed;

    // The game's own mosquito, baked once to a static mesh, so what rises from the water is recognisably
    // the thing in the swarm. Falls back to a dark speck if the prefab cannot be found.
    static Mesh s_mosquitoMesh;
    static Material[] s_mosquitoMaterials;
    static float s_mosquitoLength;
    static bool s_mosquitoTried;

    class Larva
    {
        public Transform root, joint1, joint2;
        public Vector2 center;
        public float orbit, angle, speed, phase, wiggleRate;
    }

    class Mote
    {
        public Transform t;
        public float age, life, phase, yaw;
        public Vector2 start;
    }

    readonly List<Larva> _larvae = new List<Larva>();
    readonly List<Mote> _motes = new List<Mote>();

    Transform _larvaRoot, _moteRoot;
    Vector3 _surfaceLocal;
    Quaternion _attachRotation;
    float _radius, _scale, _moteSize, _moteScale;

    // Longest side of a rising mosquito in metres at a jar's scale; smaller containers scale it down.
    const float MoteLength = 0.16f;
    bool _clearing;
    float _clearedAt;

    /// <summary>Attach to a site (once). Does nothing if the water cannot be found or the assets are missing.</summary>
    public static BreedingSiteFx Attach(Component site)
    {
        if (site == null) return null;
        var fx = site.GetComponent<BreedingSiteFx>();
        if (fx == null) fx = site.gameObject.AddComponent<BreedingSiteFx>();
        return fx;
    }

    /// <summary>The site has been dealt with: wrigglers vanish, no new mosquitoes rise.</summary>
    public void Clear()
    {
        if (_clearing) return;
        _clearing = true;
        _clearedAt = Time.time;
    }

    void Awake()
    {
        if (!LoadShared() || !FindWater(out Bounds water))
        {
            enabled = false;
            return;
        }

        _radius = Mathf.Max(Mathf.Min(water.extents.x, water.extents.z) * 0.75f, 0.03f);
        // A vase's water is an 8 cm disc: keep the wrigglers big enough to read on it.
        _scale = Mathf.Clamp(_radius / 0.3f, 0.55f, 1.3f);
        _surfaceLocal = transform.InverseTransformPoint(new Vector3(water.center.x, water.max.y, water.center.z));
        _attachRotation = transform.rotation;

        _larvaRoot = new GameObject("[runtime] Larvae").transform;
        _moteRoot = new GameObject("[runtime] Rising mosquitoes").transform;
        Follow();

        BuildLarvae();
        BuildMotes();
    }

    void OnEnable()
    {
        if (_larvaRoot != null) _larvaRoot.gameObject.SetActive(true);
        if (_moteRoot != null) _moteRoot.gameObject.SetActive(true);
    }

    void OnDisable()
    {
        if (_larvaRoot != null) _larvaRoot.gameObject.SetActive(false);
        if (_moteRoot != null) _moteRoot.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_larvaRoot != null) Destroy(_larvaRoot.gameObject);
        if (_moteRoot != null) Destroy(_moteRoot.gameObject);
    }

    // ------------------------------------------------------------------ setup

    static bool LoadShared()
    {
        if (s_loaded) return true;
        if (s_failed) return false;

        s_larvaMat = Resources.Load<Material>(LarvaMaterial);
        s_moteMat = Resources.Load<Material>(MoteMaterial);
        if (s_larvaMat == null || s_moteMat == null)
        {
            Debug.LogWarning("[BreedingSiteFx] Materials missing under Resources/Module2 - no site effects this run.");
            s_failed = true;
            return false;
        }

        // Meshes come from a throwaway primitive so they exist in a player build exactly as they do here.
        s_sphere = PrimitiveMesh(PrimitiveType.Sphere);
        s_capsule = PrimitiveMesh(PrimitiveType.Capsule);
        s_loaded = s_sphere != null && s_capsule != null;
        s_failed = !s_loaded;
        return s_loaded;
    }

    static Mesh PrimitiveMesh(PrimitiveType type)
    {
        var temp = GameObject.CreatePrimitive(type);
        var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);
        return mesh;
    }

    static bool LoadMosquito()
    {
        if (s_mosquitoMesh != null) return true;
        if (s_mosquitoTried) return false;
        s_mosquitoTried = true;

        var spawner = FindFirstObjectByType<MosquitoSpawn>(FindObjectsInactive.Include);
        if (spawner == null || spawner.mosquitoPrefab == null) return false;

        // Bake from a throwaway instance: BakeMesh gives the pose as rendered, scale included, which the
        // skinned mesh's own bounds do not (its armature carries the scale). Needs Read/Write on the
        // mosquito FBX (Mosquito_Female.fbx), which is switched on for this.
        var temp = Instantiate(spawner.mosquitoPrefab);
        try
        {
            var skinned = temp.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinned == null) return false;

            var baked = new Mesh { name = "Mosquito (baked)" };
            skinned.BakeMesh(baked, true);
            if (baked.vertexCount == 0) return false;
            var size = baked.bounds.size;
            s_mosquitoLength = Mathf.Max(size.x, size.y, size.z);
            if (s_mosquitoLength < 0.0001f) return false;

            s_mosquitoMesh = baked;
            s_mosquitoMaterials = skinned.sharedMaterials;
            return true;
        }
        finally
        {
            DestroyImmediate(temp);
        }
    }

    // Where the water is, in world space. Each site type keeps it somewhere different.
    bool FindWater(out Bounds water)
    {
        water = default;

        var button = GetComponent<WaterButton>();
        if (button != null && button.waterPrefab != null && Combine(button.waterPrefab.GetComponentsInChildren<Renderer>(), out water))
            return true;

        // The fish bowl's own mesh is the flat disc of water.
        if (GetComponent<FishCon>() != null)
        {
            var own = GetComponent<Renderer>();
            if (own != null) { water = own.bounds; return true; }
        }

        var jar = GetComponent<Jar>();
        if (jar != null)
        {
            // The water is a flat disc among the jar's children; the lid pieces are flat too, so skip them.
            Renderer flattest = null;
            float best = 0.3f;
            foreach (var r in GetComponentsInChildren<Renderer>(false))
            {
                if (r.gameObject == gameObject || IsLidPiece(jar, r.transform)) continue;
                var b = r.bounds;
                float wide = Mathf.Max(b.size.x, b.size.z);
                if (wide < 0.05f) continue;
                float ratio = b.size.y / wide;
                if (ratio < best) { best = ratio; flattest = r; }
            }
            if (flattest != null) { water = flattest.bounds; return true; }
        }

        // Unknown site: the top of whatever it is made of.
        if (Combine(GetComponentsInChildren<Renderer>(), out var all))
        {
            water = new Bounds(new Vector3(all.center.x, all.max.y, all.center.z), new Vector3(all.size.x * 0.6f, 0f, all.size.z * 0.6f));
            return true;
        }
        return false;
    }

    static bool IsLidPiece(Jar jar, Transform t)
    {
        return (jar.top != null && t.IsChildOf(jar.top.transform))
            || (jar.topHolo != null && t.IsChildOf(jar.topHolo.transform))
            || (jar.top_grab != null && t.IsChildOf(jar.top_grab.transform));
    }

    static bool Combine(Renderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    Transform Piece(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition, Vector3 localScale)
    {
        var go = new GameObject(name);
        var t = go.transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        t.localScale = localScale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return t;
    }

    void BuildLarvae()
    {
        int count = Mathf.Clamp(Mathf.RoundToInt(2f + _radius * 18f), 2, 7);
        float head = 0.04f * _scale;
        float seg = 0.05f * _scale;

        for (int i = 0; i < count; i++)
        {
            var larva = new Larva();
            var root = new GameObject("Larva").transform;
            root.SetParent(_larvaRoot, false);
            larva.root = root;

            Piece(root, "Head", s_sphere, s_larvaMat, Vector3.zero, Vector3.one * head);

            larva.joint1 = new GameObject("Joint1").transform;
            larva.joint1.SetParent(root, false);
            larva.joint1.localPosition = new Vector3(0f, 0f, -head * 0.4f);
            Piece(larva.joint1, "Tail1", s_capsule, s_larvaMat, new Vector3(0f, 0f, -seg * 0.5f), new Vector3(head * 0.55f, seg * 0.5f, head * 0.55f))
                .localRotation = Quaternion.Euler(90f, 0f, 0f);

            larva.joint2 = new GameObject("Joint2").transform;
            larva.joint2.SetParent(larva.joint1, false);
            larva.joint2.localPosition = new Vector3(0f, 0f, -seg);
            Piece(larva.joint2, "Tail2", s_capsule, s_larvaMat, new Vector3(0f, 0f, -seg * 0.5f), new Vector3(head * 0.33f, seg * 0.5f, head * 0.33f))
                .localRotation = Quaternion.Euler(90f, 0f, 0f);

            larva.center = Random.insideUnitCircle * (_radius * 0.3f);
            larva.orbit = Random.Range(0.25f, 0.6f) * _radius;
            larva.angle = Random.value * Mathf.PI * 2f;
            float dir = Random.value < 0.5f ? -1f : 1f;
            larva.speed = dir * Random.Range(0.35f, 0.7f) / Mathf.Max(_radius * 4f, 0.4f);
            larva.phase = Random.value * 10f;
            larva.wiggleRate = Random.Range(6f, 9f);
            _larvae.Add(larva);
        }
    }

    void BuildMotes()
    {
        int count = Mathf.Clamp(Mathf.RoundToInt(3f + _radius * 12f), 3, 6);
        bool real = LoadMosquito();
        _moteSize = real ? MoteLength * _scale : 0.05f * _scale;
        _moteScale = real ? _moteSize / s_mosquitoLength : 1f;

        for (int i = 0; i < count; i++)
        {
            var mote = new Mote();
            if (real)
            {
                var go = new GameObject("Mosquito");
                mote.t = go.transform;
                mote.t.SetParent(_moteRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = s_mosquitoMesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = s_mosquitoMaterials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            else
            {
                mote.t = Piece(_moteRoot, "Mosquito", s_sphere, s_moteMat, Vector3.zero, new Vector3(_moteSize, _moteSize * 0.6f, _moteSize * 0.6f));
            }
            mote.yaw = Random.value * 360f;
            mote.life = Random.Range(3.2f, 4.6f);
            mote.age = Random.value * mote.life;   // staggered, so the column is already full when the round starts
            mote.phase = Random.value * 10f;
            mote.start = Random.insideUnitCircle * (_radius * 0.6f);
            _motes.Add(mote);
        }
    }

    // ------------------------------------------------------------------ per frame

    void Follow()
    {
        Vector3 surface = transform.TransformPoint(_surfaceLocal);
        // Larvae keep to the water's plane and tilt with the container; the mosquitoes rise in world up.
        _larvaRoot.SetPositionAndRotation(surface, transform.rotation * Quaternion.Inverse(_attachRotation));
        _moteRoot.position = surface;
    }

    void Update()
    {
        Follow();
        float now = Time.time;
        float dt = Time.deltaTime;

        float clearT = _clearing ? Mathf.Clamp01((now - _clearedAt) / 0.5f) : 0f;
        float larvaScale = 1f - clearT * clearT * (3f - 2f * clearT);
        _larvaRoot.gameObject.SetActive(larvaScale > 0.001f);

        if (larvaScale > 0.001f)
            for (int i = 0; i < _larvae.Count; i++) UpdateLarva(_larvae[i], now, dt, larvaScale);

        bool anyMote = false;
        for (int i = 0; i < _motes.Count; i++) anyMote |= UpdateMote(_motes[i], now, dt);

        // Once cleared and everything has faded, the whole effect goes.
        if (_clearing && larvaScale <= 0.001f && !anyMote) Destroy(this);
    }

    void UpdateLarva(Larva l, float now, float dt, float scale)
    {
        // A little speed pulse makes the swimming stop-and-go rather than a smooth orbit.
        l.angle += l.speed * (1f + 0.6f * Mathf.Sin(now * 2.3f + l.phase)) * dt;
        float c = Mathf.Cos(l.angle), s = Mathf.Sin(l.angle);
        float lift = 0.004f + 0.006f * (0.5f + 0.5f * Mathf.Sin(now * 1.7f + l.phase * 1.3f));

        l.root.localPosition = new Vector3(l.center.x + l.orbit * c, lift * _scale, l.center.y + l.orbit * s);
        float sign = Mathf.Sign(l.speed);
        l.root.localRotation = Quaternion.LookRotation(new Vector3(-s * sign, 0f, c * sign));
        l.root.localScale = Vector3.one * scale;

        float w = now * l.wiggleRate + l.phase;
        l.joint1.localRotation = Quaternion.Euler(0f, Mathf.Sin(w) * 28f, 0f);
        l.joint2.localRotation = Quaternion.Euler(0f, Mathf.Sin(w - 1.1f) * 40f, 0f);
    }

    // Returns true while the mosquito is still visible.
    bool UpdateMote(Mote m, float now, float dt)
    {
        m.age += dt;
        if (m.age >= m.life)
        {
            if (_clearing) { m.t.gameObject.SetActive(false); return false; }
            m.age -= m.life;
            m.start = Random.insideUnitCircle * (_radius * 0.6f);
            m.life = Random.Range(3.2f, 4.6f);
        }
        if (!m.t.gameObject.activeSelf) m.t.gameObject.SetActive(true);

        float u = m.age / m.life;
        float rise = 1.1f * Mathf.Max(_scale, 0.7f) * u;
        float sway = 0.07f * _scale;
        m.t.localPosition = new Vector3(
            m.start.x + Mathf.Sin(now * 2.1f + m.phase) * sway,
            0.06f + rise,
            m.start.y + Mathf.Cos(now * 1.6f + m.phase) * sway);

        // Fade in over the water, out at the top.
        float fade = Mathf.SmoothStep(0f, 1f, u / 0.12f) * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.75f) / 0.25f));
        float flutter = Mathf.Sin(now * 14f + m.phase) * 10f;
        m.t.localRotation = Quaternion.Euler(0f, m.yaw, flutter);
        if (s_mosquitoMesh != null) m.t.localScale = Vector3.one * (_moteScale * fade);
        else m.t.localScale = new Vector3(_moteSize, _moteSize * 0.6f, _moteSize * 0.6f) * fade;
        return true;
    }
}
