using System.Collections.Generic;

namespace Aedes.Module3.Sim
{
    public enum ScoreReason
    {
        ChainBroken,
        ReferredCorrectly,
        GoodHomeCare,
        /// <summary>Not a deduction. Section 10: "not points deducted - a consequence shown."</summary>
        MissedWarningSign,
        /// <summary>Not a penalty. Section 10: "gently corrected, not punished."</summary>
        UnnecessaryReferral,
    }

    public struct ScoreLine
    {
        public ScoreReason Reason;
        public int Count;
        public int Points;
        public string MessageKey;
        /// <summary>A teaching moment rather than anything that moves the number.</summary>
        public bool IsNote;
    }

    /// <summary>
    /// Section 10's scoring: "failure, then consequence, then success. The player is not scored
    /// on speed."
    ///
    /// Two rules from that section are structural rather than cosmetic and are enforced by tests:
    /// breaking a chain outweighs everything else the squad can do put together, and nothing the
    /// player gets wrong ever subtracts. A missed warning sign and an unnecessary referral appear
    /// as notes with zero points, because the module corrects them by showing what happened, not
    /// by taking marks away.
    /// </summary>
    public sealed class Scorecard
    {
        public readonly List<ScoreLine> Lines = new List<ScoreLine>();

        public int Total;
        public int ChainsBroken;
        public int CorrectReferrals;
        public int HouseholdsGivenGoodCare;
        public int MissedWarningSigns;
        public int UnnecessaryReferrals;

        /// <summary>Points from breaking chains, as against points from everything else.</summary>
        public int ChainPoints;
        public int OtherPoints;
    }

    public static class Scoring
    {
        public static Scorecard Evaluate(Neighbourhood n, Session session)
        {
            var card = new Scorecard();
            var cfg = n.Config;

            for (int i = 0; i < n.People.Count; i++)
            {
                var p = n.People[i];
                if (!p.HasBeenInfected) continue;

                // Section 10: "A sick person netted or screened before a mosquito could feed on
                // them." Only counted for people the squad could actually have found - covering
                // someone with no symptoms is not something a player can aim at.
                if (!p.IsAsymptomatic && p.WasCoveredWhileInfectious && !p.EverFedOnWhileInfectious)
                {
                    card.ChainsBroken++;
                }

                if (p.Warning != WarningSign.None)
                {
                    if (p.WentToHospitalUnaided) card.MissedWarningSigns++;
                    else if (p.Referred) card.CorrectReferrals++;
                }
                else if (p.ReferredWithoutNeed)
                {
                    card.UnnecessaryReferrals++;
                }
            }

            // Good home care: rest, fluids, and a return visit. Section 7 step 6 - "Move on, and
            // come back. Returning matters."
            for (int i = 0; i < n.Households.Count; i++)
            {
                var h = n.Households[i];
                if (h.VisitCount < 2) continue;

                for (int r = 0; r < h.ResidentIds.Count; r++)
                {
                    var p = n.PersonById(h.ResidentIds[r]);
                    if (p != null && p.HasRested && p.HasFluids) { card.HouseholdsGivenGoodCare++; break; }
                }
            }

            card.ChainPoints = card.ChainsBroken * cfg.PointsChainBroken;
            card.OtherPoints = card.CorrectReferrals * cfg.PointsReferredCorrectly
                             + card.HouseholdsGivenGoodCare * cfg.PointsGoodHomeCare;
            card.Total = card.ChainPoints + card.OtherPoints;

            Add(card, ScoreReason.ChainBroken, card.ChainsBroken, card.ChainsBroken * cfg.PointsChainBroken,
                "m3.score.chainBroken", false);
            Add(card, ScoreReason.ReferredCorrectly, card.CorrectReferrals,
                card.CorrectReferrals * cfg.PointsReferredCorrectly, "m3.score.referredCorrectly", false);
            Add(card, ScoreReason.GoodHomeCare, card.HouseholdsGivenGoodCare,
                card.HouseholdsGivenGoodCare * cfg.PointsGoodHomeCare, "m3.score.goodHomeCare", false);

            // Both of these are worth exactly zero, on purpose.
            Add(card, ScoreReason.MissedWarningSign, card.MissedWarningSigns, 0, "m3.score.missedWarningSign", true);
            Add(card, ScoreReason.UnnecessaryReferral, card.UnnecessaryReferrals, 0,
                "m3.score.unnecessaryReferral", true);

            return card;
        }

        private static void Add(Scorecard card, ScoreReason reason, int count, int points, string key, bool note)
        {
            if (count == 0) return;
            card.Lines.Add(new ScoreLine
            {
                Reason = reason, Count = count, Points = points, MessageKey = key, IsNote = note,
            });
        }
    }
}
