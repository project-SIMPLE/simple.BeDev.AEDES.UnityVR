using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Aedes.Module3.Sim.Tests
{
    /// <summary>
    /// The placeholder props exist where the code expects them, with the sockets the code looks
    /// up by name.
    ///
    /// The whole unblocking pattern rests on paths and socket names staying put while the meshes
    /// inside get replaced by real art. If one moves, nothing fails at compile time and nothing
    /// fails in play mode either - a prop just quietly stops appearing. These tests are the only
    /// thing that would catch that.
    /// </summary>
    [TestFixture]
    public class PlaceholderAssetTests
    {
        [Test]
        public void EveryPropThatCodeBindsToExists()
        {
            var missing = new List<string>();
            foreach (string path in Module3Props.AllPrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) missing.Add(path);
            }

            Assert.IsEmpty(missing,
                "code binds to these prefab paths and nothing is there:\n  " + string.Join("\n  ", missing));
        }

        [Test]
        public void TheFanBladeIsASeparateObjectTheViewCanTurn()
        {
            // A welded blade makes the prop unusable, which is why the asset brief called it out.
            var fan = AssetDatabase.LoadAssetAtPath<GameObject>(Module3Props.ElectricFan);
            Assert.IsNotNull(fan, "no fan prefab");

            Transform blade = null;
            foreach (var t in fan.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == Module3Props.FanBlade) { blade = t; break; }
            }

            Assert.IsNotNull(blade, $"the fan has no '{Module3Props.FanBlade}' object to rotate");
            Assert.IsNotNull(blade.GetComponent<MeshFilter>(), "the blade carries no mesh of its own");
        }

        [Test]
        public void TheTwoNetStatesSwapAtOneTransform()
        {
            // PutUpNet swaps rolled-up for deployed. If their origins disagree the net jumps
            // across the room at the moment the player does the most important thing in the module.
            var rolled = AssetDatabase.LoadAssetAtPath<GameObject>(Module3Props.MosquitoNetRolledUp);
            var deployed = AssetDatabase.LoadAssetAtPath<GameObject>(Module3Props.MosquitoNetDeployed);
            Assert.IsNotNull(rolled);
            Assert.IsNotNull(deployed);

            Assert.AreEqual(Vector3.zero, rolled.transform.localPosition);
            Assert.AreEqual(Vector3.zero, deployed.transform.localPosition);
        }

        [Test]
        public void TheScreenStatesSwapAtOneTransform()
        {
            var torn = AssetDatabase.LoadAssetAtPath<GameObject>(Module3Props.WindowScreenTorn);
            var intact = AssetDatabase.LoadAssetAtPath<GameObject>(Module3Props.WindowScreenIntact);
            Assert.IsNotNull(torn);
            Assert.IsNotNull(intact);

            var tornBounds = Bounds(torn);
            var intactBounds = Bounds(intact);

            // Both have to fill the same window opening; depth may differ where the tear sticks out.
            Assert.AreEqual(intactBounds.size.x, tornBounds.size.x, 0.02f, "the screens are different widths");
            Assert.AreEqual(intactBounds.size.y, tornBounds.size.y, 0.02f, "the screens are different heights");
        }

        [Test]
        public void HandheldPropsCarryAGripSocket()
        {
            string[] handheld =
            {
                Module3Props.RepellentBottle, Module3Props.DrinkingVessel,
                Module3Props.Cloth, Module3Props.Torch,
            };

            foreach (string path in handheld)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(go, path);

                bool found = false;
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == Module3Props.Socket.Grip) { found = true; break; }
                }
                Assert.IsTrue(found, $"{path} has no {Module3Props.Socket.Grip} for a hand to hold it by");
            }
        }

        [Test]
        public void PropsAreWithinTheMobileTriangleBudget()
        {
            // Quest 3 is a standalone mobile GPU holding ten houses at once.
            var over = new List<string>();
            foreach (string path in Module3Props.AllPrefabs)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                int tris = 0;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                }
                if (tris > 8000) over.Add($"{path}: {tris} triangles");
            }

            Assert.IsEmpty(over, "over budget:\n  " + string.Join("\n  ", over));
        }

        private static Bounds Bounds(GameObject go)
        {
            var b = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }
    }
}
