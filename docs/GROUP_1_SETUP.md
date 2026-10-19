# Group 1: question bank setup and acceptance

## Database and first administrator

Apply the `CompleteQuestionBank` migration to your chosen database after reviewing it:

```powershell
$env:ConnectionStrings__DefaultConnection = '<your SQL Server connection>'
dotnet ef database update --project AIpoweredVivaExamSystem.Persistence --startup-project AIpoweredVivaExamSystem.Web
```

The Web host does not apply migrations automatically. Migration generation uses a design-time factory and needs no JWT/AI secrets. Install the `dotnet-ef` 10.0.12 tool locally if it is unavailable.

Existing users receive `STUDENT`, with no automatic privilege escalation. A database administrator should explicitly designate the first administrator for an existing, verified account:

```sql
UPDATE Users SET Role = 'ADMIN' WHERE Email = N'<verified administrator email>' AND DeletedAt IS NULL;
```

Sign in and use **Phân quyền & phân môn** (`/Access`) to promote lecturers and assign subjects. Only administrators create/edit/delete subjects. Lecturers manage topics in assigned subjects and their own questions, rubrics and documents. Existing questions whose `CreatedBy` is null remain available to administrators; ownership must be assigned intentionally by the database administrator if lecturers should maintain them. No existing questions are deleted or reassigned by this migration.

## Import CSV/XLSX

Open `/QuestionImports`, download the CSV template, or create an XLSX with these columns on its first sheet:

```text
SubjectCode,Topic,Content,ExpectedAnswer,BloomLevel,Difficulty
```

- `SubjectCode`: an active subject assigned to the lecturer.
- `Topic`: optional existing topic in that subject.
- `Content`: required, at most 4000 characters; `ExpectedAnswer`: optional, at most 8000.
- `BloomLevel`: Remember, Understand, Apply or Analyze (or enum values 1–4).
- `Difficulty`: Easy, Medium or Hard (or enum values 1–3).
- UTF-8 CSV supports quoted commas, quotes and multiline cells. XLSX supports shared strings and inline strings; formulas are rejected.

Limits: 5 MB and 1000 data rows. Preview reports row errors; confirmation is available only when every row is valid. Preview belongs to the current user, expires after 15 minutes and is held in process memory (restart requires another upload). Confirmation revalidates permissions and rows. Questions are Draft with Imported provenance. SHA-256 receipts and a unique index prevent the same file from creating duplicate questions for the same user; concurrent duplicate submissions return a conflict without partial writes. Different files with the same text are separate imports; deduplication is per-file, not semantic.

## Documents and RAG generation

Open `/Documents` and upload a text-based PDF or PPTX to an assigned active subject. Limits: 20 MB, 500 pages/slides, 2000 chunks, and 100 MB expanded Office archive. Scanned PDFs need OCR before upload. The app retains extracted text and document metadata in SQL; it does not retain or serve the original binary. Text is chunked at 2000 characters with 200 characters of overlap, preserving page/slide numbers. Reuploading identical content in the same ownership/subject scope reuses the document.

Configure the server (never commit a real key):

```powershell
$env:Ai__ApiKey = '<your provider key>'
$env:Ai__Model = '<a Chat Completions model supporting JSON mode>'
```

For Docker, set `AI_API_KEY` and `AI_MODEL` in your local `.env`. The UI explicitly identifies that relevant source text is sent to OpenAI. Generation requires an existing uploaded document, an instruction, Bloom level, difficulty, optional topic and 1–10 questions. Retrieval currently ranks local chunks by query-term overlap; it is lexical RAG, with no embeddings or vector database. At most eight chunks are sent to the provider. AI schema handling follows the [official structured outputs documentation](https://developers.openai.com/api/docs/guides/structured-outputs?api-mode=chat).

Every generated question is Draft and stores a chunk reference plus an exact source quote. Invalid/unknown citations or malformed/incomplete responses reject the entire batch. Question Details displays the file name, page/slide and quote. Lecturer adds a rubric, reviews and approves through the existing workflow. AI cannot approve questions. Provider calls time out after 90 seconds; failures display a retryable error without saving a partial batch. No live AI request is required at application startup.

## Verification

```powershell
$env:AIVES_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True'
dotnet test AIpoweredVivaExamSystem.slnx
```

Tests create and remove only a separate `AivesRubricTests_<guid>` database. They cover migrations, manual question/rubric regression, authentication, CSV/XLSX validation, import receipts, lecturer scope, chunk retrieval, exact citations, atomic generated drafts, and MVC forms. The generator is substituted in integration tests; live provider access requires the configured key/model and an explicit manual generation through the application.
