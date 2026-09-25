namespace Aedes.Module3.Sim
{
    /// <summary>
    /// Everything the volunteer can do, and nothing else.
    ///
    /// There is no ClearContainer (section 5: "not a button, not a task, not a score") and no
    /// Diagnose or Treat (sections 4 and 12: "The player never names the illness, never prescribes
    /// anything and never gives medicine"). The absence of those verbs is itself a teaching device,
    /// so this enum is a closed list on purpose - adding to it needs a reason from the design.
    /// </summary>
    public enum ActionKind
    {
        Visit,
        PutUpNet,
        ReclaimNet,
        RepairScreen,
        SetFan,
        AdviseClosingHours,
        GiveRepellent,
        BringWater,
        HelpRest,
        ReferToHealthCentre,
    }

    public struct PlayerAction
    {
        public ActionKind Kind;
        public int PersonId;
        public int HouseholdId;

        public static PlayerAction OnPerson(ActionKind kind, int personId) =>
            new PlayerAction { Kind = kind, PersonId = personId, HouseholdId = -1 };

        public static PlayerAction OnHousehold(ActionKind kind, int householdId) =>
            new PlayerAction { Kind = kind, PersonId = -1, HouseholdId = householdId };
    }

    public enum ActionOutcome
    {
        /// Taking a net back from someone who no longer needs it, to use it on someone who does.
        NetReclaimed,
        Done,
        /// The action was correct and is the strongest positive in the module (section 10).
        ReferralCorrect,
        /// Section 10: "Gently corrected, not punished. This is a teaching moment, not a penalty."
        ReferralNotNeeded,
        /// No nets left. Section 5: the shortage is the point.
        NoNetsRemaining,
        AlreadyDone,
        Invalid,
    }

    public struct ActionResult
    {
        public ActionOutcome Outcome;
        public PlayerAction Action;
        /// Localization key for what the game says back to the player.
        public string MessageKey;
    }
}
