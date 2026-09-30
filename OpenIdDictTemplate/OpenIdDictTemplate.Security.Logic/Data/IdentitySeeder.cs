using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace OpenIdDictTemplate.Security.Logic.Data;

/// <summary>Idempotently creates the configured roles and users; existing users are never modified.</summary>
public class IdentitySeeder(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ILogger<IdentitySeeder> logger)
{
    public async Task SeedAsync(SeedOptions options)
    {
        var roles = options.Roles.Concat(options.Users.Values.SelectMany(u => u.Roles)).Distinct();
        foreach (var role in roles)
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;

            Ensure(await roleManager.CreateAsync(new IdentityRole(role)), $"create role '{role}'");
            logger.LogInformation("Seeded role {Role}", role);
        }

        foreach (var (key, seedUser) in options.Users)
        {
            if (string.IsNullOrWhiteSpace(seedUser.Email))
                throw new InvalidOperationException($"Seed user '{key}' has no Email.");

            var user = await userManager.FindByEmailAsync(seedUser.Email);
            if (user is null)
            {
                if (string.IsNullOrEmpty(seedUser.Password))
                    throw new InvalidOperationException($"Seed user '{key}' has no Password (set Seed:Users:{key}:Password, e.g. via user-secrets).");

                user = new ApplicationUser { UserName = seedUser.Email, Email = seedUser.Email, EmailConfirmed = true };
                Ensure(await userManager.CreateAsync(user, seedUser.Password), $"create user '{key}'");
                logger.LogInformation("Seeded user {Email}", seedUser.Email);
            }

            var currentRoles = await userManager.GetRolesAsync(user);
            var missingRoles = seedUser.Roles.Except(currentRoles).ToList();
            if (missingRoles.Count > 0)
                Ensure(await userManager.AddToRolesAsync(user, missingRoles), $"add roles to user '{key}'");
        }
    }

    private static void Ensure(IdentityResult result, string action)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Failed to {action}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
    }
}
