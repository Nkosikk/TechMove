namespace TechMove.Api.Services;

public sealed record ValidatedProfilePicture(byte[] Content, string ContentType);

public sealed class ProfilePictureValidator
{
    public const long MaximumFileSizeBytes = 1024 * 1024;

    private static readonly byte[] JpegSignature = [0xff, 0xd8, 0xff];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebpSignature = "WEBP"u8.ToArray();

    public async Task<ValidatedProfilePicture> ReadAndValidateAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
        {
            throw new InvalidDataException("The profile picture is empty.");
        }
        if (file.Length > MaximumFileSizeBytes)
        {
            throw new InvalidDataException("The profile picture must be 1 MB or smaller.");
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream((int)file.Length);
        await stream.CopyToAsync(buffer, cancellationToken);
        var content = buffer.ToArray();
        var contentType = DetectContentType(content)
            ?? throw new InvalidDataException("Only valid JPEG, PNG, or WebP profile pictures are allowed.");
        return new ValidatedProfilePicture(content, contentType);
    }

    private static string? DetectContentType(byte[] content)
    {
        if (content.AsSpan().StartsWith(JpegSignature))
        {
            return "image/jpeg";
        }
        if (content.AsSpan().StartsWith(PngSignature))
        {
            return "image/png";
        }
        if (content.Length >= 12
            && content.AsSpan(0, 4).SequenceEqual(RiffSignature)
            && content.AsSpan(8, 4).SequenceEqual(WebpSignature))
        {
            return "image/webp";
        }
        return null;
    }
}
