using System.ComponentModel.DataAnnotations;
using TechMove.Contracts;

namespace TechMove.Api.Models;

public class Contract
{
    public int Id { get; set; }

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

    [StringLength(260)]
    public string? AgreementStoredFileName { get; set; }

    [StringLength(260)]
    public string? AgreementOriginalFileName { get; set; }

    public string? OwnerId { get; set; }
    public AppUser? Owner { get; set; }

    public string? ApprovedById { get; set; }
    public AppUser? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    [StringLength(500)]
    public string? ApprovalNote { get; set; }

    public Client? Client { get; set; }

    public ICollection<ServiceRequest> ServiceRequests { get; set; } = [];
}

