using System.Collections.Generic;
using Aedes.Module3.Sim;
using UnityEngine;

/// <summary>One house of the scene's village that can hold a household, and where it stands.</summary>
public sealed class M3VillageSite
{
    public GameObject House;
    public PlotSite Plot;
}

/// <summary>
/// Finds the houses of the village the scene already carries.
///
/// Module 3 first built a village of its own at runtime, which was the wrong place for it: the module
/// belongs in the village Modules 1 and 2 share - the `Environment` object of their scenes, with its
/// road, terraces, fences, trees and houses. Module3SceneBuilder now takes that village from the Module 2
/// scene unchanged, and this class reads it: every house under Environment/House that is the Lao house
/// the builder knows how to furnish (PF_Lao_House_One_FloorV2, the only one with doors and a room)
/// becomes a plot, exactly where and how it stands. The other buildings - the two-storey house, the
/// closed cabins across the road - have no interior the Pilot could enter, and stay as they are:
/// scenery. Nothing in the village is moved, re-spaced or replaced.
/// </summary>
public static class M3VillageSites
{
    public const string VillageRoot = "Environment";
    public const string HouseGroup = "House";

    /// <summary>The scene's village, or null when the scene has none (the neighbourhood is then generated).</summary>
    public static GameObject FindVillage() => GameObject.Find(VillageRoot);

    /// <summary>
    /// The houses of the scene's village that can take a household, in lane order (PlotSites.LaneOrder),
    /// or null when there is no village or none of its houses is <paramref name="housePrefab"/>. Instances
    /// are known by name, as Unity names them: the prefab's name, or that followed by " (n)".
    /// </summary>
    public static List<M3VillageSite> Find(GameObject housePrefab)
    {
        var village = FindVillage();
        if (village == null || housePrefab == null) return null;
        var group = village.transform.Find(HouseGroup);
        if (group == null) return null;

        var found = new List<M3VillageSite>();
        foreach (Transform child in group)
        {
            if (!child.gameObject.activeInHierarchy || !IsInstanceOf(child.name, housePrefab.name)) continue;
            found.Add(new M3VillageSite
            {
                House = child.gameObject,
                Plot = new PlotSite { X = child.position.x, Z = child.position.z, Yaw = child.eulerAngles.y, Scale = child.lossyScale.x },
            });
        }
        if (found.Count == 0) return null;

        var plots = new PlotSite[found.Count];
        for (int i = 0; i < plots.Length; i++) plots[i] = found[i].Plot;
        var sites = new List<M3VillageSite>(found.Count);
        foreach (int i in PlotSites.LaneOrder(plots)) sites.Add(found[i]);
        return sites;
    }

    /// <summary>True for "Name" and "Name (n)", which is how Unity names the instances of a prefab.</summary>
    public static bool IsInstanceOf(string objectName, string prefabName)
        => objectName == prefabName || (objectName.StartsWith(prefabName + " (") && objectName.EndsWith(")"));

    /// <summary>
    /// The village's footprint: everything it draws and its terrain, with y running from the ground (the
    /// terrains' base, or failing that the lowest thing drawn) to the tallest roof. The countryside is
    /// laid out around this, at its ground level.
    /// </summary>
    public static Bounds Footprint(GameObject village)
    {
        bool any = false;
        var b = new Bounds(village.transform.position, Vector3.zero);
        foreach (var r in village.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; }
        }

        float ground = float.MaxValue;
        foreach (var t in village.GetComponentsInChildren<Terrain>())
        {
            if (t.terrainData == null) continue;
            var size = t.terrainData.size;
            // Only the ground: a terrain's own bounds reach as high as its data allows (hundreds of metres).
            var flat = new Bounds(t.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f), new Vector3(size.x, 0f, size.z));
            if (any) b.Encapsulate(flat); else { b = flat; any = true; }
            ground = Mathf.Min(ground, t.transform.position.y);
        }
        if (ground < float.MaxValue) b.min = new Vector3(b.min.x, ground, b.min.z);
        return b;
    }
}
