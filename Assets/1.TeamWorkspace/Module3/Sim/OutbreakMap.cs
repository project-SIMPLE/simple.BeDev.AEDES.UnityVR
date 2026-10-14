using System.Collections.Generic;

namespace Aedes.Module3.Sim
{
    public sealed class ChainLink
    {
        public int FromHouseholdId;
        public int ToHouseholdId;
        public int Day;
        public bool Preventable;
        public bool FromAnInvisibleSource;
    }

    public sealed class HouseholdOutcome
    {
        public int HouseholdId;
        public int Cases;
        public bool AnyoneWasCovered;
        public bool Visited;
        /// <summary>Cases that left this house and infected someone else.</summary>
        public int CasesItPassedOn;
    }

    /// <summary>
    /// The neighbourhood at the end, showing every chain that ran and every chain that was
    /// stopped. Section 5: "A squad that netted the first patient on day one sees a map with
    /// almost nothing on it, and should understand exactly why."
    /// </summary>
    public sealed class OutbreakMap
    {
        public readonly List<ChainLink> Links = new List<ChainLink>();
        public readonly List<HouseholdOutcome> Households = new List<HouseholdOutcome>();

        public int TotalCases;
        public int SecondaryCases;
        public int PreventableCases;
        public int CasesFromInvisibleSources;

        /// <summary>
        /// Households the squad covered someone in, and where the chain stopped there. This is
        /// the thing to draw brightly: the outbreak that did not happen.
        /// </summary>
        public readonly List<int> HouseholdsWhereTheChainStopped = new List<int>();

        /// <summary>
        /// The one chain that began where nobody was ever visibly ill. Section 5 hangs the
        /// bridge back to Module 2 on it: "The debrief asks the squad what could have stopped
        /// that chain. Every answer they try fails until they reach the only true one."
        ///
        /// -1 when no such chain ran this session. That is commonest for a squad that played
        /// well, because they have fewer chains of every kind - so the debrief falls back to
        /// <see cref="HouseholdsWithAnInvisibleCase"/>, which makes the same point.
        /// </summary>
        public int InvisibleChainHouseholdId = -1;

        /// <summary>
        /// Houses where somebody caught it and never felt a thing, whether or not they passed it
        /// on. Section 5: "the neighbourhood therefore contains sources the volunteer can never
        /// find, because there is nothing to see."
        ///
        /// This is the evidence the debrief can always show. A chain from an invisible source is
        /// the sharper version of the moment, but it does not happen every session, and the
        /// lesson - you cannot net a person who does not know they are infected - does not
        /// depend on one having run.
        /// </summary>
        public readonly List<int> HouseholdsWithAnInvisibleCase = new List<int>();

        public int InvisibleCases;

        /// <summary>True when the debrief has something to show for the Module 2 bridge.</summary>
        public bool HasInvisibleEvidence =>
            InvisibleChainHouseholdId >= 0 || HouseholdsWithAnInvisibleCase.Count > 0;

        public static OutbreakMap Build(Neighbourhood n)
        {
            var map = new OutbreakMap();

            for (int i = 0; i < n.Households.Count; i++)
            {
                var h = n.Households[i];
                bool covered = false;
                for (int r = 0; r < h.ResidentIds.Count; r++)
                {
                    var p = n.PersonById(h.ResidentIds[r]);
                    if (p != null && p.HasNet) { covered = true; break; }
                }

                map.Households.Add(new HouseholdOutcome
                {
                    HouseholdId = h.Id,
                    AnyoneWasCovered = covered,
                    Visited = h.VisitCount > 0,
                });
            }

            // People who were infected and never looked ill. Findable only in hindsight, which is
            // exactly the point the debrief makes with them.
            for (int i = 0; i < n.People.Count; i++)
            {
                var p = n.People[i];
                if (!p.HasBeenInfected || !p.IsAsymptomatic) continue;
                map.InvisibleCases++;
                if (!map.HouseholdsWithAnInvisibleCase.Contains(p.HouseholdId))
                {
                    map.HouseholdsWithAnInvisibleCase.Add(p.HouseholdId);
                }
            }

            var events = n.Log.Events;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                map.TotalCases++;
                map.Households[e.TargetHouseholdId].Cases++;

                if (e.IsIndexCase) continue;

                map.SecondaryCases++;
                if (e.WasPreventable) map.PreventableCases++;
                if (!e.SourceWasVisiblyIll)
                {
                    map.CasesFromInvisibleSources++;
                    if (map.InvisibleChainHouseholdId < 0) map.InvisibleChainHouseholdId = e.SourceHouseholdId;
                }

                if (e.SourceHouseholdId >= 0 && e.SourceHouseholdId < map.Households.Count)
                {
                    map.Households[e.SourceHouseholdId].CasesItPassedOn++;
                }

                map.Links.Add(new ChainLink
                {
                    FromHouseholdId = e.SourceHouseholdId,
                    ToHouseholdId = e.TargetHouseholdId,
                    Day = e.Day,
                    Preventable = e.WasPreventable,
                    FromAnInvisibleSource = !e.SourceWasVisiblyIll,
                });
            }

            // A household that had a case, where somebody was covered, and which never passed the
            // virus on. That is a chain the squad stopped.
            for (int i = 0; i < map.Households.Count; i++)
            {
                var o = map.Households[i];
                if (o.Cases > 0 && o.AnyoneWasCovered && o.CasesItPassedOn == 0)
                {
                    map.HouseholdsWhereTheChainStopped.Add(o.HouseholdId);
                }
            }

            return map;
        }
    }

    /// <summary>
    /// The squad's outbreak beside the one that would have happened had the first patient been
    /// covered on day one. Section 5 puts this before any score is shown.
    /// </summary>
    public sealed class CounterfactualComparison
    {
        public OutbreakMap AsPlayed;
        public OutbreakMap HadPatientZeroBeenCovered;

        public int CasesAvoided => AsPlayed.SecondaryCases - HadPatientZeroBeenCovered.SecondaryCases;

        /// <summary>
        /// True when the squad already did as well as the replay. Section 5: "A squad that did so
        /// sees their own outcome confirmed."
        /// </summary>
        public bool SquadMatchedIt => CasesAvoided <= 0;

        public string HeadlineKey => SquadMatchedIt
            ? "m3.replay.youAlreadyDidThis"
            : "m3.replay.thisNeedNotHaveHappened";
    }

    /// <summary>
    /// What the facilitator puts to the squad about the cases nobody could see. Section 5: "The
    /// debrief asks the squad what could have stopped that chain. Every answer they try fails
    /// until they reach the only true one - fewer mosquitoes, which was Module 2."
    /// </summary>
    public static class InvisibleCaseDebrief
    {
        public static string PromptKeyFor(OutbreakMap map)
        {
            if (map == null || !map.HasInvisibleEvidence) return "m3.debrief.noInvisibleEvidence";
            return map.InvisibleChainHouseholdId >= 0
                ? "m3.debrief.chainFromNowhere"
                : "m3.debrief.peopleWhoNeverKnew";
        }
    }
}
