using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TechMove.Models;

public class ServiceRequest
{
    public int Id { get; set; }

    [Required, Display(Name = "Contract")]
    public int ContractId { get; set; }

    [Required, StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)"), Range(0.01, 999999999.99), Display(Name = "Cost (USD)")]
    public decimal CostUsd { get; set; }

    [Column(TypeName = "decimal(18,2)"), Display(Name = "Local cost (ZAR)")]
    public decimal LocalCostZar { get; set; }

    [Column(TypeName = "decimal(18,6)"), Display(Name = "USD/ZAR rate")]
    public decimal ExchangeRate { get; set; }

    public ServiceRequestStatus Status { get; set; }

    [Display(Name = "Created at")]
    public DateTime CreatedAtUtc { get; set; }

    public Contract? Contract { get; set; }
}

public enum ServiceRequestStatus
{
    New,
    InProgress,
    Completed,
    Cancelled
}
