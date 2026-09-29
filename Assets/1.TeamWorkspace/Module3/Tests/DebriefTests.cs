using System.Collections.Generic;
using Aedes.Module3.Sim;
using NUnit.Framework;

namespace Aedes.Module3.Sim.Tests
{
    /// <summary>
    /// The end of the session: the trace-back replays, the outbreak map, the counterfactual, and
    /// the scorecard. Section 5 calls these the moments the module is built to deliver, so they
    /// are held to the design rather than left to look right.
    /// </summary>
    [TestFixture]
    public class DebriefTests
    {
        private static Session PlayOut(int seed, IStrategy strategy = null)
        {
            var n = ScenarioBuilder.Build(seed, Module3Config.Default, ScenarioOptions.Default);
            var s = new Session(n);
            while (!s.Finished)
            {
                strategy?.OnTurn(n, s.TurnIndex);
                s.EndTurn();
            }
            return s;
        }

        // ---- trace-back ------------------------------------------------------------------

        [Test]
        public void ANetPutUpAfterTheBiteIsNotBlamedForTheCase()
        {
            // Found in a playtest journal: patients bitten before the squad arrived and netted on
            // day 0 were later reported as "a patient who was under a net", because the net was
            // read on the day the new case appeared rather than the day the mosquito fed.
            for (int seed = 1; seed < 30; seed++)
            {
                var n = ScenarioBuilder.Build(seed, Module3Config.Default, ScenarioOptions.Default);
                var watched = new HashSet<int>();
                for (int d = 0; d < 60; d++)
                {
                    n.AdvanceDay();
                    foreach (var m in n.Mosquitoes)
                    {
                        if (m.AcquiredOnDay != n.Day || m.AcquiredFromNettedSource || watched.Contains(m.Id)) continue;
                        watched.Add(m.Id);
                        var source = n.PersonById(m.AcquiredFromPersonId);
                        if (source != null) source.HasNet = true;   // netted only after the bite
                    }
                    foreach (var e in n.Log.Events)
                    {
                        if (!watched.Contains(e.MosquitoId)) continue;
                        Assert.IsFalse(e.SourceWasProtected,
                            "a case was blamed on a net that went up after the mosquito had fed");
                        return;
                    }
                }
            }
            Assert.Inconclusive("no watched mosquito transmitted within the window");
        }

        [Test]
        public void EveryCaseCanBeTracedBack()
        {
            // Section 5: "When a new case appears, the game traces it back." Every case, not most.
            var s = PlayOut(7);
            foreach (var e in s.Neighbourhood.Log.Events)
            {
                var replay = TraceBack.Build(s.Neighbourhood, e);
                Assert.IsNotNull(replay, $"case for person {e.TargetPersonId} had no replay");
                Assert.Greater(replay.Steps.Count, 0, "a replay with no steps shows the squad nothing");
                Assert.IsNotNull(replay.SummaryKey);
            }
        }

        [Test]
        public void ASecondaryCaseNamesTheHouseTheContainerAndTheBite()
        {
            // "A short replay shows the house it came from, the container the mosquito came from,
            // and the night the sick person slept unprotected."
            var s = PlayOut(7);
            TransmissionEvent secondary = s.Neighbourhood.Log.Events.Find(e => !e.IsIndexCase);
            Assert.IsNotNull(secondary, "seed 7 produced no secondary case");

            var replay = TraceBack.Build(s.Neighbourhood, secondary);
            Assert.AreEqual(3, replay.Steps.Count, "a secondary case should show container, source, and bite");
            Assert.AreEqual(secondary.SourceHouseholdId, replay.Steps[0].HouseholdId);
            Assert.AreEqual(secondary.OriginContainerId, replay.Steps[0].ContainerId);
            Assert.AreEqual(secondary.SourcePersonId, replay.Steps[1].PersonId);
            Assert.AreEqual(secondary.TargetPersonId, replay.Steps[2].PersonId);
        }

        [Test]
        public void AnIndexCaseIsShownAsAlreadyIllRatherThanBlamedOnAnyone()
        {
            var s = PlayOut(1);
            var index = s.Neighbourhood.Log.Events.Find(e => e.IsIndexCase);
            var replay = TraceBack.Build(s.Neighbourhood, index);

            Assert.AreEqual("m3.trace.arrivedIll", replay.SummaryKey);
            Assert.IsFalse(replay.WasPreventable, "the outbreak the squad arrived into is not their failure");
        }

        // ---- the map ---------------------------------------------------------------------

        [Test]
        public void TheMapAccountsForEveryCase()
        {
            var s = PlayOut(5);
            var map = OutbreakMap.Build(s.Neighbourhood);

            Assert.AreEqual(s.Neighbourhood.Log.Events.Count, map.TotalCases);
            Assert.AreEqual(s.Neighbourhood.SecondaryCases, map.SecondaryCases);
            Assert.AreEqual(map.SecondaryCases, map.Links.Count, "every secondary case is one link on the map");

            int summed = 0;
            foreach (var h in map.Households) summed += h.Cases;
            Assert.AreEqual(map.TotalCases, summed, "a case was drawn in no household");
        }

        [Test]
        public void TheDebriefAlwaysHasSomethingToShowAboutTheCasesNobodyCouldSee()
        {
            // Section 5 hangs the bridge back to Module 2 on this moment, so it cannot be absent.
            // A chain from an invisible source is the sharper version, but a well-playing squad
            // has fewer chains of every kind and only sees one about two thirds of the time -
            // so the map also carries the people who caught it and never felt a thing, which is
            // available in every session and makes the same point.
            int withEvidence = 0;
            for (int seed = 1; seed <= 60; seed++)
            {
                var map = OutbreakMap.Build(PlayOut(seed, new NetEveryVisiblePatient()).Neighbourhood);
                if (map.HasInvisibleEvidence) withEvidence++;
            }

            Assert.AreEqual(60, withEvidence,
                $"only {withEvidence} of 60 neighbourhoods gave the debrief anything to point at");
        }

        [Test]
        public void ASharperChainFromAnInvisibleSourceTurnsUpOftenEnoughToMatter()
        {
            int withChain = 0;
            for (int seed = 1; seed <= 60; seed++)
            {
                var map = OutbreakMap.Build(PlayOut(seed, new NetEveryVisiblePatient()).Neighbourhood);
                if (map.InvisibleChainHouseholdId >= 0) withChain++;
            }

            // Measured at about two thirds. Asserted below that so a real regression shows up,
            // without pretending the moment is guaranteed - see the decisions log.
            Assert.Greater(withChain / 60.0, 0.5,
                $"a chain from an invisible source appeared in only {withChain} of 60 neighbourhoods");
        }

        [Test]
        public void TheDebriefPromptMatchesWhatTheMapCanActuallyShow()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var map = OutbreakMap.Build(PlayOut(seed, new NetEveryVisiblePatient()).Neighbourhood);
                string key = InvisibleCaseDebrief.PromptKeyFor(map);

                if (map.InvisibleChainHouseholdId >= 0)
                    Assert.AreEqual("m3.debrief.chainFromNowhere", key);
                else if (map.HouseholdsWithAnInvisibleCase.Count > 0)
                    Assert.AreEqual("m3.debrief.peopleWhoNeverKnew", key);
                else
                    Assert.AreEqual("m3.debrief.noInvisibleEvidence", key);
            }
        }

        [Test]
        public void ASquadThatCoveredItsPatientsSeesANearlyEmptyMap()
        {
            // "A squad that netted the first patient on day one sees a map with almost nothing on
            // it, and should understand exactly why."
            double covered = 0, ignored = 0;
            for (int seed = 1; seed <= 60; seed++)
            {
                covered += OutbreakMap.Build(PlayOut(seed, new NetEveryVisiblePatient()).Neighbourhood).Links.Count;
                ignored += OutbreakMap.Build(PlayOut(seed, new DoNothing()).Neighbourhood).Links.Count;
            }

            Assert.Less(covered, ignored * 0.6,
                $"the map for a squad that covered its patients ({covered / 60:F1} links) has to look visibly "
                + $"emptier than for one that did not ({ignored / 60:F1})");
        }

        // ---- the counterfactual ----------------------------------------------------------

        [Test]
        public void TheCounterfactualConfirmsASquadThatAlreadyCoveredPatientZero()
        {
            // Section 5: "A squad that did so sees their own outcome confirmed."
            var n = ScenarioBuilder.Build(9, Module3Config.Default, ScenarioOptions.Default);
            var s = new Session(n);
            s.Do(PlayerAction.OnPerson(ActionKind.PutUpNet, s.PatientZeroId()));
            while (!s.Finished) s.EndTurn();

            var replay = SimRunner.Counterfactual(9, Module3Config.Default, ScenarioOptions.Default,
                                                  s.RecordedActions(), s.PatientZeroId());
            var comparison = new CounterfactualComparison
            {
                AsPlayed = OutbreakMap.Build(n),
                HadPatientZeroBeenCovered = OutbreakMap.Build(replay.Neighbourhood),
            };

            Assert.IsTrue(comparison.SquadMatchedIt,
                "a squad that covered patient zero on day one was told it could have done better");
            Assert.AreEqual("m3.replay.youAlreadyDidThis", comparison.HeadlineKey);
        }

        // ---- scoring (section 10) --------------------------------------------------------

        [Test]
        public void BreakingAChainOutscoresEverythingElsePutTogether()
        {
            // Section 10: "Scored higher than every other action in the module combined, because
            // it is the only one that stops the virus leaving the house." Checked in played
            // sessions rather than on paper, because the weights alone do not prove it.
            int checkedSessions = 0;
            for (int seed = 1; seed <= 60; seed++)
            {
                var s = PlayOut(seed, new NetEveryVisiblePatient());
                var card = Scoring.Evaluate(s.Neighbourhood, s);
                if (card.ChainsBroken == 0) continue;

                checkedSessions++;
                Assert.Greater(card.ChainPoints, card.OtherPoints,
                    $"seed {seed}: {card.ChainsBroken} broken chain(s) scored {card.ChainPoints}, "
                    + $"behind {card.OtherPoints} from everything else");
            }

            Assert.Greater(checkedSessions, 20, "too few sessions broke a chain to test this meaningfully");
        }

        [Test]
        public void NothingThePlayerGetsWrongEverSubtracts()
        {
            // Section 10: a missed warning sign is "not points deducted - a consequence shown",
            // and an unnecessary referral is "gently corrected, not punished".
            for (int seed = 1; seed <= 30; seed++)
            {
                var s = PlayOut(seed, new DoNothing());
                var card = Scoring.Evaluate(s.Neighbourhood, s);

                Assert.GreaterOrEqual(card.Total, 0, $"seed {seed} produced a negative score");
                foreach (var line in card.Lines)
                {
                    Assert.GreaterOrEqual(line.Points, 0, $"{line.Reason} took marks away");
                    if (line.Reason == ScoreReason.MissedWarningSign
                        || line.Reason == ScoreReason.UnnecessaryReferral)
                    {
                        Assert.AreEqual(0, line.Points, $"{line.Reason} should cost nothing");
                        Assert.IsTrue(line.IsNote, $"{line.Reason} should read as a teaching moment");
                    }
                }
            }
        }

        [Test]
        public void ASquadThatDoesNothingScoresLessThanOneThatCoversItsPatients()
        {
            double idle = 0, working = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var a = PlayOut(seed, new DoNothing());
                var b = PlayOut(seed, new NetEveryVisiblePatient());
                idle += Scoring.Evaluate(a.Neighbourhood, a).Total;
                working += Scoring.Evaluate(b.Neighbourhood, b).Total;
            }
            Assert.Greater(working, idle * 2, $"doing the work scored {working / 40:F0} against {idle / 40:F0} for doing nothing");
        }

        [Test]
        public void CoveringSomeoneWhoNeverLookedIllIsNotScored()
        {
            // A player cannot aim at an invisible case, so it must not quietly count for or
            // against them. Section 5: the squad cannot act on cases it cannot see.
            var s = PlayOut(3, new NetEveryVisiblePatient());
            var card = Scoring.Evaluate(s.Neighbourhood, s);

            int asymptomaticNetted = 0;
            foreach (var p in s.Neighbourhood.People)
            {
                if (p.HasBeenInfected && p.IsAsymptomatic && p.HasNet) asymptomaticNetted++;
            }

            int visibleNetted = 0;
            foreach (var p in s.Neighbourhood.People)
            {
                if (p.HasBeenInfected && !p.IsAsymptomatic && p.HasNet) visibleNetted++;
            }

            Assert.LessOrEqual(card.ChainsBroken, visibleNetted,
                $"{card.ChainsBroken} chains scored but only {visibleNetted} findable patients were covered "
                + $"({asymptomaticNetted} symptomless people also had nets)");
        }

        // ---- the journal -----------------------------------------------------------------

        [Test]
        public void TheJournalEndsOnTheQuestionTheClassAnswers()
        {
            // Section 5: "The Field Journal closes with one prompt... The facilitator reads these
            // aloud." It has to be the last thing on the page.
            var s = PlayOut(7, new NetEveryVisiblePatient());
            var export = FieldJournal.Build(s.Neighbourhood, s);
            string text = export.ToPlainText();

            StringAssert.Contains(FieldJournalExport.ClosingPrompt, text);
            int promptAt = text.IndexOf(FieldJournalExport.ClosingPrompt);
            Assert.Greater(promptAt, text.Length / 2, "the closing prompt should be at the end, not the middle");
        }

        [Test]
        public void TheJournalRecordsHowEachCaseHappened()
        {
            var s = PlayOut(7);
            var export = FieldJournal.Build(s.Neighbourhood, s);

            Assert.AreEqual(s.Neighbourhood.Log.Events.Count, export.Chains.Count,
                "the journal has to account for every case the squad could be asked about");
            Assert.AreEqual(s.Journal.Count, export.Turns.Count);
        }

        [Test]
        public void TheJournalNamesTheHousesNobodyReached()
        {
            var s = PlayOut(1);
            var export = FieldJournal.Build(s.Neighbourhood, s);
            Assert.Greater(export.Unvisited.Count, 0,
                "a squad that visited nothing should be told which houses it never reached");
        }
    }
}
