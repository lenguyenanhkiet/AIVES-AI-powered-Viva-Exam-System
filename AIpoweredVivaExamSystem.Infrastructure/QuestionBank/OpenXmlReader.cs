using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using AIpoweredVivaExamSystem.Application.Common;

namespace AIpoweredVivaExamSystem.Infrastructure.QuestionBank;

internal static class OpenXmlReader
{
    public static ZipArchive Open(byte[] content)
    {
        var archive = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        if (archive.Entries.Count > 10000 || archive.Entries.Sum(x => x.Length) > 100 * 1024 * 1024)
        { archive.Dispose(); throw new ResourceConflictException("Expanded Office file exceeds limits."); }
        return archive;
    }
    public static XDocument Read(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 30 * 1024 * 1024 });
        return XDocument.Load(reader);
    }
}
