using System.Collections.Generic;
using Aedes.Module3.Sim;
using UnityEngine;

/// <summary>
/// The countryside around the Module 3 neighbourhood: sky, grass, rice paddies, a tree line and
/// hills on the horizon.
///
/// Before this the village stood on a grey plane under Unity's default gradient sky, so from
/// inside the lanes the horizon was an empty white-blue band. Like the neighbourhood itself it is
/// built at runtime from a seed rather than dressed by hand, so the scene file stays small and
/// the surroundings always fit whatever layout the builder produced.
///
/// Scenery only: no colliders, nothing the model or the player interacts with.
/// </summary>
public class M3Surroundings : MonoBehaviour
{
    [Header("Art")]
    [SerializeField] private Material skybox;
    [Tooltip("Palms and banana plants, placed close to the houses.")]
    [SerializeField] private GameObject[] nearTrees;
    [Tooltip("Broad trees and bushes for the tree line further out.")]
    [SerializeField] private GameObject[] farTrees;

    [Header("Layout")]
    [SerializeField] private int seed = 11;
    [SerializeField] private float hillRadius = 330f;
    [SerializeField] private float farClip = 520f;
    [Tooltip("Fences, power poles, washing lines, lean-tos, trees between the houses and hens (M3VillageDressing).")]
    [SerializeField] private bool villageDressing = true;

    [Header("Colours")]
    [SerializeField] private Color grass = new Color(0.36f, 0.55f, 0.24f);
    [SerializeField] private Color track = new Color(0.55f, 0.45f, 0.32f);
    [SerializeField] private Color dyke = new Color(0.47f, 0.40f, 0.27f);
    [SerializeField] private Color paddyGreen = new Color(0.42f, 0.70f, 0.26f);
    [SerializeField] private Color paddyWater = new Color(0.40f, 0.52f, 0.46f);
    [SerializeField] private Color hills = new Color(0.30f, 0.44f, 0.36f);
    [SerializeField] private Color haze = new Color(0.74f, 0.82f, 0.88f);

    [Header("Ground pattern")]
    [Tooltip("Metres per texel of the painted ground over the village. Smaller is crisper and costs texture "
             + "memory and bandwidth on the headset: 0.06 gave a 2600 x 1150 image (16 MB with its mipmaps), "
             + "0.08 gives 1970 x 870 (9 MB) and from eye height the difference does not show.")]
    [SerializeField] private float groundTexel = 0.08f;
    [Tooltip("How strongly the grass mottles and the track is marked. 0 is the old flat colour.")]
    [Range(0f, 1.5f)]
    [SerializeField] private float groundContrast = 1f;

    /// <summary>Metres one repeat of the far-fields grass tile covers.</summary>
    private const float FieldTile = 32f;
    /// <summary>How far the painted ground reaches past the houses along the lane, and across it (up to the paddies).</summary>
    private const float PaintMarginAlong = 36f, PaintMarginAcross = 14f;
    /// <summary>Width of the dirt track down the lane, before it wanders.</summary>
    private const float TrackWidth = 3.2f;
    /// <summary>
    /// The Lao house as modelled, measured along its plot's axes (z toward the lane) at scale 1 and
    /// multiplied by the builder's house scale: its side walls, its front wall, and where the side
    /// door opens in the +x wall - the one a visitor comes in by, from the gap between houses.
    /// </summary>
    private const float ModelMinX = -4.16f, ModelMaxX = 4.39f, ModelFrontZ = 3.4f, ModelDoorZ = -0.89f;
    /// <summary>Metres out from the side wall that the worn path down the gap runs.</summary>
    private const float PathOffset = 1f;

    private System.Random rng;
    private Transform root;
    private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
    private readonly List<Texture2D> paintedTextures = new List<Texture2D>();

    /// <summary>
    /// Called by M3NeighbourhoodBuilder once the plots stand; <paramref name="village"/> covers the
    /// houses. The plots themselves (whose yards get worn) are read from the builder on this object.
    /// </summary>
    public void Build(Bounds village)
    {
        // Not the builder's static HouseholdViews: that is only set in Awake, so it is empty when the
        // neighbourhood is built in edit mode (the scene smoke test).
        var builder = GetComponent<M3NeighbourhoodBuilder>();
        IReadOnlyList<HouseholdView> plots = builder != null ? builder.Households : M3NeighbourhoodBuilder.HouseholdViews;
        if (root != null) Destroy(root.gameObject);
        ReleaseTextures();
        root = new GameObject("Surroundings (runtime)").transform;
        root.SetParent(transform, false);
        rng = new System.Random(seed);

        BuildSky();
        BuildGround(village, plots);
        BuildPaddies(village);
        BuildTrees(village);
        BuildHills(village.center);
        M3WorldBounds.Build(root, village);   // walking is the only way to move: keep the Pilot on the ground
        // What makes the lane a village: fences, poles and wires, washing, lean-tos, trees between the
        // houses, hens. Before the outdoor life, so the trees it plants get the wind too.
        if (villageDressing) Debug.Log("Module 3: " + M3VillageDressing.Build(root, village, plots, seed, nearTrees, farTrees));
        // Wind in the trees, drifting clouds, animals and a few walkers: decoration, from the same seed.
        M3OutdoorLife.Build(root, village, plots, seed);

        // The houses are part of the look too: give them their surface texture now that they stand.
        M3HouseSurface.ApplyToPlots(plots);

        // Last, once every material is final: the broad trees and bushes of the ring become static batches.
        BatchStaticTrees();
    }

    /// <summary>
    /// Static-batches the ring's broad trees and bushes: two hundred small identical meshes that never
    /// move, drawn in a handful of calls instead of one each. The palms and bananas are left out - their
    /// wind is computed from each plant's own origin (M3FoliageSway.hlsl), which a static batch, whose
    /// vertices are baked into world space, would lose; they are cheap already, merged to one renderer
    /// each by M3CombinedPrefab. Needs the models readable (Read/Write on their import), or a build
    /// cannot combine them and they are simply left as they are.
    /// </summary>
    private void BatchStaticTrees()
    {
        var batch = new List<GameObject>();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
        {
            if (!r.name.StartsWith("PF_Tree") && !r.name.StartsWith("PF_Bush")) continue;
            if (!r.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
            bool sways = false;
            foreach (var m in r.sharedMaterials)
                if (m != null && m.shader != null && m.shader.name == "AEDES/Module 3/Foliage Lit") sways = true;
            if (sways) continue;
            batch.Add(r.gameObject);
        }
        if (batch.Count > 1) StaticBatchingUtility.Combine(batch.ToArray(), root.gameObject);
    }

    private void BuildSky()
    {
        if (skybox != null)
        {
            RenderSettings.skybox = skybox;
            DynamicGI.UpdateEnvironment();
        }
        // The default reflection cube is baked from the sky once, here, in its morning-blue state, and never
        // again (M3TimeOfDay warms the sky and the light through the turn but calling UpdateEnvironment for
        // that costs a frame). The houses' smoothness of 0.5 reflects it, so glancing wall and floor surfaces
        // kept a cool blue sheen however warm the room was. Turned down, the reflection is a hint of sky
        // rather than a tint.
        RenderSettings.reflectionIntensity = 0.4f;
        // A little haze so the hills and the far tree line recede instead of ending in a hard edge.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = haze;
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = hillRadius * 1.9f;

        var cam = Camera.main;
        if (cam != null) cam.farClipPlane = Mathf.Max(cam.farClipPlane, farClip);
    }

    private void BuildGround(Bounds village, IReadOnlyList<HouseholdView> plots)
    {
        // Grass to the horizon: a small seamless tile repeated, since out there the repeats vanish into the haze.
        float fieldSize = hillRadius * 2.2f;
        var fields = Quad("Fields", village.center + Vector3.down * 0.03f, new Vector2(fieldSize, fieldSize), grass);
        fields.GetComponent<Renderer>().sharedMaterial = PaintedMat(
            M3GroundPainter.PaintTile(256, grass, groundContrast, seed), Vector2.one * (fieldSize / FieldTile));

        // Over the village and its lane, one image that never repeats, with the dirt track between the
        // two rows of houses painted into the grass, and the worn yard and side-door path of every house.
        // The scene's own grey ground plane sits at the same height, so its renderer is switched off
        // (its collider is still what the player stands on and teleports to) rather than left to fight
        // the painted quad for the pixels.
        var area = new Rect(village.min.x - PaintMarginAlong, village.min.z - PaintMarginAcross,
                            village.size.x + PaintMarginAlong * 2f, village.size.z + PaintMarginAcross * 2f);
        var frames = new M3GroundPainter.PlotFrame[plots != null ? plots.Count : 0];
        for (int i = 0; i < frames.Length; i++)
        {
            var at = plots[i].transform.position;
            frames[i] = M3GroundPainter.FrameOf(plots[i].transform, VillageLayout.DistanceToLane(at.x, at.z));
        }
        float houseScale = M3NeighbourhoodBuilder.HouseScale;
        var yard = new M3GroundPainter.YardLayout
        {
            minX = ModelMinX * houseScale,
            maxX = ModelMaxX * houseScale,
            frontZ = ModelFrontZ * houseScale,
            doorZ = ModelDoorZ * houseScale,
            pathX = ModelMaxX * houseScale + PathOffset,
        };
        var painted = Quad("Ground (painted)", new Vector3(area.center.x, 0f, area.center.y), area.size, grass);
        painted.GetComponent<Renderer>().sharedMaterial = PaintedMat(
            M3GroundPainter.PaintVillage(area, groundTexel, 4096, TrackWidth * 0.5f,
                                         frames, yard, grass, track, groundContrast, seed), Vector2.one);
        var sceneGround = GameObject.Find("Ground");
        if (sceneGround != null && sceneGround.TryGetComponent<Renderer>(out var groundRenderer))
            groundRenderer.enabled = false;
    }

    private void BuildPaddies(Bounds village)
    {
        // Paddies either side of the village, beyond its back yards: the landscape a Lao
        // neighbourhood actually sits in, and flat, so it costs almost nothing to draw.
        //
        // Two meshes for the lot - the dykes and the green paddies in one, the flooded ones (glossy) in
        // the other - rather than a quad each: three hundred quads were three hundred draw calls, a
        // quarter of the frame's, for six hundred triangles. Each quad takes its colour from a swatch
        // of a small palette made from the colours above, so the greens still vary field to field.
        const float cell = 13f, bank = 0.7f;
        const int Greens = 6;
        var palette = new Color[Greens + 2];
        palette[0] = dyke;
        palette[1] = paddyWater;
        for (int i = 0; i < Greens; i++)
        {
            float v = 1f + ((i / (float)(Greens - 1)) * 2f - 1f) * 0.06f;
            palette[2 + i] = new Color(paddyGreen.r * v, paddyGreen.g * v, paddyGreen.b * v);
        }
        var matte = new PaddyMesh(palette.Length);
        var water = new PaddyMesh(palette.Length);

        float x0 = village.min.x - 70f, x1 = village.max.x + 70f;
        foreach (float side in new[] { -1f, 1f })
        {
            float zStart = side > 0 ? village.max.z + 14f : village.min.z - 14f;
            for (int row = 0; row < 5; row++)
            {
                float z = zStart + side * (row * cell + cell * 0.5f);
                for (float x = x0; x < x1; x += cell)
                {
                    if (Next() < 0.12f) continue; // a few gaps read as fallow ground
                    var centre = new Vector3(x + cell * 0.5f, 0f, z);
                    matte.Quad(centre + Vector3.up * 0.01f, cell, 0);
                    bool flooded = Next() < 0.35f;
                    int green = 2 + rng.Next(Greens);
                    if (flooded) water.Quad(centre + Vector3.up * 0.02f, cell - bank, 1);
                    else matte.Quad(centre + Vector3.up * 0.02f, cell - bank, green);
                }
            }
        }

        var texture = new Texture2D(palette.Length, 1, TextureFormat.RGBA32, false, false)
        {
            name = "Paddies (palette)", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave,
        };
        texture.SetPixels(palette);
        texture.Apply(false, true);
        paintedTextures.Add(texture);
        matte.Spawn("Paddies", root, PaddyMaterial(texture, 0.05f));
        water.Spawn("Paddies (flooded)", root, PaddyMaterial(texture, 0.85f));
    }

    private Material PaddyMaterial(Texture2D palette, float smoothness)
        => new Material(Mat(Color.white, smoothness)) { mainTexture = palette, hideFlags = HideFlags.DontSave };

    /// <summary>Flat quads on the ground, each coloured by a swatch of a one-row palette, as one mesh.</summary>
    private sealed class PaddyMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();
        private readonly int swatches;

        public PaddyMesh(int swatches) => this.swatches = swatches;

        public void Quad(Vector3 centre, float size, int swatch)
        {
            float h = size * 0.5f;
            var uv = new Vector2((swatch + 0.5f) / swatches, 0.5f);
            int i = vertices.Count;
            vertices.Add(centre + new Vector3(-h, 0f, -h));
            vertices.Add(centre + new Vector3(-h, 0f, h));
            vertices.Add(centre + new Vector3(h, 0f, h));
            vertices.Add(centre + new Vector3(h, 0f, -h));
            for (int k = 0; k < 4; k++) uvs.Add(uv);
            triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        public void Spawn(string name, Transform parent, Material material)
        {
            if (triangles.Count == 0) return;
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.SetNormals(normals);
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // flat on the ground: it has no shadow to cast
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
    }

    private void BuildTrees(Bounds village)
    {
        var keepOut = village;
        keepOut.Expand(new Vector3(6f, 0f, 6f));

        // Palms and bananas close around the houses, as in the Module 1 village.
        if (nearTrees != null && nearTrees.Length > 0)
        {
            for (int i = 0; i < 46; i++)
            {
                var p = RingPoint(village, 3f, 14f);
                if (keepOut.Contains(new Vector3(p.x, keepOut.center.y, p.z))) continue;
                Place(nearTrees, p, 0.9f, 1.3f);
            }
        }

        // A thicker, broken tree line further out - the edge of the next village, or the forest.
        if (farTrees != null && farTrees.Length > 0)
        {
            for (int i = 0; i < 160; i++)
            {
                float a = (float)(Next() * Mathf.PI * 2f);
                float r = Mathf.Lerp(95f, 160f, (float)Next());
                var p = village.center + new Vector3(Mathf.Cos(a) * r * 1.2f, 0f, Mathf.Sin(a) * r);
                Place(farTrees, p, 1.4f, 2.4f);
            }
        }
    }

    /// <summary>A point in a band around the village's footprint.</summary>
    private Vector3 RingPoint(Bounds b, float inner, float outer)
    {
        float d = Mathf.Lerp(inner, outer, (float)Next());
        switch (rng.Next(4))
        {
            case 0: return new Vector3(Mathf.Lerp(b.min.x - d, b.max.x + d, (float)Next()), 0f, b.max.z + d);
            case 1: return new Vector3(Mathf.Lerp(b.min.x - d, b.max.x + d, (float)Next()), 0f, b.min.z - d);
            case 2: return new Vector3(b.min.x - d, 0f, Mathf.Lerp(b.min.z, b.max.z, (float)Next()));
            default: return new Vector3(b.max.x + d, 0f, Mathf.Lerp(b.min.z, b.max.z, (float)Next()));
        }
    }

    private void Place(GameObject[] prefabs, Vector3 at, float minScale, float maxScale)
    {
        var prefab = prefabs[rng.Next(prefabs.Length)];
        if (prefab == null) return;
        // One renderer per tree where the prefab is many (the palm is twenty): see M3CombinedPrefab.
        var tree = M3CombinedPrefab.Spawn(prefab, root);
        tree.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, (float)Next() * 360f, 0f));
        tree.transform.localScale *= Mathf.Lerp(minScale, maxScale, (float)Next());
        foreach (var c in tree.GetComponentsInChildren<Collider>())
        {
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }
    }

    /// <summary>A low ring of hills on the horizon: one mesh, a few hundred triangles.</summary>
    private void BuildHills(Vector3 centre)
    {
        const int segments = 72;
        var verts = new List<Vector3>();
        var tris = new List<int>();
        float phase1 = (float)Next() * 10f, phase2 = (float)Next() * 10f;
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            // Noise sampled round a circle, so the ring closes without a cliff where it meets.
            float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
            float h = 24f + 18f * Mathf.PerlinNoise(phase1 + cx * 1.6f, phase1 + cz * 1.6f)
                          + 22f * Mathf.Pow(Mathf.PerlinNoise(phase2 + cx * 4f, phase2 + cz * 4f), 2f);
            verts.Add(centre + dir * (hillRadius - 60f));                    // foot, near side
            verts.Add(centre + dir * hillRadius + Vector3.up * h);           // ridge
            verts.Add(centre + dir * (hillRadius + 70f) + Vector3.up * h * 0.4f); // far shoulder
            if (i == segments) break;
            int k = i * 3;
            // Wound to face the village: the player only ever sees the inner slopes.
            tris.AddRange(new[] { k, k + 3, k + 1, k + 1, k + 3, k + 4, k + 1, k + 4, k + 2, k + 2, k + 4, k + 5 });
        }
        var mesh = new Mesh { name = "Hills" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();

        var go = new GameObject("Hills");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var hillRenderer = go.AddComponent<MeshRenderer>();
        hillRenderer.sharedMaterial = Mat(hills, 0f);
        hillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 300 m off: never within the shadow distance
    }

    // ---------- helpers ----------

    private GameObject Quad(string name, Vector3 centre, Vector2 size, Color colour)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        var collider = q.GetComponent<Collider>();
        if (Application.isPlaying) Destroy(collider);
        else DestroyImmediate(collider);
        // Flat on the ground, so nothing to cast: a shadow-map pass over a 700 m quad is pure waste.
        q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        q.transform.SetParent(root, false);
        q.transform.position = centre;
        q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        q.transform.localScale = new Vector3(size.x, size.y, 1f);
        q.GetComponent<Renderer>().sharedMaterial = Mat(colour, 0.05f);
        return q;
    }

    private Material Mat(Color colour, float smoothness)
    {
        var key = new Color(colour.r, colour.g, colour.b, smoothness);
        if (materials.TryGetValue(key, out var m)) return m;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        m = new Material(shader) { color = colour };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        materials[key] = m;
        return m;
    }

    /// <summary>A matte material showing <paramref name="tex"/>. The texture is remembered so a rebuild or destroy can free it.</summary>
    private Material PaintedMat(Texture2D tex, Vector2 tiling)
    {
        paintedTextures.Add(tex);
        return new Material(Mat(Color.white, 0.05f)) { mainTexture = tex, mainTextureScale = tiling };
    }

    private void ReleaseTextures()
    {
        foreach (var t in paintedTextures)
            if (t != null) Destroy(t);
        paintedTextures.Clear();
    }

    private void OnDestroy() => ReleaseTextures();

    private Color Vary(Color c, float amount)
    {
        float v = 1f + ((float)Next() * 2f - 1f) * amount;
        return new Color(c.r * v, c.g * v, c.b * v);
    }

    private double Next() => rng.NextDouble();
}
