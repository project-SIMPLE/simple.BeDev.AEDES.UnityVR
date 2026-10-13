using System.Collections.Generic;
using Aedes.Module3.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Generates the Module 3 scene: the village Modules 1 and 2 share, plus Module 3's own few objects.
///
/// Module 3 plays in the same village as the other two modules. That village is the `Environment`
/// object of the Module 2 scene (road, terraces, fences, trees, houses), so this builder opens that
/// scene, throws away everything that is Module 2's own game and saves what is left as the Module 3
/// scene, with the Module 3 managers, player and screens added. The Module 2 scene itself is never
/// written to. Which of the village's houses hold households is decided at runtime, by
/// M3NeighbourhoodBuilder and M3VillageSites: the ones with a room the Pilot can enter.
///
/// The scene is a build artefact, not something anybody hand-dresses. CLAUDE.md §9 and the roadmap
/// both name Unity YAML merges as the likeliest thing to hurt this project. Regenerating is how you
/// change the scene - and how a change to the village in the Module 2 scene reaches this one. If you
/// find yourself dragging things into it by hand, the change probably belongs in this file instead.
/// </summary>
public static class Module3SceneBuilder
{
    public const string ScenePath = "Assets/1.TeamWorkspace/Module3/Scenes/Module_3_MainScene.unity";

    /// <summary>The scene whose village this one is built in: Modules 1 and 2 share it, and so does Module 3.</summary>
    public const string VillageScenePath = "Assets/1.TeamWorkspace/Module2/Scene/Module_2_MainScene.unity";

    /// <summary>
    /// Where the Pilot starts: on the kerb at the road's south end, on the houses' side of it, facing up
    /// the road with the houses ahead on the left - between the fence line (x 18.5) and the electric
    /// poles (x 20.5, at z -42 and -39.5), clear of both. Module 2's own invisible walls keep the Pilot
    /// to that half of the road and the yards along it.
    /// </summary>
    private static readonly Vector3 VillageSpawn = new Vector3(19.4f, 0.2f, -38.3f);
    private const float VillageSpawnYaw = 0f;

    private const string House = "Assets/1.TeamWorkspace/Team Assets/Prefabs/Buildings/PF_Lao_House_One_FloorV2.prefab";
    private const string Bed = "Assets/1.TeamWorkspace/Team Assets/Prefabs/PF_Bed.prefab";
    private const string Villager = "Assets/1.TeamWorkspace/Team Assets/Prefabs/Character/PF_CharacterV1.prefab";
    // Purpose-built child and elder bodies (Tools/Module3/make_villagers.py, Module3VillagerPrefabs.cs).
    private const string Characters = "Assets/1.TeamWorkspace/Team Assets/Prefabs/Character/";
    private const string ChildA = Characters + "PF_Villager_Child_A.prefab";
    private const string ChildB = Characters + "PF_Villager_Child_B.prefab";
    private const string Elder = Characters + "PF_Villager_Elder.prefab";
    private const string Container = "Assets/1.TeamWorkspace/Team Assets/Prefabs/Props/PF_Jar.prefab";
    private const string DebugOverlay = "Assets/Resources/Prefabs/Utils/Debug Overlay.prefab";
    // The font Modules 1 and 2 use (M3Ui.FontResource): the whole Lao block and Basic Latin, static.
    private const string LaoFont = "Assets/Resources/Fonts/Lao_SomVang Full SDF.asset";

    [MenuItem("AEDES/Module 3/Generate Main Scene")]
    public static void Generate()
    {
        var scene = OpenVillageScene();

        BuildLighting();
        // No ground plane of our own: the village brings its terrain and road, and the Pilot walks on those.
        BuildManagers();
        BuildNeighbourhood();
        BuildPlayer();
        BuildSquadScreens();

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        RegisterInBuildSettings();

        Debug.Log($"Module 3: generated {ScenePath} in the village of {VillageScenePath}. The households are "
                  + "built at runtime on the village's own houses, so nothing of Module 3's is dressed by hand here.");
    }

    // -------------------------------------------------------------------------------------

    /// <summary>
    /// Opens the Module 2 scene and strips it to its village: the `Environment` object, with the road, the
    /// terraces, the fences, the trees and the houses. Everything else there is Module 2's own game - its
    /// rig, manager, canvas, mosquito spawner, cars and props - and goes. The Module 2 scene file is not
    /// touched: what is left is saved as this module's scene.
    /// </summary>
    private static Scene OpenVillageScene()
    {
        var scene = EditorSceneManager.OpenScene(VillageScenePath, OpenSceneMode.Single);

        GameObject village = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (village == null && root.name == M3VillageSites.VillageRoot) village = root;
            else Object.DestroyImmediate(root);
        }
        if (village == null)
        {
            throw new System.InvalidOperationException(
                $"{VillageScenePath} has no root object named '{M3VillageSites.VillageRoot}' to build the Module 3 scene in.");
        }

        // Modules 1 and 2's play rides on some of the village's props - containers that fill when it rains,
        // jars that score a lid - and their scripts read managers this scene does not have. Scenery only here.
        int stripped = 0;
        foreach (var script in village.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Object.DestroyImmediate(script);
            stripped++;
        }

        // Baked lighting belongs to the Module 2 scene; this one lights the village live (M3TimeOfDay).
        Lightmapping.lightingDataAsset = null;

        Debug.Log($"Module 3: took the village from {VillageScenePath}; {stripped} Module 1/2 scripts stripped from it.");
        return scene;
    }

    private static void BuildLighting()
    {
        var sun = new GameObject("Directional Light");
        var light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        // Module 1's sun; M3TimeOfDay takes it from here at runtime.
        light.color = new Color(0.9150943f, 0.7907398f, 0.56545925f);
        light.intensity = 0.8f;
        light.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static void BuildManagers()
    {
        var managers = new GameObject("Managers");

        // Localization first: everything else reads keys through it, and it is DontDestroyOnLoad.
        managers.AddComponent<LocalizationManager>();

        // Parameters before the session, so the session starts from whatever GAMA last sent.
        managers.AddComponent<Module3Parameters>();
        managers.AddComponent<M3Session>();

        // Only ONE SimulationManager subclass may sit on a GameObject - Unity calls Awake on
        // disabled components too and SimulationManager.Awake assigns the static Instance, so two
        // of them race (CLAUDE.md §10.4). Module3GamaLink is therefore NOT added here: the module
        // runs standalone, and turning the GAMA link on means adding that one component plus a
        // ConnectionManager, deliberately, to this object.

        var overlay = AssetDatabase.LoadAssetAtPath<GameObject>(DebugOverlay);
        if (overlay != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(overlay);
            instance.name = "Debug Overlay";
            instance.transform.SetParent(managers.transform, false);
        }
    }

    private static void BuildNeighbourhood()
    {
        var root = new GameObject("Neighbourhood");
        var builder = root.AddComponent<M3NeighbourhoodBuilder>();

        var so = new SerializedObject(builder);
        Assign(so, "housePrefab", House);
        Assign(so, "bedPrefab", Bed);
        Assign(so, "villagerPrefab", Villager);
        AssignArray(so, "childPrefabs", ChildA, ChildB);
        AssignArray(so, "elderPrefabs", Elder);
        Assign(so, "containerPrefab", Container);
        so.ApplyModifiedPropertiesWithoutUndo();

        // Sky, paddies, tree line and hills, built at runtime around the neighbourhood.
        var surroundings = new SerializedObject(root.AddComponent<M3Surroundings>());
        surroundings.FindProperty("skybox").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterial);
        AssignArray(surroundings, "nearTrees", CoconutTree, BananaTree, CoconutTree);
        AssignArray(surroundings, "farTrees", BroadTree, CoconutTree, Bush1, Bush2, BroadTree);
        surroundings.ApplyModifiedPropertiesWithoutUndo();
    }

    private const string Nature = "Assets/1.TeamWorkspace/Team Assets/Prefabs/Neture/";
    private const string CoconutTree = Nature + "PF_CoconutTree.prefab";
    private const string BananaTree = Nature + "PF_BananaTree.prefab";
    private const string BroadTree = Nature + "PF_Tree.prefab";
    private const string Bush1 = Nature + "PF_Bush_1.prefab";
    private const string Bush2 = Nature + "PF_Bush_2.prefab";
    private const string SkyboxMaterial = "Assets/Packages/Fantasy Skybox FREE/Cubemaps/Classic/FS000_Day_03.mat";

    private static void AssignArray(SerializedObject so, string field, params string[] assetPaths)
    {
        var prop = so.FindProperty(field);
        if (prop == null) return;
        prop.arraySize = assetPaths.Length;
        for (int i = 0; i < assetPaths.Length; i++)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPaths[i]);
            if (asset == null) Debug.LogWarning($"Module 3: no prefab at {assetPaths[i]}; '{field}' entry left empty.");
            prop.GetArrayElementAtIndex(i).objectReferenceValue = asset;
        }
    }

    private static void BuildPlayer()
    {
        var player = new GameObject("Player");
        player.transform.position = VillageSpawn + Vector3.up * 1.6f;
        player.transform.rotation = Quaternion.Euler(0f, VillageSpawnYaw, 0f);

        var camera = player.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 200f;
        player.AddComponent<AudioListener>();
        player.tag = "MainCamera";

        // Keyboard driver so a whole session can be played without a headset. It compiles out of
        // a release build, so leaving it in the scene costs nothing.
        player.AddComponent<M3DebugDriver>();

        // The Pilot in the headset. At runtime this swaps the plain camera above for Module 2's player
        // rig - so Module 3 controls exactly like Module 2 - and turns each turn's clock over to the
        // person wearing it; the camera stays as the desktop fallback if the rig prefab is ever missing.
        var xr = new SerializedObject(player.AddComponent<M3XRPlayer>());
        var rig = AssetDatabase.LoadAssetAtPath<GameObject>(XRRigPrefab);
        if (rig == null) Debug.LogWarning($"Module 3: no player rig at {XRRigPrefab}; the scene will run on the desktop camera.");
        xr.FindProperty("rigPrefab").objectReferenceValue = rig;
        xr.FindProperty("spawnPosition").vector3Value = VillageSpawn;
        xr.FindProperty("spawnYaw").floatValue = VillageSpawnYaw;
        xr.ApplyModifiedPropertiesWithoutUndo();

        // The conversation panel and the "walking in counts as a visit" tracker.
        var pilot = new GameObject("Pilot Interaction");
        pilot.AddComponent<M3PersonPanel>();
        pilot.AddComponent<M3VisitTracker>();
    }

    /// <summary>Module 2's player rig: the same sticks, hands, grab, poke and wrist watch (see M3XRPlayer).</summary>
    private const string XRRigPrefab = "Assets/1.TeamWorkspace/Module2/Prefabs/Player/VR_Player_Module_2.prefab";

    private static void BuildSquadScreens()
    {
        // The Coach and the Analyst watch the cast, not the headset (section 9), so this is a
        // screen-space overlay rather than anything in world space.
        var squad = new GameObject("Squad Screens");
        var hud = squad.AddComponent<M3Hud>();
        squad.AddComponent<M3JournalWriter>();

        // One font for English and Lao: it carries all 95 printable ASCII as well as the Lao block,
        // so nothing needs a fallback.
        var so = new SerializedObject(hud);
        var font = so.FindProperty("font");
        var asset = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(LaoFont);
        if (asset == null) Debug.LogWarning($"Module 3: no font at {LaoFont}; the HUD will use the TMP default.");
        if (font != null) font.objectReferenceValue = asset;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Assign(SerializedObject so, string field, string assetPath)
    {
        var prop = so.FindProperty(field);
        if (prop == null) return;

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null) Debug.LogWarning($"Module 3: no prefab at {assetPath}; '{field}' left empty.");
        prop.objectReferenceValue = asset;
    }

    private static void RegisterInBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in scenes)
        {
            if (s.path == ScenePath) { s.enabled = true; EditorBuildSettings.scenes = scenes.ToArray(); return; }
        }

        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        // SaveManager wipes the save file when the active scene's build index is 0 or 1
        // (CLAUDE.md §10). Worth knowing where Module 3 landed before relying on persistence.
        int index = scenes.Count - 1;
        if (index <= 1)
        {
            Debug.LogWarning($"Module 3 is at build index {index}. SaveManager deletes the save file at "
                             + "index 0 or 1, so anything carried over from Module 2 will be wiped.");
        }
    }
}
