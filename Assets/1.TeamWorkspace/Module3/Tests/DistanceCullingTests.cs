using NUnit.Framework;
using UnityEngine;

namespace Aedes.Module3.Sim.Tests
{
    /// <summary>
    /// M3DistanceCulling keys off a named layer in ProjectSettings/TagManager. If the layer is
    /// renamed or lost in a merge nothing fails at runtime - the culling silently switches off
    /// and every villager in the village is drawn again. This is the only thing that would notice.
    /// </summary>
    [TestFixture]
    public class DistanceCullingTests
    {
        [Test]
        public void TheDistanceCulledLayerExists()
        {
            Assert.GreaterOrEqual(LayerMask.NameToLayer(Module3Props.DistanceCulledLayer), 0,
                $"no '{Module3Props.DistanceCulledLayer}' layer in TagManager: distance culling of villagers and props is off");
        }
    }
}
