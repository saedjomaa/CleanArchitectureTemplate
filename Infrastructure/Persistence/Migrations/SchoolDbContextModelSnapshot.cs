using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(SchoolDbContext))]
internal sealed class SchoolDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder) => InitialSchool.BuildInitialModel(modelBuilder);
}
