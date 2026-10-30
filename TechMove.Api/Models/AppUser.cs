using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace TechMove.Api.Models;

public sealed class AppUser : IdentityUser
{
    [Required, StringLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public bool IsApproved { get; set; }
    public DateTime RegisteredAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovedById { get; set; }
}
