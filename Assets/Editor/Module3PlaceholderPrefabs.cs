using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the Module 3 prop prefabs at their FINAL paths and names, per the
/// "Unblocking pattern" in Documentation/Module3-Roadmap.md §4: code binds to
/// the prefab path and the Socket_* transforms, and art replaces the mesh
/// inside the prefab with no scene or prefab-path churn.
///
/// The meshes come from Tools/Module3/make_final_models.py (Blender); their
/// materials from Tools/Module3/make_final_materials.py, one flat colour per
/// asset per Asset Brief §2. This script only does the Unity half:
/// instantiate each model, assign its material, save the prefab, and check
/// the §4 contract held on import (sockets present, triangles within
/// budget). Eight of the nine entries were greybox placeholders (forced onto
/// the shared M_Module3Placeholder.mat) before the final art pass; Entry.
/// Material is what changed -- paths, sockets and budgets did not.
///
/// Re-running is safe and idempotent -- it overwrites the prefabs in place, so
/// the GUIDs and every scene reference to them survive.
/// </summary>
public static class Module3PlaceholderPrefabs
{
    private const string MenuBase = "AEDES/Module 3/";
    private const string BuildPath  = MenuBase + "Build Placeholder Prefabs";
    private const string VerifyPath = MenuBase + "Verify Placeholder Contract";

    private const string Models    = "Assets/1.TeamWorkspace/Team Assets/Models";
    private const string Prefabs   = "Assets/1.TeamWorkspace/Team Assets/Prefabs";
    private const string Materials = "Assets/1.TeamWorkspace/Team Assets/Materials";
    private const string PlaceholderMaterial = Materials + "/M_Module3Placeholder.mat";

    private class Entry
    {
        public string Model;        // source .fbx
        public string Prefab;       // destination .prefab
        public int TriBudget;       // §4 budget, 0 = not specified by the contract
        public string[] Sockets;    // sockets the contract expects to survive import
        public Vector3 Size;        // expected size in metres, Unity axes (Y = up)
        public bool Greybox = true; // true = force M_Module3Placeholder (no Material set)
        public string Material;     // finished art's own material; null = use Greybox rule
        public string Note;
    }

    /// <summary>
    /// The §4 outsourcing list, plus the two net states. Paths here ARE the
    /// contract -- changing one is a breaking change for any code or scene that
    /// has already bound to it.
    /// </summary>
    private static readonly Entry[] Entries =
    {
        new Entry {
            Model = Models + "/SM_ElectricFan/SM_ElectricFan.fbx",
            Prefab = Prefabs + "/Furniture/PF_ElectricFan.prefab",
            TriBudget = 3000,
            Sockets = new[] { "Socket_Mount" },
            Size = new Vector3(0.306f, 0.453f, 0.21f),
            Greybox = false,
            Material = Materials + "/M_ElectricFan.mat",
            Note = "Fan_Blade is a separate child on the spin axis; animate its local Z.",
        },
        new Entry {
            Model = Models + "/SM_WindowScreen/SM_WindowScreen.fbx",
            Prefab = Prefabs + "/Buildings/PF_WindowScreen.prefab",
            TriBudget = 500,
            Sockets = new[] { "Socket_Mount" },
            Size = new Vector3(1.5f, 1f, 0.04f),
            Greybox = false,
            Material = Materials + "/M_WindowScreen.mat",
            Note = "1.50 x 1.00 m, sized to the measured Lao house window opening.",
        },
        new Entry {
            Model = Models + "/SM_WindowScreenTorn/SM_WindowScreenTorn.fbx",
            Prefab = Prefabs + "/Buildings/PF_WindowScreenTorn.prefab",
            TriBudget = 500,
            Sockets = new[] { "Socket_Mount" },
            Size = new Vector3(1.5f, 1f, 0.099f),
            Greybox = false,
            Material = Materials + "/M_WindowScreen.mat",   // same material as intact (Asset Brief A3)
            Note = "Repair target for RepairScreen(householdId). Same opening as PF_WindowScreen.",
        },
        new Entry {
            Model = Models + "/SM_MosquitoNet_RolledUp/SM_MosquitoNet_RolledUp.fbx",
            Prefab = Prefabs + "/Props/PF_MosquitoNet_RolledUp.prefab",
            TriBudget = 1500,
            Sockets = new[] { "Socket_Mount", "Socket_Hook" },
            Size = new Vector3(0.32f, 0.589f, 0.32f),
            Greybox = false,
            Material = Materials + "/M_MosquitoNet_RolledUp.mat",
            Note = "The 'up' state. Shares an origin with PF_MosquitoNet_Deployed -- swap at one transform.",
        },
        // The 'down' state is existing finished art, not a placeholder. It gets a
        // prefab of its own only so the two net states are addressed by symmetric
        // paths; the mesh and materials are the shipped ones.
        new Entry {
            Model = Models + "/SM_MosquitoNet/SM_MosquitoNet.fbx",
            Prefab = Prefabs + "/Props/PF_MosquitoNet_Deployed.prefab",
            TriBudget = 0,
            Sockets = new string[0],
            Size = new Vector3(1.56f, 1.383f, 1.766f),
            Greybox = false,
            Note = "Existing art. Reuses SM_MosquitoNet; PF_MosquitoNet stays as it is.",
        },
        new Entry {
            Model = Models + "/SM_RepellentBottle/SM_RepellentBottle.fbx",
            Prefab = Prefabs + "/Props/PF_RepellentBottle.prefab",
            TriBudget = 1500,
            Sockets = new[] { "Socket_Grip" },
            Size = new Vector3(0.071f, 0.191f, 0.06f),
            Greybox = false,
            Material = Materials + "/M_RepellentBottle.mat",
            Note = "GiveRepellent(personId).",
        },
        new Entry {
            Model = Models + "/SM_DrinkingVessel/SM_DrinkingVessel.fbx",
            Prefab = Prefabs + "/Props/PF_DrinkingVessel.prefab",
            TriBudget = 1500,
            Sockets = new[] { "Socket_Grip" },
            Size = new Vector3(0.084f, 0.11f, 0.084f),
            Greybox = false,
            Material = Materials + "/M_DrinkingVessel.mat",
            Note = "BringWater(personId).",
        },
        new Entry {
            Model = Models + "/SM_Cloth/SM_Cloth.fbx",
            Prefab = Prefabs + "/Props/PF_Cloth.prefab",
            TriBudget = 1500,
            Sockets = new[] { "Socket_Grip" },
            Size = new Vector3(0.4f, 0.06f, 0.26f),
            Greybox = false,
            Material = Materials + "/M_Cloth.mat",
            Note = "HelpRest(personId) dressing. §5: dresses the scene, never flags the person.",
        },
        new Entry {
            Model = Models + "/SM_Torch/SM_Torch.fbx",
            Prefab = Prefabs + "/Props/PF_Torch.prefab",
            TriBudget = 1500,
            Sockets = new[] { "Socket_Grip", "Socket_Light" },
            Size = new Vector3(0.064f, 0.171f, 0.066f),
            Greybox = false,
            Material = Materials + "/M_Torch.mat",
            Note = "Parent a Light to Socket_Light with identity rotation; its forward is the beam.",
        },
        new Entry {
            Model = Models + "/SM_HealthCentre/SM_HealthCentre.fbx",
            Prefab = Prefabs + "/Buildings/PF_HealthCentre.prefab",
            TriBudget = 2000,
            Sockets = new[] { "Socket_Entrance" },
            Size = new Vector3(12.6f, 3.65f, 10.3f),
            Greybox = false,
            Material = Materials + "/M_HealthCentre.mat",
            Note = "PROVISIONAL massing. §13 item 4 has not resolved whether the player travels here.",
        },
    };

    [MenuItem(BuildPath)]
    public static void Build()
    {
        AssetDatabase.Refresh();

        // Only needed if an Entry still falls back to the shared grey
        // placeholder (Greybox = true, Material = null); every current entry
        // has its own finished-art material, but a future placeholder could
        // still bind here.
        bool anyGreybox = false;
        foreach (var e in Entries) anyGreybox |= e.Greybox && e.Material == null;
        Material placeholder = null;
        if (anyGreybox)
        {
            placeholder = AssetDatabase.LoadAssetAtPath<Material>(PlaceholderMaterial);
            if (placeholder == null)
            {
                Debug.LogError($"[Module3] Missing {PlaceholderMaterial}. " +
                               "Run Tools/Module3/make_unity_assets.py first.");
                return;
            }
        }

        var log = new StringBuilder("[Module3] Prop prefabs\n");
        int built = 0, failed = 0;

        // Deliberately NOT wrapped in StartAssetEditing/StopAssetEditing:
        // SaveAsPrefabAsset has to import the asset it just wrote, and import is
        // paused inside that block, so every save fails silently.
        foreach (var e in Entries)
        {
            if (BuildOne(e, placeholder, log)) built++;
            else failed++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        log.Append($"\n{built} built, {failed} failed.");
        if (failed > 0) Debug.LogError(log.ToString());
        else Debug.Log(log.ToString());
    }

    private static bool BuildOne(Entry e, Material placeholder, StringBuilder log)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(e.Model);
        if (model == null)
        {
            log.Append($"  MISSING MODEL  {e.Model}\n");
            return false;
        }

        var dir = Path.GetDirectoryName(e.Prefab).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir))
        {
            log.Append($"  MISSING FOLDER {dir}\n");
            return false;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        if (instance == null)
        {
            log.Append($"  INSTANTIATE FAILED {e.Model}\n");
            return false;
        }

        try
        {
            instance.name = Path.GetFileNameWithoutExtension(e.Prefab);

            // Unity's FBX importer puts a -90 deg X rotation on the root node of
            // a Z-up-declared file. Our meshes are already authored in Unity's
            // axes (Tools/Module3/make_final_models.py does the conversion into
            // the vertex data, because letting the exporter bake it displaces
            // parented children like Fan_Blade), so that rotation is a leftover
            // and the prefab root should sit at identity like the rest of the
            // repo's art. The Size check below is what proves this is right: if
            // the importer ever stops adding it, the reported bounds change and
            // Verify says so.
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localPosition = Vector3.zero;

            // Break the link to the model so the saved prefab is a plain prefab
            // whose mesh reference art can later repoint, rather than a variant
            // that fights every re-import.
            PrefabUtility.UnpackPrefabInstance(
                instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // Finished art (Entry.Material set) gets its own single material;
            // a still-greybox entry is forced onto the shared placeholder.
            // Reused existing art (Greybox = false, Material = null, e.g.
            // PF_MosquitoNet_Deployed) is left exactly as the FBX imported it.
            Material assign = e.Material != null
                ? AssetDatabase.LoadAssetAtPath<Material>(e.Material)
                : (e.Greybox ? placeholder : null);
            if (e.Material != null && assign == null)
            {
                log.Append($"  MISSING MATERIAL {e.Material}\n");
                return false;
            }
            if (assign != null)
            {
                foreach (var r in instance.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                    for (int i = 0; i < mats.Length; i++) mats[i] = assign;
                    r.sharedMaterials = mats;
                }
            }

            var saved = PrefabUtility.SaveAsPrefabAsset(instance, e.Prefab, out bool ok);
            if (!ok || saved == null)
            {
                log.Append($"  SAVE FAILED    {e.Prefab}\n");
                return false;
            }

            log.Append($"  {Describe(e, instance)}\n");
            return true;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    /// <summary>
    /// Reports what actually survived the FBX import, which is the only way to
    /// know the §4 contract held: Unity keeps or drops empty nodes depending on
    /// importer settings, and a silently-dropped socket would strand the code
    /// that expects it.
    /// </summary>
    private static string Describe(Entry e, GameObject instance)
    {
        int tris = 0;
        foreach (var mf in instance.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;

        var missing = new List<string>();
        foreach (var s in e.Sockets)
            if (FindDeep(instance.transform, s) == null) missing.Add(s);

        var size = LocalBoundsSize(instance);

        var sb = new StringBuilder();
        sb.Append($"{Path.GetFileName(e.Prefab),-34} {tris,5} tris");
        sb.Append($"  size {size.x:0.###} x {size.y:0.###} x {size.z:0.###} m");
        if (e.Size != Vector3.zero)
        {
            const float tol = 0.02f;   // 2 cm; reported sizes are rounded
            bool fits = Mathf.Abs(size.x - e.Size.x) <= tol
                     && Mathf.Abs(size.y - e.Size.y) <= tol
                     && Mathf.Abs(size.z - e.Size.z) <= tol;
            if (!fits)
                sb.Append($" WRONG SIZE, expected {e.Size.x:0.###} x " +
                          $"{e.Size.y:0.###} x {e.Size.z:0.###}");
        }
        if (e.TriBudget > 0)
            sb.Append(tris <= e.TriBudget
                ? $" (budget {e.TriBudget}, ok)"
                : $" (OVER BUDGET {e.TriBudget})");
        if (e.Sockets.Length > 0)
            sb.Append(missing.Count == 0
                ? $"  sockets ok: {string.Join(", ", e.Sockets)}"
                : $"  MISSING SOCKETS: {string.Join(", ", missing)}");
        return sb.ToString();
    }

    /// <summary>
    /// Combined mesh bounds of the whole hierarchy, in the root's local space --
    /// i.e. the size the prefab has when dropped into a scene at scale 1. This
    /// is the number that decides whether PF_WindowScreen actually fits a Lao
    /// house window, so it is worth printing on every run.
    /// </summary>
    private static Vector3 LocalBoundsSize(GameObject root)
    {
        var toRoot = root.transform.worldToLocalMatrix;
        bool any = false;
        Vector3 min = Vector3.zero, max = Vector3.zero;

        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = mf.sharedMesh;
            if (mesh == null) continue;
            var m = toRoot * mf.transform.localToWorldMatrix;
            var b = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3(
                    (i & 1) == 0 ? -1 : 1,
                    (i & 2) == 0 ? -1 : 1,
                    (i & 4) == 0 ? -1 : 1));
                var p = m.MultiplyPoint3x4(corner);
                if (!any) { min = max = p; any = true; }
                else { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            }
        }
        return any ? max - min : Vector3.zero;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            var hit = FindDeep(c, name);
            if (hit != null) return hit;
        }
        return null;
    }

    [MenuItem(VerifyPath)]
    public static void Verify()
    {
        var log = new StringBuilder("[Module3] Placeholder contract\n");
        int problems = 0;

        foreach (var e in Entries)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(e.Prefab);
            if (prefab == null)
            {
                log.Append($"  NOT BUILT      {e.Prefab}\n");
                problems++;
                continue;
            }

            var line = Describe(e, prefab);
            if (line.Contains("OVER BUDGET") || line.Contains("MISSING SOCKETS")
                || line.Contains("WRONG SIZE")) problems++;
            log.Append($"  {line}\n");
            if (!string.IsNullOrEmpty(e.Note)) log.Append($"      {e.Note}\n");
        }

        log.Append($"\n{problems} problem(s).");
        if (problems > 0) Debug.LogWarning(log.ToString());
        else Debug.Log(log.ToString());
    }
}
