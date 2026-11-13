# Three-layer architecture

The three production projects follow the supplied reference diagram:

Diagram: [SVG](architecture-three-layer.svg) · [editable draw.io](architecture-three-layer.drawio).

```text
AIpoweredVivaExamSystem.Presentation
                  |
                  v
AIpoweredVivaExamSystem.Business --> AI / speech providers
                  |
                  v
AIpoweredVivaExamSystem.DataAccess --> SQL Server / local files
```

`Presentation` references only `Business`. `Business` references only `DataAccess`. `DataAccess` has no project reference. There are no separate Domain, Application or Infrastructure projects. Tests and seed/smoke tools are independent entry points that access the layers they verify or populate.

| Layer | Responsibilities |
| --- | --- |
| Presentation | `Pages` and PageModels, `ViewModels`, role filters, cookie/session handling, static assets, SignalR hubs and delivery adapters. |
| Business | Services and service interfaces, input validation, access checks, exam/interview/grading orchestration, HTTP AI/speech integrations, password verification and token creation. |
| DataAccess | Entities/enums, shared data models and repository contracts, SQL repositories, EF context/configurations/migrations, file readers/extractors and recording storage. |

PageModels call Business services. They do not call repositories, query EF, or invoke AI providers. Shared entities, enums and transport models are declared in DataAccess and may appear in service signatures; using those types does not grant Presentation a data-access API.

Presentation calls `AddBusiness()` to register services. Business calls `AddDataAccess()` to register repositories and the EF context, so Presentation does not need a DataAccess project reference. PageModels, cookie events and SignalR hubs receive Business services through constructor injection. DataAccess never registers or calls a Business service.

Services, repositories and the context are scoped to the request or SignalR invocation. The current-user adapter reads the request principal when accessed, including after cookie validation refreshes its roles. Connection registries and the examiner speech cache are singletons; HTTP clients use `IHttpClientFactory`. Presentation supplies the SignalR notification adapters. DI manages object lifetimes and does not change the three-layer reference graph.

AI evaluation runs in Business before the short answer commit. DataAccess serializes submissions and enforces persistence consistency under SQL locks. A transaction commits exam changes before the Business notification wrapper calls Presentation's SignalR adapter; delivery failure cannot undo committed work. File imports and generated drafts are saved atomically with their receipts/citations.

Authorization is enforced by role attributes and Business access guards. DataAccess applies identity-scoped query filters; lecturers see assigned subjects and their own questions/documents. Students receive only safe question projections and finalized lecturer results. Cookie validation reloads account status and role on each request, so revocation is reflected without waiting for expiry.

Setup, migrations, accounts and demonstration steps are maintained in [GROUP_1_SETUP.md](GROUP_1_SETUP.md).
