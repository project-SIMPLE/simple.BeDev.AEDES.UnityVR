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
            ground = GrowToCover(sceneGround.transform, groundCollider.bounds, village);
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

    /// <summary>
    /// The scene's ground is a plane of a fixed size (120 m, made by the scene builder), which a village
    /// that winds and spreads out can outgrow: the walls are held inside it, so a house past its edge
    /// would stand on nothing and its far side would be out of the Pilot's reach. The plane is stretched
    /// to reach <see cref="Margin"/> past the village if it does not already. Returns its new bounds.
    /// </summary>
    private static Bounds GrowToCover(Transform plane, Bounds current, Bounds village)
    {
        if (current.size.x <= 0f || current.size.z <= 0f) return current;   // a disabled collider has no bounds to grow from
        var needed = Limits(village, null);
        float xMin = Mathf.Min(current.min.x, needed.xMin - GroundInset), xMax = Mathf.Max(current.max.x, needed.xMax + GroundInset);
        float zMin = Mathf.Min(current.min.z, needed.yMin - GroundInset), zMax = Mathf.Max(current.max.z, needed.yMax + GroundInset);
        if (xMin >= current.min.x && xMax <= current.max.x && zMin >= current.min.z && zMax <= current.max.z) return current;

        var scale = plane.localScale;
        scale.x *= (xMax - xMin) / current.size.x;
        scale.z *= (zMax - zMin) / current.size.z;
        plane.localScale = scale;
        plane.position = new Vector3((xMin + xMax) * 0.5f, plane.position.y, (zMin + zMax) * 0.5f);
        Physics.SyncTransforms();   // the collider is not moved to the new transform until the next physics step otherwise
        return new Bounds(new Vector3((xMin + xMax) * 0.5f, current.center.y, (zMin + zMax) * 0.5f),
                          new Vector3(xMax - xMin, current.size.y, zMax - zMin));
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
