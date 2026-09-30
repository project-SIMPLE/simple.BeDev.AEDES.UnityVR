using TMPro;
using UnityEngine;

/// <summary>
/// Paints each house's number on its walls, large enough to read from the lane.
///
/// The handover screen says "Someone in house 4 is bleeding" and the journal lists the houses nobody
/// reached, so the number a message names has to be the number the Pilot can see on the building.
/// Household.NumberOf decides what that number is; this only draws it.
///
/// Three boards per house: a large one on the front wall - the blank wall that both rows turn toward
/// the lane - and a smaller one at the lane end of each side wall, so the number is still in view as
/// the Pilot turns into the gap between houses and walks down to the door. A painted wooden board in
/// the same brown as the doors, with cream digits: it sits with the doors and the jars instead of
/// shouting over the pale walls (a blue-and-white plate did). Built in code like the rest of the
/// neighbourhood, so there is no prefab or scene YAML to merge.
/// </summary>
public static class M3HouseNumber
{
    /// <summary>Weathered wood, a shade darker than the door leaves so it reads as a separate thing.</summary>
    public static readonly Color BoardColour = new Color(0.40f, 0.26f, 0.15f);
    /// <summary>Old cream paint, not white: enough contrast to read from the lane, no glare.</summary>
    public static readonly Color DigitColour = new Color(0.93f, 0.88f, 0.76f);

    /// <summary>Front board in metres. It is a sign, so it does not scale with the house.</summary>
    private const float FrontWidth = 1.7f, FrontHeight = 1.25f;
    /// <summary>The side boards are the front board at this scale.</summary>
    private const float SideScale = 0.6f;
    /// <summary>Gap between a board's top edge and the eave, and how far it stands off the wall.</summary>
    private const float BelowEave = 0.12f, OffWall = 0.04f;
    /// <summary>How far in from the front corner the side boards sit.</summary>
    private const float CornerInset = 0.3f;
    /// <summary>Lowest a board's centre goes: above the villagers' heads and the yard jar.</summary>
    private const float MinCentreHeight = 1.9f;
    /// <summary>
    /// The digits' layout box relative to the board. TMP fits its line height (ascender to descender)
    /// into the box, and a digit is only about two thirds of that, so a box the size of the board left
    /// the number small; a taller box lets the digit fill most of the board's height.
    /// </summary>
    private const float DigitBoxHeight = 1.4f, DigitBoxWidth = 0.92f;

    private static Material boardMaterial, digitMaterial;

    /// <summary>
    /// Hangs <paramref name="number"/> on <paramref name="house"/>, which stands in
    /// <paramref name="plot"/> with +z toward the lane and its side door in the +x wall.
    /// </summary>
    public static void Attach(Transform plot, GameObject house, int number)
    {
        if (!Measure(house, plot, out Bounds walls, out float eave)) return;

        var root = new GameObject($"House Number {number}").transform;
        root.SetParent(plot, false);

        string digits = number.ToString();

        // Front: centred on the wall facing the lane, tucked under the eave.
        float frontY = Mathf.Max(MinCentreHeight, eave - BelowEave - FrontHeight * 0.5f);
        Board(root, digits, new Vector3(walls.center.x, frontY, walls.max.z + OffWall), Vector3.forward,
              FrontWidth, FrontHeight);

        // Sides: at the lane end of each side wall, clear of the door and the window further along
        // the +x wall. A board over the side door sat across the top of the doorway, in the Pilot's
        // face as they went in.
        float sideW = FrontWidth * SideScale, sideH = FrontHeight * SideScale;
        float sideY = Mathf.Max(MinCentreHeight, eave - BelowEave - sideH * 0.5f);
        float sideZ = walls.max.z - CornerInset - sideW * 0.5f;
        // Small, and only useful from the gap they face: not drawn from two houses away (M3DistanceCulling),
        // which is forty renderers fewer in a view down the lane.
        M3DistanceCulling.Mark(Board(root, digits, new Vector3(walls.max.x + OffWall, sideY, sideZ), Vector3.right, sideW, sideH));
        M3DistanceCulling.Mark(Board(root, digits, new Vector3(walls.min.x - OffWall, sideY, sideZ), Vector3.left, sideW, sideH));
    }

    /// <summary>
    /// The walls' extent in plot space and the eave height, read from the model itself rather than
    /// from constants, so a re-scaled or replaced house keeps its number in the right place. Door
    /// leaves are left out of the walls: the builder swings them open, so they stand proud of the wall.
    /// </summary>
    private static bool Measure(GameObject house, Transform plot, out Bounds walls, out float eave)
    {
        walls = default;
        eave = 0f;
        bool any = false;
        Renderer roof = null;

        foreach (var r in house.GetComponentsInChildren<Renderer>())
        {
            if (r.name.StartsWith("Door")) continue;
            if (r.name == "Roof") { roof = r; continue; }

            var b = InPlotSpace(r.bounds, plot);
            if (any) walls.Encapsulate(b); else { walls = b; any = true; }
        }
        if (!any) return false;

        eave = roof != null ? InPlotSpace(roof.bounds, plot).min.y : walls.max.y;
        return true;
    }

    private static Bounds InPlotSpace(Bounds world, Transform plot)
    {
        var local = new Bounds(plot.InverseTransformPoint(world.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? world.min.x : world.max.x,
                                     (i & 2) == 0 ? world.min.y : world.max.y,
                                     (i & 4) == 0 ? world.min.z : world.max.z);
            local.Encapsulate(plot.InverseTransformPoint(corner));
        }
        return local;
    }

    /// <summary>One board with its digits; returns an object holding both, for callers that want to mark them.</summary>
    private static GameObject Board(Transform root, string digits, Vector3 centre, Vector3 facing, float width, float height)
    {
        var holder = new GameObject("Board").transform;
        holder.SetParent(root, false);
        root = holder;
        // A quad's face looks down its own -z, so its +z points into the wall.
        var turn = Quaternion.LookRotation(-facing, Vector3.up);

        var board = GameObject.CreatePrimitive(PrimitiveType.Quad);
        board.name = "Board";
        var collider = board.GetComponent<Collider>();   // nothing to grab, poke or teleport onto
        if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
        board.transform.SetParent(root, false);
        board.transform.localPosition = centre;
        board.transform.localRotation = turn;
        board.transform.localScale = new Vector3(width, height, 1f);
        board.GetComponent<Renderer>().sharedMaterial = BoardMaterial();

        // Beside the board rather than under it: the board's scale would squash the glyphs.
        var text = new GameObject("Digits").AddComponent<TextMeshPro>();
        text.transform.SetParent(root, false);
        text.transform.localPosition = centre + facing * 0.01f;
        text.transform.localRotation = turn;
        text.rectTransform.sizeDelta = new Vector2(width * DigitBoxWidth, height * DigitBoxHeight);
        text.text = digits;
        text.color = DigitColour;
        text.fontSharedMaterial = DigitMaterial(text.fontSharedMaterial);
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        // Fill the board whatever the digit count; 3D TMP sizes are in points, ten to the metre.
        text.enableAutoSizing = true;
        text.fontSizeMin = 1f;
        text.fontSizeMax = 100f;
        return holder.gameObject;
    }

    private static Material BoardMaterial()
    {
        if (boardMaterial != null) return boardMaterial;
        // URP/Lit, as every flat colour in Module 3 - the toon graph ignores its base colour and
        // draws black (M3Surroundings does the same). Matte: it is old paint on wood.
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        boardMaterial = new Material(shader) { name = "M_M3_HouseNumberBoard", color = BoardColour };
        if (boardMaterial.HasProperty("_Smoothness")) boardMaterial.SetFloat("_Smoothness", 0.1f);
        return boardMaterial;
    }

    /// <summary>
    /// One copy of the font's material for every sign, with the outline off: the default material
    /// draws a dark rim round each glyph, which made the digits look like a HUD label stuck on the
    /// wall rather than paint. Shared between the signs so they still batch.
    /// </summary>
    private static Material DigitMaterial(Material fontMaterial)
    {
        if (digitMaterial != null) return digitMaterial;
        if (fontMaterial == null) return null;
        digitMaterial = new Material(fontMaterial) { name = fontMaterial.name + " (house numbers)" };
        if (digitMaterial.HasProperty(ShaderUtilities.ID_OutlineWidth)) digitMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        if (digitMaterial.HasProperty(ShaderUtilities.ID_FaceDilate)) digitMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.05f);
        return digitMaterial;
    }
}
