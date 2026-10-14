using System;
using Aedes.Module3.Sim;
using NUnit.Framework;

namespace Aedes.Module3.Sim.Tests
{
    /// <summary>
    /// The module's teaching guarantees, written as tests.
    ///
    /// Section 5 of the design document does not describe these as nice-to-haves. It says the
    /// module "should make it impossible to succeed by any other route" and lists the outcomes
    /// that must hold. Each one is a test here. If a parameter change breaks one, the module has
    /// stopped teaching the thing it exists to teach, and that should fail loudly rather than be
    /// discovered by a class in Vientiane.
    ///
    /// This matters more now that the parameters come from a CSV that GAMA generates: this suite
    /// is the acceptance gate for any parameter set, not just a tuning aid. Run it against a new
    /// file before that file goes anywhere near a classroom.
    ///
    /// The assertions are deliberately RELATIVE - orderings and ratios, not absolute case counts -
    /// so that they survive legitimate re-calibration while still catching a set that has stopped
    /// teaching the lesson.
    /// </summary>
    [TestFixture]
    public class Module3CalibrationTests
    {
        private const int Seeds = 200;

        private static Module3Config Config() => Module3Config.Default;
        private static ScenarioOptions Typical() => ScenarioOptions.Default;

        private static ScenarioOptions AllCleared()
        {
            var o = ScenarioOptions.Default;
            o.Module2 = Module2Outcome.EverythingCleared;
            return o;
        }

        private static double Average(ScenarioOptions options, Func<IStrategy> strategy)
        {
            return SimRunner.AverageSecondaryCases(Seeds, Config(), options, strategy);
        }

        // -----------------------------------------------------------------------------------
        // The model has to be re-runnable before any of the rest means anything.
        // -----------------------------------------------------------------------------------

        [Test]
        public void SameSeedAndStrategyGivesTheSameOutbreak()
        {
            for (int seed = 1; seed <= 25; seed++)
            {
                var a = SimRunner.Run(seed, Config(), Typical(), new NetEveryVisiblePatient());
                var b = SimRunner.Run(seed, Config(), Typical(), new NetEveryVisiblePatient());

                Assert.AreEqual(a.SecondaryCases, b.SecondaryCases, $"seed {seed} was not reproducible");
                Assert.AreEqual(a.Neighbourhood.Log.Events.Count, b.Neighbourhood.Log.Events.Count,
                    $"seed {seed} produced a different transmission log on re-run");
            }
        }

        [Test]
        public void CoveringPatientZeroOnDayOneIsAlmostAlwaysBetter()
        {
            // Section 5: "before any score is shown, the game replays what would have happened had
            // the first patient been covered on day one."
            //
            // Asserted across seeds rather than on one, because an outbreak is stochastic: in a
            // small minority of neighbourhoods, covering patient zero leaves mosquitoes to find
            // somebody else and the counterfactual comes out no better. That is honest modelling,
            // but it is also a classroom risk worth knowing the size of - if this rate climbs, the
            // replay starts telling squads that the right action would have made things worse.
            int worse = 0, counted = 0;
            double totalReduction = 0;

            for (int seed = 1; seed <= Seeds; seed++)
            {
                var squad = new ScriptedActions();
                var asPlayed = SimRunner.Run(seed, Config(), Typical(), squad);

                int patientZero = -1;
                foreach (var e in asPlayed.Neighbourhood.Log.Events)
                {
                    if (e.IsIndexCase) { patientZero = e.TargetPersonId; break; }
                }
                if (patientZero < 0) continue;

                var covered = SimRunner.Counterfactual(seed, Config(), Typical(), squad, patientZero);
                if (covered.PreventableCases > asPlayed.PreventableCases) worse++;
                totalReduction += asPlayed.PreventableCases - covered.PreventableCases;
                counted++;
            }

            Assert.Greater(counted, 0, "the scenario seeded no index case");
            Assert.Greater(totalReduction / counted, 1.0,
                "on average, covering patient zero on day one has to visibly shrink the outbreak, "
                + $"but the reduction was only {totalReduction / counted:F2} preventable chains");
            Assert.Less(worse / (double)counted, 0.05,
                $"the replay showed a WORSE outcome in {worse} of {counted} neighbourhoods; above about "
                + "one in twenty this stops being acceptable noise and starts undermining the lesson");
        }

        // -----------------------------------------------------------------------------------
        // Section 5 - "Designing it so the lesson cannot be avoided"
        // -----------------------------------------------------------------------------------

        [Test]
        public void ADoNothingNeighbourhoodSeesAnOutbreak()
        {
            double doNothing = Average(Typical(), () => new DoNothing());
            Assert.Greater(doNothing, 5.0,
                "a squad that does nothing should watch a clearly growing outbreak, "
                + $"but the average was only {doNothing:F2} secondary cases");
        }

        [Test]
        public void ClearingEveryContainerDoesNotStopAnOutbreakAlreadyUnderWay()
        {
            // "A squad that clears every container in the neighbourhood but leaves the sick
            // unprotected should still watch the outbreak grow. Containers reduce the mosquito
            // population; they do not stop a virus that is already in a person."
            double cleared = Average(AllCleared(), () => new DoNothing());
            double nettingTheSick = Average(Typical(), () => new NetFirstPatients(2));

            Assert.Greater(cleared, 3.0,
                $"prevention alone left only {cleared:F2} secondary cases - the outbreak has to still run, "
                + "or the module teaches that Module 2's work was sufficient on its own");

            Assert.Greater(cleared, nettingTheSick,
                $"clearing containers ({cleared:F2}) must not beat covering the patients "
                + $"({nettingTheSick:F2}) - that inverts the whole lesson of the module");
        }

        [Test]
        public void NettingTheWellIsNoBetterThanDoingNothing()
        {
            // "A student will reach for the net to protect the healthy, which is the instinct
            // everyone has. The module should let them try it, and then show them that the
            // neighbourhood kept getting new cases anyway."
            double doNothing = Average(Typical(), () => new DoNothing());
            double netTheWell = Average(Typical(), () => new NetTheWell());

            Assert.Greater(netTheWell, doNothing * 0.85,
                $"netting the well ({netTheWell:F2}) came out meaningfully better than doing nothing "
                + $"({doNothing:F2}); the instinct students arrive with has to be shown not to work");
        }

        [Test]
        public void CoveringTheFirstPatientsOnDayOneStopsTheOutbreak()
        {
            // "A squad that covers the first two patients on the first day should see the outbreak
            // stop, even with containers still standing in the yards."
            double doNothing = Average(Typical(), () => new DoNothing());
            double coveredEarly = Average(Typical(), () => new NetFirstPatients(2));

            Assert.Less(coveredEarly, doNothing * 0.55,
                $"covering the first two patients on day one only brought {doNothing:F2} down to "
                + $"{coveredEarly:F2}; this is the strongest action in the module and it has to look like it");
        }

        [Test]
        public void CoveringEveryVisiblePatientLeavesOnlyTheInvisibleChains()
        {
            // "The outbreak smoulders on even when every visible patient is covered, and a sharp
            // student asks why. The honest answer is the bridge back to Module 2."
            var results = SimRunner.RunMany(Seeds, Config(), Typical(), () => new NetEveryVisiblePatient());

            double secondary = 0, invisible = 0;
            foreach (var r in results) { secondary += r.SecondaryCases; invisible += r.CasesFromInvisibleSource; }

            Assert.Greater(secondary, 0, "covering every visible patient should not end the outbreak completely - "
                + "the cases nobody can see are what sends the debrief back to Module 2");

            Assert.Greater(invisible / secondary, 0.4,
                $"only {invisible / secondary:P0} of the remaining cases came from sources nobody could see; "
                + "the residue has to be mostly invisible or the bridge back to Module 2 does not hold up");
        }

        [Test]
        public void EverySessionPresentsAtLeastOneReferralSituation()
        {
            // Section 4 calls the referral rule "the heart of the module"; section 10 calls acting
            // on it "the strongest positive". A session where no warning sign ever appears has
            // failed to teach the thing students will use most often in life.
            var results = SimRunner.RunMany(Seeds, Config(), Typical(), () => new DoNothing());

            int without = 0;
            foreach (var r in results)
            {
                int warned = 0;
                foreach (var p in r.Neighbourhood.People) if (p.Warning != WarningSign.None) warned++;
                if (warned == 0) without++;
            }

            Assert.AreEqual(0, without,
                $"{without} of {Seeds} sessions presented no warning sign at all");
        }

        [Test]
        public void ActingOnWarningSignsKeepsPeopleOutOfHospitalUnaided()
        {
            // Section 10: a missed warning sign is "not points deducted - a consequence shown."
            // The consequence has to actually differ.
            double ignored = 0, acted = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                ignored += SimRunner.Run(seed, Config(), Typical(), new DoNothing()).HospitalisedUnaided;
                acted += SimRunner.Run(seed, Config(), Typical(), new NetEveryVisiblePatient()).HospitalisedUnaided;
            }

            Assert.Less(acted, ignored * 0.6,
                $"a squad that refers ({acted / Seeds:F2} unaided hospitalisations) has to visibly differ from "
                + $"one that does not ({ignored / Seeds:F2})");
        }

        [Test]
        public void ThereAreFewerNetsThanHouseholds()
        {
            // "The nets available to the player should be limited in number - fewer than there are
            // households. The player must choose who gets one, and the choice is the whole point."
            var options = Typical();
            int households = options.Lanes * options.HouseholdsPerLane;
            Assert.Less(Config().NetsPerSession, households,
                "give the player a net for every household and there is nothing left to learn");
        }

        [Test]
        public void ANetOverSomeoneUpAndAboutIsWorthFarLessThanOverAPatientInBed()
        {
            // The distinction the whole module turns on, and the one a re-calibration could
            // quietly erase. Section 6: Aedes bites during the day, and the patient is in bed
            // during the day.
            var c = Config();
            Assert.Less(c.NetOverRestingPerson, c.NetOverActivePerson * 0.5,
                "if a net helps a well person nearly as much as a patient, netting the sick stops "
                + "being a different action from netting the well, and the module has no lesson left");
        }

        [Test]
        public void SymptomlessCasesAreLessInfectiousThanFebrileOnes()
        {
            // Section 5: "Netting the visible patients still removes the largest and most
            // infectious contributors, because virus levels peak during the fever."
            Assert.Less(Config().AsymptomaticRelativeInfectiousness, 1.0,
                "with symptomless cases as infectious as febrile ones, covering the patients a squad "
                + "can actually find stops being the best available action");
        }

        // -----------------------------------------------------------------------------------
        // Section 8 - the compression is in the calendar, not the biology
        // -----------------------------------------------------------------------------------

        [Test]
        public void ClinicalTimingsAreNotCompressed()
        {
            var c = Config();
            Assert.GreaterOrEqual(c.IncubationMinDays, 4, "bite to symptoms is four to ten days");
            Assert.LessOrEqual(c.IncubationMaxDays, 10, "bite to symptoms is four to ten days");
            Assert.GreaterOrEqual(c.ExtrinsicIncubationMinDays, 8,
                "a mosquito needs eight to twelve days after feeding before it can transmit");
            Assert.LessOrEqual(c.ExtrinsicIncubationMaxDays, 12,
                "a mosquito needs eight to twelve days after feeding before it can transmit");
        }

        [Test]
        public void AnIndexCaseInfectedBeforeDayZeroIsStillInfectious()
        {
            // A regression guard. The squad arrives into an outbreak already under way, so an
            // index case's exposure, onset and infectious window can all sit before day 0.
            // Treating a negative day as "not set" silently makes the outbreak's starting point
            // non-infectious, and every chain then traces to somewhere else.
            var n = ScenarioBuilder.Build(7, Config(), Typical());
            int infectiousOnDayOne = 0;
            foreach (var p in n.People) if (p.IsInfectiousToMosquitoes(1)) infectiousOnDayOne++;

            Assert.Greater(infectiousOnDayOne, 0,
                "nobody was infectious on day one - the squad has arrived into an outbreak with no source");
        }
    }
}
