using System.Text;
using Microsoft.AspNetCore.Http;
using TechMove.Models;
using TechMove.Services;

namespace TechMove.Tests;

public class CurrencyServiceTests
{
    [Fact]
    public void CalculateZar_WithKnownRate_ReturnsRoundedLocalCost()
    {
        var service = new CurrencyService(new HttpClient());

        var result = service.CalculateZar(125.50m, 18.625m);

        Assert.Equal(2337.44m, result);
    }

    [Fact]
    public void CalculateZar_WithInvalidRate_Throws()
    {
        var service = new CurrencyService(new HttpClient());

        Assert.Throws<ArgumentOutOfRangeException>(() => service.CalculateZar(100m, 0m));
    }
}

public class AgreementFileValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WithExecutableFile_ThrowsInvalidDataException()
    {
        var content = new MemoryStream(Encoding.UTF8.GetBytes("not an agreement"));
        var file = new FormFile(content, 0, content.Length, "AgreementFile", "malware.exe");
        var validator = new AgreementFileValidator(new AgreementStorageOptions());

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => validator.ValidateAsync(file));

        Assert.Contains("Only PDF", exception.Message);
    }

    [Fact]
    public async Task ValidateAsync_WithPdfSignature_AcceptsFile()
    {
        var content = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7 test content"));
        var file = new FormFile(content, 0, content.Length, "AgreementFile", "agreement.pdf");
        var validator = new AgreementFileValidator(new AgreementStorageOptions());

        await validator.ValidateAsync(file);
    }
}

public class ContractWorkflowServiceTests
{
    [Theory]
    [InlineData(ContractStatus.Expired)]
    [InlineData(ContractStatus.OnHold)]
    public void EnsureServiceRequestAllowed_WithBlockedContract_Throws(ContractStatus status)
    {
        var service = new ContractWorkflowService();
        var contract = new Contract { Status = status };

        Assert.Throws<InvalidOperationException>(() => service.EnsureServiceRequestAllowed(contract));
    }

    [Theory]
    [InlineData(ContractStatus.Draft)]
    [InlineData(ContractStatus.Active)]
    public void CanCreateServiceRequest_WithAllowedContract_ReturnsTrue(ContractStatus status)
    {
        var service = new ContractWorkflowService();
        var contract = new Contract { Status = status };

        Assert.True(service.CanCreateServiceRequest(contract));
    }
}
