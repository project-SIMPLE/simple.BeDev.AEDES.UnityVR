namespace Aedes.Module3.Sim
{
    public enum MosquitoState { Susceptible, Exposed, Infectious }

    /// <summary>
    /// Mosquitoes are individuals rather than a per-household count, and that is a design
    /// requirement rather than a preference: section 5 asks the trace-back replay to show
    /// "the house it came from, the container the mosquito came from, and the night the sick
    /// person slept unprotected". Only an individual carries that provenance.
    ///
    /// A neighbourhood of ten plots over three weeks is a few hundred of these. It costs nothing.
    /// </summary>
    public sealed class Mosquito
    {
        public int Id;
        public int HouseholdId;
        public int OriginContainerId;
        public int OriginHouseholdId;
        public int EmergedDay;
        public int LifespanDays;

        public MosquitoState State = MosquitoState.Susceptible;
        public int InfectiousFromDay = -1;

        /// Who this mosquito took the virus from - the other half of the chain.
        public int AcquiredFromPersonId = -1;
        public int AcquiredFromHouseholdId = -1;
        public int AcquiredOnDay = -1;

        public bool Alive = true;

        public bool CanTransmit(int day)
        {
            return State == MosquitoState.Infectious && day >= InfectiousFromDay;
        }
    }
}
