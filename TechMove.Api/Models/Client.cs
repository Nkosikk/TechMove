using System.ComponentModel.DataAnnotations;

namespace TechMove.Api.Models;

public class Client
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(250), Display(Name = "Contact details")]
    public string ContactDetails { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Region { get; set; } = string.Empty;

    public string? OwnerId { get; set; }
    public AppUser? Owner { get; set; }

    public ICollection<Contract> Contracts { get; set; } = [];
}

