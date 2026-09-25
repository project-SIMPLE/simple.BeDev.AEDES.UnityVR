using System;
using System.Collections.Generic;
using Aedes.Module3.Sim;
using UnityEngine;

/// <summary>
/// Holds the simulation parameters in force, and takes delivery of new ones from GAMA while a
/// session is running.
///
/// Design section 5 asks for the mosquito population to be "driven by the GAMA model rather than
/// scripted", and open question 3b asks how the state of the yards carries over from Module 2.
/// Both arrive as a CSV that GAMA generates; this is the Unity side of that handover.
///
/// Three rules govern anything that arrives mid-session, and they exist because the thing on the
/// other end of this is a classroom:
///
///   1. A file that fails validation is rejected WHOLE. Never half-apply a broken parameter set -
///      a session running on a mixture of old and new numbers is worse than one running on old
///      numbers, because nobody can tell which they are looking at.
///   2. Changes take effect from the next simulated day, never retroactively. People already
///      infected keep the course of illness they were given, or the trace-back replay becomes a
///      lie about what the squad actually saw.
///   3. Keys that would rebuild the neighbourhood (plot count, what Module 2 left behind) are
///      held for the next session rather than applied under the player's feet.
/// </summary>
public class Module3Parameters : MonoBehaviour
{
    public static Module3Parameters Instance { get; private set; }

    /// <summary>The CSV baked into the build, used until GAMA says otherwise.</summary>
    private const string DefaultCsvResource = "Module3/Module3Parameters";

    /// <summary>What GAMA labels the payload with (the first key of its json_output contents).</summary>
    public const string GamaMessageKey = "module3_params";

    [SerializeField] private bool logOnReceive = true;

    /// <summary>The set in force right now, including anything GAMA has sent this session.</summary>
    public ParameterSet Current { get; private set; } = new ParameterSet();

    /// <summary>
    /// Structural changes waiting for the next session, because they cannot be applied to a
    /// neighbourhood that already exists.
    /// </summary>
    public ParameterSet PendingNextSession { get; private set; }

    /// <summary>Everything that has arrived this session, for the Field Journal and the debrief.</summary>
    public readonly List<string> DeliveryLog = new List<string>();

    public event Action<ParameterSet> OnParametersApplied;
    public event Action<ParameterSet> OnParametersHeldForNextSession;
    public event Action<ParameterSet> OnParametersRejected;

    /// <summary>Set by the session once a neighbourhood exists, so changes can be staged onto it.</summary>
    public Neighbourhood LiveNeighbourhood { get; set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadBakedDefaults();
    }

    private void LoadBakedDefaults()
    {
        TextAsset csv = Resources.Load<TextAsset>(DefaultCsvResource);
        if (csv == null)
        {
            // Not fatal: Module3Config's own defaults are a complete, playable set. The module
            // must still run on a headset that has never seen GAMA.
            Debug.LogWarning($"Module 3: no baked parameter file at 'Resources/{DefaultCsvResource}'. "
                             + "Falling back to the built-in defaults.");
            Current = new ParameterSet { Origin = "built-in defaults" };
            return;
        }

        var parsed = ParameterCsv.Parse(csv.text, "baked CSV");
        if (!parsed.IsValid)
        {
            Debug.LogError("Module 3: the baked parameter file is invalid, using built-in defaults instead:\n  "
                           + string.Join("\n  ", parsed.Errors));
            Current = new ParameterSet { Origin = "built-in defaults" };
            return;
        }

        Current = parsed;
        Record($"loaded {parsed.PresentKeys.Count} parameters from the baked CSV");
    }

    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Takes delivery of a parameter CSV. Safe to call at any point in a session; the routing
    /// rules above decide what actually happens.
    /// </summary>
    public bool ReceiveCsv(string csvText, string origin)
    {
        if (string.IsNullOrEmpty(csvText))
        {
            Debug.LogWarning($"Module 3: empty parameter payload from {origin}, ignored.");
            return false;
        }

        // Layered on what is in force, not on the build's defaults: GAMA is free to send only
        // the handful of keys that changed.
        var incoming = ParameterCsv.Parse(csvText, origin, Current);

        if (!incoming.IsValid)
        {
            string why = string.Join("\n  ", incoming.Errors);
            Debug.LogError($"Module 3: parameters from {origin} were REJECTED and nothing has changed:\n  {why}");
            Record($"rejected a file from {origin} ({incoming.Errors.Count} problems)");
            OnParametersRejected?.Invoke(incoming);
            return false;
        }

        if (incoming.UnknownKeys.Count > 0)
        {
            // Not a failure: a newer GAMA model may know about keys this build does not.
            Debug.LogWarning($"Module 3: ignoring {incoming.UnknownKeys.Count} unrecognised parameter(s) from "
                             + $"{origin}: {string.Join(", ", incoming.UnknownKeys)}");
        }

        bool midSession = LiveNeighbourhood != null;
        bool structural = ParameterCsv.RequiresRestart(incoming.PresentKeys, out List<string> blocking);

        if (midSession && structural)
        {
            // Applied now, this would rebuild the neighbourhood around a player who is standing
            // in it. Hold the whole set until the next session instead.
            PendingNextSession = incoming;
            Debug.LogWarning($"Module 3: parameters from {origin} include {blocking.Count} setting(s) that "
                             + $"cannot change mid-session ({string.Join(", ", blocking)}). "
                             + "They will be used when the next neighbourhood is built.");
            Record($"held a file from {origin} for the next session ({string.Join(", ", blocking)})");
            OnParametersHeldForNextSession?.Invoke(incoming);
            return false;
        }

        Current = incoming;

        if (midSession)
        {
            // From the next day, never retroactively.
            LiveNeighbourhood.StageParameters(incoming.Config, origin);
            Record($"applied {incoming.PresentKeys.Count} parameters from {origin} "
                   + $"from day {LiveNeighbourhood.Day + 1}");
        }
        else
        {
            Record($"applied {incoming.PresentKeys.Count} parameters from {origin} before the session started");
        }

        if (logOnReceive)
        {
            Debug.Log($"Module 3: applied parameters from {origin}"
                      + (string.IsNullOrEmpty(incoming.SourceRunId) ? "" : $" (GAMA run {incoming.SourceRunId})")
                      + $": {string.Join(", ", incoming.PresentKeys)}");
        }

        OnParametersApplied?.Invoke(incoming);
        return true;
    }

    /// <summary>
    /// Called when a new neighbourhood is about to be built. Anything GAMA sent that had to wait
    /// for a rebuild takes effect here.
    /// </summary>
    public ParameterSet TakeForNewSession()
    {
        if (PendingNextSession != null)
        {
            Current = PendingNextSession;
            PendingNextSession = null;
            Record("applied the parameters that were waiting for a new session");
        }
        return Current;
    }

    private void Record(string what)
    {
        DeliveryLog.Add($"[{DateTime.Now:HH:mm:ss}] {what}");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
