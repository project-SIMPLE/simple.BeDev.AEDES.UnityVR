using System.Collections.Generic;

namespace Aedes.Module3.Sim
{
    public enum ScreenState { Missing, Torn, Intact }

    /// <summary>
    /// The container kinds are Module 2's set (design section 11), reused deliberately.
    /// They are NOT a task in Module 3 - section 5: "not a button, not a task, not a score".
    /// They exist to set the mosquito population and to be named in the trace-back replay.
    /// </summary>
    public enum ContainerKind { WaterJar, Tyre, Bucket, PlantPot, DiscardedPlastic, RoofGutter }

    public sealed class Container
    {
        public int Id;
        public int HouseholdId;
        public ContainerKind Kind;

        /// <summary>
        /// Set by what the squad left behind in Module 2 (section 5, and open question 3b).
        /// A cleared container produces nothing; it is still there to be seen.
        /// </summary>
        public bool ClearedInModule2;

        public bool IsProductive => !ClearedInModule2;
    }

    public sealed class Household
    {
        public int Id;
        public string Name;

        /// Plot position along the lanes. Only used for adjacency.
        public int LaneX;
        public int LaneY;

        public readonly List<int> ResidentIds = new List<int>();
        public readonly List<Container> Containers = new List<Container>();

        /// Houses close enough for a mosquito to reach. Section 5: the virus moves between
        /// neighbours, not across a district.
        public readonly List<int> NeighbourIds = new List<int>();

        public ScreenState Screens = ScreenState.Torn;
        public bool HasFan;
        public bool HasAirConditioning;
        public bool ClosesDuringBitingHours;

        /// Turn bookkeeping so the Coach can say which houses nobody has been to.
        public int LastVisitedDay = -1;
        public int VisitCount;

        public int ProductiveContainerCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Containers.Count; i++)
                {
                    if (Containers[i].IsProductive) n++;
                }
                return n;
            }
        }
    }
}
