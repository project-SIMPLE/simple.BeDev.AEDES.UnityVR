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
                if (builder.GetComponent<M3Surroundings>() != null)
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
                // ...and that the swap reached the houses that were actually built (M3Surroundings applies it).
                if (builder.GetComponent<M3Surroundings>() != null)
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
