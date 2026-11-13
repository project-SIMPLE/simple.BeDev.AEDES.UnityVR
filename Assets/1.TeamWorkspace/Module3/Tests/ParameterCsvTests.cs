using Aedes.Module3.Sim;
using NUnit.Framework;

namespace Aedes.Module3.Sim.Tests
{
    /// <summary>
    /// The CSV that GAMA generates is the only route the simulation parameters take into the
    /// game, including mid-session. These cover the things that would go wrong quietly.
    /// </summary>
    [TestFixture]
    public class ParameterCsvTests
    {
        [Test]
        public void TheTemplateRoundTrips()
        {
            var defaults = new ParameterSet();
            string csv = ParameterCsv.WriteTemplate(defaults);
            var back = ParameterCsv.Parse(csv, "round-trip");

            Assert.IsTrue(back.IsValid, string.Join("; ", back.Errors));
            Assert.IsEmpty(back.UnknownKeys, "the template contains a key the reader does not know");
            Assert.AreEqual(defaults.Config.IncubationMinDays, back.Config.IncubationMinDays);
            Assert.AreEqual(defaults.Config.AsymptomaticShare, back.Config.AsymptomaticShare, 1e-4);
            Assert.AreEqual(defaults.Scenario.Module2.ContainerClearance,
                            back.Scenario.Module2.ContainerClearance, 1e-4);
        }

        [Test]
        public void CommentLinesAboveTheHeaderAreSkipped()
        {
            string csv = "# a note from GAMA\n# another\nkey,value\nrounds,4\n";
            var set = ParameterCsv.Parse(csv);
            Assert.IsTrue(set.IsValid, string.Join("; ", set.Errors));
            Assert.AreEqual(4, set.Config.Rounds);
        }

        [Test]
        public void APartialFileLeavesEverythingElseAlone()
        {
            // GAMA may send only what changed. A partial file must not reset the rest to the
            // build's defaults, so mid-session reads layer onto what is already in force.
            var current = ParameterCsv.Parse("key,value\nrounds,9\nnets_per_session,7\n");
            var update = ParameterCsv.Parse("key,value\ndaily_bite_probability,0.2\n", "gama", current);

            Assert.IsTrue(update.IsValid, string.Join("; ", update.Errors));
            Assert.AreEqual(0.2, update.Config.DailyBiteProbability, 1e-6);
            Assert.AreEqual(9, update.Config.Rounds, "a key absent from the update was reset");
            Assert.AreEqual(7, update.Config.NetsPerSession, "a key absent from the update was reset");
            CollectionAssert.AreEqual(new[] { "daily_bite_probability" }, update.PresentKeys);
        }

        [Test]
        public void UnknownKeysAreReportedButDoNotFailTheFile()
        {
            // A newer GAMA model may know about parameters this build does not. That should be
            // visible in the log, not fatal in a classroom.
            var set = ParameterCsv.Parse("key,value\nrounds,5\nsome_future_parameter,3\n");
            Assert.IsTrue(set.IsValid);
            CollectionAssert.Contains(set.UnknownKeys, "some_future_parameter");
        }

        [Test]
        public void ValuesOutsideTheirRangeAreRefused()
        {
            var set = ParameterCsv.Parse("key,value\nasymptomatic_share,1.4\n");
            Assert.IsFalse(set.IsValid);
        }

        [Test]
        public void AFileThatWouldGiveEveryHouseholdANetIsRefused()
        {
            // Section 5: the shortage is what makes the player choose. This is a design
            // invariant, so a parameter file cannot quietly remove it.
            var set = ParameterCsv.Parse("key,value\nlanes,2\nhouseholds_per_lane,5\nnets_per_session,10\n");
            Assert.IsFalse(set.IsValid);
        }

        [Test]
        public void AFileThatMakesANetEquallyGoodForTheWellIsRefused()
        {
            var set = ParameterCsv.Parse("key,value\nnet_over_resting_person,0.9\nnet_over_active_person,0.92\n");
            Assert.IsFalse(set.IsValid);
        }

        [Test]
        public void WholeNumbersWrittenAsFloatsAreAccepted()
        {
            // GAMA writes integers as "4.0" often enough that refusing it would be petty.
            var set = ParameterCsv.Parse("key,value\nrounds,6.0\n");
            Assert.IsTrue(set.IsValid, string.Join("; ", set.Errors));
            Assert.AreEqual(6, set.Config.Rounds);
        }

        [Test]
        public void StructuralKeysAreFlaggedAsNeedingANewSession()
        {
            Assert.IsTrue(ParameterCsv.RequiresRestart(new[] { "lanes" }, out var blocking));
            CollectionAssert.Contains(blocking, "lanes");

            Assert.IsFalse(ParameterCsv.RequiresRestart(new[] { "daily_bite_probability" }, out _));
        }

        [Test]
        public void AMidSessionChangeAppliesFromTheNextDayAndNotBefore()
        {
            var n = ScenarioBuilder.Build(1, Module3Config.Default, ScenarioOptions.Default);
            n.AdvanceDays(5);

            double before = n.Config.EmergencePerContainerPerDay;
            var changed = n.Config.Clone();
            changed.EmergencePerContainerPerDay = before + 5.0;
            n.StageParameters(changed, "gama");

            Assert.AreEqual(before, n.Config.EmergencePerContainerPerDay, 1e-6,
                "a change must not take effect on the day it arrives - the day is already half simulated");

            n.AdvanceDays(1);
            Assert.AreEqual(before + 5.0, n.Config.EmergencePerContainerPerDay, 1e-6,
                "the change should be in force on the following day");
            Assert.AreEqual(1, n.Parameters.ChangeCount);
        }

        [Test]
        public void ResendingTheSameChangeDoesNotStackTheTimeline()
        {
            var n = ScenarioBuilder.Build(1, Module3Config.Default, ScenarioOptions.Default);
            n.AdvanceDays(3);

            var changed = n.Config.Clone();
            changed.DailyBiteProbability = 0.11;
            n.StageParameters(changed, "gama");
            n.StageParameters(changed, "gama");
            n.StageParameters(changed, "gama");

            Assert.AreEqual(1, n.Parameters.ChangeCount,
                "a repeated delivery should replace the staged entry, not pile up");
        }
    }
}
