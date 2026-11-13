using System.Collections.Generic;

namespace Aedes.Module3.Sim
{
    /// <summary>
    /// One link in the chain the player is breaking. Every field here exists because the
    /// trace-back replay or the end-of-outbreak map needs it (design section 5).
    /// </summary>
    public sealed class TransmissionEvent
    {
        public int Day;

        public int TargetPersonId;
        public int TargetHouseholdId;

        /// The person the mosquito had taken the virus from. -1 for a seeded index case.
        public int SourcePersonId = -1;
        public int SourceHouseholdId = -1;

        public int MosquitoId = -1;
        public int OriginContainerId = -1;
        public ContainerKind OriginContainerKind;

        /// <summary>
        /// The source was visibly ill on the day the mosquito fed on them - so the squad could
        /// have found and covered them. False means an asymptomatic source: the chain nobody
        /// could have seen, and the bridge back to Module 2 in the debrief.
        /// </summary>
        public bool SourceWasVisiblyIll;

        /// <summary>The source had a net up when the mosquito fed on them.</summary>
        public bool SourceWasProtected;

        /// <summary>
        /// This link would not have happened had the source been covered - the sentence the
        /// outbreak map is built to make visible.
        /// </summary>
        public bool WasPreventable => SourceWasVisiblyIll && !SourceWasProtected;

        public bool IsIndexCase => SourcePersonId < 0;
    }

    public sealed class TransmissionLog
    {
        public readonly List<TransmissionEvent> Events = new List<TransmissionEvent>();

        public void Add(TransmissionEvent e) => Events.Add(e);

        public int CaseCount => Events.Count;

        public int PreventableCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Events.Count; i++) if (Events[i].WasPreventable) n++;
                return n;
            }
        }

        public int InvisibleSourceCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Events.Count; i++)
                {
                    var e = Events[i];
                    if (!e.IsIndexCase && !e.SourceWasVisiblyIll) n++;
                }
                return n;
            }
        }

        /// <summary>Walks a case back to the index case it descends from.</summary>
        public List<TransmissionEvent> ChainTo(int personId)
        {
            var chain = new List<TransmissionEvent>();
            int current = personId;
            for (int guard = 0; guard < 64; guard++)
            {
                TransmissionEvent found = null;
                for (int i = 0; i < Events.Count; i++)
                {
                    if (Events[i].TargetPersonId == current) { found = Events[i]; break; }
                }
                if (found == null) break;
                chain.Insert(0, found);
                if (found.IsIndexCase) break;
                current = found.SourcePersonId;
            }
            return chain;
        }
    }
}
