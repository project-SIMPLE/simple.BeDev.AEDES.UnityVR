namespace Aedes.Module3.Sim
{
    /// <summary>
    /// Every clinical and entomological number the model uses, in one place.
    ///
    /// Section 6 of the design says the ranking of protective measures will be reviewed by
    /// Dr Marcombe and endorsed by CMPE, and section 5 leaves the asymptomatic share and the
    /// infectious window open. So none of these are constants in code: they are data, and the
    /// calibration tests re-check the module's teaching guarantees whenever they change.
    ///
    /// Timings are NOT compressed. Section 8 is explicit that the compression lives in the
    /// calendar - the game skips days at the handover - so the numbers a student learns stay true.
    /// </summary>
    public sealed class Module3Config
    {
        // =======================================================================================
        // CLINICAL / ENTOMOLOGICAL. These describe the disease and must not be moved to make the
        // game work. Section 8: "The compression is in the calendar, not in the biology." They
        // change only on Dr Marcombe's advice and with CMPE's endorsement.
        // =======================================================================================

        // ---- Human course of infection (days) -------------------------------------------------
        /// Intrinsic incubation: bite to symptoms. Design section 5: "four to ten days".
        public int IncubationMinDays = 4;
        public int IncubationMaxDays = 10;

        /// Fever duration once symptoms begin.
        public int FeverMinDays = 4;
        public int FeverMaxDays = 7;

        /// Viraemia starts shortly before symptoms and runs into the febrile period.
        /// Pending Marcombe: "confirm the period during which a patient is infectious to mosquitoes,
        /// since that determines when netting them actually matters."
        public int InfectiousDaysBeforeOnset = 1;
        public int InfectiousDaysAfterOnset = 5;

        /// Section 5: "commonly estimated at half to three quarters" never feel ill.
        public double AsymptomaticShare = 0.6;
        public int AsymptomaticInfectiousDays = 5;

        /// <summary>
        /// How infectious to mosquitoes someone with no symptoms is, relative to someone in the
        /// thick of a fever. Section 5: "Netting the visible patients still removes the largest
        /// and most infectious contributors, because virus levels peak during the fever."
        ///
        /// This one number is what makes covering the visible patients the strongest action in
        /// the module while still leaving the residue that sends the debrief back to Module 2.
        /// Set it to 1.0 and the lesson disappears, which is why a calibration test guards it.
        /// </summary>
        public double AsymptomaticRelativeInfectiousness = 0.35;

        /// Share of symptomatic cases that develop a warning sign (bleeding / persistent vomiting).
        public double WarningSignShare = 0.08;

        /// Section 8: "Danger often arrives as the fever comes down." Warning signs appear around
        /// defervescence, which is what punishes a Pilot who sees someone improving and moves on.
        public int WarningSignOnsetOffsetMin = -1;
        public int WarningSignOnsetOffsetMax = 1;

        /// <summary>
        /// How long an unreferred warning sign goes before the household takes itself to hospital.
        /// Section 10: a consequence shown to the player, never a deduction.
        ///
        /// MUST be longer than DaysPerHandover. At two days against a three-day handover jump, a
        /// warning sign could appear and resolve itself entirely between two turns - the squad
        /// never saw it and could not have acted, which quietly removes the module's second
        /// takeaway. ParameterCsv refuses a file that reintroduces that.
        /// </summary>
        public int UnreferredWarningGraceDays = 4;

        // ---- Extrinsic incubation (clinical) ---------------------------------------------------
        /// Extrinsic incubation: "eight to twelve days before a mosquito that has fed on a case
        /// can transmit" (design section 5).
        public int ExtrinsicIncubationMinDays = 8;
        public int ExtrinsicIncubationMaxDays = 12;

        // =======================================================================================
        // POPULATION RATES. Tuning knobs, not clinical claims. They set how fast an outbreak runs
        // through a forty-person neighbourhood over a fifty-day session, so that a squad can see
        // the difference their choices made inside one classroom period. The calibration tests
        // are what keep them honest: change one and the module's teaching guarantees are re-checked.
        // =======================================================================================

        public int MosquitoLifespanMinDays = 10;
        public int MosquitoLifespanMaxDays = 28;

        /// Adult mosquitoes emerging per productive container per day.
        public double EmergencePerContainerPerDay = 0.9;

        /// Cap so a neglected neighbourhood stays playable rather than exploding.
        public int MaxMosquitoesPerHousehold = 80;

        /// <summary>
        /// Days' worth of emergence already on the wing when the squad arrives. Section 5:
        /// "The mosquitoes are already flying and the virus is already in the neighbourhood."
        /// Module 3 does not start from an empty sky.
        /// </summary>
        public int StandingPopulationDays = 10;

        /// Probability a given mosquito takes a blood meal on a given day.
        // Tuned so a do-nothing neighbourhood sees a clearly growing outbreak that still leaves
        /// most people well - an epidemic that infects everyone regardless of what the squad does
        /// teaches nothing, because every strategy then looks the same.
        public double DailyBiteProbability = 0.34;

        /// Aedes aegypti disperses very little - section 5: "commonly no more than about a hundred
        /// metres in its lifetime, often only a few houses". This is why covering one patient
        /// matters to the houses either side of them.
        public double DailyDispersalProbability = 0.10;

        public double ProbabilityHumanToMosquito = 0.65;
        public double ProbabilityMosquitoToHuman = 0.45;

        // ---- Protective measures (section 6) --------------------------------------------------
        // Multipliers on a person's chance of being bitten. Lower is better. The ORDER of these
        // is the ranking the module teaches, so it carries weight and needs endorsing.

        /// A net over a RESTING patient. Section 6: this works "for a particular reason: Aedes
        /// bites during the day, and the patient is in bed during the day."
        public double NetOverRestingPerson = 0.10;

        /// The same net over someone who is up and about. It barely helps - they are not under it
        /// when the biting happens. This single distinction is what makes netting the sick
        /// dramatically stronger than netting the well, without any of it being scripted.
        public double NetOverActivePerson = 0.92;

        public double IntactScreen = 0.40;

        /// A torn screen is very nearly no screen at all - which is exactly why noticing and
        /// repairing one is worth the player's time (section 6).
        public double TornScreen = 0.88;

        public double ClosedDuringBitingHours = 0.75;
        public double ElectricFan = 0.70;
        public double AirConditioning = 0.50;
        public double Repellent = 0.60;
        public int RepellentDurationDays = 1;
        public double LongSleeves = 0.85;

        // ---- Session shape --------------------------------------------------------------------
        /// Section 5: "fewer than there are households. The player must choose who gets one, and
        /// the choice is the whole point."
        public int NetsPerSession = 4;

        /// Section 8: to be fixed in playtesting between two and four minutes.
        public float TurnSeconds = 180f;
        public int TurnsPerRound = 3;
        public int Rounds = 6;
        /// Days the calendar jumps at each handover ("Three days later.").
        public int DaysPerHandover = 3;

        public static Module3Config Default => new Module3Config();

        public Module3Config Clone()
        {
            return (Module3Config)MemberwiseClone();
        }
    }
}
