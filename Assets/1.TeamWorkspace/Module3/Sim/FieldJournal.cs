using System.Collections.Generic;
using System.Text;

namespace Aedes.Module3.Sim
{
    /// <summary>
    /// The squad's record of the session, for the facilitator to read from at the debrief.
    ///
    /// Section 9: the journal "is not paperwork bolted on afterwards; it is how the squad plays."
    /// Section 5 gives it its last line: "Why did the outbreak stop, or why did it not?" - which
    /// the Analyst answers in the squad's own words and the facilitator reads aloud. A class that
    /// has understood will say some version of the sentence the module exists to teach.
    /// </summary>
    public sealed class FieldJournalExport
    {
        public int Seed;
        public int Days;
        public readonly List<string> Turns = new List<string>();
        public readonly List<string> Chains = new List<string>();
        public readonly List<string> Unvisited = new List<string>();

        public Scorecard Score;
        public OutbreakMap Map;
        public CounterfactualComparison Counterfactual;

        /// <summary>The closing prompt. Deliberately the last thing on the page.</summary>
        public const string ClosingPrompt = "Why did the outbreak stop, or why did it not?";

        /// <summary>
        /// The score line's player-facing text. The Sim assembly cannot reach the localization
        /// table, so the caller passes a lookup; the journal used to print the raw enum
        /// ("ReferredCorrectly x2"). Without a lookup the enum name is split into words.
        /// </summary>
        private static string Label(ScoreLine l, System.Func<string, string> text)
        {
            string t = text != null && !string.IsNullOrEmpty(l.MessageKey) ? text(l.MessageKey) : null;
            if (!string.IsNullOrEmpty(t) && t != l.MessageKey) return t;
            string name = l.Reason.ToString();
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i])) sb.Append(' ');
                sb.Append(i > 0 ? char.ToLowerInvariant(name[i]) : name[i]);
            }
            return sb.ToString();
        }

        public string ToPlainText(System.Func<string, string> text = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SCIENTIFIC FIELD JOURNAL - Module 3");
            sb.AppendLine($"Neighbourhood {Seed}, {Days} days");
            sb.AppendLine();

            sb.AppendLine("WHAT WE DID");
            for (int i = 0; i < Turns.Count; i++) sb.AppendLine("  " + Turns[i]);
            sb.AppendLine();

            sb.AppendLine("HOW EACH CASE HAPPENED");
            if (Chains.Count == 0) sb.AppendLine("  No new cases after we arrived.");
            for (int i = 0; i < Chains.Count; i++) sb.AppendLine("  " + Chains[i]);
            sb.AppendLine();

            if (Unvisited.Count > 0)
            {
                sb.AppendLine("HOUSES WE NEVER GOT TO");
                for (int i = 0; i < Unvisited.Count; i++) sb.AppendLine("  " + Unvisited[i]);
                sb.AppendLine();
            }

            if (Map != null)
            {
                sb.AppendLine("THE OUTBREAK");
                sb.AppendLine($"  {Map.TotalCases} people fell ill in all; {Map.SecondaryCases} caught it here.");
                sb.AppendLine($"  {Map.PreventableCases} of those came from someone we could see was unwell "
                              + "and who had no net.");
                sb.AppendLine($"  {Map.CasesFromInvisibleSources} came from someone who never looked ill at all.");
                if (Map.HouseholdsWhereTheChainStopped.Count > 0)
                {
                    sb.AppendLine($"  We stopped the chain in {Map.HouseholdsWhereTheChainStopped.Count} house(s).");
                }
                sb.AppendLine();
            }

            if (Counterfactual != null)
            {
                sb.AppendLine("IF THE FIRST PATIENT HAD BEEN COVERED ON DAY ONE");
                sb.AppendLine(Counterfactual.SquadMatchedIt
                    ? "  The same as what you did. You covered them."
                    : $"  {Counterfactual.CasesAvoided} fewer people would have caught it.");
                sb.AppendLine();
            }

            if (Score != null)
            {
                sb.AppendLine("WHAT WENT WELL");
                for (int i = 0; i < Score.Lines.Count; i++)
                {
                    var l = Score.Lines[i];
                    string label = Label(l, text);
                    sb.AppendLine(l.IsNote
                        ? $"  - {label} x{l.Count} (something to talk about, not a mark against you)"
                        : $"  - {label} x{l.Count}");
                }
                sb.AppendLine();
            }

            sb.AppendLine(ClosingPrompt);
            sb.AppendLine("  ______________________________________________________________");
            return sb.ToString();
        }
    }

    public static class FieldJournal
    {
        public static FieldJournalExport Build(Neighbourhood n, Session session,
                                               CounterfactualComparison counterfactual = null)
        {
            var export = new FieldJournalExport
            {
                Seed = n.Seed,
                Days = n.Day,
                Map = OutbreakMap.Build(n),
                Score = Scoring.Evaluate(n, session),
                Counterfactual = counterfactual,
            };

            for (int t = 0; t < session.Journal.Count; t++)
            {
                var turn = session.Journal[t];
                int done = 0;
                for (int a = 0; a < turn.Actions.Count; a++)
                {
                    if (turn.Actions[a].Outcome != ActionOutcome.Invalid) done++;
                }
                export.Turns.Add($"Day {turn.DayStart}-{turn.DayEnd}: visited "
                                 + $"{turn.HouseholdsVisited.Count} house(s), did {done} thing(s).");
            }

            var events = n.Log.Events;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.IsIndexCase)
                {
                    export.Chains.Add($"Day {e.Day}: someone in house {e.TargetHouseholdId} was already ill "
                                      + "when we arrived.");
                    continue;
                }

                string how = e.SourceWasVisiblyIll
                    ? (e.SourceWasProtected
                        ? "from a patient who was under a net"
                        : "from a patient we could see was unwell, with no net up")
                    : "from someone who never looked ill";

                string from = e.SourceHouseholdId == e.TargetHouseholdId
                    ? "from someone in the same house"
                    : $"from house {e.SourceHouseholdId}";
                export.Chains.Add($"Day {e.Day}: house {e.TargetHouseholdId} caught it {from}, {how}.");
            }

            for (int i = 0; i < n.Households.Count; i++)
            {
                if (n.Households[i].VisitCount == 0) export.Unvisited.Add($"House {n.Households[i].Id}");
            }

            return export;
        }
    }
}
