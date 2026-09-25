using Aedes.Module3.Sim;
using NUnit.Framework;

namespace Aedes.Module3.Sim.Tests
{
    /// <summary>
    /// The turn and handover structure of section 8, where the handover carries the time.
    /// </summary>
    [TestFixture]
    public class SessionTests
    {
        private static Session NewSession(int seed = 1)
        {
            return new Session(ScenarioBuilder.Build(seed, Module3Config.Default, ScenarioOptions.Default));
        }

        [Test]
        public void ASessionRunsToItsPlannedLengthAndThenStops()
        {
            var s = NewSession();
            var cfg = Module3Config.Default;
            int expectedTurns = cfg.Rounds * cfg.TurnsPerRound;

            int turns = 0;
            while (!s.Finished)
            {
                s.EndTurn();
                turns++;
                Assert.Less(turns, expectedTurns + 5, "the session did not finish when it should have");
            }

            Assert.AreEqual(expectedTurns, turns);
            Assert.AreEqual(expectedTurns * cfg.DaysPerHandover, s.Neighbourhood.Day,
                "the calendar should have moved by the handover jump once per turn");
        }

        [Test]
        public void TheCalendarOnlyMovesAtAHandover()
        {
            // Section 8: the game skips forward rather than speeding anything up. Nothing should
            // advance while a Pilot is in the middle of a turn.
            var s = NewSession();
            int day = s.Neighbourhood.Day;

            s.Do(PlayerAction.OnHousehold(ActionKind.Visit, 0));
            s.Do(PlayerAction.OnHousehold(ActionKind.SetFan, 0));
            Assert.AreEqual(day, s.Neighbourhood.Day, "acting inside a turn moved the calendar");

            s.EndTurn();
            Assert.AreEqual(day + Module3Config.Default.DaysPerHandover, s.Neighbourhood.Day);
        }

        [Test]
        public void TheRolesRotateEveryTurn()
        {
            // Section 9: one student is in the headset at a time and the roles rotate, which is
            // what stops one confident student playing while the other two watch.
            var s = NewSession();
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int t = 0; t < Module3Config.Default.TurnsPerRound; t++)
            {
                seen.Add(s.PilotIndex);
                s.EndTurn();
            }
            Assert.AreEqual(Module3Config.Default.TurnsPerRound, seen.Count,
                "every squad member should have had the headset once per round");
        }

        [Test]
        public void TheBriefNamesHouseholdsNobodyHasVisited()
        {
            // Section 8: "A house nobody has visited is still a house nobody has visited, and the
            // Coach should be saying so."
            var s = NewSession();
            s.Do(PlayerAction.OnHousehold(ActionKind.Visit, 3));
            var brief = s.EndTurn();

            CollectionAssert.DoesNotContain(brief.HouseholdsNeverVisited, 3);
            Assert.Greater(brief.HouseholdsNeverVisited.Count, 0,
                "with one household visited, the rest should be reported as unvisited");
        }

        [Test]
        public void TheBriefFlagsPatientsWhoLookBetterButAreInTheDangerWindow()
        {
            // Section 8: "Danger often arrives as the fever comes down. A Pilot who sees someone
            // looking better and moves on has made the mistake the module teaches."
            //
            // Asserted across seeds: a thin outbreak may genuinely have nobody defervescing on a
            // handover day, and that is not a fault. What would be a fault is the Coach rarely
            // having the warning to give.
            const int seeds = 100;
            int withWarning = 0;

            for (int seed = 1; seed <= seeds; seed++)
            {
                var s = NewSession(seed);
                while (!s.Finished)
                {
                    var brief = s.EndTurn();
                    if (brief != null && brief.LookingBetterButWatch.Count > 0) { withWarning++; break; }
                }
            }

            Assert.Greater(withWarning / (double)seeds, 0.8,
                $"the Coach was only able to warn about a recovering patient in {withWarning} of {seeds} "
                + "sessions; this is the mistake the module is built to teach and it has to come up");
        }

        [Test]
        public void EveryCaseSurfacesExactlyOnceAcrossTheSession()
        {
            // Every case gets a trace-back replay (section 5), so none may fall between two
            // briefs. The index cases arrive with the squad and belong to the opening brief;
            // everything after that belongs to the handover it appeared in.
            var s = NewSession(7);
            int reported = s.OpeningBrief().NewCases.Count;
            int indexCases = reported;

            while (!s.Finished)
            {
                var brief = s.EndTurn();
                if (brief != null) reported += brief.NewCases.Count;
            }

            Assert.Greater(indexCases, 0, "the opening brief named no starting patients");
            Assert.AreEqual(s.Neighbourhood.Log.Events.Count, reported,
                "a case surfaced in no brief at all, or in more than one");
        }

        [Test]
        public void AWarningSignCannotAppearAndResolveBetweenTwoTurns()
        {
            // The grace period has to outlast the handover jump. Otherwise a warning sign can come
            // and go while the headset is being passed, the squad never sees it, and section 4's
            // referral rule becomes unreachable rather than merely hard.
            var c = Module3Config.Default;
            Assert.Greater(c.UnreferredWarningGraceDays, c.DaysPerHandover,
                "a warning sign could appear and send itself to hospital between two turns");
        }

        [Test]
        public void AWarningSignIsBriefedBeforeItBecomesAHospitalVisit()
        {
            // The referral has to be actionable: the Coach must hear about it while the squad can
            // still do something. Section 10 calls acting on it the strongest positive available.
            var s = NewSession(3);
            bool briefed = false;
            while (!s.Finished)
            {
                var brief = s.EndTurn();
                if (brief != null && brief.NeedReferralNow.Count > 0) { briefed = true; break; }
            }
            Assert.IsTrue(briefed, "no referral situation ever reached the Coach");
        }

        [Test]
        public void TheJournalRecordsWhatEachPilotDid()
        {
            // Section 9: the journal "is not paperwork bolted on afterwards; it is how the squad plays."
            var s = NewSession();
            s.Do(PlayerAction.OnHousehold(ActionKind.Visit, 2));
            s.Do(PlayerAction.OnPerson(ActionKind.HelpRest, 0));
            s.EndTurn();
            s.Do(PlayerAction.OnHousehold(ActionKind.Visit, 5));
            s.EndTurn();

            Assert.AreEqual(2, s.Journal[0].Actions.Count);
            CollectionAssert.Contains(s.Journal[0].HouseholdsVisited, 2);
            CollectionAssert.Contains(s.Journal[1].HouseholdsVisited, 5);
            Assert.AreEqual(0, s.Journal[0].DayStart);
            Assert.AreEqual(Module3Config.Default.DaysPerHandover, s.Journal[0].DayEnd);
        }

        [Test]
        public void TheRecordedActionsReplayIntoTheSameOutbreak()
        {
            // The counterfactual depends on this: replaying the squad's own actions against the
            // same seed has to reproduce the session they actually played, or the comparison is
            // against a different world.
            var s = NewSession(5);
            while (!s.Finished)
            {
                if (s.TurnIndex == 0) s.Do(PlayerAction.OnPerson(ActionKind.PutUpNet, s.PatientZeroId()));
                s.EndTurn();
            }

            var replay = SimRunner.Run(5, Module3Config.Default, ScenarioOptions.Default, s.RecordedActions());
            Assert.AreEqual(s.Neighbourhood.TotalCases, replay.TotalCases,
                "replaying the squad's recorded actions produced a different outbreak");
        }

        [Test]
        public void PatientZeroIsSomeoneTheSquadCouldHaveFound()
        {
            // Replaying "you could have covered them" at a squad only works if the person was
            // findable. An asymptomatic index case is not.
            for (int seed = 1; seed <= 30; seed++)
            {
                var s = NewSession(seed);
                var p = s.Neighbourhood.PersonById(s.PatientZeroId());
                Assert.IsNotNull(p, $"seed {seed} had no patient zero");
                Assert.IsFalse(p.IsAsymptomatic,
                    $"seed {seed} picked an invisible index case for the counterfactual");
            }
        }
    }
}
