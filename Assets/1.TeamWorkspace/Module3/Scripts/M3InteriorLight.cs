using UnityEngine;

/// <summary>
/// One warm, shadowless lamp under the ceiling of whichever house the Pilot is standing in.
///
/// Ambient alone leaves a room flat: with the cyan sky ambient gone (M3TimeOfDay) the walls read
/// cream, but the bed, the net and the fan still had nothing to pick them out from the wall behind
/// them. A point light in the middle of the room gives the near things a little more light than the
/// corners, and the fill it adds is what the villagers' skin is seen by at conversation distance.
///
/// Only the house the Pilot is in is lit - the Quest URP configuration allows one additional light
/// per object and this is that one - so there is a single Light in the scene for the job, moved to
/// whichever room the head is in and switched off outdoors. Which house that is comes from the same
/// footprint test M3VisitTracker uses to count a visit. Added at runtime by M3TimeOfDay.
/// </summary>
public class M3InteriorLight : MonoBehaviour
{
    [SerializeField] private Color colour = new Color(1f, 0.87f, 0.70f);
    [Tooltip("What the lamp fades to as the afternoon comes on outside.")]
    [SerializeField] private Color afternoonColour = new Color(1f, 0.78f, 0.56f);
    [SerializeField] private float intensity = 0.7f;
    [Tooltip("Metres. The main room of the scaled house is about 9 m across.")]
    [SerializeField] private float range = 9f;
    [Tooltip("Where the lamp hangs, in the house model's space (scaled with the house): over the middle "
             + "of the main room, clear of the partition at x = -1.9, and well below the ceiling - at "
             + "1.65 (0.45 m under it) the ceiling took a blown-out white disc.")]
    [SerializeField] private Vector3 roomCentre = new Vector3(1.2f, 1.30f, 0.15f);

    private Light lamp;
    private int litHousehold = -1;

    private void Awake()
    {
        var go = new GameObject("Interior Light (runtime)");
        go.transform.SetParent(transform, false);
        lamp = go.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.shadows = LightShadows.None;
        lamp.range = range;
        lamp.intensity = intensity;
        lamp.color = colour;
        lamp.enabled = false;
    }

    private void LateUpdate()
    {
        var cam = Camera.main;
        HouseholdView inside = null;
        if (cam != null)
        {
            Vector3 head = cam.transform.position;
            var plots = M3NeighbourhoodBuilder.HouseholdViews;
            for (int i = 0; i < plots.Count; i++)
            {
                if (M3VisitTracker.IsInside(plots[i], head)) { inside = plots[i]; break; }
            }
        }

        if (inside == null)
        {
            if (lamp.enabled) lamp.enabled = false;
            litHousehold = -1;
            return;
        }

        if (inside.householdId != litHousehold)
        {
            litHousehold = inside.householdId;
            lamp.transform.position = inside.transform.TransformPoint(roomCentre * M3NeighbourhoodBuilder.HouseScale);
            lamp.enabled = true;
        }

        var tod = M3TimeOfDay.Instance;
        lamp.color = tod != null ? Color.Lerp(colour, afternoonColour, tod.Warmth) : colour;
    }
}
