using UnityEngine;

/// <summary>
/// Marks the proboscis. Drinking used to run off this object's trigger volume, but that capsule is
/// only 0.03 thick in world units against a flower barely 0.35 across, so feeding meant spearing
/// the target almost dead centre. PlayerMain does the detection now -
/// a distance-and-angle test from the head - and it covers mating and egg laying the same way.
///
/// The component is kept so the scene reference on the proboscis stays valid, and because the
/// object's placement is still what tells you where the mosquito's mouth is.
/// </summary>
public class Drink : MonoBehaviour
{
}
