using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// The Module 3 end of the SIMPLE bridge. Its only job today is to take delivery of the
/// simulation parameter CSV that GAMA generates, including mid-session, and hand it to
/// <see cref="Module3Parameters"/>.
///
/// Subclassing rather than editing SimulationManager is deliberate - see CLAUDE.md section 9,
/// "Extend, don't modify", which is what keeps pulling upstream template changes possible.
///
/// GAMA sends it as an ordinary json_output whose first key is the one below:
///
///     {"type": "json_output",
///      "contents": {"module3_params": "key,value\nemergence_per_container_per_day,2.4\n..."}}
///
/// Only the keys that changed need to be present; a partial file is layered onto whatever is
/// already in force. What happens next - applied from the next day, held for the next session,
/// or rejected whole - is decided by Module3Parameters, not here.
///
/// NOTE: only one SimulationManager subclass may sit on a GameObject. Unity calls Awake on
/// disabled components too, and SimulationManager.Awake assigns the static Instance, so two of
/// them fight over it in an order that depends on nothing you can see. Both Module 1 and
/// Module 2 have this problem (CLAUDE.md section 10.4); Module 3 should not inherit it.
/// </summary>
public class Module3GamaLink : SimulationManager
{
    protected override void ManageOtherMessages(string content)
    {
        base.ManageOtherMessages(content);

        if (string.IsNullOrEmpty(content)) return;
        if (content.IndexOf(Module3Parameters.GamaMessageKey, System.StringComparison.Ordinal) < 0) return;

        string csv = ExtractCsv(content);
        if (string.IsNullOrEmpty(csv))
        {
            Debug.LogWarning($"Module 3: a '{Module3Parameters.GamaMessageKey}' message arrived but carried no CSV.");
            return;
        }

        if (Module3Parameters.Instance == null)
        {
            Debug.LogWarning("Module 3: parameters arrived from GAMA before Module3Parameters existed in the scene. "
                             + "Put the Module3Parameters component in the first scene that loads.");
            return;
        }

        Module3Parameters.Instance.ReceiveCsv(csv, "GAMA");
    }

    private static string ExtractCsv(string content)
    {
        try
        {
            JObject payload = JObject.Parse(content);
            JToken token = payload[Module3Parameters.GamaMessageKey];
            if (token == null) return null;

            // Usually a single string. A GAMA list of rows is accepted too, because writing the
            // file out line by line is the obvious thing to do at the other end and it would be
            // unkind to make that a failure.
            if (token.Type == JTokenType.Array)
            {
                var rows = new System.Text.StringBuilder();
                foreach (JToken line in token) rows.AppendLine(line.ToString());
                return rows.ToString();
            }

            return token.ToString();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Module 3: could not read the parameter payload from GAMA: {e.Message}");
            return null;
        }
    }
}
