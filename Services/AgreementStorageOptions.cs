namespace TechMove.Services;

public class AgreementStorageOptions
{
    public const string SectionName = "AgreementStorage";

    public string Path { get; set; } = "App_Data/Agreements";
    public long MaximumFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}
