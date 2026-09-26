using System.Collections.Generic;
using Aedes.Module3.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Generates the Module 3 scene from scratch.
///
/// The scene is a build artefact, not something anybody hand-dresses. CLAUDE.md §9 and the roadmap
/// both name Unity YAML merges as the likeliest thing to hurt this project - Module 1's main scene
/// is about 38,000 lines and two people editing one conflict irreconcilably. So the scene contains
/// four root objects and nothing else; the neighbourhood is laid out at runtime by
/// M3NeighbourhoodBuilder from the simulation's own parameters.
///
/// Regenerating is how you change the scene. If you find yourself dragging things into it by hand,
/// the change probably belongs in this file instead.
/// </summary>
public static class Module3SceneBuilder
{
    public const string ScenePath = "Assets/1.TeamWorkspace/Module3/Scenes/Module_3_MainScene.unity";

    private const string House = "Assets/1.TeamWorkspace/Team Assets/Prefabs/Buildings/PF_Lao_House_One_FloorV2.prefab";
    private const string Bed = "Assets/1.TeamWorkspace/Team Assets/Prefabs/PF_Bed.prefab";
    private const string Villager = "Assets/1.TeamWorkspace/Team Assets/Prefabs/Character/PF_CharacterV1.prefab";
    private const string Container = "Assets/1.TeamWorkspace/Team Assets/Prefabs/Props/PF_Jar.prefab";
    private const string DebugOverlay = "Assets/Resources/Prefabs/Utils/Debug Overlay.prefab";

    [MenuItem("AEDES/Module 3/Generate Main Scene")]
    public static void Generate()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildLighting();
        BuildGround();
        BuildManagers();
        BuildNeighbourhood();
        BuildPlayer();

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        RegisterInBuildSettings();

        Debug.Log($"Module 3: generated {ScenePath}. The neighbourhood itself is built at runtime, "
                  + "so this scene stays small and two people can work on the module at once.");
    }

    // -------------------------------------------------------------------------------------

    private static void BuildLighting()
    {
        var sun = new GameObject("Directional Light");
        var light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(48f, 150f, 0f);
    }

    private static void BuildGround()
    {
        // A plain plane under the lanes. Real ground art is a later job; nothing binds to this.
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(12f, 1f, 12f);
        ground.transform.position = new Vector3(18f, 0f, 8f);
        ground.isStatic = true;
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
        Assign(so, "containerPrefab", Container);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildPlayer()
    {
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 1.6f, -4f);

        var camera = player.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 200f;
        player.AddComponent<AudioListener>();
        player.tag = "MainCamera";

        // Keyboard driver so a whole session can be played without a headset. It compiles out of
        // a release build, so leaving it in the scene costs nothing.
        player.AddComponent<M3DebugDriver>();
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
