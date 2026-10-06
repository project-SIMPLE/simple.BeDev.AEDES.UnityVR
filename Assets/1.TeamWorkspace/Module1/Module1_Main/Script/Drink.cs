using UnityEngine;

/// <summary>
/// Marks the proboscis. Drinking, mating and laying eggs used to run off this object's trigger
/// volume, but the snout is only a few millimetres thick against a flower barely 0.35 across, so
/// feeding meant spearing the target almost dead centre. PlayerMain does the detection now - a
/// distance-and-angle test from the head - and plays the particle effects and scoring from there.
///
/// The component is kept so the scene references on the proboscis stay valid.
/// </summary>
public class Drink : MonoBehaviour
{
    public PlayerMain player;
    private void Start()
    {
        player = PlayerMain.instance;
    }
}
