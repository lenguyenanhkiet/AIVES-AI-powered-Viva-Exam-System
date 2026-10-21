# Razor Pages and three logical layers

The presentation layer is ASP.NET Core Razor Pages. There are no MVC controllers: each URL is a page `Pages/<Module>/<Name>.cshtml` with its `PageModel` in `<Name>.cshtml.cs`.

```text
Presentation
  AIpoweredVivaExamSystem.Web/
    Pages/                Razor pages (.cshtml) and their PageModels (.cshtml.cs), grouped by module;
                          Pages/Shared holds the layout and shared partials
    ViewModels/           Form input ([BindProperty] Input) and page-specific data
    Realtime/             SignalR ExamHub (/hubs/exams) and the IExamNotifier adapter
    Authentication/       Cookie validation and current request identity
    Common/               Presentation helpers and HTTP exception mapping
    wwwroot/              CSS, JavaScript and static assets

Business
  AIpoweredVivaExamSystem.Application/
    Authentication/       Login, role/subject administration, current-user abstraction
    Subjects/, Topics/    Academic services, DTOs and validators
    Questions/, Rubrics/  Existing manual CRUD and review workflow
    QuestionBank/         Import, document ingestion, retrieval and generation orchestration
    Interfaces/           Repository abstractions for existing CRUD
  AIpoweredVivaExamSystem.Domain/
    Entities/             Academic entities, documents, chunks, citations and assignments
    Enums/, Common/       Value choices and invariant validation

Data access and external integrations
  AIpoweredVivaExamSystem.Persistence/
    Context/              EF Core context and design-time factory
    Configurations/       Schema mapping
    Repositories/         SQL implementations of Application interfaces
    Migrations/           Versioned database changes
  AIpoweredVivaExamSystem.Infrastructure/
    Authentication/       Password and token adapters
    QuestionBank/         CSV/XLSX readers, PDF/PPTX extraction and OpenAI adapter
```

References: `Web → Application → Domain`; `Persistence → Application + Domain`; `Infrastructure → Application + Domain`. Web references the adapters to compose dependency injection. Application and Domain do not reference Web, EF Core, SignalR or an AI SDK.

PageModels take Application services through constructor injection and never query DbContext or call the AI provider directly. Application services orchestrate operations; adapters parse files and persist data. A single `SaveChangesAsync` transaction saves each import batch or generated batch together with its receipt/citations. No partial import is committed when validation fails.

Authorization is enforced by `[Authorize]` roles on each PageModel and by request-aware EF query filters. Lecturers see assigned subjects, and only their own questions/documents in those subjects. Rubrics, chunks and citations inherit the parent scope. The current user is supplied by Web; direct design-time/test contexts explicitly omit request scoping. Administrators can manage all data. Authentication reloads the current role from SQL for every cookie request, so role revocation does not wait for cookie expiry.

Group 2 has the shared foundation (schema/contracts, RBAC guards, transaction boundary), student roster (`ExamStudents`), sequential scheduling (`Schedules`), the question pool (`ExamPools`) and sessions with the SignalR dashboard (`VivaSessions`, `ExamFoundation/Details`). Exam CRUD/lifecycle remains a separate module. Module status and links are in the README and [GROUP_2_TEAM_BACKLOG.md](GROUP_2_TEAM_BACKLOG.md).
