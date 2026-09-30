using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Counts what the Module 3 scene asks a headset to draw, so a change can be judged in numbers
/// rather than by eye: renderers and triangles in the scene and in view from a given pose, the
/// draw-call figures Unity's own statistics report for that view, and what the counts are made of.
///
/// Run in play mode (the neighbourhood is built at runtime), from the menu or from the playtest
/// harness: `call type=Module3PerformanceCensus static=true method=Report args='[[-8,1.6,8],[20,1.2,9.5]]'`.
/// The pose is where the Pilot's eye is and what it looks at; nothing given means the main camera as it stands.
/// A Quest draws every renderer in view once for the eyes (single-pass instanced) and again for the
/// shadow map within the shadow distance, so "in view" is the number that matters; the whole-scene
/// count says what culling has to wade through.
/// </summary>
public static class Module3PerformanceCensus
{
    [MenuItem("AEDES/Module 3/Dev/Performance Census")]
    public static void Menu() => Debug.Log(Report(null, null));

    public static string Report(float[] from, float[] at)
    {
        var sb = new StringBuilder();
        var cam = Camera.main;
        GameObject temp = null;
        if (cam != null && from != null && from.Length == 3)
        {
            temp = new GameObject("[census camera]") { hideFlags = HideFlags.HideAndDontSave };
            var c = temp.AddComponent<Camera>();
            c.CopyFrom(cam);
            c.transform.position = new Vector3(from[0], from[1], from[2]);
            if (at != null && at.Length == 3) c.transform.LookAt(new Vector3(at[0], at[1], at[2]));
            c.fieldOfView = 90f;   // a headset's eye: wide
            cam = c;
        }

        var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();

        // What Unity itself counts for one render of this view.
        if (cam != null)
        {
            var rt = RenderTexture.GetTemporary(1024, 1024, 24);
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previous;
            RenderTexture.ReleaseTemporary(rt);
            var stats = typeof(EditorApplication).Assembly.GetType("UnityEditor.UnityStats");
            if (stats != null)
            {
                sb.Append("UnityStats for this view:");
                foreach (var name in new[] { "batches", "drawCalls", "setPassCalls", "triangles", "vertices", "shadowCasters", "staticBatchedDrawCalls", "instancedBatchedDrawCalls" })
                {
                    var p = stats.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null) sb.Append($" {name}={p.GetValue(null)}");
                }
                sb.AppendLine();
            }
        }

        // Our own count of what is in the view frustum, honouring the per-layer cull distances.
        int inView = 0, inViewTris = 0, shadowInView = 0;
        var byGroup = new Dictionary<string, (int all, int allTris, int seen, int seenTris)>();
        var byShader = new Dictionary<string, (int all, int seen)>();
        var byType = new Dictionary<string, int>();
        var materials = new HashSet<Material>();
        int totalTris = 0, casters = 0;
        Plane[] planes = cam != null ? GeometryUtility.CalculateFrustumPlanes(cam) : null;
        float[] cull = cam != null ? cam.layerCullDistances : null;
        float shadowDistance = QualitySettings.shadowDistance;
        var eye = cam != null ? cam.transform.position : Vector3.zero;

        int staticBatched = 0;
        foreach (var r in renderers)
        {
            // A static-batched renderer's MeshFilter holds the whole batch's mesh, so its triangles are not its own.
            bool batched = r.isPartOfStaticBatch;
            if (batched) staticBatched++;
            int tris = batched ? 0 : Triangles(r);
            totalTris += tris;
            string group = Group(r.transform);
            string type = r.GetType().Name;
            byType[type] = byType.GetValueOrDefault(type) + 1;
            foreach (var m in r.sharedMaterials) if (m != null) materials.Add(m);
            string shader = r.sharedMaterial != null && r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "(none)";
            bool casts = r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off;
            if (casts) casters++;

            bool seen = false;
            if (planes != null)
            {
                var b = r.bounds;
                seen = GeometryUtility.TestPlanesAABB(planes, b);
                if (seen && cull != null && cull[r.gameObject.layer] > 0f)
                {
                    float d = Vector3.Distance(eye, b.ClosestPoint(eye));
                    if (d > cull[r.gameObject.layer]) seen = false;
                }
                if (seen)
                {
                    inView++;
                    inViewTris += tris;
                    if (casts && Vector3.Distance(eye, r.bounds.ClosestPoint(eye)) < shadowDistance) shadowInView++;
                }
            }
            var g = byGroup.GetValueOrDefault(group);
            byGroup[group] = (g.all + 1, g.allTris + tris, g.seen + (seen ? 1 : 0), g.seenTris + (seen ? tris : 0));
            var s = byShader.GetValueOrDefault(shader);
            byShader[shader] = (s.all + 1, s.seen + (seen ? 1 : 0));
        }

        sb.AppendLine($"Scene: {renderers.Length} renderers ({staticBatched} in static batches, their triangles not counted), {totalTris} triangles, {materials.Count} materials, {casters} shadow casters, "
                      + $"{UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length} lights, "
                      + $"{UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Length} animators "
                      + $"({UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Count(a => a.cullingMode == AnimatorCullingMode.AlwaysAnimate)} always animating)");
        if (planes != null)
            sb.AppendLine($"In view: {inView} renderers, {inViewTris} triangles, {shadowInView} shadow casters within {shadowDistance:0} m");

        sb.AppendLine("By group (all / tris | in view / tris):");
        foreach (var pair in byGroup.OrderByDescending(p => p.Value.seen).ThenByDescending(p => p.Value.all))
            sb.AppendLine($"  {pair.Key,-34} {pair.Value.all,5} / {pair.Value.allTris,8} | {pair.Value.seen,5} / {pair.Value.seenTris,8}");
        sb.AppendLine("By shader (all | in view):");
        foreach (var pair in byShader.OrderByDescending(p => p.Value.seen).ThenByDescending(p => p.Value.all).Take(12))
            sb.AppendLine($"  {pair.Key,-50} {pair.Value.all,5} | {pair.Value.seen,5}");
        sb.AppendLine("By type: " + string.Join(", ", byType.OrderByDescending(p => p.Value).Select(p => $"{p.Key} {p.Value}")));

        // Texture memory, for what the scene's materials reference.
        var textures = new Dictionary<Texture, long>();
        foreach (var m in materials)
        {
            int n = m.shader != null ? m.shader.GetPropertyCount() : 0;
            for (int i = 0; i < n; i++)
            {
                if (m.shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
                var t = m.GetTexture(m.shader.GetPropertyNameId(i));
                if (t != null && !textures.ContainsKey(t)) textures[t] = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t);
            }
        }
        if (RenderSettings.skybox != null)
            foreach (var t in new[] { RenderSettings.skybox.mainTexture }) if (t != null && !textures.ContainsKey(t)) textures[t] = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t);
        sb.AppendLine($"Textures referenced: {textures.Count}, {textures.Values.Sum() / (1024 * 1024)} MB. Largest:");
        foreach (var pair in textures.OrderByDescending(p => p.Value).Take(8))
            sb.AppendLine($"  {pair.Key.name,-40} {pair.Key.width}x{pair.Key.height} {pair.Value / (1024 * 1024)} MB");

        if (temp != null) UnityEngine.Object.DestroyImmediate(temp);
        return sb.ToString();
    }

    private static int Triangles(Renderer r)
    {
        Mesh mesh = null;
        if (r is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
        else if (r.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
        if (mesh == null) return 0;
        int n = 0;
        for (int i = 0; i < mesh.subMeshCount; i++) n += (int)mesh.GetIndexCount(i) / 3;
        return n;
    }

    /// <summary>A short name for where a renderer lives: which part of the village it belongs to.</summary>
    private static string Group(Transform t)
    {
        string path = t.name;
        var names = new List<string>();
        for (var p = t; p != null; p = p.parent) names.Add(p.name);
        names.Reverse();
        string root = names[0];
        if (root == "Neighbourhood")
        {
            if (names.Count > 1 && names[1] == "Surroundings (runtime)")
            {
                if (names.Count > 2 && (names[2] == "Village Dressing" || names[2] == "Outdoor Life")) return names[2];
                string leaf = names[names.Count - 1];
                if (names.Count > 2 && names[2].StartsWith("PF_")) leaf = names[2];
                if (leaf.StartsWith("PF_")) return "Surroundings/" + leaf.Replace("(Clone)", "");
                if (leaf == "Paddy" || leaf == "Dyke") return "Surroundings/Paddies";
                return "Surroundings/" + leaf;
            }
            if (names.Count > 1 && names[1].StartsWith("Plot_"))
            {
                if (names.Count > 2)
                {
                    if (names[2].StartsWith("PF_Lao_House")) return "Plots/House";
                    if (names[2].StartsWith("House Number")) return "Plots/House number";
                    if (names[2] == "Dressing" && names.Count > 3) return "Plots/Dressing " + names[3];
                    if (names[2].StartsWith("Villager")) return "Plots/Villagers";
                    if (names[2].StartsWith("Container")) return "Plots/Jar";
                    return "Plots/" + names[2].Replace("(Clone)", "");
                }
            }
            return "Neighbourhood/other";
        }
        return root;
    }
}
