using Aedes.Module3.Sim;
using NUnit.Framework;
using UnityEngine;

namespace Aedes.Module3.Sim.Tests
{
    /// <summary>
    /// The CSV that ships in Resources is what every session starts from, so it has to be a file
    /// the game will actually accept.
    ///
    /// This suite exists because it drifted once: a default moved in Module3Config, the committed
    /// CSV kept the old value, and the result was a file that failed its own validation at
    /// startup and silently fell back to the built-in defaults. Nothing would have shown that in
    /// play mode except a warning nobody was reading.
    /// </summary>
    [TestFixture]
    public class BakedParameterFileTests
    {
        private const string Resource = "Module3/Module3Parameters";

        [Test]
        public void TheShippedParameterFileExists()
        {
            Assert.IsNotNull(Resources.Load<TextAsset>(Resource),
                $"no parameter file at Resources/{Resource} - run Module 3 > Regenerate parameter template");
        }

        [Test]
        public void TheShippedParameterFileIsAccepted()
        {
            var csv = Resources.Load<TextAsset>(Resource);
            Assert.IsNotNull(csv);

            var set = ParameterCsv.Parse(csv.text, "baked");
            Assert.IsTrue(set.IsValid,
                "the shipped parameter file would be rejected at startup:\n  " + string.Join("\n  ", set.Errors));
            Assert.IsEmpty(set.UnknownKeys,
                "the shipped file carries keys this build does not understand: " + string.Join(", ", set.UnknownKeys));
        }

        [Test]
        public void TheShippedFileMatchesTheBuiltInDefaults()
        {
            // If these drift apart, whichever one a reader happens to look at is misleading.
            var csv = Resources.Load<TextAsset>(Resource);
            Assert.IsNotNull(csv);

            var fromFile = ParameterCsv.Parse(csv.text, "baked");
            var defaults = new ParameterSet();

            Assert.AreEqual(defaults.Config.UnreferredWarningGraceDays, fromFile.Config.UnreferredWarningGraceDays);
            Assert.AreEqual(defaults.Config.DaysPerHandover, fromFile.Config.DaysPerHandover);
            Assert.AreEqual(defaults.Config.NetsPerSession, fromFile.Config.NetsPerSession);
            Assert.AreEqual(defaults.Config.AsymptomaticShare, fromFile.Config.AsymptomaticShare, 1e-4);
            Assert.AreEqual(defaults.Config.NetOverRestingPerson, fromFile.Config.NetOverRestingPerson, 1e-4);
            Assert.AreEqual(defaults.Scenario.Lanes, fromFile.Scenario.Lanes);
            Assert.AreEqual(defaults.Scenario.HouseholdsPerLane, fromFile.Scenario.HouseholdsPerLane);
        }

        [Test]
        public void EveryKeyInTheSchemaAppearsInTheShippedFile()
        {
            var csv = Resources.Load<TextAsset>(Resource);
            Assert.IsNotNull(csv);

            var set = ParameterCsv.Parse(csv.text, "baked");
            foreach (var d in ParameterCsv.Descriptors)
            {
                CollectionAssert.Contains(set.PresentKeys, d.Key,
                    $"'{d.Key}' is in the schema but missing from the file the GAMA modellers work from");
            }
        }
    }
}
