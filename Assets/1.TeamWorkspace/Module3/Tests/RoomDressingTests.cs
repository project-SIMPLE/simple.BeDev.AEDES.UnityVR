using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Aedes.Module3.Sim.Tests
{
    /// <summary>
    /// The furniture and yard plants M3RoomDresser scatters through the houses.
    ///
    /// The dresser lives in Assembly-CSharp, which this suite cannot see, so these read its asset
    /// (Resources/Module3/M3RoomDresser.asset) as plain serialized data. What they guard is the design
    /// rule the dressing could quietly break: anything that could hold water is one of the simulation's
    /// clues, so a jar, bucket, basin, tyre, vase or sink added as decoration would be a false one.
    /// </summary>
    [TestFixture]
    public class RoomDressingTests
    {
        private const string AssetPath = "Assets/Resources/Module3/M3RoomDresser.asset";

        [Test]
        public void EveryDressingSlotIsFilled()
        {
            var empty = new List<string>();
            var prefabs = Prefabs(empty);
            Assert.IsEmpty(empty, "the dresser has empty prefab references (AEDES > Module 3 > Generate Room Dresser Asset):\n  "
                                  + string.Join("\n  ", empty));
            Assert.GreaterOrEqual(prefabs.Count, 10, "the dresser has almost nothing to furnish a house with");
        }

        [Test]
        public void NothingTheDresserPlacesCouldHoldWater()
        {
            var wet = new SortedSet<string>();
            foreach (var go in Prefabs(null))
            {
                string name = go.name.ToLowerInvariant();
                foreach (string word in new[] { "jar", "bucket", "basin", "tire", "tyre", "vase", "counter", "sink" })
                    if (name.Contains(word)) wet.Add($"{go.name}: a '{word}'");

                foreach (var c in go.GetComponentsInChildren<Component>(true))
                    if (c != null && c.GetType().Name == "WaterContainer") wet.Add($"{go.name}: carries a WaterContainer");

                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m != null && (m.name.Contains("Water") || m.name.Contains("Pond")))
                            wet.Add($"{go.name}: shows water ({m.name})");
            }

            Assert.IsTrue(wet.Count == 0,
                "the room dressing would plant a false breeding-site clue:\n  " + string.Join("\n  ", wet));
        }

        [Test]
        public void DressingPiecesAreCheapAndNotBlack()
        {
            var bad = new SortedSet<string>();
            foreach (var go in Prefabs(null))
            {
                int tris = 0;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                // Up to eight of these stand in a room, ten rooms to a village.
                if (tris > 1500) bad.Add($"{go.name}: {tris} triangles (budget 1500)");

                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m != null && m.shader != null && m.shader.name.Contains("SHD_SimpleLit"))
                            bad.Add($"{go.name}: {m.name} is on {m.shader.name}, which draws flat colour black");
            }

            Assert.IsTrue(bad.Count == 0, string.Join("\n  ", bad));
        }

        /// <summary>Every prefab the asset references; null slots are named in <paramref name="empty"/>.</summary>
        private static List<GameObject> Prefabs(List<string> empty)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetPath);
            Assert.IsNotNull(asset, $"no {AssetPath}");

            var found = new List<GameObject>();
            var it = new SerializedObject(asset).GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference || it.name == "m_Script") continue;
                if (it.objectReferenceValue is GameObject go) found.Add(go);
                else empty?.Add(it.propertyPath);
            }
            return found;
        }
    }
}
