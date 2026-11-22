# AGENTS.md

Business rules and the endpoint list live in `Requirements.md`. Read it before adding or changing a use case. This file describes how the code is written.

## Stack
- .NET 11 (target framework, nullable and implicit usings are set once in `Directory.Build.props`), ASP.NET Core Web API with **Controllers**
- Clean Architecture + CQRS + Repository Pattern with Unit of Work
- **No MediatR/Mediator. No AutoMapper** (direct handler injection, manual mapping only)
- FluentValidation, EF Core (SQLite), Scrutor, Serilog

## Layers (dependencies point inward only)
- `Domain`: entities, enums, domain exceptions. References nothing.
- `Application`: commands, queries, handlers, validators, responses, results, repository interfaces and `IUnitOfWork`. References Domain only. **Application has no EF Core package reference**; keep it that way.
- `Infrastructure`: EF Core, `DbContext`, repository and Unit of Work implementations, migrations, external services. Implements Application interfaces.
- `WebAPI`: controllers, `Program.cs`. Composition root.

Handlers depend on repository interfaces and `IUnitOfWork`, never on `DbContext` or other Infrastructure types.

## Data Access (Repositories and Unit of Work)
- Repository interfaces and `IUnitOfWork` live in `Application/Common/Abstractions`. Implementations live in `Infrastructure/Persistence/Repositories` (repositories) and `Infrastructure/Persistence` (`SchoolUnitOfWork`).
- One repository per aggregate or feature area: `IStudentRepository`, `ICourseRepository`, `IEnrollmentRepository`.
- `DbContext`, `DbSet<T>`, `IQueryable<T>` and provider-specific types never leave Infrastructure. Repository methods return domain entities, response records, `PagedResult<T>`, `bool`, or small records such as `EnrollmentForCancellation`.
- Method kinds:
  - **Writes**: `Add` / `Remove` only stage changes. They never call `SaveChangesAsync`.
  - **Loads for a command**: return tracked domain entities, named after the use case that needs them (`GetForEnrollmentAsync`, `GetForCancellationAsync`).
  - **Reads for a query**: return response records directly (`GetByIdAsync`, `ListAsync`).
  - **Checks**: `ExistsAsync` returns `bool`.
- Before adding a new repository method, check whether an existing one already fits. Do not add a generic repository (`IRepository<T>`).
- `IUnitOfWork.SaveChangesAsync` commits once per command and returns `Result`. It translates persistence failures in one place:
  - `DbUpdateConcurrencyException` → `PersistenceErrors.ConcurrencyConflict`
  - SQLite primary-key or unique violation → `PersistenceErrors.DuplicateKey`
- Handlers translate a `PersistenceErrors` value to the module's own error when the meaning is known (e.g. `DuplicateKey` → `EnrollmentErrors.AlreadyEnrolled`). Otherwise they return the result as is. Handlers never use `try/catch` for persistence errors.
- Repositories and `IUnitOfWork` are registered manually as scoped services in `AddInfrastructure()`, so they share the same `DbContext`. Every new repository needs a registration line there.

## EF Core Entity Configuration
- Each persisted entity must define its EF Core mapping in `[EntityName]Configuration.cs` under `Infrastructure/Persistence/Configurations` using `IEntityTypeConfiguration<TEntity>`.
- `OnModelCreating` must apply these configurations and call `SchoolSeed.Configure(modelBuilder)`.
- Column lengths come from the entity's constants (e.g. `Course.MaximumTitleLength`), never from literal numbers.
- Enforce uniqueness in the database (keys or unique indexes), not only in handlers.

## Migrations
- Create migrations with the EF Core CLI only, from the repository root:
  `dotnet ef migrations add <Name> --project Infrastructure --startup-project WebAPI --output-dir Persistence/Migrations`
- Do not hand-write new migrations and do not edit a migration that has already been applied. Change the model, then add a new migration, and check that it preserves existing data.
- After a persistence refactor, check for pending model changes (`dotnet ef migrations has-pending-model-changes --project Infrastructure --startup-project WebAPI`).
- Seed data lives in `SchoolSeed`. Changing it requires a new migration.
- `20261120061000_InitialSchool` was written by hand and has no `.Designer.cs` file. This is a known exception; do not copy that style.
- The database is migrated automatically at startup (`MigrateDatabaseAsync` in `Program.cs`).

## Application Structure
```
Application/
  Common/
    Abstractions/   (ICommandHandler, IQueryHandler, I[Entity]Repository, IUnitOfWork)
    Decorators/     (ValidationDecorator, LoggingDecorator)
    Pagination/     (Pagination, PageQueryValidator)
    Results/        (Result, Error, PagedResult)
  [Module]/
    [Entity]Response.cs        (only when shared by 2+ use cases)
    Commands/[UseCaseName]/
    Queries/[UseCaseName]/
  Shared/
    Errors/[Module]Errors.cs, PersistenceErrors.cs
  DependencyInjection.cs
```

## File Naming
- `[UseCaseName]Command.cs` / `[UseCaseName]Query.cs`
- `[UseCaseName]CommandHandler.cs` / `[UseCaseName]QueryHandler.cs`
- `[UseCaseName]CommandValidator.cs` / `[UseCaseName]QueryValidator.cs`
- `[ResponseName]Response.cs`: inside the use-case folder when only that use case returns it; in the module root when 2+ use cases return it.
- `[UseCaseName]Mapping.cs` only if the use case has 2+ mappings; otherwise map inside the handler.
- `[EntityName]Repository.cs` implementing `I[EntityName]Repository`.

## Rules
- Commands change state and return `Result<T>` when there is a response payload (e.g. `Result<Guid>` for a created entity ID), or `Result` when there is no payload. Queries are read-only and return `Result<TResponse>`.
- Commands and queries are plain records. No marker interfaces.
- Handlers: `internal sealed`, primary constructor, return `Result` or `Result<T>` according to the command or query contract, always pass `CancellationToken`.
- For expected failures (NotFound, Validation, Conflict), return a failed `Result` or `Result<T>` containing an `Error`.
- Use `ValidationDecorator` to validate commands and queries that have a registered validator. Use a logging decorator for handler execution. Follow the Transactions section for transaction handling.
- DI: `Application` exposes `AddApplication()`, and `Infrastructure` exposes `AddInfrastructure()`. In `AddApplication()`, use Scrutor to register command and query handlers and validators as scoped services. Set `publicOnly: false` in `AddClasses` to include `internal sealed` types, and exclude open generic decorators from the scan.

## Mapping
- Commands: build entities through domain factory methods (`Course.Create(...)`), not object initializers. Use a `ToEntity` extension only when the factory call needs non-trivial argument mapping.
- Queries: EF Core projections live in repositories. Use `AsNoTracking()` and project directly to the response with `Select`. Never load the entity and map it in memory.
- When a repository uses the same projection in 2+ methods, define it once as a `private static readonly Expression<Func<TEntity, TResponse>> ToResponse` in that repository and pass it to `Select`.

## Validation
- Every command has a validator.
- Every query that takes an ID or is paginated has a validator. IDs are checked with `NotEmpty()` (route constraint `{id:guid}` still accepts `Guid.Empty`).
- Paginated query validators inherit from `PageQueryValidator<T>`.
- Validators mirror domain constraints using the entity's constants (`Student.MaximumFullNameLength`), so invalid user input is rejected before reaching the domain.
- Domain factory methods still guard their invariants with `ArgumentException` / `ArgumentOutOfRangeException`. These only fire on programming errors and end up in the global exception handler as a 500.

## Errors
- Each module has an `internal static class [Module]Errors` in `Application/Shared/Errors`, with `public static readonly Error` fields. Persistence-level errors live in `PersistenceErrors`.
- Error codes use the form `[Module].[Reason]` with a plural module name, e.g. `Courses.NotFound`, `Enrollments.CourseFull`.
- `ErrorType` maps to HTTP status in `ResultExtensions.ToProblemDetails`: `Validation` → 400, `NotFound` → 404, `Conflict` → 409. The error code is returned in the `code` extension of the `ProblemDetails`.

## Controllers
- No business logic. Inject handlers per action with `[FromServices]`, convert a failed `Result` with `result.ToProblemDetails(HttpContext)`.
- Request body:
  - When the body maps 1:1 to the command, bind the command record directly (e.g. `CreateCourseCommand`).
  - When the command also needs route values, define a `[Action]Request` record at the bottom of the controller file (e.g. `EnrollStudentRequest`) and build the command in the action.
- Responses:
  - Create command returning an ID → `201 Created` via `CreatedAtAction(nameof(Get), new { id }, id)`.
  - Command without payload → `204 No Content`.
  - Query → `200 OK` with the response.
- Every action has `[EndpointSummary]` and a `[ProducesResponseType]` for the success status and for every error status it can return (`ValidationProblemDetails` for 400, `ProblemDetails` for 404/409).
- List endpoints take `page` and `pageSize` from the query string with defaults from `Pagination`.
- Keep `WebAPI/WebAPI.http` in sync with the endpoints: add an example for every new endpoint and remove examples for deleted ones.

## Cross-Cutting Rules

**Global error handling**
- Unexpected exceptions are handled by a single `IExceptionHandler` registered in WebAPI that returns a uniform `ProblemDetails`.
- Never return stack traces or exception messages to the client. Log the exception server-side.

**Transactions**
- One `IUnitOfWork.SaveChangesAsync` per command is the default and is already atomic.
- If a command needs multiple saves that must succeed together, expose an explicit transaction through an Application interface. No global transaction decorator.

**Concurrency**
- Use a concurrency token (`RowVersion`, a `Guid` regenerated by the entity on every change) on entities that support concurrent edits.
- Include `rowVersion` in read responses and require it in update requests.
- Use the client-supplied `rowVersion` as the expected version when saving the update.
- Return a `Conflict` error (HTTP 409) if the version is stale or the Unit of Work returns `PersistenceErrors.ConcurrencyConflict`.
- Operations that change an entity as a side effect (e.g. enrolling changes `Course.EnrolledCount`) do not take a client `rowVersion`, but still map `PersistenceErrors.ConcurrencyConflict` to a module `Conflict` error.

**Pagination and filtering**
- Every list query returns `Result<PagedResult<T>>` (items, page, pageSize, totalCount). Repositories build it with `ToPageAsync` from `Infrastructure/Persistence/PagedQueryExtensions.cs`.
- Page size has a default (`Pagination.DefaultPageSize`) and a hard maximum (`Pagination.MaximumPageSize`) enforced in the query validator.
- Every list query has a deterministic order: a business field first, then `Id` as a tie-breaker.
- No endpoint supports client-side filtering or sorting yet. When the first one is needed, design one shared pattern (contract in `Application/Common/Pagination`, applied in Infrastructure), document it here, and only then use it. Do not add ad-hoc filter parameters to a single endpoint.

**Domain entities**
- Entities are not anemic: business rules live inside them (`Order.Create(...)`, `order.Cancel()`), with private setters and a private parameterless constructor for EF Core.
- Handlers orchestrate (load through repositories, call domain methods, save through `IUnitOfWork`). They do not contain business rules.
- Checks that need the database (an entity exists, a duplicate exists) are done in the handler through repository methods before calling the domain method, and return the matching `Error`.

**Observability**
- Use **Serilog** for structured logging (inject `ILogger<T>`, no static `Log` calls in application code) and Serilog request logging for HTTP.
- Every request has a correlation id set by `CorrelationIdMiddleware` from `HttpContext.TraceIdentifier`. It is server-generated (an incoming header is not trusted), returned in the `X-Correlation-Id` response header, added to the log scope, and included as `correlationId` in every `ProblemDetails`.
- Expose `/health` with a database readiness check from the start.

## Testing
- There is no test project yet. Until one exists, verify changes with `dotnet build CleanArchitectureTemplate.slnx` and by running the affected requests in `WebAPI/WebAPI.http`, including the failure cases.

## New Use Case Checklist
1. Check `Requirements.md` for the business rules; add the new rule there if it is missing.
2. Create the folder under `[Module]/Commands` or `[Module]/Queries`.
3. Add the command or query record, its handler, and its validator (see Validation). Add a response record where needed.
4. Add or reuse repository methods (see Data Access). Register any new repository in `AddInfrastructure()`.
5. Add errors to `[Module]Errors.cs` if needed.
6. If the model changed, add a migration with the EF Core CLI (see Migrations).
7. Add the controller action following the Controllers section, and an example request in `WebAPI.http`.
8. Add the endpoint to `Requirements.md`.
9. Run `dotnet build CleanArchitectureTemplate.slnx` from the repository root.
