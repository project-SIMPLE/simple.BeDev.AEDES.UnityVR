using System.Collections.Generic;

namespace Aedes.Module3.Sim
{
    /// <summary>What one Pilot did during one turn, for the Analyst's Field Journal.</summary>
    public sealed class TurnRecord
    {
        public int TurnIndex;
        public int RoundIndex;
        public int DayStart;
        public int DayEnd;
        public readonly List<ActionResult> Actions = new List<ActionResult>();
        public readonly List<int> HouseholdsVisited = new List<int>();
    }

    /// <summary>
    /// The turn and handover structure: who is in the headset, for how long, and how far the
    /// calendar moves when they hand it over.
    ///
    /// Section 8 makes the handover carry the time. A turn is two to four minutes, which is far
    /// too short for anything in dengue to develop, so the game skips days between turns rather
    /// than speeding anything up - "The compression is in the calendar, not in the biology." That
    /// is why this class advances the model in whole days at a boundary and never continuously.
    ///
    /// Deliberately free of UnityEngine so the whole session shape can be tested headlessly. The
    /// MonoBehaviour that drives it only supplies the clock.
    /// </summary>
    public sealed class Session
    {
        public readonly Neighbourhood Neighbourhood;
        public readonly List<TurnRecord> Journal = new List<TurnRecord>();

        private readonly Module3Config config;
        private int lastLoggedEventCount;

        public int TurnIndex { get; private set; }
        public bool Finished { get; private set; }

        public Session(Neighbourhood neighbourhood)
        {
            Neighbourhood = neighbourhood;
            config = neighbourhood.Config;
            BeginTurn();
        }

        public int RoundIndex => config.TurnsPerRound > 0 ? TurnIndex / config.TurnsPerRound : 0;

        /// <summary>Which of the three squad members is in the headset this turn.</summary>
        public int PilotIndex => config.TurnsPerRound > 0 ? TurnIndex % config.TurnsPerRound : 0;

        public int TotalTurns => config.Rounds * config.TurnsPerRound;

        public TurnRecord CurrentTurn => Journal.Count > 0 ? Journal[Journal.Count - 1] : null;

        // -----------------------------------------------------------------------------------

        private void BeginTurn()
        {
            Journal.Add(new TurnRecord
            {
                TurnIndex = TurnIndex,
                RoundIndex = RoundIndex,
                DayStart = Neighbourhood.Day,
            });
            lastLoggedEventCount = Neighbourhood.Log.Events.Count;
        }

        /// <summary>
        /// The situation the squad arrives into, for the first Pilot. The index cases are not
        /// "new" - nothing happened to produce them during a turn - so they never appear in a
        /// handover brief, and without this the opening patients would go unannounced.
        /// </summary>
        public HandoverBrief OpeningBrief()
        {
            var brief = BuildBrief(Neighbourhood.Day);
            brief.DaysElapsed = 0;
            brief.NewCases.Clear();
            for (int i = 0; i < Neighbourhood.Log.Events.Count; i++)
            {
                if (Neighbourhood.Log.Events[i].IsIndexCase) brief.NewCases.Add(Neighbourhood.Log.Events[i]);
            }
            return brief;
        }

        /// <summary>Applies a player action and records it in the journal.</summary>
        public ActionResult Do(PlayerAction action)
        {
            if (Finished) return new ActionResult { Action = action, Outcome = ActionOutcome.Invalid };

            ActionResult result = Neighbourhood.Apply(action);
            var turn = CurrentTurn;
            turn.Actions.Add(result);

            if (action.Kind == ActionKind.Visit && result.Outcome == ActionOutcome.Done
                && !turn.HouseholdsVisited.Contains(action.HouseholdId))
            {
                turn.HouseholdsVisited.Add(action.HouseholdId);
            }

            return result;
        }

        /// <summary>
        /// Ends the turn: the calendar jumps, the world moves on, and the Coach gets something to
        /// brief the next Pilot with.
        /// </summary>
        public HandoverBrief EndTurn()
        {
            if (Finished) return null;

            var turn = CurrentTurn;
            int before = Neighbourhood.Day;

            Neighbourhood.AdvanceDays(config.DaysPerHandover);
            turn.DayEnd = Neighbourhood.Day;

            var brief = BuildBrief(before);

            TurnIndex++;
            if (TurnIndex >= TotalTurns)
            {
                Finished = true;
                brief.IsFinalHandover = true;
            }
            else
            {
                BeginTurn();
            }

            return brief;
        }

        private HandoverBrief BuildBrief(int dayBefore)
        {
            var brief = new HandoverBrief
            {
                TurnIndex = TurnIndex,
                RoundIndex = RoundIndex,
                DaysElapsed = Neighbourhood.Day - dayBefore,
                DayNow = Neighbourhood.Day,
                NetsRemaining = Neighbourhood.NetsRemaining,
            };

            var events = Neighbourhood.Log.Events;
            for (int i = lastLoggedEventCount; i < events.Count; i++) brief.NewCases.Add(events[i]);

            int day = Neighbourhood.Day;

            for (int i = 0; i < Neighbourhood.People.Count; i++)
            {
                var p = Neighbourhood.People[i];
                if (p.State == HealthState.Hospitalised) continue;

                if (p.HasVisibleWarningSign(day) && !p.Referred)
                {
                    brief.NeedReferralNow.Add(new PersonNote
                    {
                        PersonId = p.Id,
                        HouseholdId = p.HouseholdId,
                        MessageKey = p.Warning == WarningSign.Bleeding
                            ? "m3.brief.bleeding"
                            : "m3.brief.vomiting",
                    });
                    continue;
                }

                if (p.IsFebrile(day) && !p.HasNet)
                {
                    brief.UncoveredPatients.Add(new PersonNote
                    {
                        PersonId = p.Id,
                        HouseholdId = p.HouseholdId,
                        MessageKey = "m3.brief.uncovered",
                    });
                }
                else if (p.IsDefervescing(day))
                {
                    // Feeling better is not the all-clear.
                    brief.LookingBetterButWatch.Add(new PersonNote
                    {
                        PersonId = p.Id,
                        HouseholdId = p.HouseholdId,
                        MessageKey = "m3.brief.lookingBetter",
                    });
                }
            }

            for (int i = 0; i < Neighbourhood.Households.Count; i++)
            {
                var h = Neighbourhood.Households[i];
                if (h.VisitCount == 0) brief.HouseholdsNeverVisited.Add(h.Id);
                else if (day - h.LastVisitedDay > config.DaysPerHandover * config.TurnsPerRound)
                {
                    brief.HouseholdsNotSeenRecently.Add(h.Id);
                }
            }

            brief.ParameterChanges.AddRange(Neighbourhood.Parameters.Describe());
            return brief;
        }

        // -----------------------------------------------------------------------------------

        /// <summary>
        /// Everything the squad did, replayable. This is what the counterfactual is built from -
        /// section 5's "replays what would have happened had the first patient been covered on
        /// day one" re-runs these same actions with one net added on turn zero.
        /// </summary>
        public ScriptedActions RecordedActions()
        {
            var script = new ScriptedActions();
            for (int t = 0; t < Journal.Count; t++)
            {
                var turn = Journal[t];
                for (int a = 0; a < turn.Actions.Count; a++)
                {
                    script.Add(turn.TurnIndex, turn.Actions[a].Action);
                }
            }
            return script;
        }

        /// <summary>The first case seeded in this neighbourhood - who the counterfactual covers.</summary>
        public int PatientZeroId()
        {
            var events = Neighbourhood.Log.Events;
            for (int i = 0; i < events.Count; i++)
            {
                if (!events[i].IsIndexCase) continue;
                var p = Neighbourhood.PersonById(events[i].TargetPersonId);
                // The one the squad could actually have found: an index case nobody could see is
                // not a fair thing to replay at them.
                if (p != null && !p.IsAsymptomatic) return p.Id;
            }
            return events.Count > 0 ? events[0].TargetPersonId : -1;
        }
    }
}
