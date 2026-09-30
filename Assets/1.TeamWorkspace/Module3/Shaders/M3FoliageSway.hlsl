#ifndef M3_FOLIAGE_SWAY_INCLUDED
#define M3_FOLIAGE_SWAY_INCLUDED

// Wind for the Module 3 palms and banana plants: a vertex offset that grows with the distance from
// the plant's base, so the trunk stands still and the fronds move. Applied in every pass that
// places a vertex (M3Foliage*Pass.hlsl), so the shadow moves with the leaf that casts it.
//
// Constants rather than material properties: a float added to the Lit material would have to go
// into its UnityPerMaterial cbuffer (LitInput.hlsl) or the SRP batcher drops the shader, and one
// setting suits both plants.
//
// Reach is in the mesh's own units. The palm and the banana are both modelled with their base at
// the origin - every frond of the palm is a separate mesh in tree space - so the distance from the
// origin says how far up the plant a vertex is, whatever the instance is scaled to.
#define M3_SWAY_AMPLITUDE 0.22   // metres, at full reach, in world space
#define M3_SWAY_REACH 5.5        // mesh units from the base at which the sway is full strength
#define M3_SWAY_SPEED 0.9        // radians per second of the main lean

float3 M3FoliageSway(float3 positionOS)
{
    float reach = saturate(length(positionOS) / M3_SWAY_REACH);
    float weight = reach * reach;
    // Each plant has its own phase from where it stands, and the fronds of one plant lag each other a little.
    float3 pivotWS = GetObjectToWorldMatrix()._m03_m13_m23;
    float phase = dot(pivotWS.xz, float2(0.37, 0.61)) + dot(positionOS.xz, float2(0.21, 0.13));
    float t = _Time.y * M3_SWAY_SPEED + phase;
    // A slow lean with a faster flutter on top, mostly along the wind with a little across it.
    float along = sin(t) + 0.45 * sin(t * 2.3 + 1.7);
    float across = 0.5 * sin(t * 0.7 + 0.9) + 0.25 * sin(t * 3.1);
    float3 offsetWS = float3(along * 0.8 + across * 0.3, 0.0, across * 0.8 - along * 0.3) * (M3_SWAY_AMPLITUDE * weight);
    // Back into object space, so the caller's own transform is applied once, as it would have been.
    return positionOS + mul((float3x3)GetWorldToObjectMatrix(), offsetWS);
}

#endif
