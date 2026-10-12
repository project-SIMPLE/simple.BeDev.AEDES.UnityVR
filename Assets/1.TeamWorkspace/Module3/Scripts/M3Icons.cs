using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Module 3's icon set: white line icons, tinted by the Image that shows them.
///
/// The PNGs under Module3/Resources/M3Icons are generated - edit Tools/Module3/icons/icons.py and
/// run its rasterize.sh rather than painting over them. The names here are the file names.
///
/// Icons carry the game so the words do not have to: a 14-year-old in a headset reads a picture of
/// a net faster than "Put up the net", and an icon needs no Lao translation. The sentences are still
/// there, as the caption under whatever the Pilot points at (M3PersonPanel).
/// </summary>
public static class M3Icons
{
    public const string Folder = "M3Icons/";

    // What the HUD counts.
    public const string Clock = "clock", Net = "net", Day = "day";

    // What you notice about a person.
    public const string Fine = "fine", Fever = "fever", FeverDown = "feverDown", Bleeding = "bleeding",
                        Vomiting = "vomiting", NoWater = "noWater", NetUp = "netUp", HealthCentre = "healthCentre";

    // What you can do.
    public const string Water = "water", Rest = "rest", Repellent = "repellent", CloseAtDusk = "closeAtDusk",
                        Fan = "fan", Screen = "screen";

    // Feedback, the handover and the debrief.
    public const string Check = "check", Close = "close", Arrow = "arrow", House = "house",
                        HouseUnvisited = "houseUnvisited", Person = "person", PersonNew = "personNew",
                        People = "people", HiddenPerson = "hiddenPerson", Eye = "eye", Calendar = "calendar",
                        Watch = "watch", Headset = "headset", Talk = "talk", Shield = "shield", NoNet = "noNet",
                        Mosquito = "mosquito";

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    /// <summary>The sprite called <paramref name="name"/>; null, with one warning, if there is none.</summary>
    public static Sprite Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (cache.TryGetValue(name, out var sprite)) return sprite;
        sprite = Resources.Load<Sprite>(Folder + name);
        if (sprite == null) Debug.LogWarning($"Module 3: no icon at Resources/{Folder}{name}");
        cache[name] = sprite;
        return sprite;
    }
}
