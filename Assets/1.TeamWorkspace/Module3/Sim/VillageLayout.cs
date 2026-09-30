using System;

namespace Aedes.Module3.Sim
{
    /// <summary>
    /// Where one plot stands: world x and z in metres, and the yaw in degrees (about +y, as Unity
    /// turns things) of its +z axis - the side of the house that faces the lane.
    /// </summary>
    public struct PlotPlacement
    {
        public float X;
        public float Z;
        public float Yaw;
    }

    /// <summary>
    /// The shape of the Module 3 village: a lane that winds, and two rows of houses that follow it.
    ///
    /// The first layout was a grid - two straight rows of identical plots, evenly spaced, all facing
    /// straight across a dead-straight lane - which reads as a housing estate, not a village. This
    /// keeps what the model needs and loosens everything else:
    ///
    ///  - The model's adjacency is untouched. It is a ladder: a house neighbours the ones either side
    ///    of it in its row and the one across the lane (ScenarioBuilder). So the rows stay rows, and
    ///    the house across is still the house across - but the lane bends, each house turns a few
    ///    degrees off square, stands a little further back or forward than its neighbour, and the gaps
    ///    between them differ. The two rows are not in step either.
    ///  - Nothing is looser than the grid was where the game needs it tight: houses never come closer
    ///    to each other (the side door is reached through the gap) or to the lane than they did, so
    ///    the doorways, the yards and the track keep the room they were built around.
    ///
    /// Pure numbers, no Unity types, so a test can check the geometry for hundreds of seeds. The draws
    /// come from the same hash as the rest of the model (<see cref="RngStream.Layout"/>), so a seed
    /// always gives the same village.
    /// </summary>
    public static class VillageLayout
    {
        // ---------- the lane ----------

        /// <summary>Where the lane's centre line runs before it starts to bend, and where it ends up on a straight stretch.</summary>
        public const float LaneBaseZ = 9f;

        /// <summary>
        /// The lane is straight west of here - the head of the lane, where the Pilot starts (M3XRPlayer)
        /// - and eases into its bends over the next <see cref="BendRunIn"/> metres.
        /// </summary>
        public const float HeadX = -8f;
        public const float BendRunIn = 28f;

        // One long bend and a shorter ripple on top of it, so it does not read as a sine wave.
        private const float BendAmplitude = 3.4f, BendWavelength = 104f, BendPhase = 30f;
        private const float RippleAmplitude = 0.45f, RippleWavelength = 41f, RipplePhase = 3f;

        /// <summary>World z of the lane's centre line at world x.</summary>
        public static float LaneZ(float x)
        {
            float run = Clamp01((x - HeadX) / BendRunIn);
            float ease = run * run * (3f - 2f * run);
            float bend = BendAmplitude * (float)Math.Sin(2.0 * Math.PI * (x - BendPhase) / BendWavelength)
                       + RippleAmplitude * (float)Math.Sin(2.0 * Math.PI * (x - RipplePhase) / RippleWavelength);
            return LaneBaseZ + ease * bend;
        }

        /// <summary>The lane's gradient, dz/dx, at world x.</summary>
        public static float LaneSlope(float x) => (LaneZ(x + 0.05f) - LaneZ(x - 0.05f)) / 0.1f;

        /// <summary>The distance from the point (x, z) to the lane's centre line, in metres.</summary>
        public static float DistanceToLane(float x, float z)
        {
            // The lane never doubles back, so its nearest point is within a few metres of the same x.
            float best = float.MaxValue;
            for (float sx = x - 10f; sx <= x + 10f; sx += 0.25f)
            {
                float dx = sx - x, dz = LaneZ(sx) - z;
                float d = dx * dx + dz * dz;
                if (d < best) best = d;
            }
            return (float)Math.Sqrt(best);
        }

        // ---------- the house ----------

        /// <summary>
        /// The Lao house as modelled, at scale 1, in plot space (z toward the lane): its side walls, its
        /// back wall and its front wall. The same numbers M3Surroundings and M3LightShafts measure.
        /// </summary>
        public const float ModelMinX = -4.16f, ModelMaxX = 4.39f, ModelBackZ = -3.0f, ModelFrontZ = 3.4f;

        // ---------- how loose it is ----------

        /// <summary>Most a house is stood back from where the grid put it, in metres. Drawn skewed toward none: most stand at the lane.</summary>
        public const float MaxSetback = 5.5f;
        /// <summary>Most a house is turned off square to the lane, either way, in degrees.</summary>
        public const float MaxYawJitter = 7f;
        /// <summary>
        /// The room a row has to spread out in, on top of its tightest spacing, in metres. It is shared out
        /// between the gaps at random rather than given to each gap, so a longer or shorter run of luck
        /// cannot push the last house off the ground: the scene's ground plane ends 78 m along the lane.
        /// </summary>
        public const float RowSlack = 4f;
        /// <summary>The narrowest gap between two houses' walls, in metres: the old grid's 2.5, so the side door's path is never tighter.</summary>
        public const float MinGap = 2.4f;

        // The plot draws, in RngStream.Layout. Salts 1-3 are the household's screens, fan and air conditioning.
        private const int SaltGap = 20, SaltSetback = 21, SaltYaw = 22, SaltStart = 23;

        /// <summary>
        /// Places every house. The result is indexed by household id, which is row by row
        /// (<c>laneY * householdsPerLane + laneX</c>, as ScenarioBuilder numbers them). Even rows stand on
        /// the low-z side of the lane, odd rows on the other, facing each other.
        /// </summary>
        /// <param name="plotWidth">The tightest centre-to-centre spacing along a row, in metres.</param>
        /// <param name="laneSpacing">The tightest distance between two facing rows, in metres.</param>
        /// <param name="houseScale">How much the model is scaled up.</param>
        public static PlotPlacement[] Place(int seed, int lanes, int householdsPerLane,
                                            float plotWidth, float laneSpacing, float houseScale)
        {
            var placements = new PlotPlacement[Math.Max(0, lanes * householdsPerLane)];

            // No nearer the lane than the grid stood: its front wall was this far from the centre line.
            float minClearance = laneSpacing * 0.5f - ModelFrontZ * houseScale;

            for (int y = 0; y < lanes; y++)
            {
                int side = y % 2 == 0 ? -1 : 1;
                // The rows do not start level: the far row's first house stands a little further in.
                float station = (side < 0 ? 0f : 1f) + 2.5f * Unit(seed, y * householdsPerLane, SaltStart);
                float[] previous = null;

                // How the row's slack is shared out among its gaps.
                var shares = new float[householdsPerLane];
                float total = 0f;
                for (int x = 1; x < householdsPerLane; x++)
                {
                    shares[x] = 0.2f + Unit(seed, y * householdsPerLane + x, SaltGap);
                    total += shares[x];
                }

                for (int x = 0; x < householdsPerLane; x++)
                {
                    int id = y * householdsPerLane + x;
                    if (x > 0) station += plotWidth + RowSlack * shares[x] / total;

                    float setback = MaxSetback * Unit(seed, id, SaltSetback) * Unit(seed, id, SaltSetback);
                    float jitter = (Unit(seed, id, SaltYaw) - 0.5f) * 2f * MaxYawJitter;
                    float distance = laneSpacing * 0.5f + setback;

                    var p = Stand(station, side, distance, jitter);
                    // Two things must hold, and moving a house to satisfy one can undo the other, so they
                    // are settled together. Turning a house swings a front corner toward the lane, and a
                    // bend in the lane can curve toward it: stand it back until it is as clear of the lane
                    // as the grid was. And it is no nearer its neighbour along the row than the grid stood.
                    for (int guard = 0; guard < 600; guard++)
                    {
                        bool nearLane = LaneClearance(p, houseScale) < minClearance;
                        bool nearNeighbour = previous != null && FootprintGap(previous, Corners(p, houseScale)) < MinGap;
                        if (!nearLane && !nearNeighbour) break;
                        if (nearLane) distance += 0.1f;
                        if (nearNeighbour) station += 0.1f;
                        p = Stand(station, side, distance, jitter);
                    }

                    placements[id] = p;
                    previous = Corners(p, houseScale);
                }
            }
            return placements;
        }

        /// <summary>The house on the lane's <paramref name="side"/> (-1 low z, +1 high z), at lane position <paramref name="station"/>, <paramref name="distance"/> from the centre line, facing it.</summary>
        private static PlotPlacement Stand(float station, int side, float distance, float yawJitter)
        {
            float slope = LaneSlope(station);
            float norm = (float)Math.Sqrt(1f + slope * slope);
            float nx = -slope / norm, nz = 1f / norm;   // the lane's normal, pointing to high z
            var p = new PlotPlacement
            {
                X = station + side * nx * distance,
                Z = LaneZ(station) + side * nz * distance,
            };
            // +z points at the lane: along the normal for the low row, against it for the high row.
            p.Yaw = (float)(Math.Atan2(-side * nx, -side * nz) * 180.0 / Math.PI) + yawJitter;
            return p;
        }

        // ---------- checks, shared with the tests ----------

        /// <summary>
        /// The corners of a house's footprint in world x, z: back-left, back-right, front-right, front-left
        /// (x0, z0, x1, z1, ...), where left and right are the plot's own -x and +x.
        /// </summary>
        public static float[] Corners(PlotPlacement p, float houseScale)
        {
            double yaw = p.Yaw * Math.PI / 180.0;
            float sin = (float)Math.Sin(yaw), cos = (float)Math.Cos(yaw);
            float x0 = ModelMinX * houseScale, x1 = ModelMaxX * houseScale;
            float z0 = ModelBackZ * houseScale, z1 = ModelFrontZ * houseScale;
            var c = new float[8];
            float[] lx = { x0, x1, x1, x0 }, lz = { z0, z0, z1, z1 };
            for (int i = 0; i < 4; i++)
            {
                c[i * 2] = p.X + lx[i] * cos + lz[i] * sin;
                c[i * 2 + 1] = p.Z - lx[i] * sin + lz[i] * cos;
            }
            return c;
        }

        /// <summary>How far the front wall of the house at <paramref name="p"/> is from the lane's centre line, at its nearest point.</summary>
        public static float LaneClearance(PlotPlacement p, float houseScale)
        {
            var c = Corners(p, houseScale);
            float best = float.MaxValue;
            for (int k = 0; k <= 4; k++)
            {
                // Along the front wall, corner to corner.
                float t = k / 4f;
                float x = c[4] + (c[6] - c[4]) * t, z = c[5] + (c[7] - c[5]) * t;
                best = Math.Min(best, DistanceToLane(x, z));
            }
            return best;
        }

        /// <summary>The distance between two houses' footprints, in metres: negative if they overlap.</summary>
        public static float FootprintGap(PlotPlacement a, PlotPlacement b, float houseScale)
            => FootprintGap(Corners(a, houseScale), Corners(b, houseScale));

        private static float FootprintGap(float[] a, float[] b)
        {
            if (NoSeparatingEdge(a, b) && NoSeparatingEdge(b, a)) return -1f;
            float best = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    int k = (j + 1) % 4;
                    best = Math.Min(best, PointToSegment(a[i * 2], a[i * 2 + 1], b[j * 2], b[j * 2 + 1], b[k * 2], b[k * 2 + 1]));
                    best = Math.Min(best, PointToSegment(b[i * 2], b[i * 2 + 1], a[j * 2], a[j * 2 + 1], a[k * 2], a[k * 2 + 1]));
                }
            }
            return best;
        }

        /// <summary>True if no edge of <paramref name="a"/> separates the two rectangles - half of a separating-axis test, so the rectangles overlap only if this holds both ways round.</summary>
        private static bool NoSeparatingEdge(float[] a, float[] b)
        {
            for (int i = 0; i < 4; i++)
            {
                int k = (i + 1) % 4;
                float ex = a[k * 2] - a[i * 2], ez = a[k * 2 + 1] - a[i * 2 + 1];
                float nx = ez, nz = -ex;   // an edge's normal
                float aMin = float.MaxValue, aMax = float.MinValue, bMin = float.MaxValue, bMax = float.MinValue;
                for (int j = 0; j < 4; j++)
                {
                    float pa = a[j * 2] * nx + a[j * 2 + 1] * nz, pb = b[j * 2] * nx + b[j * 2 + 1] * nz;
                    aMin = Math.Min(aMin, pa); aMax = Math.Max(aMax, pa);
                    bMin = Math.Min(bMin, pb); bMax = Math.Max(bMax, pb);
                }
                if (aMax < bMin || bMax < aMin) return false;
            }
            return true;
        }

        private static float PointToSegment(float px, float pz, float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az;
            float len2 = dx * dx + dz * dz;
            float t = len2 <= 0f ? 0f : Clamp01(((px - ax) * dx + (pz - az) * dz) / len2);
            float qx = ax + dx * t - px, qz = az + dz * t - pz;
            return (float)Math.Sqrt(qx * qx + qz * qz);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        private static float Unit(int seed, int id, int salt) => (float)Rng.Unit(seed, 0, RngStream.Layout, id, salt);
    }
}
