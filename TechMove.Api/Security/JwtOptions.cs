namespace TechMove.Api.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "TechMove.Api";
    public string Audience { get; set; } = "TechMove.Clients";
    public string Key { get; set; } = string.Empty;
    public int LifetimeMinutes { get; set; } = 60;
    public string ServiceUsername { get; set; } = string.Empty;
    public string ServicePassword { get; set; } = string.Empty;
}
