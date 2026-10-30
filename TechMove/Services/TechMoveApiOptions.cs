namespace TechMove.Services;

public sealed class TechMoveApiOptions
{
    public const string SectionName = "TechMoveApi";

    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
