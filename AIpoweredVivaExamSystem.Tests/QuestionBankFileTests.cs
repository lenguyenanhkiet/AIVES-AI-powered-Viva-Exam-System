using System.IO.Compression;
using System.Text;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.QuestionBank;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Infrastructure.QuestionBank;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public class QuestionBankFileTests
{
    [Fact]
    public void CsvHandlesBomQuotedCommasMultilineAndEscapedQuotes()
    {
        var text = "\uFEFFSubjectCode,Topic,Content,ExpectedAnswer,BloomLevel,Difficulty\r\nPRN222,,\"Explain, MVC\nwith \"\"views\"\"\",Answer,Understand,Medium\r\n";
        var row = Assert.Single(new QuestionFileReader().Read(Encoding.UTF8.GetBytes(text), "test.csv"));
        Assert.Equal("Explain, MVC\nwith \"views\"", row.Content);
        Assert.Equal(2, row.RowNumber);
    }
    [Theory]
    [InlineData("Subject,Content\nPRN222,Question")]
    [InlineData("SubjectCode,Topic,Content,ExpectedAnswer,BloomLevel,Difficulty\nPRN222,,\"unterminated")]
    public void InvalidCsvDoesNotSilentlyImport(string text) => Assert.Throws<ResourceConflictException>(() =>
        new QuestionFileReader().Read(Encoding.UTF8.GetBytes(text), "test.csv"));

    [Fact]
    public void XlsxResolvesWorkbookSheetAndPreservesSparseCells()
    {
        var bytes = Archive(new Dictionary<string, string>
        {
            ["xl/workbook.xml"] = "<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Questions' sheetId='1' r:id='sheetRel'/></sheets></workbook>",
            ["xl/_rels/workbook.xml.rels"] = "<Relationships><Relationship Id='sheetRel' Target='worksheets/questions.xml'/></Relationships>",
            ["xl/worksheets/questions.xml"] = "<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData><row>" +
                string.Concat(new[] { "SubjectCode", "Topic", "Content", "ExpectedAnswer", "BloomLevel", "Difficulty" }.Select((v, i) => $"<c r='{(char)('A'+i)}1' t='inlineStr'><is><t>{v}</t></is></c>")) +
                "</row><row><c r='A2' t='inlineStr'><is><t>PRN222</t></is></c><c r='C2' t='inlineStr'><is><t>Explain MVC</t></is></c><c r='E2' t='inlineStr'><is><t>Understand</t></is></c><c r='F2' t='inlineStr'><is><t>Medium</t></is></c></row></sheetData></worksheet>"
        });
        var row = Assert.Single(new QuestionFileReader().Read(bytes, "test.xlsx"));
        Assert.Equal("", row.Topic); Assert.Equal("Explain MVC", row.Content);
    }
    [Fact]
    public void CitationRejectsInventedQuote()
    {
        var chunk = new DocumentChunk(Guid.NewGuid(), 2, "MVC separates model, view and controller.");
        Assert.Throws<DomainValidationException>(() => new QuestionCitation(Guid.NewGuid(), chunk, "Invented source"));
        Assert.Equal("model, view", new QuestionCitation(Guid.NewGuid(), chunk, "model, view").Quote);
    }
    [Fact]
    public void RetrievalRanksRelevantTextAndChunkingPreservesOverlap()
    {
        var irrelevant = new DocumentChunk(Guid.NewGuid(), 1, "Database isolation");
        var relevant = new DocumentChunk(Guid.NewGuid(), 2, "MVC controllers return views");
        Assert.Equal(relevant.Id, DocumentService.Retrieve([irrelevant, relevant], "MVC controllers", 1)[0].Id);
        var chunks = DocumentService.Chunk(new string('a', 4000)).ToArray();
        Assert.Equal(3, chunks.Length); Assert.All(chunks, c => Assert.InRange(c.Length, 1, 2000));
        Assert.Equal(chunks[0][^200..], chunks[1][..200]);
    }
    [Fact]
    public void PdfAndUnsupportedDocumentsAreValidated()
    {
        Assert.Throws<ResourceConflictException>(() => new DocumentTextExtractor().Extract("not a pdf"u8.ToArray(), "fake.pdf"));
        Assert.Throws<ResourceConflictException>(() => new DocumentTextExtractor().Extract([1, 2, 3], "macro.docm"));
    }
    [Fact]
    public void PptxUsesPresentationOrderForCitationSlideNumbers()
    {
        var bytes = Archive(new Dictionary<string,string>
        {
            ["ppt/presentation.xml"] = "<p:presentation xmlns:p='http://schemas.openxmlformats.org/presentationml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><p:sldIdLst><p:sldId id='256' r:id='second'/><p:sldId id='257' r:id='first'/></p:sldIdLst></p:presentation>",
            ["ppt/_rels/presentation.xml.rels"] = "<Relationships><Relationship Id='first' Target='slides/slide1.xml'/><Relationship Id='second' Target='slides/slide2.xml'/></Relationships>",
            ["ppt/slides/slide1.xml"] = "<slide xmlns:a='http://schemas.openxmlformats.org/drawingml/2006/main'><a:t>Last slide</a:t></slide>",
            ["ppt/slides/slide2.xml"] = "<slide xmlns:a='http://schemas.openxmlformats.org/drawingml/2006/main'><a:t>First slide</a:t></slide>"
        });
        var pages = new DocumentTextExtractor().Extract(bytes, "lecture.pptx");
        Assert.Equal("First slide", pages[0].Text); Assert.Equal(1, pages[0].Number);
        Assert.Equal("Last slide", pages[1].Text); Assert.Equal(2, pages[1].Number);
    }
    public static byte[] Archive(Dictionary<string, string> files)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            foreach (var (name, text) in files) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(text); }
        return stream.ToArray();
    }
}
