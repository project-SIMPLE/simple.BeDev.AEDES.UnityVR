using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    // The path to the CSV file within any "Resources" folder.
    private const string CsvFilePath = "Localization/LocalizationData";
    private const string ColorTextsPath = "Localization/ColorTexts";
    private const string LanguagePref = "Language";

    // Data structure: Dictionary<Language, Dictionary<Key, Value>>
    private Dictionary<string, Dictionary<string, string>> localizedData;
    [SerializeField] private string currentLanguage = "English";

    public delegate void LanguageChanged();
    public static event LanguageChanged OnLanguageChanged;

    private List<ColorText> colorTexts = new List<ColorText>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadLocalizationData();
            SetLanguage(PlayerPrefs.GetString(LanguagePref, currentLanguage));
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadLocalizationData()
    {
        localizedData = new Dictionary<string, Dictionary<string, string>>();

        // Load the TextAsset from the Resources folder.
        TextAsset csvFile = Resources.Load<TextAsset>(CsvFilePath);

        if (csvFile == null)
        {
            Debug.LogError($"Localization CSV not found at 'Resources/{CsvFilePath}'.");
            return;
        }

        ColorTexts autoColors = Resources.Load<ColorTexts>(ColorTextsPath);
        if (autoColors != null)
        {
            colorTexts = autoColors.colorTexts;
        }

        List<List<string>> rows = ParseCsv(csvFile.text);
        if (rows.Count < 2) return;

        // The first row names the languages: Key, English, Lao, ...
        List<string> headers = rows[0];
        for (int i = 1; i < headers.Count; i++)
        {
            string language = headers[i].Trim();
            if (language.Length == 0) continue;
            localizedData[language] = new Dictionary<string, string>();
        }

        for (int r = 1; r < rows.Count; r++)
        {
            List<string> values = rows[r];
            if (values.Count == 0) continue;

            string key = values[0].Trim();
            if (key.Length == 0 || key.StartsWith("#")) continue; // blank row or comment

            for (int c = 1; c < values.Count && c < headers.Count; c++)
            {
                string language = headers[c].Trim();
                if (language.Length == 0 || !localizedData.ContainsKey(language)) continue;

                localizedData[language][key] = ApplyAutoColors(values[c].Trim());
            }
        }

        Debug.Log($"Localization data loaded: {rows.Count - 1} keys, {localizedData.Count} languages.");
    }

    /// <summary>
    /// RFC-4180 style CSV parse over the whole file at once.
    /// Values may contain commas and line breaks as long as they are quoted, and a literal
    /// double quote is written as "". Parsing the whole text (rather than splitting on newlines
    /// first) is what makes multi-line values safe - Module 3 dialogue needs both.
    /// </summary>
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        bool rowHasContent = false;

        void EndField()
        {
            row.Add(field.ToString());
            field.Clear();
        }

        void EndRow()
        {
            EndField();
            rows.Add(row);
            row = new List<string>();
            rowHasContent = false;
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++; // skip the escaped quote
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    rowHasContent = true;
                    break;
                case ',':
                    EndField();
                    rowHasContent = true;
                    break;
                case '\r':
                    break; // handled by the \n that follows, or ignored on its own
                case '\n':
                    if (rowHasContent || field.Length > 0) EndRow();
                    break;
                default:
                    field.Append(c);
                    if (c != ' ') rowHasContent = true;
                    break;
            }
        }

        if (rowHasContent || field.Length > 0) EndRow();

        // A UTF-8 BOM on the first cell would otherwise become part of the first key.
        if (rows.Count > 0 && rows[0].Count > 0)
        {
            rows[0][0] = rows[0][0].TrimStart('﻿');
        }

        return rows;
    }

    private string ApplyAutoColors(string originalText)
    {
        if (string.IsNullOrEmpty(originalText) || colorTexts.Count == 0)
            return originalText;

        string modifiedText = originalText;

        foreach (var text in colorTexts)
        {
            if (string.IsNullOrEmpty(text.text)) continue;

            string hexColor = "#" + ColorUtility.ToHtmlStringRGB(text.color);
            string pattern = Regex.Escape(text.text);

            // $0 re-inserts the matched text verbatim, so the original casing survives.
            modifiedText = Regex.Replace(modifiedText, pattern,
                $"<color={hexColor}>$0</color>", RegexOptions.IgnoreCase);
        }

        return modifiedText;
    }

    public void SetLanguage(string languageName)
    {
        if (localizedData != null && localizedData.ContainsKey(languageName))
        {
            currentLanguage = languageName;
            PlayerPrefs.SetString(LanguagePref, currentLanguage);
            OnLanguageChanged?.Invoke();
            Debug.Log($"Language changed to: {currentLanguage}");
        }
        else
        {
            Debug.LogWarning($"Language '{languageName}' not found in localization data.");
        }
    }

    public string GetLanguage()
    {
        return currentLanguage;
    }

    public IEnumerable<string> GetLanguages()
    {
        return localizedData != null ? (IEnumerable<string>)localizedData.Keys : new List<string>();
    }

    public bool HasKey(string key)
    {
        return localizedData != null
            && localizedData.TryGetValue(currentLanguage, out var table)
            && table.TryGetValue(key, out string value)
            && value.Length > 0;
    }

    /// <summary>Keys present in English but not yet translated into the current language.</summary>
    public List<string> UntranslatedKeys()
    {
        var missing = new List<string>();
        if (localizedData == null || !localizedData.TryGetValue(FallbackLanguage, out var english)) return missing;

        localizedData.TryGetValue(currentLanguage, out var table);
        foreach (var pair in english)
        {
            if (table == null || !table.TryGetValue(pair.Key, out string v) || v.Length == 0) missing.Add(pair.Key);
        }
        return missing;
    }

    /// <summary>
    /// The language used when the current one has no value for a key. Translation lands
    /// incrementally - Module 3's dialogue is written in English first and the Lao wording for
    /// the warning signs is being agreed with the NUOL team - so an untranslated row should show
    /// the English sentence rather than a raw key on a headset in a classroom.
    /// </summary>
    private const string FallbackLanguage = "English";

    public string GetLocalizedValue(string key)
    {
        if (HasKey(key))
        {
            return localizedData[currentLanguage][key];
        }

        if (currentLanguage != FallbackLanguage
            && localizedData != null
            && localizedData.TryGetValue(FallbackLanguage, out var fallback)
            && fallback.TryGetValue(key, out string english)
            && english.Length > 0)
        {
            return english;
        }

        Debug.LogWarning($"Localization key '{key}' not found for language '{currentLanguage}'.");
        return key;
    }

    /// <summary>
    /// Substitutes {0}, {1}, ... into the localized string. Module 3 needs this for lines like
    /// "she has been hot for {0} days".
    /// </summary>
    public string GetLocalizedValue(string key, params object[] args)
    {
        string value = GetLocalizedValue(key);
        return args == null || args.Length == 0 ? value : string.Format(value, args);
    }
}
