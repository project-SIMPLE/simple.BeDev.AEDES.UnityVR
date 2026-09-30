using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Daylight coming in at the openings of every house: a soft glow on the wall around each window and
/// the door, and, while the sun is on that side of the house, a beam from the opening down to the
/// patch it makes on the floor.
///
/// Once the sun is off a room's openings it is lit by ambient alone, and ambient is flat - it says
/// nothing about where the light comes from. These put that back for next to nothing: additive,
/// unlit geometry, a few triangles per house, drawn through one small material. The window side of
/// a room reads brighter than its corners, and the beam swings across the floor as the day goes on
/// (M3TimeOfDay moves the sun; the beams follow it), golden by the late afternoon.
///
/// The material is an asset (Resources/Module3/M_M3_LightShaft) on a shader of its own with the
/// additive blend hard-coded: a transparent URP/Lit material made at runtime would lean on shader
/// variants a Quest build strips (see M3HouseSurface). Only the two gradient textures are made here.
/// Everything is on the DistanceCulled layer, like the rest of the room. Added at runtime by
/// M3TimeOfDay.
/// </summary>
public class M3LightShafts : MonoBehaviour
{
    /// <summary>An opening in a house wall. In the model's space until Build places it.</summary>
    private struct Opening
    {
        public Vector3 centre;     // of the opening, on the room side of the wall
        public Vector3 across;     // unit, along the wall
        public Vector3 outward;    // unit wall normal, pointing outdoors
        public float halfWidth, halfHeight;
        /// <summary>Metres from this wall to the next house's wall it looks onto; 0 when it opens onto open ground.</summary>
        public float neighbourGap;
    }

    // Measured on PF_Lao_House_One_FloorV2 (the bounds of SM_WindowLeft, SM_WindowLeft (1) and the
    // opening Door.003 swings in), in model units: the window in the +x wall, the window in the back
    // wall, and the side door a visitor comes in by. The front wall, on the lane, has no openings.
    private static readonly Opening[] Openings =
    {
        new Opening { centre = new Vector3(4.02f, 1.04f, 1.37f), across = Vector3.forward, outward = Vector3.right, halfWidth = 0.68f, halfHeight = 0.32f },
        new Opening { centre = new Vector3(1.10f, 1.04f, -2.86f), across = Vector3.right, outward = Vector3.back, halfWidth = 0.68f, halfHeight = 0.32f },
        new Opening { centre = new Vector3(4.02f, 0.94f, -1.27f), across = Vector3.forward, outward = Vector3.right, halfWidth = 0.36f, halfHeight = 0.76f },
    };
    /// <summary>Height of the house floor in the model (M3NeighbourhoodBuilder's modelFloorHeight).</summary>
    private const float ModelFloorY = 0.178f;

    [Header("Look")]
    [Tooltip("How far the glow spreads onto the wall past the frame, in metres.")]
    [SerializeField] private float glowMargin = 0.45f;
    [Tooltip("Brightness of the wall glow. Applied once: the shader multiplies the colour by this alpha, and "
             + "an earlier version also scaled the colour by it, so 0.45 here meant 0.45 squared (0.2).")]
    [SerializeField] private float glowStrength = 0.2f;
    [Tooltip("Brightness of the sun beams per unit of sun intensity. Applied once (the old 0.5 acted as its "
             + "square, times intensity squared); 0.28 matches the old look at intensity 1.1, and beams now grow "
             + "in step with the sun instead of with its square.")]
    [SerializeField] private float beamStrength = 0.28f;
    [Tooltip("The next house's wall stops a beam that would have to climb over it: below this much clearance "
             + "the beam fades out, and it is gone once the ray meets the wall this far under its top, in metres.")]
    [SerializeField] private float occlusionSoftness = 0.8f;
    [Tooltip("Beams are laid out only for houses this near the Pilot, in metres (they are not drawn beyond 22 m, "
             + "and a house is 12 m wide).")]
    [SerializeField] private float layRadius = 30f;
    [Tooltip("A beam is cut off this far from its opening whatever the sun's angle, in metres.")]
    [SerializeField] private float maxBeamLength = 7f;
    [Tooltip("How far round onto a wall the sun must be (cosine) before its openings get a beam.")]
    [SerializeField] private float beamFromFacing = 0.12f;

    private const string MaterialResource = "Module3/M_M3_LightShaft";
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    private class House
    {
        public Transform plot;
        public Opening[] openings;   // in world space, scaled and placed
        public Mesh glow, beams;
        public Color[] glowColours;   // kept, so brightening a sunlit opening allocates nothing
        /// <summary>Height a neighbouring house's wall or eave reaches, in world y.</summary>
        public float neighbourTop;
        /// <summary>The sun these beams were laid for. The sun never shines upward, so Vector3.up means "not yet".</summary>
        public Vector3 laidFor = Vector3.up;
    }

    private readonly List<House> houses = new List<House>();
    private Material glowMaterial, beamMaterial;
    private Texture2D glowGradient, beamGradient;
    private bool built, failed;

    // Scratch buffers reused for every rebuild, so a moving sun costs no allocation once warm.
    private readonly List<Vector3> verts = new List<Vector3>(48);
    private readonly List<Vector2> uvs = new List<Vector2>(48);
    private readonly List<Color> colours = new List<Color>(48);
    private readonly List<int> tris = new List<int>(72);

    private void LateUpdate()
    {
        if (failed) return;
        if (!built)
        {
            var plots = M3NeighbourhoodBuilder.HouseholdViews;
            if (plots.Count == 0) return;
            Build(plots);
            if (failed) return;
        }

        var tod = M3TimeOfDay.Instance;
        Vector3 sunDirection = tod != null ? tod.SunDirection : Vector3.down;
        Color sunColour = tod != null ? tod.Now.sun : Color.white;
        float sunIntensity = tod != null ? tod.Now.sunIntensity : 1f;
        float warmth = tod != null ? tod.Warmth : 0f;

        // The shader multiplies the colour by its alpha, so the strength goes in the alpha only. (Multiplying
        // a Color by a float scales its alpha as well, which made every strength count twice.)
        var glowColour = Color.Lerp(new Color(1f, 0.96f, 0.88f), new Color(1f, 0.84f, 0.64f), warmth);
        glowColour.a = glowStrength;
        glowMaterial.SetColor(ColorId, glowColour);
        sunColour.a = sunIntensity * beamStrength;
        beamMaterial.SetColor(ColorId, sunColour);

        // The sun moves about a degree a second during play, and fast during the handover sweep. Only the
        // houses near the Pilot are laid out: the rest are not drawn (M3DistanceCulling), and doing every
        // house each time the sun moved 0.3 degrees was a CPU spike at each handover. A house that was
        // skipped is laid out for the sun of the day the moment the Pilot comes within reach of it.
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 at = cam.transform.position;
        float radius2 = layRadius * layRadius;
        foreach (var house in houses)
        {
            if ((house.plot.position - at).sqrMagnitude > radius2) continue;
            if (Vector3.Angle(house.laidFor, sunDirection) <= 0.3f) continue;
            Lay(house, sunDirection);
            house.laidFor = sunDirection;
        }
    }

    private void Build(IReadOnlyList<HouseholdView> plots)
    {
        var asset = Resources.Load<Material>(MaterialResource);
        if (asset == null)
        {
            Debug.LogWarning($"Module 3: no material at Resources/{MaterialResource}; the rooms get no light shafts.");
            failed = true;
            return;
        }

        // Glow: a soft-edged rectangle. Beam: soft across its width, fading along its length.
        glowGradient = Gradient("M3 glow gradient", (u, v) => SoftEdge(u) * SoftEdge(v));
        beamGradient = Gradient("M3 beam gradient", (u, v) => Mathf.Pow(Mathf.Sin(u * Mathf.PI), 1.6f) * Mathf.Pow(1f - v, 2f));
        glowMaterial = new Material(asset) { name = asset.name + " (glow)" };
        glowMaterial.SetTexture(BaseMapId, glowGradient);
        beamMaterial = new Material(asset) { name = asset.name + " (beam)" };
        beamMaterial.SetTexture(BaseMapId, beamGradient);

        float scale = M3NeighbourhoodBuilder.HouseScale;
        foreach (var view in plots)
        {
            if (view == null) continue;
            var t = view.transform;
            var house = new House { plot = t, openings = new Opening[Openings.Length] };
            for (int i = 0; i < Openings.Length; i++)
            {
                var o = Openings[i];
                house.openings[i] = new Opening
                {
                    centre = t.TransformPoint(o.centre * scale),
                    across = t.TransformDirection(o.across),
                    outward = t.TransformDirection(o.outward),
                    halfWidth = o.halfWidth * scale,
                    halfHeight = o.halfHeight * scale,
                };
            }
            house.neighbourTop = NeighbourTop(t);
            house.glow = NewMesh("Window glow", t, glowMaterial);
            house.beams = NewMesh("Sun beams", t, beamMaterial);
            LayGlow(house);
            houses.Add(house);
        }

        // Which openings look straight onto another house? Houses stand in rows with a gap of 2.4 to about
        // 6 m between their side walls, and the window and the door are in the side wall: at a low sun the
        // neighbour's wall is in the way, and the beam has to be dropped or it shines into a room the real,
        // shadow-mapped sun cannot reach. The neighbour is found by the ray straight out of the opening: the
        // wall it meets, if any, on the side wall that faces this house - the houses need not be level or
        // parallel (VillageLayout turns them a few degrees and stands them further back or forward).
        foreach (var house in houses)
        {
            for (int i = 0; i < house.openings.Length; i++)
            {
                var o = house.openings[i];
                // The gap is measured from this house's own wall, as it always was, not from the opening in it.
                var ownWall = house.plot.TransformPoint(new Vector3(
                    (Vector3.Dot(o.outward, house.plot.right) > 0f ? ModelMaxX : ModelMinX) * scale, 0f, 0f));
                float depth = Mathf.Max(0f, Vector3.Dot(ownWall - o.centre, o.outward));
                foreach (var other in houses)
                {
                    if (other == house) continue;
                    bool plusX = Vector3.Dot(other.plot.right, o.outward) < 0f;   // its +x wall faces us if +x points back at us
                    Vector3 normal = plusX ? other.plot.right : -other.plot.right;
                    float facing = -Vector3.Dot(o.outward, normal);
                    if (facing < 0.7f) continue;   // not a wall that looks back at this opening
                    Vector3 wall = other.plot.TransformPoint(new Vector3((plusX ? ModelMaxX : ModelMinX) * scale, 0f, 0f));
                    float reach = Vector3.Dot(wall - o.centre, normal) / -facing;   // along the ray, to the wall's plane
                    float gap = reach - depth;
                    if (reach < 1f || gap < 0.3f || gap > 7f) continue;
                    // ...and the ray lands on that wall, not past the end of it.
                    float z = other.plot.InverseTransformPoint(o.centre + o.outward * reach).z;
                    if (z < Aedes.Module3.Sim.VillageLayout.ModelBackZ * scale - 0.5f
                        || z > Aedes.Module3.Sim.VillageLayout.ModelFrontZ * scale + 0.5f) continue;
                    o.neighbourGap = gap;
                    break;
                }
                house.openings[i] = o;
            }
        }
        built = true;
    }

    /// <summary>The house's outer side walls, measured on the model: -x to +x, at scale 1 (M3Surroundings.ModelMinX/MaxX).</summary>
    private const float ModelMinX = -4.16f, ModelMaxX = 4.39f;

    /// <summary>
    /// How high a neighbour's wall reaches, in world y: the top of the walls or the low edge of the roof,
    /// whichever is higher. Every house is the same model, so this house's own renderers give it.
    /// </summary>
    private static float NeighbourTop(Transform plot)
    {
        Transform house = null;
        foreach (Transform child in plot)
            if (child.Find("Roof") != null) { house = child; break; }
        float top = plot.position.y + 3f;   // about a house's wall height, if the house cannot be measured
        if (house == null) return top;
        top = plot.position.y;
        foreach (var r in house.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name == "Main_Floor") top = Mathf.Max(top, r.bounds.max.y);
            else if (r.name == "Roof") top = Mathf.Max(top, r.bounds.min.y);
        }
        return top;
    }

    /// <summary>One quad on the wall at each opening, a little larger than the frame.</summary>
    private void LayGlow(House house)
    {
        verts.Clear(); uvs.Clear(); colours.Clear(); tris.Clear();
        foreach (var o in house.openings)
        {
            Vector3 c = o.centre - o.outward * 0.02f;
            Vector3 a = o.across * (o.halfWidth + glowMargin), u = Vector3.up * (o.halfHeight + glowMargin);
            Quad(house.plot, c - a - u, c + a - u, c + a + u, c - a + u, 1f);
        }
        Commit(house.glow);
        house.glowColours = colours.ToArray();
    }

    /// <summary>
    /// The beams for one house, given where the sunlight travels: from each opening the sun is on,
    /// down to where its light lands on the floor. The glow at a sunlit opening brightens too.
    /// </summary>
    private void Lay(House house, Vector3 sunDirection)
    {
        float floorY = house.plot.position.y + ModelFloorY * M3NeighbourhoodBuilder.HouseScale;
        verts.Clear(); uvs.Clear(); colours.Clear(); tris.Clear();

        var glowColours = house.glowColours;
        for (int i = 0; i < house.openings.Length; i++)
        {
            var o = house.openings[i];
            float facing = Vector3.Dot(o.outward, -sunDirection);
            float strength = Mathf.Clamp01(Mathf.InverseLerp(beamFromFacing, 0.6f, facing));

            // Followed back toward the sun, the ray from this opening reaches the neighbour's wall plane after
            // gap / facing metres, having climbed that far times the sun's height per metre. If it is still
            // below the wall's top there, the wall is in the way.
            if (o.neighbourGap > 0f && strength > 0f)
            {
                float heightAtWall = o.centre.y + o.neighbourGap / facing * -sunDirection.y;
                float clear = heightAtWall - house.neighbourTop;
                strength *= Mathf.Clamp01(clear / Mathf.Max(0.01f, occlusionSoftness) + 0.5f);
            }

            for (int k = 0; k < 4 && i * 4 + k < glowColours.Length; k++) glowColours[i * 4 + k].a = 0.65f + 0.35f * strength;
            if (strength <= 0f || sunDirection.y > -0.05f) continue;

            Vector3 c = o.centre - o.outward * 0.03f;
            Vector3 a = o.across * o.halfWidth, u = Vector3.up * o.halfHeight;
            // The four corners of the opening, without an array: this runs for a house each time the sun moves.
            Vector3 n0 = c - a - u, n1 = c + a - u, n2 = c + a + u, n3 = c - a + u;
            Beam(house, n0, n1, floorY, sunDirection, strength);
            Beam(house, n1, n2, floorY, sunDirection, strength);
            Beam(house, n2, n3, floorY, sunDirection, strength);
            Beam(house, n3, n0, floorY, sunDirection, strength);
        }
        house.glow.SetColors(glowColours);
        Commit(house.beams);
    }

    /// <summary>One face of a beam: the edge p0-p1 of the opening, swept along the light to the floor.</summary>
    private void Beam(House house, Vector3 p0, Vector3 p1, float floorY, Vector3 sunDirection, float strength)
        => Quad(house.plot, p0, p1, p1 + sunDirection * Reach(p1, floorY, sunDirection),
                p0 + sunDirection * Reach(p0, floorY, sunDirection), strength);

    /// <summary>How far light from <paramref name="from"/> travels before it reaches the floor.</summary>
    private float Reach(Vector3 from, float floorY, Vector3 sunDirection)
        => Mathf.Clamp((from.y - floorY) / -sunDirection.y, 0f, maxBeamLength);

    /// <summary>Adds a quad (world corners, wound round) to the scratch buffers, in the plot's space.</summary>
    private void Quad(Transform plot, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float alpha)
    {
        int b = verts.Count;
        verts.Add(plot.InverseTransformPoint(p0)); uvs.Add(new Vector2(0f, 0f));
        verts.Add(plot.InverseTransformPoint(p1)); uvs.Add(new Vector2(1f, 0f));
        verts.Add(plot.InverseTransformPoint(p2)); uvs.Add(new Vector2(1f, 1f));
        verts.Add(plot.InverseTransformPoint(p3)); uvs.Add(new Vector2(0f, 1f));
        var colour = new Color(1f, 1f, 1f, alpha);
        for (int i = 0; i < 4; i++) colours.Add(colour);
        tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
        tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
    }

    private void Commit(Mesh mesh)
    {
        mesh.Clear(true);   // the vertex count changes as the sun comes round to other walls
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colours);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
    }

    private static Mesh NewMesh(string name, Transform plot, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(plot, false);
        var mesh = new Mesh { name = name };
        mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        M3DistanceCulling.Mark(go);   // indoors, so not drawn from two houses away
        return mesh;
    }

    /// <summary>A 64 x 64 single-channel ramp, linear, clamped: data for the shader, not a picture.</summary>
    private static Texture2D Gradient(string name, System.Func<float, float, float> f)
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                byte v = (byte)Mathf.RoundToInt(Mathf.Clamp01(f((x + 0.5f) / size, (y + 0.5f) / size)) * 255f);
                pixels[y * size + x] = new Color32(v, v, v, 255);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }

    /// <summary>1 across the middle half, easing to 0 at the edges.</summary>
    private static float SoftEdge(float x) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, Mathf.Abs(x * 2f - 1f)));

    private void OnDestroy()
    {
        if (glowMaterial != null) Destroy(glowMaterial);
        if (beamMaterial != null) Destroy(beamMaterial);
        if (glowGradient != null) Destroy(glowGradient);
        if (beamGradient != null) Destroy(beamGradient);
    }
}
