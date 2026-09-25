using System.Collections.Generic;

namespace Aedes.Module3.Sim
{
    public struct RunResult
    {
        public int Seed;
        public int TotalCases;
        public int SecondaryCases;
        public int PreventableCases;
        public int CasesFromInvisibleSource;
        public int HospitalisedUnaided;
        public int FinalMosquitoCount;
        public Neighbourhood Neighbourhood;
    }

    /// <summary>
    /// Plays a whole session without Unity. Used by the calibration tests, by the end-of-session
    /// counterfactual, and by anyone who wants to see what a parameter change does before opening
    /// the editor.
    /// </summary>
    public static class SimRunner
    {
        public static RunResult Run(int seed, Module3Config config, ScenarioOptions options, IStrategy strategy)
        {
            var n = ScenarioBuilder.Build(seed, config, options);
            int turns = config.Rounds * config.TurnsPerRound;

            for (int turn = 0; turn < turns; turn++)
            {
                strategy?.OnTurn(n, turn);
                // Section 8: the handover carries the time. The game skips days rather than
                // speeding anything up, so the biology a student learns stays true.
                n.AdvanceDays(config.DaysPerHandover);
            }

            int unaided = 0;
            for (int i = 0; i < n.People.Count; i++) if (n.People[i].WentToHospitalUnaided) unaided++;

            return new RunResult
            {
                Seed = seed,
                TotalCases = n.TotalCases,
                SecondaryCases = n.SecondaryCases,
                PreventableCases = n.Log.PreventableCount,
                CasesFromInvisibleSource = n.Log.InvisibleSourceCount,
                HospitalisedUnaided = unaided,
                FinalMosquitoCount = n.AliveMosquitoCount,
                Neighbourhood = n,
            };
        }

        /// <summary>
        /// Section 5: "At the end, before any score is shown, the game replays what would have
        /// happened had the first patient been covered on day one."
        ///
        /// The squad's own actions are replayed unchanged; the only difference is one net, put up
        /// on turn zero. Because every random draw is keyed on the entity rather than drawn from a
        /// running stream, the two outbreaks are genuinely comparable - the difference is the net.
        /// </summary>
        public static RunResult Counterfactual(int seed, Module3Config config, ScenarioOptions options,
                                               ScriptedActions squadActions, int patientZeroId)
        {
            var withNet = new ScriptedActions();
            withNet.Add(0, PlayerAction.OnPerson(ActionKind.PutUpNet, patientZeroId));
            if (squadActions != null) withNet.Absorb(squadActions);
            return Run(seed, config, options, withNet);
        }

        /// <summary>
        /// Runs one strategy over many seeds. The calibration harness asserts on the average,
        /// because a single outbreak is noisy and a teaching guarantee should not be.
        /// </summary>
        public static double AverageSecondaryCases(int seedCount, Module3Config config,
                                                   ScenarioOptions options, System.Func<IStrategy> strategy)
        {
            double total = 0;
            for (int seed = 1; seed <= seedCount; seed++)
            {
                total += Run(seed, config, options, strategy()).SecondaryCases;
            }
            return total / seedCount;
        }

        public static List<RunResult> RunMany(int seedCount, Module3Config config,
                                              ScenarioOptions options, System.Func<IStrategy> strategy)
        {
            var results = new List<RunResult>(seedCount);
            for (int seed = 1; seed <= seedCount; seed++) results.Add(Run(seed, config, options, strategy()));
            return results;
        }
    }
}
