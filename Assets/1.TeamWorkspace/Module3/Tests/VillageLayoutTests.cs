using System;
using NUnit.Framework;

namespace Aedes.Module3.Sim.Tests
{
    /// <summary>
    /// The shape of the village (VillageLayout): loose enough to read as a village, tight enough that
    /// the doorways, yards and track the rest of the module was built around still have their room.
    /// Every check is run over a couple of hundred seeds, because the layout is drawn from the seed.
    /// </summary>
    [TestFixture]
    public class VillageLayoutTests
    {
        // What M3NeighbourhoodBuilder and the scene give it.
        private const float PlotWidth = 14.5f, LaneSpacing = 18f, HouseScale = 1.4f;
        private const int Lanes = 2, PerLane = 5, Seeds = 200;

        private static PlotPlacement[] Village(int seed) =>
            VillageLayout.Place(seed, Lanes, PerLane, PlotWidth, LaneSpacing, HouseScale);

        private static float Rad(float degrees) => (float)(degrees * Math.PI / 180.0);

        /// <summary>Yaw of a house relative to squarely facing the lane, in degrees (-180..180).</summary>
        private static float OffSquare(PlotPlacement p, int lane)
        {
            float square = (float)(Math.Atan2(-VillageLayout.LaneSlope(p.X), 1.0) * 180.0 / Math.PI) + (lane % 2 == 1 ? 180f : 0f);
            float d = (p.Yaw - square) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }

        [Test]
        public void EveryHouseGetsAPlaceAndTheSameSeedGivesTheSameVillage()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var a = Village(seed);
                var b = Village(seed);
                Assert.AreEqual(Lanes * PerLane, a.Length);
                for (int i = 0; i < a.Length; i++)
                {
                    Assert.AreEqual(a[i].X, b[i].X, $"seed {seed} house {i}");
                    Assert.AreEqual(a[i].Z, b[i].Z, $"seed {seed} house {i}");
                    Assert.AreEqual(a[i].Yaw, b[i].Yaw, $"seed {seed} house {i}");
                }
            }
        }

        [Test]
        public void DifferentSeedsGiveDifferentVillages()
        {
            var a = Village(1);
            int differing = 0;
            for (int seed = 2; seed <= 20; seed++)
            {
                var b = Village(seed);
                if (Math.Abs(a[3].X - b[3].X) > 0.1f || Math.Abs(a[3].Z - b[3].Z) > 0.1f) differing++;
            }
            Assert.Greater(differing, 15, "the village should not be the same one every session");
        }

        [Test]
        public void NoTwoHousesTouchAndNeighboursKeepTheGridsGap()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var v = Village(seed);
                for (int i = 0; i < v.Length; i++)
                {
                    for (int j = i + 1; j < v.Length; j++)
                    {
                        float gap = VillageLayout.FootprintGap(v[i], v[j], HouseScale);
                        Assert.Greater(gap, 1f, $"seed {seed}: houses {i} and {j} are {gap:F2} m apart");
                        bool sameRowNeighbours = i / PerLane == j / PerLane && j == i + 1;
                        if (sameRowNeighbours)
                            Assert.GreaterOrEqual(gap, VillageLayout.MinGap - 0.01f,
                                $"seed {seed}: houses {i} and {j} leave {gap:F2} m for the side door's path");
                    }
                }
            }
        }

        [Test]
        public void NoHouseStandsNearerTheLaneThanTheGridPutIt()
        {
            // The grid's front wall was 9 - 3.4 * 1.4 = 4.24 m from the lane's centre line, with the yard
            // jars, the motorbike and the walkers' verge all fitted in the gap.
            float grid = LaneSpacing * 0.5f - VillageLayout.ModelFrontZ * HouseScale;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var v = Village(seed);
                for (int i = 0; i < v.Length; i++)
                {
                    Assert.GreaterOrEqual(VillageLayout.LaneClearance(v[i], HouseScale), grid - 0.01f,
                        $"seed {seed}: house {i} is too near the lane");
                }
            }
        }

        [Test]
        public void EveryHouseStillFacesTheLane()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var v = Village(seed);
                for (int i = 0; i < v.Length; i++)
                {
                    float off = OffSquare(v[i], i / PerLane);
                    // The lane's heading changes a little between where the layout measures it and where the house ends up.
                    Assert.LessOrEqual(Math.Abs(off), VillageLayout.MaxYawJitter + 4f,
                        $"seed {seed}: house {i} is turned {off:F1} degrees off square to the lane");

                    // And it is on the side of the lane its row belongs on, looking across it.
                    float laneZ = VillageLayout.LaneZ(v[i].X);
                    bool nearRow = i / PerLane % 2 == 0;
                    Assert.IsTrue(nearRow ? v[i].Z < laneZ : v[i].Z > laneZ,
                        $"seed {seed}: house {i} is on the wrong side of the lane");
                }
            }
        }

        [Test]
        public void TheHouseAcrossTheLaneIsStillAcrossTheLane()
        {
            // The model says these two are neighbours, so the player has to be able to see one from the other.
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var v = Village(seed);
                for (int i = 0; i < PerLane; i++)
                {
                    var a = v[i];
                    var b = v[PerLane + i];
                    float dx = b.X - a.X, dz = b.Z - a.Z;
                    float across = (float)Math.Sqrt(dx * dx + dz * dz);
                    Assert.GreaterOrEqual(across, LaneSpacing - 0.5f, $"seed {seed}: pair {i} is closer than the grid had it");
                    Assert.LessOrEqual(across, 32f, $"seed {seed}: pair {i} is {across:F1} m apart");

                    // How far one is along the lane from the other. The rows are not in step, but the far one
                    // should still overlap it (a house is 12 m wide), or it is a diagonal neighbour.
                    float midX = (a.X + b.X) * 0.5f;
                    float slope = VillageLayout.LaneSlope(midX);
                    float norm = (float)Math.Sqrt(1f + slope * slope);
                    float along = (dx + dz * slope) / norm;
                    Assert.LessOrEqual(Math.Abs(along), 8f, $"seed {seed}: pair {i} is {along:F1} m out of step along the lane");
                }
            }
        }

        [Test]
        public void TheVillageIsNotAGrid()
        {
            // Each village varies in every way the grid did not: how square each house stands, how far back
            // it is, and how much room it has beside it. Far below what any seed produces (the least varied
            // of 300 spreads 7 degrees, 1.3 m and 2 m), so this only fails if the layout went back to a grid.
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var v = Village(seed);
                float yawMin = float.MaxValue, yawMax = float.MinValue;
                float backMin = float.MaxValue, backMax = float.MinValue;
                float gapMin = float.MaxValue, gapMax = float.MinValue;
                for (int i = 0; i < v.Length; i++)
                {
                    float off = OffSquare(v[i], i / PerLane);
                    yawMin = Math.Min(yawMin, off); yawMax = Math.Max(yawMax, off);
                    float clear = VillageLayout.LaneClearance(v[i], HouseScale);
                    backMin = Math.Min(backMin, clear); backMax = Math.Max(backMax, clear);
                    if (i % PerLane > 0)
                    {
                        float gap = VillageLayout.FootprintGap(v[i - 1], v[i], HouseScale);
                        gapMin = Math.Min(gapMin, gap); gapMax = Math.Max(gapMax, gap);
                    }
                }
                Assert.Greater(yawMax - yawMin, 4f, $"seed {seed}: the houses all stand square to the lane");
                Assert.Greater(backMax - backMin, 0.8f, $"seed {seed}: the houses are all the same distance from the lane");
                Assert.Greater(gapMax - gapMin, 1f, $"seed {seed}: the houses are all evenly spaced");
            }
        }

        [Test]
        public void TheLaneWindsButNeverTurnsSharply()
        {
            float lowest = float.MaxValue, highest = float.MinValue;
            for (float x = -40f; x <= 100f; x += 0.5f)
            {
                float z = VillageLayout.LaneZ(x);
                lowest = Math.Min(lowest, z); highest = Math.Max(highest, z);
                Assert.Less(Math.Abs(VillageLayout.LaneSlope(x)), 0.3f, $"the lane turns more than 16 degrees at x = {x}");
            }
            Assert.Greater(highest - lowest, 4f, "the lane should visibly wind, not run straight");
        }

        [Test]
        public void TheLaneIsStraightAtItsHeadWhereThePilotStarts()
        {
            // M3XRPlayer spawns the Pilot at (-8, 0, 8), facing down the lane.
            for (float x = -60f; x <= VillageLayout.HeadX; x += 2f)
            {
                Assert.AreEqual(VillageLayout.LaneBaseZ, VillageLayout.LaneZ(x), 0.001f, $"the lane bends at x = {x}");
            }
            Assert.Less(VillageLayout.DistanceToLane(-8f, 8f), 1.5f, "the Pilot's start is off the lane");
        }

        [Test]
        public void NoHouseStandsOnThePilotsStart()
        {
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var v = Village(seed);
                for (int i = 0; i < v.Length; i++)
                {
                    var c = VillageLayout.Corners(v[i], HouseScale);
                    for (int k = 0; k < 4; k++)
                    {
                        float dx = c[k * 2] + 8f, dz = c[k * 2 + 1] - 8f;
                        Assert.Greater(Math.Sqrt(dx * dx + dz * dz), 2.5, $"seed {seed}: a corner of house {i} is at the Pilot's start");
                    }
                }
            }
        }

        [Test]
        public void ASingleRowAndAnEmptyVillageAreFine()
        {
            Assert.AreEqual(5, VillageLayout.Place(1, 1, 5, PlotWidth, LaneSpacing, HouseScale).Length);
            Assert.AreEqual(0, VillageLayout.Place(1, 0, 5, PlotWidth, LaneSpacing, HouseScale).Length);
            Assert.AreEqual(0, VillageLayout.Place(1, 2, 0, PlotWidth, LaneSpacing, HouseScale).Length);
        }
    }
}
