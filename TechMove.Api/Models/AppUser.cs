using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace TechMove.Api.Models;

public sealed class AppUser : IdentityUser
{
    [Required, StringLength(100)]
    public string DisplayName { get; set; } = string.Empty;
}
