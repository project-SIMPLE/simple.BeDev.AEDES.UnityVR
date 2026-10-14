using System.Collections.Generic;

namespace Aedes.Module3.Sim
{
    /// <summary>One thing the incoming Pilot is told about a person.</summary>
    public struct PersonNote
    {
        public int PersonId;
        public int HouseholdId;
        /// <summary>Localization key for the line the Coach reads out.</summary>
        public string MessageKey;
    }

    /// <summary>
    /// What the Coach briefs the incoming Pilot with, assembled at the moment the headset changes
    /// hands.
    ///
    /// Section 8 is specific about this: "Every handover should show consequence. The point of
    /// passing the headset is that the world is different when you get it back." So this is not a
    /// score screen - it is a list of what changed, what was missed, and what nobody has looked at
    /// yet. Section 9 adds that the squad plays from the Analyst's record rather than from memory,
    /// because a few minutes is not long enough to hold a neighbourhood in your head.
    /// </summary>
    public sealed class HandoverBrief
    {
        /// <summary>The turn that just ended (0-based).</summary>
        public int TurnIndex;
        public int RoundIndex;

        /// <summary>How far the calendar jumped. Shown as plain words: "Three days later."</summary>
        public int DaysElapsed;
        public int DayNow;

        /// <summary>Cases that appeared during the jump. Each one gets a trace-back replay.</summary>
        public readonly List<TransmissionEvent> NewCases = new List<TransmissionEvent>();

        /// <summary>Visibly unwell and with no net up. The work that is waiting.</summary>
        public readonly List<PersonNote> UncoveredPatients = new List<PersonNote>();

        /// <summary>
        /// Bleeding or persistent vomiting, right now. Section 4: not a judgement call, and the
        /// strongest positive in the module if acted on.
        /// </summary>
        public readonly List<PersonNote> NeedReferralNow = new List<PersonNote>();

        /// <summary>
        /// People whose fever has broken. Section 8: "Danger often arrives as the fever comes down.
        /// A Pilot who sees someone looking better and moves on has made the mistake the module
        /// teaches, and the next Pilot inherits it."
        /// </summary>
        public readonly List<PersonNote> LookingBetterButWatch = new List<PersonNote>();

        /// <summary>Section 8: "A house nobody has visited is still a house nobody has visited,
        /// and the Coach should be saying so."</summary>
        public readonly List<int> HouseholdsNeverVisited = new List<int>();

        /// <summary>Visited once, but not lately. Returning is what makes the record worth keeping.</summary>
        public readonly List<int> HouseholdsNotSeenRecently = new List<int>();

        public int NetsRemaining;

        /// <summary>Anything GAMA changed during the turn, so the squad is not misled by a silent shift.</summary>
        public readonly List<string> ParameterChanges = new List<string>();

        public bool IsFinalHandover;

        public bool HasUrgentWork => NeedReferralNow.Count > 0 || UncoveredPatients.Count > 0;
    }
}
