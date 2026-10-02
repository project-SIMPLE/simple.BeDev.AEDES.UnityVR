using System;
using System.Collections.Generic;

namespace Aedes.Module3.Sim
{
    /// <summary>
    /// Where a house stands: world x and z in metres, the yaw in degrees of the plot's +z axis, and how
    /// much larger than its model the house is. VillageLayout draws these for a generated neighbourhood;
    /// a scene that already has a village - Modules 1 and 2's, which Module 3 now plays in - hands them
    /// in instead, one per house that can hold a household (M3VillageSites), and the simulation then
    /// has exactly as many households as the village has such houses.
    /// </summary>
    public struct PlotSite
    {
        public float X;
        public float Z;
        public float Yaw;
        public float Scale;

        public static float Distance(PlotSite a, PlotSite b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }

    /// <summary>What ScenarioBuilder needs of a set of sites: their order along the lane, and who neighbours whom.</summary>
    public static class PlotSites
    {
        /// <summary>
        /// Centre to centre, how far apart two houses can stand and still be neighbours in the model.
        /// Section 5: Aedes aegypti does not travel far, so the chain runs between neighbours. The
        /// generated village stands facing houses 18 m apart and the ones along a row 14.5 m; this is a
        /// little wider, for a village laid out by hand. The nearest house always counts whatever the
        /// distance, both ways, so no household is ever cut off from the outbreak.
        /// </summary>
        public const float NeighbourReach = 20f;

        /// <summary>
        /// The indices of <paramref name="sites"/> in lane order: along whichever of x and z the houses
        /// spread further on (the road), then across it. Household ids follow this order, so the numbers
        /// on the houses count up the road.
        /// </summary>
        public static int[] LaneOrder(IReadOnlyList<PlotSite> sites)
        {
            var order = new int[sites.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            if (sites.Count < 2) return order;

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < sites.Count; i++)
            {
                minX = Math.Min(minX, sites[i].X); maxX = Math.Max(maxX, sites[i].X);
                minZ = Math.Min(minZ, sites[i].Z); maxZ = Math.Max(maxZ, sites[i].Z);
            }
            bool alongX = maxX - minX >= maxZ - minZ;

            Array.Sort(order, (a, b) =>
            {
                float pa = alongX ? sites[a].X : sites[a].Z, pb = alongX ? sites[b].X : sites[b].Z;
                int c = pa.CompareTo(pb);
                if (c != 0) return c;
                float qa = alongX ? sites[a].Z : sites[a].X, qb = alongX ? sites[b].Z : sites[b].X;
                c = qa.CompareTo(qb);
                return c != 0 ? c : a.CompareTo(b);
            });
            return order;
        }

        /// <summary>
        /// For every site, the indices of its neighbours: each other site within <see cref="NeighbourReach"/>,
        /// plus its nearest one regardless. Symmetric: if a lists b, b lists a.
        /// </summary>
        public static List<int>[] Neighbours(IReadOnlyList<PlotSite> sites)
        {
            var result = new List<int>[sites.Count];
            for (int i = 0; i < sites.Count; i++) result[i] = new List<int>();

            for (int i = 0; i < sites.Count; i++)
            {
                int nearest = -1;
                float nearestDistance = float.MaxValue;
                for (int j = 0; j < sites.Count; j++)
                {
                    if (j == i) continue;
                    float d = PlotSite.Distance(sites[i], sites[j]);
                    if (d <= NeighbourReach) Link(result, i, j);
                    if (d < nearestDistance) { nearestDistance = d; nearest = j; }
                }
                if (nearest >= 0) Link(result, i, nearest);
            }
            for (int i = 0; i < result.Length; i++) result[i].Sort();
            return result;
        }

        private static void Link(List<int>[] result, int a, int b)
        {
            if (!result[a].Contains(b)) result[a].Add(b);
            if (!result[b].Contains(a)) result[b].Add(a);
        }
    }
}
