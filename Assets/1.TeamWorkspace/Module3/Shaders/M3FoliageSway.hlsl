#ifndef M3_FOLIAGE_SWAY_INCLUDED
#define M3_FOLIAGE_SWAY_INCLUDED

// Wind for the Module 3 palms and banana plants: a vertex offset that grows with the distance from
// the plant's base, so the trunk stands still and the fronds move. Applied in every pass that
// places a vertex (M3Foliage*Pass.hlsl), so the shadow moves with the leaf that casts it.
//
// Distances are in the mesh's own units. The palm and the banana are both modelled with their base at
// the origin - every frond of the palm is a separate mesh in tree space - so the distance from the
// origin says how far up the plant a vertex is, whatever the instance is scaled to.
//
// The two plants differ (a palm's fronds hang from the top of a 3 unit trunk, a banana's leaves start
// nearer the ground), and one fixed reach gave the palm 0.13 m of sway at its tips instead of 0.22
// while its crown base, which is bolted to a trunk that does not move, slid 6.5 cm off the top of it.
// So the sway runs from 0 where a plant's leaves begin to full strength at the farthest leaf tip, and
// those two distances come from the material: Module3OutdoorLifeAssets measures them off the plant's
// own leaf meshes and writes them in.
//
// They live in two cbuffer slots URP's Lit already declares and this shader never uses (the clear-coat
// mask and smoothness; the Properties block calls them "SRP batching compatibility for Clear Coat (Not
// used in Lit)"): a float added to the material of its own would have to go into the UnityPerMaterial
// cbuffer in LitInput.hlsl, which is the package's and not ours to change, or the SRP batcher drops
// the shader. A material that carries no range (0, 0) gets the old fixed reach.
#define M3_SWAY_FROM _ClearCoatMask         // mesh units: distance from the origin of the nearest leaf vertex
#define M3_SWAY_TO _ClearCoatSmoothness     // mesh units: distance of the farthest, where the sway is full
#define M3_SWAY_AMPLITUDE 0.22   // metres, at the farthest leaf tip, in world space
#define M3_SWAY_REACH 5.5        // fallback reach for a material with no measured range
#define M3_SWAY_SPEED 0.9        // radians per second of the main lean

float M3SwayWeight(float distanceFromOrigin)
{
    float from = M3_SWAY_FROM, to = M3_SWAY_TO;
    float reach = (to > from + 0.05)
        ? saturate((distanceFromOrigin - from) / (to - from))
        : saturate(distanceFromOrigin / M3_SWAY_REACH);
    return reach * reach;
}

float3 M3FoliageSway(float3 positionOS)
{
    float weight = M3SwayWeight(length(positionOS));
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
