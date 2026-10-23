using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MonitoringSystem.Models;

[Table("Users")]
public partial class User
{
    [Key]
    public int UserId { get; set; }

    [Required]
    public string Username { get; set; } = null!;

    [Required]
    public string PasswordHash { get; set; } = null!;

    [Required]
    public string FullName { get; set; } = null!;

    public int RoleId { get; set; }

    public bool IsActive { get; set; } = true;

    public int? LastPeriodId { get; set; }

    public string Theme { get; set; } = "light";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("RoleId")]
    public virtual Role? Role { get; set; }
}
