using System.Collections.Generic;
using Aedes.Module3.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Opens the generated scene and actually builds a neighbourhood in it.
///
/// This is not a unit test and cannot be: the builder and the views are MonoBehaviours in
/// Assembly-CSharp, and a Unity assembly definition cannot reference the predefined assemblies,
/// so the Sim test suite cannot see them. What it can do is run the real thing and report, which
/// catches the failures that matter here - an unassigned prefab reference, a missing component,
/// a builder that silently produces nothing.
///
/// Run headlessly:
///   Unity -batchmode -quit -executeMethod Module3SceneSmokeTest.Run
/// </summary>
public static class Module3SceneSmokeTest
{
    [MenuItem("AEDES/Module 3/Smoke Test Main Scene")]
    public static void Run()
    {
        var failures = new List<string>();
        EditorSceneManager.OpenScene(Module3SceneBuilder.ScenePath, OpenSceneMode.Single);

        var session = Object.FindFirstObjectByType<M3Session>();
        var parameters = Object.FindFirstObjectByType<Module3Parameters>();
        var builder = Object.FindFirstObjectByType<M3NeighbourhoodBuilder>();

        if (session == null) failures.Add("no M3Session in the scene");
        if (parameters == null) failures.Add("no Module3Parameters in the scene");
        if (builder == null) failures.Add("no M3NeighbourhoodBuilder in the scene");
        if (Object.FindFirstObjectByType<LocalizationManager>() == null)
            failures.Add("no LocalizationManager - every string would render as a raw key");
        if (Object.FindFirstObjectByType<M3Hud>() == null)
            failures.Add("no M3Hud - the Coach and the Analyst would have nothing to work from");
        if (Object.FindFirstObjectByType<M3JournalWriter>() == null)
            failures.Add("no M3JournalWriter - the facilitator gets no record of the session");

        // The scene is built in the village Modules 1 and 2 share; its houses are where the households go.
        var village = M3VillageSites.FindVillage();
        if (village == null)
            failures.Add($"no '{M3VillageSites.VillageRoot}' in the scene - it is built in the Module 2 village "
                         + "(AEDES > Module 3 > Generate Main Scene)");
        if (village != null && village.GetComponentInChildren<MonoBehaviour>(true) != null)
            failures.Add("the village still carries Module 1/2 scripts, which read managers this scene does not have");
        int villageHouses = M3NeighbourhoodBuilder.ScenePlotSites()?.Length ?? 0;
        if (village != null && villageHouses == 0)
            failures.Add("the village has no house the builder can furnish (none named after its house prefab)");

        // Only one SimulationManager subclass may exist, or they race over the static Instance.
        var simManagers = Object.FindObjectsByType<SimulationManager>(FindObjectsInactive.Include,
                                                                     FindObjectsSortMode.None);
        if (simManagers.Length > 1)
            failures.Add($"{simManagers.Length} SimulationManager subclasses in the scene; Awake order decides "
                         + "which wins (CLAUDE.md 10.4)");

        if (builder != null)
        {
            var so = new SerializedObject(builder);
            foreach (string field in new[] { "housePrefab", "bedPrefab", "villagerPrefab", "containerPrefab" })
            {
                var prop = so.FindProperty(field);
                if (prop == null || prop.objectReferenceValue == null)
                    failures.Add($"builder field '{field}' is unassigned; that part of the plot will be missing");
            }
        }

        // Build a real neighbourhood, not a mocked one.
        if (session != null && builder != null && failures.Count == 0)
        {
            parameters.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);
            session.StartSession();
            builder.Build();

            var sim = session.Neighbourhood;
            if (sim == null) failures.Add("the session produced no neighbourhood");
            else
            {
                if (builder.Households.Count != sim.Households.Count)
                    failures.Add($"built {builder.Households.Count} plots for {sim.Households.Count} households");
                if (village != null && sim.Households.Count != villageHouses)
                    failures.Add($"{sim.Households.Count} households for a village of {villageHouses} houses");
                if (builder.Villagers.Count != sim.People.Count)
                    failures.Add($"built {builder.Villagers.Count} villagers for {sim.People.Count} residents");

                // Two plots must not stand in the same place, or the neighbourhood is a single pile.
                var seen = new HashSet<Vector3>();
                foreach (var h in builder.Households)
                {
                    if (!seen.Add(h.transform.position))
                        failures.Add($"plot {h.householdId} overlaps another at {h.transform.position}");
                }

                // The ground is painted at build time (M3GroundPainter); flat colour means the paint step failed.
                // (The scene's village has its own ground, which is left alone.)
                if (village == null && builder.GetComponent<M3Surroundings>() != null)
                {
                    var painted = GameObject.Find("Ground (painted)");
                    var groundTexture = painted != null ? painted.GetComponent<Renderer>().sharedMaterial.mainTexture : null;
                    if (groundTexture == null) failures.Add("the village ground has no painted texture");
                }

                // The houses get their surface texture by material swap (M3HouseSurface); missing assets leave them smooth.
                foreach (string asset in new[] { "Module3/M_M3_House_Light", "Module3/M_M3_House_Door" })
                {
                    var houseMaterial = Resources.Load<Material>(asset);
                    if (houseMaterial == null || !houseMaterial.HasProperty("_M3DetailMap") || houseMaterial.GetTexture("_M3DetailMap") == null)
                        failures.Add($"Resources/{asset} is missing or has no detail texture (AEDES > Module 3 > Generate House Surface Assets)");
                }
                // ...and that the swap reached the houses that were actually built (M3Surroundings applies it;
                // the scene's own houses keep their look).
                if (village == null && builder.GetComponent<M3Surroundings>() != null)
                {
                    foreach (var plot in builder.Households)
                    {
                        foreach (var part in plot.GetComponentsInChildren<Transform>())
                        {
                            if (part.name != "Roof") continue;
                            if (!part.GetComponent<Renderer>().sharedMaterial.HasProperty("_M3DetailMap"))
                                failures.Add($"the house on plot {plot.householdId} kept its smooth palette material");
                        }
                    }
                }

                // Every house is furnished by M3RoomDresser; a bare one means its Resources asset is missing.
                foreach (var plot in builder.Households)
                {
                    var indoors = plot.transform.Find("Dressing/Indoors");
                    if (indoors == null || indoors.childCount < 3)
                        failures.Add($"plot {plot.householdId} has no furniture (Resources/{M3RoomDresser.ResourcePath})");
                }

                // The yards and the lane are dressed (M3VillageDressing) - fences, poles, washing, trees, hens -
                // and within a budget: a Quest draws the whole village, so the dressing is a few meshes per
                // yard, not a prop per draw call. (The scene's village is dressed already.)
                if (village == null && builder.GetComponent<M3Surroundings>() != null)
                {
                    var dressing = GameObject.Find("Village Dressing");
                    if (dressing == null) failures.Add("no Village Dressing under the surroundings: the lane is bare");
                    int dressed = 0, renderers = 0, triangles = 0;
                    var roots = new List<Transform>();
                    if (dressing != null) roots.Add(dressing.transform);
                    foreach (var plot in builder.Households)
                    {
                        var outside = plot.transform.Find("Dressing/Outside");
                        if (outside == null) continue;
                        roots.Add(outside);
                        if (outside.childCount > 0) dressed++;
                    }
                    foreach (var root in roots)
                    {
                        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                        {
                            renderers++;
                            // A static-batched tree's MeshFilter holds the whole batch (M3Surroundings.BatchStaticTrees): not its own triangles.
                            if (r.isPartOfStaticBatch) continue;
                            if (r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null) triangles += mf.sharedMesh.triangles.Length / 3;
                        }
                    }
                    if (dressed < builder.Households.Count / 2)
                        failures.Add($"only {dressed} of {builder.Households.Count} yards have any outside dressing");
                    if (renderers > 220) failures.Add($"the village dressing is {renderers} renderers (budget 220): too many draw calls for a Quest");
                    if (triangles > 120000) failures.Add($"the village dressing is {triangles} triangles (budget 120000)");
                    Debug.Log($"Module 3 smoke test: village dressing on {dressed} yards, {renderers} renderers, {triangles} triangles.");
                }

                // The whole scene stays drawable on a headset. It was 2,576 renderers before the palms were merged
                // (each was twenty), the paddies became two meshes and the side number boards were distance-culled;
                // it is about 850 now. Anything that puts it back over 1,200 has added a renderer per prop somewhere.
                // In the Module 2 village, with its two households built, the scene is about 710 renderers.
                int sceneRenderers = 0;
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (r.enabled) sceneRenderers++;
                if (sceneRenderers > 1200)
                    failures.Add($"the scene is {sceneRenderers} renderers (budget 1200): see AEDES > Module 3 > Dev > Performance Census");
                Debug.Log($"Module 3 smoke test: {sceneRenderers} renderers in the built scene.");

                // Every door leaf swings (M3DoorSwing), and none keeps a collider to shove the Pilot's body.
                foreach (var plot in builder.Households)
                {
                    var doors = plot.GetComponentsInChildren<M3DoorSwing>(true);
                    if (doors.Length == 0) failures.Add($"plot {plot.householdId} has no swinging doors");
                    foreach (var door in doors)
                        foreach (var c in door.GetComponentsInChildren<Collider>(true))
                            if (c.enabled) failures.Add($"door {door.name} on plot {plot.householdId} still has a collider");
                }

                Debug.Log($"Module 3 smoke test: seed {session.Seed}, {sim.Households.Count} households, "
                          + $"{sim.People.Count} residents, {sim.AliveMosquitoCount} mosquitoes, "
                          + $"{sim.VisiblyIll().Count} visibly ill on day one, "
                          + $"{sim.NetsRemaining} nets for {sim.Households.Count} households.");
            }
        }

        if (failures.Count == 0) Debug.Log("===SMOKE OK=== the scene builds a playable neighbourhood");
        else Debug.LogError("===SMOKE FAILED===\n  " + string.Join("\n  ", failures));
    }
}
