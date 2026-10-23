#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonitoringSystem.Data;
using MonitoringSystem.Models;

namespace MonitoringSystem.Areas.Identity.Pages.Account
{
    // Opsi D: Halaman ini hanya dapat diakses oleh Administrator yang sedang login.
    [Authorize(Roles = "Administrator,Admin")]
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ScaffoldedDbContext _scaffoldedDb;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ScaffoldedDbContext scaffoldedDb,
            ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _scaffoldedDb = scaffoldedDb;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public List<Role> AvailableRoles { get; set; } = new();

        public List<UserViewModel> ExistingUsers { get; set; } = new();

        public class UserViewModel
        {
            public int UserId { get; set; }
            public string Username { get; set; }
            public string FullName { get; set; }
            public int RoleId { get; set; }
            public string RoleName { get; set; }
            public bool IsActive { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        public class InputModel
        {
            [Required(ErrorMessage = "Nama Lengkap wajib diisi.")]
            [StringLength(200, ErrorMessage = "Nama Lengkap maksimal 200 karakter.")]
            [Display(Name = "Nama Lengkap")]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Username wajib diisi.")]
            [StringLength(50, MinimumLength = 3, ErrorMessage = "Username minimal 3 dan maksimal 50 karakter.")]
            [RegularExpression(@"^[a-zA-Z0-9_.-]+$", ErrorMessage = "Username hanya boleh huruf, angka, titik, minus, dan underscore.")]
            [Display(Name = "Username")]
            public string Username { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email wajib diisi.")]
            [EmailAddress(ErrorMessage = "Format Email tidak valid.")]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Pilihan Role wajib dipilih.")]
            [Display(Name = "Role")]
            public int RoleId { get; set; } = 2; // Default to User

            [Required(ErrorMessage = "Password wajib diisi.")]
            [StringLength(100, ErrorMessage = "Password minimal {2} karakter.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Konfirmasi Password")]
            [Compare("Password", ErrorMessage = "Password dan Konfirmasi Password tidak cocok.")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            await LoadDataAsync();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;

            if (ModelState.IsValid)
            {
                var trimmedUsername = Input.Username.Trim();
                var trimmedEmail = Input.Email.Trim();
                var trimmedFullName = Input.FullName.Trim();

                // 1. Cek duplikasi di dbo.Users
                var existingCustomUser = await _scaffoldedDb.Users
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == trimmedUsername.ToLower());
                if (existingCustomUser != null)
                {
                    ModelState.AddModelError("Input.Username", $"Username '{trimmedUsername}' sudah digunakan.");
                }

                // 2. Cek duplikasi di AspNetUsers
                var existingIdentityByEmail = await _userManager.FindByEmailAsync(trimmedEmail);
                if (existingIdentityByEmail != null)
                {
                    ModelState.AddModelError("Input.Email", $"Email '{trimmedEmail}' sudah terdaftar.");
                }

                var existingIdentityByName = await _userManager.FindByNameAsync(trimmedUsername);
                if (existingIdentityByName != null)
                {
                    ModelState.AddModelError("Input.Username", $"Username '{trimmedUsername}' sudah terdaftar di Identity.");
                }

                if (ModelState.IsValid)
                {
                    // 3. Buat di AspNetUsers (Identity)
                    var identityUser = new ApplicationUser
                    {
                        UserName = trimmedUsername,
                        Email = trimmedEmail,
                        EmailConfirmed = true
                    };

                    var createResult = await _userManager.CreateAsync(identityUser, Input.Password);
                    if (createResult.Succeeded)
                    {
                        // 4. Assign Identity Role
                        string targetIdentityRole = Input.RoleId switch
                        {
                            1 => "Administrator",
                            3 => "Supervisor",
                            _ => "User"
                        };

                        if (!await _roleManager.RoleExistsAsync(targetIdentityRole))
                        {
                            await _roleManager.CreateAsync(new IdentityRole(targetIdentityRole));
                        }
                        await _userManager.AddToRoleAsync(identityUser, targetIdentityRole);

                        // 5. Simpan di dbo.Users (Kompatibel dengan PROMOSYS)
                        string bcryptHash = BCrypt.Net.BCrypt.HashPassword(Input.Password);
                        var customUser = new User
                        {
                            Username = trimmedUsername,
                            FullName = trimmedFullName,
                            RoleId = Input.RoleId,
                            IsActive = true,
                            Theme = "light",
                            PasswordHash = bcryptHash,
                            CreatedAt = DateTime.UtcNow
                        };

                        _scaffoldedDb.Users.Add(customUser);
                        await _scaffoldedDb.SaveChangesAsync();

                        _logger.LogInformation("Admin created new user: {Username} (Role: {RoleId})", trimmedUsername, Input.RoleId);
                        StatusMessage = $"Pengguna '{trimmedUsername}' ({trimmedFullName}) berhasil dibuat dengan role '{targetIdentityRole}'.";

                        // Admin tetap login sebagai Admin, halaman di-refresh
                        return RedirectToPage();
                    }

                    foreach (var error in createResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }

            await LoadDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostToggleStatusAsync(int id)
        {
            var user = await _scaffoldedDb.Users.FindAsync(id);
            if (user != null)
            {
                if (user.Username.ToLower() == "admin")
                {
                    ErrorMessage = "Akun Administrator utama tidak boleh dinonaktifkan.";
                    return RedirectToPage();
                }

                user.IsActive = !user.IsActive;
                await _scaffoldedDb.SaveChangesAsync();
                StatusMessage = $"Status akun '{user.Username}' berhasil diubah menjadi {(user.IsActive ? "Aktif" : "Non-aktif")}.";
            }
            return RedirectToPage();
        }

        private async Task LoadDataAsync()
        {
            AvailableRoles = await _scaffoldedDb.Roles.OrderBy(r => r.RoleId).ToListAsync();
            if (!AvailableRoles.Any())
            {
                AvailableRoles = new List<Role>
                {
                    new Role { RoleId = 1, RoleName = "Admin" },
                    new Role { RoleId = 2, RoleName = "User" },
                    new Role { RoleId = 3, RoleName = "Manajemen" }
                };
            }

            var usersFromDb = await _scaffoldedDb.Users
                .Include(u => u.Role)
                .OrderBy(u => u.UserId)
                .ToListAsync();

            ExistingUsers = usersFromDb.Select(u => new UserViewModel
            {
                UserId = u.UserId,
                Username = u.Username,
                FullName = u.FullName,
                RoleId = u.RoleId,
                RoleName = u.Role != null ? u.Role.RoleName : (u.RoleId == 1 ? "Admin" : (u.RoleId == 3 ? "Manajemen" : "User")),
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            }).ToList();
        }
    }
}
