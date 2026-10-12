using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SamyrTechWay.SpinWin.Data;

/// <summary>
/// Used only by the `dotnet ef` CLI. Reads the connection string from user-secrets / environment
/// (ConnectionStrings__DefaultConnection). `migrations add` and `migrations script` work without one.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<DesignTimeDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(cs))
            cs = "Server=localhost;Database=SamyrSpinWin_DesignTime;Integrated Security=false;TrustServerCertificate=true";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(cs, sql => sql.CommandTimeout(120))
            .Options;
        return new AppDbContext(options);
    }
}
