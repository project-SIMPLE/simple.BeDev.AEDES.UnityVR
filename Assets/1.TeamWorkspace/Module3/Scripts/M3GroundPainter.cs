using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

/// <summary>
/// Paints the Module 3 ground: grass in patches and tufts, a dirt track with wheel ruts, pebbles
/// and a strip of grass down the middle, and the worn dirt round each house - a swept yard on the
/// lane side, and the path down the gap between houses to the side door.
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
    /// <summary>How deep the swept yard reaches out from a house's front wall, in metres.</summary>
    private const float YardDepth = 1.8f;

    /// <summary>
    /// Where one plot stands, in world x/z: its origin and its local +x and +z axes. Local +z points
    /// across the front yard to the lane, so one <see cref="YardLayout"/> serves the row on either
    /// side of the lane, mirrored by the plot's rotation.
    /// </summary>
    public struct PlotFrame
    {
        public float2 origin, right, forward;
        /// <summary>Local z of the lane's centre line.</summary>
        public float laneDist;
    }

    /// <summary>Where the parts of a house sit in plot space, in metres.</summary>
    public struct YardLayout
    {
        /// <summary>The house's side walls. The side door is in the +x wall, facing the gap to the next house.</summary>
        public float minX, maxX;
        /// <summary>The front wall, on the lane side.</summary>
        public float frontZ;
        /// <summary>Where the side door opens along that wall.</summary>
        public float doorZ;
        /// <summary>Where the path from the lane runs down the gap.</summary>
        public float pathX;
    }

    /// <summary>The frame of a plot object whose local +z faces the lane, whose centre line runs along world x at <paramref name="laneZ"/>.</summary>
    public static PlotFrame FrameOf(Transform plot, float laneZ)
    {
        Vector3 o = plot.position, r = plot.right, f = plot.forward;
        float2 forward = normalizesafe(float2(f.x, f.z));
        return new PlotFrame
        {
            origin = float2(o.x, o.z),
            right = normalizesafe(float2(r.x, r.z)),
            forward = forward,
            laneDist = (laneZ - o.z) * forward.y,
        };
    }

    /// <summary>
    /// A non-repeating ground image for <paramref name="area"/> (x, z in world metres), with the track
    /// running along x at <paramref name="trackZ"/> and worn yards round each of <paramref name="plots"/>.
    /// Texels are about <paramref name="texel"/> metres across, coarser if that would pass
    /// <paramref name="maxSize"/> pixels on a side.
    /// </summary>
    public static Texture2D PaintVillage(Rect area, float texel, int maxSize, float trackZ, float trackHalfWidth,
                                         PlotFrame[] plots, YardLayout yard,
                                         Color grass, Color track, float contrast, int seed)
    {
        int w = Mathf.Clamp(Mathf.CeilToInt(area.width / texel), 16, maxSize);
        int h = Mathf.Clamp(Mathf.CeilToInt(area.height / texel), 16, maxSize);
        var pixels = new NativeArray<Color32>(w * h, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var plotFrames = new NativeArray<PlotFrame>(plots ?? new PlotFrame[0], Allocator.TempJob);
        try
        {
            new VillageJob
            {
                pixels = pixels,
                plots = plotFrames,
                yard = yard,
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
        finally
        {
            pixels.Dispose();
            plotFrames.Dispose();
        }
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
        [ReadOnly] public NativeArray<PlotFrame> plots;
        public YardLayout yard;
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

        private static float SegmentDistance(float2 p, float2 a, float2 b)
        {
            float2 ab = b - a;
            float t = saturate(dot(p - a, ab) / dot(ab, ab));
            return length(p - (a + ab * t));
        }

        /// <summary>
        /// The ground worn bare round the houses. <paramref name="core"/> is where it is dirt (0..1);
        /// <paramref name="halo"/> is the grass round it, trodden thin and dry. Each plot is worked out in
        /// its own space (see <see cref="PlotFrame"/>), so both rows of houses get the same yard.
        /// </summary>
        private void Wear(float2 w, float2 s, out float core, out float halo)
        {
            core = 0f;
            halo = 0f;
            bool sampled = false;
            float2 wobble = float2(0f, 0f);
            float patch = 0f;
            for (int i = 0; i < plots.Length; i++)
            {
                PlotFrame p = plots[i];
                float2 dw = w - p.origin;
                float2 l = float2(dot(dw, p.right), dot(dw, p.forward));
                // Only the yard, the gap beside the house and the door apron are of interest.
                if (l.x < yard.minX - 3f || l.x > yard.maxX + 4f || l.y < yard.doorZ - 4f || l.y > p.laneDist) continue;
                if (!sampled)
                {
                    wobble = 0.2f * float2(noise.snoise(s * 2.1f + 5f), noise.snoise(s * 2.1f + 19f));
                    patch = Fbm(s * 0.6f + 61f, 2);
                    sampled = true;
                }
                float2 q = l + wobble;

                // The path a visitor takes: from the lane down the gap between houses, then in to the side door.
                float2 a = float2(yard.pathX, p.laneDist - 1f);
                float2 b = float2(yard.pathX, yard.doorZ - 0.2f);
                float2 c = float2(yard.maxX + 0.35f, b.y);
                float dPath = min(SegmentDistance(q, a, b), SegmentDistance(q, b, c));
                // And the bare apron in front of the door itself.
                float dApron = length(q - float2(yard.maxX + 0.7f, yard.doorZ - 0.2f));

                // The lane-side yard: a strip swept bare against the front wall, breaking up into patches
                // further out. It stops short of the track, so a band of grass keeps the road distinct.
                float2 centre = float2((yard.minX + yard.maxX) * 0.5f, yard.frontZ + YardDepth * 0.5f);
                float2 extent = float2((yard.maxX - yard.minX) * 0.5f + 0.5f, YardDepth * 0.5f);
                float2 outside = abs(q - centre) - extent;
                float sd = length(max(outside, 0f)) + min(max(outside.x, outside.y), 0f);
                float cover = 1f - smoothstep(-0.3f, 0.5f, sd);
                float nearWall = smoothstep(1.4f, 0f, q.y - yard.frontZ);
                float bare = smoothstep(0f, 0.45f, patch + 0.10f + 0.60f * nearWall);

                core = max(core, max(1f - smoothstep(0.42f, 0.72f, dPath),
                                 max(1f - smoothstep(1.0f, 1.6f, dApron), cover * bare)));
                halo = max(halo, max(1f - smoothstep(0.42f, 1.6f, dPath),
                                 max(1f - smoothstep(1.0f, 2.6f, dApron), cover * 0.6f)));
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

            // The yards and paths round the houses are dirt too, so they share the track's dirt below.
            Wear(w, s, out float yardCore, out float yardHalo);
            float dirtness = max(t, yardCore);

            // Grass beside the track, and round the yards, is trodden thin and dry.
            g = lerp(g, g * DryTint, max(smoothstep(0.9f, 0f, d) * 0.5f, yardHalo * 0.5f) * (1f - dirtness) * amount);
            if (dirtness <= 0f) return g;

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

            return lerp(g, dirt, dirtness);
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
