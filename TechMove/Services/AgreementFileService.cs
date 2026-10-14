using Microsoft.AspNetCore.Http;

namespace TechMove.Services;

public class AgreementFileService(
    IWebHostEnvironment environment,
    AgreementStorageOptions options,
    AgreementFileValidator validator)
{
    private string StoragePath => Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Path));

    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAsync(file, cancellationToken);
        Directory.CreateDirectory(StoragePath);

        var storedFileName = $"{Guid.NewGuid():N}.pdf";
        var destinationPath = Path.Combine(StoragePath, storedFileName);
        await using var destination = File.Create(destinationPath);
        await file.CopyToAsync(destination, cancellationToken);
        return storedFileName;
    }

    public FileStream OpenRead(string storedFileName)
    {
        if (Path.GetFileName(storedFileName) != storedFileName)
        {
            throw new InvalidDataException("The stored agreement filename is invalid.");
        }

        var filePath = Path.Combine(StoragePath, storedFileName);
        return File.OpenRead(filePath);
    }

    public void Delete(string? storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName) || Path.GetFileName(storedFileName) != storedFileName)
        {
            return;
        }

        var filePath = Path.Combine(StoragePath, storedFileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
