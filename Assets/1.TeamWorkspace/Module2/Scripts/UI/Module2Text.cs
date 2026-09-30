using UnityEngine;

/// <summary>
/// Every player-facing string in Module 2, in one place.
///
/// Each member reads from Resources/Localization/Module2Text.txt by key, falling back to the English
/// literal here. So the English build needs no table at all, a Lao build needs only the table filled
/// in, and a missing key degrades to readable English instead of a blank.
///
/// Keep these short. Two minutes per child, no VR experience and possibly low English means prose is
/// not read; and Lao does not separate words with spaces, so long lines wrap badly.
/// </summary>
public static class Module2Text
{
    static string L(string key, string english) => Module2Localization.Get(key, english);

    // Vitals (B4 specifies these two for M2)
    public static string EnergyLabel => L("hud.energy", "ENERGY");
    public static string Bites(int count, int limit) => L("hud.bites", "Bites") + "  " + count + " / " + limit;
    public static string BitesNearLimit => L("toast.bitesNearLimit", "Too many bites - get away from the swarm");

    // Status
    public static string DayTag => L("hud.dayTag", "Daytime - Aedes bite by day");
    public static string NightTag => L("hud.nightTag", "Evening - the swarm is settling");
    public static string Score(int score) => "Score " + score;
    public static string Sites(int cleared, int total) =>
        total > 0 ? "BREEDING SITES  " + cleared + " / " + total : "BREEDING SITES";

    // Objective
    public static string SiteJar => L("site.jar", "Water jar");
    public static string SiteContainer => L("site.container", "Open container");
    public static string SiteFishBowl => L("site.bowl", "Water bowl");
    public static string SiteGeneric => L("site.generic", "Standing water");
    public static string AllClear => L("hud.allClear", "Every breeding site dealt with");
    public static string Target(string what, float dist) => what + "  " + Mathf.RoundToInt(dist) + " m";

    // The 5 ປ / 5P. CMPE-validated wording (Marcombe, Jul 2026): Lao is the operative campaign
    // wording, English only a comprehension gloss. Lao lives in the table's Lao column - it will not
    // render under TMP's default LiberationSans, which has no Lao glyphs.
    public static string MeasureCover => L("measure.cover", "Cover");      // ປິດ
    public static string MeasureChange => L("measure.change", "Change");   // ປ່ຽນ
    public static string MeasureRelease => L("measure.release", "Release");// ປ່ອຍ
    public static string MeasureImprove => L("measure.improve", "Improve");// ປັບປຸງ
    public static string MeasurePractice => L("measure.practice", "Practice"); // ປະຕິບັດ

    // Prompts, each naming the measure it teaches.
    public static string PromptJar => L("prompt.jar", MeasureCover + " - grab the lid beside the jar");
    public static string PromptJarCarrying => L("prompt.jarCarrying", MeasureCover + " - hold the lid over the jar and let go");
    public static string PromptTip => L("prompt.tip", MeasureChange + " - tip the water out");
    public static string PromptFish => L("prompt.fish", MeasureRelease + " - drop a guppy in, the fish eat the larvae");
    public static string PromptGeneric => L("prompt.generic", MeasureImprove + " - get rid of the standing water");

    // Toasts
    public static string StartToast => L("toast.start", "Find the standing water - that is where they breed");
    public static string TrashBinned => L("toast.trash", "Rubbish binned - no more water traps");
    public static string NightFalls => L("toast.night", "Evening is coming - the light is going");
    public static string OneMinuteLeft => L("toast.oneMinute", "One minute left!");
    public static string ThirtySecondsLeft => L("toast.thirtySeconds", "30 seconds left!");
    public static string SwatterSpawned => L("toast.swatterOut", "Swatter is in front of you - grab it");
    public static string SwatterAlreadyOut => L("toast.swatterAlready", "You already have a swatter");
    public static string CreamSpawned => L("toast.creamOut", "Repellent is in front of you - grab it");
    public static string CreamAlreadyOut => L("toast.creamAlready", "You already have repellent");
    public static string RepellentOn(int seconds) => L("toast.repellentOn", "Repellent on - no bites for {0} s").Replace("{0}", seconds.ToString());
    public static string RepellentWornOff => L("toast.repellentOff", "Repellent has worn off");
    public static string RepellentStatus(int seconds) => L("hud.repellent", "Repellent") + " " + seconds + "s";
    public static string SiteCleared(string what, int cleared, int total) =>
        what + " dealt with  -  " + cleared + " / " + total;

    // Briefing
    public static string IntroKicker => L("intro.kicker", "BRIEFING");
    public static string IntroTitle => L("intro.title", "Clear the mosquito breeding sites");
    public static string IntroBody => L("intro.body",
        "Mosquitoes breed in still water.\n" +
        "Find it. Cover it. Tip it out.\n" +
        "\n" +
        "<color=#73D2FF>Follow the arrow.</color>");
    public static string IntroFooter => L("intro.footer", "Press A, trigger or grip to begin");

    // Debrief
    public static string EndKicker => L("end.kicker", "ROUND OVER");
    public static string EndFooter => L("end.footer", "Press A, trigger or grip to play again");

    public static string EndTitle(M2Manager.RoundEndReason reason)
    {
        switch (reason)
        {
            case M2Manager.RoundEndReason.AllSitesCleared: return L("end.titleClear", "House is clear!");
            case M2Manager.RoundEndReason.BitesReached: return L("end.titleBitten", "Driven off by the swarm");
            default: return L("end.titleTimeout", "Time's up");
        }
    }

    public static string EndSubtitle(M2Manager.RoundEndReason reason, int cleared, int total)
    {
        if (reason == M2Manager.RoundEndReason.BitesReached)
            return L("end.subBitten", "The swarm got you before the water was dealt with. Fewer breeding containers means fewer mosquitoes to bite you - protection alone does not reduce the swarm.");
        if (reason == M2Manager.RoundEndReason.AllSitesCleared)
            return L("end.subClear", "Every source of standing water is dealt with. Do this once a week and the mosquitoes never finish their life cycle.");
        if (total > 0 && cleared == 0)
            return L("end.subNone", "Nothing was dealt with, so every jar and container is still a nursery. Look for standing water, not for mosquitoes.");
        return L("end.subPartial", "Some standing water is still out there. One forgotten container is enough to keep a household supplied with mosquitoes.");
    }

    /// <summary>
    /// A8: Action Score and Community Outcome are two scores that can diverge, and squads are ranked
    /// by the Community Outcome - here the Source Reduction %, which is also the M2 -> M3 GAMA hook.
    /// Swatting and protection move the action score only; they cannot move source reduction at all.
    /// </summary>
    public static string EndStats(int score, int sitesCleared, int sitesTotal, int swatted, int trash, int sourceReductionPercent, int bites)
    {
        return "<b>" + L("end.statSourceReduction", "SOURCE REDUCTION") + "  " + sourceReductionPercent + "%</b>   "
               + "<size=70%>(" + sitesCleared + " / " + sitesTotal + ")</size>\n"
               + L("end.statAction", "Action score") + "  " + score + "\n"
               + "<size=80%>" + L("end.statBites", "Bites") + "  " + bites + "   "
               + L("end.statTrash", "Rubbish") + "  " + trash + "   "
               + L("end.statSwatted", "Swatted") + "  " + swatted + "</size>";
    }

    /// <summary>The point of the whole round: what the containers you left behind will do.</summary>
    public static string Consequence(int missed, int sourceReductionPercent, bool fromSimulation)
    {
        if (missed <= 0) return L("end.consequenceNone", "Nothing left breeding. Do this every week and the cycle never restarts.");

        // A5: Outbreak_Intensity = (100 - Source_Reduction_Percent) x Population_Density_Factor, and
        // below 50% the Critical triage cases in Module 3 double. So the warning is stated in the
        // terms the model actually uses rather than as an invented mosquito count.
        string containers = missed == 1 ? L("end.container", "container") : L("end.containers", "containers");
        string line = missed + " " + containers + " " + L("end.stillHolding", "still holding water") + ".";

        if (sourceReductionPercent < 50)
            line += "\n" + L("end.belowHalf", "Under half the sites cleared - the outbreak you hand on will be twice as severe.");

        return line;
    }

    static readonly string[] Facts =
    {
        "Aedes aegypti breeds in clean standing water - a jar, a bucket or a saucer under a plant pot.",
        "Aedes eggs can survive months in a dry container and hatch as soon as the rain returns.",
        "Guppies and other small fish eat mosquito larvae, which is why they are put in water jars.",
        "Emptying and scrubbing water containers once a week breaks the mosquito life cycle.",
        "Swatting adults barely dents the population - removing their breeding water is what works.",
        "A covered water jar is the single most effective change most households can make.",
    };

    public static string RandomFact()
    {
        int i = Random.Range(0, Facts.Length);
        return L("fact." + i, Facts[i]);
    }
}
