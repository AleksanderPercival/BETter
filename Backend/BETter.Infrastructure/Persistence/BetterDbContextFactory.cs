using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BETter.Infrastructure.Persistence;

public class BetterDbContextFactory : IDesignTimeDbContextFactory<BetterDbContext>
{
    public BetterDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BetterDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("BETTER_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=better;Username=better;Password=better";

        optionsBuilder.UseNpgsql(connectionString);
        return new BetterDbContext(optionsBuilder.Options);
    }
}
