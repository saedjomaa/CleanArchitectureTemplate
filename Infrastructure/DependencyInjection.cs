using Application.Common.Abstractions;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SchoolDbContext>(options => options.UseSqlite(connectionString));
        services.Scan(scan => scan.FromAssemblyOf<SchoolDbContext>()
            .AddClasses(classes => classes
                .Where(type => type.Name.EndsWith("Repository", StringComparison.Ordinal)), publicOnly: false)
            .AsMatchingInterface()
            .WithScopedLifetime());
        services.AddScoped<IUnitOfWork, SchoolUnitOfWork>();
        services.AddHealthChecks().AddDbContextCheck<SchoolDbContext>("database",
            customTestQuery: async (db, cancellationToken) =>
            {
                await db.Set<Course>().AnyAsync(cancellationToken);
                return true;
            });

        return services;
    }

    public static async Task MigrateDatabaseAsync(
        this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
