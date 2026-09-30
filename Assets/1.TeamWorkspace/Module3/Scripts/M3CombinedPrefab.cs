using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns a many-renderer prefab as ONE renderer: its meshes merged, in its own space, one submesh
/// per material.
///
/// The shared coconut palm is twenty renderers - sixteen fronds, three coconuts and a trunk, each its
/// own mesh - so the fifty-odd palms round the village were a thousand draw calls, the largest single
/// cost in the scene. Every part is modelled in tree space (identity child transforms), so merging
/// them keeps every vertex where it was relative to the tree's origin, which the foliage wind
/// (M3FoliageSway.hlsl) measures from. Materials are kept as they are, per submesh, so the wind
/// material swap M3OutdoorLife makes by reference still finds them. The merged mesh is built once
/// per prefab and shared by every instance.
///
/// Needs the prefab's meshes to be readable in a build (Read/Write on the model import); a prefab
/// whose meshes are not, or that has skinned parts, is instantiated as it is, with a warning once.
/// </summary>
public static class M3CombinedPrefab
{
    private sealed class Entry
    {
        public Mesh mesh;
        public Material[] materials;
        public UnityEngine.Rendering.ShadowCastingMode shadows;
    }

    private static readonly Dictionary<GameObject, Entry> cache = new Dictionary<GameObject, Entry>();
    private static readonly HashSet<GameObject> warned = new HashSet<GameObject>();

    /// <summary>Fewer renderers than this and a prefab is left alone: nothing to gain.</summary>
    private const int WorthIt = 3;

    /// <summary>
    /// An instance of <paramref name="prefab"/> under <paramref name="parent"/>, as a single renderer where
    /// that is possible, otherwise a plain instance.
    /// </summary>
    public static GameObject Spawn(GameObject prefab, Transform parent)
    {
        var entry = Get(prefab);
        if (entry == null)
        {
            var plain = Object.Instantiate(prefab, parent);
            plain.name = prefab.name;
            return plain;
        }
        var go = new GameObject(prefab.name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = entry.mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = entry.materials;
        renderer.shadowCastingMode = entry.shadows;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go;
    }

    /// <summary>How many renderers <paramref name="prefab"/> would spawn as; 1 once it is merged.</summary>
    public static int RendererCount(GameObject prefab) => Get(prefab) != null ? 1 : prefab.GetComponentsInChildren<Renderer>(true).Length;

    private static Entry Get(GameObject prefab)
    {
        if (prefab == null) return null;
        // A cached mesh can have been destroyed under us (play mode left with domain reload off): build it again.
        if (cache.TryGetValue(prefab, out var entry) && (entry == null || entry.mesh != null)) return entry;
        entry = Build(prefab);
        cache[prefab] = entry;
        return entry;
    }

    private static Entry Build(GameObject prefab)
    {
        var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length < WorthIt) return null;
        if (prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0) return null;

        // The parts, grouped by material, each with its transform relative to the prefab's root.
        var root = prefab.transform.worldToLocalMatrix;
        var byMaterial = new Dictionary<Material, List<CombineInstance>>();
        var order = new List<Material>();
        var shadows = UnityEngine.Rendering.ShadowCastingMode.Off;
        foreach (var r in renderers)
        {
            // Not activeInHierarchy: a prefab asset is in no hierarchy, so that is false for every part of it.
            if (!r.enabled || !ActiveWithin(r.transform, prefab.transform)) continue;
            if (!r.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) continue;
            var mesh = filter.sharedMesh;
            if (!mesh.isReadable && Application.isPlaying && !Application.isEditor)
            {
                Warn(prefab, $"{mesh.name} is not readable (turn on Read/Write in its model import)");
                return null;
            }
            if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) shadows = r.shadowCastingMode;
            var materials = r.sharedMaterials;
            var matrix = root * r.transform.localToWorldMatrix;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var material = sub < materials.Length ? materials[sub] : null;
                if (material == null) continue;
                if (!byMaterial.TryGetValue(material, out var list))
                {
                    byMaterial[material] = list = new List<CombineInstance>();
                    order.Add(material);
                }
                list.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = matrix });
            }
        }
        if (order.Count == 0) return null;

        // One mesh per material first, then those side by side as the submeshes of the final mesh.
        var perMaterial = new CombineInstance[order.Count];
        int vertices = 0;
        for (int i = 0; i < order.Count; i++)
        {
            var part = new Mesh { name = $"{prefab.name} {order[i].name}" };
            var pieces = byMaterial[order[i]];
            foreach (var p in pieces) vertices += p.mesh.vertexCount;
            if (vertices > 65000) part.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            part.CombineMeshes(pieces.ToArray(), true, true, false);
            perMaterial[i] = new CombineInstance { mesh = part, subMeshIndex = 0, transform = Matrix4x4.identity };
        }
        var merged = new Mesh { name = $"{prefab.name} (merged)" };
        if (vertices > 65000) merged.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        merged.CombineMeshes(perMaterial, false, false, false);
        merged.RecalculateBounds();
        foreach (var p in perMaterial) DestroyNow(p.mesh);
        merged.UploadMeshData(true);   // the GPU has it; nothing reads the vertices back

        return new Entry { mesh = merged, materials = order.ToArray(), shadows = shadows };
    }

    /// <summary>True if <paramref name="t"/> and every parent up to <paramref name="root"/> is active.</summary>
    private static bool ActiveWithin(Transform t, Transform root)
    {
        for (var p = t; p != null; p = p.parent)
        {
            if (!p.gameObject.activeSelf) return false;
            if (p == root) break;
        }
        return true;
    }

    private static void Warn(GameObject prefab, string why)
    {
        if (!warned.Add(prefab)) return;
        Debug.LogWarning($"Module 3: {prefab.name} is spawned part by part, not merged: {why}.");
    }

    private static void DestroyNow(Object o)
    {
        if (Application.isPlaying) Object.Destroy(o);
        else Object.DestroyImmediate(o);
    }
}
