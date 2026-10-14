using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Configures the new villager FBX (Tools/Module3/make_villagers.py) as Unity Humanoid rigs
/// and builds their prefabs, addressing Documentation/Module3-Animation-Brief.md §7 ("Age
/// variety -- a question, not a task"): genuinely child- and elder-proportioned bodies, not
/// the uniform-scale hack M3NeighbourhoodBuilder used until now.
///
/// This is the character equivalent of Module3PlaceholderPrefabs.cs, with one extra step
/// that script never needed: configuring a Humanoid Avatar. That is done through the
/// ModelImporter.humanDescription API rather than by hand-writing the ~150-line
/// humanDescription YAML a human-configured .fbx.meta carries -- scripting it through the
/// importer is what Unity does when someone clicks "Configure Avatar" and hits Apply, so it
/// is the reliable path in a headless pipeline with no Editor GUI.
///
/// Re-running is idempotent: reimporting with the same HumanDescription and saving over the
/// same prefab paths changes no GUID and no scene reference.
///
/// Verify measures what the prefab actually does, not what the exporter said: baked-mesh
/// height, which way the feet point (villagers are aimed with LookRotation, so +Z must be
/// forward), and whether a shared Humanoid clip really drives the new skeleton.
/// </summary>
public static class Module3VillagerPrefabs
{
    private const string MenuBase = "AEDES/Module 3/";
    private const string BuildPath = MenuBase + "Build Villager Prefabs";
    private const string VerifyPath = MenuBase + "Verify Villager Contract";

    private const string Models = "Assets/1.TeamWorkspace/Team Assets/Models";
    private const string Prefabs = "Assets/1.TeamWorkspace/Team Assets/Prefabs/Character";
    private const string Materials = "Assets/1.TeamWorkspace/Team Assets/Materials";

    /// <summary>The two plain eye bars, one material shared by all three characters.</summary>
    private const string EyeMaterial = Materials + "/M_Villager_Eye.mat";

    /// <summary>
    /// The existing NPC controller. Its default state plays the "idle" clip that lives inside
    /// SK_Character.fbx -- a Humanoid import, so it is a muscle-space clip and retargets onto
    /// any Humanoid avatar, including these. That is Animation Brief §7 option 2's promise
    /// ("all clips retarget to all bodies with no extra animation work") kept concretely.
    /// </summary>
    private const string Controller = Models + "/SK_Character/NpcAnimationController.controller";

    /// <summary>
    /// The 17 bones make_villagers.py authors on every new skeleton, mapped to Unity's
    /// Humanoid bone names. Humanoid retargeting goes through this muscle-space map, not
    /// shared bone names, so these need not match SK_Character's own (typo'd) names.
    /// </summary>
    private static readonly (string boneName, string humanName)[] BoneMap =
    {
        ("Hips", "Hips"), ("Spine", "Spine"), ("Head", "Head"),
        ("Shoulder.L", "LeftShoulder"), ("UpperArm.L", "LeftUpperArm"),
        ("LowerArm.L", "LeftLowerArm"), ("Hand.L", "LeftHand"),
        ("Shoulder.R", "RightShoulder"), ("UpperArm.R", "RightUpperArm"),
        ("LowerArm.R", "RightLowerArm"), ("Hand.R", "RightHand"),
        ("UpperLeg.L", "LeftUpperLeg"), ("LowerLeg.L", "LeftLowerLeg"), ("Foot.L", "LeftFoot"),
        ("UpperLeg.R", "RightUpperLeg"), ("LowerLeg.R", "RightLowerLeg"), ("Foot.R", "RightFoot"),
    };

    private class Entry
    {
        public string Model;
        public string Prefab;
        public int TriBudget;
        public float HeightM;      // expected standing height, for Verify
        public string SkinMat, SkinUnwellMat, HairMat, ClothMat, TrimMat;
        public string Note;
    }

    private static readonly Entry[] Entries =
    {
        new Entry {
            Model = Models + "/SK_Villager_Elder/SK_Villager_Elder.fbx",
            Prefab = Prefabs + "/PF_Villager_Elder.prefab",
            TriBudget = 1800, HeightM = 1.58f,
            SkinMat = Materials + "/M_Villager_Elder_Skin.mat",
            SkinUnwellMat = Materials + "/M_Villager_Elder_Skin_Unwell.mat",
            HairMat = Materials + "/M_Villager_Elder_Hair.mat",
            ClothMat = Materials + "/M_Villager_Elder_Cloth.mat",
            TrimMat = Materials + "/M_Villager_Elder_Trim.mat",
            Note = "Elder: shorter adult-scale body, sinh-style skirt, shoulder sash, hair in a low bun.",
        },
        new Entry {
            Model = Models + "/SK_Villager_Child_A/SK_Villager_Child_A.fbx",
            Prefab = Prefabs + "/PF_Villager_Child_A.prefab",
            TriBudget = 1800, HeightM = 1.10f,
            SkinMat = Materials + "/M_Villager_ChildA_Skin.mat",
            SkinUnwellMat = Materials + "/M_Villager_ChildA_Skin_Unwell.mat",
            HairMat = Materials + "/M_Villager_ChildA_Hair.mat",
            ClothMat = Materials + "/M_Villager_ChildA_Cloth.mat",
            TrimMat = Materials + "/M_Villager_ChildA_Trim.mat",
            Note = "Child: own child-proportioned skeleton, not a scaled adult. Tee, shorts, cropped hair.",
        },
        new Entry {
            Model = Models + "/SK_Villager_Child_B/SK_Villager_Child_B.fbx",
            Prefab = Prefabs + "/PF_Villager_Child_B.prefab",
            TriBudget = 1800, HeightM = 1.10f,
            SkinMat = Materials + "/M_Villager_ChildB_Skin.mat",
            SkinUnwellMat = Materials + "/M_Villager_ChildB_Skin_Unwell.mat",
            HairMat = Materials + "/M_Villager_ChildB_Hair.mat",
            ClothMat = Materials + "/M_Villager_ChildB_Cloth.mat",
            TrimMat = Materials + "/M_Villager_ChildB_Trim.mat",
            Note = "Child: same skeleton as Child_A (cheap second variant). Tunic, skirt, pigtails.",
        },
    };

    [MenuItem(BuildPath)]
    public static void Build()
    {
        AssetDatabase.Refresh();
        var log = new StringBuilder("[Module3] Villager prefabs\n");
        int built = 0, failed = 0;

        foreach (var e in Entries)
        {
            if (ConfigureHumanoid(e, log) && BuildOne(e, log)) built++;
            else failed++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        log.Append($"\n{built} built, {failed} failed.");
        if (failed > 0) Debug.LogError(log.ToString());
        else Debug.Log(log.ToString());
    }

    /// <summary>
    /// Sets Animation Type to Humanoid and builds the HumanDescription bone map via script.
    /// Verifies the resulting Avatar sub-asset is valid and human before BuildOne touches it,
    /// because a silently-invalid avatar would strand every clip that later retargets onto it.
    /// </summary>
    private static bool ConfigureHumanoid(Entry e, StringBuilder log)
    {
        var importer = AssetImporter.GetAtPath(e.Model) as ModelImporter;
        if (importer == null)
        {
            log.Append($"  MISSING MODEL {e.Model}\n");
            return false;
        }

        var bones = new HumanBone[BoneMap.Length];
        for (int i = 0; i < BoneMap.Length; i++)
        {
            bones[i] = new HumanBone
            {
                boneName = BoneMap[i].boneName,
                humanName = BoneMap[i].humanName,
                limit = new HumanLimit { useDefaultValues = true },
            };
        }

        var hd = new HumanDescription
        {
            human = bones,
            skeleton = new SkeletonBone[0],   // the importer derives this from the FBX itself
            upperArmTwist = 0.5f, lowerArmTwist = 0.5f,
            upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
            armStretch = 0.05f, legStretch = 0.05f,
            feetSpacing = 0f,
            hasTranslationDoF = false,
        };

        importer.animationType = ModelImporterAnimationType.Human;
        importer.humanDescription = hd;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        Avatar avatar = LoadAvatar(e.Model);
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            log.Append($"  AVATAR INVALID  {e.Model}" +
                       $" (found={avatar != null}, valid={avatar != null && avatar.isValid}," +
                       $" human={avatar != null && avatar.isHuman})\n");
            return false;
        }
        return true;
    }

    private static Avatar LoadAvatar(string modelPath)
    {
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            if (obj is Avatar a) return a;
        return null;
    }

    private static bool BuildOne(Entry e, StringBuilder log)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(e.Model);
        var avatar = LoadAvatar(e.Model);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Controller);
        var skin = AssetDatabase.LoadAssetAtPath<Material>(e.SkinMat);
        var skinUnwell = AssetDatabase.LoadAssetAtPath<Material>(e.SkinUnwellMat);
        var hair = AssetDatabase.LoadAssetAtPath<Material>(e.HairMat);
        var cloth = AssetDatabase.LoadAssetAtPath<Material>(e.ClothMat);
        var trim = AssetDatabase.LoadAssetAtPath<Material>(e.TrimMat);
        var eye = AssetDatabase.LoadAssetAtPath<Material>(EyeMaterial);
        if (model == null || avatar == null || controller == null
            || skin == null || skinUnwell == null || hair == null || cloth == null || trim == null || eye == null)
        {
            log.Append($"  MISSING ASSET for {e.Prefab} (model={model != null}, avatar={avatar != null}," +
                       $" controller={controller != null}, skin={skin != null}, unwell={skinUnwell != null}," +
                       $" hair={hair != null}, cloth={cloth != null}, trim={trim != null}, eye={eye != null})\n");
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
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            PrefabUtility.UnpackPrefabInstance(
                instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // Explicit null checks, not `??`: Unity overloads == null, and in the editor a missing built-in
            // component comes back as a placeholder that `??` treats as present.
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer == null)
            {
                log.Append($"  NO SkinnedMeshRenderer in {e.Model}\n");
                return false;
            }

            // Slot order is fixed by make_villagers.py's build order: Skin, Hair, Cloth, Trim, Eye.
            // Skin MUST be slot 0 -- VillagerView.Refresh() swaps it through the singular
            // `skinRenderer.sharedMaterial`, which always addresses slot 0.
            var slots = new[] { skin, hair, cloth, trim, eye };
            if (renderer.sharedMesh.subMeshCount != slots.Length)
            {
                log.Append($"  SUBMESH MISMATCH {e.Model}: mesh has {renderer.sharedMesh.subMeshCount}," +
                           $" builder expects {slots.Length}\n");
                return false;
            }
            renderer.sharedMaterials = slots;

            float height = Measure(instance).height;

            // The reference PF_CharacterV1 ships with a capsule so the villager can be pointed at.
            // Size one to this body: a fallback sized for the 1.7 m adult would stand a full 0.6 m
            // above a child's head. Its presence also tells the neighbourhood builder to add none.
            var capsule = instance.GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = instance.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.height = height;
            capsule.radius = 0.22f * height;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);

            var view = instance.GetComponent<VillagerView>();
            if (view == null) view = instance.AddComponent<VillagerView>();
            var so = new SerializedObject(view);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("skinRenderer").objectReferenceValue = renderer;
            so.FindProperty("wellSkin").objectReferenceValue = skin;
            so.FindProperty("unwellSkin").objectReferenceValue = skinUnwell;
            so.FindProperty("bodyHeight").floatValue = Mathf.Round(height * 100f) / 100f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(instance, e.Prefab, out bool ok);
            if (!ok || saved == null)
            {
                log.Append($"  SAVE FAILED    {e.Prefab}\n");
                return false;
            }

            log.Append($"  {Describe(e, instance, avatar)}\n");
            return true;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private struct Measurement
    {
        public int tris;
        public float height;       // world-space, bind pose
        public float toeZ;         // mean z of the lowest vertices; > 0 means feet point +Z
    }

    /// <summary>
    /// Bakes the skinned mesh in world space (bind pose) and measures it, so the answer includes
    /// whatever rotation/scale the FBX importer put on the armature. sharedMesh.bounds would be in
    /// mesh space and say nothing about what a scene actually sees.
    /// </summary>
    private static Measurement Measure(GameObject instance)
    {
        var m = new Measurement();
        float minY = float.MaxValue, maxY = float.MinValue;
        double toeSum = 0; int toeN = 0;

        foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) continue;
            m.tris += smr.sharedMesh.triangles.Length / 3;

            var baked = new Mesh();
            smr.BakeMesh(baked, false);
            var verts = baked.vertices;
            var t = smr.transform;
            var world = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                world[i] = t.TransformPoint(verts[i]);
                minY = Mathf.Min(minY, world[i].y);
                maxY = Mathf.Max(maxY, world[i].y);
            }
            for (int i = 0; i < world.Length; i++)
            {
                if (world[i].y < minY + 0.05f) { toeSum += world[i].z; toeN++; }
            }
            Object.DestroyImmediate(baked);
        }

        m.height = maxY > minY ? maxY - minY : 0f;
        m.toeZ = toeN > 0 ? (float)(toeSum / toeN) : 0f;
        return m;
    }

    /// <summary>
    /// Plays the shared walk clip on the skeleton and returns how far the left foot swings relative
    /// to the hips, in world metres. The shared idle is only a static pose (plus a small root sway),
    /// so it cannot prove retargeting; the walk is a real Humanoid clip with limb motion. ~0 would
    /// mean the clip is not driving this skeleton at all.
    ///
    /// Culling has to be forced off: with no visible renderer (headless) the Animator skips writing
    /// bone transforms and only applies root motion, which looks exactly like a working clip on a
    /// skeleton that is not moving.
    /// </summary>
    private static float WalkSwing(GameObject instance)
    {
        var animator = instance.GetComponent<Animator>();
        var hips = FindDeep(instance.transform, "Hips");
        var foot = FindDeep(instance.transform, "Foot.L");
        if (animator == null || hips == null || foot == null || animator.runtimeAnimatorController == null)
            return -1f;

        AnimationClip walk = null;
        foreach (var c in animator.runtimeAnimatorController.animationClips)
            if (c.name == "Armature_walk") walk = c;
        if (walk == null) return -1f;

        var culling = animator.cullingMode;
        var rootMotion = animator.applyRootMotion;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;

        AnimationPlayableUtilities.PlayClip(animator, walk, out PlayableGraph graph);
        Vector3 min = Vector3.one * 1e9f, max = -Vector3.one * 1e9f;
        for (int i = 0; i < 12; i++)
        {
            graph.Evaluate(0.06f);
            var d = foot.position - hips.position;
            min = Vector3.Min(min, d);
            max = Vector3.Max(max, d);
        }
        graph.Destroy();

        animator.cullingMode = culling;
        animator.applyRootMotion = rootMotion;
        return (max - min).magnitude;
    }

    private static string Describe(Entry e, GameObject instance, Avatar avatar)
    {
        var m = Measure(instance);
        float swing = WalkSwing(instance);

        var sb = new StringBuilder();
        sb.Append($"{Path.GetFileName(e.Prefab),-26} {m.tris,5} tris");
        sb.Append(m.tris <= e.TriBudget ? $" (budget {e.TriBudget}, ok)" : $" (OVER BUDGET {e.TriBudget})");
        sb.Append($"  height {m.height:0.###} m");
        if (Mathf.Abs(m.height - e.HeightM) > 0.03f) sb.Append($" WRONG HEIGHT, expected {e.HeightM:0.###}");
        sb.Append(m.toeZ > 0.01f ? $"  faces +Z (toe z {m.toeZ:0.###})" : $"  FACES AWAY (toe z {m.toeZ:0.###})");
        sb.Append(avatar != null && avatar.isHuman ? "  avatar: human" : "  avatar: NOT HUMAN");
        sb.Append(swing > 0.05f ? $"  walk clip retargets (foot swings {swing * 100f:0} cm)" : "  WALK CLIP DOES NOT RETARGET");
        return sb.ToString();
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
        var log = new StringBuilder("[Module3] Villager contract\n");
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

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animator = instance.GetComponent<Animator>();
                var line = Describe(e, instance, animator != null ? animator.avatar : null);
                if (line.Contains("OVER BUDGET") || line.Contains("WRONG HEIGHT") || line.Contains("NOT HUMAN")
                    || line.Contains("FACES AWAY") || line.Contains("DOES NOT RETARGET")) problems++;
                log.Append($"  {line}\n");
                if (!string.IsNullOrEmpty(e.Note)) log.Append($"      {e.Note}\n");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        log.Append($"\n{problems} problem(s).");
        if (problems > 0) Debug.LogWarning(log.ToString());
        else Debug.Log(log.ToString());
    }
}
