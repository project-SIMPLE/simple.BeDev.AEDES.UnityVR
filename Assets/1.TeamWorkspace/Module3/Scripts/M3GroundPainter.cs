using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

/// <summary>
/// Paints the Module 3 ground: grass in patches and tufts, and a dirt track with wheel ruts,
/// pebbles and a strip of grass down the middle.
///
/// The ground used to be flat colour quads, which from a headset reads as a grey-green void with
/// a brown stripe. Two images fix that without a texture asset and without touching the scene file:
///
///  - <see cref="PaintVillage"/>: one image that never repeats, laid over the village and its
///    lane. The track is painted into it, so its edge can be ragged and grass can creep across it -
///    two quads of different colours cannot do that.
///  - <see cref="PaintTile"/>: a small seamless tile for the fields out to the horizon, where the
///    repetition disappears into the haze.
///
/// Everything comes from noise seeded by the scene seed, so a given seed always paints the same
/// ground. The work runs as a Burst job: a 2200 x 1250 image is about 2.7 million pixels.
///
/// Colours are written the way the flat quads had them - as sRGB values in an sRGB texture - so
/// the average ground colour is still the `grass` and `track` colours the artist picked.
/// </summary>
public static class M3GroundPainter
{
    /// <summary>How far in from the edge of the village image the pattern fades to a calm grass, so it meets the tile.</summary>
    private const float EdgeFade = 14f;
    /// <summary>How far in from the ends of the village image the track peters out into grass.</summary>
    private const float TrackFade = 18f;

    /// <summary>
    /// A non-repeating ground image for <paramref name="area"/> (x, z in world metres), with the track
    /// running along x at <paramref name="trackZ"/>. Texels are about <paramref name="texel"/> metres
    /// across, coarser if that would pass <paramref name="maxSize"/> pixels on a side.
    /// </summary>
    public static Texture2D PaintVillage(Rect area, float texel, int maxSize, float trackZ, float trackHalfWidth,
                                         Color grass, Color track, float contrast, int seed)
    {
        int w = Mathf.Clamp(Mathf.CeilToInt(area.width / texel), 16, maxSize);
        int h = Mathf.Clamp(Mathf.CeilToInt(area.height / texel), 16, maxSize);
        var pixels = new NativeArray<Color32>(w * h, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        try
        {
            new VillageJob
            {
                pixels = pixels,
                width = w,
                origin = new float2(area.xMin, area.yMin),
                step = new float2(area.width / w, area.height / h),
                rectMin = new float2(area.xMin, area.yMin),
                rectMax = new float2(area.xMax, area.yMax),
                seedOffset = SeedOffset(seed),
                seedF = seed * 1.7f,
                trackZ = trackZ,
                trackHalf = trackHalfWidth,
                grass = float3(grass.r, grass.g, grass.b),
                track = float3(track.r, track.g, track.b),
                contrast = contrast,
            }.Schedule(h, 4).Complete();
            return Finish(pixels, w, h, TextureWrapMode.Clamp, "Ground (painted)");
        }
        finally { pixels.Dispose(); }
    }

    /// <summary>A seamless grass tile of <paramref name="size"/> pixels a side, calmer than the village image.</summary>
    public static Texture2D PaintTile(int size, Color grass, float contrast, int seed)
    {
        var pixels = new NativeArray<Color32>(size * size, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        try
        {
            new TileJob
            {
                pixels = pixels,
                size = size,
                seedOffset = SeedOffset(seed + 101),
                grass = float3(grass.r, grass.g, grass.b),
                contrast = contrast,
            }.Schedule(size, 4).Complete();
            return Finish(pixels, size, size, TextureWrapMode.Repeat, "Fields (tile)");
        }
        finally { pixels.Dispose(); }
    }

    private static float2 SeedOffset(int seed) => float2(seed * 37.31f % 900f, seed * 19.77f % 900f);

    private static Texture2D Finish(NativeArray<Color32> pixels, int w, int h, TextureWrapMode wrap, string name)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, false)
        {
            name = name,
            wrapMode = wrap,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4,
        };
        tex.SetPixelData(pixels, 0);
        tex.Apply(true, true); // mipmaps, and drop the CPU copy: nothing reads it back
        return tex;
    }

    // ---------- shading ----------

    /// <summary>The noise the grass is made from, each roughly -1..1. The two jobs sample it differently (plain vs seamless).</summary>
    private struct GrassNoise
    {
        public float macro; // ~10 m: whole patches of lush or dry grass
        public float mid;   // ~2 m: mottling inside a patch
        public float fine;  // ~30 cm: grain
        public float speck; // ~12 cm: dark tufts
        public float blade; // ~15 cm: pale dry blades
    }

    private static float3 DryTint => float3(1.26f, 1.06f, 0.86f);
    private static float3 LushTint => float3(0.74f, 0.86f, 0.86f);

    private static float3 ShadeGrass(float3 grass, in GrassNoise n, float amount)
    {
        // Big lazy patches: sun-bleached and yellow, or damp and deep green.
        float dry = smoothstep(0.10f, 0.65f, n.macro);
        float lush = smoothstep(-0.10f, -0.65f, n.macro);
        float3 c = lerp(grass, grass * DryTint, dry * 0.85f * amount);
        c = lerp(c, grass * LushTint, lush * 0.75f * amount);
        // Mottling and grain, then tufts (dark) and dry blades (pale) as sparse specks.
        c *= 1f + amount * (0.11f * n.mid + 0.07f * n.fine);
        c *= 1f - amount * 0.24f * smoothstep(0.45f, 0.85f, n.speck);
        c = lerp(c, c * float3(1.30f, 1.25f, 0.90f), amount * 0.5f * smoothstep(0.55f, 0.90f, n.blade));
        return c;
    }

    private static Color32 ToColor32(float3 c)
    {
        c = saturate(c);
        return new Color32((byte)(c.x * 255f + 0.5f), (byte)(c.y * 255f + 0.5f), (byte)(c.z * 255f + 0.5f), 255);
    }

    /// <summary>Normalised fbm sits well inside -1..1; this stretches it so the patches actually reach the thresholds.</summary>
    private const float FbmGain = 1.6f;

    private static float Fbm(float2 p, int octaves)
    {
        float sum = 0f, amp = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += amp * noise.snoise(p);
            norm += amp;
            p = p * 2.03f + 17.3f;
            amp *= 0.5f;
        }
        return clamp(sum / norm * FbmGain, -1f, 1f);
    }

    /// <summary>Seamless fbm: <paramref name="cycles"/> whole periods across the unit square, doubling each octave.</summary>
    private static float FbmTile(float2 uv, float2 offset, int cycles, int octaves)
    {
        float sum = 0f, amp = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            float c = cycles << i;
            sum += amp * noise.pnoise(uv * c + offset * (i + 1), float2(c, c));
            norm += amp;
            amp *= 0.5f;
        }
        return clamp(sum / norm * FbmGain, -1f, 1f);
    }

    // ---------- jobs ----------

    [BurstCompile]
    private struct VillageJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<Color32> pixels;
        public int width;
        public float2 origin, step, rectMin, rectMax, seedOffset;
        public float seedF, trackZ, trackHalf, contrast;
        public float3 grass, track;

        public void Execute(int row)
        {
            for (int col = 0; col < width; col++)
            {
                float2 w = origin + float2(col + 0.5f, row + 0.5f) * step;
                pixels[row * width + col] = ToColor32(Shade(w));
            }
        }

        private float3 Shade(float2 w)
        {
            float2 s = w + seedOffset;

            // Near the edge of the image the pattern calms down so the seam against the tile is soft.
            float edge = min(min(w.x - rectMin.x, rectMax.x - w.x), min(w.y - rectMin.y, rectMax.y - w.y));
            float amount = contrast * lerp(0.35f, 1f, smoothstep(0f, EdgeFade, edge));

            var n = new GrassNoise
            {
                macro = Fbm(s * 0.09f, 3),
                mid = Fbm(s * 0.50f + 31f, 2),
                fine = noise.snoise(s * 3.1f + 7f),
                speck = noise.snoise(s * 8.5f + 91f),
                blade = noise.snoise(s * 6.3f + 53f),
            };
            float3 g = ShadeGrass(grass, n, amount);

            // The track wanders a little along its length and varies in width.
            float zc = trackZ + 0.45f * noise.snoise(float2(w.x * 0.03f, seedF))
                              + 0.12f * noise.snoise(float2(w.x * 0.11f, seedF + 5f));
            float halfWidth = trackHalf * (1f + 0.15f * noise.snoise(float2(w.x * 0.07f, seedF + 9f)));
            float u = w.y - zc;
            // Distance outside the edge (negative inside), with the edge itself ragged.
            float d = abs(u) + 0.26f * noise.snoise(s * 1.4f + 3f) + 0.08f * noise.snoise(s * 5f) - halfWidth;
            float t = 1f - smoothstep(-0.10f, 0.22f, d);
            // Peter out into grass at the ends, in patches rather than a clean fade.
            float end = smoothstep(0f, TrackFade, min(w.x - rectMin.x, rectMax.x - w.x));
            t *= smoothstep(0.2f, 0.8f, end + 0.3f * n.mid);

            // Grass beside the track is trodden thin and dry.
            g = lerp(g, g * DryTint, smoothstep(0.9f, 0f, d) * (1f - t) * 0.5f * amount);
            if (t <= 0f) return g;

            // Dirt: dry and damp patches, pebbles, and two wheel ruts that come and go.
            float3 dirt = track * (1f + amount * (0.13f * Fbm(s * 0.30f + 5f, 2) + 0.05f * noise.snoise(s * 3.5f + 21f)));
            float peb = noise.snoise(s * 10f + 3f);
            dirt *= 1f + amount * (0.17f * smoothstep(0.5f, 0.85f, peb) - 0.15f * smoothstep(0.5f, 0.85f, -peb));
            float rutDist = abs(u) - 0.72f;
            float rut = exp(-rutDist * rutDist / (2f * 0.15f * 0.15f));
            float rutOn = smoothstep(-0.4f, 0.2f, noise.snoise(float2(w.x * 0.20f, seedF + (u > 0f ? 40f : 80f))));
            dirt *= 1f - 0.28f * rut * rutOn * amount;
            // Between the ruts, grass grows in tussocks.
            float hump = smoothstep(0.55f, 0.15f, abs(u)) * smoothstep(0.05f, 0.50f, Fbm(s * 0.9f + 77f, 2));
            dirt = lerp(dirt, g * 0.95f, hump * 0.85f);

            return lerp(g, dirt, t);
        }
    }

    [BurstCompile]
    private struct TileJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, WriteOnly] public NativeArray<Color32> pixels;
        public int size;
        public float2 seedOffset;
        public float3 grass;
        public float contrast;

        public void Execute(int row)
        {
            for (int col = 0; col < size; col++)
            {
                float2 uv = float2(col + 0.5f, row + 0.5f) / size;
                // Cycle counts per tile of 32 m: ~10 m, ~2.7 m, ~0.65 m and ~0.33 m features.
                var n = new GrassNoise
                {
                    macro = FbmTile(uv, seedOffset, 3, 2),
                    mid = FbmTile(uv, seedOffset + 11f, 12, 2),
                    fine = noise.pnoise(uv * 48f + seedOffset, float2(48f, 48f)),
                    speck = noise.pnoise(uv * 96f + seedOffset + 5f, float2(96f, 96f)),
                    blade = noise.pnoise(uv * 72f + seedOffset + 9f, float2(72f, 72f)),
                };
                // Calmer than the village image: at that distance it only has to look like the same field.
                pixels[row * size + col] = ToColor32(ShadeGrass(grass, n, contrast * 0.5f));
            }
        }
    }
}
