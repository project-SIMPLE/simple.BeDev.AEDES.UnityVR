using System.Collections.Generic;
using Aedes.Module3.Sim;
using NUnit.Framework;

namespace Aedes.Module3.Sim.Tests
{
    /// <summary>
    /// Module 3 plays in the village Modules 1 and 2 share, on the houses that stand there. The scene
    /// hands those in as sites (M3VillageSites); this is what the model makes of them.
    /// </summary>
    [TestFixture]
    public class VillageSitesTests
    {
        /// <summary>The two one-floor Lao houses of the Module 2 village as they stand: west of the road, 25 m apart.</summary>
        private static PlotSite[] TwoHouses => new[]
        {
            new PlotSite { X = 9.87f, Z = -33.11f, Yaw = 0f, Scale = 1.25f },
            new PlotSite { X = 9.87f, Z = -8.22f, Yaw = 0f, Scale = 1.25f },
        };

        private static ScenarioOptions WithSites(PlotSite[] sites)
        {
            var o = ScenarioOptions.Default;
            o.Sites = sites;
            return o;
        }

        [Test]
        public void TheHouseholdsAreTheHousesAndNothingElse()
        {
            var options = WithSites(TwoHouses);
            Assert.IsTrue(options.HasSites);
            Assert.AreEqual(2, options.HouseholdCount);

            var n = ScenarioBuilder.Build(1, Module3Config.Default, options);
            Assert.AreEqual(2, n.Households.Count);
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(i, n.Households[i].Id);
                Assert.IsTrue(n.Households[i].Site.HasValue, $"house {i} does not know where it stands");
                Assert.AreEqual(TwoHouses[i].Z, n.Households[i].Site.Value.Z, 1e-4f);
                Assert.GreaterOrEqual(n.Households[i].ResidentIds.Count, 3, "a household has its residents");
            }
        }

        [Test]
        public void TwoHousesFurtherApartThanReachAreStillEachOthersNeighbour()
        {
            Assert.Greater(PlotSite.Distance(TwoHouses[0], TwoHouses[1]), PlotSites.NeighbourReach);
            var n = ScenarioBuilder.Build(1, Module3Config.Default, WithSites(TwoHouses));
            CollectionAssert.AreEqual(new[] { 1 }, n.Households[0].NeighbourIds);
            CollectionAssert.AreEqual(new[] { 0 }, n.Households[1].NeighbourIds);
        }

        [Test]
        public void NeighboursAreTheHousesWithinReachAndAlwaysBothWays()
        {
            var row = new[]
            {
                new PlotSite { X = 0f, Z = 0f }, new PlotSite { X = 15f, Z = 0f }, new PlotSite { X = 30f, Z = 0f },
                // Alone at the end of the lane: its nearest is the third house, which must list it back.
                new PlotSite { X = 90f, Z = 0f },
            };
            var n = PlotSites.Neighbours(row);
            CollectionAssert.AreEqual(new[] { 1 }, n[0]);
            CollectionAssert.AreEqual(new[] { 0, 2 }, n[1]);
            CollectionAssert.AreEqual(new[] { 1, 3 }, n[2]);
            CollectionAssert.AreEqual(new[] { 2 }, n[3]);
        }

        [Test]
        public void LaneOrderCountsUpTheRoadWhicheverWayItRuns()
        {
            // A road along z (the Module 2 village), the houses handed over out of order.
            var alongZ = new[] { new PlotSite { X = 9f, Z = -8f }, new PlotSite { X = 14f, Z = -30f }, new PlotSite { X = 9f, Z = -33f } };
            CollectionAssert.AreEqual(new[] { 2, 1, 0 }, PlotSites.LaneOrder(alongZ));
            // A road along x (the generated village).
            var alongX = new[] { new PlotSite { X = 30f, Z = 0f }, new PlotSite { X = 0f, Z = 18f }, new PlotSite { X = 0f, Z = 0f } };
            CollectionAssert.AreEqual(new[] { 2, 1, 0 }, PlotSites.LaneOrder(alongX));
            CollectionAssert.AreEqual(new[] { 0 }, PlotSites.LaneOrder(new[] { new PlotSite() }));
        }

        [Test]
        public void ThreeIndexCasesInTwoHousesAreThreeDifferentPeople()
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                var n = ScenarioBuilder.Build(seed, Module3Config.Default, WithSites(TwoHouses));
                var people = new HashSet<int>();
                int indexCases = 0;
                foreach (var e in n.Log.Events)
                {
                    if (!e.IsIndexCase) continue;
                    indexCases++;
                    Assert.IsTrue(people.Add(e.TargetPersonId), $"seed {seed}: person {e.TargetPersonId} was seeded twice");
                }
                Assert.AreEqual(3, indexCases, $"seed {seed}: patient zero, a second patient and the invisible source");
            }
        }

        [Test]
        public void WithoutSitesTheLaneGridIsUnchanged()
        {
            Assert.IsFalse(ScenarioOptions.Default.HasSites);
            Assert.AreEqual(10, ScenarioOptions.Default.HouseholdCount);
            var n = ScenarioBuilder.Build(1, Module3Config.Default, ScenarioOptions.Default);
            Assert.AreEqual(10, n.Households.Count);
            Assert.IsFalse(n.Households[0].Site.HasValue);
            CollectionAssert.AreEqual(new[] { 1, 5 }, n.Households[0].NeighbourIds);
        }
    }
}
