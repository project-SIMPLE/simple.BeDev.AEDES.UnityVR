using System;
using Aedes.Module3.Sim;
using UnityEngine;

/// <summary>
/// Drives one Module 3 session: builds the neighbourhood, runs the turn clock, and moves the
/// calendar on at each handover.
///
/// The session logic itself lives in Aedes.Module3.Sim.Session, which knows nothing about Unity
/// and is covered by tests. This class supplies the clock and the events, and nothing else -
/// keeping it thin is what lets the whole session shape be verified without entering play mode.
///
/// Section 8 on the clock: a turn is "fixed and enforced by the game rather than left to the
/// class - an untimed turn becomes one confident student playing while the other two watch,
/// which is the failure mode the squad structure exists to prevent."
/// </summary>
public class M3Session : MonoBehaviour
{
    public static M3Session Instance { get; private set; }

    [Header("Session")]
    [Tooltip("0 picks a fresh seed each session. Set a value to replay an exact neighbourhood.")]
    [SerializeField] private int seed;

    [Tooltip("Start as soon as the scene loads. Off if a menu or tutorial starts it instead.")]
    [SerializeField] private bool autoStart = true;

    [Header("Debug")]
    [SerializeField] private bool logHandovers = true;

    public Session Session { get; private set; }
    public Neighbourhood Neighbourhood => Session?.Neighbourhood;
    public int Seed { get; private set; }

    /// <summary>Seconds left in this turn. The Coach watches this on the cast, not only the Pilot.</summary>
    public float TurnSecondsRemaining { get; private set; }
    public float TurnSecondsTotal { get; private set; }
    public bool Running { get; private set; }

    /// <summary>The turn clock does not run before this time - the handover screen is still up.</summary>
    private float clockHeldUntil;

    [Tooltip("Hold each turn's clock until the Pilot says they are ready (A). In a headset the next "
             + "Pilot still has to put it on; a fixed delay either wastes their turn or starts it "
             + "while they are fumbling with the strap.")]
    [SerializeField] private bool waitForPilot = true;

    /// <summary>True while a turn is waiting for its Pilot to press ready.</summary>
    public bool WaitingForPilot { get; private set; }
    public event Action OnPilotReady;

    /// <summary>The Pilot is in the headset and ready: start the clock.</summary>
    public void PilotReady()
    {
        if (!WaitingForPilot) return;
        WaitingForPilot = false;
        OnPilotReady?.Invoke();
    }

    public event Action<int> OnTurnStarted;              // turn index
    public event Action<HandoverBrief> OnHandover;
    public event Action<HandoverBrief> OnSessionFinished;
    public event Action<ActionResult> OnActionResolved;

    private ParameterSet parameters;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (autoStart) StartSession();
    }

    public void StartSession()
    {
        // Anything GAMA sent that had to wait for a rebuild takes effect here.
        parameters = Module3Parameters.Instance != null
            ? Module3Parameters.Instance.TakeForNewSession()
            : new ParameterSet();

        Seed = seed != 0 ? seed : Environment.TickCount & 0x7FFFFFFF;

        var neighbourhood = ScenarioBuilder.Build(Seed, parameters.Config, parameters.Scenario);
        Session = new Session(neighbourhood);

        // So a mid-session parameter delivery knows what to stage onto.
        if (Module3Parameters.Instance != null) Module3Parameters.Instance.LiveNeighbourhood = neighbourhood;

        TurnSecondsTotal = parameters.Config.TurnSeconds;
        TurnSecondsRemaining = TurnSecondsTotal;
        Running = true;

        Debug.Log($"Module 3: session started, seed {Seed}, "
                  + $"{parameters.Config.Rounds} rounds x {parameters.Config.TurnsPerRound} turns of "
                  + $"{TurnSecondsTotal:0}s, parameters from {parameters.Origin}.");

        WaitingForPilot = waitForPilot;
        OnTurnStarted?.Invoke(0);
    }

    private void Update()
    {
        if (!Running) return;
        if (WaitingForPilot || Time.time < clockHeldUntil) return;

        TurnSecondsRemaining -= Time.deltaTime;
        if (TurnSecondsRemaining > 0f) return;

        EndTurn();
    }

    /// <summary>
    /// Ends the turn early. The timer is the rule, but a Pilot who has finished a household
    /// should not have to stand around waiting for it.
    /// </summary>
    public void EndTurn()
    {
        if (!Running) return;

        HandoverBrief brief = Session.EndTurn();
        if (brief == null) return;

        if (logHandovers) LogBrief(brief);

        if (brief.IsFinalHandover)
        {
            Running = false;
            if (Module3Parameters.Instance != null) Module3Parameters.Instance.LiveNeighbourhood = null;
            OnSessionFinished?.Invoke(brief);
            return;
        }

        TurnSecondsRemaining = TurnSecondsTotal;
        WaitingForPilot = waitForPilot;
        OnHandover?.Invoke(brief);
        OnTurnStarted?.Invoke(Session.TurnIndex);
    }

    /// <summary>
    /// Holds the turn clock while the handover screen is up. The clock used to start the instant
    /// the turn ended, so every incoming Pilot lost the whole handover (12 s of a 180 s turn,
    /// measured) before touching anything.
    /// </summary>
    public void HoldTurnClock(float seconds)
    {
        clockHeldUntil = Mathf.Max(clockHeldUntil, Time.time + seconds);
    }

    /// <summary>Everything the interaction layer routes through.</summary>
    public ActionResult Do(PlayerAction action)
    {
        if (Session == null) return new ActionResult { Action = action, Outcome = ActionOutcome.Invalid };

        ActionResult result = Session.Do(action);
        OnActionResolved?.Invoke(result);
        return result;
    }

    /// <summary>
    /// Section 5: before any score is shown, replay what would have happened had the first patient
    /// been covered on day one. Same seed, same squad actions, one extra net.
    /// </summary>
    public RunResult BuildCounterfactual()
    {
        if (Session == null) return default;
        return SimRunner.Counterfactual(Seed, parameters.Config, parameters.Scenario,
                                        Session.RecordedActions(), Session.PatientZeroId());
    }

    private static void LogBrief(HandoverBrief b)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"--- Handover: {b.DaysElapsed} days later (day {b.DayNow}), nets left {b.NetsRemaining}");
        if (b.NewCases.Count > 0) sb.AppendLine($"    {b.NewCases.Count} new case(s) since the last turn");
        if (b.NeedReferralNow.Count > 0) sb.AppendLine($"    {b.NeedReferralNow.Count} need a doctor NOW");
        if (b.UncoveredPatients.Count > 0) sb.AppendLine($"    {b.UncoveredPatients.Count} unwell and uncovered");
        if (b.LookingBetterButWatch.Count > 0) sb.AppendLine($"    {b.LookingBetterButWatch.Count} looking better - keep watching");
        if (b.HouseholdsNeverVisited.Count > 0) sb.AppendLine($"    {b.HouseholdsNeverVisited.Count} household(s) nobody has visited");
        Debug.Log(sb.ToString());
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
