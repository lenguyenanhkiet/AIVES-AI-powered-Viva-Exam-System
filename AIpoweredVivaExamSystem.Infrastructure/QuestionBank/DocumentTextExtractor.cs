using System.Text.RegularExpressions;
using System.Xml.Linq;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.QuestionBank;
using UglyToad.PdfPig;

namespace AIpoweredVivaExamSystem.Infrastructure.QuestionBank;

public sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    public IReadOnlyList<ExtractedPage> Extract(byte[] content, string fileName)
    {
        try
        {
            switch (Path.GetExtension(fileName).ToLowerInvariant())
            {
                case ".pdf":
                    if (!content.AsSpan().StartsWith("%PDF-"u8)) throw new InvalidDataException();
                    using (var pdf = PdfDocument.Open(content))
                    {
                        if (pdf.NumberOfPages > 500) throw new ResourceConflictException("Maximum 500 pages/slides per document.");
                        return pdf.GetPages().Select(p => new ExtractedPage(p.Number,
                            string.Join(" ", p.GetWords().Select(w => w.Text)))).ToArray();
                    }
                case ".pptx":
                    using (var archive = OpenXmlReader.Open(content))
                    {
                        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
                        XNamespace p = "http://schemas.openxmlformats.org/presentationml/2006/main";
                        XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                        var presentation = OpenXmlReader.Read(archive.GetEntry("ppt/presentation.xml") ?? throw new InvalidDataException());
                        var relations = OpenXmlReader.Read(archive.GetEntry("ppt/_rels/presentation.xml.rels") ?? throw new InvalidDataException())
                            .Root!.Elements().Where(e => (string?)e.Attribute("TargetMode") != "External")
                            .ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);
                        var slides = presentation.Descendants(p + "sldId").Select(e =>
                        {
                            var target = relations[(string)e.Attribute(r + "id")!];
                            var path = target.StartsWith('/') ? target.TrimStart('/') : "ppt/" + target;
                            return archive.GetEntry(path) ?? throw new InvalidDataException();
                        }).ToArray();
                        if (slides.Length is 0 or > 500) throw new ResourceConflictException("Presentation requires 1–500 slides.");
                        return slides.Select((e, i) => new ExtractedPage(i + 1,
                            string.Join(" ", OpenXmlReader.Read(e).Descendants(a + "t").Select(t => t.Value)))).ToArray();
                    }
                default: throw new ResourceConflictException("Only PDF and PPTX documents are supported.");
            }
        }
        catch (ResourceConflictException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        { throw new ResourceConflictException("Could not read this document. Check that it is a valid, unencrypted PDF/PPTX."); }
    }
}
