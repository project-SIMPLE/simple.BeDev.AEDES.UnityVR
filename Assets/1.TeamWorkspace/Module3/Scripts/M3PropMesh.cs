using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the small props of the Module 3 village - fences, poles, laundry, a woodpile, a chicken -
/// as meshes, in code, at runtime, all drawn through ONE material.
///
/// Why not model them: every prop built this way is a few dozen lines of boxes and cylinders, needs
/// no FBX, no .meta, no prefab, no Resources set and no Blender run, and can be re-shaped from a
/// number in a script. And because the meshes are made here, a whole yard's worth of them is
/// appended into one mesh and drawn in one call, which a set of prefab instances could not be
/// (their meshes are not readable, so they cannot be combined at runtime). On a Quest, draw calls
/// are the budget that runs out first.
///
/// Colour comes the way the rest of Team Assets gets it: every part's UVs are parked on one swatch
/// of a palette texture, so one material serves every prop. The palette is an 8 x 8 texture made in
/// memory (<see cref="Material"/>), point-filtered so a swatch centre samples exactly its colour.
/// Boxes are flat-shaded (their own normals per face); round parts are smooth.
/// </summary>
public sealed class M3PropMesh
{
    // ---------- the palette ----------

    /// <summary>The swatches, by index. Append only: nothing else remembers a colour by name.</summary>
    public static class Swatch
    {
        public const int Wood = 0, WoodDark = 1, Bamboo = 2, BambooDark = 3, Thatch = 4, Concrete = 5,
                         Pole = 6, Rust = 7, ClothIndigo = 8, ClothMaroon = 9, ClothCream = 10, ClothOlive = 11,
                         ClothTeal = 12, ClothRose = 13, HenWhite = 14, HenBrown = 15, HenBlack = 16, Comb = 17,
                         Beak = 18, Leg = 19, SandalBlue = 20, SandalRose = 21, Gold = 22, ShrineRed = 23,
                         Basket = 24, Dark = 25, LogEnd = 26, Straw = 27, Tarp = 28, Sack = 29, Tin = 30, Leaf = 31;
    }

    /// <summary>sRGB, in swatch order. The village's own browns, creams and a few textile colours.</summary>
    private static readonly Color[] Colours =
    {
        new Color(0.45f, 0.36f, 0.26f), new Color(0.33f, 0.25f, 0.17f), new Color(0.76f, 0.66f, 0.40f), new Color(0.55f, 0.47f, 0.28f),
        new Color(0.62f, 0.50f, 0.28f), new Color(0.62f, 0.61f, 0.58f), new Color(0.52f, 0.50f, 0.47f), new Color(0.45f, 0.30f, 0.20f),
        new Color(0.22f, 0.28f, 0.48f), new Color(0.52f, 0.20f, 0.22f), new Color(0.90f, 0.86f, 0.76f), new Color(0.45f, 0.52f, 0.30f),
        new Color(0.30f, 0.52f, 0.52f), new Color(0.80f, 0.50f, 0.55f), new Color(0.93f, 0.91f, 0.86f), new Color(0.55f, 0.35f, 0.20f),
        new Color(0.16f, 0.14f, 0.13f), new Color(0.80f, 0.18f, 0.15f), new Color(0.90f, 0.70f, 0.30f), new Color(0.85f, 0.70f, 0.40f),
        new Color(0.20f, 0.35f, 0.60f), new Color(0.80f, 0.45f, 0.50f), new Color(0.85f, 0.68f, 0.30f), new Color(0.60f, 0.16f, 0.14f),
        new Color(0.72f, 0.58f, 0.34f), new Color(0.13f, 0.13f, 0.14f), new Color(0.78f, 0.66f, 0.46f), new Color(0.80f, 0.70f, 0.42f),
        new Color(0.26f, 0.42f, 0.60f), new Color(0.82f, 0.78f, 0.66f), new Color(0.58f, 0.60f, 0.62f), new Color(0.36f, 0.55f, 0.24f),
    };

    private const int Grid = 8;
    private static Material material;
    private static Texture2D palette;

    /// <summary>The one material every prop is drawn with: URP Lit over the palette, matte.</summary>
    public static Material Material
    {
        get
        {
            if (material != null) return material;
            palette = new Texture2D(Grid, Grid, TextureFormat.RGBA32, false, false)
            {
                name = "M3 prop palette",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,   // made in memory; never something to save into a scene
            };
            var pixels = new Color32[Grid * Grid];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = i < Colours.Length ? (Color32)Colours[i] : new Color32(255, 0, 255, 255);
            palette.SetPixels32(pixels);
            palette.Apply(false, true);

            // URP/Lit, like every flat colour Module 3 draws (the toon graph ignores its base colour and draws black).
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "M3 props", mainTexture = palette, hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", palette);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
            return material;
        }
    }

    /// <summary>The UV at the centre of swatch <paramref name="index"/>.</summary>
    public static Vector2 Uv(int index)
        => new Vector2((index % Grid + 0.5f) / Grid, (index / Grid + 0.5f) / Grid);

    // ---------- the builder ----------

    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Vector3> normals = new List<Vector3>();
    private readonly List<Vector2> uvs = new List<Vector2>();
    private readonly List<int> triangles = new List<int>();

    public int TriangleCount => triangles.Count / 3;
    public int VertexCount => vertices.Count;

    /// <summary>An axis-aligned box of <paramref name="size"/> about <paramref name="centre"/>, turned by <paramref name="rotation"/>.</summary>
    public void Box(Vector3 centre, Vector3 size, Quaternion rotation, int swatch)
    {
        var h = size * 0.5f;
        var uv = Uv(swatch);
        for (int axis = 0; axis < 3; axis++)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 n = Vector3.zero, u = Vector3.zero, v = Vector3.zero;
                n[axis] = sign;
                u[(axis + 1) % 3] = 1f;
                v[(axis + 2) % 3] = 1f;
                if (sign < 0) (u, v) = (v, u);   // keep the winding outward
                var c = Vector3.Scale(n, h);
                var du = Vector3.Scale(u, h);
                var dv = Vector3.Scale(v, h);
                int i0 = vertices.Count;
                var wn = rotation * n;
                Add(centre + rotation * (c - du - dv), wn, uv);
                Add(centre + rotation * (c + du - dv), wn, uv);
                Add(centre + rotation * (c + du + dv), wn, uv);
                Add(centre + rotation * (c - du + dv), wn, uv);
                Face(i0, i0 + 1, i0 + 2, i0 + 3);
            }
        }
    }

    public void Box(Vector3 centre, Vector3 size, int swatch) => Box(centre, size, Quaternion.identity, swatch);

    /// <summary>A (tapered) cylinder from <paramref name="from"/> to <paramref name="to"/>, smooth-sided, with flat caps.</summary>
    public void Cylinder(Vector3 from, Vector3 to, float radiusFrom, float radiusTo, int segments, int swatch, bool caps = true)
    {
        var axis = to - from;
        float length = axis.magnitude;
        if (length < 1e-5f) return;
        axis /= length;
        var side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        var up = Vector3.Cross(side, axis);
        var uv = Uv(swatch);
        float slope = (radiusFrom - radiusTo) / length;

        int ring0 = vertices.Count;
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            var dir = side * Mathf.Cos(a) + up * Mathf.Sin(a);
            var n = (dir + axis * slope).normalized;
            Add(from + dir * radiusFrom, n, uv);
            Add(to + dir * radiusTo, n, uv);
        }
        for (int i = 0; i < segments; i++)
        {
            int a = ring0 + i * 2;
            Face(a, a + 1, a + 3, a + 2);
        }
        if (!caps) return;
        Disc(from, -axis, side, up, radiusFrom, segments, uv);
        Disc(to, axis, side, up, radiusTo, segments, uv);
    }

    private void Disc(Vector3 centre, Vector3 normal, Vector3 side, Vector3 up, float radius, int segments, Vector2 uv)
    {
        if (radius <= 0f) return;
        int c = vertices.Count;
        Add(centre, normal, uv);
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Add(centre + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius, normal, uv);
        }
        bool flip = Vector3.Dot(Vector3.Cross(side, up), normal) < 0f;
        for (int i = 0; i < segments; i++)
        {
            int a = c + 1 + i, b = c + 1 + (i + 1) % segments;
            if (flip) triangles.AddRange(new[] { c, b, a });
            else triangles.AddRange(new[] { c, a, b });
        }
    }

    /// <summary>An ellipsoid of <paramref name="radii"/> about <paramref name="centre"/>, turned by <paramref name="rotation"/>.</summary>
    public void Ellipsoid(Vector3 centre, Vector3 radii, Quaternion rotation, int segments, int rings, int swatch)
    {
        var uv = Uv(swatch);
        int start = vertices.Count;
        for (int r = 0; r <= rings; r++)
        {
            float phi = (r / (float)rings - 0.5f) * Mathf.PI;
            float y = Mathf.Sin(phi), c = Mathf.Cos(phi);
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                var unit = new Vector3(c * Mathf.Cos(a), y, c * Mathf.Sin(a));
                var n = new Vector3(unit.x / radii.x, unit.y / radii.y, unit.z / radii.z).normalized;
                Add(centre + rotation * Vector3.Scale(unit, radii), rotation * n, uv);
            }
        }
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int a = start + r * (segments + 1) + s;
                int b = a + segments + 1;
                if (r > 0) triangles.AddRange(new[] { a, b, a + 1 });
                if (r < rings - 1) triangles.AddRange(new[] { a + 1, b, b + 1 });
            }
        }
    }

    public void Ellipsoid(Vector3 centre, Vector3 radii, int segments, int rings, int swatch)
        => Ellipsoid(centre, radii, Quaternion.identity, segments, rings, swatch);

    /// <summary>A quad a-b-c-d, its front where (b - a) x (d - a) points (Unity's clockwise winding), optionally drawn from both sides.</summary>
    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int swatch, bool twoSided = false)
    {
        var uv = Uv(swatch);
        var n = Vector3.Cross(b - a, d - a).normalized;
        int i0 = vertices.Count;
        Add(a, n, uv); Add(b, n, uv); Add(c, n, uv); Add(d, n, uv);
        Face(i0, i0 + 1, i0 + 2, i0 + 3);
        if (!twoSided) return;
        int j0 = vertices.Count;
        Add(a, -n, uv); Add(d, -n, uv); Add(c, -n, uv); Add(b, -n, uv);
        Face(j0, j0 + 1, j0 + 2, j0 + 3);
    }

    /// <summary>One triangle a-b-c, its front where (b - a) x (c - a) points.</summary>
    public void Triangle(Vector3 a, Vector3 b, Vector3 c, int swatch)
    {
        var uv = Uv(swatch);
        var n = Vector3.Cross(b - a, c - a).normalized;
        int i0 = vertices.Count;
        Add(a, n, uv); Add(b, n, uv); Add(c, n, uv);
        triangles.AddRange(new[] { i0, i0 + 1, i0 + 2 });
    }

    /// <summary>A thin slab standing on the x/z plane: a fence slat, a cloth on a line. Width along <paramref name="across"/>, height up.</summary>
    public void Slat(Vector3 bottomCentre, Vector3 across, float width, float height, float thickness, int swatch)
    {
        var right = across.normalized * (width * 0.5f);
        var fwd = Vector3.Cross(Vector3.up, across).normalized * (thickness * 0.5f);
        var up = Vector3.up * height;
        var a = bottomCentre - right;
        var b = bottomCentre + right;
        Quad(b + fwd, a + fwd, a + fwd + up, b + fwd + up, swatch);   // the +fwd face
        Quad(a - fwd, b - fwd, b - fwd + up, a - fwd + up, swatch);   // the -fwd face
        if (thickness > 0.004f)
        {
            Quad(b - fwd, b + fwd, b + fwd + up, b - fwd + up, swatch);   // the b end
            Quad(a + fwd, a - fwd, a - fwd + up, a + fwd + up, swatch);   // the a end
            Quad(a - fwd + up, b - fwd + up, b + fwd + up, a + fwd + up, swatch);   // the top
        }
    }

    /// <summary>Appends <paramref name="other"/>, every vertex taken through <paramref name="transform"/>.</summary>
    public void Append(M3PropMesh other, Matrix4x4 transform)
    {
        int offset = vertices.Count;
        for (int i = 0; i < other.vertices.Count; i++)
        {
            vertices.Add(transform.MultiplyPoint3x4(other.vertices[i]));
            normals.Add(transform.MultiplyVector(other.normals[i]).normalized);
            uvs.Add(other.uvs[i]);
        }
        for (int i = 0; i < other.triangles.Count; i++) triangles.Add(other.triangles[i] + offset);
    }

    public bool IsEmpty => triangles.Count == 0;

    public Mesh ToMesh(string name)
    {
        var mesh = new Mesh { name = name };
        if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>A renderer object drawing this mesh with the shared prop material, under <paramref name="parent"/>.</summary>
    public GameObject Spawn(string name, Transform parent, bool castShadows = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = ToMesh(name);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = Material;
        renderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go;
    }

    private void Add(Vector3 v, Vector3 n, Vector2 uv)
    {
        vertices.Add(v);
        normals.Add(n);
        uvs.Add(uv);
    }

    private void Face(int a, int b, int c, int d)
    {
        triangles.AddRange(new[] { a, b, c, a, c, d });
    }
}
