# MVC and three logical layers

The application uses ASP.NET Core MVC with Razor Views. It does not use Razor Pages routing or `PageModel`.

```text
Presentation
  AIpoweredVivaExamSystem.Web/
    Controllers/          HTTP, validation, redirects and view selection
    ViewModels/           Form binding and view-specific data
    Views/                Razor .cshtml templates, grouped by controller
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

Controllers never query DbContext or call the AI provider directly. Application services orchestrate operations; adapters parse files and persist data. A single `SaveChangesAsync` transaction saves each import batch or generated batch together with its receipt/citations. No partial import is committed when validation fails.

Authorization is enforced by controller roles and by request-aware EF query filters. Lecturers see assigned subjects, and only their own questions/documents in those subjects. Rubrics, chunks and citations inherit the parent scope. The current user is supplied by Web; direct design-time/test contexts explicitly omit request scoping. Administrators can manage all data. Authentication reloads the current role from SQL for every cookie request, so role revocation does not wait for cookie expiry.

Group 2 now has shared schema/contracts, RBAC guards, a transaction boundary and a read-only integration MVC page on `codex/g2-integration`. Exam CRUD, pool/roster/scheduling/session modules and actual SignalR Hub/client still await their owners. See [GROUP_2_TEAM_BACKLOG.md](GROUP_2_TEAM_BACKLOG.md) and [Kiet's handoff](GROUP_2_KIET_HANDOFF.md).
