using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the assets behind the Module 3 outdoor life (M3OutdoorLife): the leaf materials of the
/// shared tree prefabs copied onto the "AEDES/Module 3/Foliage Lit" shader, which sways them, and the
/// Resources set that names them and the animal and walker prefabs.
///
/// Why copies: PF_CoconutTree, PF_BananaTree and T_CoconuTree.mat are shared with Modules 1 and 2, so
/// their materials are not switched; M3OutdoorLife swaps these onto the trees Module 3 places. Why
/// assets rather than materials made at runtime: a material created in play cannot rely on a shader
/// variant the build stripped, and a shader no asset in the build references is not in the build at
/// all. Like the house surface materials next to them, the results are committed under Resources;
/// this menu item exists so they can be regenerated rather than hand-edited:
///   Unity -batchmode -quit -projectPath . -executeMethod Module3OutdoorLifeAssets.Generate
/// </summary>
public static class Module3OutdoorLifeAssets
{
    private const string Folder = "Assets/Resources/Module3";
    private const string SetPath = Folder + "/M3OutdoorLifeSet.asset";
    private const string ShaderName = "AEDES/Module 3/Foliage Lit";

    private const string Nature = "Assets/1.TeamWorkspace/Team Assets/Prefabs/Neture/";
    private const string CoconutTree = Nature + "PF_CoconutTree.prefab";
    private const string BananaTree = Nature + "PF_BananaTree.prefab";

    private const string Creatures = "Assets/1.TeamWorkspace/Team Assets/Prefabs/Creatures/";
    private static readonly string[] Dragonflies =
    {
        // PF_DragonFlyYellow is left out: its material asset is missing from the project, so it draws magenta.
        Creatures + "Insects/PF_DragonFlyBlue.prefab",
        Creatures + "Insects/PF_DragonFlyYellow1.prefab",
    };
    private const string Frog = Creatures + "Frog/PF_Frog.prefab";

    private const string Characters = "Assets/1.TeamWorkspace/Team Assets/Prefabs/Character/";
    private static readonly string[] Walkers =
    {
        Characters + "PF_CharacterV1.prefab",
        Characters + "PF_Villager_Elder.prefab",
        Characters + "PF_Villager_Child_A.prefab",
        Characters + "PF_Villager_Child_B.prefab",
    };

    /// <summary>The tree whose leaf material is copied, and the asset the copy becomes.</summary>
    private static readonly (string tree, string asset)[] Foliage =
    {
        (CoconutTree, Folder + "/M_M3_PalmLeaf.mat"),
        (BananaTree, Folder + "/M_M3_BananaLeaf.mat"),
    };

    [MenuItem("AEDES/Module 3/Generate Outdoor Life Assets")]
    public static void Generate()
    {
        var shader = Shader.Find(ShaderName);
        if (shader == null) { Debug.LogError($"Shader '{ShaderName}' not found - has it imported?"); return; }

        var set = AssetDatabase.LoadAssetAtPath<M3OutdoorLifeSet>(SetPath);
        bool isNew = set == null;
        if (isNew) set = ScriptableObject.CreateInstance<M3OutdoorLifeSet>();

        var swaps = new System.Collections.Generic.List<M3OutdoorLifeSet.FoliageSwap>();
        foreach (var (tree, asset) in Foliage)
        {
            var original = LeafMaterial(tree);
            if (original == null) { Debug.LogError($"No leaf material found on {tree}."); continue; }

            var material = AssetDatabase.LoadAssetAtPath<Material>(asset);
            bool newMaterial = material == null;
            if (newMaterial) material = new Material(shader);
            material.CopyPropertiesFromMaterial(original);
            material.shaderKeywords = original.shaderKeywords;
            // The copy replaces the property sheet with the original's; assigning the shader again is
            // what maps it onto the fork (same property names as Lit, so nothing is lost).
            material.shader = shader;
            if (newMaterial) AssetDatabase.CreateAsset(material, asset);
            else EditorUtility.SetDirty(material);
            swaps.Add(new M3OutdoorLifeSet.FoliageSwap { from = original, to = material });
            Debug.Log($"Module 3: {asset} copies '{original.name}' ({original.shader.name}) from {tree}");
        }
        set.foliage = swaps.ToArray();
        set.dragonflies = Load(Dragonflies);
        set.frog = AssetDatabase.LoadAssetAtPath<GameObject>(Frog);
        set.walkers = Load(Walkers);

        if (isNew) AssetDatabase.CreateAsset(set, SetPath);
        else EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        Debug.Log($"Module 3: outdoor life assets generated in {Folder} ({set.foliage.Length} leaf materials, "
                  + $"{set.dragonflies.Length} dragonflies, {set.walkers.Length} walker bodies).");
    }

    /// <summary>
    /// The material the tree's leaves use: on the palm every frond is its own renderer with
    /// T_CoconuTree; the banana is one mesh with the material embedded in its FBX. Either way it is
    /// the material on the renderer whose name says "leaf", or failing that the most-used one.
    /// </summary>
    private static Material LeafMaterial(string prefabPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return null;
        var counts = new System.Collections.Generic.Dictionary<Material, int>();
        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var m in renderer.sharedMaterials)
            {
                if (m == null) continue;
                if (renderer.name.ToLowerInvariant().Contains("leaf")) return m;
                counts.TryGetValue(m, out int n);
                counts[m] = n + 1;
            }
        }
        Material best = null;
        int most = 0;
        foreach (var pair in counts)
            if (pair.Value > most) { best = pair.Key; most = pair.Value; }
        return best;
    }

    private static GameObject[] Load(string[] paths)
    {
        var list = new System.Collections.Generic.List<GameObject>(paths.Length);
        foreach (var path in paths)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) Debug.LogError($"Module 3: {path} is missing.");
            else list.Add(go);
        }
        return list.ToArray();
    }
}
