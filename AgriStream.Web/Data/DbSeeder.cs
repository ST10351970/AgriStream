using AgriStream.Shared;
using AgriStream.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Data;

/// <summary>
/// Creates one starter user per role the first time the app runs
/// Passwords hashed with ASP.NET Core PasswordHasher
/// Starter password comes from configuration (user-secrets / App Service settings)
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AgriStreamDbContext>();
        var hasher = services.GetRequiredService<IPasswordHasher<AppUser>>();
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        if (await db.Users.AnyAsync())
            return; // already seeded

        var password = config["Seed:DefaultPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:DefaultPassword is not configured - no starter users created.");
            return;
        }

        var roleIds = await db.Roles.ToDictionaryAsync(r => r.RoleName, r => r.RoleId);
        if (roleIds.Count == 0)
        {
            logger.LogWarning("No roles found - run the Phase 1 SQL script first.");
            return;
        }

        var starters = new[]
        {
            (Username: "admin",       FullName: "System Administrator", Role: AppRoles.Admin),
            (Username: "farmer1",     FullName: "Demo Farmer",          Role: AppRoles.Farmer),
            (Username: "agronomist1", FullName: "Demo Agronomist",      Role: AppRoles.Agronomist)
        };

        foreach (var s in starters)
        {
            if (!roleIds.TryGetValue(s.Role, out var roleId)) continue;

            var user = new AppUser { Username = s.Username, FullName = s.FullName, RoleId = roleId };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} starter users.", starters.Length);
    }
}