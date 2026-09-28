using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Module 2's string table.
///
/// Separate from the framework's LocalizationManager on purpose: that one parses its CSV with a bare
/// Split(','), so no value may contain a comma, and it currently holds three keys for English,
/// Vietnamese and French - no Lao. This reads a tab-separated file instead, so copy can contain
/// commas, and its columns are the languages this module actually ships in.
///
/// The table lives at Resources/Localization/Module2Text.tsv. Every lookup falls back to the English
/// string compiled into Module2Text, so a missing key or a missing file degrades to readable English
/// rather than to blanks.
///
/// Note for a Lao build: TMP's default font (LiberationSans) has no Lao glyphs. Set
/// Module2HUD.fontOverride to a Lao-capable font asset, and keep strings short - Lao does not put
/// spaces between words, so TMP's word wrapping has nothing to break on in a long line.
/// </summary>
public static class Module2Localization
{
    const string ResourcePath = "Localization/Module2Text";
    const string DefaultLanguage = "English";
    public const string LanguagePrefKey = "AEDES.Language";

    static Dictionary<string, string[]> _rows;   // key -> value per column
    static string[] _languages;                  // column headers, index 0 is the key column
    static int _column = -1;
    static bool _loaded;

    /// <summary>Language column in use. Falls back to English when the name is not a column.</summary>
    public static string Language { get; private set; } = DefaultLanguage;

    public static void SetLanguage(string language)
    {
        Language = string.IsNullOrWhiteSpace(language) ? DefaultLanguage : language.Trim();
        _column = ResolveColumn(Language);
        PlayerPrefs.SetString(LanguagePrefKey, Language);
    }

    /// <summary>Localised value for a key, or the supplied English fallback.</summary>
    public static string Get(string key, string fallback)
    {
        EnsureLoaded();

        if (_rows == null || _column < 1 || string.IsNullOrEmpty(key)) return fallback;
        if (!_rows.TryGetValue(key, out var values)) return fallback;
        if (_column >= values.Length) return fallback;

        string value = values[_column];
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Replace("\\n", "\n");
    }

    static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        var asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            Debug.Log($"[Module2Localization] No table at Resources/{ResourcePath} - using built-in English.");
            return;
        }

        _rows = new Dictionary<string, string[]>();
        var lines = asset.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("#")) continue;
            var cells = lines[i].Split('\t');
            for (int c = 0; c < cells.Length; c++) cells[c] = cells[c].Trim();

            if (_languages == null) { _languages = cells; continue; }
            if (cells.Length < 2 || string.IsNullOrEmpty(cells[0])) continue;
            _rows[cells[0]] = cells;
        }

        string stored = PlayerPrefs.GetString(LanguagePrefKey, DefaultLanguage);
        Language = string.IsNullOrWhiteSpace(stored) ? DefaultLanguage : stored;
        _column = ResolveColumn(Language);

        Debug.Log($"[Module2Localization] {_rows.Count} key(s), columns: {string.Join(", ", _languages)} - using '{Language}'.");
    }

    static int ResolveColumn(string language)
    {
        EnsureLoaded();
        if (_languages == null) return -1;

        for (int i = 1; i < _languages.Length; i++)
            if (string.Equals(_languages[i], language, System.StringComparison.OrdinalIgnoreCase)) return i;

        for (int i = 1; i < _languages.Length; i++)
            if (string.Equals(_languages[i], DefaultLanguage, System.StringComparison.OrdinalIgnoreCase)) return i;

        return -1;
    }
}
