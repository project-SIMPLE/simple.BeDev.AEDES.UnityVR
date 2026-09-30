using UnityEngine;

/// <summary>
/// Font sizes worked out from what the reader sees rather than from canvas units.
///
/// A world-space canvas is laid out in its own units and then scaled to metres, so "22" means a
/// different thing on every panel. What decides whether text can be read in a headset is the angle
/// its letters subtend at the eye, and that follows from the canvas scale and the reading distance.
/// The size is the font's em (TextMeshPro's font size), not the height of a capital: in the Lao
/// SomVang font a capital is 0.62 of it, so text sized for 1.5 degrees has capitals of about 0.9.
/// </summary>
public static class M3TextSize
{
    /// <summary>Smallest text worth reading in the headset: the em subtends this many degrees.</summary>
    public const float MinDegrees = 1.5f;

    /// <summary>The distance at which a hand-held panel is read, in metres.</summary>
    public const float ReadingMetres = 1.5f;

    /// <summary>
    /// The font size, in canvas units, whose em subtends <paramref name="degrees"/> at
    /// <paramref name="metres"/> on a canvas laid out at <paramref name="unitsPerMetre"/>.
    /// </summary>
    public static float ForAngle(float degrees, float metres, float unitsPerMetre)
        => Mathf.Tan(degrees * Mathf.Deg2Rad) * metres * unitsPerMetre;

    /// <summary>The angle, in degrees, that <paramref name="fontSize"/> subtends at <paramref name="metres"/>.</summary>
    public static float Angle(float fontSize, float metres, float unitsPerMetre)
        => Mathf.Atan(fontSize / unitsPerMetre / metres) * Mathf.Rad2Deg;

    /// <summary>
    /// The smallest font size that meets <see cref="MinDegrees"/>. Rounded up, so it never lands a
    /// fraction under.
    /// </summary>
    public static float Floor(float unitsPerMetre, float metres = ReadingMetres)
        => Mathf.Ceil(ForAngle(MinDegrees, metres, unitsPerMetre));

    /// <summary><paramref name="fontSize"/>, raised to <see cref="Floor"/> if it is under it.</summary>
    public static float AtLeast(float fontSize, float unitsPerMetre, float metres = ReadingMetres)
        => Mathf.Max(fontSize, Floor(unitsPerMetre, metres));
}
