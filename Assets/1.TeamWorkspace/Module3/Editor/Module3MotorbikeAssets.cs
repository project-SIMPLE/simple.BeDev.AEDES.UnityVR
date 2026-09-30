using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Writes a parked motorbike for the Module 3 yards: the SCOOTER from the city vehicle pack with its
/// rider taken off.
///
/// The only motorbike in the repo is Vehicles.fbx's Scooter, and its rider is part of the same mesh, so
/// a scooter in a yard came with a stranger sitting on it. The rider is two separate pieces of that mesh
/// (head, and body with arms and legs) that stand above everything belonging to the bike, so this copies
/// each LOD, drops the pieces that reach above the handlebars, and bakes the FBX node's rotation and
/// 89x scale in, giving a mesh in metres, Y up, nose toward -z. The FBX and its SCOOTER prefab are
/// shared with the GAMA city demo and are not touched.
///
/// The results are committed; regenerate rather than hand-edit:
///   Unity -batchmode -quit -projectPath . -executeMethod Module3MotorbikeAssets.Generate
/// </summary>
public static class Module3MotorbikeAssets
{
    private const string Fbx = "Assets/Resources/Prefabs/Visual Prefabs/City/Vehicles/Vehicles.fbx";
    private const string Material = "Assets/Resources/Prefabs/Visual Prefabs/City/Materials/MAT_SimpleLit.mat";
    private const string Folder = "Assets/1.TeamWorkspace/Module3/Prefabs";
    public const string MeshPath = Folder + "/SM_M3_Motorbike.asset";
    public const string PrefabPath = Folder + "/PF_M3_Motorbike.prefab";

    /// <summary>Metres above the ground. The bike's body tops out at 1.14 m (the handlebars); the rider's pieces reach 1.32 and 1.62.</summary>
    private const float RiderAbove = 1.2f;
    /// <summary>The SCOOTER prefab's own LOD switch heights.</summary>
    private static readonly float[] LodHeights = { 0.1f, 0.03f, 0.001f };

    [MenuItem("AEDES/Module 3/Generate Motorbike Asset")]
    public static void Generate()
    {
        var sources = new Dictionary<string, (Mesh mesh, Transform node)>();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Fbx))
        {
            if (o is Mesh m && m.name.StartsWith("Scooter_LOD")) sources[m.name] = (m, sources.TryGetValue(m.name, out var s) ? s.node : null);
            if (o is GameObject g && g.name.StartsWith("Scooter_LOD"))
                sources[g.name] = (sources.TryGetValue(g.name, out var s) ? s.mesh : null, g.transform);
        }

        var lods = new List<Mesh>();
        for (int i = 0; i < 3; i++)
        {
            if (!sources.TryGetValue($"Scooter_LOD{i}", out var src) || src.mesh == null || src.node == null)
            {
                Debug.LogError($"Module 3: Scooter_LOD{i} not found in {Fbx}.");
                return;
            }
            var bake = Matrix4x4.TRS(Vector3.zero, src.node.localRotation, src.node.localScale);
            lods.Add(WithoutRider(src.mesh, bake, $"SM_M3_Motorbike_LOD{i}"));
        }

        Directory.CreateDirectory(Folder);
        AssetDatabase.DeleteAsset(MeshPath);
        AssetDatabase.CreateAsset(lods[0], MeshPath);
        AssetDatabase.AddObjectToAsset(lods[1], MeshPath);
        AssetDatabase.AddObjectToAsset(lods[2], MeshPath);
        AssetDatabase.SaveAssets();

        var material = AssetDatabase.LoadAssetAtPath<Material>(Material);
        var root = new GameObject("PF_M3_Motorbike");
        var levels = new LOD[3];
        for (int i = 0; i < 3; i++)
        {
            var child = new GameObject($"Motorbike_LOD{i}");
            child.transform.SetParent(root.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = lods[i];
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            levels[i] = new LOD(LodHeights[i], new Renderer[] { renderer });
        }
        var group = root.AddComponent<LODGroup>();
        group.SetLODs(levels);
        group.RecalculateBounds();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"Module 3: wrote {PrefabPath} ({lods[0].triangles.Length / 3} / {lods[1].triangles.Length / 3} / "
                  + $"{lods[2].triangles.Length / 3} triangles).");
    }

    /// <summary>
    /// Copies <paramref name="source"/> through <paramref name="bake"/>, keeping only the connected pieces
    /// that stay below <see cref="RiderAbove"/>. Pieces are found by welding vertices at the same position,
    /// since the FBX splits vertices along UV seams.
    /// </summary>
    private static Mesh WithoutRider(Mesh source, Matrix4x4 bake, string name)
    {
        var verts = source.vertices;
        var normals = source.normals;
        var uvs = source.uv;
        var tris = source.triangles;

        var baked = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++) baked[i] = bake.MultiplyPoint3x4(verts[i]);

        var weldIds = new Dictionary<Vector3Int, int>();
        var weld = new int[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            var key = Vector3Int.RoundToInt(baked[i] * 10000f);
            if (!weldIds.TryGetValue(key, out int id)) weldIds[key] = id = weldIds.Count;
            weld[i] = id;
        }
        var parent = new int[weldIds.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Root(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        for (int i = 0; i < tris.Length; i += 3)
        {
            int a = Root(weld[tris[i]]);
            parent[Root(weld[tris[i + 1]])] = a;
            parent[Root(weld[tris[i + 2]])] = a;
        }

        var top = new Dictionary<int, float>();
        foreach (int v in tris)
        {
            int r = Root(weld[v]);
            top[r] = top.TryGetValue(r, out float y) ? Mathf.Max(y, baked[v].y) : baked[v].y;
        }

        var remap = new Dictionary<int, int>();
        var outVerts = new List<Vector3>();
        var outNormals = new List<Vector3>();
        var outUvs = new List<Vector2>();
        var outTris = new List<int>();
        int dropped = 0;
        for (int i = 0; i < tris.Length; i += 3)
        {
            if (top[Root(weld[tris[i]])] > RiderAbove) { dropped++; continue; }
            for (int k = 0; k < 3; k++)
            {
                int v = tris[i + k];
                if (!remap.TryGetValue(v, out int n))
                {
                    remap[v] = n = outVerts.Count;
                    outVerts.Add(baked[v]);
                    outNormals.Add(normals.Length > 0 ? bake.MultiplyVector(normals[v]).normalized : Vector3.up);
                    outUvs.Add(uvs.Length > 0 ? uvs[v] : Vector2.zero);
                }
                outTris.Add(n);
            }
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(outVerts);
        mesh.SetNormals(outNormals);
        mesh.SetUVs(0, outUvs);
        mesh.SetTriangles(outTris, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        Debug.Log($"Module 3: {name}: kept {outTris.Count / 3} triangles, dropped {dropped} (the rider).");
        return mesh;
    }
}
