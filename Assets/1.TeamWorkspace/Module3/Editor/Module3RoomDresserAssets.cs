using UnityEditor;
using UnityEngine;

/// <summary>
/// Writes Resources/Module3/M3RoomDresser.asset: the furniture and yard plants M3RoomDresser picks from.
///
/// The pieces are Team Assets prefabs that Modules 1 and 2 share, referenced where they are, never
/// copied or edited. Keeping the list in an asset under Resources, rather than on an object in the
/// generated scene, means the scene file does not change and a build always carries the prefabs.
/// The result is committed; regenerate rather than hand-edit:
///   Unity -batchmode -quit -projectPath . -executeMethod Module3RoomDresserAssets.Generate
/// </summary>
public static class Module3RoomDresserAssets
{
    private const string AssetPath = "Assets/Resources/" + M3RoomDresser.ResourcePath + ".asset";

    private const string Furniture = "Assets/1.TeamWorkspace/Team Assets/Prefabs/Furniture/";
    private const string Props = "Assets/1.TeamWorkspace/Team Assets/Prefabs/Props/";
    private const string Nature = "Assets/1.TeamWorkspace/Team Assets/Prefabs/Neture/";

    // Deliberately missing: PF_Jar, PF_TireWithWater, PlasticBucket, PlasticBasin, PF_VaseWithFlowers
    // (it holds a blue water mesh) and PF_Counter (a sink) - anything that could hold water is one of
    // the simulation's own clues. Also PF_Cloth, which Module 3 uses for caring for someone sick, and
    // the SCOOTER prefab itself, whose rider is part of its mesh (Module3MotorbikeAssets makes a riderless copy).
    [MenuItem("AEDES/Module 3/Generate Room Dresser Asset")]
    public static void Generate()
    {
        var asset = AssetDatabase.LoadAssetAtPath<M3RoomDresser>(AssetPath);
        bool isNew = asset == null;
        if (isNew) asset = ScriptableObject.CreateInstance<M3RoomDresser>();

        var so = new SerializedObject(asset);
        Assign(so, "table", Furniture + "PF_Table.prefab");
        Assign(so, "deskAndChair", Furniture + "PF_Desk&Chair.prefab");
        Assign(so, "tvDesk", Furniture + "PF_TV.prefab");
        Assign(so, "refrigerator", Furniture + "PF_Refrigerator.prefab");
        AssignArray(so, "carpets", Furniture + "PF_Carpet_Red.prefab", Furniture + "PF_Carpet_Black.prefab");
        Assign(so, "platesAndBowls", Furniture + "PF_Plates&Bowls.prefab");
        Assign(so, "stove", Furniture + "PF_Pot&GasStove.prefab");
        Assign(so, "spiceRack", Furniture + "PF_SpiceRack.prefab");
        Assign(so, "lamp", Props + "PF_Lamp.prefab");
        AssignArray(so, "pictures", Furniture + "PF_Picture.prefab", Furniture + "PF_Picture 1.prefab");
        AssignArray(so, "plantPots", Nature + "PF_Plantpot.prefab", Nature + "PF_Plantpot1.prefab");
        AssignArray(so, "bushes", Nature + "PF_Bush_1.prefab", Nature + "PF_Bush_2.prefab");
        AssignArray(so, "flowers", Nature + "PF_Flower1.prefab", Nature + "PF_Flower2.prefab", Nature + "PF_Flower3.prefab");
        Assign(so, "bananaTree", Nature + "PF_BananaTree.prefab");
        Assign(so, "motorbike", Module3MotorbikeAssets.PrefabPath);
        so.ApplyModifiedPropertiesWithoutUndo();

        if (isNew) AssetDatabase.CreateAsset(asset, AssetPath);
        else EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log($"Module 3: wrote {AssetPath}.");
    }

    private static void Assign(SerializedObject so, string field, string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) Debug.LogWarning($"Module 3: no prefab at {path}; '{field}' left empty.");
        so.FindProperty(field).objectReferenceValue = prefab;
    }

    private static void AssignArray(SerializedObject so, string field, params string[] paths)
    {
        var prop = so.FindProperty(field);
        prop.arraySize = paths.Length;
        for (int i = 0; i < paths.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            if (prefab == null) Debug.LogWarning($"Module 3: no prefab at {paths[i]}; '{field}' entry left empty.");
            prop.GetArrayElementAtIndex(i).objectReferenceValue = prefab;
        }
    }
}
