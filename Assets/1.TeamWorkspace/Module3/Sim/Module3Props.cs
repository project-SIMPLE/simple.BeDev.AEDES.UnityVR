namespace Aedes.Module3.Sim
{
    /// <summary>
    /// Where the Module 3 props live, in one place.
    ///
    /// The placeholder assets are greybox stand-ins whose dimensions, pivots, socket names and
    /// PATHS are the deliverable - art replaces the mesh inside each prefab later and nothing else
    /// changes (see Documentation/Module3-Placeholder-Assets.md). Binding to these constants rather
    /// than to literals scattered through the view code is what makes that swap a non-event.
    ///
    /// Each of these exists because a Phase 1 player action needs something to show for it. There is
    /// deliberately no container-clearing prop: section 5 of the design is explicit that clearing
    /// containers is "not a button, not a task, not a score".
    /// </summary>
    public static class Module3Props
    {
        private const string Root = "Assets/1.TeamWorkspace/Team Assets/Prefabs/";

        // SetFan(householdId)
        public const string ElectricFan = Root + "Furniture/PF_ElectricFan.prefab";

        // RepairScreen(householdId): torn becomes intact, in the same place
        public const string WindowScreenTorn = Root + "Buildings/PF_WindowScreenTorn.prefab";
        public const string WindowScreenIntact = Root + "Buildings/PF_WindowScreen.prefab";

        // PutUpNet(personId): rolled up becomes deployed, over the same bed
        public const string MosquitoNetRolledUp = Root + "Props/PF_MosquitoNet_RolledUp.prefab";
        public const string MosquitoNetDeployed = Root + "Props/PF_MosquitoNet_Deployed.prefab";

        // GiveRepellent / BringWater / HelpRest
        public const string RepellentBottle = Root + "Props/PF_RepellentBottle.prefab";
        public const string DrinkingVessel = Root + "Props/PF_DrinkingVessel.prefab";
        public const string Cloth = Root + "Props/PF_Cloth.prefab";

        // Night visits
        public const string Torch = Root + "Props/PF_Torch.prefab";

        // ReferToHealthCentre - provisional, pending design question 13.4 (whether the player
        // travels there at all, or referral happens off-screen).
        public const string HealthCentre = Root + "Buildings/PF_HealthCentre.prefab";

        // Reused from the existing world, not made for Module 3.
        public const string Bed = Root + "PF_Bed.prefab";
        public const string LaoHouse = Root + "Buildings/PF_Lao_House_One_FloorV2.prefab";

        /// <summary>Socket names, as the delivered assets carry them (decision D12).</summary>
        public static class Socket
        {
            /// <summary>Attaches to a surface or into an opening.</summary>
            public const string Mount = "Socket_Mount";
            /// <summary>Held in a hand.</summary>
            public const string Grip = "Socket_Grip";
            /// <summary>Hangs from above - the net's ceiling tie.</summary>
            public const string Hook = "Socket_Hook";
            /// <summary>Where a beam starts, on the torch.</summary>
            public const string Light = "Socket_Light";
            /// <summary>The way into the health centre.</summary>
            public const string Entrance = "Socket_Entrance";
        }

        /// <summary>The fan's moving part, spun around its local Z by the view.</summary>
        public const string FanBlade = "Fan_Blade";

        public static readonly string[] AllPrefabs =
        {
            ElectricFan, WindowScreenTorn, WindowScreenIntact,
            MosquitoNetRolledUp, MosquitoNetDeployed,
            RepellentBottle, DrinkingVessel, Cloth, Torch, HealthCentre,
        };
    }
}
