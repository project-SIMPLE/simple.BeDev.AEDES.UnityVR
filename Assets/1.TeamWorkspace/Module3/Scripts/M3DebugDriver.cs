#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using Aedes.Module3.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Plays a whole Module 3 session from the keyboard, with no headset, no art and no scene.
///
/// The point is to be able to check that the session, the handovers and the consequences work
/// long before there is anything to look at - and, later, to reproduce a bug a class hit by
/// typing the same actions against the same seed.
///
/// Editor and development builds only; it compiles out of a release APK.
/// </summary>
public class M3DebugDriver : MonoBehaviour
{
    [SerializeField] private bool showOverlay = true;

    private int selectedHousehold;
    private int selectedResident;
    private string lastMessage = "";

    private M3Session S => M3Session.Instance;

    [Tooltip("Move this camera to the lane in front of the selected house. Without it the desktop "
             + "view never moved from the spawn point, which faces house 0's wall.")]
    [SerializeField] private bool frameSelectedHouse = true;
    private int framedHousehold = -1;

    private void Update()
    {
        var k = Keyboard.current;
        if (S == null || S.Session == null) return;
        if (frameSelectedHouse && framedHousehold != selectedHousehold) FrameHousehold(selectedHousehold);
        if (k == null) return;

        if (k.tabKey.wasPressedThisFrame) CycleHousehold(k.leftShiftKey.isPressed ? -1 : 1);
        if (k.qKey.wasPressedThisFrame) CycleResident(-1);
        if (k.eKey.wasPressedThisFrame) CycleResident(1);

        if (k.vKey.wasPressedThisFrame) DoHousehold(ActionKind.Visit);
        if (k.nKey.wasPressedThisFrame) DoPerson(ActionKind.PutUpNet);
        if (k.bKey.wasPressedThisFrame) DoPerson(ActionKind.ReclaimNet);
        if (k.rKey.wasPressedThisFrame) DoPerson(ActionKind.ReferToHealthCentre);
        if (k.wKey.wasPressedThisFrame) DoPerson(ActionKind.BringWater);
        if (k.tKey.wasPressedThisFrame) DoPerson(ActionKind.HelpRest);
        if (k.gKey.wasPressedThisFrame) DoPerson(ActionKind.GiveRepellent);
        if (k.fKey.wasPressedThisFrame) DoHousehold(ActionKind.SetFan);
        if (k.cKey.wasPressedThisFrame) DoHousehold(ActionKind.RepairScreen);
        if (k.xKey.wasPressedThisFrame) DoHousehold(ActionKind.AdviseClosingHours);

        if (k.spaceKey.wasPressedThisFrame) S.EndTurn();
    }

    private void CycleHousehold(int delta)
    {
        int count = S.Neighbourhood.Households.Count;
        selectedHousehold = (selectedHousehold + delta + count) % count;
        selectedResident = 0;
    }

    /// <summary>Stand in the lane in front of the house, looking at it.</summary>
    private void FrameHousehold(int householdId)
    {
        foreach (var view in FindObjectsByType<HouseholdView>(FindObjectsSortMode.None))
        {
            if (view.householdId != householdId) continue;
            var plot = view.transform;
            transform.position = plot.position + plot.forward * 7f + Vector3.up * 1.6f;
            transform.rotation = Quaternion.LookRotation(plot.position + Vector3.up * 1.2f - transform.position);
            framedHousehold = householdId;
            return;
        }
    }

    private void CycleResident(int delta)
    {
        var h = S.Neighbourhood.HouseholdById(selectedHousehold);
        if (h == null || h.ResidentIds.Count == 0) return;
        selectedResident = (selectedResident + delta + h.ResidentIds.Count) % h.ResidentIds.Count;
    }

    private void DoHousehold(ActionKind kind)
    {
        var r = S.Do(PlayerAction.OnHousehold(kind, selectedHousehold));
        lastMessage = $"{kind} on house {selectedHousehold}: {r.Outcome}";
    }

    private void DoPerson(ActionKind kind)
    {
        var h = S.Neighbourhood.HouseholdById(selectedHousehold);
        if (h == null || selectedResident >= h.ResidentIds.Count) return;
        int personId = h.ResidentIds[selectedResident];
        var r = S.Do(PlayerAction.OnPerson(kind, personId));
        lastMessage = $"{kind} on person {personId}: {r.Outcome}";
    }

    private void OnGUI()
    {
        if (!showOverlay || S == null || S.Session == null) return;

        var sim = S.Neighbourhood;
        var h = sim.HouseholdById(selectedHousehold);

        var sb = new StringBuilder();
        sb.AppendLine($"MODULE 3  seed {S.Seed}   day {sim.Day}   "
                      + $"turn {S.Session.TurnIndex + 1}/{S.Session.TotalTurns}  "
                      + $"(round {S.Session.RoundIndex + 1}, pilot {S.Session.PilotIndex + 1})");
        sb.AppendLine($"turn time {Mathf.Max(0f, S.TurnSecondsRemaining):0}s    "
                      + $"nets left {sim.NetsRemaining}    cases {sim.TotalCases}    "
                      + $"mosquitoes {sim.AliveMosquitoCount}");
        sb.AppendLine();

        if (h != null)
        {
            sb.AppendLine($"HOUSE {h.Id}   screens {h.Screens}   fan {(h.HasFan ? "on" : "no")}   "
                          + $"containers {h.ProductiveContainerCount}   "
                          + $"{(h.VisitCount == 0 ? "NEVER VISITED" : $"last visit day {h.LastVisitedDay}")}");

            for (int i = 0; i < h.ResidentIds.Count; i++)
            {
                var p = sim.PersonById(h.ResidentIds[i]);
                string state = p.HasVisibleWarningSign(sim.Day) ? $"** {p.Warning} - DOCTOR NOW **"
                             : p.IsFebrile(sim.Day) ? "unwell, fever"
                             : p.IsDefervescing(sim.Day) ? "looking better (keep watching)"
                             : p.State == HealthState.Hospitalised ? "in hospital"
                             : "well";
                sb.AppendLine($"  {(i == selectedResident ? ">" : " ")} p{p.Id} {p.Age}  {state}"
                              + $"{(p.HasNet ? "  [net]" : "")}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("TAB house  Q/E person  V visit  N net  B take net back  R refer");
        sb.AppendLine("W water  T rest  G repellent  F fan  C fix screen  X closing hours  SPACE end turn");
        if (lastMessage.Length > 0) sb.AppendLine($"> {lastMessage}");

        GUI.Label(new Rect(12, 12, 900, 460), sb.ToString());
    }
}
#endif
