# Group 1: question bank setup and acceptance

## Database and first administrator

Apply the committed migrations to your chosen database after reviewing them:

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
$env:Ai__Model = 'glm-5.3-flash' # optional; blank uses this default
$env:Ai__BaseUrl = 'https://api.z.ai/api/coding/paas/v4' # optional; blank uses this default
```

For Docker, set `AI_API_KEY`, `AI_MODEL` and optionally `AI_BASE_URL` in your local `.env`. Relevant source text is sent to the configured AI provider (z.ai by default). Generation requires an existing uploaded document, an instruction, Bloom level, difficulty, optional topic and 1–10 questions. Retrieval currently ranks local chunks by query-term overlap; it is lexical RAG, with no embeddings or vector database. At most eight chunks are sent to the provider. The provider must support OpenAI-compatible Chat Completions with JSON mode (`response_format: json_object`).

Every generated question is Draft and stores a chunk reference plus an exact source quote. Invalid/unknown citations or malformed/incomplete responses reject the entire batch. Question Details displays the file name, page/slide and quote. Lecturer adds a rubric, reviews and approves through the existing workflow. AI cannot approve questions. Generation has a 90-second total budget, including up to three attempts for connection failures and HTTP 408/429/502/503/504, with 250 ms then 750 ms backoff; failures display a retryable error without saving a partial batch. No live AI request is required at application startup.

## Interview voice and answer timing

Set `ElevenLabs:ApiKey` on the server (`ElevenLabs__ApiKey` outside Docker; `ELEVENLABS_API_KEY` in Docker's `.env`) to enable examiner audio. `ElevenLabs:VoiceId` defaults to `iSFxP4Z6YNcx9OXl62Ic` (Viên, Vietnamese northern accent) and `ElevenLabs:Model` defaults to `eleven_v4_turbo`, which supports Vietnamese and English. Docker overrides are `ELEVENLABS_VOICE_ID` and `ELEVENLABS_MODEL`. The key stays on the server. Missing configuration or a failed 12-second synthesis returns HTTP 503 without a body, and the browser uses its built-in speech. Successful MP3s are shared by voice/model/prompt hash in process memory for one hour, capped at 64 MiB; concurrent replays share a single paid synthesis. Requesting the active question's voice also warms the next selected main question in parallel, using the same cache; current playback never waits for that prefetch. This may synthesise one unused main question if the session ends early. Follow-up speech starts once the follow-up is known. Restarting the app or running multiple instances gives each process a separate cache.

Examiner evaluation uses the same retry policy with a 30-second total budget. It runs only when another follow-up is allowed by the exam's cap and at least 15 seconds remain in the slot at answer receipt; otherwise the server saves the answer and advances without waiting for an examiner decision. Rubric grading remains separate. Speech recognition uses the ElevenLabs key independently of the examiner provider: `ElevenLabs:SttModel` defaults to `scribe_v3` for batch audio transcription (30-second total budget, same retry policy); `ElevenLabs:RealtimeSttModel` defaults to `scribe_v3_live` for live transcripts. Docker overrides are `ELEVENLABS_STT_MODEL` and `ELEVENLABS_REALTIME_STT_MODEL`. The student-only, antiforgery-protected `POST /VivaSessions/ScribeToken` checks ownership and rejects completed sessions before issuing a single-use token (12-second total budget) for the browser's realtime connection. Missing configuration or provider failure returns HTTP 503 without a body. The browser falls back to built-in speech recognition or recorded audio with batch transcription; students can also type their answers. A submitted live/typed transcript bypasses batch STT while its attached recording is still saved. Failed manual audio-only transcription leaves the turn open for retry.

On manual submission the browser finishes the recorder and waits up to two seconds for Scribe's final transcript, in parallel with the recorder's 1.2-second stop guard, to retain the last spoken words. Timeout submission closes recognition immediately. The next page starts loading question audio after initialization, without an added playback delay.

The server enforces each turn deadline. A browser `timedOut=true` flag is accepted up to two seconds early. From the deadline through two seconds afterward, transcript/audio are retained and the session advances without AI follow-up. Empty timeout answers receive a clear timeout marker; transcription failure does not block them. Later content is discarded, including audio, and the marker is recorded. After the slot deadline the same rules apply and the session completes on submission.

## Lecturer grading and student results

Apply the `AddLecturerGrading` migration with the database-update command above before opening `/Grading`. Choose a managed exam to see every enrollment and its interview/review state. Opening the list or review persistently closes abandoned sessions after their slot plus the two-second submission grace; unsubmitted turns become timeout/no-answer records. Recordings are served only to the exam's authorised lecturer or an administrator through `/Grading/Audio/{audioId}`, with range requests for seeking.

Answer submission holds a SQL session application lock from transcription through the short commit, so duplicate submissions and expiry cleanup cannot discard an answer being processed. The lock is specific to one student's session; speech and examiner AI run without a database transaction or exam-row lock. Expiry skips an in-flight submission without waiting and can close it on the next list/review request if processing was cancelled.

The review page loads without AI calls. Each main question can request or regenerate a suggestion independently (the browser starts at most two at a time). A suggestion scores the whole transcript thread against the saved rubric, with per-criterion reasons, strengths, weaknesses and missing points. Timing, audio duration, follow-up count and answer length are evidence for the lecturer only, never scoring inputs. Suggestions use the configured AI key/model, the shared retry policy and a 60-second total budget. A two-minute claim lease prevents simultaneous duplicate requests and lets a request resume after process loss; successful suggestions survive other questions' failures. No-answer questions receive zero suggestions without a provider call. AI failure leaves manual grading available.

Enter lecturer scores (0.01 precision, between zero and each criterion maximum), comments per question and an overall comment. **Save draft** may leave scores blank. **Finalise** requires every criterion score. AI values never become lecturer scores automatically, and regeneration never changes saved lecturer values. Finalisation records the lecturer and time separately from the AI model/time. To change a finalised grade, explicitly reopen it, edit and re-finalise; the previous finalisation and reopen audit fields remain stored, and the latest finalisation fields update on re-finalising. Concurrent stale saves are refused using the review version.

Students receive no grading data before finalisation or while a grade is reopened. An open completed-session page updates automatically when the lecturer finalises or reopens the grade: finalisation shows the result, and reopening hides it again. It shows only the final total, question totals and lecturer comments; it never receives suggested scores, rubric criteria, expected answers or lecturer evidence signals. The student connection is limited to their own completed session and sends only a change notification; the page re-fetches the authorised result. Reconnecting checks for missed changes. If the connection is unavailable, refresh the page to see the latest result.

## Panel demo

From the repository root on Windows with .NET 10 and local SQL Server on port 1433:

```powershell
./tools/demo.ps1 seed
./tools/demo.ps1 start
```

The seed command creates/migrates **AivesPanelDemo** using Windows authentication. Running it again resets only that dedicated database, including grades changed during rehearsals, and opens a fresh 30-minute slot for the live student. It refuses to reset an existing database without its own ownership marker. Stop the demo app before reseeding. The app runs at **http://127.0.0.1:5192**; QA/test databases are separate.

Put `AI_API_KEY`, `ELEVENLABS_API_KEY` and `JWT_KEY` in the ignored root `.env`, or set `Ai__ApiKey`, `ElevenLabs__ApiKey` and `Jwt__Key` in the environment. The script maps the supported `.env` names to ASP.NET settings in memory; it writes no keys. Optional AI/ElevenLabs model and voice settings use the names described above. SQL permissions must allow creating the demo database. Audio and actual AI suggestions are kept under `tools/DemoSeed/assets`; subsequent seeds reuse them. Answer recordings are synthetic readings of fictional Vietnamese students, not recordings of real students. Generated session recordings/IDs live in ignored `tools/DemoSeed/.local`; the start command uses that same recording directory via `VivaAudio:RootDirectory`.

Every account below uses **DemoPass#2026**:

| Account | Role / scenario |
| --- | --- |
| `tructiep@demo.aives.test` | Võ Gia Bảo — fresh live interview |
| `giangvien@demo.aives.test` | Nguyễn Minh Khôi — lecturer |
| `gioi@demo.aives.test` | Nguyễn Hoàng Anh — strong answers, suggestions ready, awaiting lecturer review |
| `motphan@demo.aives.test` | Trần Thảo Vy — partial answers and an answered follow-up, suggestions arrive on opening review |
| `hetgio@demo.aives.test` | Lê Minh Đức — one unanswered timeout, already finalised at 6/20 |
| `chuathi@demo.aives.test` | Phạm Ngọc Hân — never started |
| `quantri@demo.aives.test` | Trần Hoàng Phúc — administrator |

Presenter sequence:

1. **Interview:** in Chrome/Edge, sign in as `tructiep`, open **Lịch thi của tôi**, then **Mở phiên thi** for the PRN222 exam. The examiner immediately starts reading. Use headphones, click **Bắt đầu trả lời** and allow microphone access. The exam draws two questions from six; each turn allows five minutes, with at most one follow-up per main question. Give an incomplete academic answer to elicit a follow-up (for DI, explain constructor injection but omit lifetimes; for EF, mention SaveChanges but omit tracking/AsNoTracking). Watch the live transcript and click **Nộp câu trả lời**. The examiner voice requires ElevenLabs connectivity; typed answers remain available. Reseed just before presenting if the live slot has expired.
2. **Lecturer:** sign out, sign in as `giangvien`, choose **Chấm thi** → **Xem bài thi** for the PRN222 exam → **Chấm bài** on Nguyễn Hoàng Anh. Show ready suggestions and play an answer. Use **Dùng gợi ý AI** or enter criterion scores, change one score, add a comment, save draft, then **Chốt điểm**. Open Trần Thảo Vy to show suggestions arriving and the main/follow-up transcript; expect about 20–30 seconds while the page shows its AI progress message, and talk through the transcript meanwhile. Lê Minh Đức demonstrates the timeout and a previously finalised result; Phạm Ngọc Hân remains not started.
3. **Student result:** sign out, sign in as `hetgio`, open **Lịch thi của tôi** and view the completed session. The student sees 6/20 and lecturer comments. To show the just-finalised strong result instead, sign in as `gioi`. A draft or reopened grade is hidden from the student.

Keep a lecturer and student in separate browser profiles if demonstrating both simultaneously; tabs in one profile share sign-in cookies. A localhost origin is required for microphone access without HTTPS. Provider/network availability affects live speech and newly requested suggestions; the ready examples require no grading calls on stage.

## Verification

```powershell
$env:AIVES_TEST_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True'
dotnet test AIpoweredVivaExamSystem.slnx
```

Tests create and remove only a separate `AivesRubricTests_<guid>` database. They cover migrations, manual question/rubric regression, authentication, CSV/XLSX validation, import receipts, lecturer scope, chunk retrieval, exact citations, atomic generated drafts, and MVC forms. The generator is substituted in integration tests; live provider access requires the configured key/model and an explicit manual generation through the application.
