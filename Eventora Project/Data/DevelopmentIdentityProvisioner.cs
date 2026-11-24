using Eventora_Project.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Eventora_Project.Data
{
    public static class DevelopmentIdentityProvisioner
    {
        private const string AllowEnvironmentVariable = "EVENTORA_ALLOW_DEV_PROVISIONING";
        private const string AdminRole = "Admin";
        private const string SocietyHeadRole = "SocietyHead";

        public static async Task ProvisionAsync(IServiceProvider services, IWebHostEnvironment environment)
        {
            EnsureDevelopmentGuard(environment);
            var options = ReadOptions();

            using var scope = services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            await EnsureRoleAsync(roleManager, AdminRole);
            await EnsureRoleAsync(roleManager, SocietyHeadRole);

            var createdUsers = new List<ApplicationUser>();

            try
            {
                await ProvisionUserAsync(userManager, options.Admin, AdminRole, createdUsers);
                await ProvisionUserAsync(userManager, options.SocietyHead, SocietyHeadRole, createdUsers);
            }
            catch (Exception provisioningException)
            {
                if (createdUsers.Count > 0)
                {
                    try
                    {
                        await CleanupCreatedUsersAsync(userManager, createdUsers);
                        Console.WriteLine("Rolled back newly created development accounts.");
                    }
                    catch (Exception cleanupException)
                    {
                        throw new InvalidOperationException(
                            $"Provisioning failed: {provisioningException.Message} Rollback failed: {cleanupException.Message}",
                            provisioningException);
                    }
                }

                throw;
            }

            Console.WriteLine("Development account provisioning completed.");
        }

        private static void EnsureDevelopmentGuard(IWebHostEnvironment environment)
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Development-only provisioning refused because the application environment is not Development.");
            }

            var allowed = Environment.GetEnvironmentVariable(AllowEnvironmentVariable);
            if (!string.Equals(allowed, "1", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Development-only provisioning refused because '{AllowEnvironmentVariable}' is not set to 1.");
            }
        }

        private static ProvisioningOptions ReadOptions()
        {
            var adminEmail = Required("EVENTORA_ADMIN_EMAIL").Trim();
            var adminPassword = Required("EVENTORA_ADMIN_PASSWORD");
            var societyHeadEmail = Required("EVENTORA_SOCIETYHEAD_EMAIL").Trim();
            var societyHeadPassword = Required("EVENTORA_SOCIETYHEAD_PASSWORD");
            var adminFullName = Environment.GetEnvironmentVariable("EVENTORA_ADMIN_FULLNAME");
            var societyHeadFullName = Environment.GetEnvironmentVariable("EVENTORA_SOCIETYHEAD_FULLNAME");

            ValidateEmail(adminEmail, "EVENTORA_ADMIN_EMAIL");
            ValidateEmail(societyHeadEmail, "EVENTORA_SOCIETYHEAD_EMAIL");

            if (string.Equals(adminEmail, societyHeadEmail, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "EVENTORA_ADMIN_EMAIL and EVENTORA_SOCIETYHEAD_EMAIL must be different addresses.");
            }

            return new ProvisioningOptions(
                new AccountOptions(
                    adminEmail,
                    adminPassword,
                    string.IsNullOrWhiteSpace(adminFullName) ? "Development Admin" : adminFullName.Trim()),
                new AccountOptions(
                    societyHeadEmail,
                    societyHeadPassword,
                    string.IsNullOrWhiteSpace(societyHeadFullName) ? "Development Society Head" : societyHeadFullName.Trim()));
        }

        private static string Required(string variableName)
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Missing required environment variable '{variableName}'.");
            }

            return value;
        }

        private static void ValidateEmail(string email, string variableName)
        {
            if (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@') || email.Contains(' '))
            {
                throw new InvalidOperationException($"Environment variable '{variableName}' must be a valid email address.");
            }
        }

        private static async Task EnsureRoleAsync(RoleManager<IdentityRole> roleManager, string role)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                return;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Could not create role '{role}': {FormatErrors(result.Errors)}");
            }

            Console.WriteLine($"Created role '{role}'.");
        }

        private static async Task ProvisionUserAsync(
            UserManager<ApplicationUser> userManager,
            AccountOptions options,
            string role,
            List<ApplicationUser> createdUsers)
        {
            var existingUser = await userManager.FindByEmailAsync(options.Email);
            if (existingUser is not null)
            {
                if (!string.Equals(existingUser.UserName, options.Email, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Account '{options.Email}' already exists with a different username. Existing accounts are not modified.");
                }

                var existingRoles = await userManager.GetRolesAsync(existingUser);
                if (!existingRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Account '{options.Email}' already exists without the '{role}' role. Existing accounts are not modified.");
                }

                Console.WriteLine($"Account '{options.Email}' already has the '{role}' role. No changes made.");
                return;
            }

            var user = new ApplicationUser
            {
                UserName = options.Email,
                Email = options.Email,
                FullName = options.FullName,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user, options.Password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create account '{options.Email}': {FormatErrors(createResult.Errors)}");
            }

            createdUsers.Add(user);

            var roleResult = await userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not assign role '{role}' to newly created account '{options.Email}': {FormatErrors(roleResult.Errors)}");
            }

            Console.WriteLine($"Created '{role}' account '{options.Email}'.");
        }

        private static async Task CleanupCreatedUsersAsync(
            UserManager<ApplicationUser> userManager,
            IEnumerable<ApplicationUser> createdUsers)
        {
            var errors = new List<string>();

            foreach (var user in createdUsers.Reverse())
            {
                var result = await userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    errors.Add($"'{user.Email}': {FormatErrors(result.Errors)}");
                }
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Newly created accounts could not be removed: {string.Join("; ", errors)}");
            }
        }

        private static string FormatErrors(IEnumerable<IdentityError> errors)
        {
            return string.Join("; ", errors.Select(error => error.Description));
        }

        private sealed record AccountOptions(string Email, string Password, string FullName);

        private sealed record ProvisioningOptions(AccountOptions Admin, AccountOptions SocietyHead);
    }
}
