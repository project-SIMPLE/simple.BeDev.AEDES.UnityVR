using System.Collections.Generic;

namespace Aedes.Module3.Sim
{
    /// <summary>One beat of the trace-back replay, in the order the camera visits it.</summary>
    public struct TraceBackStep
    {
        public string MessageKey;
        public int HouseholdId;
        public int PersonId;
        public int ContainerId;
        public int Day;
    }

    /// <summary>
    /// Why this person fell ill, shown rather than explained.
    ///
    /// Section 5: "When a new case appears, the game traces it back. A short replay shows the
    /// house it came from, the container the mosquito came from, and the night the sick person
    /// slept unprotected."
    ///
    /// Every field this needs was recorded at the moment of infection, so building one is a read
    /// rather than a reconstruction. That is the whole reason mosquitoes are individuals.
    /// </summary>
    public sealed class TraceBackReplay
    {
        public int NewCasePersonId;
        public int NewCaseHouseholdId;
        public int Day;
        public bool WasPreventable;
        public bool SourceWasInvisible;
        public readonly List<TraceBackStep> Steps = new List<TraceBackStep>();

        /// <summary>The single line the Analyst writes in the Field Journal.</summary>
        public string SummaryKey;
    }

    public static class TraceBack
    {
        public static TraceBackReplay Build(Neighbourhood n, TransmissionEvent e)
        {
            if (e == null) return null;

            var replay = new TraceBackReplay
            {
                NewCasePersonId = e.TargetPersonId,
                NewCaseHouseholdId = e.TargetHouseholdId,
                Day = e.Day,
                WasPreventable = e.WasPreventable,
                SourceWasInvisible = !e.IsIndexCase && !e.SourceWasVisiblyIll,
            };

            if (e.IsIndexCase)
            {
                // The outbreak the squad arrived into. There is nothing behind it to show.
                replay.SummaryKey = "m3.trace.arrivedIll";
                replay.Steps.Add(new TraceBackStep
                {
                    MessageKey = "m3.trace.arrivedIll",
                    HouseholdId = e.TargetHouseholdId,
                    PersonId = e.TargetPersonId,
                    Day = e.Day,
                });
                return replay;
            }

            // 1. The water the mosquito came out of.
            replay.Steps.Add(new TraceBackStep
            {
                MessageKey = "m3.trace.fromThisContainer",
                HouseholdId = e.SourceHouseholdId,
                ContainerId = e.OriginContainerId,
                Day = e.Day,
            });

            // 2. The person it fed on, and whether anyone could have known.
            replay.Steps.Add(new TraceBackStep
            {
                MessageKey = e.SourceWasVisiblyIll
                    ? (e.SourceWasProtected ? "m3.trace.fedOnCoveredPatient" : "m3.trace.fedOnUncoveredPatient")
                    : "m3.trace.fedOnSomeoneWhoSeemedWell",
                HouseholdId = e.SourceHouseholdId,
                PersonId = e.SourcePersonId,
                Day = e.Day,
            });

            // 3. Where it ended up.
            replay.Steps.Add(new TraceBackStep
            {
                MessageKey = e.SourceHouseholdId == e.TargetHouseholdId
                    ? "m3.trace.bitSomeoneInTheSameHouse"
                    : "m3.trace.bitSomeoneNextDoor",
                HouseholdId = e.TargetHouseholdId,
                PersonId = e.TargetPersonId,
                Day = e.Day,
            });

            replay.SummaryKey = e.WasPreventable ? "m3.trace.summaryPreventable"
                              : replay.SourceWasInvisible ? "m3.trace.summaryInvisible"
                              : "m3.trace.summaryCovered";
            return replay;
        }

        /// <summary>Every replay for the cases that appeared during one handover.</summary>
        public static List<TraceBackReplay> ForBrief(Neighbourhood n, HandoverBrief brief)
        {
            var list = new List<TraceBackReplay>();
            if (brief == null) return list;
            for (int i = 0; i < brief.NewCases.Count; i++) list.Add(Build(n, brief.NewCases[i]));
            return list;
        }
    }
}
