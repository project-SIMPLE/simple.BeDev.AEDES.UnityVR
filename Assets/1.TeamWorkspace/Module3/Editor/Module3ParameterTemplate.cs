using System.IO;
using Aedes.Module3.Sim;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Regenerates the parameter CSV that ships in Resources and that the GAMA modellers work from.
///
/// This exists because the file drifted once already: a default changed in Module3Config, the CSV
/// kept the old value, and the file the build loads at startup no longer passed its own
/// validation. Regenerating by hand is exactly the step that gets forgotten, so it is a menu item
/// and a test rather than a note in a document.
/// </summary>
public static class Module3ParameterTemplate
{
    public const string Path = "Assets/Resources/Module3/Module3Parameters.csv";

    [MenuItem("Module 3/Regenerate parameter template")]
    public static void Regenerate()
    {
        string csv = ParameterCsv.WriteTemplate(new ParameterSet());

        var check = ParameterCsv.Parse(csv, "regenerated");
        if (!check.IsValid)
        {
            // The defaults in Module3Config no longer satisfy the rules in ParameterCsv. Writing
            // the file anyway would just move the failure to startup.
            Debug.LogError("Module 3: the built-in defaults do not pass validation, so the template was "
                           + "NOT written:\n  " + string.Join("\n  ", check.Errors));
            return;
        }

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        File.WriteAllText(Path, csv);
        AssetDatabase.ImportAsset(Path);
        Debug.Log($"Module 3: parameter template regenerated at {Path} "
                  + $"({ParameterCsv.Descriptors.Count} keys). Hand this to the GAMA modellers.");
    }
}
