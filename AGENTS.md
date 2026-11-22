# AGENTS.md

## Stack
- .NET 11, ASP.NET Core Web API with **Controllers**
- Clean Architecture + CQRS + Repository Pattern
- **No MediatR/Mediator. No AutoMapper** (manual mapping only)
- FluentValidation, EF Core, Scrutor, Serilog

## Layers (dependencies point inward only)
- `Domain`: entities, enums, domain exceptions. References nothing.
- `Application`: commands, queries, handlers, validators, mappings, repository interfaces, and `IUnitOfWork`. References Domain.
- `Infrastructure`: EF Core, repositories, migrations, and external services. Implements Application interfaces.
- `WebAPI`: controllers, `Program.cs`. Composition root.

Handlers depend on repository and unit-of-work interfaces. Keep EF Core queries and provider errors in Infrastructure.

## EF Core Entity Configuration
- Each persisted entity must define its EF Core mapping in `[EntityName]Configuration.cs` under `Infrastructure/Persistence/Configurations` using `IEntityTypeConfiguration<TEntity>`.
- `OnModelCreating` must apply these configurations and call `SchoolSeed.Configure(modelBuilder)`.
- Implement repositories in `Infrastructure/Persistence/Repositories`. Register repositories and the unit of work as scoped services sharing the same DbContext.

## Application Structure
```
Application/
  Common/
    Abstractions/   (handler interfaces, repository interfaces, IUnitOfWork)
    Results/        (Result, Error)
  [Module]/
    Commands/[UseCaseName]/
    Queries/[UseCaseName]/
  Shared/
    Errors/[Module]Errors.cs
  DependencyInjection.cs
```

## File Naming
- `[UseCaseName]Command.cs` / `[UseCaseName]Query.cs`
- `[UseCaseName]CommandHandler.cs` / `[UseCaseName]QueryHandler.cs`
- `[UseCaseName]CommandValidator.cs`
- `[UseCaseName]QueryValidator.cs`
- `[UseCaseName]Mapping.cs` only if 2+ mappings; otherwise map inside the handler

## Rules
- Commands change state and return `Result<T>` when there is a response payload (e.g. `Result<Guid>` for a created entity ID), or `Result` when there is no payload. Queries are read-only and return `Result<TResponse>`. Repositories use `AsNoTracking()` and project directly to response types for EF Core reads.
- Commands and queries are plain records. No marker interfaces.
- Handlers: `internal sealed`, primary constructor, return `Result` or `Result<T>` according to the command or query contract, always pass `CancellationToken`.
- For expected failures (NotFound, Validation, Conflict), return a failed `Result` or `Result<T>` containing an `Error`.
- Mappings are manual extension methods (`ToEntity`, `ToResponse`).
- Controllers: no business logic. Inject handlers per action with `[FromServices]`, convert `Result` with `ToProblemDetails()`.
- Use `ValidationDecorator` to validate commands and queries that have a registered validator. Use a logging decorator for handler execution. Follow the Transactions section for transaction handling.
- DI: `Application` exposes `AddApplication()`, and `Infrastructure` exposes `AddInfrastructure()`. In `AddApplication()`, use Scrutor to register command and query handlers as scoped services under their handler interfaces. Set `publicOnly: false` in `AddClasses` to include `internal sealed` handlers.

## Cross-Cutting Rules

**Global error handling**
- Unexpected exceptions are handled by a single `IExceptionHandler` registered in WebAPI that returns a uniform `ProblemDetails`.
- Never return stack traces or exception messages to the client. Log the exception server-side.

**Transactions**
- Commit each command once through `IUnitOfWork.SaveChangesAsync` by default. Repositories stage changes on the shared DbContext.
- If a command needs multiple saves in one transaction, expose that transaction through an Application interface and coordinate it within the handler.

**Concurrency**
- Use a concurrency token (`RowVersion`) on entities that support concurrent edits.
- Include `rowVersion` in read responses and require it in update requests.
- Use the client-supplied `rowVersion` as the expected version when saving the update.
- Translate EF Core concurrency exceptions in Infrastructure. Return a `Conflict` error (HTTP 409) when the version is stale or persistence detects a concurrency conflict.

**Pagination and filtering**
- Every list query returns `Result<PagedResult<T>>` (items, page, pageSize, totalCount).
- Page size has a default and a hard maximum enforced in the query validator.
- Filtering and sorting use a shared pattern. Do not invent a new one per endpoint.

**Domain entities**
- Entities are not anemic: business rules live inside them (`Order.Create(...)`, `order.Cancel()`), with private setters.
- Handlers orchestrate (load, call domain methods, save). They do not contain business rules.

**Observability**
- Use **Serilog** for structured logging (inject `ILogger<T>`, no static `Log` calls in application code) with a correlation id on every request (middleware, returned in a response header and included in `ProblemDetails`).
- Expose health check endpoints (`/health`) from the start.

## New Use Case Checklist
1. Create the folder under `[Module]/Commands` or `[Module]/Queries`.
2. Add the command or query record and its handler. Add a validator for every command and every paginated query. For other queries, add a validator when the inputs have constraints. Add a mapping file when there are two or more mappings.
3. Add errors to `[Module]Errors.cs` if needed.
4. Add the controller action. No manual DI registration needed.
5. Run `dotnet build CleanArchitectureTemplate.slnx` from the repository root.
