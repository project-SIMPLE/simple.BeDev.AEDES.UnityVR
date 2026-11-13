using Aedes.Module3.Sim;
using NUnit.Framework;

namespace Aedes.Module3.Sim.Tests
{
    /// <summary>
    /// What the Coach is told, and in what order.
    ///
    /// This is not cosmetic. Section 9 gives the Coach the job of warning the Pilot what is being
    /// missed, and section 8 gives them the length of a handover to do it in. Whatever is at the
    /// top of that list is what the squad does next, so the ordering is a teaching decision and
    /// belongs in tests rather than in a UI layout.
    /// </summary>
    [TestFixture]
    public class CoachBriefTests
    {
        private static Session PlayTo(int seed, System.Func<HandoverBrief, bool> until, int maxTurns = 50)
        {
            var n = ScenarioBuilder.Build(seed, Module3Config.Default, ScenarioOptions.Default);
            var s = new Session(n);
            for (int t = 0; t < maxTurns && !s.Finished; t++)
            {
                var brief = s.EndTurn();
                if (brief != null && until(brief)) return s;
            }
            return s;
        }

        [Test]
        public void AnyoneNeedingADoctorComesFirst()
        {
            // Section 4 calls the referral rule the heart of the module. If it is third on a list
            // a fourteen-year-old is reading aloud against a clock, it may as well not be there.
            for (int seed = 1; seed <= 40; seed++)
            {
                var n = ScenarioBuilder.Build(seed, Module3Config.Default, ScenarioOptions.Default);
                var s = new Session(n);

                while (!s.Finished)
                {
                    var brief = s.EndTurn();
                    if (brief == null || brief.NeedReferralNow.Count == 0) continue;

                    var lines = CoachBrief.Compose(brief);
                    Assert.Greater(lines.Count, 0);
                    Assert.AreEqual(BriefUrgency.Now, lines[0].Urgency,
                        $"seed {seed}: somebody needed a doctor and it was not the first thing said");
                }
            }
        }

        [Test]
        public void AReferralIsNeverTruncatedAwayByTheLineCap()
        {
            // The cap exists so the brief stays sayable. It must not be able to hide the one
            // thing the module most wants a student to act on.
            var brief = new HandoverBrief { DaysElapsed = 3, NetsRemaining = 0 };
            for (int i = 0; i < 10; i++)
            {
                brief.UncoveredPatients.Add(new PersonNote { PersonId = i, HouseholdId = i, MessageKey = "m3.brief.uncovered" });
                brief.HouseholdsNeverVisited.Add(i);
            }
            brief.NeedReferralNow.Add(new PersonNote { PersonId = 99, HouseholdId = 4, MessageKey = "m3.brief.bleeding" });

            var lines = CoachBrief.Compose(brief, maxLines: 3);

            Assert.LessOrEqual(lines.Count, 3);
            Assert.AreEqual(BriefUrgency.Now, lines[0].Urgency);
            Assert.AreEqual("m3.brief.bleeding", lines[0].MessageKey);
        }

        [Test]
        public void SomeoneWhoLooksBetterIsFlaggedAheadOfSomeoneWhoLooksIll()
        {
            // Section 8: the Pilot who sees someone improving and moves on has made the module's
            // mistake. A patient who is visibly unwell will get attention anyway; the one who
            // seems fine will not, unless the Coach says so.
            var brief = new HandoverBrief { DaysElapsed = 3, NetsRemaining = 2 };
            brief.UncoveredPatients.Add(new PersonNote { PersonId = 1, HouseholdId = 1, MessageKey = "m3.brief.uncovered" });
            brief.LookingBetterButWatch.Add(new PersonNote { PersonId = 2, HouseholdId = 2, MessageKey = "m3.brief.lookingBetter" });

            var lines = CoachBrief.Compose(brief);

            int better = lines.FindIndex(l => l.MessageKey == "m3.brief.lookingBetter");
            int uncovered = lines.FindIndex(l => l.MessageKey == "m3.brief.uncovered");
            Assert.Greater(better, -1);
            Assert.Greater(uncovered, -1);
            Assert.Less(better, uncovered,
                "the patient who looks better has to be mentioned before the one who obviously is not");
        }

        [Test]
        public void TheBriefStaysShortEnoughToSayOutLoud()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var n = ScenarioBuilder.Build(seed, Module3Config.Default, ScenarioOptions.Default);
                var s = new Session(n);
                while (!s.Finished)
                {
                    var brief = s.EndTurn();
                    if (brief == null) continue;

                    int urgent = brief.NeedReferralNow.Count;
                    var lines = CoachBrief.Compose(brief);

                    // The cap yields only to referrals, and only as far as it must.
                    Assert.LessOrEqual(lines.Count, System.Math.Max(CoachBrief.MaxLines, urgent),
                        $"seed {seed}: a handover brief ran to {lines.Count} lines");
                }
            }
        }

        [Test]
        public void ABriefWithNothingUrgentStillPointsSomewhere()
        {
            var brief = new HandoverBrief { DaysElapsed = 3, NetsRemaining = 4 };
            brief.HouseholdsNeverVisited.Add(6);

            var lines = CoachBrief.Compose(brief);
            Assert.Greater(lines.Count, 0, "the Coach was left with nothing to say");
            Assert.AreEqual(6, lines[0].HouseholdId, "the brief should point at the house nobody has been to");
        }

        [Test]
        public void TheTimeJumpIsStatedInWordsNotJustANumber()
        {
            // Section 8: 'The screen states plainly how far: "Three days later."'
            Assert.AreEqual("m3.coach.oneDayLater", CoachBrief.TimeJumpKey(1));
            Assert.AreEqual("m3.coach.daysLater", CoachBrief.TimeJumpKey(3));
            Assert.AreEqual("m3.coach.sameDay", CoachBrief.TimeJumpKey(0));
        }

        [Test]
        public void EveryLineCarriesAHouseToLookAtOrIsDeliberatelyGeneral()
        {
            // The Coach is holding the map. A warning they cannot locate is not much use.
            for (int seed = 1; seed <= 20; seed++)
            {
                var n = ScenarioBuilder.Build(seed, Module3Config.Default, ScenarioOptions.Default);
                var s = new Session(n);
                while (!s.Finished)
                {
                    var brief = s.EndTurn();
                    if (brief == null) continue;

                    foreach (var line in CoachBrief.Compose(brief))
                    {
                        if (line.Urgency == BriefUrgency.Background) continue;
                        Assert.GreaterOrEqual(line.HouseholdId, 0,
                            $"'{line.MessageKey}' is urgent but names no house");
                    }
                }
            }
        }
    }
}
