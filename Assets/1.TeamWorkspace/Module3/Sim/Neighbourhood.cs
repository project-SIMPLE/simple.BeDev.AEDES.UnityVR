using System.Collections.Generic;

namespace Aedes.Module3.Sim
{
    /// <summary>
    /// The epidemic. Pure C# - no UnityEngine, no coroutines, no physics, no UnityEngine.Random.
    ///
    /// Everything Unity does in Module 3 is read this and send it <see cref="PlayerAction"/>s.
    /// The reason for that separation is section 5's ending: before any score is shown, the game
    /// replays what would have happened had the first patient been covered on day one. That is
    /// only possible if the whole outbreak can be re-run from a seed with one input changed, and
    /// it is impossible if the epidemic lives in Update().
    /// </summary>
    public sealed class Neighbourhood
    {
        public readonly int Seed;

        /// <summary>
        /// Parameters keyed by the day they took effect. GAMA may send a new set mid-session, so
        /// the model reads the timeline rather than a fixed object - see ParameterTimeline.
        /// </summary>
        public readonly ParameterTimeline Parameters;

        /// <summary>The parameters in force today.</summary>
        public Module3Config Config => Parameters.At(Day);

        public readonly List<Person> People = new List<Person>();
        public readonly List<Household> Households = new List<Household>();
        public readonly List<Mosquito> Mosquitoes = new List<Mosquito>();
        public readonly TransmissionLog Log = new TransmissionLog();

        public int Day { get; private set; }
        public int NetsRemaining { get; private set; }

        /// <summary>
        /// Warning-sign cases still owed to this session. The scenario builder sets it; the next
        /// symptomatic cases to appear take them up. See ScenarioOptions.GuaranteedWarningSignCases.
        /// </summary>
        public int PendingGuaranteedWarningSigns;

        private int nextMosquitoId;

        public Neighbourhood(int seed, Module3Config config)
            : this(seed, new ParameterTimeline(config ?? Module3Config.Default)) { }

        public Neighbourhood(int seed, ParameterTimeline parameters)
        {
            Seed = seed;
            Parameters = parameters ?? new ParameterTimeline(Module3Config.Default);
            NetsRemaining = Config.NetsPerSession;
        }

        /// <summary>
        /// Takes a parameter set that arrived mid-session. It applies from the NEXT day, never
        /// retroactively: people already infected keep the course of illness they were given when
        /// they were infected, because rewriting that would make the trace-back replay a lie
        /// about what the squad saw.
        /// </summary>
        public void StageParameters(Module3Config config, string origin)
        {
            Parameters.Stage(Day + 1, config, origin);
        }

        public Person PersonById(int id) => id >= 0 && id < People.Count ? People[id] : null;
        public Household HouseholdById(int id) => id >= 0 && id < Households.Count ? Households[id] : null;

        // -----------------------------------------------------------------------------------
        // Player actions
        // -----------------------------------------------------------------------------------

        public ActionResult Apply(PlayerAction action)
        {
            switch (action.Kind)
            {
                case ActionKind.Visit:
                {
                    var h = HouseholdById(action.HouseholdId);
                    if (h == null) return Fail(action);
                    h.LastVisitedDay = Day;
                    h.VisitCount++;
                    return Ok(action, "m3.action.visited");
                }

                case ActionKind.PutUpNet:
                {
                    var p = PersonById(action.PersonId);
                    if (p == null) return Fail(action);
                    if (p.HasNet) return Result(action, ActionOutcome.AlreadyDone, "m3.action.netAlready");
                    if (NetsRemaining <= 0)
                        return Result(action, ActionOutcome.NoNetsRemaining, "m3.action.noNetsLeft");
                    p.HasNet = true;
                    NetsRemaining--;
                    return Ok(action, "m3.action.netUp");
                }

                case ActionKind.ReclaimNet:
                {
                    // The volunteer carries the net on to the next house. The budget is about
                    // where the squad's attention goes, not about a warehouse - but taking a net
                    // off someone who still needs it has to cost them, so it is never automatic.
                    var p = PersonById(action.PersonId);
                    if (p == null || !p.HasNet) return Fail(action);
                    p.HasNet = false;
                    NetsRemaining++;
                    return Result(action, ActionOutcome.NetReclaimed, "m3.action.netReclaimed");
                }

                case ActionKind.RepairScreen:
                {
                    var h = HouseholdById(action.HouseholdId);
                    if (h == null) return Fail(action);
                    if (h.Screens == ScreenState.Intact)
                        return Result(action, ActionOutcome.AlreadyDone, "m3.action.screenAlready");
                    h.Screens = ScreenState.Intact;
                    return Ok(action, "m3.action.screenFixed");
                }

                case ActionKind.SetFan:
                {
                    var h = HouseholdById(action.HouseholdId);
                    if (h == null) return Fail(action);
                    h.HasFan = true;
                    return Ok(action, "m3.action.fanOn");
                }

                case ActionKind.AdviseClosingHours:
                {
                    var h = HouseholdById(action.HouseholdId);
                    if (h == null) return Fail(action);
                    h.ClosesDuringBitingHours = true;
                    return Ok(action, "m3.action.closingHours");
                }

                case ActionKind.GiveRepellent:
                {
                    var p = PersonById(action.PersonId);
                    if (p == null) return Fail(action);
                    p.RepellentUntilDay = Day + Config.RepellentDurationDays;
                    return Ok(action, "m3.action.repellent");
                }

                case ActionKind.BringWater:
                {
                    var p = PersonById(action.PersonId);
                    if (p == null) return Fail(action);
                    p.HasFluids = true;
                    return Ok(action, "m3.action.water");
                }

                case ActionKind.HelpRest:
                {
                    var p = PersonById(action.PersonId);
                    if (p == null) return Fail(action);
                    p.HasRested = true;
                    return Ok(action, "m3.action.rest");
                }

                case ActionKind.ReferToHealthCentre:
                {
                    var p = PersonById(action.PersonId);
                    if (p == null) return Fail(action);
                    if (p.HasVisibleWarningSign(Day))
                    {
                        p.Referred = true;
                        p.ReferredDay = Day;
                        p.State = HealthState.Hospitalised;
                        return Result(action, ActionOutcome.ReferralCorrect, "m3.action.referralCorrect");
                    }
                    // Section 10: gently corrected, never punished. The person stays at home,
                    // which is where someone with a fever and no warning signs belongs - so they
                    // are NOT marked Referred. That flag used to be set here too, and it then
                    // greyed out the referral action, kept their warning sign out of the handover
                    // brief, stopped the unreferred-warning consequence from ever firing, and
                    // scored them as a correct referral when the sign did come.
                    p.ReferredWithoutNeed = true;
                    return Result(action, ActionOutcome.ReferralNotNeeded,
                        p.IsFebrile(Day) ? "m3.action.referralNotNeeded" : "m3.action.referralNotNeededWell");
                }
            }

            return Fail(action);
        }

        private static ActionResult Ok(PlayerAction a, string key) => Result(a, ActionOutcome.Done, key);
        private static ActionResult Fail(PlayerAction a) => Result(a, ActionOutcome.Invalid, "m3.action.invalid");
        private static ActionResult Result(PlayerAction a, ActionOutcome o, string key) =>
            new ActionResult { Action = a, Outcome = o, MessageKey = key };

        // -----------------------------------------------------------------------------------
        // Time
        // -----------------------------------------------------------------------------------

        public void AdvanceDays(int days)
        {
            for (int i = 0; i < days; i++) AdvanceDay();
        }

        public void AdvanceDay()
        {
            Day++;
            AdvancePeople();
            AdvanceMosquitoes();
            Bite();
        }

        private void AdvancePeople()
        {
            for (int i = 0; i < People.Count; i++)
            {
                var p = People[i];

                if (p.State == HealthState.Exposed && Day >= p.InfectiousStartDay)
                {
                    p.State = HealthState.Infectious;
                }

                if (p.State == HealthState.Infectious)
                {
                    int recoversOn = p.IsAsymptomatic ? p.InfectiousEndDay : Max(p.InfectiousEndDay, p.FeverEndDay);
                    if (Day > recoversOn) p.State = HealthState.Recovered;
                }

                // An unreferred warning sign does not go away. Section 10: not points deducted,
                // a consequence shown - "on the next visit that household has gone to hospital,
                // and the player is told why."
                if (p.HasVisibleWarningSign(Day) && !p.Referred
                    && Day >= p.WarningOnsetDay + Config.UnreferredWarningGraceDays)
                {
                    p.State = HealthState.Hospitalised;
                    p.WentToHospitalUnaided = true;
                }
            }
        }

        private void AdvanceMosquitoes()
        {
            // Death first, so a mosquito never bites on the day it should already be gone.
            for (int i = 0; i < Mosquitoes.Count; i++)
            {
                var m = Mosquitoes[i];
                if (!m.Alive) continue;
                if (Day - m.EmergedDay >= m.LifespanDays) m.Alive = false;
            }

            // Extrinsic incubation.
            for (int i = 0; i < Mosquitoes.Count; i++)
            {
                var m = Mosquitoes[i];
                if (m.Alive && m.State == MosquitoState.Exposed && Day >= m.InfectiousFromDay)
                {
                    m.State = MosquitoState.Infectious;
                }
            }

            // Dispersal - only ever to an adjacent plot.
            for (int i = 0; i < Mosquitoes.Count; i++)
            {
                var m = Mosquitoes[i];
                if (!m.Alive) continue;
                var h = HouseholdById(m.HouseholdId);
                if (h == null || h.NeighbourIds.Count == 0) continue;
                if (!Rng.Chance(Config.DailyDispersalProbability, Seed, Day, RngStream.MosquitoDispersal, m.Id))
                    continue;
                int pick = Rng.Range(0, h.NeighbourIds.Count - 1, Seed, Day, RngStream.MosquitoDispersal, m.Id, 1);
                m.HouseholdId = h.NeighbourIds[pick];
            }

            // Emergence from whatever containers the squad left behind in Module 2.
            for (int i = 0; i < Households.Count; i++)
            {
                var h = Households[i];
                int productive = h.ProductiveContainerCount;
                if (productive == 0) continue;
                if (AliveInHousehold(h.Id) >= Config.MaxMosquitoesPerHousehold) continue;

                double expected = productive * Config.EmergencePerContainerPerDay;
                int whole = (int)expected;
                double frac = expected - whole;
                int count = whole;
                if (Rng.Unit(Seed, Day, RngStream.MosquitoEmergence, h.Id) < frac) count++;

                for (int n = 0; n < count; n++)
                {
                    var container = PickProductiveContainer(h, n);
                    if (container == null) break;
                    SpawnMosquito(h, container, Day);
                }
            }
        }

        /// <summary>
        /// Puts an adult mosquito on the wing. Used by the daily emergence loop and by the
        /// scenario builder, which seeds the population that is already flying on day one.
        /// </summary>
        public Mosquito SpawnMosquito(Household h, Container from, int emergedDay)
        {
            var m = new Mosquito
            {
                Id = nextMosquitoId,
                HouseholdId = h.Id,
                OriginHouseholdId = h.Id,
                OriginContainerId = from.Id,
                EmergedDay = emergedDay,
                LifespanDays = Rng.Range(Config.MosquitoLifespanMinDays, Config.MosquitoLifespanMaxDays,
                                         Seed, 0, RngStream.MosquitoLifespan, nextMosquitoId),
            };
            nextMosquitoId++;
            Mosquitoes.Add(m);
            return m;
        }

        public Container PickProductiveContainer(Household h, int salt)
        {
            int productive = h.ProductiveContainerCount;
            if (productive == 0) return null;
            int wanted = Rng.Range(0, productive - 1, Seed, Day, RngStream.Containers, h.Id, salt);
            for (int i = 0; i < h.Containers.Count; i++)
            {
                if (!h.Containers[i].IsProductive) continue;
                if (wanted-- == 0) return h.Containers[i];
            }
            return null;
        }

        private int AliveInHousehold(int householdId)
        {
            int n = 0;
            for (int i = 0; i < Mosquitoes.Count; i++)
            {
                if (Mosquitoes[i].Alive && Mosquitoes[i].HouseholdId == householdId) n++;
            }
            return n;
        }

        // -----------------------------------------------------------------------------------
        // Biting - where the chain is made and broken
        // -----------------------------------------------------------------------------------

        private readonly List<Person> biteCandidates = new List<Person>();
        private readonly List<double> biteWeights = new List<double>();

        private void Bite()
        {
            // Note who is under a net on a day they could have infected a mosquito, before any
            // biting happens. This is what "netted before a mosquito could feed on them" means.
            for (int i = 0; i < People.Count; i++)
            {
                var person = People[i];
                if (person.HasNet && person.IsInfectiousToMosquitoes(Day)) person.WasCoveredWhileInfectious = true;
            }

            for (int i = 0; i < Mosquitoes.Count; i++)
            {
                var m = Mosquitoes[i];
                if (!m.Alive) continue;
                if (!Rng.Chance(Config.DailyBiteProbability, Seed, Day, RngStream.Bite, m.Id)) continue;

                var target = ChooseBiteTarget(m);
                if (target == null) continue;

                // Mosquito to human.
                if (m.CanTransmit(Day) && target.State == HealthState.Susceptible
                    && Rng.Chance(Config.ProbabilityMosquitoToHuman, Seed, Day, RngStream.MosquitoToHuman, m.Id))
                {
                    var source = PersonById(m.AcquiredFromPersonId);
                    Infect(target, m, source);
                    continue;
                }

                // Human to mosquito.
                double acquire = Config.ProbabilityHumanToMosquito
                                 * (target.IsAsymptomatic ? Config.AsymptomaticRelativeInfectiousness : 1.0);
                if (m.State == MosquitoState.Susceptible && target.IsInfectiousToMosquitoes(Day)
                    && Rng.Chance(acquire, Seed, Day, RngStream.HumanToMosquito, m.Id))
                {
                    m.State = MosquitoState.Exposed;
                    target.EverFedOnWhileInfectious = true;
                    m.AcquiredFromPersonId = target.Id;
                    m.AcquiredFromHouseholdId = target.HouseholdId;
                    m.AcquiredOnDay = Day;
                    m.InfectiousFromDay = Day + Rng.Range(
                        Config.ExtrinsicIncubationMinDays, Config.ExtrinsicIncubationMaxDays,
                        Seed, 0, RngStream.MosquitoLifespan, m.Id, 7);
                }
            }
        }

        private Person ChooseBiteTarget(Mosquito m)
        {
            var h = HouseholdById(m.HouseholdId);
            if (h == null) return null;

            biteCandidates.Clear();
            biteWeights.Clear();
            double total = 0;

            for (int i = 0; i < h.ResidentIds.Count; i++)
            {
                var p = PersonById(h.ResidentIds[i]);
                if (p == null || p.State == HealthState.Hospitalised) continue;
                double w = ExposureWeight(p, h);
                if (w <= 0) continue;
                biteCandidates.Add(p);
                biteWeights.Add(w);
                total += w;
            }

            if (biteCandidates.Count == 0 || total <= 0) return null;

            // A mosquito looking for a meal finds whoever is in the house; whether it gets one
            // depends on what is between it and them. Protection is therefore a chance of
            // FAILING to feed, not a smaller share of a fixed number of bites - otherwise
            // netting one person would silently push the bites onto their family.
            int pick = Rng.Range(0, biteCandidates.Count - 1, Seed, Day, RngStream.BiteTarget, m.Id);
            var chosen = biteCandidates[pick];
            return Rng.Unit(Seed, Day, RngStream.BiteTarget, m.Id, 1) < biteWeights[pick] ? chosen : null;
        }

        /// <summary>
        /// How likely this person is to be bitten, 0 to 1. The ranking encoded here is section 6's
        /// and is expected to be re-ordered after Dr Marcombe's review - which is why it reads off
        /// <see cref="Module3Config"/> rather than hard-coding anything.
        /// </summary>
        public double ExposureWeight(Person p, Household h)
        {
            double w = 1.0;

            if (p.HasNet)
            {
                // The one distinction the whole module rests on. A net over someone lying down
                // during the day is close to total protection; the same net over someone who is
                // up and about is worth almost nothing, because they are not under it when
                // Aedes bites. Netting the sick and netting the well are therefore not variations
                // of one action - they are different actions, and the model says so.
                w *= p.IsRestingInBitingHours(Day) ? Config.NetOverRestingPerson : Config.NetOverActivePerson;
            }

            switch (h.Screens)
            {
                case ScreenState.Intact: w *= Config.IntactScreen; break;
                case ScreenState.Torn: w *= Config.TornScreen; break;
            }

            if (h.HasAirConditioning) w *= Config.AirConditioning;
            else if (h.HasFan) w *= Config.ElectricFan;

            if (h.ClosesDuringBitingHours) w *= Config.ClosedDuringBitingHours;
            if (p.RepellentUntilDay >= Day) w *= Config.Repellent;
            if (p.WearsLongSleeves) w *= Config.LongSleeves;

            return w;
        }

        // -----------------------------------------------------------------------------------
        // Infection
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// Seeds an index case - the outbreak the squad arrives into.
        /// <paramref name="onsetDay"/> pins the day symptoms show, so that "the first patient"
        /// really is visible on the squad's first turn. Section 5 turns on being able to cover
        /// them on day one, which is only a choice if they can be found on day one.
        /// </summary>
        public void SeedIndexCase(int personId, int daysAlreadyInfected, bool forceAsymptomatic = false,
                                  int onsetDay = int.MinValue, bool forceSymptomatic = false)
        {
            var p = PersonById(personId);
            if (p == null || p.State != HealthState.Susceptible) return;

            int exposed = Day - daysAlreadyInfected;
            if (onsetDay != int.MinValue)
            {
                int incubation = Rng.Range(Config.IncubationMinDays, Config.IncubationMaxDays,
                                           Seed, 0, RngStream.Incubation, p.Id);
                exposed = onsetDay - incubation;
            }
            ScheduleInfection(p, exposed, forceAsymptomatic, forceSymptomatic);
            Log.Add(new TransmissionEvent
            {
                Day = p.ExposedDay,
                TargetPersonId = p.Id,
                TargetHouseholdId = p.HouseholdId,
            });
            if (Day >= p.InfectiousStartDay) p.State = HealthState.Infectious;
        }

        private void Infect(Person target, Mosquito m, Person source)
        {
            ScheduleInfection(target, Day, false);
            Log.Add(new TransmissionEvent
            {
                Day = Day,
                TargetPersonId = target.Id,
                TargetHouseholdId = target.HouseholdId,
                SourcePersonId = m.AcquiredFromPersonId,
                SourceHouseholdId = m.AcquiredFromHouseholdId,
                MosquitoId = m.Id,
                OriginContainerId = m.OriginContainerId,
                OriginContainerKind = ContainerKindOf(m.OriginHouseholdId, m.OriginContainerId),
                // Judged on the day the mosquito fed, which is the moment the squad could have
                // acted - not on the day the new case shows up.
                SourceWasVisiblyIll = source != null && source.IsFebrile(m.AcquiredOnDay),
                SourceWasProtected = source != null && source.HasNet,
            });
        }

        private ContainerKind ContainerKindOf(int householdId, int containerId)
        {
            var h = HouseholdById(householdId);
            if (h == null) return ContainerKind.WaterJar;
            for (int i = 0; i < h.Containers.Count; i++)
            {
                if (h.Containers[i].Id == containerId) return h.Containers[i].Kind;
            }
            return ContainerKind.WaterJar;
        }

        /// <summary>
        /// Lays out the whole course of one person's infection at the moment they are infected.
        /// Every draw is keyed on the person's id and not on a running stream, so a counterfactual
        /// re-run gives the same person the same incubation period - only the infection itself
        /// differs. That is what makes the replay attributable to the net rather than to noise.
        /// </summary>
        private void ScheduleInfection(Person p, int exposedDay, bool forceAsymptomatic, bool forceSymptomatic = false)
        {
            p.State = HealthState.Exposed;
            p.HasBeenInfected = true;
            p.ExposedDay = exposedDay;

            int incubation = Rng.Range(Config.IncubationMinDays, Config.IncubationMaxDays,
                                       Seed, 0, RngStream.Incubation, p.Id);
            p.SymptomOnsetDay = exposedDay + incubation;

            p.IsAsymptomatic = forceSymptomatic
                ? false
                : forceAsymptomatic || Rng.Unit(Seed, 0, RngStream.Asymptomatic, p.Id) < Config.AsymptomaticShare;

            p.InfectiousStartDay = p.SymptomOnsetDay - Config.InfectiousDaysBeforeOnset;

            if (p.IsAsymptomatic)
            {
                p.FeverEndDay = -1;
                p.InfectiousEndDay = p.SymptomOnsetDay + Config.AsymptomaticInfectiousDays;
                return;
            }

            int fever = Rng.Range(Config.FeverMinDays, Config.FeverMaxDays,
                                  Seed, 0, RngStream.FeverDuration, p.Id);
            p.FeverEndDay = p.SymptomOnsetDay + fever;
            p.InfectiousEndDay = p.SymptomOnsetDay + Config.InfectiousDaysAfterOnset;

            bool warns = Rng.Unit(Seed, 0, RngStream.WarningSign, p.Id) < Config.WarningSignShare;
            if (!warns && PendingGuaranteedWarningSigns > 0)
            {
                // The session still owes the squad a referral to make. See the note on
                // ScenarioOptions.GuaranteedWarningSignCases - a teaching guarantee, not a
                // statement about how often dengue turns severe.
                warns = true;
                PendingGuaranteedWarningSigns--;
            }

            if (warns)
            {
                // Section 8: danger often arrives as the fever comes down.
                int offset = Rng.Range(Config.WarningSignOnsetOffsetMin, Config.WarningSignOnsetOffsetMax,
                                       Seed, 0, RngStream.WarningSignOnset, p.Id);
                p.WarningOnsetDay = p.FeverEndDay + offset;
                p.Warning = Rng.Unit(Seed, 0, RngStream.WarningSignOnset, p.Id, 1) < 0.5
                    ? WarningSign.Bleeding
                    : WarningSign.PersistentVomiting;
            }
        }

        /// <summary>
        /// Forces a warning sign onto a person who is already infected, at defervescence.
        /// Used by the scenario builder to guarantee the squad meets the referral situation -
        /// see ScenarioOptions.GuaranteedWarningSignCases for why that is a scenario decision
        /// rather than a change to the clinical rate.
        /// </summary>
        public bool ForceWarningSign(int personId)
        {
            var p = PersonById(personId);
            if (p == null || !p.HasBeenInfected || p.IsAsymptomatic) return false;
            if (p.Warning != WarningSign.None) return true;

            int offset = Rng.Range(Config.WarningSignOnsetOffsetMin, Config.WarningSignOnsetOffsetMax,
                                   Seed, 0, RngStream.WarningSignOnset, p.Id);
            p.WarningOnsetDay = p.FeverEndDay + offset;
            p.Warning = Rng.Unit(Seed, 0, RngStream.WarningSignOnset, p.Id, 1) < 0.5
                ? WarningSign.Bleeding
                : WarningSign.PersistentVomiting;
            return true;
        }

        private static int Max(int a, int b) => a > b ? a : b;

        // -----------------------------------------------------------------------------------
        // Readouts
        // -----------------------------------------------------------------------------------

        public int TotalCases => Log.CaseCount;

        public int SecondaryCases
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Log.Events.Count; i++) if (!Log.Events[i].IsIndexCase) n++;
                return n;
            }
        }

        public int AliveMosquitoCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Mosquitoes.Count; i++) if (Mosquitoes[i].Alive) n++;
                return n;
            }
        }

        /// <summary>Visibly unwell right now - what a Pilot walking in would find.</summary>
        public List<Person> VisiblyIll()
        {
            var list = new List<Person>();
            for (int i = 0; i < People.Count; i++)
            {
                if (People[i].IsFebrile(Day) || People[i].HasVisibleWarningSign(Day)) list.Add(People[i]);
            }
            return list;
        }
    }
}
