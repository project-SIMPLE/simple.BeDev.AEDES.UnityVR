using System.Collections.Generic;

namespace Aedes.Module3.Sim
{
    /// <summary>
    /// The parameters in force on each day of a session, in order.
    ///
    /// GAMA can send a new CSV while a session is running, so "the config" is not one object -
    /// it is a sequence of them with the days they took effect. Keeping it as a timeline rather
    /// than overwriting a field is what lets the counterfactual replay stay honest: the replay
    /// re-runs against the same timeline, so the difference between the two outbreaks is still
    /// the net the squad did or did not put up, and not a parameter change that only the second
    /// run happened to see.
    /// </summary>
    public sealed class ParameterTimeline
    {
        private struct Entry
        {
            public int Day;
            public Module3Config Config;
            public string Origin;
        }

        private readonly List<Entry> entries = new List<Entry>();

        public ParameterTimeline(Module3Config initial, string origin = "defaults")
        {
            entries.Add(new Entry { Day = int.MinValue, Config = initial ?? Module3Config.Default, Origin = origin });
        }

        /// <summary>Parameters that take effect on <paramref name="day"/> and stay until the next change.</summary>
        public void Stage(int day, Module3Config config, string origin)
        {
            if (config == null) return;

            // A change staged for a day already covered replaces that entry rather than stacking,
            // so re-sending the same file twice cannot drift the timeline.
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Day == day)
                {
                    entries[i] = new Entry { Day = day, Config = config, Origin = origin };
                    return;
                }
            }

            int at = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Day > day) { at = i; break; }
            }
            entries.Insert(at, new Entry { Day = day, Config = config, Origin = origin });
        }

        public Module3Config At(int day)
        {
            Module3Config current = entries[0].Config;
            for (int i = 1; i < entries.Count; i++)
            {
                if (entries[i].Day > day) break;
                current = entries[i].Config;
            }
            return current;
        }

        public string OriginAt(int day)
        {
            string origin = entries[0].Origin;
            for (int i = 1; i < entries.Count; i++)
            {
                if (entries[i].Day > day) break;
                origin = entries[i].Origin;
            }
            return origin;
        }

        public int ChangeCount => entries.Count - 1;

        /// <summary>A copy the counterfactual can replay against without touching the live one.</summary>
        public ParameterTimeline Clone()
        {
            var copy = new ParameterTimeline(entries[0].Config, entries[0].Origin);
            for (int i = 1; i < entries.Count; i++)
            {
                copy.entries.Add(entries[i]);
            }
            return copy;
        }

        /// <summary>For the Field Journal and the debrief: what changed, and when.</summary>
        public List<string> Describe()
        {
            var lines = new List<string>();
            for (int i = 1; i < entries.Count; i++)
            {
                lines.Add($"day {entries[i].Day}: parameters from {entries[i].Origin}");
            }
            return lines;
        }
    }
}
