using Microsoft.AspNetCore.Http;

namespace TechMove.Services;

public class AgreementFileValidator(AgreementStorageOptions options)
{
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    public async Task ValidateAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
        {
            throw new InvalidDataException("The agreement file is empty.");
        }

        if (file.Length > options.MaximumFileSizeBytes)
        {
            throw new InvalidDataException("The agreement file exceeds the permitted size.");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Only PDF agreement files are allowed.");
        }

        await using var stream = file.OpenReadStream();
        var signature = new byte[PdfSignature.Length];
        var bytesRead = await stream.ReadAsync(signature, cancellationToken);
        if (bytesRead != PdfSignature.Length || !signature.SequenceEqual(PdfSignature))
        {
            throw new InvalidDataException("The uploaded file is not a valid PDF document.");
        }
    }
}
