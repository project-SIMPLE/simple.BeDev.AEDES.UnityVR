namespace Aedes.Module3.Sim
{
    public enum HealthState
    {
        Susceptible,
        Exposed,      // infected, incubating, nothing to see
        Infectious,   // can pass the virus to a mosquito that bites them
        Recovered,
        Hospitalised,
    }

    public enum AgeBand { Child, Adult, Elder }

    /// <summary>The two warning signs the module teaches. Section 4 - deliberately only two.</summary>
    public enum WarningSign { None, Bleeding, PersistentVomiting }

    public sealed class Person
    {
        public int Id;
        public int HouseholdId;
        public AgeBand Age;
        public string Name;

        public HealthState State = HealthState.Susceptible;

        /// <summary>
        /// True once this person has been infected and their course of illness scheduled.
        ///
        /// This exists because the day fields below can legitimately be NEGATIVE: an index case
        /// was infected before the squad arrived, so their exposure, onset and infectious window
        /// may all sit before day 0. Testing "is this day field >= 0" to mean "has this been set"
        /// silently makes those people non-infectious, which is the outbreak's whole starting
        /// point. Ask this flag instead.
        /// </summary>
        public bool HasBeenInfected;

        // Set when the person is infected; all derived from per-person RNG draws so they are
        // stable across a counterfactual re-run.
        public int ExposedDay = -1;
        public int SymptomOnsetDay = -1;
        public int FeverEndDay = -1;
        public int InfectiousStartDay = -1;
        public int InfectiousEndDay = -1;
        public bool IsAsymptomatic;

        public WarningSign Warning = WarningSign.None;
        public int WarningOnsetDay = -1;

        // What the volunteer has done for them.
        public bool HasNet;
        public bool WearsLongSleeves;
        public int RepellentUntilDay = -1;
        /// <summary>Set only when the person actually went to the health centre.</summary>
        public bool Referred;
        public int ReferredDay = -1;
        /// <summary>Referred while they did not need it: gently corrected, and they stayed home.</summary>
        public bool ReferredWithoutNeed;
        public bool WentToHospitalUnaided;
        public bool HasRested;
        public bool HasFluids;

        /// <summary>
        /// A mosquito took the virus from this person at some point. The chain left the house.
        /// </summary>
        public bool EverFedOnWhileInfectious;

        /// <summary>
        /// There was a net over this person on at least one day they could have infected a
        /// mosquito. Recorded as it happens, because scoring cannot read it off the end state:
        /// a squad that plays well takes the net back once a patient is past it (see ReclaimNet),
        /// so by the end of the session the people whose chains they broke have no net at all.
        /// </summary>
        public bool WasCoveredWhileInfectious;

        /// <summary>
        /// Visibly unwell on a given day - what a Pilot walking in would see.
        ///
        /// Deliberately a pure function of the scheduled days and NOT of the current State: the
        /// trace-back replay asks "was the source visibly ill on the night the mosquito fed?",
        /// which is a question about a day in the past, long after that person has recovered.
        /// </summary>
        public bool IsFebrile(int day)
        {
            return HasBeenInfected
                && !IsAsymptomatic
                && day >= SymptomOnsetDay
                && day <= FeverEndDay;
        }

        /// <summary>
        /// Lying down during the hours Aedes bites. A febrile patient is; a well person is not.
        /// This is the hinge the whole module turns on - see Module3Config.NetOverRestingPerson.
        /// </summary>
        public bool IsRestingInBitingHours(int day)
        {
            return IsFebrile(day) || State == HealthState.Hospitalised;
        }

        /// <summary>Carrying enough virus for a biting mosquito to pick it up, on a given day.</summary>
        public bool IsInfectiousToMosquitoes(int day)
        {
            return HasBeenInfected
                && day >= InfectiousStartDay
                && day <= InfectiousEndDay;
        }

        public bool HasVisibleWarningSign(int day)
        {
            return Warning != WarningSign.None
                && HasBeenInfected
                && day >= WarningOnsetDay
                && day <= WarningOnsetDay + 4;
        }

        /// <summary>
        /// True once the person has been ill and is now improving. Section 8: "feeling better is
        /// not the all-clear" - the dangerous phase often begins as the fever drops.
        /// </summary>
        public bool IsDefervescing(int day)
        {
            return HasBeenInfected && !IsAsymptomatic && day > FeverEndDay && day <= FeverEndDay + 3;
        }
    }
}
