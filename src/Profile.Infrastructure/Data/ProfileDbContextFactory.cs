using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Profile.Infrastructure.Data;

public class ProfileDbContextFactory : IDesignTimeDbContextFactory<ProfileDbContext>
{
    public ProfileDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ProfileDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost,1433;Database=ProfileDb;User Id=sa;Password=YourStrong@Passw0rd!2026;TrustServerCertificate=True;");

        return new ProfileDbContext(optionsBuilder.Options);
    }
}
