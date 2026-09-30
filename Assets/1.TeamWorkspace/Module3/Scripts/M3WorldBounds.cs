using UnityEngine;

/// <summary>
/// Four invisible walls round the village, so the Pilot cannot walk out of the world.
///
/// Walking with the right stick is the only way to move (teleport was taken out), the scene's Ground
/// collider is a 120 m plane, and the grass the Pilot sees runs on to the horizon: past the edge of
/// the plane the CharacterController had nothing under it and fell, with no floor to catch it and no
/// way back. The walls stand a generous margin outside the village - beyond the tree line, so nobody
/// meets one in ordinary play - and never further out than the ground collider reaches.
///
/// The walls sit on the Ignore Raycast layer: the CharacterController still stops at them, but the
/// XR rays, the villager-talk ray and the panel's floor probe pass through.
/// </summary>
public static class M3WorldBounds
{
    /// <summary>Metres past the village bounds the Pilot may walk: past the yards, the palms and the first paddy.</summary>
    public const float Margin = 18f;
    /// <summary>Metres kept inside the scene ground's own edge, so a wall never stands on the last metre of it.</summary>
    private const float GroundInset = 3f;
    private const float Height = 8f, Thickness = 2f;

    /// <summary>The walkable rectangle for a village, on the ground plane: x, z minimum and maximum.</summary>
    public static Rect Limits(Bounds village, Bounds? ground)
    {
        float xMin = village.min.x - Margin, xMax = village.max.x + Margin;
        float zMin = village.min.z - Margin, zMax = village.max.z + Margin;
        if (ground.HasValue)
        {
            var g = ground.Value;
            xMin = Mathf.Max(xMin, g.min.x + GroundInset);
            xMax = Mathf.Min(xMax, g.max.x - GroundInset);
            zMin = Mathf.Max(zMin, g.min.z + GroundInset);
            zMax = Mathf.Min(zMax, g.max.z - GroundInset);
        }
        return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
    }

    public static void Build(Transform parent, Bounds village)
    {
        Bounds? ground = null;
        var sceneGround = GameObject.Find("Ground");
        if (sceneGround != null && sceneGround.TryGetComponent<Collider>(out var groundCollider))
            ground = groundCollider.bounds;
        var limits = Limits(village, ground);

        var root = new GameObject("World Bounds (runtime)");
        root.transform.SetParent(parent, false);
        root.layer = 2;   // Ignore Raycast

        float cx = limits.center.x, cz = limits.center.y;
        float w = limits.width + 2f * Thickness, d = limits.height + 2f * Thickness;
        Wall(root, "West", new Vector3(limits.xMin - Thickness * 0.5f, Height * 0.5f, cz), new Vector3(Thickness, Height, d));
        Wall(root, "East", new Vector3(limits.xMax + Thickness * 0.5f, Height * 0.5f, cz), new Vector3(Thickness, Height, d));
        Wall(root, "South", new Vector3(cx, Height * 0.5f, limits.yMin - Thickness * 0.5f), new Vector3(w, Height, Thickness));
        Wall(root, "North", new Vector3(cx, Height * 0.5f, limits.yMax + Thickness * 0.5f), new Vector3(w, Height, Thickness));
    }

    private static void Wall(GameObject root, string name, Vector3 centre, Vector3 size)
    {
        var go = new GameObject("Wall " + name);
        go.transform.SetParent(root.transform, false);
        go.layer = root.layer;
        go.transform.position = centre;
        go.AddComponent<BoxCollider>().size = size;
    }
}
