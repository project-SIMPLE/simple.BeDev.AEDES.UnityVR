using System.Collections.Generic;
using System.Text;

namespace Aedes.Module3.Sim
{
    /// <summary>
    /// A minimal RFC-4180 reader. The Sim assembly references nothing - not even UnityEngine -
    /// so it cannot borrow the project's other CSV parser, and 60 lines is a cheaper price than
    /// a dependency that would stop the model being testable outside the editor.
    /// </summary>
    public static class Csv
    {
        public static List<List<string>> Parse(string text)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(text)) return rows;

            var row = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;
            bool started = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else field.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '"': inQuotes = true; started = true; break;
                    case ',': row.Add(field.ToString()); field.Clear(); started = true; break;
                    case '\r': break;
                    case '\n':
                        if (started || field.Length > 0)
                        {
                            row.Add(field.ToString()); field.Clear();
                            rows.Add(row); row = new List<string>(); started = false;
                        }
                        break;
                    default:
                        field.Append(c);
                        if (c != ' ') started = true;
                        break;
                }
            }

            if (started || field.Length > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }

            if (rows.Count > 0 && rows[0].Count > 0) rows[0][0] = rows[0][0].TrimStart('﻿');
            return rows;
        }

        public static string Quote(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            bool needs = value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0
                      || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;
            if (!needs) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
