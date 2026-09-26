using System.Collections.Generic;

namespace Aedes.Module3.Sim
{
    public enum BriefUrgency
    {
        /// <summary>Someone needs a doctor. Section 4: not a judgement call.</summary>
        Now,
        /// <summary>Work waiting: a patient with no net, a house nobody has been to.</summary>
        Soon,
        /// <summary>Context the incoming Pilot should have.</summary>
        Background,
    }

    public struct BriefLine
    {
        public BriefUrgency Urgency;
        public string MessageKey;
        public object[] Args;
        /// <summary>Where to look, for the Coach's map. -1 when it is not about one house.</summary>
        public int HouseholdId;
    }

    /// <summary>
    /// Turns a handover into the few sentences the Coach actually says out loud.
    ///
    /// Section 9: the Coach "holds the map of which households have been visited and when, and
    /// warns the Pilot what is being missed", and the squad "plays from the journal, not from
    /// memory". Section 8 gives them two to four minutes to do it in, which is the real design
    /// constraint here - a screen listing everything is a screen nobody reads aloud. So this is
    /// ordered by urgency and capped.
    ///
    /// It lives in the engine-free assembly so the ordering can be tested. What the Coach is told
    /// first decides what the squad does next, and that is too important to leave to a UI layout.
    /// </summary>
    public static class CoachBrief
    {
        /// <summary>
        /// How many lines the Coach gets. More than this and it stops being something a
        /// fourteen-year-old reads out in the time it takes to pass a headset over.
        /// </summary>
        public const int MaxLines = 6;

        public static List<BriefLine> Compose(HandoverBrief brief, int maxLines = MaxLines)
        {
            var lines = new List<BriefLine>();
            if (brief == null) return lines;

            // 1. Anyone who needs a doctor, always first and never truncated away.
            for (int i = 0; i < brief.NeedReferralNow.Count; i++)
            {
                var note = brief.NeedReferralNow[i];
                lines.Add(new BriefLine
                {
                    Urgency = BriefUrgency.Now,
                    MessageKey = note.MessageKey,
                    Args = new object[] { note.HouseholdId },
                    HouseholdId = note.HouseholdId,
                });
            }

            // 2. The trap: someone whose fever has broken. Section 8 - "A Pilot who sees someone
            // looking better and moves on has made the mistake the module teaches." It outranks
            // uncovered patients because it is the one the squad will otherwise walk past.
            for (int i = 0; i < brief.LookingBetterButWatch.Count; i++)
            {
                var note = brief.LookingBetterButWatch[i];
                lines.Add(new BriefLine
                {
                    Urgency = BriefUrgency.Soon,
                    MessageKey = note.MessageKey,
                    Args = new object[] { note.HouseholdId },
                    HouseholdId = note.HouseholdId,
                });
            }

            // 3. Unwell and no net - the work that is actually waiting.
            for (int i = 0; i < brief.UncoveredPatients.Count; i++)
            {
                var note = brief.UncoveredPatients[i];
                lines.Add(new BriefLine
                {
                    Urgency = BriefUrgency.Soon,
                    MessageKey = note.MessageKey,
                    Args = new object[] { note.HouseholdId },
                    HouseholdId = note.HouseholdId,
                });
            }

            // 4. Houses nobody has been to. Section 8: "the Coach should be saying so."
            if (brief.HouseholdsNeverVisited.Count > 0)
            {
                lines.Add(new BriefLine
                {
                    Urgency = BriefUrgency.Soon,
                    MessageKey = "m3.coach.neverVisited",
                    Args = new object[] { brief.HouseholdsNeverVisited.Count },
                    HouseholdId = brief.HouseholdsNeverVisited[0],
                });
            }

            // 5. New cases since the last turn, as one line rather than one each.
            if (brief.NewCases.Count > 0)
            {
                lines.Add(new BriefLine
                {
                    Urgency = BriefUrgency.Background,
                    MessageKey = "m3.coach.newCases",
                    Args = new object[] { brief.NewCases.Count },
                    HouseholdId = -1,
                });
            }

            if (brief.NetsRemaining == 0)
            {
                lines.Add(new BriefLine
                {
                    Urgency = BriefUrgency.Background,
                    MessageKey = "m3.coach.noNetsLeft",
                    Args = null,
                    HouseholdId = -1,
                });
            }

            // Truncate, but never drop a referral - if the cap would cut one, the cap loses.
            if (lines.Count <= maxLines) return lines;

            var trimmed = new List<BriefLine>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Urgency == BriefUrgency.Now) trimmed.Add(lines[i]);
            }
            for (int i = 0; i < lines.Count && trimmed.Count < maxLines; i++)
            {
                if (lines[i].Urgency != BriefUrgency.Now) trimmed.Add(lines[i]);
            }
            return trimmed;
        }

        /// <summary>
        /// The time jump, as plain words rather than a number. Section 8: 'The screen states
        /// plainly how far: "Three days later."'
        /// </summary>
        public static string TimeJumpKey(int days)
        {
            if (days <= 0) return "m3.coach.sameDay";
            return days == 1 ? "m3.coach.oneDayLater" : "m3.coach.daysLater";
        }
    }
}
