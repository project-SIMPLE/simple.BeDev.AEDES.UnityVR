using System.Collections.Generic;
using Aedes.Module3.Sim;
using NUnit.Framework;

namespace Aedes.Module3.Sim.Tests
{
    /// <summary>
    /// What the player can see, and what they are offered to do about it.
    ///
    /// Sections 4, 5 and 12 impose rules that are easy to break by accident and hard to catch in
    /// review: the player never diagnoses or treats, they cannot see who is infected, and nothing
    /// marks a sick person as a hazard. These are tests rather than a style guide.
    /// </summary>
    [TestFixture]
    public class ObservationTests
    {
        private static Neighbourhood Build(int seed = 1) =>
            ScenarioBuilder.Build(seed, Module3Config.Default, ScenarioOptions.Default);

        [Test]
        public void TheOfferedVerbsContainNoClinicalAction()
        {
            // Section 12: no medicines, no naming the illness, no procedures. Section 4: "never
            // 'diagnose' or 'treat'." The verb list is a closed set for exactly this reason.
            foreach (var verb in Observe.OfferedVerbs)
            {
                string name = verb.ToString().ToLowerInvariant();
                Assert.IsFalse(name.Contains("diagnos"), $"{verb} is a diagnostic action");
                Assert.IsFalse(name.Contains("treat"), $"{verb} is a treatment action");
                Assert.IsFalse(name.Contains("medicine") || name.Contains("drug"), $"{verb} dispenses medicine");
                Assert.IsFalse(name.Contains("test") || name.Contains("blood"), $"{verb} is a clinical procedure");
            }
        }

        [Test]
        public void ClearingContainersIsNotAVerb()
        {
            // Section 5: "Clearing containers is therefore not an action in this module at all -
            // not a button, not a task, not a score."
            foreach (var verb in Observe.OfferedVerbs)
            {
                string name = verb.ToString().ToLowerInvariant();
                Assert.IsFalse(name.Contains("container") || name.Contains("clear") || name.Contains("empty"),
                    $"{verb} lets the player act on a container; that was Module 2's work");
            }
        }

        [Test]
        public void EveryOfferedVerbIsOneTheModelAccepts()
        {
            var n = Build();
            var offered = new List<AvailableAction>();
            offered.AddRange(Observe.ActionsFor(n, n.People[0].Id));
            offered.AddRange(Observe.ActionsFor(n, 0, household: true));

            foreach (var a in offered)
            {
                CollectionAssert.Contains(Observe.OfferedVerbs, a.Kind,
                    $"{a.Kind} is offered to the player but is not in the module's verb list");
            }
        }

        [Test]
        public void SomeoneIncubatingLooksExactlyLikeSomeoneWell()
        {
            // Section 5: the squad cannot act on cases it cannot see. Nothing in the observation
            // may leak the model's hidden state.
            var n = Build();
            var exposed = n.People.Find(p => p.State == HealthState.Exposed);
            if (exposed == null) Assert.Ignore("this seed seeded no incubating person");

            var obs = Observe.Person(n, exposed.Id);
            Assert.AreEqual(VisibleCondition.Seems_Well, obs.Condition,
                "an incubating person was distinguishable from a well one");
            Assert.IsEmpty(obs.Signs, "an incubating person showed something in the room");
        }

        [Test]
        public void SomeoneWhoNeverFeelsIllIsNeverVisible()
        {
            // Section 5: "You cannot net a person who does not know they are infected."
            for (int seed = 1; seed <= 40; seed++)
            {
                var n = Build(seed);
                n.AdvanceDays(20);

                for (int i = 0; i < n.People.Count; i++)
                {
                    var p = n.People[i];
                    if (!p.IsAsymptomatic || !p.HasBeenInfected) continue;
                    if (p.State == HealthState.Hospitalised) continue;

                    var obs = Observe.Person(n, p.Id);
                    Assert.AreEqual(VisibleCondition.Seems_Well, obs.Condition,
                        $"seed {seed}: a symptomless case was visible to the player as {obs.Condition}");
                }
            }
        }

        [Test]
        public void AWarningSignReadsAsNeedingADoctorAndNotAsADiagnosis()
        {
            var n = Build(3);
            Person found = null;
            for (int d = 0; d < 40 && found == null; d++)
            {
                n.AdvanceDays(1);
                found = n.People.Find(p => p.HasVisibleWarningSign(n.Day) && p.State != HealthState.Hospitalised);
            }
            Assert.IsNotNull(found, "no warning sign appeared in forty days");

            var obs = Observe.Person(n, found.Id);
            Assert.AreEqual(VisibleCondition.NeedsADoctorNow, obs.Condition);
            Assert.IsNotNull(obs.DialogueKey);
            StringAssert.StartsWith("m3.say.", obs.DialogueKey);
            Assert.Greater(obs.Signs.Count, 0, "a warning sign should be visible in the room, not only spoken");
        }

        [Test]
        public void ThePersonWhoseFeverHasBrokenSaysTheyAreBetter()
        {
            // Section 8: the trap. The household reports improvement; the danger window is open.
            var n = Build(3);
            Person found = null;
            for (int d = 0; d < 40 && found == null; d++)
            {
                n.AdvanceDays(1);
                found = n.People.Find(p => p.IsDefervescing(n.Day) && p.State != HealthState.Hospitalised
                                        && !p.HasVisibleWarningSign(n.Day));
            }
            Assert.IsNotNull(found, "nobody's fever broke in forty days");

            var obs = Observe.Person(n, found.Id);
            Assert.AreEqual(VisibleCondition.Improving, obs.Condition);
            Assert.AreEqual("m3.say.feelingBetterToday", obs.DialogueKey);
        }

        [Test]
        public void TheHouseholdReportsDaysUnwellInPlainNumbers()
        {
            // Section 7: "she has been hot for three days" - what the family noticed, not what
            // the model knows. It must count from symptoms, never from infection.
            var n = Build(1);
            n.AdvanceDays(3);

            for (int i = 0; i < n.People.Count; i++)
            {
                var p = n.People[i];
                if (!p.IsFebrile(n.Day)) continue;

                var obs = Observe.Person(n, p.Id);
                Assert.AreEqual(n.Day - p.SymptomOnsetDay, obs.DaysUnwell);
                Assert.GreaterOrEqual(obs.DaysUnwell, 0, "a household reported a negative number of days");
                Assert.LessOrEqual(obs.DaysUnwell, n.Day - p.ExposedDay,
                    "the household knew about the illness before the symptoms started");
            }
        }

        [Test]
        public void TheReferralActionIsAlwaysOfferedAndNeverPreJudged()
        {
            // Section 4: "This is not a judgement call and the player should not weigh it up."
            // Hiding the button until it is correct would do the discriminating for the student.
            var n = Build();
            for (int i = 0; i < 12; i++)
            {
                var actions = Observe.ActionsFor(n, n.People[i].Id);
                var referral = actions.Find(a => a.Kind == ActionKind.ReferToHealthCentre);
                Assert.AreNotEqual(default(AvailableAction), referral,
                    $"person {i} was not offered the referral action");
                Assert.IsTrue(referral.Enabled, "the referral action was greyed out for someone who has not been referred");
            }
        }

        [Test]
        public void AHouseholdObservationShowsTheYardWithoutOfferingToClearIt()
        {
            var n = Build();
            var obs = Observe.Household(n, 0);

            Assert.Greater(obs.VisibleContainers, 0, "the yard should show its containers - section 5 wants them seen");

            var actions = Observe.ActionsFor(n, 0, household: true);
            foreach (var a in actions)
            {
                Assert.AreNotEqual(ActionKind.Visit, a.Kind);
                CollectionAssert.Contains(Observe.OfferedVerbs, a.Kind);
            }
        }

        [Test]
        public void ANettedPatientShowsTheNetAsSomethingDoneForThem()
        {
            // Section 5 on framing: protective, never accusatory. What the room shows is care
            // that has been given, not a containment marker.
            var n = Build(1);
            var patient = n.People.Find(p => p.IsFebrile(n.Day));
            Assert.IsNotNull(patient);

            n.Apply(PlayerAction.OnPerson(ActionKind.PutUpNet, patient.Id));
            var obs = Observe.Person(n, patient.Id);

            Assert.IsTrue(obs.HasNet);
            Assert.IsTrue(obs.Signs.Exists(s => s.Key == "m3.see.netIsUp"));
        }
    }
}
