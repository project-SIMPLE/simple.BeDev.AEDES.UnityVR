using System.Collections.Generic;

namespace Aedes.Module3.Sim
{
    /// <summary>
    /// What the squad left behind at the end of Module 2 (design section 5, open question 3b).
    /// Module 3 begins after prevention has already been decided; this is how that decision
    /// arrives. A squad that cleared the neighbourhood turns up to fewer mosquitoes.
    /// </summary>
    public struct Module2Outcome
    {
        /// 0 = nothing was cleared, 1 = every container in the neighbourhood was cleared.
        public double ContainerClearance;

        public static Module2Outcome NothingCleared => new Module2Outcome { ContainerClearance = 0.0 };
        public static Module2Outcome Typical => new Module2Outcome { ContainerClearance = 0.35 };

        /// <summary>
        /// The best a squad can realistically manage. Not 1.0: a blocked gutter, water under a
        /// slab and the jar behind the shed survive any sweep, which is why prevention lowers the
        /// mosquito population without ever removing it - and why Module 3 still has work to do.
        /// </summary>
        public static Module2Outcome EverythingCleared => new Module2Outcome { ContainerClearance = 0.80 };
    }

    public struct ScenarioOptions
    {
        public int HouseholdsPerLane;
        public int Lanes;
        public Module2Outcome Module2;

        /// Days the symptomatic index case has already been ill when the squad arrives.
        public int IndexCaseHeadStartDays;

        /// Section 5: "one chain begins at a house where nobody was ever visibly ill."
        public bool SeedAsymptomaticIndexCase;

        /// <summary>
        /// Even a squad that did Module 3 perfectly cannot clear what it cannot find - a blocked
        /// gutter, water under a slab. Every plot keeps at least this many productive containers,
        /// which is why "we cleared everything" never means "there are no mosquitoes" and why the
        /// outbreak still runs for a squad that did the container work and nothing else.
        /// </summary>
        public int MinProductiveContainersPerHousehold;

        /// <summary>
        /// How many cases are guaranteed to develop a warning sign during the session.
        ///
        /// This is a teaching guarantee, not a clinical claim. Section 4 calls the referral rule
        /// "the heart of the module" and section 10 calls acting on it "the strongest positive",
        /// so a session in which no warning sign ever appears has failed to teach the thing the
        /// module says students will use most often in life. The background rate in
        /// Module3Config.WarningSignShare stays honest for everybody else; this only makes sure
        /// the squad meets the situation at all.
        /// </summary>
        public int GuaranteedWarningSignCases;

        public static ScenarioOptions Default => new ScenarioOptions
        {
            // Section 11: "eight to ten household plots packed along two or three narrow lanes".
            HouseholdsPerLane = 5,
            Lanes = 2,
            Module2 = Module2Outcome.Typical,
            IndexCaseHeadStartDays = 4,
            SeedAsymptomaticIndexCase = true,
            MinProductiveContainersPerHousehold = 2,
            GuaranteedWarningSignCases = 2,
        };
    }

    public static class ScenarioBuilder
    {
        public static Neighbourhood Build(int seed, Module3Config config, ScenarioOptions options)
        {
            var n = new Neighbourhood(seed, config);

            int containerId = 0;

            for (int y = 0; y < options.Lanes; y++)
            {
                for (int x = 0; x < options.HouseholdsPerLane; x++)
                {
                    var h = new Household
                    {
                        Id = n.Households.Count,
                        LaneX = x,
                        LaneY = y,
                        Name = $"house.{y}.{x}",
                    };

                    // Section 6: screens are "widespread, though often torn or missing on one
                    // window - a good thing for the player to notice and fix."
                    double screenRoll = Rng.Unit(seed, 0, RngStream.Layout, h.Id, 1);
                    h.Screens = screenRoll < 0.35 ? ScreenState.Missing
                              : screenRoll < 0.80 ? ScreenState.Torn
                              : ScreenState.Intact;

                    // Fans are "very common"; air conditioning is "uncommon in the households this
                    // module depicts" and the game must not depend on it.
                    h.HasFan = Rng.Unit(seed, 0, RngStream.Layout, h.Id, 2) < 0.35;
                    h.HasAirConditioning = Rng.Unit(seed, 0, RngStream.Layout, h.Id, 3) < 0.08;

                    int containers = Rng.Range(2, 6, seed, 0, RngStream.Containers, h.Id);
                    for (int c = 0; c < containers; c++)
                    {
                        h.Containers.Add(new Container
                        {
                            Id = containerId++,
                            HouseholdId = h.Id,
                            Kind = (ContainerKind)Rng.Range(0, 5, seed, 0, RngStream.Containers, h.Id, 10 + c),
                            ClearedInModule2 =
                                Rng.Unit(seed, 0, RngStream.Containers, h.Id, 100 + c) < options.Module2.ContainerClearance,
                        });
                    }

                    EnsureResidualContainers(h, options.MinProductiveContainersPerHousehold);
                    n.Households.Add(h);
                }
            }

            // Neighbours: along the lane and directly across it. Section 5 - Aedes aegypti does not
            // travel far, so the chain runs between neighbours rather than across a district.
            for (int i = 0; i < n.Households.Count; i++)
            {
                var a = n.Households[i];
                for (int j = 0; j < n.Households.Count; j++)
                {
                    if (i == j) continue;
                    var b = n.Households[j];
                    int dx = a.LaneX - b.LaneX; if (dx < 0) dx = -dx;
                    int dy = a.LaneY - b.LaneY; if (dy < 0) dy = -dy;
                    if (dx + dy == 1) a.NeighbourIds.Add(b.Id);
                }
            }

            // Residents. Section 11: "ages varied, including children and older people."
            for (int i = 0; i < n.Households.Count; i++)
            {
                var h = n.Households[i];
                int residents = Rng.Range(3, 5, seed, 0, RngStream.Residents, h.Id);
                for (int r = 0; r < residents; r++)
                {
                    var p = new Person
                    {
                        Id = n.People.Count,
                        HouseholdId = h.Id,
                        Age = (AgeBand)Rng.Range(0, 2, seed, 0, RngStream.Residents, h.Id, 10 + r),
                        Name = $"person.{h.Id}.{r}",
                    };
                    h.ResidentIds.Add(p.Id);
                    n.People.Add(p);
                }
            }

            SeedStandingMosquitoes(n);
            SeedIndexCases(n, options);
            return n;
        }

        private static void EnsureResidualContainers(Household h, int minimum)
        {
            for (int i = 0; i < h.Containers.Count && h.ProductiveContainerCount < minimum; i++)
            {
                h.Containers[i].ClearedInModule2 = false;
            }
        }

        /// <summary>
        /// Section 5: "The mosquitoes are already flying and the virus is already in the
        /// neighbourhood." The squad does not arrive to an empty sky - it arrives into the
        /// population that the state of these yards has been producing for weeks.
        /// </summary>
        private static void SeedStandingMosquitoes(Neighbourhood n)
        {
            int days = n.Config.StandingPopulationDays;
            for (int i = 0; i < n.Households.Count; i++)
            {
                var h = n.Households[i];
                int productive = h.ProductiveContainerCount;
                if (productive == 0) continue;

                int total = (int)(productive * n.Config.EmergencePerContainerPerDay * days);
                if (total > n.Config.MaxMosquitoesPerHousehold) total = n.Config.MaxMosquitoesPerHousehold;

                for (int k = 0; k < total; k++)
                {
                    var c = n.PickProductiveContainer(h, 200 + k);
                    if (c == null) break;
                    // Staggered so the standing population ages out naturally rather than all at once.
                    n.SpawnMosquito(h, c, -Rng.Range(0, days - 1, n.Seed, 0, RngStream.MosquitoEmergence, h.Id, 300 + k));
                }
            }
        }

        private static void SeedIndexCases(Neighbourhood n, ScenarioOptions options)
        {
            // The visible patient the squad is sent to find: mid-lane, so the chain has somewhere
            // to run in both directions.
            // Section 1: "Dengue cases are rising in a neighbourhood." The squad arrives to more
            // than one patient, which is what makes the net budget a choice from the first turn.
            // Both show symptoms on day 0, so "cover the first two patients on the first day"
            // is something a Pilot can actually do rather than a hidden requirement.
            int middle = n.Households.Count / 2;
            int patientZero = FirstAdult(n, middle);
            if (patientZero >= 0)
                n.SeedIndexCase(patientZero, options.IndexCaseHeadStartDays, false, onsetDay: 0, forceSymptomatic: true);

            int second = FirstAdult(n, middle + 1 < n.Households.Count ? middle + 1 : 0);
            if (second >= 0 && second != patientZero)
                n.SeedIndexCase(second, options.IndexCaseHeadStartDays, false, onsetDay: 0, forceSymptomatic: true);

            // One of the two patients the squad meets first develops a warning sign as their
            // fever comes down - section 8: "Danger often arrives as the fever comes down."
            // A Pilot who sees them looking better and moves on has made the module's mistake.
            int forced = 0;
            if (options.GuaranteedWarningSignCases > 0 && second >= 0 && n.ForceWarningSign(second)) forced++;
            if (forced < options.GuaranteedWarningSignCases && patientZero >= 0 && n.ForceWarningSign(patientZero)) forced++;
            n.PendingGuaranteedWarningSigns = options.GuaranteedWarningSignCases - forced;

            if (!options.SeedAsymptomaticIndexCase) return;

            // The source nobody can find. Section 5: "You cannot net a person who does not know
            // they are infected." Placed at the far end so its chain is distinguishable on the
            // outbreak map from patient zero's.
            int farEnd = n.Households.Count - 1;
            int invisible = FirstAdult(n, farEnd);
            if (invisible >= 0) n.SeedIndexCase(invisible, options.IndexCaseHeadStartDays, forceAsymptomatic: true);
        }

        private static int FirstAdult(Neighbourhood n, int householdId)
        {
            var h = n.HouseholdById(householdId);
            if (h == null) return -1;
            for (int i = 0; i < h.ResidentIds.Count; i++)
            {
                var p = n.PersonById(h.ResidentIds[i]);
                if (p != null && p.Age == AgeBand.Adult) return p.Id;
            }
            return h.ResidentIds.Count > 0 ? h.ResidentIds[0] : -1;
        }
    }
}
