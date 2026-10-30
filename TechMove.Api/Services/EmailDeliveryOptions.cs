using System.ComponentModel.DataAnnotations;

namespace TechMove.Api.Services;

public sealed class EmailDeliveryOptions
{
    public const string SectionName = "EmailDelivery";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string? Username { get; set; }
    public string? Password { get; set; }

    [Required, EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    [Required]
    public string FromName { get; set; } = "TechMove";

    [Required, Url]
    public string PublicAppBaseUrl { get; set; } = string.Empty;
}
