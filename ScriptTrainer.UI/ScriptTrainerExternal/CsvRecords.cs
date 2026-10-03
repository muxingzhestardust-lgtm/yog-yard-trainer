using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScriptTrainerExternal;

internal static class CsvRecords
{
    // Preserve all characters inside quoted fields, including CRLF and empty lines.
    internal static IEnumerable<string[]> Read(TextReader reader)
    {
        List<string> fields = new List<string>();
        StringBuilder field = new StringBuilder();
        bool quoted = false;
        bool closedQuote = false;
        bool hasRecord = false;
        int next;
        while ((next = reader.Read()) != -1)
        {
            char c = (char)next;
            if (quoted)
            {
                if (c == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        field.Append('"');
                    }
                    else
                    {
                        quoted = false;
                        closedQuote = true;
                    }
                }
                else
                    field.Append(c);
            }
            else if (c == ',')
            {
                fields.Add(field.ToString());
                field.Length = 0;
                closedQuote = false;
                hasRecord = true;
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && reader.Peek() == '\n')
                    reader.Read();
                if (hasRecord)
                {
                    fields.Add(field.ToString());
                    yield return fields.ToArray();
                }
                fields.Clear();
                field.Length = 0;
                closedQuote = false;
                hasRecord = false;
            }
            else if (c == '"' && field.Length == 0 && !closedQuote)
            {
                quoted = true;
                hasRecord = true;
            }
            else
            {
                if (closedQuote || c == '"')
                    throw new FormatException("CSV field contains an unexpected quote or characters after a closing quote.");
                field.Append(c);
                hasRecord = true;
            }
        }
        if (quoted)
            throw new FormatException("CSV ends inside a quoted field.");
        if (hasRecord)
        {
            fields.Add(field.ToString());
            yield return fields.ToArray();
        }
    }
}
