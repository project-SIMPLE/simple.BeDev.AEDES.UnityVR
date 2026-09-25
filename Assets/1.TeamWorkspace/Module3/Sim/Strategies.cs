using System.Collections.Generic;

namespace Aedes.Module3.Sim
{
    /// <summary>
    /// A scripted stand-in for a squad. Called once per turn, before the calendar jumps.
    /// Used by the calibration harness to prove the module's teaching guarantees hold, and by
    /// the counterfactual replay to re-run an outbreak with one decision changed.
    /// </summary>
    public interface IStrategy
    {
        void OnTurn(Neighbourhood n, int turnIndex);
    }

    /// <summary>The squad that does nothing. The outbreak the neighbourhood gets by default.</summary>
    public sealed class DoNothing : IStrategy
    {
        public void OnTurn(Neighbourhood n, int turnIndex) { }
    }

    /// <summary>
    /// The instinct everybody has: protect the people who are still healthy. Section 5 says the
    /// module "should let them try it, and then show them that the neighbourhood kept getting new
    /// cases anyway - because the source was never covered."
    /// </summary>
    public sealed class NetTheWell : IStrategy
    {
        public void OnTurn(Neighbourhood n, int turnIndex)
        {
            if (n.NetsRemaining <= 0) return;
            for (int i = 0; i < n.People.Count && n.NetsRemaining > 0; i++)
            {
                var p = n.People[i];
                if (p.HasNet) continue;
                if (p.IsFebrile(n.Day)) continue; // pointedly skipping the sick
                n.Apply(PlayerAction.OnPerson(ActionKind.PutUpNet, p.Id));
            }
        }
    }

    /// <summary>
    /// Section 5: "A squad that covers the first two patients on the first day should see the
    /// outbreak stop, even with containers still standing in the yards."
    /// </summary>
    public sealed class NetFirstPatients : IStrategy
    {
        private readonly int count;
        public NetFirstPatients(int count = 2) { this.count = count; }

        public void OnTurn(Neighbourhood n, int turnIndex)
        {
            if (turnIndex > 0) return; // day one only

            // Only what a Pilot can actually see on a visit. A strategy that acted on the model's
            // hidden state would flatter the module and prove nothing about what a squad can do.
            var ill = n.VisiblyIll();
            int done = 0;
            for (int i = 0; i < ill.Count && done < count; i++)
            {
                if (ill[i].HasNet) continue;
                if (n.Apply(PlayerAction.OnPerson(ActionKind.PutUpNet, ill[i].Id)).Outcome == ActionOutcome.Done) done++;
            }
        }
    }

    /// <summary>
    /// The best a squad can actually do: cover every patient they can see, every turn.
    /// The outbreak should nearly stop - but not completely, because of the cases nobody can see.
    /// That residue is the bridge back to Module 2 (section 5).
    /// </summary>
    public sealed class NetEveryVisiblePatient : IStrategy
    {
        public void OnTurn(Neighbourhood n, int turnIndex)
        {
            // Take the nets back off people who are past it. A Pilot can work this out from the
            // household's own words - section 7: "she has been hot for three days."
            for (int i = 0; i < n.People.Count; i++)
            {
                var p = n.People[i];
                if (p.HasNet && !p.IsInfectiousToMosquitoes(n.Day))
                {
                    n.Apply(PlayerAction.OnPerson(ActionKind.ReclaimNet, p.Id));
                }
            }

            // Cover the newest patients first: they have the most infectious days ahead of them,
            // and a net put up on someone whose fever is nearly over buys almost nothing.
            var ill = n.VisiblyIll();
            ill.Sort((a, b) => b.SymptomOnsetDay.CompareTo(a.SymptomOnsetDay));
            for (int i = 0; i < ill.Count && n.NetsRemaining > 0; i++)
            {
                if (ill[i].HasNet) continue;
                n.Apply(PlayerAction.OnPerson(ActionKind.PutUpNet, ill[i].Id));
            }

            // And refer anyone with a warning sign - section 4, not a judgement call.
            for (int i = 0; i < ill.Count; i++)
            {
                if (ill[i].HasVisibleWarningSign(n.Day) && !ill[i].Referred)
                {
                    n.Apply(PlayerAction.OnPerson(ActionKind.ReferToHealthCentre, ill[i].Id));
                }
            }
        }
    }

    /// <summary>Replays a recorded list of actions, which is how the counterfactual is built.</summary>
    public sealed class ScriptedActions : IStrategy
    {
        private readonly Dictionary<int, List<PlayerAction>> byTurn = new Dictionary<int, List<PlayerAction>>();

        public ScriptedActions Add(int turnIndex, PlayerAction action)
        {
            if (!byTurn.TryGetValue(turnIndex, out var list))
            {
                list = new List<PlayerAction>();
                byTurn[turnIndex] = list;
            }
            list.Add(action);
            return this;
        }

        /// <summary>Copies another recording in on top of this one, keeping turn order.</summary>
        public ScriptedActions Absorb(ScriptedActions other)
        {
            if (other == null) return this;
            foreach (var pair in other.byTurn)
            {
                for (int i = 0; i < pair.Value.Count; i++) Add(pair.Key, pair.Value[i]);
            }
            return this;
        }

        public void OnTurn(Neighbourhood n, int turnIndex)
        {
            if (!byTurn.TryGetValue(turnIndex, out var list)) return;
            for (int i = 0; i < list.Count; i++) n.Apply(list[i]);
        }
    }
}
