using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonitoringSystem.Models;

namespace MonitoringSystem.Data
{
    public static class AudioIdentitySeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider, IConfiguration configuration, ILogger logger)
        {
            using var scope = serviceProvider.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var scaffoldedDb = scope.ServiceProvider.GetRequiredService<ScaffoldedDbContext>();

            // 1. Seed Roles in dbo.Roles
            try
            {
                if (!await scaffoldedDb.Roles.AnyAsync())
                {
                    scaffoldedDb.Roles.AddRange(
                        new Role { RoleId = 1, RoleName = "Admin" },
                        new Role { RoleId = 2, RoleName = "User" },
                        new Role { RoleId = 3, RoleName = "Manajemen" }
                    );
                    await scaffoldedDb.SaveChangesAsync();
                    logger.LogInformation("dbo.Roles seeded with Admin, User, Manajemen.");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("dbo.Roles seeding notice: {Message}", ex.Message);
            }

            // 2. Seed Audio ASP.NET Identity Roles
            string[] audioRoles = new[] { "Administrator", "Supervisor", "Operator", "User", "Manajemen" };
            foreach (var role in audioRoles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                    if (roleResult.Succeeded)
                    {
                        logger.LogInformation("Audio Identity Role seeded: {Role}", role);
                    }
                    else
                    {
                        logger.LogWarning("Failed to seed role {Role}: {Errors}", role, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                    }
                }
            }

            // 3. Seed Default Audio Admin
            string adminEmail = configuration["Audio:DefaultAdmin:Email"] ?? "admin@audio.local";
            string adminPassword = configuration["Audio:DefaultAdmin:Password"] ?? "AdminAudio123!";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };
                var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Administrator");
                    logger.LogInformation("Audio Default Admin created: {Email}", adminEmail);
                }
                else
                {
                    logger.LogWarning("Failed to create Audio Admin {Email}: {Errors}", adminEmail, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                if (!adminUser.EmailConfirmed)
                {
                    adminUser.EmailConfirmed = true;
                    await userManager.UpdateAsync(adminUser);
                }
                if (!await userManager.IsInRoleAsync(adminUser, "Administrator"))
                {
                    await userManager.AddToRoleAsync(adminUser, "Administrator");
                }
            }

            // 4. Ensure test@gmail.com is confirmed and assigned Administrator
            var testUser = await userManager.FindByEmailAsync("test@gmail.com");
            if (testUser != null)
            {
                if (!testUser.EmailConfirmed)
                {
                    testUser.EmailConfirmed = true;
                    await userManager.UpdateAsync(testUser);
                }
                if (!await userManager.IsInRoleAsync(testUser, "Administrator"))
                {
                    await userManager.AddToRoleAsync(testUser, "Administrator");
                }
                var hasPassword = await userManager.HasPasswordAsync(testUser);
                if (hasPassword)
                {
                    var checkPass = await userManager.CheckPasswordAsync(testUser, "Admin123!");
                    if (!checkPass)
                    {
                        var token = await userManager.GeneratePasswordResetTokenAsync(testUser);
                        await userManager.ResetPasswordAsync(testUser, token, "Admin123!");
                        logger.LogInformation("Password for test@gmail.com synchronized to Admin123!");
                    }
                }
            }

            // 5. Seed Operator User
            string opEmail = configuration["Audio:DefaultOperator:Email"] ?? "operator@audio.local";
            string opPassword = configuration["Audio:DefaultOperator:Password"] ?? "Operator123!";
            var opUser = await userManager.FindByEmailAsync(opEmail);
            if (opUser == null)
            {
                opUser = new ApplicationUser
                {
                    UserName = opEmail,
                    Email = opEmail,
                    EmailConfirmed = true
                };
                var createResult = await userManager.CreateAsync(opUser, opPassword);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(opUser, "Operator");
                    logger.LogInformation("Audio Default Operator created: {Email}", opEmail);
                }
            }

            // 6. Seed dbo.Users (PROMOSYS compatible)
            try
            {
                if (!await scaffoldedDb.Users.AnyAsync(u => u.Username == "admin" || u.Username == adminEmail))
                {
                    scaffoldedDb.Users.Add(new User
                    {
                        Username = "admin",
                        FullName = "Administrator Audio",
                        RoleId = 1,
                        IsActive = true,
                        Theme = "light",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (!await scaffoldedDb.Users.AnyAsync(u => u.Username == "operator" || u.Username == opEmail))
                {
                    scaffoldedDb.Users.Add(new User
                    {
                        Username = "operator",
                        FullName = "Operator Audio",
                        RoleId = 2,
                        IsActive = true,
                        Theme = "light",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(opPassword),
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (!await scaffoldedDb.Users.AnyAsync(u => u.Username == "manajemen"))
                {
                    scaffoldedDb.Users.Add(new User
                    {
                        Username = "manajemen",
                        FullName = "Manajemen Audio",
                        RoleId = 3,
                        IsActive = true,
                        Theme = "light",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Manajemen123!"),
                        CreatedAt = DateTime.UtcNow
                    });
                }

                await scaffoldedDb.SaveChangesAsync();
                logger.LogInformation("dbo.Users seeded with default admin, operator, and manajemen.");
            }
            catch (Exception ex)
            {
                logger.LogWarning("dbo.Users seeding notice: {Message}", ex.Message);
            }

            // 7. Seed Employee and pengguna if empty
            try
            {
                if (!await scaffoldedDb.Employees.AnyAsync())
                {
                    scaffoldedDb.Employees.AddRange(
                        new Employee { EmployeeName = "Lead Audio", Division = "Lead" },
                        new Employee { EmployeeName = "PIC Final Assembly", Division = "PIC" }
                    );
                    await scaffoldedDb.SaveChangesAsync();
                }

                if (!await scaffoldedDb.Penggunas.AnyAsync())
                {
                    scaffoldedDb.Penggunas.AddRange(
                        new Pengguna { Username = "Administrator", Password = "administrator" },
                        new Pengguna { Username = "Operator", Password = "operator" }
                    );
                    await scaffoldedDb.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Employee/Pengguna seeding notice: {Message}", ex.Message);
            }
        }
    }
}
