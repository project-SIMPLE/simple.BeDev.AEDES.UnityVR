using System;
using System.IO;
using Aedes.Module3.Sim;
using UnityEngine;

/// <summary>
/// Writes the squad's Field Journal to disk when the session ends.
///
/// Section 9 makes the Analyst's record how the squad plays rather than paperwork bolted on
/// afterwards, and section 5 has the facilitator read the closing answers aloud. On a Quest the
/// file lands in the app's own storage and comes off with `adb pull`; in the editor it goes in
/// the project's persistent data path.
/// </summary>
public class M3JournalWriter : MonoBehaviour
{
    [SerializeField] private bool writeOnSessionEnd = true;

    public string LastWrittenPath { get; private set; }

    // Same race as M3Hud: when this OnEnable ran before M3Session's Awake it subscribed to nothing
    // and the journal was never written. Start runs after every Awake, so try again there.
    private M3Session subscribedTo;

    private void OnEnable() => Subscribe();
    private void Start() => Subscribe();

    private void Subscribe()
    {
        var s = M3Session.Instance;
        if (s == null || subscribedTo == s) return;
        s.OnSessionFinished += Write;
        subscribedTo = s;
    }

    private void OnDisable()
    {
        if (subscribedTo == null) return;
        subscribedTo.OnSessionFinished -= Write;
        subscribedTo = null;
    }

    private void Write(HandoverBrief finalBrief)
    {
        if (!writeOnSessionEnd) return;

        var s = M3Session.Instance;
        if (s == null || s.Session == null) return;

        var map = OutbreakMap.Build(s.Neighbourhood);
        var comparison = new CounterfactualComparison
        {
            AsPlayed = map,
            HadPatientZeroBeenCovered = OutbreakMap.Build(s.BuildCounterfactual().Neighbourhood),
        };

        var export = FieldJournal.Build(s.Neighbourhood, s.Session, comparison);

        try
        {
            string dir = Path.Combine(Application.persistentDataPath, "Module3Journals");
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir,
                $"journal_{DateTime.Now:yyyyMMdd_HHmmss}_seed{s.Seed}.txt");
            File.WriteAllText(path, export.ToPlainText());

            LastWrittenPath = path;
            Debug.Log($"Module 3: Field Journal written to {path}");
        }
        catch (Exception e)
        {
            // Never let a failed write take the debrief down with it - the class is still in
            // the room and the screen still has to show them their outbreak.
            Debug.LogError($"Module 3: could not write the Field Journal: {e.Message}");
        }
    }
}
