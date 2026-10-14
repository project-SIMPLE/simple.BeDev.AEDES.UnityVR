using System.Collections.Generic;
using Aedes.Module3.Sim;
using UnityEngine;

/// <summary>
/// Walking into a house is looking in on the household (section 7 step 1). Records one Visit per
/// house per turn, the first time the Pilot's head is inside the house's footprint - or when they
/// start a conversation with someone who lives there.
/// </summary>
public class M3VisitTracker : MonoBehaviour
{
    [Tooltip("The house footprint in plot space (the Lao house: 8.55 x 6.4 m around the plot origin).")]
    [SerializeField] private Bounds footprint = new Bounds(new Vector3(0.1f, 1f, 0.2f), new Vector3(8.4f, 3f, 6.2f));

    private static M3VisitTracker instance;
    private readonly HashSet<int> visitedThisTurn = new HashSet<int>();
    private M3Session subscribed;

    private void Awake() => instance = this;

    private void Update()
    {
        var s = M3Session.Instance;
        if (s == null || s.Session == null) return;
        if (subscribed != s)
        {
            s.OnTurnStarted += _ => visitedThisTurn.Clear();
            subscribed = s;
        }
        if (!s.Running || s.WaitingForPilot || Camera.main == null) return;

        Vector3 head = Camera.main.transform.position;
        foreach (var view in M3NeighbourhoodBuilder.HouseholdViews)
        {
            if (view == null || visitedThisTurn.Contains(view.householdId)) continue;
            // The footprint is authored for the house model; the neighbourhood scales it up.
            float scale = M3NeighbourhoodBuilder.HouseScale;
            var scaled = new Bounds(footprint.center * scale, footprint.size * scale);
            if (scaled.Contains(view.transform.InverseTransformPoint(head))) Visit(view.householdId);
        }
    }

    /// <summary>Records a visit to <paramref name="householdId"/> unless one was already made this turn.</summary>
    public static void Visit(int householdId)
    {
        var s = M3Session.Instance;
        if (s == null || !s.Running) return;
        if (instance != null && !instance.visitedThisTurn.Add(householdId)) return;
        s.Do(PlayerAction.OnHousehold(ActionKind.Visit, householdId));
    }
}
