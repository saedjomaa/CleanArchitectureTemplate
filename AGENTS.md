# AGENTS.md

## Stack

- Use ASP.NET Core Web API with Controllers, Clean Architecture, CQRS, and Repository Pattern.
- Use EF Core, FluentValidation, Scrutor, and Serilog.
- Use direct handler injection and manual mapping. Do not add MediatR, a mediator abstraction, or AutoMapper.
- Keep the target framework, nullable settings, and implicit usings in `Directory.Build.props`.

## Layers

- `Domain` owns entities, value objects, enums, and business rules. It has no project dependencies. Use domain methods and private setters to protect entity state.
- `Application` owns commands, queries, handlers, validators, responses, results, repository interfaces, and `IUnitOfWork`. It references Domain.
- `Infrastructure` implements Application interfaces and owns EF Core, repository implementations, migrations, and external integrations.
- `WebAPI` owns controllers, HTTP routes, OpenAPI metadata, and HTTP responses. `Program.cs` is the composition root.

## EF Core Entity Configuration

- Each persisted entity must define its EF Core mapping in `[EntityName]Configuration.cs` under `Infrastructure/Persistence/Configurations` using `IEntityTypeConfiguration<TEntity>`.
- `OnModelCreating` must apply these configurations and call `SchoolSeed.Configure(modelBuilder)`.

## Use Cases

- Put each operation under `Application/[Module]/Commands/[UseCaseName]` or `Application/[Module]/Queries/[UseCaseName]`.
- Use `[UseCaseName]Command.cs` or `[UseCaseName]Query.cs` for the request, with matching `CommandHandler`, `QueryHandler`, `CommandValidator`, and `QueryValidator` filenames.
- Use plain records for commands and queries without marker interfaces. Use `internal sealed` handlers with primary constructors and pass `CancellationToken` through async calls.
- Commands change state and return `Result<T>` when they return data or `Result` otherwise. Queries are read-only and return `Result<TResponse>`.
- Handlers load data through interfaces, call domain methods, and coordinate saving. Keep business rules in Domain.
- Put expected errors in `Application/Shared/Errors/[Module]Errors.cs`. Return them in failed results.
- Run validation and handler logging through decorators. Add a validator for every command, every paginated query, and other queries with input constraints.
- Keep a single manual conversion inline. When a use case needs two or more conversions, use `[UseCaseName]Mapping.cs` with methods such as `ToEntity` and `ToResponse`. Keep EF Core projections in repositories.

## Persistence

- Put repository contracts and `IUnitOfWork` in `Application/Common/Abstractions`, and implementations in `Infrastructure/Persistence/Repositories`.
- Define repository boundaries around feature or aggregate needs. Keep `DbContext`, `DbSet<T>`, `IQueryable<T>`, and provider-specific errors inside Infrastructure.
- Give each persisted entity a `[EntityName]Configuration.cs` file under `Infrastructure/Persistence/Configurations` implementing `IEntityTypeConfiguration<TEntity>`. Use `OnModelCreating` to apply configurations and call a separate seed configuration when needed.
- Use `AsNoTracking()` and project directly to the response for read-only EF Core queries.
- Register repositories and `IUnitOfWork` as scoped services sharing the same `DbContext`. Repositories stage writes. Commit once per command through `IUnitOfWork.SaveChangesAsync` by default. For multiple saves that must succeed together, expose an explicit transaction through an Application interface.
- Enforce uniqueness in the database. Use a provider-appropriate concurrency token where concurrent changes matter. For client-driven updates, return `rowVersion` in reads, require it on updates, and use the client value as the expected version when saving. Translate persistence conflicts in Infrastructure and return HTTP `409`.
- List queries return `Result<PagedResult<T>>` with `items`, `page`, `pageSize`, and `totalCount`. Enforce the default and maximum page size through shared pagination settings and query validation. Use shared filtering and sorting conventions with a stable order.

## API and Operations

- Keep controllers focused on HTTP. Define routes in WebAPI, inject the handler per action with `[FromServices]`, and convert failed results with `ToProblemDetails()`.
- Map validation, missing resources, and conflicts to `400`, `404`, and `409`. Use one `IExceptionHandler` for unexpected errors. Log exception details server-side and return a generic `ProblemDetails`.
- Register Application services in `AddApplication()` and Infrastructure services in `AddInfrastructure()`. Scan handlers as scoped services with Scrutor using `publicOnly: false`, and exclude open generic decorators from the scan. Include internal validators in registration.
- Use `ILogger<T>` for application logging and Serilog request logging for HTTP. Include a correlation ID in logs, the response header, and `ProblemDetails`. Expose `/health` with a database readiness check.

## Before Finishing

1. Verify the use case through its normal entry point and check relevant failure cases.
2. Check for pending EF Core model changes after persistence refactors. If the schema changes, add a migration and verify that it preserves existing data.
3. Update OpenAPI metadata and `.http` examples when the HTTP contract changes.
4. Run `dotnet build` for the target solution from the repository root.