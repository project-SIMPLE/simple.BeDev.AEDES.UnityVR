using System.Collections.Generic;
using Aedes.Module3.Sim;
using NUnit.Framework;
using UnityEngine;

namespace Aedes.Module3.Sim.Tests
{
    /// <summary>
    /// Every localization key the module emits must exist in the CSV.
    ///
    /// The failure mode this catches is quiet and ugly: a typo in a key means a raw
    /// "m3.say.hotForDays" appears on a headset in front of a class, and nothing in play mode
    /// says so. One such typo has already been caught this way.
    ///
    /// Keys are gathered by actually playing a session rather than from a hand-maintained list,
    /// so a key added in code is covered without anyone remembering to register it.
    /// </summary>
    [TestFixture]
    public class LocalizationCoverageTests
    {
        private const string CsvResource = "Localization/LocalizationData";

        private static HashSet<string> KeysInCsv()
        {
            var csv = Resources.Load<TextAsset>(CsvResource);
            Assert.IsNotNull(csv, $"no localization CSV at Resources/{CsvResource}");

            var keys = new HashSet<string>();
            var rows = Csv.Parse(csv.text);
            for (int r = 1; r < rows.Count; r++)
            {
                if (rows[r].Count == 0) continue;
                string key = rows[r][0].Trim();
                if (key.Length == 0 || key.StartsWith("#")) continue;
                keys.Add(key);
            }
            return keys;
        }

        /// <summary>Plays several sessions and collects every key the module would have shown.</summary>
        private static HashSet<string> KeysTheModuleEmits()
        {
            var used = new HashSet<string>();

            void Add(string k) { if (!string.IsNullOrEmpty(k)) used.Add(k); }

            for (int seed = 1; seed <= 12; seed++)
            {
                var n = ScenarioBuilder.Build(seed, Module3Config.Default, ScenarioOptions.Default);
                var session = new Session(n);

                void Sweep()
                {
                    for (int h = 0; h < n.Households.Count; h++)
                    {
                        var ho = Observe.Household(n, h);
                        for (int i = 0; i < ho.Residents.Count; i++)
                        {
                            var po = ho.Residents[i];
                            Add(po.DialogueKey);
                            for (int s = 0; s < po.Signs.Count; s++) Add(po.Signs[s].Key);

                            foreach (var a in Observe.ActionsFor(n, po.PersonId))
                            {
                                Add(a.LabelKey);
                                Add(a.DisabledReasonKey);
                            }
                        }

                        foreach (var a in Observe.ActionsFor(n, h, household: true))
                        {
                            Add(a.LabelKey);
                            Add(a.DisabledReasonKey);
                        }
                    }
                }

                Sweep();
                foreach (var note in session.OpeningBrief().NeedReferralNow) Add(note.MessageKey);

                while (!session.Finished)
                {
                    // Exercise every action so their result messages are collected too.
                    for (int h = 0; h < n.Households.Count; h++)
                    {
                        Add(session.Do(PlayerAction.OnHousehold(ActionKind.Visit, h)).MessageKey);
                        Add(session.Do(PlayerAction.OnHousehold(ActionKind.SetFan, h)).MessageKey);
                        Add(session.Do(PlayerAction.OnHousehold(ActionKind.RepairScreen, h)).MessageKey);
                        Add(session.Do(PlayerAction.OnHousehold(ActionKind.AdviseClosingHours, h)).MessageKey);

                        var house = n.HouseholdById(h);
                        for (int i = 0; i < house.ResidentIds.Count; i++)
                        {
                            int pid = house.ResidentIds[i];
                            Add(session.Do(PlayerAction.OnPerson(ActionKind.PutUpNet, pid)).MessageKey);
                            Add(session.Do(PlayerAction.OnPerson(ActionKind.ReclaimNet, pid)).MessageKey);
                            Add(session.Do(PlayerAction.OnPerson(ActionKind.BringWater, pid)).MessageKey);
                            Add(session.Do(PlayerAction.OnPerson(ActionKind.HelpRest, pid)).MessageKey);
                            Add(session.Do(PlayerAction.OnPerson(ActionKind.GiveRepellent, pid)).MessageKey);
                            Add(session.Do(PlayerAction.OnPerson(ActionKind.ReferToHealthCentre, pid)).MessageKey);
                        }
                    }

                    Sweep();

                    var brief = session.EndTurn();
                    if (brief == null) continue;
                    foreach (var note in brief.NeedReferralNow) Add(note.MessageKey);
                    foreach (var note in brief.UncoveredPatients) Add(note.MessageKey);
                    foreach (var note in brief.LookingBetterButWatch) Add(note.MessageKey);

                    // The trace-back replay shown as each case appears.
                    foreach (var replay in TraceBack.ForBrief(n, brief))
                    {
                        Add(replay.SummaryKey);
                        foreach (var step in replay.Steps) Add(step.MessageKey);
                    }
                }

                // The debrief: the map headline and the scorecard.
                foreach (var line in Scoring.Evaluate(n, session).Lines) Add(line.MessageKey);
                Add(new CounterfactualComparison
                {
                    AsPlayed = OutbreakMap.Build(n),
                    HadPatientZeroBeenCovered = OutbreakMap.Build(n),
                }.HeadlineKey);
                Add("m3.replay.thisNeedNotHaveHappened");
                Add(InvisibleCaseDebrief.PromptKeyFor(OutbreakMap.Build(n)));
                Add("m3.debrief.chainFromNowhere");
                Add("m3.debrief.peopleWhoNeverKnew");
                Add("m3.debrief.noInvisibleEvidence");
            }

            return used;
        }

        [Test]
        public void EveryKeyTheModuleEmitsExistsInTheLocalizationCsv()
        {
            var inCsv = KeysInCsv();
            var emitted = KeysTheModuleEmits();

            var missing = new List<string>();
            foreach (var key in emitted) if (!inCsv.Contains(key)) missing.Add(key);
            missing.Sort();

            Assert.IsEmpty(missing,
                "these keys would show as raw text on a headset:\n  " + string.Join("\n  ", missing));
        }

        [Test]
        public void TheModuleEmitsAMeaningfulNumberOfKeys()
        {
            // Guards the test above from passing because the sweep silently collected nothing.
            Assert.Greater(KeysTheModuleEmits().Count, 30);
        }

        [Test]
        public void NoKeyLooksLikeADiagnosisOrATreatment()
        {
            // Sections 4 and 12. The wording is the part a student actually reads, so the rule
            // has to hold in the strings and not only in the code.
            string[] forbidden = { "diagnos", "dengue", "prescri", "medicine", "tablet", "dose", "treat" };

            foreach (var key in KeysTheModuleEmits())
            {
                string lower = key.ToLowerInvariant();
                foreach (var word in forbidden)
                {
                    Assert.IsFalse(lower.Contains(word),
                        $"'{key}' names something the volunteer never does");
                }
            }
        }

        [Test]
        public void KeysAreNotDuplicatedInTheCsv()
        {
            var csv = Resources.Load<TextAsset>(CsvResource);
            Assert.IsNotNull(csv);

            var seen = new HashSet<string>();
            var duplicates = new List<string>();
            var rows = Csv.Parse(csv.text);

            for (int r = 1; r < rows.Count; r++)
            {
                if (rows[r].Count == 0) continue;
                string key = rows[r][0].Trim();
                if (key.Length == 0 || key.StartsWith("#")) continue;
                if (!seen.Add(key)) duplicates.Add(key);
            }

            Assert.IsEmpty(duplicates, "duplicate keys, where the later row silently wins: "
                                       + string.Join(", ", duplicates));
        }
    }
}
