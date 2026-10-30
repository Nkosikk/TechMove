using System.ComponentModel.DataAnnotations;

namespace TechMove.Contracts;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
}

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
    public string OwnerName { get; set; } = string.Empty;
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
    public string OwnerName { get; set; } = string.Empty;
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovalNote { get; set; }
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
    public string OwnerName { get; set; } = string.Empty;
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? ApprovalNote { get; set; }
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
public sealed record ApprovalRequest([property: StringLength(500)] string? Note);
public sealed record LoginRequest(string Username, string Password);
public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string DisplayName,
    IReadOnlyCollection<string> Roles);
public sealed record RegistrationResponse(string UserId, string Email, string Message);
public sealed record ConfirmEmailRequest(string UserId, string Token);

public sealed class PendingUserDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime RegisteredAtUtc { get; set; }
}

public sealed class ManagedUserDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public DateTime RegisteredAtUtc { get; set; }
}

public sealed class RegisterRequest
{
    [Required, StringLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password)), DataType(DataType.Password), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class DashboardDto
{
    public int ClientCount { get; set; }
    public int ContractCount { get; set; }
    public int PendingContractCount { get; set; }
    public int ServiceRequestCount { get; set; }
    public int PendingServiceRequestCount { get; set; }
    public int UserCount { get; set; }
    public int PendingUserCount { get; set; }
    public IReadOnlyCollection<ContractDto> PendingContracts { get; set; } = [];
    public IReadOnlyCollection<ServiceRequestDto> PendingServiceRequests { get; set; } = [];
    public IReadOnlyCollection<PendingUserDto> PendingUsers { get; set; } = [];
    public IReadOnlyCollection<ManagedUserDto> Users { get; set; } = [];
}
