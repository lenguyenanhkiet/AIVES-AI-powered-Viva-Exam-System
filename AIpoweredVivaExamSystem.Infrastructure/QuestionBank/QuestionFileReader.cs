using System.Text;
using System.Xml.Linq;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.QuestionBank;

namespace AIpoweredVivaExamSystem.Infrastructure.QuestionBank;

public sealed class QuestionFileReader : IQuestionFileReader
{
    private static readonly string[] Headers = ["SubjectCode", "Topic", "Content", "ExpectedAnswer", "BloomLevel", "Difficulty"];
    public IReadOnlyList<ImportRow> Read(byte[] content, string fileName)
    {
        try
        {
            var rows = Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".csv" => Csv(new UTF8Encoding(false, true).GetString(content).TrimStart('\uFEFF')),
                ".xlsx" => Xlsx(content),
                _ => throw new ResourceConflictException("Only UTF-8 CSV and XLSX are supported.")
            };
            if (rows.Count == 0 || !rows[0].SequenceEqual(Headers, StringComparer.OrdinalIgnoreCase))
                throw new ResourceConflictException("Headers must be: " + string.Join(",", Headers));
            return rows.Skip(1).Select((r, i) => (r, number: i + 2))
                .Where(x => x.r.Any(v => !string.IsNullOrWhiteSpace(v)))
                .Select(x => x.r.Length == Headers.Length
                    ? new ImportRow(x.number, x.r[0], x.r[1], x.r[2], x.r[3], x.r[4], x.r[5])
                    : throw new ResourceConflictException($"Row {x.number}: expected six columns.")).ToArray();
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException or DecoderFallbackException or FormatException or IndexOutOfRangeException or OverflowException or InvalidOperationException)
        { throw new ResourceConflictException("The file is malformed or is not a supported CSV/XLSX file."); }
    }

    private static List<string[]> Csv(string text)
    {
        var rows = new List<string[]>(); var cells = new List<string>(); var cell = new StringBuilder();
        var quoted = false; var closed = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') { quoted = false; closed = true; }
                else cell.Append(c);
            }
            else if (c is ',' or '\r' or '\n')
            {
                cells.Add(cell.ToString()); cell.Clear(); closed = false;
                if (c != ',')
                {
                    rows.Add(cells.ToArray()); cells.Clear();
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
            }
            else if (c == '"' && cell.Length == 0 && !closed) quoted = true;
            else if (closed || c == '"') throw new FormatException("Invalid CSV quoting.");
            else cell.Append(c);
            if (rows.Count > 1001 || cell.Length > 20000) throw new ResourceConflictException("Import exceeds row or cell limits.");
        }
        if (quoted) throw new FormatException("Unclosed CSV quote.");
        if (cell.Length > 0 || cells.Count > 0 || closed) { cells.Add(cell.ToString()); rows.Add(cells.ToArray()); }
        return rows;
    }

    private static List<string[]> Xlsx(byte[] content)
    {
        using var archive = OpenXmlReader.Open(content);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var workbook = archive.GetEntry("xl/workbook.xml") ?? throw new InvalidDataException();
        var sheet = OpenXmlReader.Read(workbook).Descendants(ns + "sheet").FirstOrDefault() ?? throw new InvalidDataException();
        var relations = OpenXmlReader.Read(archive.GetEntry("xl/_rels/workbook.xml.rels") ?? throw new InvalidDataException());
        var target = relations.Root!.Elements().Single(x => (string?)x.Attribute("Id") == (string?)sheet.Attribute(rel + "id"));
        if ((string?)target.Attribute("TargetMode") == "External") throw new InvalidDataException();
        var path = (string?)target.Attribute("Target") ?? throw new InvalidDataException();
        path = path.StartsWith('/') ? path.TrimStart('/') : "xl/" + path;
        var strings = archive.GetEntry("xl/sharedStrings.xml") is { } shared
            ? OpenXmlReader.Read(shared).Descendants(ns + "si").Select(x => string.Concat(x.Descendants(ns + "t").Select(t => t.Value))).ToArray() : [];
        var result = new List<string[]>();
        foreach (var row in OpenXmlReader.Read(archive.GetEntry(path) ?? throw new InvalidDataException()).Descendants(ns + "row"))
        {
            var rowNumber = (int?)row.Attribute("r") ?? result.Count + 1;
            if (rowNumber <= result.Count || rowNumber > 1001) throw new ResourceConflictException("XLSX row numbers must be ordered within 1–1001.");
            while (result.Count < rowNumber - 1) result.Add(Enumerable.Repeat("", 6).ToArray());
            var values = Enumerable.Repeat("", 6).ToArray();
            foreach (var cell in row.Elements(ns + "c"))
            {
                var reference = (string?)cell.Attribute("r") ?? throw new InvalidDataException();
                var column = 0;
                foreach (var ch in reference.TakeWhile(char.IsLetter)) column = checked(column * 26 + char.ToUpperInvariant(ch) - 'A' + 1);
                if (column is < 1 or > 6) throw new ResourceConflictException("XLSX must contain exactly the six template columns.");
                if (cell.Element(ns + "f") is not null) throw new ResourceConflictException("Formulas are not accepted; paste values into the import template.");
                var value = cell.Element(ns + "v")?.Value ?? "";
                if ((string?)cell.Attribute("t") == "s") value = strings[int.Parse(value)];
                if ((string?)cell.Attribute("t") == "inlineStr") value = string.Concat(cell.Descendants(ns + "t").Select(t => t.Value));
                if (value.Length > 20000) throw new ResourceConflictException("Cell exceeds limits.");
                values[column - 1] = value;
            }
            result.Add(values);
            if (result.Count > 1001) throw new ResourceConflictException("Import exceeds 1000 data rows.");
        }
        return result;
    }
}
