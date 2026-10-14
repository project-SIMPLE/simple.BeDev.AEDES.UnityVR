namespace Aedes.Module3.Sim
{
    /// <summary>
    /// Which decision a random draw belongs to. Every draw in the model names its stream so that
    /// two draws for the same entity on the same day never collide.
    /// </summary>
    public enum RngStream
    {
        Layout = 1,
        Residents,
        Containers,
        IndexCase,
        Incubation,
        Asymptomatic,
        FeverDuration,
        WarningSign,
        WarningSignOnset,
        MosquitoEmergence,
        MosquitoLifespan,
        MosquitoDispersal,
        Bite,
        BiteTarget,
        HumanToMosquito,
        MosquitoToHuman,
    }

    /// <summary>
    /// A stateless, hash-based random source.
    ///
    /// This is deliberately NOT a sequential PRNG. Every draw is a pure function of
    /// (seed, day, stream, entity, salt), so a draw for one mosquito on one day is completely
    /// independent of how many draws anything else made. That is what makes the counterfactual
    /// replay in section 5 of the design honest: re-running the outbreak with one extra net
    /// changes the outcome because of the net, not because the whole random stream shifted.
    /// </summary>
    public static class Rng
    {
        private static ulong Mix(ulong x)
        {
            // splitmix64 finalizer
            x += 0x9E3779B97F4A7C15UL;
            ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private static ulong Hash(int seed, int day, RngStream stream, int entity, int salt)
        {
            ulong h = Mix((ulong)(uint)seed);
            h = Mix(h ^ (ulong)(uint)day * 0x100000001B3UL);
            h = Mix(h ^ (ulong)(uint)(int)stream * 0x9E3779B1UL);
            h = Mix(h ^ (ulong)(uint)entity * 0xC2B2AE35UL);
            h = Mix(h ^ (ulong)(uint)salt * 0x27D4EB2FUL);
            return h;
        }

        /// <summary>Uniform value in [0, 1).</summary>
        public static double Unit(int seed, int day, RngStream stream, int entity, int salt = 0)
        {
            // 53 bits of mantissa, the standard way to get a double without bias.
            return (Hash(seed, day, stream, entity, salt) >> 11) * (1.0 / 9007199254740992.0);
        }

        public static bool Chance(double probability, int seed, int day, RngStream stream, int entity, int salt = 0)
        {
            return Unit(seed, day, stream, entity, salt) < probability;
        }

        /// <summary>Uniform integer in [minInclusive, maxInclusive].</summary>
        public static int Range(int minInclusive, int maxInclusive, int seed, int day, RngStream stream, int entity, int salt = 0)
        {
            if (maxInclusive <= minInclusive) return minInclusive;
            int span = maxInclusive - minInclusive + 1;
            return minInclusive + (int)(Unit(seed, day, stream, entity, salt) * span);
        }
    }
}
