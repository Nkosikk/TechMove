using System.ComponentModel.DataAnnotations;

namespace TechMove.Contracts;

public enum ContractStatus
{
    Draft,
    Active,
    Expired,
    OnHold
}

public enum ServiceRequestStatus
{
    New,
    InProgress,
    Completed,
    Cancelled
}

public sealed class ClientDto
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(250), Display(Name = "Contact details")]
    public string ContactDetails { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Region { get; set; } = string.Empty;

    public int ContractCount { get; set; }
}

public sealed class ContractDto
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public ContractStatus Status { get; set; }
    public string ServiceLevel { get; set; } = string.Empty;
    public bool HasAgreement { get; set; }
    public string? AgreementOriginalFileName { get; set; }
}

public sealed class ContractUpsertRequest
{
    [Required, Display(Name = "Client")]
    public int ClientId { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [DataType(DataType.Date), Display(Name = "Start date")]
    public DateTime StartDate { get; set; }

    [DataType(DataType.Date), Display(Name = "End date")]
    public DateTime EndDate { get; set; }

    public ContractStatus Status { get; set; }

    [Required, StringLength(100), Display(Name = "Service level")]
    public string ServiceLevel { get; set; } = string.Empty;
}

public sealed class ServiceRequestDto
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string ContractTitle { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal CostUsd { get; set; }
    public decimal LocalCostZar { get; set; }
    public decimal ExchangeRate { get; set; }
    public ServiceRequestStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class CreateServiceRequestRequest
{
    [Required, Display(Name = "Contract")]
    public int ContractId { get; set; }

    [Required, StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 999999999.99), Display(Name = "Cost (USD)")]
    public decimal CostUsd { get; set; }
}

public sealed record UpdateContractStatusRequest(ContractStatus Status);
public sealed record UpdateServiceRequestStatusRequest(ServiceRequestStatus Status);
public sealed record LoginRequest(string Username, string Password);
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc);
