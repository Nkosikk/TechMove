namespace TechMove.Api.Security;

public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
