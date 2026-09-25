using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Aedes.Module3.Sim
{
    /// <summary>
    /// How safe a parameter is to change while a session is already running.
    /// </summary>
    public enum ApplyScope
    {
        /// <summary>
        /// Takes effect on the next simulated day. Rates and multipliers: nothing already
        /// scheduled depends on them, so changing them mid-outbreak is consistent.
        /// </summary>
        NextDay,

        /// <summary>
        /// Takes effect for people and mosquitoes infected AFTER the change. A person's incubation
        /// period is fixed at the moment they are infected, so a new value cannot be applied
        /// retroactively without rewriting history - the neighbourhood simply ends up holding two
        /// generations under slightly different numbers, which is recorded rather than hidden.
        /// </summary>
        FutureInfections,

        /// <summary>
        /// Cannot take effect until a new session. The neighbourhood is already built: changing
        /// how many plots it has, or what Module 2 left in the yards, would mean rebuilding it
        /// underneath the player.
        /// </summary>
        NextSession,
    }

    public sealed class ParameterDescriptor
    {
        public string Key;
        public string Unit;
        public string Section;
        public string Notes;
        public ApplyScope Scope;
    }

    /// <summary>
    /// Everything GAMA hands over: the clinical and population numbers, plus the shape of the
    /// neighbourhood the squad arrives into.
    ///
    /// Section 5 of the design asks for the mosquito population to be "driven by the GAMA model
    /// rather than scripted", and open question 3b asks how the state of the yards carries over
    /// from Module 2. Both arrive here.
    /// </summary>
    public sealed class ParameterSet
    {
        public Module3Config Config = Module3Config.Default;
        public ScenarioOptions Scenario = ScenarioOptions.Default;

        /// <summary>Keys in the file that this build does not know about, kept for diagnostics.</summary>
        public readonly List<string> UnknownKeys = new List<string>();

        /// <summary>Rows that could not be read - a bad number, a missing column.</summary>
        public readonly List<string> Errors = new List<string>();

        /// <summary>
        /// The keys this particular file actually carried. GAMA may send only what changed, so
        /// knowing which keys were present is what lets the receiver work out whether anything
        /// arrived that cannot take effect until the next session.
        /// </summary>
        public readonly List<string> PresentKeys = new List<string>();

        /// <summary>Where this set came from: a Resources file, a GAMA message, a test.</summary>
        public string Origin = "defaults";

        /// <summary>GAMA's own identifier for the run that produced it, if it sent one.</summary>
        public string SourceRunId = "";

        public bool IsValid => Errors.Count == 0;

        public ParameterSet Clone()
        {
            var copy = new ParameterSet
            {
                Config = Config.Clone(),
                Scenario = Scenario,
                Origin = Origin,
                SourceRunId = SourceRunId,
            };
            copy.UnknownKeys.AddRange(UnknownKeys);
            copy.Errors.AddRange(Errors);
            copy.PresentKeys.AddRange(PresentKeys);
            return copy;
        }
    }

    /// <summary>
    /// Reads and writes the parameter CSV that GAMA produces.
    ///
    /// The writer matters as much as the reader: <see cref="WriteTemplate"/> emits the current
    /// values with their units, sections and notes, which is the document handed to the GAMA
    /// modellers so both sides agree on key names before either writes code against them.
    /// </summary>
    public static class ParameterCsv
    {
        public const string KeyColumn = "key";
        public const string ValueColumn = "value";

        private static readonly List<ParameterDescriptor> Schema = BuildSchema();

        public static IReadOnlyList<ParameterDescriptor> Descriptors => Schema;

        // -----------------------------------------------------------------------------------

        /// <param name="baseSet">
        /// What the file is applied on top of. GAMA may send only the keys that changed, and a
        /// partial file must not silently reset everything else to the build's defaults - so
        /// mid-session reads pass the set currently in force.
        /// </param>
        public static ParameterSet Parse(string csvText, string origin = "csv", ParameterSet baseSet = null)
        {
            var set = baseSet != null ? baseSet.Clone() : new ParameterSet();
            set.Origin = origin;
            set.Errors.Clear();
            set.UnknownKeys.Clear();
            set.PresentKeys.Clear();
            var rows = Csv.Parse(csvText);

            if (rows.Count == 0)
            {
                set.Errors.Add("The parameter file is empty.");
                return set;
            }

            // Locate the key and value columns, so GAMA is free to add columns of its own (units,
            // provenance, a comment) without breaking the reader. Leading '#' lines are skipped:
            // the template carries its own instructions at the top and must survive a round trip.
            int keyCol = -1, valueCol = -1, headerRow = -1;
            for (int r = 0; r < rows.Count && headerRow < 0; r++)
            {
                var candidate = rows[r];
                if (candidate.Count == 0) continue;
                if (candidate[0].TrimStart().StartsWith("#")) continue;

                int k = -1, v = -1;
                for (int i = 0; i < candidate.Count; i++)
                {
                    string h = candidate[i].Trim().ToLowerInvariant();
                    if (h == KeyColumn) k = i;
                    else if (h == ValueColumn) v = i;
                }
                if (k >= 0 && v >= 0) { keyCol = k; valueCol = v; headerRow = r; }
            }

            if (headerRow < 0)
            {
                set.Errors.Add($"No header row with '{KeyColumn}' and '{ValueColumn}' columns was found.");
                return set;
            }

            for (int r = headerRow + 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count <= keyCol) continue;

                string key = row[keyCol].Trim();
                if (key.Length == 0 || key.StartsWith("#")) continue;

                string raw = row.Count > valueCol ? row[valueCol].Trim() : "";
                if (raw.Length == 0) continue;

                if (key == "source_run_id") { set.SourceRunId = raw; continue; }

                if (Apply(set, key, raw, out string error))
                {
                    set.PresentKeys.Add(key);
                }
                else if (error == null) set.UnknownKeys.Add(key);
                else set.Errors.Add($"row {r + 1}: {error}");
            }

            Validate(set);
            return set;
        }

        /// <summary>
        /// Emits the set as a CSV with units, sections, notes and the apply scope of each key.
        /// This is the template to hand the GAMA side.
        /// </summary>
        public static string WriteTemplate(ParameterSet set)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# AEDES Module 3 - simulation parameters");
            sb.AppendLine("# Generated by GAMA and read by Unity. Only 'key' and 'value' are read;");
            sb.AppendLine("# the other columns are documentation and may be reordered or extended.");
            sb.AppendLine("# apply_scope says when a value sent mid-session can take effect:");
            sb.AppendLine("#   next_day          - from the next simulated day");
            sb.AppendLine("#   future_infections - only for people infected after the change");
            sb.AppendLine("#   next_session      - not until the neighbourhood is rebuilt");
            sb.AppendLine("key,value,unit,section,apply_scope,notes");

            for (int i = 0; i < Schema.Count; i++)
            {
                var d = Schema[i];
                sb.Append(d.Key).Append(',')
                  .Append(Csv.Quote(Read(set, d.Key))).Append(',')
                  .Append(Csv.Quote(d.Unit)).Append(',')
                  .Append(Csv.Quote(d.Section)).Append(',')
                  .Append(ScopeName(d.Scope)).Append(',')
                  .Append(Csv.Quote(d.Notes)).AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>
        /// True if any of these keys cannot take effect until the neighbourhood is rebuilt.
        /// </summary>
        public static bool RequiresRestart(IEnumerable<string> keys, out List<string> blocking)
        {
            blocking = new List<string>();
            foreach (var k in keys)
            {
                if (ScopeOf(k) == ApplyScope.NextSession) blocking.Add(k);
            }
            return blocking.Count > 0;
        }

        public static ApplyScope ScopeOf(string key)
        {
            for (int i = 0; i < Schema.Count; i++) if (Schema[i].Key == key) return Schema[i].Scope;
            return ApplyScope.NextSession;
        }

        private static string ScopeName(ApplyScope s) =>
            s == ApplyScope.NextDay ? "next_day"
            : s == ApplyScope.FutureInfections ? "future_infections"
            : "next_session";

        // -----------------------------------------------------------------------------------
        // Sanity checks. A parameter file that GAMA generated is still a file that can be wrong,
        // and a classroom is a bad place to find out.
        // -----------------------------------------------------------------------------------

        private static void Validate(ParameterSet set)
        {
            var c = set.Config;
            Range(set, "asymptomatic_share", c.AsymptomaticShare, 0, 1);
            Range(set, "asymptomatic_relative_infectiousness", c.AsymptomaticRelativeInfectiousness, 0, 1);
            Range(set, "warning_sign_share", c.WarningSignShare, 0, 1);
            Range(set, "daily_bite_probability", c.DailyBiteProbability, 0, 1);
            Range(set, "daily_dispersal_probability", c.DailyDispersalProbability, 0, 1);
            Range(set, "probability_human_to_mosquito", c.ProbabilityHumanToMosquito, 0, 1);
            Range(set, "probability_mosquito_to_human", c.ProbabilityMosquitoToHuman, 0, 1);
            Range(set, "net_over_resting_person", c.NetOverRestingPerson, 0, 1);
            Range(set, "net_over_active_person", c.NetOverActivePerson, 0, 1);
            Range(set, "module2_container_clearance", set.Scenario.Module2.ContainerClearance, 0, 1);

            Ordered(set, "incubation", c.IncubationMinDays, c.IncubationMaxDays);
            Ordered(set, "fever", c.FeverMinDays, c.FeverMaxDays);
            Ordered(set, "extrinsic_incubation", c.ExtrinsicIncubationMinDays, c.ExtrinsicIncubationMaxDays);
            Ordered(set, "mosquito_lifespan", c.MosquitoLifespanMinDays, c.MosquitoLifespanMaxDays);

            if (c.NetsPerSession >= set.Scenario.Lanes * set.Scenario.HouseholdsPerLane)
            {
                // Section 5: "The nets available to the player should be limited in number - fewer
                // than there are households. Give them enough for everyone and there is nothing
                // to learn." This one is a design invariant, not a preference.
                set.Errors.Add($"nets_per_session ({c.NetsPerSession}) must be fewer than the number of "
                               + $"households ({set.Scenario.Lanes * set.Scenario.HouseholdsPerLane}); "
                               + "the shortage is what makes the player choose.");
            }

            if (c.UnreferredWarningGraceDays <= c.DaysPerHandover)
            {
                // Section 4 calls the referral rule the heart of the module. If a warning sign can
                // appear and resolve itself inside one handover jump, the squad never sees it and
                // cannot act on it - the lesson becomes unreachable rather than merely hard.
                set.Errors.Add($"unreferred_warning_grace_days ({c.UnreferredWarningGraceDays}) must be greater "
                               + $"than days_per_handover ({c.DaysPerHandover}), or a warning sign can come and go "
                               + "between two turns and the squad never gets the chance to refer.");
            }

            if (c.NetOverRestingPerson >= c.NetOverActivePerson)
            {
                set.Errors.Add("net_over_resting_person must be well below net_over_active_person: "
                               + "a net protects a patient who is lying down during the day, which is the "
                               + "distinction the module is built to teach.");
            }
        }

        private static void Range(ParameterSet set, string key, double v, double lo, double hi)
        {
            if (v < lo || v > hi) set.Errors.Add($"{key} = {v} is outside {lo}..{hi}.");
        }

        private static void Ordered(ParameterSet set, string name, int min, int max)
        {
            if (min > max) set.Errors.Add($"{name}_min_days ({min}) is greater than {name}_max_days ({max}).");
            if (min < 0) set.Errors.Add($"{name}_min_days ({min}) is negative.");
        }

        // -----------------------------------------------------------------------------------

        private static bool Apply(ParameterSet set, string key, string raw, out string error)
        {
            error = null;
            var c = set.Config;

            switch (key)
            {
                // clinical - human
                case "incubation_min_days": return Int(raw, ref c.IncubationMinDays, out error);
                case "incubation_max_days": return Int(raw, ref c.IncubationMaxDays, out error);
                case "fever_min_days": return Int(raw, ref c.FeverMinDays, out error);
                case "fever_max_days": return Int(raw, ref c.FeverMaxDays, out error);
                case "infectious_days_before_onset": return Int(raw, ref c.InfectiousDaysBeforeOnset, out error);
                case "infectious_days_after_onset": return Int(raw, ref c.InfectiousDaysAfterOnset, out error);
                case "asymptomatic_share": return Dbl(raw, ref c.AsymptomaticShare, out error);
                case "asymptomatic_infectious_days": return Int(raw, ref c.AsymptomaticInfectiousDays, out error);
                case "asymptomatic_relative_infectiousness":
                    return Dbl(raw, ref c.AsymptomaticRelativeInfectiousness, out error);
                case "warning_sign_share": return Dbl(raw, ref c.WarningSignShare, out error);
                case "warning_sign_onset_offset_min": return Int(raw, ref c.WarningSignOnsetOffsetMin, out error);
                case "warning_sign_onset_offset_max": return Int(raw, ref c.WarningSignOnsetOffsetMax, out error);
                case "unreferred_warning_grace_days": return Int(raw, ref c.UnreferredWarningGraceDays, out error);

                // clinical - mosquito
                case "extrinsic_incubation_min_days": return Int(raw, ref c.ExtrinsicIncubationMinDays, out error);
                case "extrinsic_incubation_max_days": return Int(raw, ref c.ExtrinsicIncubationMaxDays, out error);

                // population rates
                case "mosquito_lifespan_min_days": return Int(raw, ref c.MosquitoLifespanMinDays, out error);
                case "mosquito_lifespan_max_days": return Int(raw, ref c.MosquitoLifespanMaxDays, out error);
                case "emergence_per_container_per_day": return Dbl(raw, ref c.EmergencePerContainerPerDay, out error);
                case "max_mosquitoes_per_household": return Int(raw, ref c.MaxMosquitoesPerHousehold, out error);
                case "standing_population_days": return Int(raw, ref c.StandingPopulationDays, out error);
                case "daily_bite_probability": return Dbl(raw, ref c.DailyBiteProbability, out error);
                case "daily_dispersal_probability": return Dbl(raw, ref c.DailyDispersalProbability, out error);
                case "probability_human_to_mosquito": return Dbl(raw, ref c.ProbabilityHumanToMosquito, out error);
                case "probability_mosquito_to_human": return Dbl(raw, ref c.ProbabilityMosquitoToHuman, out error);

                // protective measures
                case "net_over_resting_person": return Dbl(raw, ref c.NetOverRestingPerson, out error);
                case "net_over_active_person": return Dbl(raw, ref c.NetOverActivePerson, out error);
                case "intact_screen": return Dbl(raw, ref c.IntactScreen, out error);
                case "torn_screen": return Dbl(raw, ref c.TornScreen, out error);
                case "closed_during_biting_hours": return Dbl(raw, ref c.ClosedDuringBitingHours, out error);
                case "electric_fan": return Dbl(raw, ref c.ElectricFan, out error);
                case "air_conditioning": return Dbl(raw, ref c.AirConditioning, out error);
                case "repellent": return Dbl(raw, ref c.Repellent, out error);
                case "repellent_duration_days": return Int(raw, ref c.RepellentDurationDays, out error);
                case "long_sleeves": return Dbl(raw, ref c.LongSleeves, out error);

                // session shape
                case "nets_per_session": return Int(raw, ref c.NetsPerSession, out error);
                case "turn_seconds": return Flt(raw, ref c.TurnSeconds, out error);
                case "turns_per_round": return Int(raw, ref c.TurnsPerRound, out error);
                case "rounds": return Int(raw, ref c.Rounds, out error);
                case "days_per_handover": return Int(raw, ref c.DaysPerHandover, out error);
            }

            // scenario (struct, so it has to be read back out)
            var s = set.Scenario;
            bool ok;
            switch (key)
            {
                case "households_per_lane": ok = Int(raw, ref s.HouseholdsPerLane, out error); break;
                case "lanes": ok = Int(raw, ref s.Lanes, out error); break;
                case "module2_container_clearance":
                    ok = Dbl(raw, ref s.Module2.ContainerClearance, out error); break;
                case "index_case_head_start_days": ok = Int(raw, ref s.IndexCaseHeadStartDays, out error); break;
                case "seed_asymptomatic_index_case": ok = Bool(raw, ref s.SeedAsymptomaticIndexCase, out error); break;
                case "min_productive_containers_per_household":
                    ok = Int(raw, ref s.MinProductiveContainersPerHousehold, out error); break;
                case "guaranteed_warning_sign_cases":
                    ok = Int(raw, ref s.GuaranteedWarningSignCases, out error); break;
                default: return false;
            }
            set.Scenario = s;
            return ok;
        }

        private static string Read(ParameterSet set, string key)
        {
            var c = set.Config;
            var s = set.Scenario;
            switch (key)
            {
                case "incubation_min_days": return Str(c.IncubationMinDays);
                case "incubation_max_days": return Str(c.IncubationMaxDays);
                case "fever_min_days": return Str(c.FeverMinDays);
                case "fever_max_days": return Str(c.FeverMaxDays);
                case "infectious_days_before_onset": return Str(c.InfectiousDaysBeforeOnset);
                case "infectious_days_after_onset": return Str(c.InfectiousDaysAfterOnset);
                case "asymptomatic_share": return Str(c.AsymptomaticShare);
                case "asymptomatic_infectious_days": return Str(c.AsymptomaticInfectiousDays);
                case "asymptomatic_relative_infectiousness": return Str(c.AsymptomaticRelativeInfectiousness);
                case "warning_sign_share": return Str(c.WarningSignShare);
                case "warning_sign_onset_offset_min": return Str(c.WarningSignOnsetOffsetMin);
                case "warning_sign_onset_offset_max": return Str(c.WarningSignOnsetOffsetMax);
                case "unreferred_warning_grace_days": return Str(c.UnreferredWarningGraceDays);
                case "extrinsic_incubation_min_days": return Str(c.ExtrinsicIncubationMinDays);
                case "extrinsic_incubation_max_days": return Str(c.ExtrinsicIncubationMaxDays);
                case "mosquito_lifespan_min_days": return Str(c.MosquitoLifespanMinDays);
                case "mosquito_lifespan_max_days": return Str(c.MosquitoLifespanMaxDays);
                case "emergence_per_container_per_day": return Str(c.EmergencePerContainerPerDay);
                case "max_mosquitoes_per_household": return Str(c.MaxMosquitoesPerHousehold);
                case "standing_population_days": return Str(c.StandingPopulationDays);
                case "daily_bite_probability": return Str(c.DailyBiteProbability);
                case "daily_dispersal_probability": return Str(c.DailyDispersalProbability);
                case "probability_human_to_mosquito": return Str(c.ProbabilityHumanToMosquito);
                case "probability_mosquito_to_human": return Str(c.ProbabilityMosquitoToHuman);
                case "net_over_resting_person": return Str(c.NetOverRestingPerson);
                case "net_over_active_person": return Str(c.NetOverActivePerson);
                case "intact_screen": return Str(c.IntactScreen);
                case "torn_screen": return Str(c.TornScreen);
                case "closed_during_biting_hours": return Str(c.ClosedDuringBitingHours);
                case "electric_fan": return Str(c.ElectricFan);
                case "air_conditioning": return Str(c.AirConditioning);
                case "repellent": return Str(c.Repellent);
                case "repellent_duration_days": return Str(c.RepellentDurationDays);
                case "long_sleeves": return Str(c.LongSleeves);
                case "nets_per_session": return Str(c.NetsPerSession);
                case "turn_seconds": return Str(c.TurnSeconds);
                case "turns_per_round": return Str(c.TurnsPerRound);
                case "rounds": return Str(c.Rounds);
                case "days_per_handover": return Str(c.DaysPerHandover);
                case "households_per_lane": return Str(s.HouseholdsPerLane);
                case "lanes": return Str(s.Lanes);
                case "module2_container_clearance": return Str(s.Module2.ContainerClearance);
                case "index_case_head_start_days": return Str(s.IndexCaseHeadStartDays);
                case "seed_asymptomatic_index_case": return s.SeedAsymptomaticIndexCase ? "true" : "false";
                case "min_productive_containers_per_household": return Str(s.MinProductiveContainersPerHousehold);
                case "guaranteed_warning_sign_cases": return Str(s.GuaranteedWarningSignCases);
            }
            return "";
        }

        private static string Str(int v) => v.ToString(CultureInfo.InvariantCulture);
        private static string Str(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
        private static string Str(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

        private static bool Int(string raw, ref int target, out string error)
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
            {
                target = v; error = null; return true;
            }
            // GAMA writes whole numbers as floats often enough that refusing "4.0" would be petty.
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                target = (int)d; error = null; return true;
            }
            error = $"'{raw}' is not a whole number."; return false;
        }

        private static bool Dbl(string raw, ref double target, out string error)
        {
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                target = v; error = null; return true;
            }
            error = $"'{raw}' is not a number."; return false;
        }

        private static bool Flt(string raw, ref float target, out string error)
        {
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            {
                target = v; error = null; return true;
            }
            error = $"'{raw}' is not a number."; return false;
        }

        private static bool Bool(string raw, ref bool target, out string error)
        {
            string v = raw.Trim().ToLowerInvariant();
            if (v == "true" || v == "1" || v == "yes") { target = true; error = null; return true; }
            if (v == "false" || v == "0" || v == "no") { target = false; error = null; return true; }
            error = $"'{raw}' is not true or false."; return false;
        }

        private static List<ParameterDescriptor> BuildSchema()
        {
            var l = new List<ParameterDescriptor>();
            void D(string key, string unit, string section, ApplyScope scope, string notes) =>
                l.Add(new ParameterDescriptor { Key = key, Unit = unit, Section = section, Scope = scope, Notes = notes });

            const string clin = "clinical";
            const string ento = "entomology";
            const string rates = "population-rates";
            const string prot = "protective-measures";
            const string sess = "session";
            const string scen = "scenario";

            D("incubation_min_days", "days", clin, ApplyScope.FutureInfections, "Bite to symptoms, shortest. Design section 5: four to ten days. Do not compress.");
            D("incubation_max_days", "days", clin, ApplyScope.FutureInfections, "Bite to symptoms, longest.");
            D("fever_min_days", "days", clin, ApplyScope.FutureInfections, "Febrile period, shortest.");
            D("fever_max_days", "days", clin, ApplyScope.FutureInfections, "Febrile period, longest.");
            D("infectious_days_before_onset", "days", clin, ApplyScope.FutureInfections, "Viraemia begins this many days before symptoms.");
            D("infectious_days_after_onset", "days", clin, ApplyScope.FutureInfections, "Last day a patient can infect a mosquito, counted from symptom onset.");
            D("asymptomatic_share", "fraction 0-1", clin, ApplyScope.FutureInfections, "Share of infections that never feel ill. Section 5: half to three quarters. Pending Dr Marcombe.");
            D("asymptomatic_infectious_days", "days", clin, ApplyScope.FutureInfections, "Length of the infectious window for someone with no symptoms.");
            D("asymptomatic_relative_infectiousness", "fraction 0-1", clin, ApplyScope.FutureInfections, "How infectious to mosquitoes a symptomless case is, relative to a febrile one. Section 5: virus levels peak during fever. Below 1, or netting the visible patients stops being the strongest action.");
            D("warning_sign_share", "fraction 0-1", clin, ApplyScope.FutureInfections, "Background rate of bleeding or persistent vomiting among symptomatic cases.");
            D("warning_sign_onset_offset_min", "days", clin, ApplyScope.FutureInfections, "Earliest warning-sign onset relative to the end of fever. Section 8: danger arrives as the fever comes down.");
            D("warning_sign_onset_offset_max", "days", clin, ApplyScope.FutureInfections, "Latest warning-sign onset relative to the end of fever.");
            D("unreferred_warning_grace_days", "days", clin, ApplyScope.NextDay, "How long before an unreferred household takes itself to hospital. A consequence shown to the player, never a penalty.");
            D("extrinsic_incubation_min_days", "days", ento, ApplyScope.FutureInfections, "Shortest time before a mosquito that fed on a case can transmit. Section 5: eight to twelve days.");
            D("extrinsic_incubation_max_days", "days", ento, ApplyScope.FutureInfections, "Longest extrinsic incubation period.");

            D("mosquito_lifespan_min_days", "days", rates, ApplyScope.NextDay, "Shortest adult female lifespan.");
            D("mosquito_lifespan_max_days", "days", rates, ApplyScope.NextDay, "Longest adult female lifespan.");
            D("emergence_per_container_per_day", "adults/container/day", rates, ApplyScope.NextDay, "Adults emerging from each productive container each day. The main lever the GAMA container model drives.");
            D("max_mosquitoes_per_household", "count", rates, ApplyScope.NextDay, "Ceiling per plot, so a neglected neighbourhood stays playable.");
            D("standing_population_days", "days", rates, ApplyScope.NextSession, "Days' worth of emergence already on the wing on day one. Section 5: the mosquitoes are already flying.");
            D("daily_bite_probability", "probability", rates, ApplyScope.NextDay, "Chance a given mosquito seeks a blood meal on a given day.");
            D("daily_dispersal_probability", "probability", rates, ApplyScope.NextDay, "Chance of moving to an adjacent plot. Aedes aegypti travels very little.");
            D("probability_human_to_mosquito", "probability", rates, ApplyScope.NextDay, "Chance a bite on an infectious person infects the mosquito.");
            D("probability_mosquito_to_human", "probability", rates, ApplyScope.NextDay, "Chance a bite by an infectious mosquito infects the person.");

            D("net_over_resting_person", "multiplier 0-1", prot, ApplyScope.NextDay, "Bite risk under a net while lying down during the day. The strongest measure in the module.");
            D("net_over_active_person", "multiplier 0-1", prot, ApplyScope.NextDay, "The same net over someone up and about - close to no protection, because they are not under it when Aedes bites. Must stay well above net_over_resting_person.");
            D("intact_screen", "multiplier 0-1", prot, ApplyScope.NextDay, "Window and door screens in good repair.");
            D("torn_screen", "multiplier 0-1", prot, ApplyScope.NextDay, "A screen with a hole in it - close to useless, which is why repairing it is worth the player's time.");
            D("closed_during_biting_hours", "multiplier 0-1", prot, ApplyScope.NextDay, "Shutting up after dawn and before dusk. Costs nothing, available to everyone.");
            D("electric_fan", "multiplier 0-1", prot, ApplyScope.NextDay, "Air movement disrupts flight and the plume mosquitoes home in on.");
            D("air_conditioning", "multiplier 0-1", prot, ApplyScope.NextDay, "Include it, but the module must not depend on it - uncommon in these households.");
            D("repellent", "multiplier 0-1", prot, ApplyScope.NextDay, "Applied to exposed skin.");
            D("repellent_duration_days", "days", prot, ApplyScope.NextDay, "How long one application lasts.");
            D("long_sleeves", "multiplier 0-1", prot, ApplyScope.NextDay, "Free, and uncomfortable with a fever.");

            D("nets_per_session", "count", sess, ApplyScope.NextSession, "Must be fewer than the number of households. Section 5: the choice is the whole point.");
            D("turn_seconds", "seconds", sess, ApplyScope.NextDay, "Section 8: two to four minutes, fixed in playtesting.");
            D("turns_per_round", "count", sess, ApplyScope.NextSession, "One per squad member.");
            D("rounds", "count", sess, ApplyScope.NextSession, "Five or six makes a session.");
            D("days_per_handover", "days", sess, ApplyScope.NextDay, "How far the calendar jumps when the headset changes hands.");

            D("households_per_lane", "count", scen, ApplyScope.NextSession, "Section 11: eight to ten plots along two or three lanes.");
            D("lanes", "count", scen, ApplyScope.NextSession, "Number of lanes.");
            D("module2_container_clearance", "fraction 0-1", scen, ApplyScope.NextSession, "What the squad cleared in Module 2. Open question 3b - this is how that state carries over.");
            D("index_case_head_start_days", "days", scen, ApplyScope.NextSession, "How long the first patients have been ill when the squad arrives.");
            D("seed_asymptomatic_index_case", "true/false", scen, ApplyScope.NextSession, "Section 5: one chain must begin where nobody was ever visibly ill.");
            D("min_productive_containers_per_household", "count", scen, ApplyScope.NextSession, "Cryptic sites no sweep finds. Why perfect prevention still leaves mosquitoes.");
            D("guaranteed_warning_sign_cases", "count", scen, ApplyScope.NextSession, "Teaching guarantee, not a clinical rate: how many referral situations a session must present.");
            return l;
        }
    }
}
